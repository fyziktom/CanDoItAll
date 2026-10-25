using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Catalog;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal sealed class AgentDecisionPolicyRecoverySession(IAgentFrameworkWorkspaceService workspace,
    IAgentDecisionPolicyPendingStore pending, ILogger<AgentDecisionPolicyRecoverySession> logger) {
    private AgentExecutionActivityWorkspaceIdentity? context;
    private bool retired;
    public IReadOnlyList<AgentDecisionPolicyPendingSummary> Rows { get; private set; } = [];
    public bool Busy { get; private set; }
    public string? Error { get; private set; }

    public async Task ReadAsync(CancellationToken cancellationToken) {
        if (Busy || retired) {
            return;
        }
        Busy = true;
        Error = null;
        Rows = [];
        context = null;
        try {
            var captured = await workspace.ReadAgentDecisionDataPolicyContextAsync(cancellationToken);
            var rows = await pending.ListAsync(captured, cancellationToken);
            if (captured != await workspace.ReadAgentDecisionDataPolicyContextAsync(cancellationToken) ||
                    rows.Count > IAgentDecisionPolicyPendingStore.MaximumRecords || rows.Any(row => row.AgentId == Guid.Empty ||
                        row.OperationId == Guid.Empty || !Enum.IsDefined(row.Stage)) || rows.Select(row => row.AgentId).Distinct().Count() != rows.Count) {
                throw new InvalidOperationException("The recovery context or retained list changed.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!retired) {
                context = captured;
                Rows = rows.ToArray();
            }
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            Fail(failure, "Retained saves could not be read. Reload the list when this workspace is available.", cancellationToken);
        } finally {
            Busy = false;
        }
    }

    public async Task<AgentDecisionPolicyRecoverySelection?> SelectAsync(AgentDecisionPolicyPendingSummary row, CancellationToken cancellationToken) {
        if (Busy || retired || context is not { } captured || !Rows.Contains(row)) {
            return null;
        }
        Busy = true;
        Error = null;
        try {
            if (captured != await workspace.ReadAgentDecisionDataPolicyContextAsync(cancellationToken)) {
                throw new InvalidOperationException("The original recovery workspace changed.");
            }
            var record = await pending.ReadAsync(new(captured, row.AgentId), cancellationToken);
            if (captured != await workspace.ReadAgentDecisionDataPolicyContextAsync(cancellationToken) || record is null ||
                    record.Command.Expectation.AgentId != row.AgentId || record.Command.OperationId != row.OperationId || record.Stage != row.Stage ||
                    record.Command.Expectation.Workspace.DatabaseProfileId != captured.DatabaseProfileId ||
                    record.Command.Expectation.Workspace.WorkspaceScope != captured.WorkspaceScope) {
                throw new InvalidOperationException("The selected retained submission changed.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return retired ? null : new(captured, record);
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            Fail(failure, "This retained submission changed or could not be read. Reload the list before choosing it again.", cancellationToken);
            return null;
        } finally {
            Busy = false;
        }
    }

    public void Retire() {
        retired = true;
        Rows = [];
        context = null;
    }

    private void Fail(Exception failure, string message, CancellationToken cancellationToken) {
        if (retired || cancellationToken.IsCancellationRequested) {
            return;
        }
        Error = message;
        logger.LogWarning("Agent policy recovery list failed with {FailureType}.", failure.GetType().FullName);
    }
}
