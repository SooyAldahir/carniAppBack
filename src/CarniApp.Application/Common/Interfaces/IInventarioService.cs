using CarniApp.Application.Features.Inventario;

namespace CarniApp.Application.Common.Interfaces;

public interface IInventarioService
{
    Task<IReadOnlyList<InventarioItemDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<InventarioResultDto<InventarioItemDto>> RegistrarAjusteAsync(
        RegistrarAjusteRequest request,
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<InventarioResultDto<IReadOnlyList<MovimientoDto>>> ObtenerKardexAsync(
        Guid corteId,
        CancellationToken cancellationToken = default);
}
