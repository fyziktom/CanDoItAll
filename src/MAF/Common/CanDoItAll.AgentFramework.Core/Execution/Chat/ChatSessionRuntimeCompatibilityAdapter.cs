using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class ChatSessionRuntimeCompatibilityAdapter
{
    public static string RuntimeSessionKeyOrEmpty(ChatSessionRecord session)
        => session.Compatibility?.RuntimeSessionKey ?? string.Empty;

    public static string? SerializedSessionStateJson(ChatSessionRecord session)
        => session.Compatibility?.SerializedSessionStateJson;

    public static IReadOnlyList<PendingToolApprovalRecord> PendingApprovalsOrEmpty(ChatSessionRecord session)
        => session.Compatibility?.PendingApprovals ?? [];

    public static bool AutoApprovePendingToolCalls(ChatSessionRecord session)
        => session.Compatibility?.AutoApprovePendingToolCalls ?? false;

    public static bool HasPendingApprovals(ChatSessionRecord session)
        => PendingApprovalsOrEmpty(session).Count > 0;

    public static ChatSessionRuntimeCompatibilityRecord? CreateCompatibility(
        string? runtimeSessionKey,
        string? serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord>? pendingApprovals,
        bool autoApprovePendingToolCalls = false)
    {
        return ChatSessionRuntimeCompatibilityRecord.Create(
            runtimeSessionKey,
            serializedSessionStateJson,
            pendingApprovals,
            autoApprovePendingToolCalls);
    }

    public static ChatSessionRuntimeCompatibilityRecord? CreateCompatibility(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return CreateCompatibility(
            run.RuntimeSessionKey,
            run.SerializedSessionStateJson,
            run.PendingApprovals,
            run.AutoApprovePendingToolCalls);
    }

    public static ChatSessionRecord ClearCompatibility(
        ChatSessionRecord session,
        DateTimeOffset updatedAtUtc,
        Guid? latestExecutionRunId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session with
        {
            UpdatedAtUtc = updatedAtUtc,
            Compatibility = null,
            LatestExecutionRunId = latestExecutionRunId ?? session.LatestExecutionRunId
        };
    }

    public static ChatSessionRecord ApplyCompatibility(
        ChatSessionRecord session,
        ChatSessionRuntimeCompatibilityRecord? compatibility,
        DateTimeOffset? updatedAtUtc = null,
        Guid? latestExecutionRunId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session with
        {
            UpdatedAtUtc = updatedAtUtc ?? session.UpdatedAtUtc,
            Compatibility = compatibility,
            LatestExecutionRunId = latestExecutionRunId ?? session.LatestExecutionRunId
        };
    }

    public static ChatSessionRecord CreateRuntimeSession(
        ExecutionRunRecord run,
        Guid agentId,
        ChatSessionRecord? transcriptSession)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (transcriptSession is not null)
        {
            return ApplyCompatibility(
                transcriptSession,
                CreateCompatibility(run),
                run.UpdatedAtUtc,
                run.Id);
        }

        return new ChatSessionRecord(
            Id: run.ChatSessionId ?? run.Id,
            AgentId: agentId,
            Title: run.Title,
            CreatedAtUtc: run.CreatedAtUtc,
            UpdatedAtUtc: run.UpdatedAtUtc,
            RuntimeSessionKey: null,
            SerializedSessionStateJson: null,
            Messages: [],
            PendingApprovals: [],
            LatestExecutionRunId: run.Id,
            Compatibility: CreateCompatibility(run));
    }

    public static ExecutionRunRecord CreateLegacyCompatibilityRun(
        ChatSessionRecord session,
        AgentDefinition agent,
        ProviderProfile provider,
        string resultSummary)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(provider);

        var compatibility = session.Compatibility;
        if (compatibility is null || compatibility.PendingApprovals.Count == 0)
        {
            throw new InvalidOperationException("A legacy compatibility run requires pending approval compatibility state.");
        }

        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            ChatSessionId: session.Id,
            Title: string.IsNullOrWhiteSpace(session.Title) ? "Approval continuation" : session.Title,
            SourceKind: "chat-session",
            SourceId: session.Id.ToString("N"),
            CorrelationId: string.Empty,
            CausationId: string.Empty,
            RequestedBy: "legacy-chat-session",
            RequestedByKind: "compatibility",
            MetadataJson: "{}",
            InputSummary: session.Messages.LastOrDefault(item => item.Role == ChatMessageRole.User)?.Content ?? string.Empty,
            ResultSummary: resultSummary,
            ProviderName: provider.Name,
            Model: string.IsNullOrWhiteSpace(agent.Model) ? provider.DefaultModel : agent.Model,
            State: ExecutionState.WaitingOnTool,
            Outcome: null,
            CreatedAtUtc: session.CreatedAtUtc,
            UpdatedAtUtc: session.UpdatedAtUtc,
            StartedAtUtc: session.CreatedAtUtc,
            CompletedAtUtc: null,
            RuntimeSessionKey: compatibility.RuntimeSessionKey,
            SerializedSessionStateJson: compatibility.SerializedSessionStateJson,
            PendingApprovals: compatibility.PendingApprovals,
            AutoApprovePendingToolCalls: compatibility.AutoApprovePendingToolCalls,
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty,
            SchedulerRunId: string.Empty,
            MessageId: string.Empty,
            ProviderProfileId: provider.Id);
    }
}
