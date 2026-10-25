namespace CarniApp.Domain.Entities;

public static class EstadoCorteCaja
{
    public const string Abierto = "Abierto";
    public const string Cerrado = "Cerrado";
}

/// <summary>
/// RF-CAJA-001..005: turno de caja. Se abre con un fondo inicial y se cierra contando el
/// efectivo. Las ventas del turno son las que caen entre FechaInicio y FechaCierre.
/// No hereda de BaseEntity: la tabla no tiene FechaCreacion/Activo.
/// </summary>
public class CorteCaja
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SucursalId { get; set; }
    public Sucursal Sucursal { get; set; } = null!;

    /// <summary>Quien abrio el turno.</summary>
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierre { get; set; }

    public decimal FondoInicial { get; set; }

    // Se llenan al cerrar (mientras esta abierto se calculan al vuelo).
    public decimal TotalVentas { get; set; }
    public decimal TotalGastos { get; set; }
    public decimal EfectivoEsperado { get; set; }
    public decimal? EfectivoContado { get; set; }

    /// <summary>Contado - esperado: positivo = sobrante, negativo = faltante.</summary>
    public decimal? Diferencia { get; set; }

    public string Estado { get; set; } = EstadoCorteCaja.Abierto;
    public string? Notas { get; set; }

    /// <summary>Proyeccion de efectivo en caja: al alcanzarla se avisa que toca hacer un retiro. Null = sin meta.</summary>
    public decimal? MetaEfectivo { get; set; }

    public ICollection<GastoCaja> Gastos { get; set; } = new List<GastoCaja>();
}

public static class TipoGastoCaja
{
    /// <summary>Gasto del negocio pagado con efectivo de la caja.</summary>
    public const string Gasto = "Gasto";

    /// <summary>Retiro de efectivo (se guarda en otro lado); no es un gasto.</summary>
    public const string Retiro = "Retiro";
}

/// <summary>Gasto o retiro pagado con efectivo de la caja durante un turno.</summary>
public class GastoCaja
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CorteCajaId { get; set; }
    public CorteCaja CorteCaja { get; set; } = null!;

    public string Concepto { get; set; } = string.Empty;
    public decimal Importe { get; set; }

    /// <summary>Ver <see cref="TipoGastoCaja"/>.</summary>
    public string Tipo { get; set; } = TipoGastoCaja.Gasto;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
