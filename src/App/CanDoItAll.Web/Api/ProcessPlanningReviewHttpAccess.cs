using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class ProcessPlanningReviewHttpAccess(WebCurrentPrincipalResolver principals,
    IApiTokenRegistry tokens, IApiUserStore users, ApiSessionService sessions, IOptions<ApiAccessOptions> options,
    ProjectProcessLaunchAuthorityService sources, TimeProvider clock) {
    private static readonly string[] RequiredScopes = [
        ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.WriteProcesses, ApiAccessScopeNames.ExecuteProcesses,
        ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents, ApiAccessScopeNames.ExecuteAgents,
        ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects
    ];

    internal async Task<ProcessPlanningManagerAuthority> CaptureAsync(HttpContext context, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Value.Enabled) {
            throw new WebCurrentPrincipalDeniedException();
        }
        ApiAuthenticationObservation? observation = null;
        if (options.Value.Authorization.Enabled) {
            observation = await principals.CaptureDefinitionWriteAuthorityAsync(cancellationToken);
            if (!observation.MatchesPrincipal(context.User) || RequiredScopes.Any(scope =>
                    !ApiAuthorizationPolicies.HasApiOrSpecificScope(context.User, scope) ||
                    observation.Credential is { } credential && !credential.Scopes.Contains(scope, StringComparer.Ordinal))) {
                throw new WebCurrentPrincipalDeniedException();
            }
        }
        var authority = observation is null
            ? await sources.CaptureLocalAsync(null, ProcessLaunchOperatorSurface.Api, cancellationToken)
            : await sources.CaptureAuthenticatedAsync(null, observation.Subject,
                DateTimeOffset.FromUnixTimeSeconds(observation.ExpiresAt), cancellationToken);
        return new(authority, observation?.Issuer,
            new ProjectDefinitionCurrentWriteAccess(context, observation, tokens, users, sessions, options, clock));
    }
}
