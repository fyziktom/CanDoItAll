using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Canonical authority request for one published context source kind. The
/// observed workspace scope is a claim to validate, never a grant; the agent
/// is the durably configured agent definition already resolved for the turn.
/// </summary>
public sealed record AgentExecutionSourceAuthorityRequest(
    AgentDefinition Agent,
    AgentChatContextSourceKind SourceKind,
    AgentChatContextSourceId SourceId,
    WorkspaceScopeDescriptor? ObservedWorkspaceScope,
    Guid CurrentDatabaseProfileId);

/// <summary>
/// Durable authority decision for one source kind: the canonical workspace
/// scope plus read/mutation rights derived from stored configuration. A
/// provider must derive these from durable product authorization data only —
/// never from UI-published access entries, payload text, or model output.
/// </summary>
public sealed record AgentExecutionSourceAuthorityDecision(
    WorkspaceScopeDescriptor WorkspaceScope,
    bool ReadAllowed,
    bool MutationAllowed,
    string PolicyVersion);

public static class AgentExecutionAuthorityPolicyVersions
{
    public const string Canonical = "v2-canonical";
    public const string FailClosedSandbox = "v2-fail-closed-sandbox";
}

/// <summary>
/// Source-keyed canonical authority rule. Exactly one provider may own a
/// source kind; the resolver validates uniqueness at construction. Source
/// kinds without a registered provider fail closed: they receive a bounded
/// read-only sandbox and can never inherit an observed workspace scope.
/// </summary>
public interface IAgentExecutionSourceAuthorityProvider
{
    /// <summary>Stable source-kind key this provider owns (ordinal match).</summary>
    string SourceKind { get; }

    ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default);
}

public static class ProjectScopedExecutionAuthority
{
    public static AgentExecutionSourceAuthorityDecision Resolve(
        AgentDefinition agent,
        Guid projectId,
        WorkspaceScopeDescriptor? observedScope)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
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

        return new AgentExecutionSourceAuthorityDecision(
            canonicalScope,
            ReadAllowed: true,
            MutationAllowed: summary.CanWrite,
            AgentExecutionAuthorityPolicyVersions.Canonical);
    }
}
