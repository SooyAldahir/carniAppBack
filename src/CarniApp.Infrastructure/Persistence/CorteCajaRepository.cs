using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class CorteCajaRepository : ICorteCajaRepository
{
    private readonly CarniAppDbContext _context;

    public CorteCajaRepository(CarniAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CorteCaja corte, CancellationToken cancellationToken = default) =>
        await _context.CortesCaja.AddAsync(corte, cancellationToken);

    public async Task AddGastoAsync(GastoCaja gasto, CancellationToken cancellationToken = default) =>
        await _context.GastosCaja.AddAsync(gasto, cancellationToken);

    private IQueryable<CorteCaja> Completo() =>
        _context.CortesCaja
            .Include(c => c.Usuario)
            .Include(c => c.Gastos).ThenInclude(g => g.Usuario);

    public async Task<CorteCaja?> ObtenerAbiertoAsync(Guid sucursalId, CancellationToken cancellationToken = default) =>
        await Completo().FirstOrDefaultAsync(c => c.SucursalId == sucursalId && c.Estado == EstadoCorteCaja.Abierto, cancellationToken);

    public async Task<CorteCaja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Completo().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CorteCaja>> ListarRecientesAsync(int limite, CancellationToken cancellationToken = default) =>
        await _context.CortesCaja
            .AsNoTracking()
            .Include(c => c.Usuario)
            .OrderByDescending(c => c.FechaInicio)
            .Take(limite)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExisteCerradoQueContieneAsync(Guid sucursalId, DateTime fechaUtc, CancellationToken cancellationToken = default) =>
        await _context.CortesCaja.AnyAsync(c =>
            c.SucursalId == sucursalId &&
            c.Estado == EstadoCorteCaja.Cerrado &&
            c.FechaInicio <= fechaUtc &&
            c.FechaCierre >= fechaUtc, cancellationToken);

    public async Task<bool> ExisteCortePosteriorAsync(Guid sucursalId, DateTime fechaInicioUtc, CancellationToken cancellationToken = default) =>
        await _context.CortesCaja.AnyAsync(c => c.SucursalId == sucursalId && c.FechaInicio > fechaInicioUtc, cancellationToken);
}
