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
