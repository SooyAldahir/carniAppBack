using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface ICorteRepository : IRepository<Corte>
{
    Task<IReadOnlyList<Corte>> GetAllConTipoAnimalAsync(CancellationToken cancellationToken = default);

    Task<Corte?> GetByIdConTipoAnimalAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId = null, CancellationToken cancellationToken = default);
}
