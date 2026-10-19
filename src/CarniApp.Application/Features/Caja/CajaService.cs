using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Ventas;
using CarniApp.Domain.Entities;
using NegocioEntidad = CarniApp.Domain.Entities.Negocio;   // 'Negocio' tambien es un namespace hermano

namespace CarniApp.Application.Features.Caja;

/// <summary>
/// RF-CAJA-001..005: apertura de turno con fondo, gastos de caja y corte con conteo de
/// efectivo. Las ventas del turno son las que caen entre FechaInicio y FechaCierre; las
/// anuladas no cuentan. Los abonos de credito se toman como efectivo. Un corte cerrado
/// bloquea la anulacion de sus ventas (ver VentaService) hasta que se reabra.
/// MVP de una sola sucursal: opera sobre la sucursal principal.
/// </summary>
public class CajaService : ICajaService
{
    private static readonly string[] OrdenMetodos = { "Efectivo", "Tarjeta", "Transferencia", "Credito" };

    private readonly ICorteCajaRepository _corteRepository;
    private readonly IVentaRepository _ventaRepository;
    private readonly IAbonoCreditoRepository _abonoRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly IRepository<NegocioEntidad> _negocioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CajaService(
        ICorteCajaRepository corteRepository,
        IVentaRepository ventaRepository,
        IAbonoCreditoRepository abonoRepository,
        IRepository<Sucursal> sucursalRepository,
        IRepository<NegocioEntidad> negocioRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _corteRepository = corteRepository;
        _ventaRepository = ventaRepository;
        _abonoRepository = abonoRepository;
        _sucursalRepository = sucursalRepository;
        _negocioRepository = negocioRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<CajaResultDto<EstadoCajaDto>> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);
        if (sucursal is null)
        {
            return CajaResultDto<EstadoCajaDto>.Fail(CajaResultCode.SinSucursalActiva, "No hay ninguna sucursal activa configurada.");
        }

        var corte = await _corteRepository.ObtenerAbiertoAsync(sucursal.Id, cancellationToken);
        if (corte is null) return CajaResultDto<EstadoCajaDto>.Ok(new EstadoCajaDto(false, null));

