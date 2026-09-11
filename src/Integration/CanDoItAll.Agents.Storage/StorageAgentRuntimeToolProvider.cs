using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Agents.Storage;

public sealed class StorageAgentRuntimeToolProvider(
    IStorageCatalogService? catalogService = null,
    IStorageDriverRegistry? driverRegistry = null,
    IStorageBrowseDriverRegistry? browseDriverRegistry = null) : IAgentRuntimeToolProvider {
    public int Order => 0;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        StorageToolPolicy.ProviderKey, "Configured Storage tools",
        "Storage tools attached through agent workspace settings and the canonical Storage services.",
        ["configured", "storage"]) {
        AttachmentPhase = AgentRuntimeToolAttachmentPhase.ConfiguredWorkspace
    };

    public AgentRuntimeConfiguredWorkspacePolicy GetConfiguredWorkspacePolicy(
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent) {
        var kinds = driverRegistry?.RegisteredKinds ?? [];
        var contentAvailable = catalogService is not null && kinds.Count > 0;
        var browseAvailable = contentAvailable && browseDriverRegistry is not null &&
            kinds.Intersect(browseDriverRegistry.RegisteredKinds).Any();
        return StorageToolPolicy.CreateConfiguredPolicy(workspaceToolAccess, contextIntent, contentAvailable, browseAvailable);
    }

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.Agent.Permissions.CanUseTools || !context.ContextIntent.WorkspaceToolsEnabled) {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }
        var access = RequireWorkspaceAccess(context);
        var policy = GetConfiguredWorkspacePolicy(access, context.ContextIntent);
        if (policy.Capabilities.Count == 0) {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }
        var runtime = new StorageRuntimePlugin(catalogService!, driverRegistry!, browseDriverRegistry, access);
        var names = policy.Capabilities.Select(capability => capability.RuntimeToolName!.Value.Value)
            .ToHashSet(StringComparer.Ordinal);
        return ValueTask.FromResult<IReadOnlyList<AITool>>(StorageToolPolicy.Capabilities
            .Where(capability => names.Contains(capability.Name)).Select(capability => CreateTool(runtime, capability.Name)).ToArray());
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(AgentRuntimeToolProviderContext context) {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Agent.Permissions.CanUseTools || !context.ContextIntent.WorkspaceToolsEnabled) {
            return [];
        }
        var names = GetConfiguredWorkspacePolicy(RequireWorkspaceAccess(context), context.ContextIntent)
            .Capabilities.Select(capability => capability.RuntimeToolName!.Value.Value).ToHashSet(StringComparer.Ordinal);
        return StorageToolPolicy.Capabilities.Where(policy => names.Contains(policy.Name))
            .Select(policy => new AgentRuntimeToolMetadata(StorageToolPolicy.ProviderKey, policy.Name,
                policy.IsStateChanging ? AgentRuntimeToolOperationKind.Mutation : AgentRuntimeToolOperationKind.Read,
                policy.RequiresApprovalByDefault, ["configured", "storage"]))
            .ToArray();
    }

    private static AgentWorkspaceToolAccessSettings RequireWorkspaceAccess(AgentRuntimeToolProviderContext context)
        => context.WorkspaceToolAccess ?? throw new InvalidOperationException("Configured Storage tools require the resolved workspace access snapshot.");

    private static AITool CreateTool(StorageRuntimePlugin runtime, string name) => name switch {
        StorageToolPolicy.StorageCatalogList => AIFunctionFactory.Create(runtime.ListStorageCatalogs, StorageToolPolicy.StorageCatalogList, "Lists storage catalogs this agent is allowed to use."),
        StorageToolPolicy.StorageBrowse => AIFunctionFactory.Create(runtime.BrowseStorage, StorageToolPolicy.StorageBrowse, "Lists one bounded page of direct child folders and objects in an allowed storage catalog. Use entryId as the read locator and a container entry id as containerKey to descend. When nextCursor is returned, pass it in the next call while repeating the same storageId, containerKey, pageSize, and includeMetadata values."),
        StorageToolPolicy.StorageReadTextFile => AIFunctionFactory.Create(runtime.ReadStorageTextFile, StorageToolPolicy.StorageReadTextFile, "Reads a text object from an allowed storage catalog through the configured storage driver."),
        StorageToolPolicy.StorageWriteTextFile => AIFunctionFactory.Create(runtime.WriteStorageTextFile, StorageToolPolicy.StorageWriteTextFile, "Writes a text object to an allowed storage catalog through the configured storage driver."),
        StorageToolPolicy.StorageDeleteObject => AIFunctionFactory.Create(runtime.DeleteStorageObject, StorageToolPolicy.StorageDeleteObject, "Deletes an object from an allowed storage catalog through the configured storage driver."),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown configured Storage tool.")
    };
}
