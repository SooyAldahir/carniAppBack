using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class LoteAnimalRepository : ILoteAnimalRepository
{
    private readonly CarniAppDbContext _context;

    public LoteAnimalRepository(CarniAppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LoteAnimal lote, CancellationToken cancellationToken = default) =>
        await _context.LotesAnimal.AddAsync(lote, cancellationToken);

    public async Task<IReadOnlyList<LoteAnimal>> ObtenerRecientesAsync(int limite, CancellationToken cancellationToken = default) =>
        (await _context.LotesAnimal
            .Include(l => l.TipoAnimal)
            .Where(l => l.Activo)
            .ToListAsync(cancellationToken))
        .OrderByDescending(l => l.FechaCompra)
        .Take(limite)
        .ToList();
}
