using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record ProcessExecutionRunPageQuery(Guid ProcessRunId, Guid? AfterExecutionRunId = null, int Take = 16) {
    public void Validate() {
        if (ProcessRunId == Guid.Empty || AfterExecutionRunId == Guid.Empty || Take is < 1 or > 64) {
            throw new ArgumentOutOfRangeException(nameof(ProcessExecutionRunPageQuery), "Process selection requires an original identity and a page of 1–64 records.");
        }
    }
}

public enum ProcessExecutionRunPageStatus { Ready, IndexUnavailable, PublicationPending, LayoutUnavailable }

public sealed record ProcessExecutionRunPage(ProcessExecutionRunPageStatus Status,
    IReadOnlyList<ExecutionRunRecord> Candidates, Guid? LastExecutionRunId, bool Exhausted) {
    public static ProcessExecutionRunPage Pending(ProcessExecutionRunPageStatus status) {
        if (!Enum.IsDefined(status) || status == ProcessExecutionRunPageStatus.Ready) {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
        return new(status, [], null, false);
    }
}

public interface ISandboxWorkspaceProcessRunPageStore {
    Task<ProcessExecutionRunPage> ReadProcessExecutionRunPageAsync(ProcessExecutionRunPageQuery query,
        CancellationToken cancellationToken = default);
}
