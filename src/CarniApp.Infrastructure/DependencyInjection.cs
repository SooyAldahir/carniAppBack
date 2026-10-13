using CarniApp.Application.Common.Interfaces;
using CarniApp.Application.Features.Auth;
using CarniApp.Application.Features.Negocio;
using CarniApp.Application.Features.Catalogos;
using CarniApp.Infrastructure.Security;
using CarniApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarniApp.Infrastructure;

/// <summary>
/// Punto unico de registro de Infrastructure en el contenedor de DI
/// (Dependency Injection, D-04). El proyecto Api solo llama a
/// AddInfrastructure(configuration); no conoce EF Core directamente.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<Persistence.CarniAppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork, Persistence.UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Persistence.Repository<>));
        services.AddScoped<IUsuarioRepository, Persistence.UsuarioRepository>();
        services.AddScoped<IRefreshTokenRepository, Persistence.RefreshTokenRepository>();
        services.AddScoped<ITipoAnimalRepository, Persistence.TipoAnimalRepository>();
        services.AddScoped<ICorteRepository, Persistence.CorteRepository>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<INegocioService, NegocioService>();
        services.AddScoped<ICatalogoService, CatalogoService>();

        return services;
    }
}
