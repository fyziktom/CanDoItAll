namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchRouteHandlerPipeline(IReadOnlyList<IProcessDispatchRouteHandler> handlers)
{
    private readonly IReadOnlyList<IProcessDispatchRouteHandler> handlers = ValidateHandlers(handlers);

    public async Task<ProcessClaimedDispatchResult> ExecuteAsync(ProcessDispatchRouteContext context)
    {
        foreach (var handler in handlers)
        {
            var result = await handler.HandleAsync(context);
            if (!result.Handled)
            {
                continue;
            }

            return result.ToClaimedDispatchResult();
        }

        throw new InvalidOperationException("The claimed dispatch route handler pipeline completed without a terminal result.");
    }

    private static IReadOnlyList<IProcessDispatchRouteHandler> ValidateHandlers(
        IReadOnlyList<IProcessDispatchRouteHandler> handlers)
    {
        ProcessDispatchRouteOrderAssertion.ThrowIfStageOrderInvalid(handlers.Select(handler => handler.Stage).ToArray());

        return handlers;
    }
}

internal static class ProcessDispatchRouteOrderAssertion
{
    public static void ThrowIfStageOrderInvalid(IReadOnlyList<ProcessDispatchRouteStage> actualStageOrder)
    {
        if (ProcessDispatchRoutePipeline.StageOrder.SequenceEqual(actualStageOrder))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Process dispatch route handler order must match the canonical route stage order. Expected: {FormatStageOrder(ProcessDispatchRoutePipeline.StageOrder)}. Actual: {FormatStageOrder(actualStageOrder)}.");
    }

    private static string FormatStageOrder(IReadOnlyList<ProcessDispatchRouteStage> stageOrder)
    {
        return string.Join(" -> ", stageOrder);
    }
}
