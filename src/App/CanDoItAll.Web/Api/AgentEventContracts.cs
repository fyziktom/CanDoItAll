using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Web.Api.Streaming;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Server-sent event names written by the agent and provider streaming operations under <c>/api/agents</c>.
/// </summary>
public static class AgentServerEventNames
{
    public const string Activity = "agent.activity";
    public const string ActivityCompleted = "agent.activity.completed";
    public const string ActivityFailed = "agent.activity.failed";
    public const string ActivityCancelled = "agent.activity.cancelled";
    public const string ApprovalWaiting = "agent.approval.waiting";
    public const string ApprovalRequired = "agent.approval.required";
    public const string CommandCompleted = "agent.command.completed";
    public const string CommandFailed = "agent.command.failed";
    public const string ReplayGap = ServerSentEventResponseWriter.GapEventName;
    public const string StreamEvicted = "stream.evicted";
    public const string ProviderAccepted = "provider.chat.accepted";
    public const string ProviderRunning = "provider.chat.running";
    public const string ProviderCompleted = "provider.chat.completed";
    public const string ProviderFailed = "provider.chat.failed";
}

/// <summary>
/// HTTP header names of the agent operations.
/// </summary>
public static class AgentApiHeaderNames
{
    /// <summary>
    /// Response header that carries the activity operation identifier of an agent command, usable with
    /// <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>.
    /// </summary>
    public const string ActivityOperationId =
        "X-CanDoItAll-Agent-Operation-Id";
}

/// <summary>
/// Data of an activity server-sent event (<c>agent.activity</c>, <c>agent.approval.waiting</c>,
/// <c>agent.activity.completed</c>, <c>agent.activity.failed</c> or <c>agent.activity.cancelled</c>). The event
/// <c>id</c> is the activity's sequence number within its operation.
/// </summary>
/// <param name="OperationId">Activity operation identifier of the command.</param>
/// <param name="Phase">Phase the operation reached, written as a camel-case string.</param>
/// <param name="OccurredAtUtc">UTC instant of the activity.</param>
/// <param name="AgentId">Agent of the operation, or null until known.</param>
/// <param name="Message">Human-readable progress text, at most 2048 characters.</param>
/// <param name="ChatSessionId">Chat session of the operation, or null.</param>
/// <param name="ExecutionRunId">Execution run of the operation, or null until the run exists.</param>
/// <param name="TerminalOutcome">
/// Outcome of the last activity of the operation (camel-case string); null for every earlier activity.
/// </param>
/// <param name="ErrorCode">Failure code of a failed last activity; otherwise null.</param>
public sealed record AgentActivityApiEvent(
    AgentExecutionOperationId OperationId,
    AgentExecutionActivityPhase Phase,
    DateTimeOffset OccurredAtUtc,
    Guid? AgentId,
    string Message,
    Guid? ChatSessionId,
    Guid? ExecutionRunId,
    AgentExecutionActivityTerminalOutcome? TerminalOutcome,
    string? ErrorCode)
{
    internal static AgentActivityApiEvent From(
        AgentExecutionOperationId operationId,
        AgentExecutionActivity activity)
    {
        return new AgentActivityApiEvent(
            operationId,
            activity.Phase,
            activity.OccurredAtUtc,
            activity.AgentId,
            activity.Message,
            activity.ChatSessionId,
            activity.ExecutionRunId,
            activity.TerminalOutcome,
            activity.ErrorCode);
    }
}

/// <summary>
/// Data of the <c>stream.gap</c> server-sent event of an agent activity stream: activity between the requested
/// cursor and the oldest retained event was discarded.
/// </summary>
/// <param name="OperationId">Activity operation identifier of the command.</param>
/// <param name="RequestedFromInclusive">First sequence number the reader asked for.</param>
/// <param name="AvailableFromInclusive">Sequence number of the oldest retained activity, which follows next.</param>
public sealed record AgentActivityReplayGap(
    AgentExecutionOperationId OperationId,
    long RequestedFromInclusive,
    long AvailableFromInclusive);

/// <summary>
/// Data of the <c>stream.evicted</c> server-sent event: the finished operation's activity was discarded and the
/// stream ends.
/// </summary>
/// <param name="OperationId">Activity operation identifier of the command.</param>
/// <param name="Reason">Why the activity was discarded, written as a camel-case string.</param>
/// <param name="EvictedAtUtc">UTC instant of the discard.</param>
public sealed record AgentActivityStreamEvicted(
    AgentExecutionOperationId OperationId,
    StreamEvictionReason Reason,
    DateTimeOffset EvictedAtUtc);

/// <summary>
/// One pending tool approval in the <c>agent.approval.required</c> server-sent event.
/// </summary>
/// <param name="ApprovalId">Identifier of the approval to answer with a decision.</param>
/// <param name="ToolName">Name of the tool call that waits for approval.</param>
/// <param name="ToolKind">Kind of that tool.</param>
/// <param name="RequestedAtUtc">Instant at which the approval was requested.</param>
public sealed record AgentPendingApprovalApiEvent(
    string ApprovalId,
    string ToolName,
    string ToolKind,
    DateTimeOffset RequestedAtUtc);

/// <summary>
/// Data of the <c>agent.approval.required</c> server-sent event: the run waits for the listed tool approvals.
/// </summary>
/// <param name="OperationId">Activity operation identifier of the command.</param>
/// <param name="ExecutionRunId">Execution run that waits for approval.</param>
/// <param name="Approvals">The approvals that are still pending.</param>
public sealed record AgentApprovalRequired(
    AgentExecutionOperationId OperationId,
    Guid ExecutionRunId,
    IReadOnlyList<AgentPendingApprovalApiEvent> Approvals);

/// <summary>
/// Data of the <c>agent.command.completed</c> server-sent event: the command finished and <c>result</c> holds what
/// the matching JSON operation would have returned.
/// </summary>
/// <typeparam name="TResult">Result type of the matching JSON operation.</typeparam>
/// <param name="OperationId">Activity operation identifier of the command.</param>
/// <param name="Result">The command result.</param>
public sealed record AgentCommandCompleted<TResult>(
    AgentExecutionOperationId OperationId,
    TResult Result);

/// <summary>
/// Data of the <c>agent.command.failed</c> server-sent event: the command failed after its stream had started. It
/// mirrors the error envelope that a JSON operation would have returned.
/// </summary>
/// <param name="OperationId">Activity operation identifier of the command.</param>
/// <param name="Code">
/// Stable failure code, for example <c>agents.run-failed</c> or <c>agents.command-failed</c>.
/// </param>
/// <param name="Message">Human-readable failure text; its wording can change.</param>
/// <param name="CorrelationId">Server trace identifier of the request, for support and log correlation.</param>
/// <param name="AgentId">Agent of the command, or null.</param>
/// <param name="ExecutionRunId">Execution run that failed, or null when none is known.</param>
/// <param name="ChatSessionId">Chat session of the command, or null.</param>
/// <param name="ProviderFailureCategory">
/// Category of a model-provider failure (camel-case string), or null for other failures.
/// </param>
public sealed record AgentCommandFailed(
    AgentExecutionOperationId OperationId,
    string Code,
    string Message,
    string? CorrelationId = null,
    Guid? AgentId = null,
    Guid? ExecutionRunId = null,
    Guid? ChatSessionId = null,
    AgentProviderFailureCategory? ProviderFailureCategory = null);
