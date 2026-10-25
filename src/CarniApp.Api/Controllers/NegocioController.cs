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
        var error = Validar(request);
        if (error is not null) return BadRequest(new { mensaje = error });

        var actualizado = await _negocioService.ActualizarAsync(request, cancellationToken);
        return Ok(actualizado);
    }

    private static string? Validar(ActualizarNegocioRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.NombreComercial) || r.NombreComercial.Trim().Length > 150)
            return "El nombre del negocio es obligatorio (maximo 150 caracteres).";
        if (!string.IsNullOrWhiteSpace(r.ColorPrimario) &&
            !System.Text.RegularExpressions.Regex.IsMatch(r.ColorPrimario.Trim(), "^#[0-9A-Fa-f]{6}$"))
            return "El color debe tener el formato #RRGGBB.";
        if (r.Telefono is { Length: > 30 }) return "El telefono no puede pasar de 30 caracteres.";
        if (r.LogoUrl is { Length: > 500 }) return "La direccion del logo es demasiado larga.";
        if (r.PieTicket is { Length: > 120 })
            return "El mensaje del ticket no puede pasar de 120 caracteres.";
        return null;
    }
}
