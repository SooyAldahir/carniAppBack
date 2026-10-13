using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface IMovimientoInventarioRepository
{
    Task AddAsync(MovimientoInventario movimiento, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MovimientoInventario>> ObtenerPorCorteAsync(
        Guid corteId,
        Guid sucursalId,
        int limite,
        CancellationToken cancellationToken = default);
}
