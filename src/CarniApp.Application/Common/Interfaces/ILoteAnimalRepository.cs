using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

/// <summary>Kilos reales acumulados por corte en los ultimos lotes pesados de un tipo de animal.</summary>
public record HistorialRendimiento(int Lotes, decimal PesoTotalKg, IReadOnlyDictionary<Guid, decimal> KgPorCorte);

/// <summary>No extiende IRepository{T}: LoteAnimal no hereda de BaseEntity.</summary>
public interface ILoteAnimalRepository
{
    Task AddAsync(LoteAnimal lote, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoteAnimal>> ObtenerRecientesAsync(int limite, CancellationToken cancellationToken = default);

    /// <summary>Ultimos [ventana] lotes MEDIDOS (EsEstimado = false) del tipo y los kg que dio cada corte.</summary>
    Task<HistorialRendimiento> ObtenerHistorialMedidoAsync(Guid tipoAnimalId, int ventana, CancellationToken cancellationToken = default);
}
