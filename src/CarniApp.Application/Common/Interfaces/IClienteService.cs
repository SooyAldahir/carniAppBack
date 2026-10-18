using CarniApp.Application.Features.Clientes;

namespace CarniApp.Application.Common.Interfaces;

public interface IClienteService
{
    Task<IReadOnlyList<ClienteDto>> ListarAsync(string? texto, CancellationToken cancellationToken = default);

    Task<ClienteResultDto<ClienteDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ClienteResultDto<ClienteDto>> CrearAsync(GuardarClienteRequest request, CancellationToken cancellationToken = default);

    Task<ClienteResultDto<ClienteDto>> ActualizarAsync(Guid id, GuardarClienteRequest request, CancellationToken cancellationToken = default);

    Task<ClienteResultDto<bool>> DesactivarAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AbonoDto>> ListarAbonosAsync(Guid clienteId, CancellationToken cancellationToken = default);

    /// <summary>RF-CRED-002/003: registra un abono y devuelve el cliente con su saldo actualizado.</summary>
    Task<ClienteResultDto<ClienteDto>> RegistrarAbonoAsync(Guid clienteId, RegistrarAbonoRequest request, Guid usuarioId, CancellationToken cancellationToken = default);
}
