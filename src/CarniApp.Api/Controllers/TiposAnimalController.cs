using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>RF-INV-001: catalogo configurable de tipos de animal.</summary>
[ApiController]
[Route("api/tipos-animal")]
[Authorize]
public class TiposAnimalController : ControllerBase
{
    private readonly ICatalogoService _catalogoService;

    public TiposAnimalController(ICatalogoService catalogoService)
    {
        _catalogoService = catalogoService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TipoAnimalDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await _catalogoService.ListarTiposAnimalAsync(cancellationToken));

    [PermisoAuthorize("catalogo.administrar")]
    [HttpPost]
    public async Task<IActionResult> Post(GuardarTipoAnimalRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _catalogoService.CrearTipoAnimalAsync(request, cancellationToken);
        return resultado.Exitoso ? Ok(resultado.Datos) : ResultadoAHttp(resultado.Code, resultado.Mensaje!);
    }

    [PermisoAuthorize("catalogo.administrar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, GuardarTipoAnimalRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _catalogoService.ActualizarTipoAnimalAsync(id, request, cancellationToken);
        return resultado.Exitoso ? Ok(resultado.Datos) : ResultadoAHttp(resultado.Code, resultado.Mensaje!);
    }

    [PermisoAuthorize("catalogo.administrar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _catalogoService.DesactivarTipoAnimalAsync(id, cancellationToken);
        return resultado.Exitoso ? NoContent() : ResultadoAHttp(resultado.Code, resultado.Mensaje!);
    }

    private IActionResult ResultadoAHttp(CatalogoResultCode code, string mensaje) => code switch
    {
        CatalogoResultCode.NoEncontrado => NotFound(new { mensaje }),
        CatalogoResultCode.NombreDuplicado => Conflict(new { mensaje }),
        CatalogoResultCode.TipoAnimalConCortesActivos => Conflict(new { mensaje }),
        _ => BadRequest(new { mensaje })
    };
}
