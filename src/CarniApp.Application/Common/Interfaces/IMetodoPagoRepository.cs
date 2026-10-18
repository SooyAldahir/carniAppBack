using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface IMetodoPagoRepository
{
    Task<IReadOnlyList<MetodoPago>> ListarActivosAsync(CancellationToken cancellationToken = default);

    Task<MetodoPago?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
