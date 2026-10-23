using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class ProcessCancellationHttpAccess(WebCurrentPrincipalResolver principals, IOptions<ApiAccessOptions> options) {
    internal async Task<ProcessRootCancellationRequester> CaptureAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Value.Enabled) {
            throw new WebCurrentPrincipalDeniedException();
        }
        if (!options.Value.Authorization.Enabled) {
            return new(new(ProcessEventActorKind.System, new("process-api-local")), token => {
                token.ThrowIfCancellationRequested();
                if (!options.Value.Enabled || options.Value.Authorization.Enabled) {
                    throw new WebCurrentPrincipalDeniedException();
                }
                return ValueTask.CompletedTask;
            });
        }
        var original = await ReadAsync(cancellationToken);
        return new(new(ProcessEventActorKind.User, new(original.Subject)), async token => {
            var current = await ReadAsync(token);
            if (current.Subject != original.Subject || current.Issuer != original.Issuer || current.Stamp != original.Stamp) {
                throw new WebCurrentPrincipalDeniedException();
            }
        });
    }

    private async Task<WebCurrentPrincipal> ReadAsync(CancellationToken cancellationToken) {
        if (!options.Value.Enabled || !options.Value.Authorization.Enabled) {
            throw new WebCurrentPrincipalDeniedException();
        }
        var principal = await principals.ResolveAsync(cancellationToken);
        if (principal.IsLocalOperator) {
            throw new WebCurrentPrincipalDeniedException();
        }
        await principals.RequireScopeAsync(principal, ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ReadProcesses, cancellationToken);
        await principals.RequireScopeAsync(principal, ApiAccessScopeNames.ExecuteProcesses, ApiAccessScopeNames.ExecuteProcesses, cancellationToken);
        return principal;
    }
}
