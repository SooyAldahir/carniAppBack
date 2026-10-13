using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class CorteRepository : Repository<Corte>, ICorteRepository
{
    public CorteRepository(CarniAppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Corte>> GetAllConTipoAnimalAsync(CancellationToken cancellationToken = default) =>
        await _dbSet
            .Include(c => c.TipoAnimal)
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<Corte?> GetByIdConTipoAnimalAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Include(c => c.TipoAnimal)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId = null, CancellationToken cancellationToken = default) =>
        await _dbSet.AnyAsync(c =>
            c.Activo &&
            c.Nombre.ToLower() == nombre.ToLower() &&
            (excluirId == null || c.Id != excluirId), cancellationToken);
}
