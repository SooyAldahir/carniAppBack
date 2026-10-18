using CarniApp.Application.Features.VentaFacil;

namespace CarniApp.Application.Common.Interfaces;

public interface IVentaFacilService
{
    Task<VentaFacilResultDto<RendimientosDto>> ObtenerRendimientosAsync(Guid tipoAnimalId, CancellationToken cancellationToken = default);

    Task<VentaFacilResultDto<RendimientosDto>> GuardarRendimientosAsync(GuardarRendimientosRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calcula el reparto estimado sin guardar nada.</summary>
    Task<VentaFacilResultDto<RepartoDto>> PrevisualizarAsync(LoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Guarda el lote y da de alta el inventario estimado, todo en una transaccion.</summary>
    Task<VentaFacilResultDto<RepartoDto>> RegistrarLoteAsync(LoteRequest request, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoteDto>> ListarLotesAsync(CancellationToken cancellationToken = default);
}
