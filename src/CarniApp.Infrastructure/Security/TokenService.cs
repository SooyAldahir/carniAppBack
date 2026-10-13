using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CarniApp.Infrastructure.Security;

/// <summary>
/// Firma los JWT de acceso (RF-AUTH-002) incluyendo rol y permisos
/// (RF-AUTH-004, SUP-03) como claims, para que la autorizacion por
/// permiso granular no necesite otra consulta a la base de datos en
/// cada request. Lee la configuracion Jwt:* de appsettings/user-secrets.
/// </summary>
public class TokenService : ITokenService
{
    public const string PermisoClaimType = "perm";

    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AccessTokenResultDto GenerarAccessToken(Usuario usuario, IEnumerable<string> permisos)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta configurar Jwt:Key.");
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var minutos = _configuration.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 20;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreUsuario),
            new("nombreCompleto", usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol.Nombre)
        };

        claims.AddRange(permisos.Select(p => new Claim(PermisoClaimType, p)));

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            SecurityAlgorithms.HmacSha256);

        var expiraUtc = DateTime.UtcNow.AddMinutes(minutos);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiraUtc,
            signingCredentials: credenciales);

        var tokenEscrito = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessTokenResultDto(tokenEscrito, expiraUtc);
    }

    public string GenerarRefreshTokenPlano()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public DateTime CalcularExpiracionRefreshToken()
    {
        var dias = _configuration.GetValue<int?>("Jwt:RefreshTokenDays") ?? 14;
        return DateTime.UtcNow.AddDays(dias);
    }
}
