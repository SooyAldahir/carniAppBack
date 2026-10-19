using System.Globalization;
using System.Text;
using CarniApp.Application.Features.Ventas;
using NegocioEntidad = CarniApp.Domain.Entities.Negocio;   // 'Negocio' tambien es un namespace hermano

namespace CarniApp.Application.Features.Caja;

/// <summary>Ticket del corte de caja en 32 columnas (mismo estilo que el ticket de venta).</summary>
public static class CorteTicketBuilder
{
    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    public static string Construir(CorteCajaDto c, NegocioEntidad? negocio, TimeZoneInfo zona)
    {
        var sb = new StringBuilder();
        sb.AppendLine(TicketBuilder.Centrar(negocio?.NombreComercial ?? "CarniApp"));
        sb.AppendLine(TicketBuilder.Centrar("CORTE DE CAJA"));
        sb.AppendLine(TicketBuilder.Linea());

        if (!string.IsNullOrWhiteSpace(c.UsuarioNombre)) sb.AppendLine(TicketBuilder.Recortar($"Abrio: {c.UsuarioNombre}", TicketBuilder.Ancho));
        sb.AppendLine(TicketBuilder.Recortar($"Inicio: {Local(c.FechaInicio, zona)}", TicketBuilder.Ancho));
        sb.AppendLine(TicketBuilder.Recortar(c.FechaCierre is null ? "Cierre: (turno abierto)" : $"Cierre: {Local(c.FechaCierre.Value, zona)}", TicketBuilder.Ancho));
        sb.AppendLine(TicketBuilder.Linea());

        sb.AppendLine(TicketBuilder.Fila($"Ventas ({c.NumeroVentas})", TicketBuilder.Dinero(c.TotalVentas)));
        foreach (var m in c.PorMetodo) sb.AppendLine(TicketBuilder.Fila($"  {m.Metodo}", TicketBuilder.Dinero(m.Importe)));
        if (c.VentasAnuladas > 0) sb.AppendLine($"Anuladas: {c.VentasAnuladas}");
        sb.AppendLine(TicketBuilder.Linea());

        sb.AppendLine(TicketBuilder.Fila("Fondo inicial", TicketBuilder.Dinero(c.FondoInicial)));
        sb.AppendLine(TicketBuilder.Fila("+ Ventas efectivo", TicketBuilder.Dinero(c.VentasEfectivo)));
        sb.AppendLine(TicketBuilder.Fila("+ Abonos credito", TicketBuilder.Dinero(c.AbonosEfectivo)));
        sb.AppendLine(TicketBuilder.Fila("- Gastos", TicketBuilder.Dinero(c.TotalGastos)));
        sb.AppendLine(TicketBuilder.Fila("= Esperado", TicketBuilder.Dinero(c.EfectivoEsperado)));

        if (c.EfectivoContado is not null)
        {
            sb.AppendLine(TicketBuilder.Fila("Contado", TicketBuilder.Dinero(c.EfectivoContado.Value)));
            var d = c.Diferencia ?? 0m;
            var etiqueta = d > 0 ? "Sobrante" : d < 0 ? "Faltante" : "Cuadra";
            sb.AppendLine(TicketBuilder.Fila(etiqueta, TicketBuilder.Dinero(Math.Abs(d))));
        }

        if (c.Gastos.Count > 0)
        {
            sb.AppendLine(TicketBuilder.Linea());
            sb.AppendLine("Gastos:");
            foreach (var g in c.Gastos) sb.AppendLine(TicketBuilder.Fila(g.Concepto, TicketBuilder.Dinero(g.Importe)));
        }

        if (!string.IsNullOrWhiteSpace(c.Notas))
        {
            sb.AppendLine(TicketBuilder.Linea());
            sb.AppendLine(TicketBuilder.Recortar($"Notas: {c.Notas}", TicketBuilder.Ancho));
        }

        return sb.ToString().TrimEnd();
    }

    private static string Local(DateTime utc, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona).ToString("dd/MM/yyyy HH:mm", Cultura);
}
