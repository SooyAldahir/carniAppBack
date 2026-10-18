using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class MetodoPagoRepository : IMetodoPagoRepository
{
    private readonly CarniAppDbContext _context;

    public MetodoPagoRepository(CarniAppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MetodoPago>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
        await _context.MetodosPago.Where(m => m.Activo).OrderBy(m => m.Nombre).ToListAsync(cancellationToken);

    public async Task<MetodoPago?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.MetodosPago.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
}
