using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class InventarioRepository : IInventarioRepository
{
    private readonly CarniAppDbContext _context;

    public InventarioRepository(CarniAppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Inventario>> ObtenerConCorteAsync(Guid sucursalId, CancellationToken cancellationToken = default) =>
        await _context.Inventarios
            .Include(i => i.Corte)
            .Where(i => i.SucursalId == sucursalId)
            .ToListAsync(cancellationToken);

    public async Task<Inventario> ObtenerOCrearAsync(Guid corteId, Guid sucursalId, CancellationToken cancellationToken = default)
    {
        var inventario = await _context.Inventarios
            .FirstOrDefaultAsync(i => i.CorteId == corteId && i.SucursalId == sucursalId, cancellationToken);

        if (inventario is not null) return inventario;

        inventario = new Inventario { CorteId = corteId, SucursalId = sucursalId, StockActual = 0 };
        await _context.Inventarios.AddAsync(inventario, cancellationToken);
        return inventario;
    }

    public void Update(Inventario inventario) => _context.Inventarios.Update(inventario);
}
