using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JalcruzFirstClass.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace JalcruzFirstClass.Api.Services;

public class JwtOptions
{
    public string Key { get; set; } = null!;
    public string Issuer { get; set; } = "jalcruz-firstclass-api";
    public string Audience { get; set; } = "jalcruz-firstclass-web";
    public int ExpiryHours { get; set; } = 12;

    /// <summary>
    /// Vida de los tokens de cuenta de servicio, en días. Sólo aplica a
    /// /api/service-token; el login normal sigue con ExpiryHours (12 h) para todos.
    /// Por defecto 10 años: n8n corre sin nadie mirando y un token vencido se
    /// manifestaría como 401 silenciosos en medio de una conversación real.
    /// </summary>
    public int ServiceTokenDays { get; set; } = 3650;
}

public class JwtTokenService(JwtOptions options)
{
    public string CreateToken(User user, IEnumerable<string> roles)
        => CreateToken(user, roles, TimeSpan.FromHours(options.ExpiryHours));

    /// <summary>
    /// Token de cuenta de servicio: MISMOS claims, MISMOS roles y MISMA firma que
    /// el del login, sólo con otra expiración. Deliberadamente no es un mecanismo
    /// de autenticación aparte — el middleware que lo valida es el de siempre, así
    /// que no hay una segunda superficie que mantener ni que auditar.
    /// </summary>
    public string CreateServiceToken(User user, IEnumerable<string> roles)
        => CreateToken(user, roles, TimeSpan.FromDays(options.ServiceTokenDays));

    private string CreateToken(User user, IEnumerable<string> roles, TimeSpan lifetime)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        // Un claim de rol por cada rol -> habilita [Authorize(Roles = "...")].
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
