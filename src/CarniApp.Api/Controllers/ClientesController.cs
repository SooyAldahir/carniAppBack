using System.Security.Claims;
using CarniApp.Api.Security;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Clientes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarniApp.Api.Controllers;

/// <summary>RF-CLI-001 y RF-CRED-001..003: clientes, limite/saldo de credito y abonos.</summary>
[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly IClienteService _clienteService;

    public ClientesController(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    /// <summary>Lista/busca clientes activos (la usa tambien el POS para elegir cliente).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClienteDto>>> Get([FromQuery] string? q, CancellationToken cancellationToken) =>
        Ok(await _clienteService.ListarAsync(q, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var r = await _clienteService.ObtenerAsync(id, cancellationToken);
        return r.Exitoso ? Ok(r.Datos) : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    [PermisoAuthorize("clientes.administrar")]
    [HttpPost]
    public async Task<IActionResult> Post(GuardarClienteRequest request, CancellationToken cancellationToken)
    {
        var r = await _clienteService.CrearAsync(request, cancellationToken);
        return r.Exitoso ? Ok(r.Datos) : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    [PermisoAuthorize("clientes.administrar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, GuardarClienteRequest request, CancellationToken cancellationToken)
    {
        var r = await _clienteService.ActualizarAsync(id, request, cancellationToken);
        return r.Exitoso ? Ok(r.Datos) : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    [PermisoAuthorize("clientes.administrar")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var r = await _clienteService.DesactivarAsync(id, cancellationToken);
        return r.Exitoso ? NoContent() : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    [PermisoAuthorize("creditos.administrar")]
    [HttpGet("{id:guid}/abonos")]
    public async Task<ActionResult<IReadOnlyList<AbonoDto>>> GetAbonos(Guid id, CancellationToken cancellationToken) =>
        Ok(await _clienteService.ListarAbonosAsync(id, cancellationToken));

    [PermisoAuthorize("creditos.administrar")]
    [HttpPost("{id:guid}/abonos")]
    public async Task<IActionResult> PostAbono(Guid id, RegistrarAbonoRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var r = await _clienteService.RegistrarAbonoAsync(id, request, usuarioId, cancellationToken);
        return r.Exitoso ? Ok(r.Datos) : ResultadoAHttp(r.Code, r.Mensaje!);
    }

    private IActionResult ResultadoAHttp(ClienteResultCode code, string mensaje) => code switch
    {
        ClienteResultCode.NoEncontrado => NotFound(new { mensaje }),
        ClienteResultCode.TelefonoDuplicado => Conflict(new { mensaje }),
        ClienteResultCode.TieneSaldoPendiente => Conflict(new { mensaje }),
        ClienteResultCode.Conflicto => Conflict(new { mensaje }),
        _ => BadRequest(new { mensaje })
    };
}
