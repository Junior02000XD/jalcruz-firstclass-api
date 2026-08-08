using System.Text.Json.Serialization;

namespace JalcruzFirstClass.Api.Domain;

/// <summary>Base con timestamps de auditoría para todas las entidades.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ───────────────────────── Núcleo compartido ─────────────────────────

public class City : BaseEntity
{
    public string Name { get; set; } = null!;
    [JsonIgnore] public ICollection<Person> People { get; set; } = new List<Person>();
    [JsonIgnore] public ICollection<WorkArea> WorkAreas { get; set; } = new List<WorkArea>();
}

public class Person : BaseEntity
{
    public int? CityId { get; set; }
    public City? City { get; set; }

    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Ci { get; set; }
    public string? CiComplement { get; set; }
    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }

    public ICollection<Phone> Phones { get; set; } = new List<Phone>();
    public WorkerDetail? WorkerDetail { get; set; }
    [JsonIgnore] public ICollection<Prospect> Prospects { get; set; } = new List<Prospect>();
    [JsonIgnore] public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class Phone : BaseEntity
{
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Number { get; set; } = null!;
    public string? Label { get; set; }   // "WhatsApp", "Casa", "Trabajo"

    /// <summary>
    /// Number en forma canónica (sólo dígitos, con código de país) para poder
    /// buscar por el número que manda Meta en cada mensaje entrante. La mantiene
    /// AppDbContext.SaveChanges, no los controllers: así ninguna ruta que cree o
    /// edite un teléfono puede olvidarse de actualizarla. Ver PhoneNormalizer.
    /// </summary>
    public string? NormalizedNumber { get; set; }
}

/// <summary>Universidad / Empresa / Convenio asociable a un prospecto.</summary>
public class Entity : BaseEntity
{
    public string Name { get; set; } = null!;
    public EntityType Type { get; set; }
    [JsonIgnore] public ICollection<Prospect> Prospects { get; set; } = new List<Prospect>();
}

// ───────────────────────── Módulo Jalcruz (RRHH) ─────────────────────────

public class Company : BaseEntity
{
    public string Name { get; set; } = null!;            // Nombre comercial
    public string? BusinessName { get; set; }            // Razón social
    public string? Nit { get; set; }
    public ICollection<WorkArea> WorkAreas { get; set; } = new List<WorkArea>();
}

public class WorkArea : BaseEntity
{
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Location { get; set; }

    public int CityId { get; set; }
    public City City { get; set; } = null!;

    public int? InChargeId { get; set; }                 // Encargado (Person)
    public Person? InCharge { get; set; }

    [JsonIgnore] public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
}

public class WorkerDetail : BaseEntity
{
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public Reliability Reliability { get; set; } = Domain.Reliability.Good;
}

