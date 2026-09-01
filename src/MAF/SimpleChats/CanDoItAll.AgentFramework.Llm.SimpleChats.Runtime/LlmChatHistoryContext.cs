using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;

internal static class LlmChatHistoryContext {
    internal static LlmInvocationRequest Attach(LlmInvocationRequest request, LlmChatOperationId operationId, HistoryCaller? caller) =>
        request with {
            History = request.History with {
                Workload = HistoryWorkload.SimpleChat,
                Caller = caller ?? request.History.Caller,
                Owner = new(HistorySourceKind.SimpleChat, new(operationId.Value.ToString("N")),
                    new(operationId.Value.ToString("N"))),
                CurrentTurn = null
            }
        };
}
