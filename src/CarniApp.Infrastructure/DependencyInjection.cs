using CarniApp.Application.Common.Interfaces;
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

        return services;
    }
}
