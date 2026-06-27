using JalcruzFirstClass.Api.Domain;
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

        // ── Índices y restricciones (espejan las migraciones de Laravel) ──
        b.Entity<City>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Person>().HasIndex(x => x.Ci).IsUnique();
        b.Entity<Person>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Payroll>().HasIndex(x => x.Code).IsUnique();
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Role>().HasIndex(x => x.Name).IsUnique();

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
        return base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
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
}
