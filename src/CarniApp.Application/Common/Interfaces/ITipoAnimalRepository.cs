using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface ITipoAnimalRepository : IRepository<TipoAnimal>
{
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId = null, CancellationToken cancellationToken = default);

    Task<int> ContarCortesActivosAsync(Guid tipoAnimalId, CancellationToken cancellationToken = default);
}
