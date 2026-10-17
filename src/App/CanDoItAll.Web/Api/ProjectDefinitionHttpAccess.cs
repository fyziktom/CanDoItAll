using CanDoItAll.Modules.Projects;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class ProjectDefinitionHttpAccess(WebCurrentPrincipalResolver principals, IOptions<ApiAccessOptions> options,
    IHttpContextAccessor http, IApiTokenRegistry tokens, IApiUserStore users, ApiSessionService sessions, TimeProvider clock) {
    internal async Task<ProjectDefinitionInvocation> ResolveWriteAsync(CancellationToken cancellationToken) {
        var context = http.HttpContext ?? throw new WebCurrentPrincipalDeniedException();
        if (!options.Value.Enabled) {
            throw new WebCurrentPrincipalDeniedException();
        }
        var observation = options.Value.Authorization.Enabled
            ? await principals.CaptureDefinitionWriteAuthorityAsync(cancellationToken) : null;
        var caller = observation is null ? ProjectMutationCallerScope.OpenApi
            : ProjectMutationCallerScope.FromVerifiedSubject(observation.Issuer, observation.Subject);
        return new(caller, new ReceiptAccess(this, caller), currentWrite:
            new ProjectDefinitionCurrentWriteAccess(context, observation, tokens, users, sessions, options, clock));
    }

    internal async Task<ProjectDefinitionReceiptInvocation> ResolveReadAsync(CancellationToken cancellationToken) {
        var caller = await ResolveCallerAsync(cancellationToken);
        return new(caller, new ReceiptAccess(this, caller));
    }

    private async Task<ProjectMutationCallerScope> ResolveCallerAsync(CancellationToken cancellationToken) {
        if (!options.Value.Authorization.Enabled) {
            return ProjectMutationCallerScope.OpenApi;
        }
        var principal = await principals.ResolveAsync(cancellationToken);
        if (principal.IsLocalOperator || string.IsNullOrWhiteSpace(principal.Issuer)) {
            throw new WebCurrentPrincipalDeniedException();
        }
        await principals.RequireScopeAsync(principal, ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.ReadProjects, cancellationToken);
        return ProjectMutationCallerScope.FromVerifiedSubject(principal.Issuer, principal.Subject);
    }

    private sealed class ReceiptAccess(ProjectDefinitionHttpAccess owner, ProjectMutationCallerScope expected) : IProjectDefinitionReceiptAccess {
        public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
            try {
                return await owner.ResolveCallerAsync(cancellationToken) == expected;
            } catch (WebCurrentPrincipalDeniedException) {
                return false;
            }
        }
    }
}
