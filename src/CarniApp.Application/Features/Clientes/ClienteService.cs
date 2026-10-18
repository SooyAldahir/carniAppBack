using CarniApp.Application.Common.Exceptions;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.Clientes;

/// <summary>RF-CLI-001 (alta/edicion/baja) y RF-CRED-001..003 (limite, saldo, abonos).</summary>
public class ClienteService : IClienteService
{
    private readonly IClienteRepository _clienteRepository;
    private readonly IAbonoCreditoRepository _abonoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ClienteService(
        IClienteRepository clienteRepository,
        IAbonoCreditoRepository abonoRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _clienteRepository = clienteRepository;
        _abonoRepository = abonoRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyList<ClienteDto>> ListarAsync(string? texto, CancellationToken cancellationToken = default) =>
        (await _clienteRepository.BuscarActivosAsync(texto, cancellationToken)).Select(Mapear).ToList();

    public async Task<ClienteResultDto<ClienteDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id, cancellationToken);
        return cliente is null || !cliente.Activo
            ? NoEncontrado<ClienteDto>()
            : ClienteResultDto<ClienteDto>.Ok(Mapear(cliente));
    }

    public async Task<ClienteResultDto<ClienteDto>> CrearAsync(GuardarClienteRequest request, CancellationToken cancellationToken = default)
    {
        var error = await ValidarAsync(request, null, cancellationToken);
        if (error is not null) return error;

        var cliente = new Cliente { FechaCreacion = _dateTimeProvider.UtcNow };
        Aplicar(cliente, request);

        await _clienteRepository.AddAsync(cliente, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ClienteResultDto<ClienteDto>.Ok(Mapear(cliente));
    }

    public async Task<ClienteResultDto<ClienteDto>> ActualizarAsync(Guid id, GuardarClienteRequest request, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id, cancellationToken);
        if (cliente is null || !cliente.Activo) return NoEncontrado<ClienteDto>();

        var error = await ValidarAsync(request, id, cancellationToken);
        if (error is not null) return error;

        Aplicar(cliente, request);
        cliente.FechaActualizacion = _dateTimeProvider.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return ClienteResultDto<ClienteDto>.Fail(ClienteResultCode.Conflicto, "El cliente cambio mientras lo editabas. Vuelve a abrirlo e intenta de nuevo.");
        }

        return ClienteResultDto<ClienteDto>.Ok(Mapear(cliente));
    }

    public async Task<ClienteResultDto<bool>> DesactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id, cancellationToken);
        if (cliente is null || !cliente.Activo) return NoEncontrado<bool>();

        if (cliente.SaldoCredito > 0)
        {
            return ClienteResultDto<bool>.Fail(
                ClienteResultCode.TieneSaldoPendiente,
                $"No se puede dar de baja: el cliente debe {cliente.SaldoCredito:0.00}. Registra sus abonos primero.");
        }

        cliente.Activo = false;
        cliente.FechaActualizacion = _dateTimeProvider.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ClienteResultDto<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<AbonoDto>> ListarAbonosAsync(Guid clienteId, CancellationToken cancellationToken = default) =>
        (await _abonoRepository.ListarPorClienteAsync(clienteId, cancellationToken))
            .Select(a => new AbonoDto(a.Id, a.Importe, a.Fecha, a.Usuario?.NombreCompleto))
            .ToList();

    public async Task<ClienteResultDto<ClienteDto>> RegistrarAbonoAsync(Guid clienteId, RegistrarAbonoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(clienteId, cancellationToken);
        if (cliente is null || !cliente.Activo) return NoEncontrado<ClienteDto>();

        var importe = Math.Round(request.Importe, 2, MidpointRounding.AwayFromZero);
        if (importe <= 0)
        {
            return ClienteResultDto<ClienteDto>.Fail(ClienteResultCode.ImporteInvalido, "El abono debe ser mayor a cero.");
        }

        if (importe > cliente.SaldoCredito)
        {
            return ClienteResultDto<ClienteDto>.Fail(
                ClienteResultCode.AbonoExcedeSaldo,
                $"El abono ({importe:0.00}) es mayor al saldo del cliente ({cliente.SaldoCredito:0.00}).");
        }

        var ahora = _dateTimeProvider.UtcNow;
        cliente.SaldoCredito -= importe;
        cliente.FechaActualizacion = ahora;

        await _abonoRepository.AddAsync(new AbonoCredito
        {
            ClienteId = cliente.Id,
            Importe = importe,
            UsuarioId = usuarioId,
            Fecha = ahora
        }, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return ClienteResultDto<ClienteDto>.Fail(ClienteResultCode.Conflicto, "El saldo del cliente cambio mientras registrabas el abono. Revisa e intenta de nuevo.");
        }

        return ClienteResultDto<ClienteDto>.Ok(Mapear(cliente));
    }

    private async Task<ClienteResultDto<ClienteDto>?> ValidarAsync(GuardarClienteRequest request, Guid? excluirId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return ClienteResultDto<ClienteDto>.Fail(ClienteResultCode.NombreRequerido, "El nombre del cliente es obligatorio.");
        }

        if (request.LimiteCredito < 0)
        {
            return ClienteResultDto<ClienteDto>.Fail(ClienteResultCode.LimiteInvalido, "El limite de credito no puede ser negativo.");
        }

        var telefono = Limpiar(request.Telefono);
        if (telefono is not null && await _clienteRepository.ExisteTelefonoAsync(telefono, excluirId, cancellationToken))
        {
            return ClienteResultDto<ClienteDto>.Fail(ClienteResultCode.TelefonoDuplicado, $"Ya hay un cliente con el telefono {telefono}.");
        }

        return null;
    }

    private static void Aplicar(Cliente cliente, GuardarClienteRequest request)
    {
        cliente.Nombre = request.Nombre.Trim();
        cliente.Telefono = Limpiar(request.Telefono);
        cliente.Direccion = Limpiar(request.Direccion);
        cliente.Referencias = Limpiar(request.Referencias);
        cliente.LimiteCredito = Math.Round(request.LimiteCredito, 2, MidpointRounding.AwayFromZero);
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ClienteResultDto<T> NoEncontrado<T>() =>
        ClienteResultDto<T>.Fail(ClienteResultCode.NoEncontrado, "El cliente no existe.");

    internal static ClienteDto Mapear(Cliente c) =>
        new(c.Id, c.Nombre, c.Telefono, c.Direccion, c.Referencias, c.LimiteCredito, c.SaldoCredito, c.CreditoDisponible);
}
