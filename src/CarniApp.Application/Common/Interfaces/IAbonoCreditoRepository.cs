using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface IAbonoCreditoRepository
{
    Task AddAsync(AbonoCredito abono, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AbonoCredito>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default);
}
