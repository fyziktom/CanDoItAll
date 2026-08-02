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
    public const string WorkspacePositionContributorId = "workspace.position";
    public const string SurfacePositionContributorId = "surface.position";

    public static AgentChatContextInvocation Create(
        AgentChatContextSnapshot? context,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentExecutionOperationId initialActivityOperationId,
        DatabaseProfileGeneration currentDatabaseProfileGeneration,
        DateTimeOffset nowUtc)
    {
        return Create(
            context,
            agentId,
            chatSessionId,
            prompt,
            initialActivityOperationId,
            currentDatabaseProfileGeneration,
            nowUtc,
            AgentChatExecutionBehavior.Default);
    }

    public static AgentChatContextInvocation Create(
        AgentChatContextSnapshot? context,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentExecutionOperationId initialActivityOperationId,
        DatabaseProfileGeneration currentDatabaseProfileGeneration,
        DateTimeOffset nowUtc,
        AgentChatExecutionBehavior behavior)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(behavior);
        if (initialActivityOperationId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An initial activity operation id is required.",
                nameof(initialActivityOperationId));
        }

        var normalizedPrompt = prompt.Trim();
        var transientContext = AgentChatContextContributionComposer.Compose(
            context,
            agentId);
        AgentChatContextAttachmentFreshnessPolicy.EnsureCurrent(
            context,
            currentDatabaseProfileGeneration,
            nowUtc);
        if (context is null)
        {
            return new AgentChatContextInvocation(
                normalizedPrompt,
                CreateRunOptions(initialActivityOperationId, behavior));
        }

        var access = context.FindAccess(agentId);
        var contextDigest = transientContext is null
            ? string.Empty
            : AgentChatContextDigest.Compute(transientContext);
        var metadataJson = JsonSerializer.Serialize(
            new InvocationMetadata(
                MetadataSchema,
                context.Scope.Id.ToString(),
                context.Version,
                context.CapturedAtUtc,
                ResolveContributorIds(context),
                access?.Permissions ?? AgentChatContextPermission.Read,
                context.Scope.CompletionRefreshMode,
                contextDigest),
            AgentOutputJson.SerializerOptions);
        metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            metadataJson,
            context.Scope.WorkspaceScope);
        metadataJson = ExecutionInvocationMetadata.ApplyReadOnlyExternalTargetAliases(
            metadataJson,
            ResolveTrustedReadOnlyExternalTargetAliases(
                context,
                currentDatabaseProfileGeneration,
                nowUtc));
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
            CreateRunOptions(initialActivityOperationId, behavior) with
            {
                Context = invocationContext,
                TransientContext = transientContext
            });
    }

    private static AgentChatRunOptions CreateRunOptions(
        AgentExecutionOperationId initialActivityOperationId,
        AgentChatExecutionBehavior behavior)
    {
        return new AgentChatRunOptions(
            initialActivityOperationId,
            behavior.RuntimeToolProvidersEnabled,
            behavior.WorkspaceToolsEnabled)
        {
            ToolCapabilitiesEnabled = behavior.ToolCapabilitiesEnabled
        };
    }

    public static AgentChatExecutionCompleted? CreateCompletionNotification(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.State != ExecutionState.Completed ||
            run.Outcome != RunOutcome.Succeeded ||
            !run.ChatSessionId.HasValue ||
            run.AgentId == Guid.Empty ||
            !string.Equals(run.RequestedBy, Requester, StringComparison.Ordinal) ||
            !string.Equals(run.RequestedByKind, RequesterKind, StringComparison.Ordinal) ||
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
        var requestsCompletionRefresh = metadata?.CompletionRefreshMode ==
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun;
        if (metadata is null ||
            !string.Equals(metadata.Schema, MetadataSchema, StringComparison.Ordinal) ||
            (metadata.Permissions != AgentChatContextPermission.Read &&
             metadata.Permissions != mutationPermissions) ||
            !requestsCompletionRefresh ||
            !Enum.IsDefined(metadata.CompletionRefreshMode) ||
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
        AgentChatContextCompletionRefreshMode CompletionRefreshMode,
        string ContextDigest);

    private static IReadOnlyList<string> ResolveContributorIds(
        AgentChatContextSnapshot context)
    {
        var contributors = new List<string>(context.Fragments.Count + 2);
        if (context.WorkspacePosition is not null)
        {
            contributors.Add(WorkspacePositionContributorId);
        }

        if (context.Scope.SurfacePosition is not null)
        {
            contributors.Add(SurfacePositionContributorId);
        }

        contributors.AddRange(context.Fragments.Select(item => item.ContributorId.Value));
        return contributors;
    }

    private static IReadOnlyList<string> ResolveTrustedReadOnlyExternalTargetAliases(
        AgentChatContextSnapshot context,
        DatabaseProfileGeneration currentDatabaseProfileGeneration,
        DateTimeOffset nowUtc)
    {
        var source = context.Scope.Source;
        var workspaceScope = context.Scope.WorkspaceScope;
        if (!string.Equals(
                source.Kind.Value,
                AgentChatExternalTargetAccessAttachmentFactory.TrustedSourceKindValue,
                StringComparison.Ordinal) ||
            workspaceScope is null ||
            workspaceScope.Kind != WorkspaceScopeKind.Project ||
            !string.Equals(workspaceScope.Key, source.Id.Value, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return context.Attachments
            .Where(attachment =>
                attachment.ScopeId == context.Scope.Id &&
                attachment.Source == source &&
                attachment.WorkspaceScope == workspaceScope &&
                string.Equals(
                    attachment.ContributorId.Value,
                    AgentChatExternalTargetAccessAttachmentFactory.TrustedContributorIdValue,
                    StringComparison.Ordinal))
            .SelectMany(ResolveValidatedAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IReadOnlyList<string> ResolveValidatedAliases(
            AgentChatContextAttachmentEnvelope attachment)
            => AgentChatExternalTargetAccessAttachmentFactory.TryGetValidatedReadOnlyAliases(
                attachment,
                currentDatabaseProfileGeneration,
                nowUtc,
                out var aliases)
                ? aliases
                : [];
    }
}
