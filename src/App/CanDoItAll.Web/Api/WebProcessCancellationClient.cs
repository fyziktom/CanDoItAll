using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Web.Api;

internal sealed class WebProcessCancellationClient(WebCurrentPrincipalResolver principals, IServiceScopeFactory scopes, TimeProvider clock)
    : IProcessCancellationClient {
    public async Task<ProcessCancellationClientView> ReadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        using var deadline = new CancellationTokenSource(ProcessRootCancellationAdmissionService.MaximumDuration, clock);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var principal = await RequireAsync(null, false, bounded.Token);
        await using var scope = scopes.CreateAsyncScope();
        var progress = await scope.ServiceProvider.GetRequiredService<IProcessCancellationProgressReader>().ReadAsync(runId, bounded.Token);
        if (progress is not null && progress.RunId != runId) {
            throw new InvalidDataException("Cancellation observation returned a different selected identity.");
        }
        principal = await RequireAsync(principal, false, bounded.Token);
        var canRequest = await principals.HasScopeAsync(principal, ApiAccessScopeNames.ExecuteProcesses, ApiAccessScopeNames.ExecuteProcesses, bounded.Token);
        return new(progress, canRequest, scope.ServiceProvider.GetRequiredService<ProcessRootCancellationAvailability>().Enabled);
    }

    public async Task<ProcessCancellationClientResult> RequestAsync(ProcessRunId runId, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        using var deadline = new CancellationTokenSource(ProcessRootCancellationAdmissionService.MaximumDuration, clock);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var principal = await RequireAsync(null, true, bounded.Token);
        await using var scope = scopes.CreateAsyncScope();
        var progress = await scope.ServiceProvider.GetRequiredService<IProcessCancellationProgressReader>().ReadAsync(runId, bounded.Token);
        if (progress is not null && progress.RunId != runId) {
            throw new InvalidDataException("Cancellation observation returned a different selected identity.");
        }
        if (progress is null || progress.RootRunId == runId) {
            ProcessRootCancellationRequester requester = new(new(ProcessEventActorKind.User, new(principal.Subject)),
                async token => { await RequireAsync(principal, true, token); });
            return new ProcessCancellationClientResult.Root(await scope.ServiceProvider.GetRequiredService<ProcessRootCancellationAdmissionService>()
                .AdmitAsync(runId, requester, bounded.Token));
        }
        await RequireAsync(principal, true, bounded.Token);
        return new ProcessCancellationClientResult.Child(await scope.ServiceProvider.GetRequiredService<ProcessRuntimeOperatorApplicationService>()
            .RequestCancellationAsync(new(runId, principal.Subject, "Operator requested cancellation of the selected child."), bounded.Token));
    }

    private async Task<WebCurrentPrincipal> RequireAsync(WebCurrentPrincipal? original, bool mutation, CancellationToken cancellationToken) {
        try {
            var current = await principals.ResolveAsync(cancellationToken);
            await principals.RequireScopeAsync(current, ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ReadProcesses, cancellationToken);
            if (mutation) {
                await principals.RequireScopeAsync(current, ApiAccessScopeNames.ExecuteProcesses, ApiAccessScopeNames.ExecuteProcesses, cancellationToken);
            }
            if (original is not null && (current.Subject != original.Subject || current.Issuer != original.Issuer || current.Stamp != original.Stamp)) {
                throw new ProcessCancellationAccessDeniedException();
            }
            return current;
        } catch (WebCurrentPrincipalDeniedException) {
            throw new ProcessCancellationAccessDeniedException();
        }
    }
}
