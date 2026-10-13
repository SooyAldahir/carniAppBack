using Microsoft.AspNetCore.Authorization;

namespace CarniApp.Api.Security;

/// <summary>
/// Uso: [PermisoAuthorize("config.critica.editar")]. Se resuelve a una
/// policy dinamica "Permiso:config.critica.editar" (ver
/// PermisoPolicyProvider), sin necesidad de registrar cada permiso a
/// mano en Program.cs -- asi el catalogo de permisos sigue siendo
/// configurable (SUP-03) sin tocar codigo cada vez que se agrega uno.
/// </summary>
public class PermisoAuthorizeAttribute : AuthorizeAttribute
{
    public const string Prefijo = "Permiso:";

    public PermisoAuthorizeAttribute(string permiso) : base()
    {
        Policy = $"{Prefijo}{permiso}";
    }
}
