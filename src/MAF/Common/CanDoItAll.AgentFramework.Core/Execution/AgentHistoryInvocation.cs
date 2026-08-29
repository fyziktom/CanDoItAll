using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Core.Execution;

internal static class AgentHistoryInvocation {
    internal static HistoryInvocationContext Create(ExecutionRunRecord run) {
        var requestId = ProviderRequestId.New();
        return new(requestId, string.IsNullOrEmpty(run.ProcessRunId) ? HistoryWorkload.Agent : HistoryWorkload.Process,
            run.HistoryCaller ?? new(HistoryAuthenticationKind.Unknown),
            new(HistorySourceKind.AgentConversation, new(run.Id.ToString("N")), new(requestId.Value.ToString("N"))),
            CorrelationId: run.CorrelationId);
    }
}
