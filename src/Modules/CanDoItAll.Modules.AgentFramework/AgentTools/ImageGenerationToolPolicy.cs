using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.AgentFramework;

public static class ImageGenerationToolPolicy {
    private static readonly ToolCapabilityOperationEffects ExternalActionEffects = new(
        [ProcessOperationContractNames.ExternalActionControlled], canExecuteExternalAction: true);

    public const string ImageGenerationCreate = "image_generation_create";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Mutation(ImageGenerationCreate, ToolCapabilitySideEffectKind.MediaGeneration, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) }
    ]);
}
