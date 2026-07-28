using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Web.Api.Streaming;

namespace CanDoItAll.Web.Api;

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

public static class AgentApiHeaderNames
{
    public const string ActivityOperationId =
        "X-CanDoItAll-Agent-Operation-Id";
}

public sealed record AgentActivityApiEvent(
    AgentExecutionOperationId OperationId,
    AgentExecutionActivity Activity);

public sealed record AgentActivityReplayGap(
    AgentExecutionOperationId OperationId,
    long RequestedFromInclusive,
    long AvailableFromInclusive);

public sealed record AgentActivityStreamEvicted(
    AgentExecutionOperationId OperationId,
    StreamEvictionReason Reason,
    DateTimeOffset EvictedAtUtc);

public sealed record AgentPendingApprovalApiEvent(
    string ApprovalId,
    string ToolName,
    string ToolKind,
    DateTimeOffset RequestedAtUtc);

public sealed record AgentApprovalRequired(
    AgentExecutionOperationId OperationId,
    Guid ExecutionRunId,
    IReadOnlyList<AgentPendingApprovalApiEvent> Approvals);

public sealed record AgentCommandCompleted<TResult>(
    AgentExecutionOperationId OperationId,
    TResult Result);

public sealed record AgentCommandFailed(
    AgentExecutionOperationId OperationId,
    string Code,
    string Message);
