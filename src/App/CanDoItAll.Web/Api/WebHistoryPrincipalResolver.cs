using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal sealed class WebHistoryPrincipalResolver(WebCurrentPrincipalResolver principals) {
    internal async Task<WebHistoryPrincipal> ResolveAsync(HistoryPermission permission, CancellationToken cancellationToken) {
        try {
            var principal = await principals.ResolveAsync(cancellationToken);
            var required = permission switch {
                HistoryPermission.ReadMetadata => (ApiAuthorizationPolicies.ReadProviderHistory, ApiAccessScopeNames.ReadProviderHistory),
                HistoryPermission.ReadContent => (ApiAuthorizationPolicies.ReadProviderHistoryContent, ApiAccessScopeNames.ReadProviderHistoryContent),
                HistoryPermission.Manage => (ApiAuthorizationPolicies.ManageProviderHistory, ApiAccessScopeNames.ManageProviderHistory),
                _ => throw Denied()
            };
            await principals.RequireScopeAsync(principal, required.Item1, required.Item2, cancellationToken);
            var caller = new HistoryCaller(principal.IsLocalOperator ? HistoryAuthenticationKind.TrustedLocalOperator
                : principal.Credential is null ? HistoryAuthenticationKind.LegacyAuthenticated : HistoryAuthenticationKind.ManagedCredential,
                principal.Credential is null ? null : new ManagedCredentialId(principal.Credential.Id), principal.Issuer, principal.Subject,
                principal.IsLocalOperator ? "Local operator" : principal.Credential?.DisplayName);
            return new(principal, caller);
        } catch (WebCurrentPrincipalDeniedException) {
            throw Denied();
        }
    }

    internal async Task RequireOwnerAsync(WebHistoryPrincipal principal, HistorySourceKind kind, CancellationToken cancellationToken) {
        try {
            if (kind == HistorySourceKind.SimpleChat) {
                await principals.RequireScopeAsync(principal.Identity, ApiAuthorizationPolicies.ReadLlmChats, ApiAccessScopeNames.ReadLlmChats,
                    cancellationToken);
                return;
            }
            if (!principal.Identity.IsLocalOperator || kind is not (HistorySourceKind.AgentConversation or HistorySourceKind.Workflow)) {
                throw Denied();
            }
        } catch (WebCurrentPrincipalDeniedException) {
            throw Denied();
        }
    }

    private static ProviderHistoryException Denied() =>
        new(HistoryFailure.Denied, "Provider history requires explicit current authority for this operation.");
}

internal sealed record WebHistoryPrincipal(WebCurrentPrincipal Identity, HistoryCaller Caller) {
    internal string Stamp => Identity.Stamp;
    internal long Revision => Identity.Revision;
}
