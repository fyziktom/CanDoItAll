using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public interface IPluginWorkflowExecutorGrantEvaluator
{
    PluginGrantDecision Evaluate(
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId = null);
}
