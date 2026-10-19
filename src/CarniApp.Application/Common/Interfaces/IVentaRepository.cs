using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

public interface IVentaRepository
{
    Task AddAsync(Venta venta, CancellationToken cancellationToken = default);

    /// <summary>Venta con detalles (+corte), pagos (+metodo) y usuario. Queda rastreada por el DbContext.</summary>
    Task<Venta?> GetByIdCompletoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ventas con Fecha en [desdeUtc, hastaUtc), mas recientes primero.</summary>
    Task<IReadOnlyList<Venta>> ListarPorRangoAsync(DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default);

    /// <summary>Ventas de la sucursal con Fecha en [desdeUtc, hastaUtc), con pagos y metodo de pago (para el corte de caja).</summary>
    Task<IReadOnlyList<Venta>> ListarConPagosPorRangoAsync(Guid sucursalId, DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken = default);
}
