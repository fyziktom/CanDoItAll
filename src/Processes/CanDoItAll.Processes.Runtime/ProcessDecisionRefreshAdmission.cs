using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public readonly record struct ProcessDecisionRefreshActionId(Guid Value);

public sealed record ProcessDecisionRefreshRequest(ProcessDecisionRefreshActionId ActionId, ProcessDecisionChildAddress Child,
    StrategyId StrategyId, StrategyResultIdempotencyKey WaitingResultKey, ProcessDecisionExpectation Decision,
    ProcessDecisionBudgetExpectation Budget, ProcessDecisionRunScopeExpectation? SharedScope, ProcessDecisionRefreshKind Kind) {
    [System.Text.Json.Serialization.JsonIgnore]
    public RuntimeCommandId CommandId {
        get {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new {
                Domain = "process-decision-refresh-admission/v1", Child.RootRunId, Child.RunId, ActionId
            });
            return new(new Guid(SHA256.HashData(payload).AsSpan(0, 16), bigEndian: true));
        }
    }

    public void Validate() {
        ArgumentNullException.ThrowIfNull(Child);
        ArgumentNullException.ThrowIfNull(Decision);
        ArgumentNullException.ThrowIfNull(Budget);
        Child.Validate();
        Decision.Validate();
        SharedScope?.Validate();
        if (ActionId.Value == Guid.Empty || !ProcessStrategyReceiptValuePolicy.IsStableIdentifier(StrategyId.Value) ||
                WaitingResultKey.Value == Guid.Empty || Decision.Reference != Child.Reference ||
                Budget.StepInstanceId != Child.StepInstanceId || Budget.BudgetId.Value == Guid.Empty || Budget.Revision < 1 ||
                SharedScope is not null && Child.RunId != Child.RootRunId ||
                Kind != ProcessDecisionRefreshKind.ManagerRefresh) {
            throw new InvalidDataException("Refresh admission requires its exact original review and an explicit manager refresh action.");
        }
    }
}

public sealed record ProcessDecisionRefreshIntent(ProcessDecisionRefreshRequest Request, ProcessEventActor Actor,
    ProcessDecisionMaterialId OriginalMaterialId, string InputFingerprint, string ChildFingerprint) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Request);
        ArgumentNullException.ThrowIfNull(Actor);
        Request.Validate();
        if (OriginalMaterialId.Value == Guid.Empty || Actor.Kind is not (ProcessEventActorKind.User or ProcessEventActorKind.Manager) ||
                !ProcessStrategyReceiptValuePolicy.IsStableIdentifier(Actor.Id.Value) ||
                !ProcessStrategyReceiptValuePolicy.IsSha256Digest(InputFingerprint) ||
                !ProcessStrategyReceiptValuePolicy.IsSha256Digest(ChildFingerprint)) {
            throw new InvalidDataException("Refresh admission requires its trusted review actor and original input and child digests.");
        }
    }
}

public sealed record ProcessDecisionBeginRefresh(ProcessDecisionRefreshIntent Intent)
    : ProcessDecisionCommand(Intent.Request.Child.StepInstanceId);

public static class ProcessDecisionRefreshAdmission {
    public static ProcessDecisionGeneration Begin(ProcessRuntimeStateSnapshot state, ProcessDecisionGeneration current,
        ProcessDecisionBeginRefresh command, DecisionDataPolicy policy) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(policy);
        command.Validate();
        current.Validate();
        var request = command.Intent.Request;
        if (current.Expectation != request.Decision || current.BudgetId != request.Budget.BudgetId || current.MaterialId != command.Intent.OriginalMaterialId) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.StaleExpectation);
        }
        if (policy.OnBasisChange == OnBasisChange.Fail ||
                (policy.ReadBasisMode == ReadBasisMode.RunSnapshot) != (request.SharedScope is not null) ||
                current.Phase != ProcessDecisionPhase.WaitingForReview || current.RefreshCommandId is not null ||
                current.ChildCount != 1 || current.UnclosedChildCount != 1) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.InvalidPhase);
        }
        if (current.Reference.Generation == ProcessDecisionGeneration.MaximumGenerations) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.GenerationCapacity);
        }
        if (ProcessDecisionReviewCompletion.ValidatePending(state, request.Child, request.StrategyId, request.WaitingResultKey) is { } refusal) {
            throw new InvalidOperationException(refusal.Message);
        }
        return ProcessDecisionTransitions.BeginReconciliation(current, request.Decision) with { RefreshCommandId = request.CommandId };
    }
}
