using CarniApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CarniApp.Infrastructure.Services;

/// <summary>Lee la seccion 'Pos' de appsettings. Sin configurar, el tope de descuento basico es 10%.</summary>
public class PosSettings : IPosSettings
{
    private const decimal PorcentajeDefault = 10m;

    private readonly IConfiguration _configuration;

    public PosSettings(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public decimal DescuentoMaximoSinAutorizacionPorcentaje =>
        _configuration.GetValue<decimal?>("Pos:DescuentoMaximoSinAutorizacionPorcentaje") ?? PorcentajeDefault;
}
