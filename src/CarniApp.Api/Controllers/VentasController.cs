using System.Security.Claims;
using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Ventas;
using CarniApp.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>RF-POS-001/002/008/011: punto de venta (ventas, catalogo para vender, anulacion).</summary>
[ApiController]
[Route("api/ventas")]
[Authorize]
public class VentasController : ControllerBase
{
    private readonly IVentaService _ventaService;
    private readonly IPosSettings _posSettings;

    public VentasController(IVentaService ventaService, IPosSettings posSettings)
    {
        _ventaService = ventaService;
        _posSettings = posSettings;
    }

    /// <summary>Reglas que la app muestra al cobrar (cargo por credito, tope de descuento).</summary>
    [PermisoAuthorize("pos.vender")]
    [HttpGet("configuracion")]
    public ActionResult<PosConfigDto> Configuracion() =>
        Ok(new PosConfigDto(_posSettings.RecargoCreditoPorcentaje, _posSettings.DescuentoMaximoSinAutorizacionPorcentaje));

    /// <summary>Cortes vendibles con precio y stock actual.</summary>
    [PermisoAuthorize("pos.vender")]
    [HttpGet("catalogo")]
    public async Task<ActionResult<IReadOnlyList<CortePosDto>>> Catalogo(CancellationToken cancellationToken) =>
        Ok(await _ventaService.ObtenerCatalogoAsync(cancellationToken));

    [PermisoAuthorize("pos.vender")]
    [HttpGet("metodos-pago")]
    public async Task<ActionResult<IReadOnlyList<MetodoPagoDto>>> MetodosPago(CancellationToken cancellationToken) =>
        Ok(await _ventaService.ListarMetodosPagoAsync(cancellationToken));

    /// <summary>Ventas en [desde, hasta) (UTC). Sin parametros: el dia UTC actual.</summary>
    [PermisoAuthorize("pos.vender")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VentaResumenDto>>> Listar(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        CancellationToken cancellationToken) =>
        Ok(await _ventaService.ListarAsync(
            desde?.ToUniversalTime(),
            hasta?.ToUniversalTime(),
            cancellationToken));

    [PermisoAuthorize("pos.vender")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken) =>
        ResponderConResultado(await _ventaService.ObtenerAsync(id, cancellationToken));

    [PermisoAuthorize("pos.vender")]
    [HttpPost]
    public async Task<IActionResult> Crear(CrearVentaRequest request, CancellationToken cancellationToken)
    {
        var permisos = User.FindAll(TokenService.PermisoClaimType).Select(c => c.Value).ToList();
        var resultado = await _ventaService.CrearAsync(request, UsuarioActualId(), permisos, cancellationToken);
        return resultado.Exitoso
            ? CreatedAtAction(nameof(Obtener), new { id = resultado.Datos!.Id }, resultado.Datos)
            : ResponderConResultado(resultado);
    }

    /// <summary>RF-POS-010: ticket en texto plano de 32 columnas (digital hoy, impresion en el sprint de hardware).</summary>
    [PermisoAuthorize("pos.vender")]
    [HttpGet("{id:guid}/ticket")]
    public async Task<IActionResult> Ticket(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _ventaService.ObtenerTicketAsync(id, cancellationToken);
        return resultado.Exitoso ? Ok(resultado.Datos) : NotFound(new { mensaje = resultado.Mensaje });
    }

    [PermisoAuthorize("pos.anular")]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Anular(Guid id, AnularVentaRequest request, CancellationToken cancellationToken) =>
        ResponderConResultado(await _ventaService.AnularAsync(id, request.Motivo, UsuarioActualId(), cancellationToken));

    private Guid UsuarioActualId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private IActionResult ResponderConResultado(VentaResultDto<VentaDto> resultado)
    {
        if (resultado.Exitoso) return Ok(resultado.Datos);

        return resultado.Code switch
        {
            VentaResultCode.VentaNoEncontrada or VentaResultCode.ClienteNoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            VentaResultCode.DescuentoNoAutorizado => StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            VentaResultCode.StockInsuficiente
                or VentaResultCode.SinSucursalActiva
                or VentaResultCode.VentaYaAnulada
                or VentaResultCode.CreditoExcedido
                or VentaResultCode.Conflicto => Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
