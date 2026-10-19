using CarniApp.Application.Features.Caja;
using CarniApp.Application.Features.Ventas;

namespace CarniApp.Application.Common.Interfaces;

public interface ICajaService
{
    Task<CajaResultDto<EstadoCajaDto>> ObtenerEstadoAsync(CancellationToken cancellationToken = default);

    Task<CajaResultDto<CorteCajaDto>> AbrirAsync(AbrirCajaRequest request, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<CajaResultDto<CorteCajaDto>> RegistrarGastoAsync(GastoCajaRequest request, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<CajaResultDto<CorteCajaDto>> CerrarAsync(CerrarCajaRequest request, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<CajaResultDto<CorteCajaDto>> ReabrirAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CajaResultDto<CorteCajaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorteCajaResumenDto>> ListarAsync(int limite, CancellationToken cancellationToken = default);

    Task<CajaResultDto<TicketDto>> ObtenerTicketAsync(Guid id, CancellationToken cancellationToken = default);
}
