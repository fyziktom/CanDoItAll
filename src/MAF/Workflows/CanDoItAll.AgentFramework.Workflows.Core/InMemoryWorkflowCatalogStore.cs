using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowCatalogStore
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    internal Dictionary<WorkflowId, List<WorkflowDefinition>> Definitions { get; } = [];

    internal Dictionary<WorkflowComponentId, LlmCallComponent> Components { get; } = [];

    internal WorkflowSettings Settings { get; set; } = WorkflowSettings.Default;
}
