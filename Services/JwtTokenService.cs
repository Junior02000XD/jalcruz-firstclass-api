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
}

public class JwtTokenService(JwtOptions options)
{
    public string CreateToken(User user, IEnumerable<string> roles)
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
            expires: DateTime.UtcNow.AddHours(options.ExpiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
