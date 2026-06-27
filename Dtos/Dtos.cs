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

public record TrialClassInput(
    [Required] int ProspectId,
    int? TeacherId,
    [Required] DateTime Schedule,
    bool? AttendanceBool,
    string? Status,
    int? ReprogrammedFromId);
