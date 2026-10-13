using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca todos los refresh tokens activos de un usuario. Se usa al
    /// detectar reuso de un token ya revocado (posible robo, RF-AUTH-007)
    /// y al desactivar/cerrar sesion remota de un usuario.
    /// </summary>
    Task RevocarTodosDelUsuarioAsync(Guid usuarioId, DateTime ahoraUtc, CancellationToken cancellationToken = default);
}
