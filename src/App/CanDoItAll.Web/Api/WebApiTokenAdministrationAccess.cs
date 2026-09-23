using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Web.Infrastructure;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class WebApiTokenAdministrationAccess(
    IInteractiveAccessPrincipalProvider interactive,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ApiAccessOptions> options,
    IApiTokenRegistry registry,
    ApiSessionService sessions) : IApiTokenAdministrationAccess {
    public async ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) {
        var context = httpContextAccessor.HttpContext;
        if (context?.Features.Get<ValidatedApiCredential>() is { Kind: ApiCredentialKind.AdministratorSession } credential) {
            if (!options.Value.AccessManagement.Enabled || context.User.Identity?.IsAuthenticated != true) {
                return false;
            }
            var token = await registry.FindAsync(credential.Id, cancellationToken);
            return token is not null && (await sessions.ResolveIdentityAsync(token, cancellationToken))?.IsAdministrator == true;
        }
        if (context?.Request.Path.StartsWithSegments("/api") == true || !interactive.IsAvailable) {
            return false;
        }
        var principal = await interactive.TryGetTrustedLocalOperatorAsync(cancellationToken);
        return principal?.Identity?.AuthenticationType == LocalOperatorAuthenticationStateProvider.AuthenticationType;
    }
}
