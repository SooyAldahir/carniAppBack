using CarniApp.Application.Features.Auth;

namespace CarniApp.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResultDto> RefreshAsync(string refreshTokenPlano, CancellationToken cancellationToken = default);

    Task<AuthResultDto> BootstrapAdminAsync(BootstrapAdminRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshTokenPlano, CancellationToken cancellationToken = default);
}
