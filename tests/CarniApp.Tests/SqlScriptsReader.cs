using System.Text.RegularExpressions;

namespace CarniApp.Tests;

/// <summary>
/// Lee los scripts SQL reales (copiados al directorio de salida) para comprobar el catalogo
/// de permisos y a que roles se asigna cada uno, sin necesitar SQL Server.
/// </summary>
internal static class SqlScriptsReader
{
    private static readonly Regex RolRegex = new(@"r\.Nombre\s*(?:=\s*'([^']+)'|IN\s*\(([^)]*)\))", RegexOptions.IgnoreCase);

    public record Permisos(HashSet<string> Declarados, Dictionary<string, HashSet<string>> PorRol);

    public static Permisos LeerPermisos()
    {
        var declarados = new HashSet<string>();
        var porRol = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(AppContext.BaseDirectory, "Scripts");

        foreach (var archivo in Directory.GetFiles(dir, "*.sql").OrderBy(f => f))
        {
            var sql = File.ReadAllText(archivo);
            sql = Regex.Replace(sql, @"--[^\r\n]*", "");
            sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);

            var declaradosAqui = new HashSet<string>();
            foreach (Match m in Regex.Matches(sql, @"INSERT\s+INTO\s+Permiso\s*\([^)]*\)\s*VALUES(.*?)(?:;|\bEND\b|\bGO\b)", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                foreach (Match k in Regex.Matches(m.Groups[1].Value, @"\(\s*'([a-z0-9_.]+)'\s*,"))
                {
                    declaradosAqui.Add(k.Groups[1].Value);
                }
            }
            declarados.UnionWith(declaradosAqui);

            foreach (var bloque in Regex.Split(sql, @"\bGO\b", RegexOptions.IgnoreCase))
            {
                if (!Regex.IsMatch(bloque, @"INSERT\s+INTO\s+RolPermiso", RegexOptions.IgnoreCase)) continue;

                var roles = RolRegex.Matches(bloque).ToList();
                for (var i = 0; i < roles.Count; i++)
                {
                    var rm = roles[i];
                    var nombres = rm.Groups[1].Success
                        ? new[] { rm.Groups[1].Value }
                        : Regex.Matches(rm.Groups[2].Value, "'([^']+)'").Select(x => x.Groups[1].Value).ToArray();

                    var inicio = rm.Index + rm.Length;
                    var fin = i + 1 < roles.Count ? roles[i + 1].Index : bloque.Length;
                    var segmento = bloque[inicio..fin];

                    var claves = new HashSet<string>();
                    foreach (Match c in Regex.Matches(segmento, @"p\.Clave\s*=\s*'([^']+)'")) claves.Add(c.Groups[1].Value);
                    foreach (Match c in Regex.Matches(segmento, @"p\.Clave\s+IN\s*\(([^)]*)\)", RegexOptions.IgnoreCase))
                    {
                        foreach (Match k in Regex.Matches(c.Groups[1].Value, "'([^']+)'")) claves.Add(k.Groups[1].Value);
                    }

                    // Sin filtro por clave: se asignan todos los permisos que ese script acaba de declarar.
                    if (claves.Count == 0 && !segmento.Contains("p.Clave")) claves.UnionWith(declaradosAqui);

                    foreach (var nombre in nombres)
                    {
                        if (!porRol.TryGetValue(nombre, out var set)) porRol[nombre] = set = new HashSet<string>();
                        set.UnionWith(claves);
                    }
                }
            }
        }

        return new Permisos(declarados, porRol);
    }
}
