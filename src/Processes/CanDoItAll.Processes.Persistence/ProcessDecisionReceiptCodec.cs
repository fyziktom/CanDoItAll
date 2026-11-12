using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessDecisionReceiptCodec {
    internal const int MaximumUtf8Bytes = 16_384;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, MaxDepth = 16
    };

    internal static string? Write(ProcessDecisionReceipt? receipt) {
        if (receipt is null) {
            return null;
        }
        Validate(receipt);
        var version = receipt.Command is ProcessDecisionBeginRefresh or ProcessDecisionRefresh { AdmissionCommandId: not null } ||
            receipt.Current.RefreshCommandId is not null || receipt.Previous?.RefreshCommandId is not null ? 7
            : receipt.Command is ProcessDecisionFinalization ? 6
            : receipt.Command is ProcessDecisionClosure { Completion: ProcessDecisionCompletion.Review or ProcessDecisionCompletion.Reconcile } ? 5
            : receipt.Command is ProcessDecisionClosure { Completion: not null } ? 4
            : receipt.Command.InheritedScope is not null ? 3 : receipt.Command.Material is null ? 1 : 2;
        var json = JsonSerializer.Serialize(new Document(version,
            ToDocument(receipt.Command) with { Material = receipt.Command.Material, InheritedScope = receipt.Command.InheritedScope }, receipt.Changed, ToDocument(receipt.Current),
            receipt.Previous is { } previous ? ToDocument(previous) : null, receipt.Child is { } child ? ToDocument(child) : null), Options);
        RequireBound(json);
        return json;
    }

    internal static ProcessDecisionReceipt? Read(string? json) {
        if (json is null) {
            return null;
        }
        RequireBound(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options) ?? throw new InvalidDataException("The Process decision receipt is empty.");
        if (document.Version is not (1 or 2 or 3 or 4 or 5 or 6 or 7) || document.Command is null || document.Current is null ||
                document.Version != 7 && (
                (document.Version == 6) != (document.Command.Kind == CommandKind.FinalizeTerminal) ||
                (document.Version == 6) != (document.Command.Budget is not null) ||
                (document.Version == 6) != (document.Command.TerminalStatus is not null) ||
                (document.Version is 2 or 3) != (document.Command.Material is not null) ||
                (document.Version == 3) != (document.Command.InheritedScope is not null) ||
                (document.Version is 4 or 5) != (document.Command.Completion is not null) ||
                (document.Version == 5) != (document.Command.Completion is ProcessDecisionCompletion.Review or ProcessDecisionCompletion.Reconcile) ||
                document.Command.Completion is not null && document.Command.Kind != CommandKind.Close)) {
            throw new InvalidDataException("The Process decision receipt has an unsupported version or missing command.");
        }
        var command = FromDocument(document.Command) with { Material = document.Command.Material, InheritedScope = document.Command.InheritedScope };
        var receipt = new ProcessDecisionReceipt(command, document.Changed, FromDocument(document.Current),
            document.Previous is { } previous ? FromDocument(previous) : null, document.Child is { } child ? FromDocument(child) : null);
        if (!string.Equals(json, Write(receipt), StringComparison.Ordinal)) {
            throw new InvalidDataException("The Process decision receipt is not a supported canonical record.");
        }
        return receipt;
    }

    internal static void RequireSameCommand(ProcessDecisionCommand? command, ProcessDecisionReceipt? receipt) {
        if (command != receipt?.Command) {
            throw new InvalidOperationException("The original runtime command has different Process decision parameters.");
        }
    }

    private static CommandDocument ToDocument(ProcessDecisionCommand command) => command switch {
        ProcessDecisionStart start => new(CommandKind.Start, start.StepInstanceId.Value, start.DecisionId, MaterialId: start.MaterialId),
        ProcessDecisionEnrollment enrollment => new(CommandKind.Enroll, enrollment.StepInstanceId.Value, Expected: enrollment.Expectation, Child: ToDocument(enrollment.Child)),
        ProcessDecisionClosure closure => new(CommandKind.Close, closure.StepInstanceId.Value, Expected: closure.Expectation, ChildId: closure.ChildId, Closure: closure.Closure) {
            Completion = closure.Completion
        },
        ProcessDecisionChangePhase phase => new(CommandKind.ChangePhase, phase.StepInstanceId.Value, Expected: phase.Expectation, PhaseChange: phase.Change),
        ProcessDecisionBeginRefresh begin => new(CommandKind.BeginRefresh, begin.StepInstanceId.Value) { RefreshIntent = ToDocument(begin.Intent) },
        ProcessDecisionFinalization finalization => new(CommandKind.FinalizeTerminal, finalization.StepInstanceId.Value, Expected: finalization.Expectation) {
            Budget = new(finalization.Budget.StepInstanceId!.Value.Value, finalization.Budget.BudgetId.Value, finalization.Budget.Revision),
            TerminalStatus = finalization.StepStatus
        },
        ProcessDecisionRefresh refresh => new(CommandKind.Refresh, refresh.StepInstanceId.Value, Expected: refresh.Expectation, MaterialId: refresh.MaterialId, RefreshKind: refresh.Kind) {
            AdmissionCommandId = refresh.AdmissionCommandId?.Value
        },
        _ => throw new InvalidDataException("The Process decision command is unsupported.")
    };

    private static ProcessDecisionCommand FromDocument(CommandDocument document) => document.Kind switch {
        CommandKind.Start when document.DecisionId is { } id && document.MaterialId is { } material => new ProcessDecisionStart(new(document.StepId), id, material),
        CommandKind.Enroll when document.Expected is { } expected && document.Child is { } child => new ProcessDecisionEnrollment(new(document.StepId), expected, FromDocument(child)),
        CommandKind.Close when document.Expected is { } expected && document.ChildId is { } child && document.Closure is { } closure
            => new ProcessDecisionClosure(new(document.StepId), expected, child, closure) { Completion = document.Completion },
        CommandKind.ChangePhase when document.Expected is { } expected && document.PhaseChange is { } change => new ProcessDecisionChangePhase(new(document.StepId), expected, change),
        CommandKind.BeginRefresh when document.RefreshIntent is { } intent && document.StepId == intent.StepId
            => new ProcessDecisionBeginRefresh(FromDocument(intent)),
        CommandKind.FinalizeTerminal when document.Expected is { } expected && document.Budget is { } budget && document.TerminalStatus is { } status &&
                budget.StepId != Guid.Empty && budget.BudgetId != Guid.Empty && budget.Revision > 0
            => new ProcessDecisionFinalization(new(document.StepId), expected,
                new(new ProcessStepInstanceId(budget.StepId), new(budget.BudgetId), budget.Revision), status),
        CommandKind.Refresh when document.Expected is { } expected && document.MaterialId is { } material && document.RefreshKind is { } kind
            => new ProcessDecisionRefresh(new(document.StepId), expected, material, kind) {
                AdmissionCommandId = document.AdmissionCommandId is { } admission ? new(admission) : null
            },
        _ => throw new InvalidDataException("The Process decision receipt has an invalid command shape.")
    };

    private static GenerationDocument ToDocument(ProcessDecisionGeneration generation)
        => new(generation.Reference, generation.Revision, generation.Phase, generation.MaterialId, generation.BudgetId.Value,
            generation.ChildCount, generation.UnclosedChildCount) { RefreshCommandId = generation.RefreshCommandId?.Value };

    private static ProcessDecisionGeneration FromDocument(GenerationDocument generation)
        => new(generation.Reference, generation.Revision, generation.Phase, generation.MaterialId, new(generation.BudgetId),
            generation.ChildCount, generation.UnclosedChildCount) {
            RefreshCommandId = generation.RefreshCommandId is { } command ? new(command) : null
        };

    private static RefreshIntentDocument ToDocument(ProcessDecisionRefreshIntent intent) {
        var request = intent.Request;
        var child = request.Child;
        return new(request.ActionId.Value, child.RootRunId.Value, child.RunId.Value, child.StepInstanceId.Value,
            child.Reference, child.ChildId.Value, request.StrategyId.Value, request.WaitingResultKey.Value,
            request.Decision.Revision, request.Budget.BudgetId.Value, request.Budget.Revision,
            request.SharedScope?.ScopeId.Value, request.SharedScope?.Revision, request.Kind,
            intent.Actor.Kind, intent.Actor.Id.Value, intent.OriginalMaterialId.Value, intent.InputFingerprint, intent.ChildFingerprint);
    }

    private static ProcessDecisionRefreshIntent FromDocument(RefreshIntentDocument intent) {
        if (intent.ScopeId.HasValue != intent.ScopeRevision.HasValue) {
            throw new InvalidDataException("Refresh admission requires its complete original shared scope expectation.");
        }
        ProcessStepInstanceId step = new(intent.StepId);
        return new(new(new(intent.ActionId), new(new(intent.RootRunId), new(intent.RunId), step, intent.Reference, new(intent.ChildId)),
            new(intent.StrategyId), new(intent.WaitingResultKey), new(intent.Reference, intent.DecisionRevision),
            new(step, new(intent.BudgetId), intent.BudgetRevision),
            intent.ScopeId is { } scope ? new(new(scope), intent.ScopeRevision!.Value) : null, intent.Kind),
            new(intent.ActorKind, new(intent.ActorId)), new(intent.MaterialId), intent.InputFingerprint, intent.ChildFingerprint);
    }

    private static ChildDocument ToDocument(ProcessDecisionChild child)
        => new(child.Reference, child.ChildId, child.Owner, child.AllocationId.Value, child.Closure);

    private static ProcessDecisionChild FromDocument(ChildDocument child)
        => new(child.Reference, child.ChildId, child.Owner, new(child.AllocationId), child.Closure);

    private static void Validate(ProcessDecisionReceipt receipt) {
        ArgumentNullException.ThrowIfNull(receipt.Command);
        ArgumentNullException.ThrowIfNull(receipt.Current);
        receipt.Command.Validate();
        receipt.Current.Validate();
        receipt.Previous?.Validate();
        receipt.Child?.Validate();
        var current = receipt.Current;
        var child = receipt.Child;
        var expected = receipt.Command.Expected;
        if (expected is not null && (current.Reference.DecisionId != expected.Reference.DecisionId ||
                current.Revision < expected.Revision || current.Revision - expected.Revision != (receipt.Changed ? 1 : 0))) {
            throw new InvalidDataException("The Process decision result differs from its original expectation.");
        }
        var valid = receipt.Command switch {
            ProcessDecisionStart start => receipt.Changed && receipt.Previous is null && child is null &&
                current.Reference == new ProcessDecisionReference(start.DecisionId, 1) && current.Revision == 1 &&
                current.MaterialId == start.MaterialId && current.Phase == ProcessDecisionPhase.Active && current.ChildCount == 0,
            ProcessDecisionEnrollment enrollment => receipt.Previous is null && child is not null &&
                current.Reference == enrollment.Expectation.Reference && child.Reference == current.Reference &&
                enrollment.Child == (child with { Closure = null }) && (!receipt.Changed || child.Closure is null),
            ProcessDecisionClosure closure => receipt.Previous is null && child is not null &&
                current.Reference == closure.Expectation.Reference && child.Reference == current.Reference &&
                child.ChildId == closure.ChildId && child.Closure == closure.Closure &&
                (closure.Completion is null || current.Phase == ProcessDecisionTransitions.CompletionPhase(closure.Completion.Value) &&
                    (closure.Completion is ProcessDecisionCompletion.Review or ProcessDecisionCompletion.Reconcile || current.UnclosedChildCount == 0)),
            ProcessDecisionChangePhase phase => receipt.Changed && receipt.Previous is null && child is null &&
                current.Reference == phase.Expectation.Reference && current.Phase == ResultPhase(phase.Change),
            ProcessDecisionBeginRefresh begin => receipt.Changed && receipt.Previous is null && child is null &&
                current.Reference == begin.Intent.Request.Decision.Reference && current.Phase == ProcessDecisionPhase.Reconciling &&
                current.BudgetId == begin.Intent.Request.Budget.BudgetId && current.ChildCount == 1 && current.UnclosedChildCount == 1 &&
                current.MaterialId == begin.Intent.OriginalMaterialId &&
                current.RefreshCommandId == begin.Intent.Request.CommandId,
            ProcessDecisionFinalization finalization => receipt.Changed && receipt.Previous is null && child is null &&
                current.Reference == finalization.Expectation.Reference && current.BudgetId == finalization.Budget.BudgetId &&
                current.Phase == ProcessDecisionTransitions.TerminalPhase(finalization.StepStatus) && current.UnclosedChildCount == 0,
            ProcessDecisionRefresh refresh => receipt.Changed && child is null && receipt.Previous is { } previous &&
                previous.Reference == refresh.Expectation.Reference && previous.Phase == ProcessDecisionPhase.Superseded &&
                previous.Revision == current.Revision && previous.BudgetId == current.BudgetId &&
                previous.RefreshCommandId == refresh.AdmissionCommandId && current.RefreshCommandId is null &&
                current.Reference.Generation == previous.Reference.Generation + 1 && current.MaterialId == refresh.MaterialId &&
                current.Phase == ProcessDecisionPhase.Active && current.ChildCount == 0,
            _ => false
        };
        if (!valid) {
            throw new InvalidDataException("The Process decision receipt does not match the original operation and result.");
        }
    }

    private static ProcessDecisionPhase ResultPhase(ProcessDecisionPhaseChange change) => change switch {
        ProcessDecisionPhaseChange.WaitForReview => ProcessDecisionPhase.WaitingForReview,
        ProcessDecisionPhaseChange.BeginReconciliation => ProcessDecisionPhase.Reconciling,
        ProcessDecisionPhaseChange.Complete => ProcessDecisionPhase.Completed,
        ProcessDecisionPhaseChange.Cancel => ProcessDecisionPhase.Cancelled,
        _ => throw new InvalidDataException("The Process phase change is unsupported.")
    };

    private static void RequireBound(string json) {
        if (Encoding.UTF8.GetByteCount(json) is 0 or > MaximumUtf8Bytes) {
            throw new InvalidDataException("The Process decision receipt exceeds its retained bound.");
        }
    }

    private enum CommandKind { Start, Enroll, Close, ChangePhase, Refresh, FinalizeTerminal, BeginRefresh }
    private sealed record CommandDocument(CommandKind Kind, Guid StepId, ProcessDecisionId? DecisionId = null,
        ProcessDecisionExpectation? Expected = null, ProcessDecisionMaterialId? MaterialId = null,
        ChildDocument? Child = null, ProcessDecisionChildId? ChildId = null, ProcessDecisionChildClosure? Closure = null,
        ProcessDecisionPhaseChange? PhaseChange = null, ProcessDecisionRefreshKind? RefreshKind = null) {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProcessDecisionMaterialStamp? Material { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProcessDecisionRunScopeExpectation? InheritedScope { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProcessDecisionCompletion? Completion { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BudgetDocument? Budget { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProcessRuntimeStepStatus? TerminalStatus { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RefreshIntentDocument? RefreshIntent { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? AdmissionCommandId { get; init; }
    }
    private sealed record BudgetDocument(Guid StepId, Guid BudgetId, long Revision);
    private sealed record GenerationDocument(ProcessDecisionReference Reference, long Revision, ProcessDecisionPhase Phase,
        ProcessDecisionMaterialId MaterialId, Guid BudgetId, int ChildCount, int UnclosedChildCount) {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? RefreshCommandId { get; init; }
    }
    private sealed record RefreshIntentDocument(Guid ActionId, Guid RootRunId, Guid RunId, Guid StepId,
        ProcessDecisionReference Reference, Guid ChildId, string StrategyId, Guid WaitingResultKey, long DecisionRevision,
        Guid BudgetId, long BudgetRevision, Guid? ScopeId, long? ScopeRevision, ProcessDecisionRefreshKind Kind,
        ProcessEventActorKind ActorKind, string ActorId, Guid MaterialId, string InputFingerprint, string ChildFingerprint);
    private sealed record ChildDocument(ProcessDecisionReference Reference, ProcessDecisionChildId ChildId,
        ProcessDecisionChildOwner Owner, Guid AllocationId, ProcessDecisionChildClosure? Closure);
    private sealed record Document(int Version, CommandDocument Command, bool Changed, GenerationDocument Current,
        GenerationDocument? Previous, ChildDocument? Child);
}
