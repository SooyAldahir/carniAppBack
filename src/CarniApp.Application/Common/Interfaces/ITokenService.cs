using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public record AccessTokenResultDto(string Token, DateTime ExpiraUtc);

/// <summary>
/// Genera y firma los JWT de acceso (RF-AUTH-002). Vive la implementacion
/// en Infrastructure porque necesita leer configuracion (Jwt:Key/Issuer/
/// Audience/AccessTokenMinutes) y las librerias de firma.
/// </summary>
public interface ITokenService
{
    AccessTokenResultDto GenerarAccessToken(Usuario usuario, IEnumerable<string> permisos);

    string GenerarRefreshTokenPlano();

    DateTime CalcularExpiracionRefreshToken();
}
