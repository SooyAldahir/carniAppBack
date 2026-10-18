using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>RF-INV-002, RF-CONFIG-002: catalogo de cortes vendibles.</summary>
[ApiController]
[Route("api/cortes")]
[Authorize]
public class CortesController : ControllerBase
{
    private readonly ICatalogoService _catalogoService;

    public CortesController(ICatalogoService catalogoService)
    {
        _catalogoService = catalogoService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CorteDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await _catalogoService.ListarCortesAsync(cancellationToken));

    [PermisoAuthorize("catalogo.administrar")]
    [HttpPost]
    public async Task<IActionResult> Post(GuardarCorteRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _catalogoService.CrearCorteAsync(request, cancellationToken);
        return resultado.Exitoso ? Ok(resultado.Datos) : ResultadoAHttp(resultado.Code, resultado.Mensaje!);
    }

    [PermisoAuthorize("catalogo.administrar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, GuardarCorteRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _catalogoService.ActualizarCorteAsync(id, request, cancellationToken);
        return resultado.Exitoso ? Ok(resultado.Datos) : ResultadoAHttp(resultado.Code, resultado.Mensaje!);
    }

    [PermisoAuthorize("catalogo.administrar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _catalogoService.DesactivarCorteAsync(id, cancellationToken);
        return resultado.Exitoso ? NoContent() : ResultadoAHttp(resultado.Code, resultado.Mensaje!);
    }

    private IActionResult ResultadoAHttp(CatalogoResultCode code, string mensaje) => code switch
    {
        CatalogoResultCode.NoEncontrado => NotFound(new { mensaje }),
        CatalogoResultCode.TipoAnimalNoEncontrado => NotFound(new { mensaje }),
        CatalogoResultCode.NombreDuplicado => Conflict(new { mensaje }),
        CatalogoResultCode.CodigoBarrasDuplicado => Conflict(new { mensaje }),
        CatalogoResultCode.PrecioInvalido => BadRequest(new { mensaje }),
        CatalogoResultCode.UnidadVentaInvalida => BadRequest(new { mensaje }),
        _ => BadRequest(new { mensaje })
    };
}
