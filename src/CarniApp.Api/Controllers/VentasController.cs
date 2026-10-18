using System.Security.Claims;
using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Ventas;
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

    public VentasController(IVentaService ventaService)
    {
        _ventaService = ventaService;
    }

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
        var resultado = await _ventaService.CrearAsync(request, UsuarioActualId(), cancellationToken);
        return resultado.Exitoso
            ? CreatedAtAction(nameof(Obtener), new { id = resultado.Datos!.Id }, resultado.Datos)
            : ResponderConResultado(resultado);
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
            VentaResultCode.VentaNoEncontrada => NotFound(new { mensaje = resultado.Mensaje }),
            VentaResultCode.StockInsuficiente
                or VentaResultCode.SinSucursalActiva
                or VentaResultCode.VentaYaAnulada
                or VentaResultCode.Conflicto => Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
