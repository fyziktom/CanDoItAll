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
