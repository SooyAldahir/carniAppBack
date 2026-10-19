using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class AbonoCreditoRepository : IAbonoCreditoRepository
{
    private readonly CarniAppDbContext _context;

    public AbonoCreditoRepository(CarniAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AbonoCredito abono, CancellationToken cancellationToken = default) =>
        await _context.AbonosCredito.AddAsync(abono, cancellationToken);

    public async Task<IReadOnlyList<AbonoCredito>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default) =>
        await _context.AbonosCredito
            .AsNoTracking()
            .Include(a => a.Usuario)
            .Where(a => a.ClienteId == clienteId)
            .OrderByDescending(a => a.Fecha)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AbonoCredito>> ListarPorRangoAsync(DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default) =>
        await _context.AbonosCredito
            .AsNoTracking()
            .Where(a => a.Fecha >= desdeUtc && a.Fecha < hastaUtc)
            .ToListAsync(cancellationToken);
}
