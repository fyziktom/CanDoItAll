namespace CanDoItAll.AgentFramework.Core;

public sealed record WorkflowDefinitionValidationOptions(bool RequireRunnableExecutors)
{
    public static WorkflowDefinitionValidationOptions Default { get; } = new(RequireRunnableExecutors: true);

    public static WorkflowDefinitionValidationOptions RegisteredExecutorsOnly { get; } = new(RequireRunnableExecutors: false);
}
