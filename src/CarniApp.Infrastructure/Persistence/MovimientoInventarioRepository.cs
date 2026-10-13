using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class MovimientoInventarioRepository : IMovimientoInventarioRepository
{
    private readonly CarniAppDbContext _context;

    public MovimientoInventarioRepository(CarniAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MovimientoInventario movimiento, CancellationToken cancellationToken = default) =>
        await _context.MovimientosInventario.AddAsync(movimiento, cancellationToken);

    public async Task<IReadOnlyList<MovimientoInventario>> ObtenerPorCorteAsync(
        Guid corteId,
        Guid sucursalId,
        int limite,
        CancellationToken cancellationToken = default) =>
        await _context.MovimientosInventario
            .Include(m => m.Usuario)
            .Where(m => m.CorteId == corteId && m.SucursalId == sucursalId)
            .OrderByDescending(m => m.Fecha)
            .Take(limite)
            .ToListAsync(cancellationToken);
}
