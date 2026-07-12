using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Memory;

public sealed class MemoryWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors() =>
        [MemoryWorkflowExecutorDescriptors.MemoryOperation];
}

internal static class MemoryWorkflowExecutorDescriptors
{
    private static readonly WorkflowExecutorSourceDescriptor BuiltInSource = WorkflowExecutorSourceDescriptor.BuiltIn(
        typeof(MemoryWorkflowExecutorDescriptors).Assembly.GetName().Version?.ToString() ?? string.Empty);

    public static WorkflowExecutorDescriptor MemoryOperation { get; } =
        WorkflowExecutorDescriptorFactory.CreateImplemented(
            WorkflowExecutorIds.Memory,
            "Memory operation",
            "Executes context-query and operation-status Memory Protocol v1 operations through the shared handler.",
            WorkflowExecutorCategoryKind.Data,
            "memory",
            "builtin.memory-operation",
            new MemoryWorkflowExecutorSettings(),
            BuiltInSource,
            inputShape: WorkflowValueShape.Text,
            resultShape: WorkflowExecutorDescriptorFactory.JsonShape,
            defaultPolicy: WorkflowExecutorExecutionPolicy.Default with
            {
                TimeoutSeconds = 90,
                CaptureOutputArtifact = true
            },
            permissionPolicy: new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.ReadsExternalData |
                WorkflowExecutorCapabilityFlags.UsesNetwork |
                WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                WorkflowExecutorApprovalRequirement.NotRequired),
            deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported(
                "Routes to the configured generic memory operation handler, which can be substituted in tests."),
            settingsJsonOptions: WorkflowExecutorJson.Options);
}
