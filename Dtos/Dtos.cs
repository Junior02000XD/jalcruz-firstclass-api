using System.ComponentModel.DataAnnotations;

namespace JalcruzFirstClass.Api.Dtos;

// ───────────── Auth ─────────────

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RegisterRequest(
    [Required] string Name,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

/// <summary>
/// `IsServiceAccount` viaja al frontend para que la pantalla de usuarios pueda
/// distinguir a los bots de las personas: una cuenta de servicio no inicia
/// sesión, así que ofrecerle "cambiar contraseña" o esperar que entre al panel
/// no tendría sentido.
/// </summary>
public record UserDto(int Id, string Name, string Email, string[] Roles, bool IsServiceAccount);

public record LoginResponse(string Message, string AccessToken, string TokenType, UserDto User);

public record AssignRolesRequest([Required] List<string> Roles);

/// <summary>
/// Cuerpo opcional de /api/service-token. Con una sola cuenta de servicio no hace
/// falta mandar nada; el email sólo es necesario si algún día hay varias.
/// </summary>
public record ServiceTokenRequest(int? Id, string? Email);

/// <summary>
/// Cuerpo para devolver una cuenta de servicio al estado de persona. Exige una
/// contraseña nueva porque al convertirla se destruyó la anterior.
/// </summary>
public record RevertirCuentaDeServicioRequest(
    [Required, MinLength(8)] string NewPassword);

// ───────────── Núcleo compartido ─────────────

public record CityInput([Required] string Name);

public record PhoneInput(string Number, string? Label);

public record PersonInput(
    int? CityId,
    [Required] string FirstName,
    [Required] string LastName,
    string? Ci,
    string? CiComplement,
    string? Email,
    DateOnly? BirthDate);

public record ZoneInput([Required] string Name);

// ───────────── Módulo Jalcruz (RRHH) ─────────────

public record CompanyInput([Required] string Name, string? BusinessName, string? Nit);

public record WorkAreaInput(
    [Required] int CompanyId,
    [Required] string Name,
    string? Location,
    [Required] int CityId,
    int? InChargeId);

public record WorkerDetailInput([Required] int PersonId, string? Reliability);

public record PayrollInput(
    [Required] int WorkAreaId,
    [Required] DateOnly StartDate,
    [Required] DateOnly EndDate,
    [Required] string Code);

public record AttendanceInput(
    [Required] int PayrollId,
    [Required] int PersonId,
    [Required] DateOnly Date,
    string? Status,
    decimal Amount,
    decimal? ExtraAmount,
    bool? DidEat,
    bool? IsPaid);

// ───────────── Módulo First Class (CRM) ─────────────

public record TeacherInput([Required] string Name, string? Specialty);

public record ProductInput([Required] string Name, decimal? Price);

public record CampaignInput(
    [Required] string Name,
    DateOnly? ExecutionDate,
    string? Description,
    string? Url,
    decimal? Budget,
    string? Type,
    // Id del anuncio de Meta (referral.source_id). Es lo que ata un prospecto
    // llegado por click-to-WhatsApp a su campaña.
    string? AdId);

public record ProspectInput(
    [Required] int PersonId,
    int? CampaignId,
    int? EntityId,
    int? ZoneId,
    string? Origin,
    string? Address,
    string? Notes,
    string? Status);

// Alta rápida de prospecto desde móvil: crea Persona (+ teléfono) y Prospecto en una sola llamada.
public record ProspectQuickInput(
    [Required] string FirstName,
    string? LastName,
    string? Phone,
    int? CampaignId,
    int? ZoneId,
    string? Origin,
    string? Address,
    string? Notes,
    string? Status);

// Cambio de estado sin el riesgo del PUT completo, que pisa con null los campos
// que el payload no traiga. Lo usa el agente de n8n tras cada conversación.
public record ProspectStatusPatchInput([Required] string Status);

// Hand-off: asignar el prospecto a un humano o devolvérselo a la IA.
// Va aparte del PATCH de estado a propósito: al ser el único campo del cuerpo,
// mandar null significa "limpiar" sin ambigüedad con "no lo mandé".
public record ProspectAssignmentPatchInput(int? AssignedToUserId);

public record ReminderInput(
    [Required] int ProspectId,
    [Required] string Note,
    [Required] DateTime RemindAt,
    bool? IsDone);

// Alta de mensaje del historial de WhatsApp. Idempotente por WhatsappMessageId.
public record MessageInput(
    [Required] int ProspectId,
    [Required] string Direction,
    string? Origin,
    string? Content,
    int? MediaAssetId,
    string? WhatsappMediaUrl,
    string? WhatsappMessageId);

// ───────────── Fase B: contenido de negocio para el agente ─────────────

// Sólo los metadatos: el archivo en sí se sube por multipart y no se reemplaza
// (para cambiarlo se sube otro y se borra el viejo).
public record MediaAssetInput([Required] string Label, string? Transcript);

public record ContextEntryInput(
    [Required] string Type,
    [Required] string Title,
    [Required] string Content,
    bool? Active,
    // Sólo promoción
    DateOnly? ValidUntil,
    // Zonas a las que se limita. Null = no la toques (para un PUT que sólo edita
    // el texto); lista vacía = quitar la restricción, o sea vale para todas.
    List<int>? RestrictedZoneIds,
    string? ConditionsText,
    // Sólo flujo
    string? NextAction,
    int? HandoffToUserId,
    // Archivos asociados; la lista reemplaza a la anterior por completo
    List<int>? MediaAssetIds);

public record PersonaInput(
    [Required] int UserId,
    [Required] string PhoneNumberId,
    [Required] string StyleGuide,
    bool? Active);

// ── Respuesta de GET /api/agent-context/{phoneNumberId} ──
// DTOs propios y no las entidades: el agente necesita una forma estable y sin
// timestamps, porque el mismo contenido tiene que producir la MISMA respuesta
// byte a byte entre llamadas (requisito del prompt caching de Claude).

public record AgentZoneDto(int Id, string Name);

public record AgentMediaDto(int Id, string Type, string UrlR2, string Label, string? Transcript);

public record AgentContextEntryDto(
    int Id,
    string Type,
    string Title,
    string Content,
    DateOnly? ValidUntil,
    // Vacía = la promoción vale para todas las zonas.
    IReadOnlyList<AgentZoneDto> RestrictedZones,
    string? ConditionsText,
    string? NextAction,
    int? HandoffToUserId,
    string? HandoffToUserName,
    IReadOnlyList<AgentMediaDto> Media);

public record AgentPersonaDto(int Id, string PhoneNumberId, string StyleGuide, int UserId, string UserName);

// Las zonas van completas y no sólo las que alguna promoción restringe: el
// agente necesita el catálogo entero para poder registrar de qué zona es el
// prospecto cuando se lo dice. Con la lista derivada de las fichas, una zona
// sin promoción propia era invisible y ese dato se perdía.
public record AgentContextResponse(
    AgentPersonaDto Persona,
    IReadOnlyList<AgentZoneDto> Zones,
    IReadOnlyList<AgentContextEntryDto> Entries);

public record EnrollmentInput(
    [Required] int ProspectId,
    [Required] int ProductId,
    [Required] DateOnly EnrollmentDate,
    string? ReceiptNumber,
    decimal? Commission);

public record TrialClassInput(
    [Required] int ProspectId,
    int? TeacherId,
    [Required] DateTime Schedule,
    bool? AttendanceBool,
    string? Status,
    int? ReprogrammedFromId);
