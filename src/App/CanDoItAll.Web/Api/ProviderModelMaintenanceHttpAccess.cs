using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class ProviderModelMaintenanceHttpAccess(WebCurrentPrincipalResolver principals, IOptions<ApiAccessOptions> options,
    ICanonicalRuntimeDatabase database, IDatabaseRuntimeState runtimeState, ProviderEditingWrites writes) {
    private const string AccessChangedCode = "agents.provider-model-access-changed";

    internal async Task<IResult> ExecuteAsync(Guid providerId, HttpContext http, Func<Task<IResult>> operation, CancellationToken cancellationToken) {
        try {
            var scope = CurrentScope();
            var original = options.Value.Authorization.Enabled ? await principals.ResolveHttpAsync(cancellationToken) : null;
            async ValueTask RequireCurrent(CancellationToken token) {
                try {
                    if (!options.Value.Enabled || scope != CurrentScope() || options.Value.Authorization.Enabled != (original is not null)) {
                        throw new UnauthorizedAccessException("The original provider HTTP scope is no longer current.");
                    }
                    if (original is not null) {
                        var current = await principals.ResolveHttpAsync(token);
                        if (!string.Equals(current.Stamp, original.Stamp, StringComparison.Ordinal)) {
                            throw new UnauthorizedAccessException("The original provider HTTP actor changed.");
                        }
                        await principals.RequireScopeAsync(current, ApiAccessScopeNames.ExecuteAgents, ApiAccessScopeNames.ExecuteAgents, token);
                        await principals.RequireScopeAsync(current, ApiAccessScopeNames.WriteAgents, ApiAccessScopeNames.WriteAgents, token);
                    }
                    if (scope != CurrentScope()) {
                        throw new UnauthorizedAccessException("The provider HTTP dataset changed during admission.");
                    }
                } catch (WebCurrentPrincipalDeniedException exception) {
                    throw new UnauthorizedAccessException("Current provider execution and configuration access is required.", exception);
                }
            }
            await RequireCurrent(cancellationToken);
            using var admission = writes.Enter(new(scope, EditingOwner.ProviderProfile, providerId),
                new(Guid.NewGuid(), Guid.NewGuid()), RequireCurrent);
            try {
                var result = await operation();
                await RequireCurrent(cancellationToken);
                return result;
            } catch (ProviderModelMaintenancePersistenceException) {
                await RequireCurrent(cancellationToken);
                throw;
            }
        } catch (Exception exception) when (exception is UnauthorizedAccessException or WebCurrentPrincipalDeniedException) {
            return ApiEndpointResults.AgentFailure(http, StatusCodes.Status403Forbidden,
                "Current provider execution and configuration write access is required. Review any in-flight operation before repeating it.", AccessChangedCode);
        }
    }

    private EditingScope CurrentScope() {
        var active = runtimeState.GetSnapshot();
        if (active.ActiveProfileId != database.Profile.Profile.Id || active.Generation != database.Generation ||
            !string.Equals(active.ActiveFingerprint, database.Profile.Profile.Runtime.Fingerprint, StringComparison.Ordinal)) {
            throw new UnauthorizedAccessException("The original provider HTTP dataset or activation is no longer current.");
        }
        return new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation);
    }
}
