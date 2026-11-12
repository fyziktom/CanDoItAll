using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionMaterialCaptureRequest(ProcessLaunchAuthority Source, ProcessDecisionMaterialScope Scope,
    ProcessDecisionContentContract ContentContract, ProcessDecisionMaterialReference Material);

public enum ProcessDecisionMaterialCaptureRefusal { SourceChanged, SourceMissing, LifetimeChanged, DatasetChanged, InvalidSource, LimitExceeded }

public abstract record ProcessDecisionMaterialCaptureResult {
    private ProcessDecisionMaterialCaptureResult() { }

    public sealed record Captured(ProcessDecisionMaterial Material) : ProcessDecisionMaterialCaptureResult;
    public sealed record Refused(ProcessDecisionMaterialCaptureRefusal Reason) : ProcessDecisionMaterialCaptureResult;
}

public interface IProcessDecisionMaterialCapturer {
    ProcessDecisionContentContract ContentContract { get; }
    Task<ProcessDecisionMaterialCaptureResult> CaptureAsync(ProcessDecisionMaterialCaptureRequest request, CancellationToken cancellationToken = default);
}
