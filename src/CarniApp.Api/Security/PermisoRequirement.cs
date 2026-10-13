using Microsoft.AspNetCore.Authorization;

namespace CarniApp.Api.Security;

/// <summary>
/// Requisito de autorizacion: el usuario debe tener la clave de permiso
/// indicada entre los claims "perm" de su JWT (RF-AUTH-004, SUP-03).
/// </summary>
public class PermisoRequirement : IAuthorizationRequirement
{
    public string Permiso { get; }

    public PermisoRequirement(string permiso)
    {
        Permiso = permiso;
    }
}
