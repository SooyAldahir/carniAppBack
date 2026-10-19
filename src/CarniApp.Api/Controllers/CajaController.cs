using System.Security.Claims;
using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Caja;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>RF-CAJA-001..005: turno de caja, gastos y corte con conteo de efectivo.</summary>
[ApiController]
[Route("api/caja")]
[Authorize]
public class CajaController : ControllerBase
{
    private readonly ICajaService _service;

    public CajaController(ICajaService service)
    {
        _service = service;
    }

    private Guid UsuarioActualId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [PermisoAuthorize("caja.cortar")]
    [HttpGet("actual")]
    public async Task<IActionResult> Actual(CancellationToken cancellationToken) =>
        Responder(await _service.ObtenerEstadoAsync(cancellationToken));

    [PermisoAuthorize("caja.cortar")]
    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir(AbrirCajaRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.AbrirAsync(request, UsuarioActualId(), cancellationToken));

    [PermisoAuthorize("caja.cortar")]
    [HttpPost("gastos")]
    public async Task<IActionResult> Gasto(GastoCajaRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.RegistrarGastoAsync(request, UsuarioActualId(), cancellationToken));

    [PermisoAuthorize("caja.cortar")]
    [HttpPost("cerrar")]
    public async Task<IActionResult> Cerrar(CerrarCajaRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.CerrarAsync(request, UsuarioActualId(), cancellationToken));

    [PermisoAuthorize("caja.cortar")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CorteCajaResumenDto>>> Listar([FromQuery] int limite = 30, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListarAsync(limite, cancellationToken));

    [PermisoAuthorize("caja.cortar")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id, CancellationToken cancellationToken) =>
        Responder(await _service.ObtenerAsync(id, cancellationToken));

    [PermisoAuthorize("caja.cortar")]
    [HttpGet("{id:guid}/ticket")]
    public async Task<IActionResult> Ticket(Guid id, CancellationToken cancellationToken) =>
        Responder(await _service.ObtenerTicketAsync(id, cancellationToken));

    [PermisoAuthorize("caja.reabrir")]
    [HttpPost("{id:guid}/reabrir")]
    public async Task<IActionResult> Reabrir(Guid id, CancellationToken cancellationToken) =>
        Responder(await _service.ReabrirAsync(id, cancellationToken));

    private IActionResult Responder<T>(CajaResultDto<T> resultado)
    {
        if (resultado.Exitoso) return Ok(resultado.Datos);

        return resultado.Code switch
        {
            CajaResultCode.CorteNoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            CajaResultCode.YaHayCajaAbierta
                or CajaResultCode.SinCajaAbierta
                or CajaResultCode.CorteNoCerrado
                or CajaResultCode.NoEsElUltimoCorte
                or CajaResultCode.SinSucursalActiva => Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
