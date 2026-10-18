using CarniApp.Application.Features.Ventas;

namespace CarniApp.Application.Common.Interfaces;

public interface IVentaService
{
    Task<IReadOnlyList<CortePosDto>> ObtenerCatalogoAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetodoPagoDto>> ListarMetodosPagoAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VentaResumenDto>> ListarAsync(DateTime? desdeUtc, DateTime? hastaUtc, CancellationToken cancellationToken = default);

    Task<VentaResultDto<VentaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="permisos">Permisos del usuario (claims). Hacen falta para aplicar descuentos (RF-POS-004).</param>
    Task<VentaResultDto<VentaDto>> CrearAsync(CrearVentaRequest request, Guid usuarioId, IReadOnlyCollection<string>? permisos = null, CancellationToken cancellationToken = default);

    Task<VentaResultDto<TicketDto>> ObtenerTicketAsync(Guid id, CancellationToken cancellationToken = default);

    Task<VentaResultDto<VentaDto>> AnularAsync(Guid id, string motivo, Guid usuarioId, CancellationToken cancellationToken = default);
}
