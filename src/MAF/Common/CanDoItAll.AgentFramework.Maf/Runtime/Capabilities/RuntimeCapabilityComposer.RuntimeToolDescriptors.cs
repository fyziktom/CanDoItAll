using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RuntimeConfiguredWorkspaceToolDescriptorCatalog {
    public static IReadOnlyList<CapabilityExposureDescriptor> CreateConfiguredWorkspaceToolDescriptors(
        AgentWorkspaceToolAccessSettings workspaceToolAccess) {
        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);
        return ToolContractCatalog.WorkspaceToolNames
            .Where(toolName => AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(normalized, toolName))
            .Select(toolName => RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
                toolName, "Configured workspace tool", "Workspace tool exposed from agent workspace-tool settings.",
                ["configured", "workspace"]))
            .ToArray();
    }
}
