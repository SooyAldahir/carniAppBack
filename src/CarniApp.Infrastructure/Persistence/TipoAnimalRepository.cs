using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class TipoAnimalRepository : Repository<TipoAnimal>, ITipoAnimalRepository
{
    public TipoAnimalRepository(CarniAppDbContext context) : base(context)
    {
    }

    public async Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId = null, CancellationToken cancellationToken = default) =>
        await _dbSet.AnyAsync(t =>
            t.Activo &&
            t.Nombre.ToLower() == nombre.ToLower() &&
            (excluirId == null || t.Id != excluirId), cancellationToken);

    public async Task<int> ContarCortesActivosAsync(Guid tipoAnimalId, CancellationToken cancellationToken = default) =>
        await _context.Cortes.CountAsync(c => c.TipoAnimalId == tipoAnimalId && c.Activo, cancellationToken);
}
