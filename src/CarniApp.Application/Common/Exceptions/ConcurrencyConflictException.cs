namespace CarniApp.Application.Common.Exceptions;

/// <summary>
/// Dos operaciones modificaron el mismo registro a la vez (ej. dos ventas del
/// mismo corte). La capa Infrastructure traduce DbUpdateConcurrencyException a
/// esta excepcion para que Application no dependa de EF Core.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
