using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentExecutionAdmissionReadLease : IAsyncDisposable {
    ExecutionRunAdmissionSnapshot Snapshot { get; }
    AgentExecutionPublicationRecovery Recovery
        => throw new NotSupportedException("This execution lease cannot describe its original publication exclusion.");
}
