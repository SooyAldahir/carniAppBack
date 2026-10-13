using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(CarniAppDbContext context) : base(context)
    {
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Include(rt => rt.Usuario)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task RevocarTodosDelUsuarioAsync(Guid usuarioId, DateTime ahoraUtc, CancellationToken cancellationToken = default)
    {
        var activos = await _dbSet
            .Where(rt => rt.UsuarioId == usuarioId && rt.RevocadoUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activos)
        {
            token.RevocadoUtc = ahoraUtc;
        }
    }
}
