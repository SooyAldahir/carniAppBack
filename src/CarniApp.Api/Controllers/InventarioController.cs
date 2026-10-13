using System.Security.Claims;
using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Inventario;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>RF-INV-003/004/005/008: stock, alertas y kardex de movimientos.</summary>
[ApiController]
[Route("api/inventario")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly IInventarioService _inventarioService;

    public InventarioController(IInventarioService inventarioService)
    {
        _inventarioService = inventarioService;
    }

    [PermisoAuthorize("inventario.consultar")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventarioItemDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await _inventarioService.ListarAsync(cancellationToken));

    [PermisoAuthorize("inventario.consultar")]
    [HttpGet("{corteId:guid}/movimientos")]
    public async Task<IActionResult> GetMovimientos(Guid corteId, CancellationToken cancellationToken)
    {
        var resultado = await _inventarioService.ObtenerKardexAsync(corteId, cancellationToken);
        return resultado.Exitoso ? Ok(resultado.Datos) : NotFound(new { mensaje = resultado.Mensaje });
    }

    [PermisoAuthorize("inventario.ajustar")]
    [HttpPost("ajustar")]
    public async Task<IActionResult> Ajustar(RegistrarAjusteRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var resultado = await _inventarioService.RegistrarAjusteAsync(request, usuarioId, cancellationToken);

        if (resultado.Exitoso) return Ok(resultado.Datos);

        return resultado.Code switch
        {
            InventarioResultCode.CorteNoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            InventarioResultCode.SinSucursalActiva => Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
