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

public record UserDto(int Id, string Name, string Email, string[] Roles);

public record LoginResponse(string Message, string AccessToken, string TokenType, UserDto User);

public record AssignRolesRequest([Required] List<string> Roles);

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
    string? Type);

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
