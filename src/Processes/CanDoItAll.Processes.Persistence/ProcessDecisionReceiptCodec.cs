using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        var json = JsonSerializer.Serialize(new Document(receipt.Command.Material is null ? 1 : 2,
            ToDocument(receipt.Command) with { Material = receipt.Command.Material }, receipt.Changed, ToDocument(receipt.Current),
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
        if (document.Version is not (1 or 2) || document.Command is null || document.Current is null ||
                (document.Version == 2) != (document.Command.Material is not null)) {
            throw new InvalidDataException("The Process decision receipt has an unsupported version or missing command.");
        }
        var command = FromDocument(document.Command) with { Material = document.Command.Material };
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
        ProcessDecisionClosure closure => new(CommandKind.Close, closure.StepInstanceId.Value, Expected: closure.Expectation, ChildId: closure.ChildId, Closure: closure.Closure),
        ProcessDecisionChangePhase phase => new(CommandKind.ChangePhase, phase.StepInstanceId.Value, Expected: phase.Expectation, PhaseChange: phase.Change),
        ProcessDecisionRefresh refresh => new(CommandKind.Refresh, refresh.StepInstanceId.Value, Expected: refresh.Expectation, MaterialId: refresh.MaterialId, RefreshKind: refresh.Kind),
        _ => throw new InvalidDataException("The Process decision command is unsupported.")
    };

    private static ProcessDecisionCommand FromDocument(CommandDocument document) => document.Kind switch {
        CommandKind.Start when document.DecisionId is { } id && document.MaterialId is { } material => new ProcessDecisionStart(new(document.StepId), id, material),
        CommandKind.Enroll when document.Expected is { } expected && document.Child is { } child => new ProcessDecisionEnrollment(new(document.StepId), expected, FromDocument(child)),
        CommandKind.Close when document.Expected is { } expected && document.ChildId is { } child && document.Closure is { } closure => new ProcessDecisionClosure(new(document.StepId), expected, child, closure),
        CommandKind.ChangePhase when document.Expected is { } expected && document.PhaseChange is { } change => new ProcessDecisionChangePhase(new(document.StepId), expected, change),
        CommandKind.Refresh when document.Expected is { } expected && document.MaterialId is { } material && document.RefreshKind is { } kind => new ProcessDecisionRefresh(new(document.StepId), expected, material, kind),
        _ => throw new InvalidDataException("The Process decision receipt has an invalid command shape.")
    };

    private static GenerationDocument ToDocument(ProcessDecisionGeneration generation)
        => new(generation.Reference, generation.Revision, generation.Phase, generation.MaterialId, generation.BudgetId.Value,
            generation.ChildCount, generation.UnclosedChildCount);

    private static ProcessDecisionGeneration FromDocument(GenerationDocument generation)
        => new(generation.Reference, generation.Revision, generation.Phase, generation.MaterialId, new(generation.BudgetId),
            generation.ChildCount, generation.UnclosedChildCount);

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
                child.ChildId == closure.ChildId && child.Closure == closure.Closure,
            ProcessDecisionChangePhase phase => receipt.Changed && receipt.Previous is null && child is null &&
                current.Reference == phase.Expectation.Reference && current.Phase == ResultPhase(phase.Change),
            ProcessDecisionRefresh refresh => receipt.Changed && child is null && receipt.Previous is { } previous &&
                previous.Reference == refresh.Expectation.Reference && previous.Phase == ProcessDecisionPhase.Superseded &&
                previous.Revision == current.Revision && previous.BudgetId == current.BudgetId &&
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

    private enum CommandKind { Start, Enroll, Close, ChangePhase, Refresh }
    private sealed record CommandDocument(CommandKind Kind, Guid StepId, ProcessDecisionId? DecisionId = null,
        ProcessDecisionExpectation? Expected = null, ProcessDecisionMaterialId? MaterialId = null,
        ChildDocument? Child = null, ProcessDecisionChildId? ChildId = null, ProcessDecisionChildClosure? Closure = null,
        ProcessDecisionPhaseChange? PhaseChange = null, ProcessDecisionRefreshKind? RefreshKind = null) {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProcessDecisionMaterialStamp? Material { get; init; }
    }
    private sealed record GenerationDocument(ProcessDecisionReference Reference, long Revision, ProcessDecisionPhase Phase,
        ProcessDecisionMaterialId MaterialId, Guid BudgetId, int ChildCount, int UnclosedChildCount);
    private sealed record ChildDocument(ProcessDecisionReference Reference, ProcessDecisionChildId ChildId,
        ProcessDecisionChildOwner Owner, Guid AllocationId, ProcessDecisionChildClosure? Closure);
    private sealed record Document(int Version, CommandDocument Command, bool Changed, GenerationDocument Current,
        GenerationDocument? Previous, ChildDocument? Child);
}
