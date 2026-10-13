using CarniApp.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace CarniApp.Api.Security;

public class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermisoRequirement requirement)
    {
        if (context.User.HasClaim(TokenService.PermisoClaimType, requirement.Permiso))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
