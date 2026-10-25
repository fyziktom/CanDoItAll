namespace CanDoItAll.Processes.Runtime;

public enum ProcessDecisionRunScopeCommandKind { BeginReconciliation, BeginCapture, CompleteCapture, AbortCapture, Complete, Cancel,
    CompleteResult, CancelResult }

public sealed record ProcessDecisionRunScopeCommand(ProcessDecisionRunScopeExpectation Expectation, ProcessDecisionRunScopeCommandKind Kind,
    ProcessDecisionRunScopeCapture? Capture = null, ProcessDecisionMaterialStamp? Material = null) {
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CompletesRuntimeResult => Kind is ProcessDecisionRunScopeCommandKind.CompleteResult or ProcessDecisionRunScopeCommandKind.CancelResult;

    public void Validate() {
        ArgumentNullException.ThrowIfNull(Expectation);
        Expectation.Validate();
        Capture?.Validate();
        Material?.Validate();
        if (!Enum.IsDefined(Kind) || (Kind == ProcessDecisionRunScopeCommandKind.BeginCapture) != (Capture is not null) ||
                (Kind == ProcessDecisionRunScopeCommandKind.CompleteCapture) != (Material is not null) ||
                Material is not null && Material.Reference.ScopeId != Expectation.ScopeId) {
            throw new InvalidDataException("The shared material scope command has an invalid original capture or material shape.");
        }
    }
}

public sealed record ProcessDecisionRunScopeReceipt(ProcessDecisionRunScopeCommand Command, ProcessDecisionRunScopeState State);
