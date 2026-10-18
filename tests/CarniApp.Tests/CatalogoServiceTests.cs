using CarniApp.Application.Features.Catalogos;
using CarniApp.Infrastructure.Persistence;

namespace CarniApp.Tests;

public class CatalogoServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private async Task<T> Con<T>(Func<CatalogoService, Task<T>> accion)
    {
        using var ctx = _db.NuevoContexto();
        var svc = new CatalogoService(new TipoAnimalRepository(ctx), new CorteRepository(ctx), new UnitOfWork(ctx));
        return await accion(svc);
    }

    private async Task<Guid> CrearTipo(string nombre = "Res") =>
        (await Con(s => s.CrearTipoAnimalAsync(new GuardarTipoAnimalRequest(nombre)))).Datos!.Id;

    private static GuardarCorteRequest Corte(Guid tipoId, string nombre = "Bistec", string unidad = "Peso", decimal precio = 150m, decimal min = 5m) =>
        new(tipoId, nombre, unidad, precio, min);

    [Fact]
    public async Task TipoAnimal_NombreDuplicado_SinImportarMayusculasNiEspacios()
    {
        await CrearTipo("Res");
        var r = await Con(s => s.CrearTipoAnimalAsync(new GuardarTipoAnimalRequest("  rEs ")));
        Assert.Equal(CatalogoResultCode.NombreDuplicado, r.Code);
    }

    [Fact]
    public async Task TipoAnimal_PuedeRenombrarseASiMismo_PeroNoAOtroExistente()
    {
        var res = await CrearTipo("Res");
        await CrearTipo("Cerdo");

        Assert.True((await Con(s => s.ActualizarTipoAnimalAsync(res, new GuardarTipoAnimalRequest("RES")))).Exitoso);
        Assert.Equal(CatalogoResultCode.NombreDuplicado,
            (await Con(s => s.ActualizarTipoAnimalAsync(res, new GuardarTipoAnimalRequest("Cerdo")))).Code);
    }

    [Fact]
    public async Task Corte_CreaYSeListaConSuTipoDeAnimal()
    {
        var tipo = await CrearTipo();
        var r = await Con(s => s.CrearCorteAsync(Corte(tipo)));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal("Res", r.Datos!.TipoAnimalNombre);
        Assert.Single(await Con(s => s.ListarCortesAsync()));
        Assert.Equal(1, (await Con(s => s.ListarTiposAnimalAsync())).Single().CantidadCortes);
    }

    [Theory]
    [InlineData("Peso", 0, CatalogoResultCode.PrecioInvalido)]
    [InlineData("Peso", -5, CatalogoResultCode.PrecioInvalido)]
    [InlineData("Litro", 10, CatalogoResultCode.UnidadVentaInvalida)]
    public async Task Corte_ValidacionesDeNegocio(string unidad, decimal precio, CatalogoResultCode esperado)
    {
        var tipo = await CrearTipo();
        var r = await Con(s => s.CrearCorteAsync(Corte(tipo, unidad: unidad, precio: precio)));
        Assert.Equal(esperado, r.Code);
    }

    [Fact]
    public async Task Corte_TipoInexistente_YNombreDuplicado()
    {
        var tipo = await CrearTipo();
        Assert.Equal(CatalogoResultCode.TipoAnimalNoEncontrado,
            (await Con(s => s.CrearCorteAsync(Corte(Guid.NewGuid())))).Code);

        await Con(s => s.CrearCorteAsync(Corte(tipo, "Bistec")));
        Assert.Equal(CatalogoResultCode.NombreDuplicado,
            (await Con(s => s.CrearCorteAsync(Corte(tipo, "bistec")))).Code);
    }

    [Fact]
    public async Task Corte_ActualizaPrecioYUnidad()
    {
        var tipo = await CrearTipo();
        var id = (await Con(s => s.CrearCorteAsync(Corte(tipo)))).Datos!.Id;

        var r = await Con(s => s.ActualizarCorteAsync(id, Corte(tipo, "Bistec", "Pieza", 99.5m, 2m)));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal("Pieza", r.Datos!.UnidadVenta);
        Assert.Equal(99.5m, r.Datos.PrecioBase);
    }

    [Fact]
    public async Task DesactivarTipo_ConCortesActivos_SeBloquea_YSePermiteTrasDesactivarLosCortes()
    {
        var tipo = await CrearTipo();
        var corte = (await Con(s => s.CrearCorteAsync(Corte(tipo)))).Datos!.Id;

        Assert.Equal(CatalogoResultCode.TipoAnimalConCortesActivos, (await Con(s => s.DesactivarTipoAnimalAsync(tipo))).Code);

        Assert.True((await Con(s => s.DesactivarCorteAsync(corte))).Exitoso);
        Assert.True((await Con(s => s.DesactivarTipoAnimalAsync(tipo))).Exitoso);
        Assert.Empty(await Con(s => s.ListarTiposAnimalAsync()));
        Assert.Empty(await Con(s => s.ListarCortesAsync()));
    }

    [Fact]
    public async Task NombreDeCorteDesactivado_SePuedeReutilizar()
    {
        var tipo = await CrearTipo();
        var id = (await Con(s => s.CrearCorteAsync(Corte(tipo, "Bistec")))).Datos!.Id;
        await Con(s => s.DesactivarCorteAsync(id));

        Assert.True((await Con(s => s.CrearCorteAsync(Corte(tipo, "Bistec")))).Exitoso);
    }

    [Fact]
    public async Task IdsInexistentes_DevuelvenNoEncontrado()
    {
        Assert.Equal(CatalogoResultCode.NoEncontrado, (await Con(s => s.DesactivarCorteAsync(Guid.NewGuid()))).Code);
        Assert.Equal(CatalogoResultCode.NoEncontrado, (await Con(s => s.DesactivarTipoAnimalAsync(Guid.NewGuid()))).Code);
        Assert.Equal(CatalogoResultCode.NoEncontrado,
            (await Con(s => s.ActualizarTipoAnimalAsync(Guid.NewGuid(), new GuardarTipoAnimalRequest("X")))).Code);
    }
}
