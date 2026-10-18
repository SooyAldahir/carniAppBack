using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.Inventario;

/// <summary>
/// RF-INV-003/004/005/008: stock por corte, alertas de stock minimo y
/// kardex de movimientos. Los ajustes manuales (Entrada/Merma/Ajuste)
/// siempre piden motivo (RF-INV-005) y quedan atados al usuario que
/// los hizo. El endpoint solo lo puede llamar quien tenga el permiso
/// 'inventario.ajustar' (ver PermisoAuthorize en el controller), asi
/// que la condicion de RN-006 (autorizacion de Super Administrador +
/// motivo obligatorio para dejar stock en negativo) ya queda cubierta
/// sin necesidad de un flag extra.
///
/// MVP de una sola sucursal (D-01): opera siempre sobre la primera
/// Sucursal activa. El modelo ya soporta varias (SUP-01) para cuando
/// se necesite seleccionar sucursal en un sprint posterior.
/// </summary>
public class InventarioService : IInventarioService
{
    private static readonly HashSet<string> TiposMovimientoManual = new(StringComparer.OrdinalIgnoreCase)
    {
        "Entrada", "Merma", "Ajuste"
    };

    private readonly IInventarioRepository _inventarioRepository;
    private readonly IMovimientoInventarioRepository _movimientoRepository;
    private readonly ICorteRepository _corteRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public InventarioService(
        IInventarioRepository inventarioRepository,
        IMovimientoInventarioRepository movimientoRepository,
        ICorteRepository corteRepository,
        IRepository<Sucursal> sucursalRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _inventarioRepository = inventarioRepository;
        _movimientoRepository = movimientoRepository;
        _corteRepository = corteRepository;
        _sucursalRepository = sucursalRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyList<InventarioItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);
        if (sucursal is null) return Array.Empty<InventarioItemDto>();

        var cortes = await _corteRepository.GetAllConTipoAnimalAsync(cancellationToken);
        var inventarios = await _inventarioRepository.ObtenerConCorteAsync(sucursal.Id, cancellationToken);
        var stockPorCorte = inventarios.ToDictionary(i => i.CorteId, i => i.StockActual);

