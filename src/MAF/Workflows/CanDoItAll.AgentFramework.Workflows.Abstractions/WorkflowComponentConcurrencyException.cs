using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed class WorkflowComponentConcurrencyException(WorkflowComponentId id, Exception? innerException = null)
    : InvalidOperationException($"Workflow component '{id}' or its bound Prompt changed after it was read. Review the current values before submitting another change.", innerException) {
}
