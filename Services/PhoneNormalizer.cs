namespace JalcruzFirstClass.Api.Services;

/// <summary>
/// Normaliza un teléfono a la forma con la que WhatsApp identifica a un contacto:
/// sólo dígitos y con código de país adelante.
///
/// Por qué existe: Meta entrega el número del remitente ya con código de país
/// ("59171234567"), mientras que en el CRM se carga a mano y casi siempre se
/// escribe local ("71234567", "7123-4567"). Sin una forma canónica, el lookup
/// por teléfono de cada mensaje entrante no encontraría al prospecto y n8n
/// crearía uno nuevo en cada conversación.
///
/// Replica EXACTAMENTE la lógica de whatsappLink() en el frontend
/// (src/lib/crm.js): quitar todo lo que no sea dígito y anteponer 591 si quedan
/// 8 dígitos o menos. Las dos implementaciones tienen que coincidir, porque la
/// del frontend arma el enlace wa.me con el que se abre la misma conversación.
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>Código de país de Bolivia; el CRM sólo opera en Santa Cruz.</summary>
    private const string DefaultCountryCode = "591";

    /// <summary>
    /// Devuelve el número canónico, o null si no queda ningún dígito
    /// (campos como "s/n" o vacíos: no sirven como clave de búsqueda).
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0) return null;

        return digits.Length <= 8 ? DefaultCountryCode + digits : digits;
    }
}
