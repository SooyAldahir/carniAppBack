namespace CarniApp.Application.Common.Interfaces;

/// <summary>Reglas configurables del punto de venta (appsettings: seccion 'Pos').</summary>
public interface IPosSettings
{
    /// <summary>
    /// Descuento maximo (% del subtotal) que se puede dar con el permiso basico
    /// 'pos.descuento'. Por encima se necesita 'pos.descuento.alto' (RF-POS-004).
    /// </summary>
    decimal DescuentoMaximoSinAutorizacionPorcentaje { get; }

    /// <summary>
    /// Cargo (% sobre lo que se fia) por vender a credito. Se muestra siempre en el
    /// ticket como "Cargo por credito". 0 = sin cargo.
    /// </summary>
    decimal RecargoCreditoPorcentaje { get; }

    /// <summary>Tope del % de cargo que se puede fijar al cobrar (con 'pos.recargo.ajustar').</summary>
    decimal RecargoCreditoMaximoPorcentaje { get; }
}
