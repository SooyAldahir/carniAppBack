using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class VentaRepository : IVentaRepository
{
    private readonly CarniAppDbContext _context;

    public VentaRepository(CarniAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Venta venta, CancellationToken cancellationToken = default) =>
        await _context.Ventas.AddAsync(venta, cancellationToken);

    public async Task<Venta?> GetByIdCompletoAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Ventas
            .Include(v => v.Usuario)
            .Include(v => v.Cliente)
            .Include(v => v.Detalles).ThenInclude(d => d.Corte)
            .Include(v => v.Pagos).ThenInclude(p => p.MetodoPago)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Venta>> ListarPorRangoAsync(DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default) =>
        await _context.Ventas
            .AsNoTracking()
            .Include(v => v.Usuario)
            .Where(v => v.Fecha >= desdeUtc && v.Fecha < hastaUtc)
            .OrderByDescending(v => v.Fecha)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Venta>> ListarConPagosPorRangoAsync(Guid sucursalId, DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default) =>
        await _context.Ventas
            .AsNoTracking()
            .Include(v => v.Pagos).ThenInclude(p => p.MetodoPago)
            .Where(v => v.SucursalId == sucursalId && v.Fecha >= desdeUtc && v.Fecha < hastaUtc)
            .ToListAsync(cancellationToken);
}
