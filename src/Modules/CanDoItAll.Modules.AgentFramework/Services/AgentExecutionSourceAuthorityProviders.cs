using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

/// <summary>
/// Canonical authority rule for the trusted project-structure source: the
/// workspace scope is derived from the source identity, and read/mutation
/// rights come from the agent's durable project-structure configuration —
/// never from the UI access projection.
/// </summary>
internal sealed class ProjectStructureExecutionAuthorityProvider : IAgentExecutionSourceAuthorityProvider
{
    public string SourceKind => AgentChatTrustedSourceKinds.ProjectStructure;

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParse(request.SourceId.Value, out var projectId) || projectId == Guid.Empty)
        {
            throw new AgentExecutionAuthorityMismatchException(
                "The project-structure source id is not a valid project identifier.");
        }

        return ValueTask.FromResult(ProjectScopedAuthority.Resolve(
            request.Agent,
            projectId,
            request.ObservedWorkspaceScope));
    }
}

/// <summary>
/// Canonical authority rule for the projects portfolio source. A selected
/// project resolves exactly like the project-structure source (the published
/// project scope must match the source identity and rights come from durable
/// configuration); the portfolio view without a selected project carries no
/// workspace claim and receives the read-only sandbox.
/// </summary>
internal sealed class ProjectsExecutionAuthorityProvider : IAgentExecutionSourceAuthorityProvider
{
    public string SourceKind => "projects";

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Guid.TryParse(request.SourceId.Value, out var projectId) && projectId != Guid.Empty)
        {
            return ValueTask.FromResult(ProjectScopedAuthority.Resolve(
                request.Agent,
                projectId,
                request.ObservedWorkspaceScope));
        }

        if (request.ObservedWorkspaceScope is not null)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The projects source '{request.SourceId.Value}' published workspace scope '{request.ObservedWorkspaceScope.DisplayName}' without a selected project.");
        }

        return ValueTask.FromResult(new AgentExecutionSourceAuthorityDecision(
            WorkspaceScopeDescriptor.Sandbox,
            ReadAllowed: true,
            MutationAllowed: false,
            CanonicalAgentExecutionAuthorityResolver.CanonicalPolicyVersion));
    }
}

/// <summary>
/// Canonical authority rule for the processes sources. A project-bound
/// publication resolves through the durable project rule. A process-run
/// scope has no durable per-run authority rule in this module and therefore
/// fails closed instead of adopting the published scope; the turn is denied
/// with an explicit reason rather than silently reduced.
/// </summary>
internal sealed class ProcessesExecutionAuthorityProvider(string sourceKind) : IAgentExecutionSourceAuthorityProvider
{
    private const string ProjectSegmentMarker = ":project:";

    public string SourceKind { get; } = sourceKind;

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceId = request.SourceId.Value;
        var projectMarkerIndex = sourceId.IndexOf(ProjectSegmentMarker, StringComparison.OrdinalIgnoreCase);
        if (projectMarkerIndex >= 0 &&
            Guid.TryParse(sourceId[(projectMarkerIndex + ProjectSegmentMarker.Length)..], out var projectId) &&
            projectId != Guid.Empty)
        {
            if (request.ObservedWorkspaceScope is { Kind: WorkspaceScopeKind.Process })
            {
                throw new AgentExecutionAuthorityMismatchException(
                    "A process-run workspace scope has no canonical per-run authority rule; run process work through the governed process execution path.");
            }

            return ValueTask.FromResult(ProjectScopedAuthority.Resolve(
                request.Agent,
                projectId,
                request.ObservedWorkspaceScope));
        }

        if (request.ObservedWorkspaceScope is not null)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The processes source '{sourceId}' published workspace scope '{request.ObservedWorkspaceScope.DisplayName}', which has no canonical authority rule for a global processes surface.");
        }

        return ValueTask.FromResult(new AgentExecutionSourceAuthorityDecision(
            WorkspaceScopeDescriptor.Sandbox,
            ReadAllowed: true,
            MutationAllowed: false,
            CanonicalAgentExecutionAuthorityResolver.CanonicalPolicyVersion));
    }
}

/// <summary>
/// Shared durable project-authority derivation: the canonical scope comes from
/// the validated project identity, and rights come from the agent's stored
/// project-structure access configuration.
/// </summary>
internal static class ProjectScopedAuthority
{
    public static AgentExecutionSourceAuthorityDecision Resolve(
        AgentDefinition agent,
        Guid projectId,
        WorkspaceScopeDescriptor? observedScope)
    {
        var canonicalScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        if (observedScope is not null && observedScope != canonicalScope)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The published workspace scope '{observedScope.DisplayName}' does not match the canonical project scope '{canonicalScope.DisplayName}'.");
        }

        var summary = ContextualAgentAccessResolver
            .Resolve([agent], ContextualAgentWorkspaceKind.ProjectStructure, projectId)
            .FirstOrDefault();
        if (summary is null || !summary.CanRead)
        {
            throw new AgentChatContextAccessDeniedException(agent.Id, default);
        }

        return new AgentExecutionSourceAuthorityDecision(
            canonicalScope,
            ReadAllowed: true,
            MutationAllowed: summary.CanWrite,
            CanonicalAgentExecutionAuthorityResolver.CanonicalPolicyVersion);
    }
}