public class Payroll : BaseEntity
{
    public int WorkAreaId { get; set; }
    public WorkArea WorkArea { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Code { get; set; } = null!;            // único, ej. PL-2026-MAY-001

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

public class Attendance : BaseEntity
{
    public int PayrollId { get; set; }
    public Payroll Payroll { get; set; } = null!;

    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public DateOnly Date { get; set; }
    public bool DidEat { get; set; }
    public decimal Amount { get; set; }
    public decimal ExtraAmount { get; set; }
    public bool IsPaid { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
}

// ───────────────────────── Módulo First Class (CRM) ─────────────────────────

public class Product : BaseEntity
{
    public string Name { get; set; } = null!;
    public decimal? Price { get; set; }
    [JsonIgnore] public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

public class Zone : BaseEntity
{
    public string Name { get; set; } = null!;            // Montero, Equipetrol, Plan 3000
    [JsonIgnore] public ICollection<Prospect> Prospects { get; set; } = new List<Prospect>();
    [JsonIgnore] public ICollection<ContextEntryZone> ContextEntries { get; set; } = new List<ContextEntryZone>();
}

public class Teacher : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Specialty { get; set; }
    [JsonIgnore] public ICollection<TrialClass> TrialClasses { get; set; } = new List<TrialClass>();
}

public class Campaign : BaseEntity
{
    public string Name { get; set; } = null!;
    public DateOnly? ExecutionDate { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public decimal? Budget { get; set; }
    public string? Type { get; set; }                    // "Facebook Ads", "TikTok"

    /// <summary>
    /// Id del anuncio de Meta (`referral.source_id` del webhook de WhatsApp).
    ///
    /// Cuando alguien llega por un anuncio click-to-WhatsApp, el mensaje entrante
    /// trae ese id. Sin esta columna no hay forma de saber de qué campaña vino, y
    /// el ROI se pierde justo en los prospectos que costaron dinero.
    ///
    /// Único cuando tiene valor (índice filtrado): dos campañas no pueden
    /// reclamar el mismo anuncio, porque la atribución quedaría a suerte. Varias
    /// campañas sin anuncio conviven sin problema — los NULL no chocan.
    /// </summary>
    public string? AdId { get; set; }

    [JsonIgnore] public ICollection<Prospect> Prospects { get; set; } = new List<Prospect>();
}

public class Prospect : BaseEntity
{
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }

    public int? EntityId { get; set; }
    public Entity? Entity { get; set; }

    public int? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public string? Origin { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public ProspectStatus Status { get; set; } = ProspectStatus.New;

    /// <summary>
    /// Humano que tomó la conversación (hand-off). Null = la atiende el agente de IA.
    /// El agente lo consulta antes de cada respuesta: si tiene valor, no contesta.
    ///
    /// Se serializa SIEMPRE, incluso en null: la opción global del proyecto omite
    /// los nulos, y un campo ausente obligaría al agente a distinguir "sin asignar"
    /// de "no vino en la respuesta". Acá el null es la información.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? AssignedToUserId { get; set; }
    public User? AssignedTo { get; set; }

    public ICollection<TrialClass> TrialClasses { get; set; } = new List<TrialClass>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    [JsonIgnore] public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class TrialClass : BaseEntity
{
    public int ProspectId { get; set; }
    public Prospect Prospect { get; set; } = null!;

    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public DateTime Schedule { get; set; }
    public bool AttendanceBool { get; set; }
    public TrialClassStatus Status { get; set; } = TrialClassStatus.Scheduled;

    // Relación recursiva: clase reprogramada a partir de otra.
    public int? ReprogrammedFromId { get; set; }
    public TrialClass? ReprogrammedFrom { get; set; }
}

public class Reminder : BaseEntity
{
    public int ProspectId { get; set; }
    public Prospect Prospect { get; set; } = null!;

    public string Note { get; set; } = null!;
    public DateTime RemindAt { get; set; }
    public bool IsDone { get; set; }
}

/// <summary>
/// Archivo que la IA puede mandar por WhatsApp (foto de promoción, audio de
/// bienvenida). Vive en Cloudflare R2; acá sólo se guarda cómo alcanzarlo.
/// </summary>
public class MediaAsset : BaseEntity
{
    public MediaType Type { get; set; }

    /// <summary>URL pública del archivo. Meta la descarga desde sus servidores al enviarlo.</summary>
    public string UrlR2 { get; set; } = null!;

    /// <summary>
    /// Clave del objeto dentro del bucket. Se guarda aparte de la URL porque es
    /// lo que necesita el borrado en R2: derivarla de la URL se rompe apenas
    /// cambia el dominio público.
    /// </summary>
    public string ObjectKey { get; set; } = null!;

    /// <summary>Nombre corto con el que se lo reconoce en la galería del panel.</summary>
    public string Label { get; set; } = null!;

    /// <summary>
    /// Qué dice o qué muestra el archivo, cargado a mano. Es lo único que la IA
    /// "sabe" de él: decide si mandar un audio leyendo esto, nunca abriendo el
    /// archivo en tiempo de respuesta.
    /// </summary>
    public string? Transcript { get; set; }

    [JsonIgnore] public ICollection<ContextEntryMedia> ContextEntries { get; set; } = new List<ContextEntryMedia>();
    [JsonIgnore] public ICollection<Message> Messages { get; set; } = new List<Message>();
}

/// <summary>
/// Contenido de negocio que los dueños cargan desde el panel y que el agente lee
/// entero en cada mensaje para armar el prompt: preguntas frecuentes, reglas,
/// promociones y flujos del embudo.
///
/// Una sola tabla con los campos específicos de cada tipo en nullables. Separarla
/// en cuatro obligaría al endpoint del agente a cuatro consultas y cuatro formas
/// distintas, cuando lo que necesita es una lista uniforme.
/// </summary>
public class ContextEntry : BaseEntity
{
    public ContextEntryType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;

    /// <summary>Interruptor del panel: apagar una entrada la saca del prompt sin borrarla.</summary>
    public bool Active { get; set; } = true;

    // ── Sólo para Type = Promotion ──

    /// <summary>Última fecha en que la promoción sigue vigente. El endpoint del agente filtra las vencidas.</summary>
    public DateOnly? ValidUntil { get; set; }

    /// <summary>
    /// Zonas a las que se limita la promoción. **Lista vacía = vale para todas**,
    /// que es el comportamiento por defecto y el más común.
    ///
    /// Antes era un solo `RestrictedZoneId`, y obligaba a cargar la misma promo
    /// dos veces —una por zona— con el trabajo doble de mantenerlas iguales.
    ///
    /// Viaja igual en la respuesta del agente: la restricción la aplica la IA
    /// conversando, porque cuando llega el primer mensaje todavía no se sabe de
    /// qué zona es el prospecto.
    /// </summary>
    public ICollection<ContextEntryZone> Zones { get; set; } = new List<ContextEntryZone>();

    public string? ConditionsText { get; set; }

    // ── Sólo para Type = Flow ──

    public NextAction? NextAction { get; set; }

    /// <summary>A quién derivarle la conversación cuando el flujo termina en "derivar".</summary>
    public int? HandoffToUserId { get; set; }
    public User? HandoffToUser { get; set; }

    public ICollection<ContextEntryMedia> Media { get; set; } = new List<ContextEntryMedia>();
}

/// <summary>
/// Unión N:M: un archivo sirve para varias entradas (la misma foto en dos
/// promociones) y una entrada puede llevar varios archivos.
/// </summary>
/// <summary>Unión N:M entre una ficha de contenido y las zonas a las que se limita.</summary>
public class ContextEntryZone
{
    public int ContextEntryId { get; set; }
    public ContextEntry ContextEntry { get; set; } = null!;
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
}

public class ContextEntryMedia
{
    public int ContextEntryId { get; set; }
    public ContextEntry ContextEntry { get; set; } = null!;
    public int MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
}

/// <summary>
/// Voz de la IA en un número de WhatsApp. OJO: no confundir con <see cref="Person"/>,
/// que es una persona real del CRM; esto es el papel que la IA representa.
///
/// Hay una por número (`PhoneNumberId` de la Cloud API), lo que permite que dos
/// números respondan con estilos distintos y que se pause uno sin tocar el otro.
/// </summary>
public class Persona : BaseEntity
{
    /// <summary>Persona real a la que representa; también el destino natural de una derivación.</summary>
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Id del número en la Cloud API de Meta (no el número en sí).</summary>
    public string PhoneNumberId { get; set; } = null!;

    /// <summary>Cómo escribe: tono, tratamiento, muletillas, qué nunca decir.</summary>
    public string StyleGuide { get; set; } = null!;

    /// <summary>Apagarla deja el número sin agente. El endpoint del agente responde 404 y n8n lo lee como "número pausado".</summary>
    public bool Active { get; set; } = true;
}

/// <summary>
/// Historial de chat de WhatsApp de un prospecto. Es el contexto que lee el
/// agente de IA antes de responder y el registro de lo que ya se contestó.
/// </summary>
public class Message : BaseEntity
{
    public int ProspectId { get; set; }
    public Prospect Prospect { get; set; } = null!;

    public MessageDirection Direction { get; set; }

    /// <summary>
    /// Quién escribió el mensaje. Distingue lo que mandó el bot de lo que
    /// escribió el dueño del número desde su propio WhatsApp (los ecos de
    /// Coexistence llegan como salientes): un saliente con origen "humano" es
    /// la señal de que alguien tomó la conversación a mano.
    /// </summary>
    public MessageOrigin Origin { get; set; } = MessageOrigin.Human;

    public string Content { get; set; } = null!;

    /// <summary>Adjunto del mensaje. Si se borra el archivo, el mensaje queda con el texto.</summary>
    public int? MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }

    /// <summary>URL temporal del adjunto en los servidores de Meta (caduca).</summary>
    public string? WhatsappMediaUrl { get; set; }

    /// <summary>
    /// wamid de Meta. Es la clave de idempotencia: Meta reintenta el webhook y
    /// el mismo mensaje puede llegar dos veces. Nullable porque un saliente se
    /// registra antes de que Meta devuelva su id.
    /// </summary>
    public string? WhatsappMessageId { get; set; }
}

public class Enrollment : BaseEntity
{
    public int ProspectId { get; set; }
    public Prospect Prospect { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public DateOnly EnrollmentDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public decimal Commission { get; set; }
}

// ───────────────────────── Identidad / RBAC ─────────────────────────
// Reemplaza Sanctum + Spatie por un esquema limpio Users <-> Roles.

public class User : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    [JsonIgnore] public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Cuenta de automatización (hoy: el agente de WhatsApp en n8n), no una persona.
    ///
    /// Existe para que la cuenta NO pueda entrar por /api/login: su hash de
    /// contraseña es de un secreto aleatorio que se descarta al crearla, así que
    /// nadie —ni siquiera Julio— puede iniciar sesión con ella. Su único acceso
    /// es un token emitido por /api/service-token, que pide ser Super Admin.
    ///
    /// El motivo de fondo: guardar email+contraseña de un usuario en n8n abre la
    /// cuenta entera (y podría cambiar su propia contraseña); un token abre sólo
    /// lo que el rol permite y se revoca sin tocar a nadie más.
    /// </summary>
    public bool IsServiceAccount { get; set; }

    [JsonIgnore] public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Role : BaseEntity
{
    public string Name { get; set; } = null!;            // "Super Admin", "HR Admin", "CRM Admin"
    [JsonIgnore] public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

/// <summary>Nombres canónicos de roles, usados en atributos [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string SuperAdmin = "Super Admin";
    public const string HrAdmin = "HR Admin";
    public const string CrmAdmin = "CRM Admin";
}
