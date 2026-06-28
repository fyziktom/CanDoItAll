using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tools;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private static IReadOnlyList<CapabilityExposureDescriptor> CreateConfiguredWorkspaceToolDescriptors(
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
            descriptors.Add(CreateStorageToolDescriptor(StorageRuntimeToolNames[0], CapabilityOperationClassification.Read));
            descriptors.Add(CreateStorageToolDescriptor(StorageRuntimeToolNames[1], CapabilityOperationClassification.Read));
        }

        if (storageToolsAvailable && normalized.CanWriteStorage)
        {
            descriptors.Add(CreateStorageToolDescriptor(
                StorageRuntimeToolNames[2],
                CapabilityOperationClassification.Write,
                CapabilityOperationClassification.Mutation));
            descriptors.Add(CreateStorageToolDescriptor(
                StorageRuntimeToolNames[3],
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
    {
        if (!TryCreateRuntimeToolName(toolName, out var runtimeToolName))
        {
            throw new InvalidOperationException($"Runtime tool name '{toolName}' cannot be represented as a capability descriptor.");
        }

        var classifications = operationClassifications ?? ResolveRuntimeToolOperationClassifications(runtimeToolName.Value);
        var tags = new HashSet<CapabilityTag>
        {
            CapabilityTag.Create("tool")
        };
        foreach (var sourceTag in sourceTags)
        {
            tags.Add(CapabilityTag.Create(sourceTag));
        }

        foreach (var classification in classifications)
        {
            tags.Add(CapabilityTag.Create(ToKebab(classification.ToString())));
        }

        var descriptor = ToolDescriptorFactory.Internal(
            CreateRuntimeToolCapabilityKey(runtimeToolName),
            runtimeToolName,
            ImplementationKey.Create($"maf.{runtimeToolName.Value}"),
            tags,
            classifications,
            ResolveRuntimeToolSideEffectProfile(runtimeToolName.Value));
        return ToolExposureDescriptorFactory.Create(descriptor) with
        {
            DisplayName = displayName,
            Description = description,
            SourcePath = null
        };
    }

    private static CapabilitySideEffectProfile ResolveRuntimeToolSideEffectProfile(string runtimeToolName)
    {
        if (ToolCapabilityRegistry.TryResolve(runtimeToolName, out var metadata))
        {
            return new CapabilitySideEffectProfile(
                MapSideEffectKind(metadata.SideEffectKind),
                metadata.RequiresApprovalByDefault,
                metadata.IsStateChanging);
        }

        return new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false);
    }

    private static CapabilitySideEffectKind MapSideEffectKind(ToolCapabilitySideEffectKind sideEffectKind)
        => sideEffectKind switch
        {
            ToolCapabilitySideEffectKind.None => CapabilitySideEffectKind.None,
            ToolCapabilitySideEffectKind.WorkspaceRead => CapabilitySideEffectKind.WorkspaceRead,
            ToolCapabilitySideEffectKind.WorkspaceWrite => CapabilitySideEffectKind.WorkspaceWrite,
            ToolCapabilitySideEffectKind.LocalProcessExecution => CapabilitySideEffectKind.LocalProcessExecution,
            ToolCapabilitySideEffectKind.RuntimeLaunch => CapabilitySideEffectKind.RuntimeLaunch,
            ToolCapabilitySideEffectKind.RuntimeProofCapture => CapabilitySideEffectKind.RuntimeProofCapture,
            ToolCapabilitySideEffectKind.ProcessMutation => CapabilitySideEffectKind.ProcessMutation,
            ToolCapabilitySideEffectKind.ProjectStructureMutation => CapabilitySideEffectKind.ProjectStructureMutation,
            ToolCapabilitySideEffectKind.ExternalAction => CapabilitySideEffectKind.ExternalAction,
            ToolCapabilitySideEffectKind.MediaGeneration => CapabilitySideEffectKind.MediaGeneration,
            ToolCapabilitySideEffectKind.DocumentConversion => CapabilitySideEffectKind.DocumentConversion,
            ToolCapabilitySideEffectKind.ProviderNative => CapabilitySideEffectKind.ProviderNative,
            ToolCapabilitySideEffectKind.McpTool => CapabilitySideEffectKind.McpTool,
            _ => CapabilitySideEffectKind.None
        };

    private static IReadOnlySet<CapabilityOperationClassification> ResolveRuntimeToolOperationClassifications(string runtimeToolName)
    {
        if (!ToolCapabilityRegistry.TryResolve(runtimeToolName, out var metadata))
        {
            var classification = ToolCapabilityRegistry.Classify(runtimeToolName);
            return classification switch
            {
                ToolInvocationClassification.Read => ToClassificationSet(CapabilityOperationClassification.Read),
                ToolInvocationClassification.Validation => ToClassificationSet(CapabilityOperationClassification.Validation),
                ToolInvocationClassification.Mutation => ToClassificationSet(CapabilityOperationClassification.Mutation),
                ToolInvocationClassification.HostedProviderNative => ToClassificationSet(CapabilityOperationClassification.ProviderNative),
                ToolInvocationClassification.LocalMcp or ToolInvocationClassification.HostedMcp => ToClassificationSet(CapabilityOperationClassification.McpTool),
                _ => ToClassificationSet(CapabilityOperationClassification.Read)
            };
        }

        if (metadata.OperationRequirementKind == ToolCapabilityOperationRequirementKind.WorkspaceFileMutation)
        {
            return ToClassificationSet(CapabilityOperationClassification.Mutation, CapabilityOperationClassification.Write);
        }

        if (metadata.OperationRequirementKind == ToolCapabilityOperationRequirementKind.WorkspaceScript)
        {
            return ToClassificationSet(CapabilityOperationClassification.ScriptExecution);
        }

        if (metadata.OperationRequirementKind == ToolCapabilityOperationRequirementKind.DotNetRun)
        {
            return ToClassificationSet(CapabilityOperationClassification.Validation);
        }

        if (metadata.OperationRequirementKind == ToolCapabilityOperationRequirementKind.ProcessArtifactWrite)
        {
            return ToClassificationSet(CapabilityOperationClassification.Write);
        }

        var operationClassifications = metadata.OperationRequirements
            .SelectMany(requirement => requirement.AnyOf)
            .SelectMany(ProcessAllowedOperationsCapabilityPolicyCompiler.ResolveClassifications)
            .ToHashSet();
        if (operationClassifications.Count > 0)
        {
            return operationClassifications;
        }

        return metadata.Classification switch
        {
            ToolInvocationClassification.Read => ToClassificationSet(CapabilityOperationClassification.Read),
            ToolInvocationClassification.Validation => ToClassificationSet(CapabilityOperationClassification.Validation),
            ToolInvocationClassification.Mutation => ToClassificationSet(CapabilityOperationClassification.Mutation),
            ToolInvocationClassification.HostedProviderNative => ToClassificationSet(CapabilityOperationClassification.ProviderNative),
            ToolInvocationClassification.LocalMcp or ToolInvocationClassification.HostedMcp => ToClassificationSet(CapabilityOperationClassification.McpTool),
            _ => ToClassificationSet(CapabilityOperationClassification.Read)
        };
    }

    private static bool TryCreateRuntimeToolName(string toolName, out RuntimeToolName runtimeToolName)
    {
        var normalized = string.IsNullOrWhiteSpace(toolName)
            ? string.Empty
            : toolName.Trim().Replace('-', '_').ToLowerInvariant();
        return RuntimeToolName.TryCreate(normalized, out runtimeToolName);
    }

    private static CapabilityKey CreateRuntimeToolCapabilityKey(RuntimeToolName runtimeToolName)
        => CapabilityKey.Create(runtimeToolName.Value.Replace('_', '-'));

    private static IReadOnlySet<CapabilityOperationClassification> ToClassificationSet(
        params CapabilityOperationClassification[] classifications)
        => classifications.ToHashSet();
}
