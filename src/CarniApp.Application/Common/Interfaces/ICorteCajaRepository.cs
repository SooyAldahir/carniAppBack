using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

/// <summary>No extiende IRepository{T}: CorteCaja no hereda de BaseEntity.</summary>
public interface ICorteCajaRepository
{
    Task AddAsync(CorteCaja corte, CancellationToken cancellationToken = default);

    Task AddGastoAsync(GastoCaja gasto, CancellationToken cancellationToken = default);

    /// <summary>Corte abierto de la sucursal (con gastos y usuarios), rastreado por el DbContext.</summary>
    Task<CorteCaja?> ObtenerAbiertoAsync(Guid sucursalId, CancellationToken cancellationToken = default);

    /// <summary>Corte con gastos y usuarios, rastreado por el DbContext.</summary>
    Task<CorteCaja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorteCaja>> ListarRecientesAsync(int limite, CancellationToken cancellationToken = default);

    /// <summary>true si la fecha cae dentro de un corte YA CERRADO de la sucursal.</summary>
    Task<bool> ExisteCerradoQueContieneAsync(Guid sucursalId, DateTime fechaUtc, CancellationToken cancellationToken = default);

    /// <summary>true si hay un corte que empezo despues de [fechaInicioUtc].</summary>
    Task<bool> ExisteCortePosteriorAsync(Guid sucursalId, DateTime fechaInicioUtc, CancellationToken cancellationToken = default);
}
