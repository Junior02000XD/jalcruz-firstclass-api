using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Configuración (variables de entorno tienen prioridad, ideal para Railway) ──
var config = builder.Configuration;

// Railway suele exponer la BD como DATABASE_URL (postgres://user:pass@host:port/db).
// Aceptamos tanto esa forma como una cadena ConnectionStrings:Default tradicional.
var connectionString = BuildConnectionString(config);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// ── JWT ──
var jwt = new JwtOptions
{
    Key = config["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key en la configuración."),
    Issuer = config["Jwt:Issuer"] ?? "jalcruz-firstclass-api",
    Audience = config["Jwt:Audience"] ?? "jalcruz-firstclass-web",
    ExpiryHours = int.TryParse(config["Jwt:ExpiryHours"], out var h) ? h : 12,
};
builder.Services.AddSingleton(jwt);
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<PayrollExportService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ── CORS para el frontend (Cloudflare Pages + desarrollo local) ──
var allowedOrigins = (config["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // Claves en snake_case para mantener el MISMO contrato que el backend Laravel
    // y no romper el frontend React (que lee first_name, access_token, etc.).
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    // Evita errores de ciclos al serializar entidades con navegación.
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // Enums como string mapeado ("asistio", "inscrito"...) igual que Laravel.
    o.JsonSerializerOptions.Converters.AddDomainEnumConverters();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Migrar + sembrar al arrancar ──
await DbSeeder.SeedAsync(app.Services, config, app.Logger);

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new { service = "Jalcruz First Class API", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();


// ─────────────────────────────────────────────────────────────────────
// Construye la cadena de conexión Npgsql desde DATABASE_URL (Railway) o
// desde ConnectionStrings:Default. Permite arrancar igual en local y en la nube.
static string BuildConnectionString(IConfiguration config)
{
    var url = config["DATABASE_URL"] ?? Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var db = uri.AbsolutePath.TrimStart('/');
        var sslMode = url.Contains("sslmode=", StringComparison.OrdinalIgnoreCase)
            ? ""
            : ";SSL Mode=Require;Trust Server Certificate=true";
        return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={db};" +
               $"Username={Uri.UnescapeDataString(userInfo[0])};" +
               $"Password={Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "")}{sslMode}";
    }

    return config.GetConnectionString("Default")
           ?? throw new InvalidOperationException("No se encontró DATABASE_URL ni ConnectionStrings:Default.");
}
