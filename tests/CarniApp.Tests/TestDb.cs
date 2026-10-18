using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

/// <summary>Reloj controlable para probar bloqueos por tiempo.</summary>
public class FakeClock : IDateTimeProvider
{
    public DateTime Now { get; set; } = DateTime.UtcNow;
    public DateTime UtcNow => Now;
    public void Avanzar(TimeSpan t) => Now += t;
}

/// <summary>
/// Base de datos SQLite en memoria con el DbContext REAL. Cada llamada a
/// NuevoContexto() simula un request HTTP distinto (change tracker limpio),
/// que es justo donde viven los bugs de EF (Added/Modified/Unchanged).
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = NuevoContexto();
        ctx.Database.EnsureCreated();
    }

    public CarniAppDbContext NuevoContexto()
    {
        var options = new DbContextOptionsBuilder<CarniAppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CarniAppDbContext(options);
    }

    public Sucursal SembrarSucursal()
    {
        using var ctx = NuevoContexto();
        var s = new Sucursal { Nombre = "Principal", EsPrincipal = true };
        ctx.Sucursales.Add(s);
        ctx.SaveChanges();
        return s;
    }

    public Corte SembrarCorte(string nombre = "Bistec", decimal stockMinimo = 0, decimal precio = 150m)
    {
        using var ctx = NuevoContexto();
        var tipo = ctx.TiposAnimal.FirstOrDefault() ?? new TipoAnimal { Nombre = "Res" };
        if (ctx.Entry(tipo).State == EntityState.Detached) ctx.TiposAnimal.Add(tipo);
        var corte = new Corte
        {
            TipoAnimalId = tipo.Id,
            Nombre = nombre,
            UnidadVenta = UnidadVenta.Peso,
            PrecioBase = precio,
            StockMinimo = stockMinimo
        };
        ctx.Cortes.Add(corte);
        ctx.SaveChanges();
        return corte;
    }

    public Usuario SembrarUsuarioParaMovimientos()
    {
        using var ctx = NuevoContexto();
        var rol = new Rol { Nombre = "Cajero" };
        var u = new Usuario { NombreCompleto = "Test", NombreUsuario = "test" + Guid.NewGuid().ToString("N")[..6], PasswordHash = "x", RolId = rol.Id, Rol = rol };
        ctx.Usuarios.Add(u);
        ctx.SaveChanges();
        return u;
    }

    public void Dispose() => _connection.Dispose();
}
