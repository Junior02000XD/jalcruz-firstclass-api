using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JalcruzFirstClass.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Phone> Phones => Set<Phone>();
    public DbSet<Entity> Entities => Set<Entity>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<WorkArea> WorkAreas => Set<WorkArea>();
    public DbSet<WorkerDetail> WorkerDetails => Set<WorkerDetail>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Prospect> Prospects => Set<Prospect>();
    public DbSet<TrialClass> TrialClasses => Set<TrialClass>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ContextEntry> ContextEntries => Set<ContextEntry>();
    public DbSet<ContextEntryMedia> ContextEntryMedia => Set<ContextEntryMedia>();
    public DbSet<Persona> Personas => Set<Persona>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Conversores de enum -> string (compatibles con los valores del Laravel original).
        b.Entity<WorkerDetail>().Property(x => x.Reliability)
            .HasConversion(MapConverter(EnumMaps.Reliability));
        b.Entity<Attendance>().Property(x => x.Status)
            .HasConversion(MapConverter(EnumMaps.AttendanceStatus));
        b.Entity<Entity>().Property(x => x.Type)
            .HasConversion(MapConverter(EnumMaps.EntityType));
        b.Entity<Prospect>().Property(x => x.Status)
            .HasConversion(MapConverter(EnumMaps.ProspectStatus));
        b.Entity<TrialClass>().Property(x => x.Status)
            .HasConversion(MapConverter(EnumMaps.TrialClassStatus));
        b.Entity<Message>().Property(x => x.Direction)
            .HasConversion(MapConverter(EnumMaps.MessageDirection));
        b.Entity<Message>().Property(x => x.Origin)
            .HasConversion(MapConverter(EnumMaps.MessageOrigin));
        b.Entity<MediaAsset>().Property(x => x.Type)
            .HasConversion(MapConverter(EnumMaps.MediaType));
        b.Entity<ContextEntry>().Property(x => x.Type)
            .HasConversion(MapConverter(EnumMaps.ContextEntryType));
        b.Entity<ContextEntry>().Property(x => x.NextAction)
            .HasConversion(MapConverter(EnumMaps.NextAction));

        // ── Índices y restricciones (espejan las migraciones de Laravel) ──
        b.Entity<City>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Person>().HasIndex(x => x.Ci).IsUnique();
        b.Entity<Person>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Payroll>().HasIndex(x => x.Code).IsUnique();
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Role>().HasIndex(x => x.Name).IsUnique();

        // Lookup por teléfono normalizado: corre una vez por cada mensaje entrante
        // de WhatsApp, así que no puede ser un scan.
        //
        // NO es único a propósito: `phones` la comparten los dos módulos, y en RRHH
        // es legítimo que dos trabajadores compartan un número de contacto. La
        // unicidad del prospecto por número la garantiza el advisory lock de
        // ProspectsController.QuickCreate. Ver docs/WHATSAPP-CRM.md.
        b.Entity<Phone>().HasIndex(x => x.NormalizedNumber);

        // Idempotencia de POST /api/messages: Meta reintenta el webhook y el mismo
        // wamid puede llegar dos veces. Filtrado porque los salientes se registran
        // sin id todavía, y varios NULL no chocan entre sí.
        b.Entity<Message>().HasIndex(x => x.WhatsappMessageId)
            .IsUnique()
            .HasFilter("whatsapp_message_id IS NOT NULL");

        // Historial de un prospecto en orden cronológico (GET /api/prospects/{id}/messages).
        b.Entity<Message>().HasIndex(x => new { x.ProspectId, x.CreatedAt });

        // Atribución de campaña por anuncio: el agente busca por el
        // `referral.source_id` que trae el mensaje entrante. Único cuando tiene
        // valor —dos campañas no pueden reclamar el mismo anuncio, la atribución
        // quedaría a suerte— y filtrado para que las campañas sin anuncio, que
        // son la mayoría, no choquen entre sí por ser todas NULL.
        b.Entity<Campaign>().HasIndex(x => x.AdId)
            .IsUnique()
            .HasFilter("ad_id IS NOT NULL");

        // Un solo agente activo por número de WhatsApp. Filtrado por `active` para
        // que las personas apagadas —las versiones anteriores del estilo, que
        // conviene conservar— no impidan crear la que va a estar en uso.
        b.Entity<Persona>().HasIndex(x => x.PhoneNumberId)
            .IsUnique()
            .HasFilter("active");

        // El agente pide todo el contenido encendido en cada mensaje entrante.
        b.Entity<ContextEntry>().HasIndex(x => new { x.Active, x.Type });

        // Relación 1:1 Person <-> WorkerDetail.
        b.Entity<WorkerDetail>().HasIndex(x => x.PersonId).IsUnique();
        b.Entity<WorkerDetail>()
            .HasOne(w => w.Person).WithOne(p => p.WorkerDetail)
            .HasForeignKey<WorkerDetail>(w => w.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Borrados en cascada / restringidos (espejan onDelete de Laravel).
        b.Entity<Phone>().HasOne(p => p.Person).WithMany(x => x.Phones)
            .HasForeignKey(p => p.PersonId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Person>().HasOne(p => p.City).WithMany(c => c.People)
            .HasForeignKey(p => p.CityId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<WorkArea>().HasOne(w => w.Company).WithMany(c => c.WorkAreas)
            .HasForeignKey(w => w.CompanyId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkArea>().HasOne(w => w.City).WithMany(c => c.WorkAreas)
            .HasForeignKey(w => w.CityId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkArea>().HasOne(w => w.InCharge).WithMany()
            .HasForeignKey(w => w.InChargeId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Payroll>().HasOne(p => p.WorkArea).WithMany(w => w.Payrolls)
            .HasForeignKey(p => p.WorkAreaId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Attendance>().HasOne(a => a.Payroll).WithMany(p => p.Attendances)
            .HasForeignKey(a => a.PayrollId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Attendance>().HasOne(a => a.Person).WithMany(p => p.Attendances)
            .HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Prospect>().HasOne(p => p.Person).WithMany(x => x.Prospects)
            .HasForeignKey(p => p.PersonId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Prospect>().HasOne(p => p.Campaign).WithMany(c => c.Prospects)
            .HasForeignKey(p => p.CampaignId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Prospect>().HasOne(p => p.Entity).WithMany(e => e.Prospects)
            .HasForeignKey(p => p.EntityId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Prospect>().HasOne(p => p.Zone).WithMany(z => z.Prospects)
            .HasForeignKey(p => p.ZoneId).OnDelete(DeleteBehavior.SetNull);
        // Si se borra el usuario que tomó la conversación, el prospecto vuelve a la IA.
        b.Entity<Prospect>().HasOne(p => p.AssignedTo).WithMany()
            .HasForeignKey(p => p.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Message>().HasOne(m => m.Prospect).WithMany(p => p.Messages)
            .HasForeignKey(m => m.ProspectId).OnDelete(DeleteBehavior.Cascade);
        // Borrar un archivo de la galería no borra el mensaje que lo mandó.
        b.Entity<Message>().HasOne(m => m.MediaAsset).WithMany(a => a.Messages)
            .HasForeignKey(m => m.MediaAssetId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<ContextEntry>().HasOne(c => c.RestrictedZone).WithMany()
            .HasForeignKey(c => c.RestrictedZoneId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<ContextEntry>().HasOne(c => c.HandoffToUser).WithMany()
            .HasForeignKey(c => c.HandoffToUserId).OnDelete(DeleteBehavior.SetNull);

        // Unión N:M ContextEntry <-> MediaAsset, con el mismo patrón que user_roles.
        b.Entity<ContextEntryMedia>().ToTable("context_entry_media")
            .HasKey(x => new { x.ContextEntryId, x.MediaAssetId });
        b.Entity<ContextEntryMedia>().HasOne(x => x.ContextEntry).WithMany(c => c.Media)
            .HasForeignKey(x => x.ContextEntryId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ContextEntryMedia>().HasOne(x => x.MediaAsset).WithMany(a => a.ContextEntries)
            .HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Cascade);

        // Si se borra el usuario, se va su persona: sin a quién representar no tiene sentido.
        b.Entity<Persona>().HasOne(p => p.User).WithMany()
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TrialClass>().HasOne(t => t.Prospect).WithMany(p => p.TrialClasses)
            .HasForeignKey(t => t.ProspectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TrialClass>().HasOne(t => t.Teacher).WithMany(te => te.TrialClasses)
            .HasForeignKey(t => t.TeacherId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<TrialClass>().HasOne(t => t.ReprogrammedFrom).WithMany()
            .HasForeignKey(t => t.ReprogrammedFromId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Reminder>().HasOne(r => r.Prospect).WithMany(p => p.Reminders)
            .HasForeignKey(r => r.ProspectId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Enrollment>().HasOne(e => e.Prospect).WithMany(p => p.Enrollments)
            .HasForeignKey(e => e.ProspectId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Enrollment>().HasOne(e => e.Product).WithMany(p => p.Enrollments)
            .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);

        // Decimales con precisión explícita (igual que las migraciones).
        b.Entity<Attendance>().Property(x => x.Amount).HasPrecision(8, 2);
        b.Entity<Attendance>().Property(x => x.ExtraAmount).HasPrecision(8, 2);
        b.Entity<Product>().Property(x => x.Price).HasPrecision(8, 2);
        b.Entity<Campaign>().Property(x => x.Budget).HasPrecision(10, 2);
        b.Entity<Enrollment>().Property(x => x.Commission).HasPrecision(8, 2);

        // Join Users <-> Roles.
        b.Entity<UserRole>().ToTable("user_roles").HasKey(x => new { x.UserId, x.RoleId });
        b.Entity<UserRole>().HasOne(ur => ur.User).WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserRole>().HasOne(ur => ur.Role).WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);

        // Roles fijos (seed) — coinciden con los de Spatie en el sistema original.
        b.Entity<Role>().HasData(
            new Role { Id = 1, Name = Domain.Roles.SuperAdmin, CreatedAt = Seed, UpdatedAt = Seed },
            new Role { Id = 2, Name = Domain.Roles.HrAdmin, CreatedAt = Seed, UpdatedAt = Seed },
            new Role { Id = 3, Name = Domain.Roles.CrmAdmin, CreatedAt = Seed, UpdatedAt = Seed }
        );
    }

    private static readonly DateTime Seed = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Construye un ValueConverter enum&lt;-&gt;string a partir de un mapa.</summary>
    private static ValueConverter<TEnum, string> MapConverter<TEnum>(Dictionary<TEnum, string> map)
        where TEnum : struct, Enum
    {
        var inverse = map.ToDictionary(kv => kv.Value, kv => kv.Key);
        return new ValueConverter<TEnum, string>(
            v => map[v],
            s => inverse[s]);
    }

    /// <summary>Asigna CreatedAt/UpdatedAt automáticamente en cada guardado.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        StampTimestamps();
        NormalizePhones();
        return base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        NormalizePhones();
        return base.SaveChanges();
    }

    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    /// <summary>
    /// Recalcula NormalizedNumber en cada alta/edición de teléfono.
    /// Va acá y no en los controllers para que la columna no pueda quedar
    /// desincronizada: si queda vieja, el prospecto se vuelve invisible para el
    /// lookup por WhatsApp y n8n lo duplica.
    /// </summary>
    private void NormalizePhones()
    {
        foreach (var entry in ChangeTracker.Entries<Phone>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            entry.Entity.NormalizedNumber = PhoneNormalizer.Normalize(entry.Entity.Number);
        }
    }
}
