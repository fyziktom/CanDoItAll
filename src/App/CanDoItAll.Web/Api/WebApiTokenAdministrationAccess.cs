using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Infrastructure;

namespace CanDoItAll.Web.Api;

internal sealed class WebApiTokenAdministrationAccess(
    IInteractiveAccessPrincipalProvider interactive,
    IHttpContextAccessor httpContextAccessor) : IApiTokenAdministrationAccess {
    public async ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) {
        var principal = interactive.IsAvailable
            ? await interactive.GetCurrentAsync(cancellationToken)
            : httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true &&
            (principal.Identity.AuthenticationType == LocalOperatorAuthenticationStateProvider.AuthenticationType ||
             ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.IssueTokens));
    }
}
