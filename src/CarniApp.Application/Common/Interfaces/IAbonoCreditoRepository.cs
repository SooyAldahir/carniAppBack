using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface IAbonoCreditoRepository
{
    Task AddAsync(AbonoCredito abono, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AbonoCredito>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default);

    /// <summary>Abonos con Fecha en [desdeUtc, hastaUtc) (entran como efectivo al corte de caja).</summary>
    Task<IReadOnlyList<AbonoCredito>> ListarPorRangoAsync(DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default);
}
