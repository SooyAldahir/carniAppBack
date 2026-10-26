using CarniApp.Application.Common.Interfaces;
using System.Security.Claims;
using CarniApp.Application.Features.Auth;
using CarniApp.Application.Features.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>
/// RF-AUTH-001/002/005/007/008: login, refresh (rotacion), logout
/// (revocacion) y alta del primer Super Administrador.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUsuarioService _usuarioService;

    public AuthController(IAuthService authService, IUsuarioService usuarioService)
    {
        _authService = authService;
        _usuarioService = usuarioService;
    }

    /// <summary>
    /// Solo funciona si el sistema no tiene ningun usuario todavia
    /// (instalacion nueva). Crea el primer Super Administrador y entrega
    /// sus tokens, igual que un login, para arrancar sin un paso extra.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("bootstrap-admin")]
    public async Task<IActionResult> BootstrapAdmin(BootstrapAdminRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _authService.BootstrapAdminAsync(request, cancellationToken);
        return ResultadoAHttp(resultado);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _authService.LoginAsync(request, cancellationToken);
        return ResultadoAHttp(resultado);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return ResultadoAHttp(resultado);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Cada usuario cambia su propia contrasena (pide la actual). Se cierran todas sus sesiones:
    /// hay que volver a iniciar sesion con la nueva.
    /// </summary>
    [Authorize]
    [HttpPost("cambiar-password")]
    public async Task<IActionResult> CambiarPassword(CambiarPasswordRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var r = await _usuarioService.CambiarPasswordPropiaAsync(usuarioId, request, cancellationToken);
        if (r.Exitoso) return NoContent();

        return r.Code switch
        {
            UsuarioResultCode.PasswordIncorrecta => BadRequest(new { mensaje = r.Mensaje }),
            UsuarioResultCode.NoEncontrado => NotFound(new { mensaje = r.Mensaje }),
            _ => BadRequest(new { mensaje = r.Mensaje })
        };
    }

    private IActionResult ResultadoAHttp(AuthResultDto resultado)
    {
        if (resultado.Exitoso)
        {
            return Ok(resultado.Datos);
        }

        return resultado.Code switch
        {
            AuthResultCode.CredencialesInvalidas => Unauthorized(new { mensaje = resultado.Mensaje }),
            AuthResultCode.TokenInvalido => Unauthorized(new { mensaje = resultado.Mensaje }),
            AuthResultCode.CuentaBloqueada => StatusCode(StatusCodes.Status423Locked, new { mensaje = resultado.Mensaje }),
            AuthResultCode.CuentaInactiva => StatusCode(StatusCodes.Status403Forbidden, new { mensaje = resultado.Mensaje }),
            AuthResultCode.YaExisteAlMenosUnUsuario => Conflict(new { mensaje = resultado.Mensaje }),
            AuthResultCode.RolSuperAdministradorNoConfigurado => Conflict(new { mensaje = resultado.Mensaje }),
            AuthResultCode.NombreUsuarioYaExiste => Conflict(new { mensaje = resultado.Mensaje }),
            _ => BadRequest(new { mensaje = resultado.Mensaje })
        };
    }
}
