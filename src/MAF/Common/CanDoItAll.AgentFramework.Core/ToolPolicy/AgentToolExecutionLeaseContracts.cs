namespace CanDoItAll.AgentFramework.Core;

public interface ISandboxWorkspaceExecutionRunLeaseStore {
    ValueTask<IAsyncDisposable> AcquireToolDispatchLeaseAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default);
}

public sealed class AgentToolAdmissionException : InvalidOperationException {
    public AgentToolAdmissionException(string code, string message) : base(message) {
        Code = code;
    }

    private AgentToolAdmissionException(AgentToolAdmissionException original) : base(original.Message, original) {
        Code = original.Code;
        IsEffectReconciliation = true;
    }

    public const string UnsupportedProtocolCode = "tool-admission.unsupported-protocol";

    public string Code { get; }
    internal bool IsEffectReconciliation { get; }
    internal AgentToolAdmissionException ForEffectReconciliation() => IsEffectReconciliation ? this : new(this);
}
