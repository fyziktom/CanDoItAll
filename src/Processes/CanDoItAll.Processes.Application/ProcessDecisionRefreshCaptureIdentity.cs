using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionRefreshCaptureIdentity(RuntimeCommandId AdmissionId) {
    public RuntimeCommandId RootReserve => new(Derive(Stage.RootReserve));
    public RuntimeCommandId RootComplete => new(Derive(Stage.RootComplete));
    public RuntimeCommandId RootAbort => new(Derive(Stage.RootAbort));
    public RuntimeCommandId LocalReserve => new(Derive(Stage.LocalReserve));
    public RuntimeCommandId LocalAbort => new(Derive(Stage.LocalAbort));
    public RuntimeCommandId Successor => new ProcessDecisionRefreshSuccessor(AdmissionId).CommandId;
    public DecisionBudgetAllocationId RootAllocation => new(Derive(Stage.RootAllocation));
    public DecisionBudgetAllocationId LocalAllocation => new(Derive(Stage.LocalAllocation));
    public ProcessDecisionMaterialId Material => new(Derive(Stage.Material));

    private Guid Derive(Stage stage) {
        ArgumentOutOfRangeException.ThrowIfEqual(AdmissionId.Value, Guid.Empty);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "process-decision-refresh-capture/v1", AdmissionId, Stage = stage
        });
        return new Guid(SHA256.HashData(bytes).AsSpan(0, 16), bigEndian: true);
    }

    private enum Stage {
        RootReserve = 1, RootComplete = 2, RootAbort = 3, LocalReserve = 4, LocalAbort = 5,
        RootAllocation = 6, LocalAllocation = 7, Material = 8
    }
}
