using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Owns hosted MAF agent creation: per-lease workspace bundle creation, the hosted runtime
/// build through <see cref="MafRuntimeAgentFactory"/>, and the <c>HostedRuntimeAgent</c> lease
/// that ties runtime build disposal to the returned <see cref="AIAgent"/>.
/// </summary>
/// <remarks>
/// ADR-006 deviation note: this surface is deliberately NOT an SDK-free port. A hosted MAF
/// agent lease hands the caller a live Microsoft Agent Framework <see cref="AIAgent"/>, so the
/// framework-native type is the contract; there is no transport-neutral abstraction that could
/// represent it without losing the lease semantics.
/// </remarks>
internal sealed class MafHostedAgentFactory
{
    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IWorkspaceRuntimeServicesFactory workspaceRuntimeServicesFactory;
    private readonly MafRuntimeAgentFactory runtimeAgentFactory;

    public MafHostedAgentFactory(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IWorkspaceRuntimeServicesFactory workspaceRuntimeServicesFactory,
        MafRuntimeAgentFactory runtimeAgentFactory)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
        this.workspaceRuntimeServicesFactory = workspaceRuntimeServicesFactory ?? throw new ArgumentNullException(nameof(workspaceRuntimeServicesFactory));
        this.runtimeAgentFactory = runtimeAgentFactory ?? throw new ArgumentNullException(nameof(runtimeAgentFactory));
    }

    public async Task<AIAgent> CreateHostedAgentAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        bool forceOmitTemperature = false,
        AgentRuntimeExecutionOptions? executionOptions = null)
    {
        var effectiveWorkspaceScope = MafRuntimeAgentFactory.ResolveContextWorkspaceScope(
            executionOptions ?? MafRuntimeExecutionOptionsResolver.CreateDisabled(null),
            workspaceScope);
        var workspaceRuntimeServices = workspaceRuntimeServicesFactory.Create(
            new WorkspaceExecutionScope(workspaceRoot, effectiveWorkspaceScope));
        try
        {
            return await runtimeAgentFactory.CreateHostedAgentAsync(
                agent,
                provider,
                capabilities,
                memory,
                workspaceRuntimeServices,
                cancellationToken,
                suppressApprovalRequirements,
                forceOmitTemperature,
                executionOptions);
        }
        catch
        {
            await workspaceRuntimeServices.DisposeAsync();
            throw;
        }
    }
}
