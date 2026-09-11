using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public static class ToolCapabilityMetadataFactory {
    public static IReadOnlyList<CapabilityOperationClassification> Classifications(params CapabilityOperationClassification[] classifications)
        => Array.AsReadOnly(classifications.ToArray());

    public static ToolCapabilityMetadata Read(string name, ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements = null, ToolCapabilityOperationEffects? effects = null)
        => Capability(name, ToolInvocationClassification.Read, false, false, sideEffectKind, requirements, effects);

    public static ToolCapabilityMetadata Validation(string name, ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements = null, ToolCapabilityOperationEffects? effects = null)
        => Capability(name, ToolInvocationClassification.Validation, false, false, sideEffectKind, requirements, effects);

    public static ToolCapabilityMetadata Validation(string name, ToolCapabilitySideEffectKind sideEffectKind,
        ToolCapabilityOperationRequirementKind requirementKind, ToolCapabilityOperationEffects? effects = null)
        => Capability(name, ToolInvocationClassification.Validation, false, false, sideEffectKind, requirementKind, [], effects);

    public static ToolCapabilityMetadata Mutation(string name, ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements = null, ToolCapabilityOperationEffects? effects = null)
        => Capability(name, ToolInvocationClassification.Mutation, true, true, sideEffectKind, requirements, effects);

    public static ToolCapabilityMetadata Mutation(string name, ToolCapabilitySideEffectKind sideEffectKind,
        ToolCapabilityOperationRequirementKind requirementKind, ToolCapabilityOperationEffects? effects = null)
        => Capability(name, ToolInvocationClassification.Mutation, true, true, sideEffectKind, requirementKind, [], effects);

    private static ToolCapabilityMetadata Capability(string name, ToolInvocationClassification classification,
        bool requiresApprovalByDefault, bool isStateChanging, ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements, ToolCapabilityOperationEffects? effects) {
        var operationRequirements = requirements ?? [];
        return Capability(name, classification, requiresApprovalByDefault, isStateChanging, sideEffectKind,
            operationRequirements.Count == 0 ? ToolCapabilityOperationRequirementKind.None : ToolCapabilityOperationRequirementKind.Static,
            operationRequirements, effects);
    }

    private static ToolCapabilityMetadata Capability(string name, ToolInvocationClassification classification,
        bool requiresApprovalByDefault, bool isStateChanging, ToolCapabilitySideEffectKind sideEffectKind,
        ToolCapabilityOperationRequirementKind requirementKind, IReadOnlyList<ToolCapabilityProcessOperationRequirement> requirements,
        ToolCapabilityOperationEffects? effects) {
        if (effects is null && (requirementKind != ToolCapabilityOperationRequirementKind.None ||
                requirements.Count != 0 || sideEffectKind == ToolCapabilitySideEffectKind.ExternalAction)) {
            throw new InvalidOperationException($"Tool capability '{name}' requires explicit owner-provided operation effects.");
        }
        var supplied = effects ?? ToolCapabilityOperationEffects.None;
        return new(ToolContractCatalog.NormalizeToolName(name), classification, requiresApprovalByDefault, isStateChanging,
            sideEffectKind, requirementKind, requirements, supplied.TargetScopeRequirements, supplied.CanMutateProduct,
            supplied.CanExecuteExternalAction, CanReadExternalTarget(classification, sideEffectKind), supplied.CanWriteManagedArtifact,
            ToolCapabilityBrowserProofRole.None, ResolveIdempotencyDescriptor(classification, sideEffectKind));
    }

    public static IReadOnlyList<ToolCapabilityProcessOperationRequirement> StaticRequirement(params string[] operations)
        => [ToolCapabilityProcessOperationRequirement.Any(operations)];

    private static bool CanReadExternalTarget(ToolInvocationClassification classification, ToolCapabilitySideEffectKind sideEffectKind)
        => classification is ToolInvocationClassification.Read or ToolInvocationClassification.Validation ||
            sideEffectKind is ToolCapabilitySideEffectKind.WorkspaceRead or ToolCapabilitySideEffectKind.WorkspaceWrite or
                ToolCapabilitySideEffectKind.LocalProcessExecution or ToolCapabilitySideEffectKind.RuntimeLaunch or
                ToolCapabilitySideEffectKind.RuntimeProofCapture;

    private static ToolCapabilityIdempotencyDescriptor ResolveIdempotencyDescriptor(
        ToolInvocationClassification classification, ToolCapabilitySideEffectKind sideEffectKind) {
        if (sideEffectKind is ToolCapabilitySideEffectKind.LocalProcessExecution or ToolCapabilitySideEffectKind.ExternalAction) {
            return ToolCapabilityIdempotencyDescriptor.ExternalSideEffect;
        }
        return classification switch {
            ToolInvocationClassification.Mutation => ToolCapabilityIdempotencyDescriptor.StateChanging,
            ToolInvocationClassification.Validation => ToolCapabilityIdempotencyDescriptor.RuntimeStateDependent,
            _ => ToolCapabilityIdempotencyDescriptor.Idempotent
        };
    }
}
