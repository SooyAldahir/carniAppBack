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

    public async Task<HistorialRendimiento> ObtenerHistorialMedidoAsync(Guid tipoAnimalId, int ventana, CancellationToken cancellationToken = default)
    {
        var lotes = (await _context.LotesAnimal
                .Where(l => l.Activo && !l.EsEstimado && l.TipoAnimalId == tipoAnimalId)
                .ToListAsync(cancellationToken))
            .OrderByDescending(l => l.FechaCompra)
            .Take(ventana)
            .ToList();

        if (lotes.Count == 0)
        {
            return new HistorialRendimiento(0, 0m, new Dictionary<Guid, decimal>());
        }

        var ids = lotes.Select(l => (Guid?)l.Id).ToList();
        var movimientos = await _context.MovimientosInventario
            .Where(m => m.LoteAnimalId != null && ids.Contains(m.LoteAnimalId) && m.TipoMovimiento == TipoMovimientoInventario.Entrada)
            .ToListAsync(cancellationToken);

        var kgPorCorte = movimientos
            .GroupBy(m => m.CorteId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Cantidad));

        return new HistorialRendimiento(lotes.Count, lotes.Sum(l => l.PesoKg), kgPorCorte);
    }
}
