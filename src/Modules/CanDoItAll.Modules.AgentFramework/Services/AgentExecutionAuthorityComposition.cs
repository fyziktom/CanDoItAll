using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.AgentFramework;

/// <summary>
/// Canonical <see cref="IAgentExecutionAuthorityResolver"/> backed by durable
/// agent configuration and the current database profile. UI-published access
/// entries never grant authority here: for the trusted project-structure
/// source the resolver independently re-derives read/mutation rights from the
/// agent's stored configuration, and every workspace-scope claim is validated
/// against the canonical scope shape before admission. Source kinds without a
/// canonical authority rule yet keep their current observed behavior and are
/// marked with the compatibility policy version so later hardening can find
/// and tighten them.
/// </summary>
internal sealed class CanonicalAgentExecutionAuthorityResolver(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource profileGenerationSource,
    TimeProvider timeProvider) : IAgentExecutionAuthorityResolver
{
    public const string CanonicalPolicyVersion = "v2-canonical";
    public const string ObservedCompatibilityPolicyVersion = "v1-observed-compat";

    public async ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(
        AgentExecutionAuthorityResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentGeneration = profileGenerationSource.GetGeneration();
        if (currentGeneration != request.ExpectedDatabaseProfileGeneration)
        {
            throw new InvalidOperationException(
                "The current database profile changed while execution authority was being resolved.");
        }

        var currentProfile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var agent = await ResolveActiveAgentAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        var (workspaceScope, readAllowed, mutationAllowed, policyVersion) = ResolveSourceAuthority(
            request,
            agent,
            currentProfile.Id);

        return new AgentExecutionAuthorityRecord(
            AgentExecutionAuthorityId.Create(),
            agent.Id,
            currentProfile.Id,
            request.ExpectedDatabaseProfileGeneration,
            workspaceScope,
            readAllowed,
            mutationAllowed,
            policyVersion,
            ComputePolicyFingerprint(
                agent.Id,
                request.SourceKind,
                request.SourceId,
                workspaceScope,
                request.ExpectedDatabaseProfileGeneration,
                readAllowed,
                mutationAllowed,
                policyVersion),
            timeProvider.GetUtcNow());
    }

    private async Task<AgentDefinition> ResolveActiveAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        var agents = await workspaceFactory
            .GetOrganizationWorkspaceService()
            .ListAgentsAsync(includeTemplates: false, cancellationToken)
            .ConfigureAwait(false);
        var agent = agents.FirstOrDefault(item => item.Id == agentId);
        if (agent is null || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Agent '{agentId:N}' is not an active executable agent in the current profile.");
        }

        return agent;
    }

    private (WorkspaceScopeDescriptor Scope, bool ReadAllowed, bool MutationAllowed, string PolicyVersion) ResolveSourceAuthority(
        AgentExecutionAuthorityResolutionRequest request,
        AgentDefinition agent,
        Guid currentProfileId)
    {
        var observedScope = request.ObservedWorkspaceScope;

        if (string.Equals(
                request.SourceKind.Value,
                AgentChatTrustedSourceKinds.ProjectStructure,
                StringComparison.Ordinal))
        {
            // Trusted project-structure source: the canonical scope is derived
            // from the source identity, and rights come from durable agent
            // configuration — never from the UI access projection.
            if (!Guid.TryParse(request.SourceId.Value, out var projectId) || projectId == Guid.Empty)
            {
                throw new AgentExecutionAuthorityMismatchException(
                    "The project-structure source id is not a valid project identifier.");
            }

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

            return (canonicalScope, true, summary.CanWrite, CanonicalPolicyVersion);
        }

        if (observedScope is null)
        {
            return (WorkspaceScopeDescriptor.Sandbox, true, false, ObservedCompatibilityPolicyVersion);
        }

        switch (observedScope.Kind)
        {
            case WorkspaceScopeKind.Organization:
                var canonicalOrganizationScope = WorkspaceScopeDescriptor.Organization(
                    currentProfileId.ToString("N"));
                if (observedScope != canonicalOrganizationScope)
                {
                    throw new AgentExecutionAuthorityMismatchException(
                        $"The published organization scope '{observedScope.DisplayName}' does not belong to the current database profile.");
                }

                break;
            case WorkspaceScopeKind.Project:
                if (!Guid.TryParse(observedScope.Key, out var scopedProjectId) ||
                    scopedProjectId == Guid.Empty)
                {
                    throw new AgentExecutionAuthorityMismatchException(
                        $"The published project scope key '{observedScope.Key}' is not a valid project identifier.");
                }

                break;
            case WorkspaceScopeKind.Sandbox:
                break;
            default:
                throw new AgentExecutionAuthorityMismatchException(
                    $"The published workspace scope kind '{observedScope.Kind}' has no canonical authority rule.");
        }

        // Compatibility path for sources without a canonical authority rule
        // yet: keep the currently observed permission projection, explicitly
        // versioned so it can be tightened per-source later. The UI hint can
        // only narrow (read-only) — a hint can never grant more than the
        // recorded compatibility baseline.
        var hintAllowsMutation = request.UiAccessHint is { } hint &&
            hint.Permissions.HasFlag(AgentChatContextPermission.Mutate);
        return (observedScope, true, hintAllowsMutation, ObservedCompatibilityPolicyVersion);
    }

    private static string ComputePolicyFingerprint(
        Guid agentId,
        AgentChatContextSourceKind sourceKind,
        AgentChatContextSourceId sourceId,
        WorkspaceScopeDescriptor scope,
        DatabaseProfileGeneration generation,
        bool readAllowed,
        bool mutationAllowed,
        string policyVersion)
    {
        var payload = string.Join(
            '',
            agentId.ToString("N"),
            sourceKind.Value,
            sourceId.Value,
            scope.Kind.ToString(),
            scope.Key,
            generation.Value.ToString(),
            readAllowed ? "read" : "no-read",
            mutationAllowed ? "mutate" : "no-mutate",
            policyVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}
