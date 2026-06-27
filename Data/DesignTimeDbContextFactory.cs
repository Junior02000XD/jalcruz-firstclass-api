using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JalcruzFirstClass.Api.Data;

/// <summary>
/// Permite a `dotnet ef migrations` / `database update` construir el DbContext en
/// tiempo de diseño sin ejecutar Program.cs (que conecta a la BD al arrancar).
/// La cadena real se resuelve en runtime; aquí solo se necesita el proveedor Npgsql.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_URL")
                   ?? "Host=localhost;Port=5432;Database=jalcruz_firstclass;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
