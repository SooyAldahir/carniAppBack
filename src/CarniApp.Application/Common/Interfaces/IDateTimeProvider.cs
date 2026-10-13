namespace CarniApp.Application.Common.Interfaces;

/// <summary>
/// Abstrae la hora del sistema para que la capa Application sea
/// testeable sin depender de DateTime.UtcNow directamente.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
