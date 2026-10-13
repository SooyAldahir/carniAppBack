using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Negocio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>
/// RF-CONFIG-003: configuracion de marca del negocio (nombre, logo,
/// telefono, redes, direccion, moneda, zona horaria).
/// </summary>
[ApiController]
[Route("api/negocio")]
[Authorize]
public class NegocioController : ControllerBase
{
    private readonly INegocioService _negocioService;

    public NegocioController(INegocioService negocioService)
    {
        _negocioService = negocioService;
    }

    /// <summary>Cualquier usuario autenticado puede leer la marca (login, dashboard, tickets).</summary>
    [HttpGet]
    public async Task<ActionResult<NegocioDto>> Get(CancellationToken cancellationToken)
    {
        var negocio = await _negocioService.ObtenerAsync(cancellationToken);
        return negocio is null ? NotFound() : Ok(negocio);
    }

    /// <summary>Solo quien tenga el permiso de configuracion critica puede editar la marca.</summary>
    [PermisoAuthorize("config.critica.editar")]
    [HttpPut]
    public async Task<ActionResult<NegocioDto>> Put(ActualizarNegocioRequest request, CancellationToken cancellationToken)
    {
        var actualizado = await _negocioService.ActualizarAsync(request, cancellationToken);
        return Ok(actualizado);
    }
}
