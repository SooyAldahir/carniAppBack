using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface IClienteRepository : IRepository<Cliente>
{
    /// <summary>Clientes activos, filtrados por nombre o telefono (si se indica texto).</summary>
    Task<IReadOnlyList<Cliente>> BuscarActivosAsync(string? texto, CancellationToken cancellationToken = default);

    Task<bool> ExisteTelefonoAsync(string telefono, Guid? excluirId = null, CancellationToken cancellationToken = default);
}
