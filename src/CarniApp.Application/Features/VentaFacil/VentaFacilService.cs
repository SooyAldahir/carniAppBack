using CarniApp.Application.Common.Exceptions;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.VentaFacil;

/// <summary>
/// Venta Facil (RF-VF): se registra un animal (tipo, peso, costo) y el sistema
/// lo reparte en cortes con los porcentajes de rendimiento configurados; el
/// reparto entra al inventario como ESTIMADO para poder vender de inmediato.
/// Lo que no se asigna a ningun corte (hueso, desperdicio) es merma esperada.
/// </summary>
public class VentaFacilService : IVentaFacilService
{
    private const decimal PesoMaximoKg = 10000m;

    private readonly ITipoAnimalRepository _tipoRepository;
    private readonly ICorteRepository _corteRepository;
    private readonly ILoteAnimalRepository _loteRepository;
    private readonly IInventarioRepository _inventarioRepository;
    private readonly IMovimientoInventarioRepository _movimientoRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public VentaFacilService(
        ITipoAnimalRepository tipoRepository,
        ICorteRepository corteRepository,
        ILoteAnimalRepository loteRepository,
        IInventarioRepository inventarioRepository,
        IMovimientoInventarioRepository movimientoRepository,
        IRepository<Sucursal> sucursalRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _tipoRepository = tipoRepository;
        _corteRepository = corteRepository;
        _loteRepository = loteRepository;
        _inventarioRepository = inventarioRepository;
        _movimientoRepository = movimientoRepository;
        _sucursalRepository = sucursalRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<VentaFacilResultDto<RendimientosDto>> ObtenerRendimientosAsync(Guid tipoAnimalId, CancellationToken cancellationToken = default)
    {
        var tipo = await _tipoRepository.GetByIdAsync(tipoAnimalId, cancellationToken);
        if (tipo is null || !tipo.Activo) return TipoNoEncontrado<RendimientosDto>();

        return VentaFacilResultDto<RendimientosDto>.Ok(await ArmarRendimientosAsync(tipoAnimalId, cancellationToken));
    }

    public async Task<VentaFacilResultDto<RendimientosDto>> GuardarRendimientosAsync(GuardarRendimientosRequest request, CancellationToken cancellationToken = default)
    {
        var tipo = await _tipoRepository.GetByIdAsync(request.TipoAnimalId, cancellationToken);
        if (tipo is null || !tipo.Activo) return TipoNoEncontrado<RendimientosDto>();

        var cortes = (await _corteRepository.ObtenerActivosPorTipoAnimalAsync(tipo.Id, cancellationToken))
            .Where(c => c.UnidadVenta == UnidadVenta.Peso)
            .ToDictionary(c => c.Id);

        var pedidos = new Dictionary<Guid, decimal?>();
        foreach (var entrada in request.Cortes)
        {
            if (!cortes.ContainsKey(entrada.CorteId) || !pedidos.TryAdd(entrada.CorteId, entrada.Porcentaje))
            {
                return VentaFacilResultDto<RendimientosDto>.Fail(
                    VentaFacilResultCode.CorteInvalido,
                    "Hay un corte que no pertenece a ese tipo de animal, no se vende por peso o esta repetido.");
            }

            if (entrada.Porcentaje is < 0 or > 100)
            {
                return VentaFacilResultDto<RendimientosDto>.Fail(
                    VentaFacilResultCode.PorcentajeInvalido,
                    "Cada porcentaje debe estar entre 0 y 100.");
            }
        }

        // Los cortes que no vienen en la peticion conservan su valor actual.
        var total = cortes.Values.Sum(c => pedidos.TryGetValue(c.Id, out var nuevo) ? nuevo ?? 0m : c.RendimientoPorcentaje ?? 0m);
        if (total > 100m)
        {
            return VentaFacilResultDto<RendimientosDto>.Fail(
                VentaFacilResultCode.SumaExcede100,
                $"Los porcentajes suman {total:0.##}%; no pueden pasar de 100%.");
        }

        foreach (var (corteId, porcentaje) in pedidos)
        {
            var corte = cortes[corteId];
            corte.RendimientoPorcentaje = porcentaje is null or 0m ? null : Math.Round(porcentaje.Value, 2, MidpointRounding.AwayFromZero);
            corte.FechaActualizacion = _dateTimeProvider.UtcNow;
            _corteRepository.Update(corte);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return VentaFacilResultDto<RendimientosDto>.Ok(await ArmarRendimientosAsync(tipo.Id, cancellationToken));
    }

    public async Task<VentaFacilResultDto<RepartoDto>> PrevisualizarAsync(LoteRequest request, CancellationToken cancellationToken = default)
    {
        var (reparto, error) = await CalcularRepartoAsync(request, cancellationToken);
        return reparto is null ? error! : VentaFacilResultDto<RepartoDto>.Ok(reparto.Dto);
    }

    public async Task<VentaFacilResultDto<RepartoDto>> RegistrarLoteAsync(LoteRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var (reparto, error) = await CalcularRepartoAsync(request, cancellationToken);
        if (reparto is null) return error!;

        var sucursales = await _sucursalRepository.GetAllAsync(cancellationToken);
        var sucursal = sucursales.FirstOrDefault(s => s.EsPrincipal) ?? sucursales.FirstOrDefault();
        if (sucursal is null)
        {
            return VentaFacilResultDto<RepartoDto>.Fail(VentaFacilResultCode.SinSucursalActiva, "No hay ninguna sucursal activa configurada.");
        }

        var ahora = _dateTimeProvider.UtcNow;
        var lote = new LoteAnimal
        {
            TipoAnimalId = request.TipoAnimalId,
            PesoKg = request.PesoKg,
            PrecioCompra = request.PrecioCompra,
            FechaCompra = request.FechaCompra ?? ahora,
            EsEstimado = true,
            FechaCreacion = ahora
        };
        await _loteRepository.AddAsync(lote, cancellationToken);

        var motivo = $"Venta Facil: lote {lote.Id.ToString()[..8]} de {reparto.Dto.TipoAnimalNombre} (estimado)";
        foreach (var linea in reparto.Dto.Lineas)
        {
            // Sin Update(): la fila de Inventario ya esta rastreada (ver InventarioService).
            var inventario = await _inventarioRepository.ObtenerOCrearAsync(linea.CorteId, sucursal.Id, cancellationToken);
            inventario.StockActual += linea.KgEstimados;
            inventario.FechaActualizacion = ahora;

            await _movimientoRepository.AddAsync(new MovimientoInventario
            {
                CorteId = linea.CorteId,
                SucursalId = sucursal.Id,
                LoteAnimalId = lote.Id,
                TipoMovimiento = TipoMovimientoInventario.Entrada,
                Cantidad = linea.KgEstimados,
                Motivo = motivo,
                UsuarioId = usuarioId,
                Fecha = ahora
            }, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return VentaFacilResultDto<RepartoDto>.Fail(
                VentaFacilResultCode.Conflicto,
                "El inventario cambio mientras se registraba el lote. Intenta de nuevo.");
        }

        return VentaFacilResultDto<RepartoDto>.Ok(reparto.Dto);
    }

    public async Task<IReadOnlyList<LoteDto>> ListarLotesAsync(CancellationToken cancellationToken = default)
    {
        var lotes = await _loteRepository.ObtenerRecientesAsync(50, cancellationToken);
        return lotes
            .Select(l => new LoteDto(
                l.Id,
                l.TipoAnimalId,
                l.TipoAnimal.Nombre,
                l.PesoKg,
                l.PrecioCompra,
                CostoPorKg(l.PrecioCompra, l.PesoKg),
                l.FechaCompra,
                l.EsEstimado))
            .ToList();
    }

    // ---------------------------------------------------------------- helpers

    private sealed record Calculo(RepartoDto Dto);

    private async Task<(Calculo? Calculo, VentaFacilResultDto<RepartoDto>? Error)> CalcularRepartoAsync(LoteRequest request, CancellationToken cancellationToken)
    {
        if (request.PesoKg <= 0 || request.PesoKg > PesoMaximoKg)
        {
            return (null, VentaFacilResultDto<RepartoDto>.Fail(VentaFacilResultCode.PesoInvalido, $"El peso debe ser mayor a 0 y no pasar de {PesoMaximoKg:0} kg."));
        }

        if (request.PrecioCompra < 0)
        {
            return (null, VentaFacilResultDto<RepartoDto>.Fail(VentaFacilResultCode.PrecioInvalido, "El costo de compra no puede ser negativo."));
        }

        var tipo = await _tipoRepository.GetByIdAsync(request.TipoAnimalId, cancellationToken);
        if (tipo is null || !tipo.Activo) return (null, TipoNoEncontrado<RepartoDto>());

        var cortes = (await _corteRepository.ObtenerActivosPorTipoAnimalAsync(tipo.Id, cancellationToken))
            .Where(c => c.UnidadVenta == UnidadVenta.Peso && c.RendimientoPorcentaje is > 0)
            .ToList();

        if (cortes.Count == 0)
        {
            return (null, VentaFacilResultDto<RepartoDto>.Fail(
                VentaFacilResultCode.SinRendimientos,
                $"Configura primero los porcentajes de rendimiento de {tipo.Nombre}."));
        }

        var lineas = cortes
            .Select(c =>
            {
                var kg = Math.Round(request.PesoKg * c.RendimientoPorcentaje!.Value / 100m, 3, MidpointRounding.AwayFromZero);
                return new RepartoLineaDto(
                    c.Id,
                    c.Nombre,
                    c.RendimientoPorcentaje.Value,
                    kg,
                    c.PrecioBase,
                    Math.Round(kg * c.PrecioBase, 2, MidpointRounding.AwayFromZero));
            })
            .Where(l => l.KgEstimados > 0)
            .OrderByDescending(l => l.KgEstimados)
            .ToList();

        var asignados = lineas.Sum(l => l.KgEstimados);
        var venta = lineas.Sum(l => l.VentaEstimada);

        var dto = new RepartoDto(
            tipo.Id,
            tipo.Nombre,
            request.PesoKg,
            request.PrecioCompra,
            lineas,
            asignados,
            request.PesoKg - asignados,
            CostoPorKg(request.PrecioCompra, request.PesoKg),
            venta,
            venta - request.PrecioCompra);

        return (new Calculo(dto), null);
    }

    private async Task<RendimientosDto> ArmarRendimientosAsync(Guid tipoAnimalId, CancellationToken cancellationToken)
    {
        var items = (await _corteRepository.ObtenerActivosPorTipoAnimalAsync(tipoAnimalId, cancellationToken))
            .Where(c => c.UnidadVenta == UnidadVenta.Peso)
            .Select(c => new RendimientoItemDto(c.Id, c.Nombre, c.PrecioBase, c.RendimientoPorcentaje))
            .ToList();

        return new RendimientosDto(tipoAnimalId, items, items.Sum(i => i.Porcentaje ?? 0m));
    }

    private static decimal CostoPorKg(decimal precio, decimal peso) =>
        peso <= 0 ? 0 : Math.Round(precio / peso, 2, MidpointRounding.AwayFromZero);

    private static VentaFacilResultDto<T> TipoNoEncontrado<T>() =>
        VentaFacilResultDto<T>.Fail(VentaFacilResultCode.TipoAnimalNoEncontrado, "El tipo de animal indicado no existe.");
}
