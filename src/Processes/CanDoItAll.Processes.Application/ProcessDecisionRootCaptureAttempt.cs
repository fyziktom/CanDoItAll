using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

internal sealed record ProcessDecisionRootCaptureAttempt(ProcessDecisionRunScopeExpectation OriginalScope,
    ProcessDecisionRunScopeCapture Capture, RuntimeCommandId ReserveId, RuntimeCommandId CompleteId, RuntimeCommandId AbortId,
    DecisionBudgetAmount Grant) {
    internal static ProcessDecisionRootCaptureAttempt Create(ProcessDecisionRootSelection selected) {
        var remaining = checked(selected.Budget.Ceiling.MaximumWorkDuration.Ticks -
            selected.Budget.ConsumedWorkTicks - selected.Budget.ReservedWorkTicks);
        if (selected.Budget.OverrunDetected || remaining <= 0 || selected.Budget.AllocationCount == DecisionBudgetBalance.MaximumAllocations) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.WorkExhausted);
        }
        Guid Identity(IdentityKind kind) {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
                Domain = "process-root-initial-capture/v1", Root = selected.Root.RunId.Value,
                Budget = selected.Budget.BudgetId.Value, Scope = selected.Scope.Admission.ScopeId.Value,
                selected.Scope.Revision, Kind = kind
            });
            return new Guid(SHA256.HashData(bytes).AsSpan(0, 16), bigEndian: true);
        }
        return new(selected.Scope.Expectation,
            new(new(Identity(IdentityKind.Allocation)), new(Identity(IdentityKind.Material)), ProcessDecisionRunCaptureKind.Initial),
            new(Identity(IdentityKind.Reserve)), new(Identity(IdentityKind.Complete)), new(Identity(IdentityKind.Abort)),
            new(Math.Min(remaining, TimeSpan.FromSeconds(10).Ticks), 0));
    }

    internal void RequireReceipt(ProcessRuntimeCommitResult receipt, ProcessDecisionRunScopeCommandKind kind,
        ProcessDecisionRootSelection selected, DecisionBudgetSettlement? work = null) {
        if (!receipt.Succeeded || receipt.State.RunId != selected.Root.RunId || receipt.State.RootRunId != selected.Root.RunId ||
                receipt.DecisionRunScope is not { } scope || scope.Command.Kind != kind ||
                scope.Command.Expectation.ScopeId != OriginalScope.ScopeId ||
                receipt.DecisionBudget is not { } budget || budget.Command.Expectation.BudgetId != selected.Root.BudgetId ||
                budget.Command.Expectation.StepInstanceId is not null || budget.Command.AllocationId != Capture.AllocationId ||
                budget.Change.Allocation.Grant != Grant || budget.Change.Allocation.Settlement != work ||
                kind == ProcessDecisionRunScopeCommandKind.BeginCapture && (scope.Command.Expectation != OriginalScope || scope.State.Capture != Capture) ||
                kind == ProcessDecisionRunScopeCommandKind.CompleteCapture && scope.State.Material?.Stamp.Reference.MaterialId != Capture.MaterialId) {
            throw new InvalidDataException("The original root capture receipt differs from its attempt and accounting.");
        }
    }

    private enum IdentityKind { Reserve, Complete, Abort, Allocation, Material }
}
