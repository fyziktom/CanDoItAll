namespace CanDoItAll.AgentFramework.Core;

public interface ISandboxWorkspaceExecutionRunLeaseStore {
    ValueTask<IAsyncDisposable> AcquireToolDispatchLeaseAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default);
}

public sealed class AgentToolAdmissionException(string code, string message) : InvalidOperationException(message) {
    public string Code { get; } = code;
}
