using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

/// <summary>
/// Repositorio de Inventario. No extiende IRepository{T} porque
/// Inventario no hereda de BaseEntity (ver comentario en la entidad).
/// </summary>
public interface IInventarioRepository
{
    Task<IReadOnlyList<Inventario>> ObtenerConCorteAsync(Guid sucursalId, CancellationToken cancellationToken = default);

    /// <summary>Si no existe fila de Inventario para ese corte/sucursal, la crea en 0.</summary>
    Task<Inventario> ObtenerOCrearAsync(Guid corteId, Guid sucursalId, CancellationToken cancellationToken = default);

    void Update(Inventario inventario);
}
