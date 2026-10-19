using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessDecisionRunScopeReceiptCodec {
    internal const int MaximumUtf8Bytes = 8192;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, MaxDepth = 16
    };

    internal static string? Write(ProcessDecisionRunScopeReceipt? receipt) {
        if (receipt is null) {
            return null;
        }
        receipt.Command.Validate();
        receipt.State.Validate();
        var command = receipt.Command;
        var state = receipt.State;
        var admission = state.Admission;
        var phase = command.Kind switch {
            ProcessDecisionRunScopeCommandKind.BeginReconciliation => ProcessDecisionRunScopePhase.Reconciling,
            ProcessDecisionRunScopeCommandKind.BeginCapture => ProcessDecisionRunScopePhase.Capturing,
            ProcessDecisionRunScopeCommandKind.CompleteCapture => ProcessDecisionRunScopePhase.Active,
            ProcessDecisionRunScopeCommandKind.AbortCapture => state.Material is null ? ProcessDecisionRunScopePhase.AwaitingCapture : ProcessDecisionRunScopePhase.Reconciling,
            ProcessDecisionRunScopeCommandKind.Complete => ProcessDecisionRunScopePhase.Completed,
            ProcessDecisionRunScopeCommandKind.Cancel => ProcessDecisionRunScopePhase.Cancelled,
            _ => throw new InvalidDataException("The shared scope command is unsupported.")
        };
        if (command.Expectation.ScopeId != admission.ScopeId || state.Revision <= command.Expectation.Revision ||
                state.Revision - command.Expectation.Revision != 1 || state.Phase != phase ||
                command.Kind == ProcessDecisionRunScopeCommandKind.BeginCapture && command.Capture != state.Capture ||
                command.Kind == ProcessDecisionRunScopeCommandKind.CompleteCapture && command.Material != state.Material?.Stamp) {
            throw new InvalidDataException("The shared scope receipt differs from its original command and result.");
        }
        var json = JsonSerializer.Serialize(new Document(1, command.Expectation, command.Kind, CaptureDocument.From(command.Capture), command.Material,
            new(admission.ScopeId, admission.Funding.RootRunId.Value, admission.Funding.BudgetId.Value, admission.DatabaseProfileId,
                admission.WorkspaceNamespaceId, admission.ContentContract), state.Revision, state.Phase, state.Material,
            state.TotalMemberships, state.CurrentMemberships, CaptureDocument.From(state.Capture)), Options);
        RequireBound(json);
        return json;
    }

    internal static ProcessDecisionRunScopeReceipt? Read(string? json) {
        if (json is null) {
            return null;
        }
        RequireBound(json);
        var wire = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new InvalidDataException("The shared scope receipt is empty.");
        if (wire.Version != 1 || wire.Admission is not { } admission) {
            throw new InvalidDataException("The shared scope receipt has an unsupported version or missing original admission.");
        }
        ProcessDecisionRunScopeReceipt result = new(new(wire.Expected, wire.Kind, wire.RequestedCapture?.ToModel(), wire.RequestedMaterial),
            new(new(admission.ScopeId, new(new(admission.RootRunId), new(admission.BudgetId)), admission.DatabaseProfileId,
                admission.WorkspaceNamespaceId, admission.ContentContract), wire.Revision, wire.Phase, wire.Material,
                wire.TotalMemberships, wire.CurrentMemberships, wire.Capture?.ToModel()));
        if (json != Write(result)) {
            throw new InvalidDataException("The shared scope receipt is not its canonical original encoding.");
        }
        return result;
    }

    internal static void RequireSameCommand(ProcessDecisionRunScopeCommand? command, ProcessDecisionRunScopeReceipt? receipt) {
        if (command != receipt?.Command) {
            throw new InvalidOperationException("The runtime command has different original shared scope parameters.");
        }
    }

    private static void RequireBound(string json) {
        if (Encoding.UTF8.GetByteCount(json) is 0 or > MaximumUtf8Bytes) {
            throw new InvalidDataException("The shared scope receipt exceeds its retained bound.");
        }
    }

    private sealed record AdmissionDocument(ProcessDecisionMaterialScopeId ScopeId, Guid RootRunId, Guid BudgetId, Guid DatabaseProfileId,
        Guid WorkspaceNamespaceId, ProcessDecisionContentContract ContentContract);
    private sealed record CaptureDocument(Guid AllocationId, ProcessDecisionMaterialId MaterialId, ProcessDecisionRunCaptureKind Kind) {
        internal ProcessDecisionRunScopeCapture ToModel() => new(new(AllocationId), MaterialId, Kind);
        internal static CaptureDocument? From(ProcessDecisionRunScopeCapture? capture)
            => capture is null ? null : new(capture.AllocationId.Value, capture.MaterialId, capture.Kind);
    }
    private sealed record Document(int Version, ProcessDecisionRunScopeExpectation Expected, ProcessDecisionRunScopeCommandKind Kind,
        CaptureDocument? RequestedCapture, ProcessDecisionMaterialStamp? RequestedMaterial, AdmissionDocument Admission,
        long Revision, ProcessDecisionRunScopePhase Phase, ProcessDecisionRunScopeMaterial? Material, int TotalMemberships,
        int CurrentMemberships, CaptureDocument? Capture);
}
