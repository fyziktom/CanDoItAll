using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Memory.Tools;

public static class MemoryToolPolicy {
    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(MemoryAgentRuntimeToolNames.ContextQuery, ToolCapabilityIdempotencyDescriptor.RuntimeStateDependent),
        Read(MemoryAgentRuntimeToolNames.OperationStatus, ToolCapabilityIdempotencyDescriptor.Idempotent)
    ]);

    private static ToolCapabilityMetadata Read(string name, ToolCapabilityIdempotencyDescriptor idempotency) => new(
        name, ToolInvocationClassification.Read, RequiresApprovalByDefault: false, IsStateChanging: false,
        ToolCapabilitySideEffectKind.InternalDataRead, ToolCapabilityOperationRequirementKind.None,
        OperationRequirements: [], TargetScopeRequirements: [], CanMutateProduct: false, CanExecuteExternalAction: false,
        CanReadExternalTarget: false, CanWriteManagedArtifact: false, ToolCapabilityBrowserProofRole.None, idempotency);
}
