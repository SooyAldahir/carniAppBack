using CarniApp.Application.Features.Catalogos;

namespace CarniApp.Application.Common.Interfaces;

public interface ICatalogoService
{
    Task<IReadOnlyList<TipoAnimalDto>> ListarTiposAnimalAsync(CancellationToken cancellationToken = default);

    Task<CatalogoResultDto<TipoAnimalDto>> CrearTipoAnimalAsync(GuardarTipoAnimalRequest request, CancellationToken cancellationToken = default);

    Task<CatalogoResultDto<TipoAnimalDto>> ActualizarTipoAnimalAsync(Guid id, GuardarTipoAnimalRequest request, CancellationToken cancellationToken = default);

    Task<CatalogoResultDto<bool>> DesactivarTipoAnimalAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorteDto>> ListarCortesAsync(CancellationToken cancellationToken = default);

    Task<CatalogoResultDto<CorteDto>> CrearCorteAsync(GuardarCorteRequest request, CancellationToken cancellationToken = default);

    Task<CatalogoResultDto<CorteDto>> ActualizarCorteAsync(Guid id, GuardarCorteRequest request, CancellationToken cancellationToken = default);

    Task<CatalogoResultDto<bool>> DesactivarCorteAsync(Guid id, CancellationToken cancellationToken = default);
}
