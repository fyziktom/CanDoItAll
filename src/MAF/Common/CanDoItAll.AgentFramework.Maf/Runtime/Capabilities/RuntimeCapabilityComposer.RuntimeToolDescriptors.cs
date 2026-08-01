using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tools;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RuntimeConfiguredWorkspaceToolDescriptorCatalog
{
    public static IReadOnlyList<CapabilityExposureDescriptor> CreateConfiguredWorkspaceToolDescriptors(
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        bool storageToolsAvailable)
    {
        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);
        var descriptors = ToolContractCatalog.WorkspaceToolNames
            .Where(toolName => AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(normalized, toolName))
            .Select(toolName => CreateRuntimeToolCapabilityDescriptor(
                toolName,
                "Configured workspace tool",
                "Workspace tool exposed from agent workspace-tool settings.",
                ["configured", "workspace"]))
            .ToList();

        if (storageToolsAvailable && normalized.CanReadStorage)
        {
            descriptors.Add(CreateStorageToolDescriptor(RuntimeStorageToolNames.CatalogList, CapabilityOperationClassification.Read));
            descriptors.Add(CreateStorageToolDescriptor(RuntimeStorageToolNames.ReadTextFile, CapabilityOperationClassification.Read));
        }

        if (storageToolsAvailable && normalized.CanWriteStorage)
        {
            descriptors.Add(CreateStorageToolDescriptor(
                RuntimeStorageToolNames.WriteTextFile,
                CapabilityOperationClassification.Write,
                CapabilityOperationClassification.Mutation));
            descriptors.Add(CreateStorageToolDescriptor(
                RuntimeStorageToolNames.DeleteObject,
                CapabilityOperationClassification.Write,
                CapabilityOperationClassification.Mutation));
        }

        return descriptors;
    }

    private static CapabilityExposureDescriptor CreateStorageToolDescriptor(
        RuntimeToolName runtimeToolName,
        params CapabilityOperationClassification[] classifications)
        => CreateRuntimeToolCapabilityDescriptor(
            runtimeToolName.Value,
            "Configured storage tool",
            "Storage tool exposed from agent workspace-tool settings.",
            ["configured", "storage"],
            classifications.ToHashSet());

    private static CapabilityExposureDescriptor CreateRuntimeToolCapabilityDescriptor(
        string toolName,
        string displayName,
        string description,
        IReadOnlyList<string> sourceTags,
        IReadOnlySet<CapabilityOperationClassification>? operationClassifications = null)
        => RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
            toolName,
            displayName,
            description,
            sourceTags,
            operationClassifications);

    private static IReadOnlySet<CapabilityOperationClassification> ResolveRuntimeToolOperationClassifications(string runtimeToolName)
        => RuntimeToolCapabilityDescriptorFactory.ResolveRuntimeToolOperationClassifications(runtimeToolName);

    private static CapabilitySideEffectProfile ResolveRuntimeToolSideEffectProfile(string runtimeToolName)
        => RuntimeToolCapabilityDescriptorFactory.ResolveRuntimeToolSideEffectProfile(runtimeToolName);

    private static CapabilitySideEffectKind MapSideEffectKind(ToolCapabilitySideEffectKind sideEffectKind)
        => RuntimeToolCapabilityDescriptorFactory.MapSideEffectKind(sideEffectKind);

    private static bool TryCreateRuntimeToolName(string toolName, out RuntimeToolName runtimeToolName)
        => RuntimeToolCapabilityDescriptorFactory.TryCreateRuntimeToolName(toolName, out runtimeToolName);

    private static bool IsWorkspaceTextWriteTool(string runtimeToolName)
        => RuntimeToolCapabilityDescriptorFactory.IsWorkspaceTextWriteTool(runtimeToolName);

    private static IReadOnlySet<CapabilityOperationClassification> ToClassificationSet(
        params CapabilityOperationClassification[] classifications)
        => RuntimeToolCapabilityDescriptorFactory.ToClassificationSet(classifications);
}
