using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentChatContextInvocation(
    string Prompt,
    AgentChatRunOptions Options);

public static class AgentChatContextInvocationFactory
{
    public const string Requester = "floating-agent-chat";
    public const string RequesterKind = "interactive";
    public const string MetadataSchema = "candoitall.agent-chat-context.v1";

    public static AgentChatContextInvocation Create(
        AgentChatContextSnapshot? context,
        Guid agentId,
        Guid? chatSessionId,
        string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        var normalizedPrompt = prompt.Trim();
        var transientContext = AgentChatContextContributionComposer.Compose(
            context,
            agentId);
        if (context is null)
        {
            return new AgentChatContextInvocation(
                normalizedPrompt,
                new AgentChatRunOptions());
        }

        var access = context.FindAccess(agentId);
        var contextDigest = transientContext is null
            ? string.Empty
            : AgentChatContextDigest.Compute(transientContext.Content);
        var metadataJson = JsonSerializer.Serialize(
            new InvocationMetadata(
                MetadataSchema,
                context.Scope.Id.ToString(),
                context.Version,
                context.CapturedAtUtc,
                context.Fragments.Select(item => item.ContributorId.Value).ToArray(),
                access?.Permissions ?? AgentChatContextPermission.Read,
                contextDigest),
            AgentOutputJson.SerializerOptions);
        metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            metadataJson,
            context.Scope.WorkspaceScope);
        if (transientContext is not null)
        {
            metadataJson = ExecutionInvocationMetadata.ApplyTransientContextRequirement(
                metadataJson,
                contextDigest);
        }

        var invocationContext = new ExecutionInvocationContext(
            SourceKind: context.Scope.Source.Kind.Value,
            SourceId: context.Scope.Source.Id.Value,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: chatSessionId?.ToString("N") ?? string.Empty,
            RequestedBy: Requester,
            RequestedByKind: RequesterKind,
            MetadataJson: metadataJson);

        return new AgentChatContextInvocation(
            normalizedPrompt,
            new AgentChatRunOptions
            {
                Context = invocationContext,
                TransientContext = transientContext
            });
    }

    public static AgentChatExecutionCompleted? CreateCompletionNotification(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.State != ExecutionState.Completed ||
            run.Outcome != RunOutcome.Succeeded ||
            !run.ChatSessionId.HasValue ||
            run.AgentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(run.MetadataJson) ||
            string.IsNullOrWhiteSpace(run.SourceKind) ||
            string.IsNullOrWhiteSpace(run.SourceId) ||
            run.SourceKind.Trim().Length > AgentChatContextLimits.MaximumSourceKindLength ||
            run.SourceId.Trim().Length > AgentChatContextLimits.MaximumSourceIdLength)
        {
            return null;
        }

        InvocationMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<InvocationMetadata>(
                run.MetadataJson,
                AgentOutputJson.SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        const AgentChatContextPermission mutationPermissions =
            AgentChatContextPermission.Read |
            AgentChatContextPermission.Mutate;
        if (metadata is null ||
            !string.Equals(metadata.Schema, MetadataSchema, StringComparison.Ordinal) ||
            metadata.Permissions != mutationPermissions ||
            !Guid.TryParse(metadata.ScopeId, out var scopeId) ||
            scopeId == Guid.Empty)
        {
            return null;
        }

        return new AgentChatExecutionCompleted(
            new AgentChatContextScopeId(scopeId),
            new AgentChatContextSource(
                new AgentChatContextSourceKind(run.SourceKind),
                new AgentChatContextSourceId(run.SourceId)),
            run.AgentId,
            run.ChatSessionId.Value,
            run.Id,
            run.CompletedAtUtc ?? run.UpdatedAtUtc);
    }

    private sealed record InvocationMetadata(
        string Schema,
        string ScopeId,
        long Version,
        DateTimeOffset CapturedAtUtc,
        IReadOnlyList<string> Contributors,
        AgentChatContextPermission Permissions,
        string ContextDigest);
}
