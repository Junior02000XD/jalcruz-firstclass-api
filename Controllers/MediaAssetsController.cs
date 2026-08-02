using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using JalcruzFirstClass.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

/// <summary>
/// Galería de archivos que la IA puede mandar por WhatsApp. El archivo va a
/// Cloudflare R2; en la base queda su URL pública, su clave y —lo que de verdad
/// usa el agente— la transcripción cargada a mano.
/// </summary>
[ApiController]
[Route("api/media")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class MediaAssetsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Límites de Meta para lo que se puede enviar por la Cloud API. Aceptar algo
    /// más grande sería guardar un archivo que después no se puede mandar.
    /// </summary>
    private const long MaxImageBytes = 5 * 1024 * 1024;    // 5 MB
    private const long MaxAudioBytes = 16 * 1024 * 1024;   // 16 MB

    /// <summary>Formatos que Meta acepta en mensajes de imagen y de audio.</summary>
    private static readonly Dictionary<string, MediaType> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = MediaType.Image,
        ["image/png"] = MediaType.Image,
        ["audio/aac"] = MediaType.Audio,
        ["audio/amr"] = MediaType.Audio,
        ["audio/mpeg"] = MediaType.Audio,
        ["audio/mp4"] = MediaType.Audio,
        ["audio/ogg"] = MediaType.Audio,
    };

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? type)
    {
        var query = db.MediaAssets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!EnumMaps.TryParse(EnumMaps.MediaType, type, out var parsed))
                return BadRequest(new { message = $"Tipo inválido '{type}'.", valid_values = EnumMaps.MediaType.Values });
            query = query.Where(a => a.Type == parsed);
        }

        return Ok(await query.OrderByDescending(a => a.Id).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var asset = await db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>Sube el archivo a R2 y guarda su ficha. multipart/form-data: file, label, transcript.</summary>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Store(
        IFormFile file,
        [FromForm] string label,
        [FromForm] string? transcript,
        CancellationToken ct)
    {
        var storage = HttpContext.RequestServices.GetService<R2StorageService>();
        if (storage is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "El almacenamiento de archivos no está configurado (faltan las variables R2:*).",
            });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No llegó ningún archivo." });

        if (string.IsNullOrWhiteSpace(label))
            return BadRequest(new { message = "La etiqueta es obligatoria: es cómo se reconoce el archivo en la galería." });

        if (!AllowedContentTypes.TryGetValue(file.ContentType ?? "", out var mediaType))
            return BadRequest(new
            {
                message = $"Tipo de archivo no admitido ({file.ContentType}).",
                valid_values = AllowedContentTypes.Keys,
            });

        var maxBytes = mediaType == MediaType.Image ? MaxImageBytes : MaxAudioBytes;
        if (file.Length > maxBytes)
            return BadRequest(new
            {
                message = $"El archivo pesa {file.Length / 1024 / 1024.0:0.0} MB y el máximo para " +
                          $"{EnumMaps.MediaType[mediaType]} es {maxBytes / 1024 / 1024} MB (límite de WhatsApp).",
            });

        await using var stream = file.OpenReadStream();
        var (url, objectKey) = await storage.UploadAsync(stream, file.FileName, file.ContentType!, ct);

        var asset = new MediaAsset
        {
            Type = mediaType,
            UrlR2 = url,
            ObjectKey = objectKey,
            Label = label.Trim(),
            Transcript = string.IsNullOrWhiteSpace(transcript) ? null : transcript.Trim(),
        };

        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Show), new { id = asset.Id }, asset);
    }

    /// <summary>
    /// Edita sólo etiqueta y transcripción. El archivo no se reemplaza: para
    /// cambiarlo se sube otro y se borra este, así ninguna entrada del contexto
    /// queda apuntando a un contenido que dice otra cosa que su transcripción.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MediaAssetInput input)
    {
        var asset = await db.MediaAssets.FindAsync(id);
        if (asset is null) return NotFound();

        asset.Label = input.Label.Trim();
        asset.Transcript = string.IsNullOrWhiteSpace(input.Transcript) ? null : input.Transcript.Trim();
        await db.SaveChangesAsync();
        return Ok(asset);
    }

    /// <summary>
    /// Borra la ficha y el objeto en R2. Si R2 falla, no se borra la fila: es
    /// preferible una ficha viva a un archivo huérfano pagando almacenamiento
    /// que ya nadie sabe que existe.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id, CancellationToken ct)
    {
        var asset = await db.MediaAssets.FindAsync(id);
        if (asset is null) return NotFound();

        var storage = HttpContext.RequestServices.GetService<R2StorageService>();
        if (storage is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "El almacenamiento de archivos no está configurado (faltan las variables R2:*).",
            });

        await storage.DeleteAsync(asset.ObjectKey, ct);

        // Las filas de context_entry_media caen en cascada y los mensajes que lo
        // usaron quedan con media_asset_id en null (el texto se conserva).
        db.MediaAssets.Remove(asset);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
