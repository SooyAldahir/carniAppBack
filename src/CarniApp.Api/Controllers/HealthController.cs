using CarniApp.Application.Features.Health;
using CarniApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Api.Controllers;

/// <summary>
/// Endpoint de salud del Sprint 0: prueba que Flutter, el API y
/// SQL Server estan conectados de punta a punta, y devuelve el
/// nombre del negocio configurado (RF-CONFIG-003) para que la
/// pantalla de login pueda mostrar la marca real.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly CarniAppDbContext _context;

    public HealthController(CarniAppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HealthStatusDto>> Get(CancellationToken cancellationToken)
    {
        bool dbConectada;
        string? nombreNegocio = null;

        try
        {
            dbConectada = await _context.Database.CanConnectAsync(cancellationToken);

            if (dbConectada)
            {
                nombreNegocio = await _context.Negocios
                    .Select(n => n.NombreComercial)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }
        catch
        {
            dbConectada = false;
        }

        var resultado = new HealthStatusDto(
            Estado: "CarniApp API en linea",
            BaseDeDatosConectada: dbConectada,
            NombreNegocio: nombreNegocio,
            HoraServidorUtc: DateTime.UtcNow);

        return Ok(resultado);
    }
}
