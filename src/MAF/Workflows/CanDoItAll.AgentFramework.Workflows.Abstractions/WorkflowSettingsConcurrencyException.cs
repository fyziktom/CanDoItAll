namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed class WorkflowSettingsConcurrencyException(Exception? innerException = null)
    : InvalidOperationException("Workflow settings changed after they were read. Reload and review the current settings before saving.", innerException) {
}
