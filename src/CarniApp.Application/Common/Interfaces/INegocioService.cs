using CarniApp.Application.Features.Negocio;

namespace CarniApp.Application.Common.Interfaces;

public interface INegocioService
{
    Task<NegocioDto?> ObtenerAsync(CancellationToken cancellationToken = default);

    Task<NegocioDto> ActualizarAsync(ActualizarNegocioRequest request, CancellationToken cancellationToken = default);
}
