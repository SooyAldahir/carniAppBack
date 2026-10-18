using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

/// <summary>No extiende IRepository{T}: LoteAnimal no hereda de BaseEntity.</summary>
public interface ILoteAnimalRepository
{
    Task AddAsync(LoteAnimal lote, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoteAnimal>> ObtenerRecientesAsync(int limite, CancellationToken cancellationToken = default);
}