        return CajaResultDto<EstadoCajaDto>.Ok(new EstadoCajaDto(true, await MapearAsync(corte, cancellationToken)));
    }

    public async Task<CajaResultDto<CorteCajaDto>> AbrirAsync(AbrirCajaRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (request.FondoInicial < 0 || request.FondoInicial > 1_000_000m)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.ImporteInvalido, "El fondo inicial no puede ser negativo.");
        }

        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);
        if (sucursal is null)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.SinSucursalActiva, "No hay ninguna sucursal activa configurada.");
        }

        if (await _corteRepository.ObtenerAbiertoAsync(sucursal.Id, cancellationToken) is not null)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.YaHayCajaAbierta, "Ya hay un turno de caja abierto. Ciérralo antes de abrir otro.");
        }

        var fondo = Redondear(request.FondoInicial);
        var corte = new CorteCaja
        {
            SucursalId = sucursal.Id,
            UsuarioId = usuarioId,
            FechaInicio = _dateTimeProvider.UtcNow,
            FondoInicial = fondo,
            EfectivoEsperado = fondo,
            Estado = EstadoCorteCaja.Abierto
        };
        await _corteRepository.AddAsync(corte, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CajaResultDto<CorteCajaDto>.Ok(await MapearAsync(await Recargar(corte.Id, cancellationToken), cancellationToken));
    }

    public async Task<CajaResultDto<CorteCajaDto>> RegistrarGastoAsync(GastoCajaRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var concepto = request.Concepto?.Trim() ?? string.Empty;
        if (concepto.Length == 0 || concepto.Length > 100)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.ConceptoInvalido, "El concepto del gasto es obligatorio (maximo 100 caracteres).");
        }

        if (request.Importe <= 0 || request.Importe > 1_000_000m)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.ImporteInvalido, "El importe del gasto debe ser mayor a cero.");
        }

        var abierto = await ObtenerAbiertoAsync(cancellationToken);
        if (abierto.Corte is null) return abierto.Error!;
        var corte = abierto.Corte;

        // AddAsync (no corte.Gastos.Add): con Id ya asignado EF podria tratarlo como existente.
        await _corteRepository.AddGastoAsync(new GastoCaja
        {
            CorteCajaId = corte.Id,
            Concepto = concepto,
            Importe = Redondear(request.Importe),
            UsuarioId = usuarioId,
            Fecha = _dateTimeProvider.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CajaResultDto<CorteCajaDto>.Ok(await MapearAsync(await Recargar(corte.Id, cancellationToken), cancellationToken));
    }

    public async Task<CajaResultDto<CorteCajaDto>> CerrarAsync(CerrarCajaRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (request.EfectivoContado < 0 || request.EfectivoContado > 100_000_000m)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.ImporteInvalido, "El efectivo contado no puede ser negativo.");
        }

        var notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
        if (notas is { Length: > 300 })
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.NotasInvalidas, "Las notas no pueden pasar de 300 caracteres.");
        }

        var abierto = await ObtenerAbiertoAsync(cancellationToken);
        if (abierto.Corte is null) return abierto.Error!;
        var corte = abierto.Corte;

        var ahora = _dateTimeProvider.UtcNow;
        var calculo = await CalcularAsync(corte, ahora, cancellationToken);
        var contado = Redondear(request.EfectivoContado);

        corte.FechaCierre = ahora;
        corte.TotalVentas = calculo.TotalVentas;
        corte.TotalGastos = calculo.TotalGastos;
        corte.EfectivoEsperado = calculo.EfectivoEsperado;
        corte.EfectivoContado = contado;
        corte.Diferencia = contado - calculo.EfectivoEsperado;
        corte.Notas = notas;
        corte.Estado = EstadoCorteCaja.Cerrado;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CajaResultDto<CorteCajaDto>.Ok(await MapearAsync(corte, cancellationToken));
    }

    public async Task<CajaResultDto<CorteCajaDto>> ReabrirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var corte = await _corteRepository.ObtenerPorIdAsync(id, cancellationToken);
        if (corte is null)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.CorteNoEncontrado, "El corte no existe.");
        }

        if (corte.Estado != EstadoCorteCaja.Cerrado)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.CorteNoCerrado, "Ese corte no esta cerrado.");
        }

        if (await _corteRepository.ObtenerAbiertoAsync(corte.SucursalId, cancellationToken) is not null)
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.YaHayCajaAbierta, "Ya hay un turno abierto; ciérralo antes de reabrir otro.");
        }

        if (await _corteRepository.ExisteCortePosteriorAsync(corte.SucursalId, corte.FechaInicio, cancellationToken))
        {
            return CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.NoEsElUltimoCorte, "Solo se puede reabrir el ultimo corte.");
        }

        corte.Estado = EstadoCorteCaja.Abierto;
        corte.FechaCierre = null;
        corte.EfectivoContado = null;
        corte.Diferencia = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CajaResultDto<CorteCajaDto>.Ok(await MapearAsync(corte, cancellationToken));
    }

    public async Task<CajaResultDto<CorteCajaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var corte = await _corteRepository.ObtenerPorIdAsync(id, cancellationToken);
        return corte is null
            ? CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.CorteNoEncontrado, "El corte no existe.")
            : CajaResultDto<CorteCajaDto>.Ok(await MapearAsync(corte, cancellationToken));
    }

    public async Task<IReadOnlyList<CorteCajaResumenDto>> ListarAsync(int limite, CancellationToken cancellationToken = default)
    {
        var cortes = await _corteRepository.ListarRecientesAsync(Math.Clamp(limite, 1, 100), cancellationToken);
        return cortes
            .Select(c =>
            {
                var cerrado = c.Estado == EstadoCorteCaja.Cerrado;
                return new CorteCajaResumenDto(
                    c.Id,
                    c.Estado,
                    c.Usuario?.NombreCompleto,
                    c.FechaInicio,
                    c.FechaCierre,
                    cerrado ? c.TotalVentas : null,
                    cerrado ? c.EfectivoEsperado : null,
                    c.EfectivoContado,
                    c.Diferencia);
            })
            .ToList();
    }

    public async Task<CajaResultDto<TicketDto>> ObtenerTicketAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var corte = await _corteRepository.ObtenerPorIdAsync(id, cancellationToken);
        if (corte is null)
        {
            return CajaResultDto<TicketDto>.Fail(CajaResultCode.CorteNoEncontrado, "El corte no existe.");
        }

        var negocio = (await _negocioRepository.GetAllAsync(cancellationToken)).FirstOrDefault();
        var zona = VentaService.ResolverZonaHoraria(negocio?.ZonaHoraria);
        var dto = await MapearAsync(corte, cancellationToken);

        return CajaResultDto<TicketDto>.Ok(new TicketDto(CorteTicketBuilder.Construir(dto, negocio, zona)));
    }

    // ---------------------------------------------------------------- helpers

    private sealed record Calculo(
        int NumeroVentas,
        int VentasAnuladas,
        decimal TotalVentas,
        IReadOnlyList<MetodoTotalDto> PorMetodo,
        decimal VentasEfectivo,
        decimal AbonosEfectivo,
        decimal TotalGastos,
        decimal EfectivoEsperado);

    private async Task<(CorteCaja? Corte, CajaResultDto<CorteCajaDto>? Error)> ObtenerAbiertoAsync(CancellationToken cancellationToken)
    {
        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);
        if (sucursal is null)
        {
            return (null, CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.SinSucursalActiva, "No hay ninguna sucursal activa configurada."));
        }

        var corte = await _corteRepository.ObtenerAbiertoAsync(sucursal.Id, cancellationToken);
        return corte is null
            ? (null, CajaResultDto<CorteCajaDto>.Fail(CajaResultCode.SinCajaAbierta, "No hay un turno de caja abierto."))
            : (corte, null);
    }

    private async Task<CorteCaja> Recargar(Guid id, CancellationToken cancellationToken) =>
        (await _corteRepository.ObtenerPorIdAsync(id, cancellationToken))!;

    private async Task<Calculo> CalcularAsync(CorteCaja corte, DateTime hastaUtc, CancellationToken cancellationToken)
    {
        // El limite superior es exclusivo en los repositorios; +1 tick incluye lo ocurrido en 'hasta'.
        var limite = hastaUtc.AddTicks(1);
        var ventas = await _ventaRepository.ListarConPagosPorRangoAsync(corte.SucursalId, corte.FechaInicio, limite, cancellationToken);
        var abonos = await _abonoRepository.ListarPorRangoAsync(corte.FechaInicio, limite, cancellationToken);

        var completadas = ventas.Where(v => v.Estado == EstadoVenta.Completada).ToList();
        var porMetodo = completadas
            .SelectMany(v => v.Pagos)
            .GroupBy(p => p.MetodoPago.Nombre)
            .Select(g => new MetodoTotalDto(g.Key, g.Sum(p => p.Importe)))
            .OrderBy(m => IndiceMetodo(m.Metodo))
            .ThenBy(m => m.Metodo)
            .ToList();

        var totalVentas = completadas.Sum(v => v.Total);
        var ventasEfectivo = porMetodo
            .Where(m => m.Metodo.Equals(MetodoPago.NombreEfectivo, StringComparison.OrdinalIgnoreCase))
            .Sum(m => m.Importe);
        var abonosEfectivo = abonos.Sum(a => a.Importe);
        var gastos = corte.Gastos.Sum(g => g.Importe);

        return new Calculo(
            completadas.Count,
            ventas.Count - completadas.Count,
            totalVentas,
            porMetodo,
            ventasEfectivo,
            abonosEfectivo,
            gastos,
            corte.FondoInicial + ventasEfectivo + abonosEfectivo - gastos);
    }

    private static int IndiceMetodo(string nombre)
    {
        var i = Array.FindIndex(OrdenMetodos, m => m.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? OrdenMetodos.Length : i;
    }

    private async Task<CorteCajaDto> MapearAsync(CorteCaja corte, CancellationToken cancellationToken)
    {
        var hasta = corte.FechaCierre ?? _dateTimeProvider.UtcNow;
        var c = await CalcularAsync(corte, hasta, cancellationToken);

        var gastos = corte.Gastos
            .OrderBy(g => g.Fecha)
            .Select(g => new GastoCajaDto(g.Id, g.Concepto, g.Importe, g.Usuario?.NombreCompleto, g.Fecha))
            .ToList();

        return new CorteCajaDto(
            corte.Id,
            corte.Estado,
            corte.Usuario?.NombreCompleto,
            corte.FechaInicio,
            corte.FechaCierre,
            corte.FondoInicial,
            c.NumeroVentas,
            c.VentasAnuladas,
            c.TotalVentas,
            c.PorMetodo,
            c.VentasEfectivo,
            c.AbonosEfectivo,
            c.TotalGastos,
            gastos,
            c.EfectivoEsperado,
            corte.EfectivoContado,
            corte.Diferencia,
            corte.Notas);
    }

    private async Task<Sucursal?> ObtenerSucursalPrincipalAsync(CancellationToken cancellationToken)
    {
        var sucursales = await _sucursalRepository.GetAllAsync(cancellationToken);
        return sucursales.FirstOrDefault(s => s.EsPrincipal) ?? sucursales.FirstOrDefault();
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
