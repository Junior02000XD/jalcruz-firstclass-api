namespace JalcruzFirstClass.Api.Domain;

// Los valores string coinciden EXACTAMENTE con los del backend Laravel original
// para que la migración de datos desde la BD Postgres existente sea directa.

/// <summary>Confiabilidad de un trabajador (módulo Jalcruz / RRHH).</summary>
public enum Reliability
{
    Excellent,      // "excelente"
    Good,           // "bueno"
    Risk,           // "riesgoso"
    Blacklist       // "no_recomendable"
}

/// <summary>Estado de asistencia diaria en una planilla.</summary>
public enum AttendanceStatus
{
    Present,        // "asistio"
    Absent          // "falto"
}

/// <summary>Tipo de entidad referenciada por un prospecto (universidad, empresa, convenio).</summary>
public enum EntityType
{
    University,     // "universidad"
    Company,        // "empresa"
    Agreement       // "convenio"
}

/// <summary>Estado de un prospecto dentro del embudo de conversión (módulo First Class / CRM).</summary>
public enum ProspectStatus
{
    New,            // "nuevo"
    Contacted,      // "contactado"
    TrialPending,   // "clase_prueba_pendiente"
    Enrolled,       // "inscrito"
    Discarded       // "descartado"
}

/// <summary>Estado de una clase de prueba.</summary>
public enum TrialClassStatus
{
    Scheduled,      // "programada"
    Completed,      // "realizada"
    Cancelled,      // "cancelada"
    Rescheduled     // "reprogramada"
}

/// <summary>Tipo de archivo que la IA puede mandar por WhatsApp.</summary>
public enum MediaType
{
    Image,          // "imagen"
    Audio           // "audio"
}

/// <summary>Clase de contenido de negocio que el agente lee para armar el prompt.</summary>
public enum ContextEntryType
{
    QuestionAnswer, // "pregunta_respuesta"
    Rule,           // "regla"
    Promotion,      // "promocion"
    Flow            // "flujo"
}

/// <summary>Paso siguiente que un flujo del embudo le indica al agente.</summary>
public enum NextAction
{
    SendLevelTest,     // "enviar_test_nivel"
    AskName,           // "pedir_nombre"
    OfferTrialClass,   // "ofrecer_clase_prueba"
    Handoff            // "derivar"
}

/// <summary>Sentido de un mensaje de WhatsApp visto desde el instituto.</summary>
public enum MessageDirection
{
    Inbound,        // "entrante"
    Outbound        // "saliente"
}

/// <summary>Quién produjo el mensaje: el agente de IA o una persona.</summary>
public enum MessageOrigin
{
    Ai,             // "ia"
    Human           // "humano"
}

/// <summary>
/// Conversores entre los enums de C# y los strings persistidos (compatibles con Laravel).
/// Se usan en AppDbContext para configurar las columnas y en los reportes.
/// </summary>
public static class EnumMaps
{
    public static readonly Dictionary<Reliability, string> Reliability = new()
    {
        [Domain.Reliability.Excellent] = "excelente",
        [Domain.Reliability.Good] = "bueno",
        [Domain.Reliability.Risk] = "riesgoso",
        [Domain.Reliability.Blacklist] = "no_recomendable",
    };

    public static readonly Dictionary<AttendanceStatus, string> AttendanceStatus = new()
    {
        [Domain.AttendanceStatus.Present] = "asistio",
        [Domain.AttendanceStatus.Absent] = "falto",
    };

    public static readonly Dictionary<EntityType, string> EntityType = new()
    {
        [Domain.EntityType.University] = "universidad",
        [Domain.EntityType.Company] = "empresa",
        [Domain.EntityType.Agreement] = "convenio",
    };

    public static readonly Dictionary<ProspectStatus, string> ProspectStatus = new()
    {
        [Domain.ProspectStatus.New] = "nuevo",
        [Domain.ProspectStatus.Contacted] = "contactado",
        [Domain.ProspectStatus.TrialPending] = "clase_prueba_pendiente",
        [Domain.ProspectStatus.Enrolled] = "inscrito",
        [Domain.ProspectStatus.Discarded] = "descartado",
    };

    public static readonly Dictionary<TrialClassStatus, string> TrialClassStatus = new()
    {
        [Domain.TrialClassStatus.Scheduled] = "programada",
        [Domain.TrialClassStatus.Completed] = "realizada",
        [Domain.TrialClassStatus.Cancelled] = "cancelada",
        [Domain.TrialClassStatus.Rescheduled] = "reprogramada",
    };

    /// <summary>
    /// Parseo tolerante para los controllers: acepta el valor persistido
    /// ("promocion") o el nombre del enum ("Promotion"). Devuelve false en vez de
    /// un valor por defecto, para que un payload mal escrito termine en 400 y no
    /// en un dato silenciosamente equivocado.
    /// </summary>
    public static bool TryParse<TEnum>(IReadOnlyDictionary<TEnum, string> map, string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        foreach (var (key, text) in map)
        {
            if (string.Equals(text, value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                result = key;
                return true;
            }
        }
        return false;
    }

    public static readonly Dictionary<MediaType, string> MediaType = new()
    {
        [Domain.MediaType.Image] = "imagen",
        [Domain.MediaType.Audio] = "audio",
    };

    public static readonly Dictionary<ContextEntryType, string> ContextEntryType = new()
    {
        [Domain.ContextEntryType.QuestionAnswer] = "pregunta_respuesta",
        [Domain.ContextEntryType.Rule] = "regla",
        [Domain.ContextEntryType.Promotion] = "promocion",
        [Domain.ContextEntryType.Flow] = "flujo",
    };

    public static readonly Dictionary<NextAction, string> NextAction = new()
    {
        [Domain.NextAction.SendLevelTest] = "enviar_test_nivel",
        [Domain.NextAction.AskName] = "pedir_nombre",
        [Domain.NextAction.OfferTrialClass] = "ofrecer_clase_prueba",
        [Domain.NextAction.Handoff] = "derivar",
    };

    public static readonly Dictionary<MessageDirection, string> MessageDirection = new()
    {
        [Domain.MessageDirection.Inbound] = "entrante",
        [Domain.MessageDirection.Outbound] = "saliente",
    };

    public static readonly Dictionary<MessageOrigin, string> MessageOrigin = new()
    {
        [Domain.MessageOrigin.Ai] = "ia",
        [Domain.MessageOrigin.Human] = "humano",
    };
}
