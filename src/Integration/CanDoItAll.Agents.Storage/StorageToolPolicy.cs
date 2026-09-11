using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Agents.Storage;

public static class StorageToolPolicy {
    public const string ProviderKey = "storage";
    public const string StorageCatalogList = "storage_catalog_list";
    public const string StorageBrowse = "storage_browse";
    public const string StorageReadTextFile = "storage_read_text_file";
    public const string StorageWriteTextFile = "storage_write_text_file";
    public const string StorageDeleteObject = "storage_delete_object";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(StorageCatalogList, ToolCapabilitySideEffectKind.InternalDataRead),
        Read(StorageBrowse, ToolCapabilitySideEffectKind.InternalDataRead),
        Read(StorageReadTextFile, ToolCapabilitySideEffectKind.InternalDataRead),
        Mutation(StorageWriteTextFile, ToolCapabilitySideEffectKind.InternalStateMutation),
        Mutation(StorageDeleteObject, ToolCapabilitySideEffectKind.InternalStateMutation)
    ]);

    private static readonly string[] ConfiguredDescriptorOrder = [
        StorageCatalogList, StorageReadTextFile, StorageBrowse, StorageWriteTextFile, StorageDeleteObject
    ];

    public static AgentRuntimeConfiguredWorkspacePolicy CreateConfiguredPolicy(
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent,
        bool contentToolsAvailable,
        bool browseToolAvailable) {
        var access = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);
        var descriptors = new List<CapabilityExposureDescriptor>();
        var rules = new List<CapabilityAccessRule>();
        foreach (var policy in Capabilities) {
            var canUse = policy.IsStateChanging ? access.CanWriteStorage : access.CanReadStorage;
            var available = policy.Name == StorageBrowse ? browseToolAvailable : contentToolsAvailable;
            if (canUse && available) {
                descriptors.Add(CreateDescriptor(policy));
            }
            if (!canUse) {
                var permission = policy.IsStateChanging ? "write storage" : "read storage";
                rules.Add(new(CapabilityRuleId.Create($"deny-runtime-tool-{policy.Name.Replace('_', '-')}"),
                    CapabilityAccessEffect.Deny, CapabilityAccessScope.AgentDefault,
                    CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create(policy.Name)),
                    $"Storage tool '{policy.Name}' is disabled because agent settings do not allow {permission}."));
            }
            if (!contextIntent.WorkspaceToolsEnabled) {
                rules.Add(new(CapabilityRuleId.Create($"deny-runtime-tool-{policy.Name.Replace('_', '-')}"),
                    CapabilityAccessEffect.Deny, CapabilityAccessScope.RuntimeOverride,
                    CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create(policy.Name)),
                    "Workspace tools are disabled by execution context."));
            }
        }
        return new(descriptors.OrderBy(descriptor => Array.IndexOf(ConfiguredDescriptorOrder,
            descriptor.RuntimeToolName!.Value.Value)).ToArray(), [new CapabilityAccessPolicy(rules.AsReadOnly())]);
    }

    private static CapabilityExposureDescriptor CreateDescriptor(ToolCapabilityMetadata policy) {
        var classifications = policy.IsStateChanging
            ? new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Write, CapabilityOperationClassification.Mutation }
            : new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Read };
        var tags = new HashSet<CapabilityTag> {
            CapabilityTag.Create("tool"), CapabilityTag.Create("configured"), CapabilityTag.Create("storage")
        };
        foreach (var classification in classifications) {
            tags.Add(CapabilityTag.Create(classification.ToString().ToLowerInvariant()));
        }
        return new(new(CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind.Tool, CapabilityKey.Create(policy.Name.Replace('_', '-'))),
            "Configured storage tool", "Storage tool exposed from agent workspace-tool settings.",
            ImplementationKey.Create($"maf.{policy.Name}"), RuntimeToolName.Create(policy.Name), null, null,
            tags, classifications,
            new(policy.IsStateChanging ? CapabilitySideEffectKind.InternalStateMutation : CapabilitySideEffectKind.InternalDataRead,
                policy.RequiresApprovalByDefault, policy.IsStateChanging), CapabilityAvailabilityState.Available, null);
    }
}
