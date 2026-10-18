using System.Text.RegularExpressions;
using CarniApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

/// <summary>
/// Contrato EF &lt;-&gt; scripts SQL reales. Atrapa los dos tipos de bug que ya nos
/// pasaron: tabla pluralizada (Negocios vs Negocio) y columna que el modelo C#
/// espera pero el script nunca creo (FechaActualizacion en Sprint 2).
/// </summary>
public class SchemaContractTests
{
    private static string DirectorioScripts() =>
        Path.Combine(AppContext.BaseDirectory, "Scripts");

    /// <summary>tabla -> columnas, juntando CREATE TABLE + ALTER TABLE ... ADD de todos los scripts.</summary>
    private static Dictionary<string, HashSet<string>> LeerEsquemaSql()
    {
        var tablas = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var archivos = Directory.GetFiles(DirectorioScripts(), "*.sql").OrderBy(f => f).ToList();
        Assert.NotEmpty(archivos);

        foreach (var archivo in archivos)
        {
            var sql = File.ReadAllText(archivo);
            sql = Regex.Replace(sql, @"--[^\r\n]*", "");           // comentarios de linea
            sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);

            foreach (Match m in Regex.Matches(sql, @"CREATE\s+TABLE\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?\s*\((.*?)\n\s*\)\s*;", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var cols = tablas.TryGetValue(m.Groups[1].Value, out var existentes) ? existentes : new(StringComparer.OrdinalIgnoreCase);
                foreach (var linea in m.Groups[2].Value.Split('\n'))
                {
                    var t = linea.Trim();
                    var c = Regex.Match(t, @"^\[?(\w+)\]?\s+\w+", RegexOptions.IgnoreCase);
                    if (!c.Success) continue;
                    var primera = c.Groups[1].Value;
                    if (primera.Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
                        primera.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase) ||
                        primera.Equals("FOREIGN", StringComparison.OrdinalIgnoreCase) ||
                        primera.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase)) continue;
                    cols.Add(primera);
                }
                tablas[m.Groups[1].Value] = cols;
            }

            foreach (Match m in Regex.Matches(sql, @"ALTER\s+TABLE\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?\s+ADD\s+\[?(\w+)\]?", RegexOptions.IgnoreCase))
            {
                if (!tablas.TryGetValue(m.Groups[1].Value, out var cols))
                    tablas[m.Groups[1].Value] = cols = new(StringComparer.OrdinalIgnoreCase);
                cols.Add(m.Groups[2].Value);
            }
        }

        return tablas;
    }

    [Fact]
    public void CadaEntidadDeEf_ApuntaAUnaTablaQueExisteEnLosScripts_ConTodasSusColumnas()
    {
        var esquemaSql = LeerEsquemaSql();

        var options = new DbContextOptionsBuilder<CarniAppDbContext>().UseSqlite("DataSource=:memory:").Options;
        using var ctx = new CarniAppDbContext(options);

        var problemas = new List<string>();
        foreach (var entidad in ctx.Model.GetEntityTypes())
        {
            var tabla = entidad.GetTableName()!;
            if (!esquemaSql.TryGetValue(tabla, out var columnasSql))
            {
                problemas.Add($"Tabla '{tabla}' ({entidad.ClrType.Name}) no existe en los scripts SQL.");
                continue;
            }

            foreach (var prop in entidad.GetProperties())
            {
                var col = prop.GetColumnName();
                if (!columnasSql.Contains(col))
                    problemas.Add($"{tabla}.{col} la espera EF ({entidad.ClrType.Name}.{prop.Name}) pero ningun script la crea.");
            }
        }

        Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
    }
}
