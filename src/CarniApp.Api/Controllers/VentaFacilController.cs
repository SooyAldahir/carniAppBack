using System.Security.Claims;
using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.VentaFacil;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>RF-VF: lotes de animal, rendimientos por corte y reparto estimado.</summary>
[ApiController]
[Route("api/venta-facil")]
[Authorize]
public class VentaFacilController : ControllerBase
{
    private readonly IVentaFacilService _service;

    public VentaFacilController(IVentaFacilService service)
    {
        _service = service;
    }

    [PermisoAuthorize("catalogo.administrar")]
    [HttpGet("rendimientos/{tipoAnimalId:guid}")]
    public async Task<IActionResult> GetRendimientos(Guid tipoAnimalId, CancellationToken cancellationToken) =>
        Responder(await _service.ObtenerRendimientosAsync(tipoAnimalId, cancellationToken));

    [PermisoAuthorize("catalogo.administrar")]
    [HttpPut("rendimientos")]
    public async Task<IActionResult> PutRendimientos(GuardarRendimientosRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.GuardarRendimientosAsync(request, cancellationToken));

    [PermisoAuthorize("ventafacil.registrar")]
    [HttpPost("previsualizar")]
    public async Task<IActionResult> Previsualizar(LoteRequest request, CancellationToken cancellationToken) =>
        Responder(await _service.PrevisualizarAsync(request, cancellationToken));

    [PermisoAuthorize("ventafacil.registrar")]
    [HttpPost("lotes")]
    public async Task<IActionResult> RegistrarLote(LoteRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Responder(await _service.RegistrarLoteAsync(request, usuarioId, cancellationToken));
    }

    [PermisoAuthorize("ventafacil.registrar")]
    [HttpGet("lotes")]
    public async Task<ActionResult<IReadOnlyList<LoteDto>>> GetLotes(CancellationToken cancellationToken) =>
        Ok(await _service.ListarLotesAsync(cancellationToken));

    private IActionResult Responder<T>(VentaFacilResultDto<T> resultado)
    {
        if (resultado.Exitoso) return Ok(resultado.Datos);

        return resultado.Code switch
        {
            VentaFacilResultCode.TipoAnimalNoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            VentaFacilResultCode.SinSucursalActiva or VentaFacilResultCode.Conflicto => Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
