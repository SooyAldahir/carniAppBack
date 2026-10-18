using System.Globalization;
using System.Text;
using CarniApp.Domain.Entities;
using NegocioEntidad = CarniApp.Domain.Entities.Negocio;   // 'Negocio' tambien es un namespace hermano

namespace CarniApp.Application.Features.Ventas;

/// <summary>
/// RF-POS-010: ticket en texto plano de 32 columnas (impresora termica de 58 mm).
/// Es el mismo texto para el ticket digital (copiar/compartir) y, en el sprint de
/// hardware, para la impresion fisica.
/// </summary>
public static class TicketBuilder
{
    public const int Ancho = 32;
    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    public static string Construir(VentaDto venta, NegocioEntidad? negocio, TimeZoneInfo zona)
    {
        var sb = new StringBuilder();

        sb.AppendLine(Centrar(negocio?.NombreComercial ?? "CarniApp"));
        if (!string.IsNullOrWhiteSpace(negocio?.Direccion)) sb.AppendLine(Centrar(negocio!.Direccion!));
        if (!string.IsNullOrWhiteSpace(negocio?.Telefono)) sb.AppendLine(Centrar($"Tel: {negocio!.Telefono}"));
        sb.AppendLine(Linea());

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(venta.Fecha, DateTimeKind.Utc), zona);
        sb.AppendLine(Fila($"Folio: {venta.Folio}", local.ToString("dd/MM/yyyy HH:mm", Cultura)));
        if (!string.IsNullOrWhiteSpace(venta.UsuarioNombre)) sb.AppendLine(Recortar($"Atendio: {venta.UsuarioNombre}", Ancho));
        if (!string.IsNullOrWhiteSpace(venta.ClienteNombre)) sb.AppendLine(Recortar($"Cliente: {venta.ClienteNombre}", Ancho));
        if (!string.IsNullOrWhiteSpace(venta.ClienteDireccion))
        {
            foreach (var l in Envolver($"Dir: {venta.ClienteDireccion}", Ancho)) sb.AppendLine(l);
        }
        sb.AppendLine(Linea());

        foreach (var l in venta.Lineas)
        {
            var unidad = l.UnidadVenta == "Pieza" ? "pza" : "kg";
            sb.AppendLine(Recortar(l.CorteNombre, Ancho));
            sb.AppendLine(Fila(
                $"  {l.Cantidad.ToString("0.###", Cultura)} {unidad} x {Dinero(l.PrecioUnitario)}",
                Dinero(l.Importe + l.Descuento)));
            if (l.Descuento > 0) sb.AppendLine(Fila("  Descuento", "-" + Dinero(l.Descuento)));
        }

        sb.AppendLine(Linea());
        sb.AppendLine(Fila("Subtotal", Dinero(venta.Subtotal)));
        if (venta.Descuento > 0) sb.AppendLine(Fila("Descuento", "-" + Dinero(venta.Descuento)));
        sb.AppendLine(Fila("TOTAL", Dinero(venta.Total)));
        sb.AppendLine(Linea());

        foreach (var p in venta.Pagos) sb.AppendLine(Fila(p.MetodoPago, Dinero(p.Importe)));

        if (venta.Estado == EstadoVenta.Anulada)
        {
            sb.AppendLine(Linea());
            sb.AppendLine(Centrar("*** VENTA ANULADA ***"));
        }

        if (!string.IsNullOrWhiteSpace(venta.Nota))
        {
            sb.AppendLine(Linea());
            foreach (var l in Envolver($"Nota: {venta.Nota}", Ancho)) sb.AppendLine(l);
        }

        sb.AppendLine(Linea());
        sb.AppendLine(Centrar("Gracias por su compra"));
        return sb.ToString().TrimEnd();
    }

    /// <summary>Parte el texto en lineas de maximo 'ancho' columnas, cortando por palabras.</summary>
    private static IEnumerable<string> Envolver(string texto, int ancho)
    {
        var actual = new StringBuilder();
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = palabra;
            while (p.Length > ancho)   // palabra mas larga que la linea: se corta
            {
                if (actual.Length > 0) { yield return actual.ToString(); actual.Clear(); }
                yield return p[..ancho];
                p = p[ancho..];
            }

            if (actual.Length > 0 && actual.Length + 1 + p.Length > ancho)
            {
                yield return actual.ToString();
                actual.Clear();
            }

            if (actual.Length > 0) actual.Append(' ');
            actual.Append(p);
        }

        if (actual.Length > 0) yield return actual.ToString();
    }

    private static string Dinero(decimal valor) => "$" + valor.ToString("N2", Cultura);

    private static string Linea() => new('-', Ancho);

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..max];

    private static string Centrar(string texto)
    {
        texto = Recortar(texto, Ancho);
        return new string(' ', (Ancho - texto.Length) / 2) + texto;
    }

    /// <summary>Texto a la izquierda y valor pegado a la derecha, en exactamente 'Ancho' columnas.</summary>
    private static string Fila(string izquierda, string derecha)
    {
        var espacioIzq = Ancho - derecha.Length - 1;
        if (espacioIzq < 1) return Recortar(izquierda + " " + derecha, Ancho);
        izquierda = Recortar(izquierda, espacioIzq);
        return izquierda + new string(' ', Ancho - izquierda.Length - derecha.Length) + derecha;
    }
}
