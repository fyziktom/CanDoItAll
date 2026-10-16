using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class ProjectDefinitionHttpAccess(WebCurrentPrincipalResolver principals, IOptions<ApiAccessOptions> options) {
    internal async Task<ProjectDefinitionInvocation> ResolveAsync(bool writing, CancellationToken cancellationToken) {
        var caller = await ResolveCallerAsync(writing, cancellationToken);
        return new(caller, new ReceiptAccess(this, caller));
    }

    private async Task<ProjectMutationCallerScope> ResolveCallerAsync(bool writing, CancellationToken cancellationToken) {
        if (!options.Value.Authorization.Enabled) {
            return ProjectMutationCallerScope.OpenApi;
        }
        var principal = await principals.ResolveAsync(cancellationToken);
        if (principal.IsLocalOperator || string.IsNullOrWhiteSpace(principal.Issuer)) {
            throw new WebCurrentPrincipalDeniedException();
        }
        await principals.RequireScopeAsync(principal, ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.ReadProjects, cancellationToken);
        if (writing) {
            await principals.RequireScopeAsync(principal, ApiAccessScopeNames.WriteProjects, ApiAccessScopeNames.WriteProjects, cancellationToken);
        }
        return ProjectMutationCallerScope.FromVerifiedSubject(principal.Issuer, principal.Subject);
    }

    private sealed class ReceiptAccess(ProjectDefinitionHttpAccess owner, ProjectMutationCallerScope expected) : IProjectDefinitionReceiptAccess {
        public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
            try {
                return await owner.ResolveCallerAsync(writing: false, cancellationToken) == expected;
            } catch (WebCurrentPrincipalDeniedException) {
                return false;
            }
        }
    }
}
