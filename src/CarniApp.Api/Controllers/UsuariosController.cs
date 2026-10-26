using System.Security.Claims;
using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>Administracion del personal: cajeros y demas usuarios, sus roles y contrasenas.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [PermisoAuthorize("usuarios.administrar")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioAdminDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await _usuarioService.ListarAsync(cancellationToken));

    [PermisoAuthorize("usuarios.administrar")]
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RolAdminDto>>> GetRoles(CancellationToken cancellationToken) =>
        Ok(await _usuarioService.ListarRolesAsync(cancellationToken));

    [PermisoAuthorize("usuarios.administrar")]
    [HttpPost]
    public async Task<IActionResult> Post(CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        var r = await _usuarioService.CrearAsync(request, cancellationToken);
        return r.Exitoso ? Ok(r.Datos) : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    [PermisoAuthorize("usuarios.administrar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, ActualizarUsuarioRequest request, CancellationToken cancellationToken)
    {
        var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var r = await _usuarioService.ActualizarAsync(id, request, actorId, cancellationToken);
        return r.Exitoso ? Ok(r.Datos) : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    [PermisoAuthorize("usuarios.administrar")]
    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> RestablecerPassword(Guid id, RestablecerPasswordRequest request, CancellationToken cancellationToken)
    {
        var r = await _usuarioService.RestablecerPasswordAsync(id, request.NuevaPassword, cancellationToken);
        return r.Exitoso ? NoContent() : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    [PermisoAuthorize("usuarios.administrar")]
    [HttpPost("{id:guid}/desbloquear")]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken cancellationToken)
    {
        var r = await _usuarioService.DesbloquearAsync(id, cancellationToken);
        return r.Exitoso ? Ok(r.Datos) : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    private IActionResult ResultadoAHttp(UsuarioResultCode code, string mensaje) => code switch
    {
        UsuarioResultCode.NoEncontrado => NotFound(new { mensaje }),
        UsuarioResultCode.NombreUsuarioDuplicado => Conflict(new { mensaje }),
        UsuarioResultCode.NoPermitido => Conflict(new { mensaje }),
        _ => BadRequest(new { mensaje })
    };
}
