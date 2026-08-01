using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public enum MemoryOperationCallerKind
{
    RuntimeCompatibility = 0,
    Tool = 1,
    WorkflowExecutor = 2,
    ContextContributor = 3,
    UiAction = 4,
    ApiEndpoint = 5,
    ManualIngestion = 6,
    SourceIngestion = 7,
    ProviderSourceRequest = 8
}

public sealed record MemoryOperationCaller(
    MemoryOperationCallerKind Kind,
    string Route,
    MemoryLedgerRequester Requester,
    MemoryProviderSelectionContext SelectionContext)
{
    public static MemoryOperationCaller RuntimeCompatibility(
        string route,
        MemoryLedgerRequester requester,
        MemoryProviderSelectionContext selectionContext) =>
        Create(MemoryOperationCallerKind.RuntimeCompatibility, route, requester, selectionContext);

    public static MemoryOperationCaller Tool(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.Tool, route, requester, ToSelectionContext(requester));

    public static MemoryOperationCaller WorkflowExecutor(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.WorkflowExecutor, route, requester, ToSelectionContext(requester));

    public static MemoryOperationCaller ContextContributor(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.ContextContributor, route, requester, ToSelectionContext(requester));

    public static MemoryOperationCaller UiAction(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.UiAction, route, requester, ToSelectionContext(requester));

    public static MemoryOperationCaller ApiEndpoint(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.ApiEndpoint, route, requester, ToSelectionContext(requester));

    public static MemoryOperationCaller ManualIngestion(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.ManualIngestion, route, requester, ToSelectionContext(requester));

    public static MemoryOperationCaller SourceIngestion(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.SourceIngestion, route, requester, ToSelectionContext(requester));

    public static MemoryOperationCaller ProviderSourceRequest(
        string route,
        MemoryLedgerRequester requester) =>
        Create(MemoryOperationCallerKind.ProviderSourceRequest, route, requester, ToSelectionContext(requester));

    private static MemoryOperationCaller Create(
        MemoryOperationCallerKind kind,
        string route,
        MemoryLedgerRequester requester,
        MemoryProviderSelectionContext selectionContext)
    {
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(selectionContext);
        return new MemoryOperationCaller(kind, EnsureText(route, nameof(route)), requester, selectionContext);
    }

    private static MemoryProviderSelectionContext ToSelectionContext(MemoryLedgerRequester requester)
    {
        ArgumentNullException.ThrowIfNull(requester);
        return new MemoryProviderSelectionContext(
            requester.AgentId,
            requester.AgentRole,
            requester.WorkflowId,
            requester.WorkflowNodeId,
            requester.ProcessId);
    }

    private static string EnsureText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}
