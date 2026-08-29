using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Web.Api;

internal static class ProviderHistoryRequestContext {
    public static HistoryCaller Caller(HttpContext context) => SharedProviderCallerHistoryMapper.Map(
        SharedProviderCallerSnapshot.From(context), SharedProviderCallerSnapshot.Subject(context.User));

    public static ExecutionInvocationContext ForExecution(ExecutionInvocationContext? invocation, HttpContext context) =>
        (invocation ?? ExecutionInvocationContext.Empty) with { HistoryCaller = Caller(context) };

    public static ProviderTestChatRequest WithCaller(ProviderTestChatRequest request, HttpContext context) =>
        request with {
            History = HistoryInvocationContext.Create(
                caller: Caller(context),
                currentTurn: new(request.Prompt, 0),
                correlationId: context.TraceIdentifier is { Length: > 0 and <= 128 } trace && !trace.Any(char.IsControl)
                    ? trace : null)
        };
}
