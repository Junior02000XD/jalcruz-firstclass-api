using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace JalcruzFirstClass.Api.Services;

/// <summary>Configuración de Cloudflare R2, leída del entorno igual que Jwt.</summary>
public class R2Options
{
    public string Endpoint { get; init; } = null!;       // https://<account_id>.r2.cloudflarestorage.com
    public string AccessKeyId { get; init; } = null!;
    public string SecretAccessKey { get; init; } = null!;
    public string Bucket { get; init; } = null!;
    public string PublicBaseUrl { get; init; } = null!;  // https://media.cruzk.dev
}

/// <summary>
/// Subida y borrado de archivos en R2.
///
/// El bucket es público detrás de un dominio propio: Meta descarga el archivo
/// desde sus servidores cuando la IA lo manda por WhatsApp, así que la URL tiene
/// que ser estable y alcanzable sin credenciales. Se descartaron las URLs
/// prefirmadas porque cambian en cada generación y romperían la respuesta
/// byte a byte de /api/agent-context (ver ese controller).
///
/// A cambio, la clave lleva un sufijo aleatorio: el contenido es público para
/// quien tenga el enlace, pero no se puede adivinar ni enumerar.
/// </summary>
public class R2StorageService(R2Options options)
{
    private readonly AmazonS3Client _client = new(
        options.AccessKeyId,
        options.SecretAccessKey,
        new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            // R2 no tiene regiones; el SDK exige una y "auto" es la que documenta Cloudflare.
            AuthenticationRegion = "auto",
            // Sin esto el SDK arma https://<bucket>.<endpoint>, que en R2 no resuelve.
            ForcePathStyle = true,
            // El SDK calcula un checksum en cada PUT y lo manda en un *trailer* con
            // codificación aws-chunked. R2 responde
            // "STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER not implemented" y la subida
            // falla. Con WhenRequired sólo lo agrega si la operación lo exige —para
            // PutObject no— y la firma vuelve a ser la del payload completo, que R2 sí
            // entiende. Verificado contra el bucket real: MinIO no sirve para detectar
            // esto porque sí implementa el trailer.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });

    public async Task<(string Url, string ObjectKey)> UploadAsync(
        Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var key = BuildKey(fileName);

        // El stream tiene que poder rebobinarse: con uno seekable el SDK firma el
        // hash del contenido completo, que es lo que R2 acepta. Si no lo fuera,
        // usaría la firma por chunks (STREAMING-AWS4-HMAC-SHA256-PAYLOAD), que R2
        // rechaza. La alternativa —DisablePayloadSigning— obliga a HTTPS y deja
        // el servicio imposible de probar contra un S3 local.
        var payload = content;
        MemoryStream? buffered = null;
        if (!content.CanSeek)
        {
            buffered = new MemoryStream();
            await content.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            payload = buffered;
        }

        try
        {
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = options.Bucket,
                Key = key,
                InputStream = payload,
                ContentType = contentType,
                // Sin esto el SDK manda el cuerpo troceado y firmado por chunks, y R2
                // contesta "STREAMING-AWS4-HMAC-SHA256-PAYLOAD not implemented". En
                // false firma el hash del contenido completo —de ahí que el stream
                // tenga que ser seekable— que es la única forma que R2 acepta.
                UseChunkEncoding = false,
            }, ct);
        }
        finally
        {
            if (buffered is not null) await buffered.DisposeAsync();
        }

        return ($"{options.PublicBaseUrl.TrimEnd('/')}/{key}", key);
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct = default)
        => _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = options.Bucket,
            Key = objectKey,
        }, ct);

    /// <summary>
    /// Clave con fecha (para poder mirar el bucket y entender qué es de cuándo),
    /// sufijo aleatorio (para que no se adivine) y el nombre original saneado
    /// (para reconocerlo desde el panel de Cloudflare).
    /// </summary>
    private static string BuildKey(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

        var safe = new string(stem.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-');
        if (safe.Length > 40) safe = safe[..40];
        if (safe.Length == 0) safe = "archivo";

        var suffix = Guid.NewGuid().ToString("N")[..10];
        return $"{DateTime.UtcNow:yyyy/MM}/{safe}-{suffix}{extension}";
    }
}
