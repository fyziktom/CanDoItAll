using CanDoItAll.AgentFramework.Models;

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
        CancelledSession = original.CancelledSession;
    }

    public const string UnsupportedProtocolCode = "tool-admission.unsupported-protocol";
    public const string CancelledExecutionCode = "tool-admission.cancelled-execution";

    public string Code { get; }
    internal AgentToolSessionAdmission? CancelledSession { get; private init; }
    internal static AgentToolAdmissionException Cancelled(AgentToolSessionAdmission original)
        => new(CancelledExecutionCode, "The original background execution was cancelled by its owner.") { CancelledSession = original };
    internal bool IsEffectReconciliation { get; }
    internal AgentToolAdmissionException ForEffectReconciliation() => IsEffectReconciliation ? this : new(this);
}