        return cortes
            .Select(corte =>
            {
                var stock = stockPorCorte.GetValueOrDefault(corte.Id, 0m);
                return new InventarioItemDto(
                    corte.Id,
                    corte.Nombre,
                    corte.UnidadVenta.ToString(),
                    stock,
                    corte.StockMinimo,
                    stock < corte.StockMinimo);
            })
            .OrderBy(i => i.CorteNombre)
            .ToList();
    }

    public async Task<InventarioResultDto<InventarioItemDto>> RegistrarAjusteAsync(
        RegistrarAjusteRequest request,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return InventarioResultDto<InventarioItemDto>.Fail(InventarioResultCode.MotivoRequerido, "El motivo es obligatorio para cualquier ajuste de inventario.");
        }

        if (request.Cantidad == 0)
        {
            return InventarioResultDto<InventarioItemDto>.Fail(InventarioResultCode.CantidadInvalida, "La cantidad no puede ser cero.");
        }

        if (!TiposMovimientoManual.Contains(request.TipoMovimiento))
        {
            return InventarioResultDto<InventarioItemDto>.Fail(
                InventarioResultCode.TipoMovimientoInvalido,
                "Tipo de movimiento invalido. Usa 'Entrada', 'Merma' o 'Ajuste'.");
        }

        // El signo debe ser coherente con el tipo: una Entrada suma, una Merma resta.
        // ('Ajuste' puede ser en cualquier sentido.) Evita que un cliente mal hecho
        // registre una merma positiva y la deje SUMANDO stock.
        var esEntrada = request.TipoMovimiento.Equals("Entrada", StringComparison.OrdinalIgnoreCase);
        var esMerma = request.TipoMovimiento.Equals("Merma", StringComparison.OrdinalIgnoreCase);
        if ((esEntrada && request.Cantidad < 0) || (esMerma && request.Cantidad > 0))
        {
            return InventarioResultDto<InventarioItemDto>.Fail(
                InventarioResultCode.CantidadInvalida,
                esEntrada ? "Una entrada debe tener cantidad positiva." : "Una merma debe tener cantidad negativa.");
        }

        var corte = await _corteRepository.GetByIdConTipoAnimalAsync(request.CorteId, cancellationToken);
        if (corte is null)
        {
            return InventarioResultDto<InventarioItemDto>.Fail(InventarioResultCode.CorteNoEncontrado, "El corte indicado no existe.");
        }

        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);
        if (sucursal is null)
        {
            return InventarioResultDto<InventarioItemDto>.Fail(InventarioResultCode.SinSucursalActiva, "No hay ninguna sucursal activa configurada.");
        }

        // No se llama a _inventarioRepository.Update(inventario) aqui: la entidad ya
        // esta siendo rastreada por el DbContext (recien creada como 'Added' por
        // ObtenerOCrearAsync, o ya 'Unchanged' si existia), asi que EF detecta estos
        // cambios de propiedades solo con SaveChangesAsync. Llamar a Update() fuerza
        // el estado a 'Modified' incluso cuando acaba de crearse, lo que generaba un
        // UPDATE sobre una fila que nunca se inserto (0 filas afectadas) y lanzaba
        // DbUpdateConcurrencyException en el primer ajuste de cada corte.
        var inventario = await _inventarioRepository.ObtenerOCrearAsync(corte.Id, sucursal.Id, cancellationToken);
        inventario.StockActual += request.Cantidad;
        inventario.FechaActualizacion = _dateTimeProvider.UtcNow;

        var movimiento = new MovimientoInventario
        {
            CorteId = corte.Id,
            SucursalId = sucursal.Id,
            TipoMovimiento = Enum.Parse<TipoMovimientoInventario>(request.TipoMovimiento, ignoreCase: true),
            Cantidad = request.Cantidad,
            Motivo = request.Motivo.Trim(),
            UsuarioId = usuarioId,
            Fecha = _dateTimeProvider.UtcNow
        };
        await _movimientoRepository.AddAsync(movimiento, cancellationToken);

        // Una sola transaccion: si algo falla, ni el stock ni el movimiento quedan a medias.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new InventarioItemDto(
            corte.Id,
            corte.Nombre,
            corte.UnidadVenta.ToString(),
            inventario.StockActual,
            corte.StockMinimo,
            inventario.StockActual < corte.StockMinimo);

        return InventarioResultDto<InventarioItemDto>.Ok(dto);
    }

    public async Task<InventarioResultDto<IReadOnlyList<MovimientoDto>>> ObtenerKardexAsync(Guid corteId, CancellationToken cancellationToken = default)
    {
        var corte = await _corteRepository.GetByIdAsync(corteId, cancellationToken);
        if (corte is null)
        {
            return InventarioResultDto<IReadOnlyList<MovimientoDto>>.Fail(InventarioResultCode.CorteNoEncontrado, "El corte indicado no existe.");
        }

        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);
        if (sucursal is null)
        {
            return InventarioResultDto<IReadOnlyList<MovimientoDto>>.Ok(Array.Empty<MovimientoDto>());
        }

        var movimientos = await _movimientoRepository.ObtenerPorCorteAsync(corteId, sucursal.Id, 100, cancellationToken);

        var dtos = movimientos
            .Select(m => new MovimientoDto(
                m.Id,
                m.TipoMovimiento.ToString(),
                m.Cantidad,
                m.Motivo,
                m.Usuario?.NombreCompleto,
                m.Fecha))
            .ToList();

        return InventarioResultDto<IReadOnlyList<MovimientoDto>>.Ok(dtos);
    }

    private async Task<Sucursal?> ObtenerSucursalPrincipalAsync(CancellationToken cancellationToken)
    {
        var sucursales = await _sucursalRepository.GetAllAsync(cancellationToken);
        return sucursales.FirstOrDefault(s => s.EsPrincipal) ?? sucursales.FirstOrDefault();
    }
}
