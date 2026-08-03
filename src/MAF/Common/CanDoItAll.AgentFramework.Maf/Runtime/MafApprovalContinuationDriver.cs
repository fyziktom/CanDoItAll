using System.Collections.Concurrent;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafApprovalContinuationDriver
{
    IEnumerable<ChatMessage> CreateApprovalInputMessages(ChatSessionRecord session, bool approved);

    void StorePendingApprovals(Guid sessionId, IReadOnlyList<ToolApprovalRequestContent> approvalRequests);

    void ClearPendingApprovals(Guid sessionId);

    PendingToolApprovalRecord MapPendingApproval(ToolApprovalRequestContent request);

    string ResolveResponseText(AgentResponse response, IReadOnlyList<PendingToolApprovalRecord> pendingApprovals);
}

internal sealed class MafApprovalContinuationDriver : IMafApprovalContinuationDriver
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ToolApprovalRequestContent>> pendingApprovalCache = new();

    public IEnumerable<ChatMessage> CreateApprovalInputMessages(ChatSessionRecord session, bool approved)
    {
        var approvals = GetCachedOrRehydratedApprovals(session);
        return approvals
            .Select(item => new ChatMessage(ChatRole.User, [item.CreateResponse(approved)]))
            .ToList();
    }

    public void StorePendingApprovals(Guid sessionId, IReadOnlyList<ToolApprovalRequestContent> approvalRequests)
    {
        ArgumentNullException.ThrowIfNull(approvalRequests);
        pendingApprovalCache[sessionId] = approvalRequests;
    }

    public void ClearPendingApprovals(Guid sessionId)
    {
        pendingApprovalCache.TryRemove(sessionId, out _);
    }

    public PendingToolApprovalRecord MapPendingApproval(ToolApprovalRequestContent request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var toolCall = request.ToolCall;
        var toolKind = toolCall switch
        {
            McpServerToolCallContent => "mcp",
            FunctionCallContent => "function",
            _ => "tool"
        };

        var details = toolCall switch
        {
            McpServerToolCallContent mcp => mcp.ServerName,
            _ => string.Empty
        };

        var argumentsJson = toolCall switch
        {
            McpServerToolCallContent mcp when mcp.Arguments is not null => JsonSerializer.Serialize(mcp.Arguments, SerializerOptions),
            FunctionCallContent function when function.Arguments is not null => JsonSerializer.Serialize(function.Arguments, SerializerOptions),
            _ => "{}"
        };
        var approvalId = RequireStableIdentifier(
            request.RequestId,
            "approval request");
        var callId = RequireStableIdentifier(
            toolCall.CallId,
            "approved tool call");

        return new PendingToolApprovalRecord(
            ApprovalId: approvalId,
            CallId: callId,
            ToolName: MafToolInvocationArgumentFormatter.ResolveToolName(toolCall),
            ToolKind: toolKind,
            Details: details ?? string.Empty,
            ArgumentsJson: argumentsJson);
    }

    public string ResolveResponseText(
        AgentResponse response,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(pendingApprovals);

        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            return response.Text.Trim();
        }

        if (pendingApprovals.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot resolve response text because the provider returned no usable text and no tool approval is pending.");
        }

        var summary = string.Join(
            Environment.NewLine,
            pendingApprovals.Select(item =>
            {
                var argumentSummary = MafToolInvocationArgumentFormatter.DescribeArguments(item.ArgumentsJson, item.ToolName);
                return item.ToolKind == "mcp"
                    ? $"- Approval required for MCP tool '{item.ToolName}' on server '{item.Details}'{MafToolInvocationArgumentFormatter.FormatInlineArgumentSummary(argumentSummary)}."
                    : $"- Approval required for tool '{item.ToolName}'{MafToolInvocationArgumentFormatter.FormatInlineArgumentSummary(argumentSummary)}.";
            }));

        return $"Approval is required before the run can continue.{Environment.NewLine}{summary}";
    }

    private IReadOnlyList<ToolApprovalRequestContent> GetCachedOrRehydratedApprovals(ChatSessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (pendingApprovalCache.TryGetValue(session.Id, out var cached))
        {
            return cached;
        }

        var compatibility = session.Compatibility;
        if (compatibility is null || compatibility.PendingApprovals.Count == 0)
        {
            throw new InvalidOperationException("This session does not have any cached approval requests to continue.");
        }

        var rehydrated = compatibility.PendingApprovals
            .Select(RehydratePendingApproval)
            .ToList();

        pendingApprovalCache[session.Id] = rehydrated;
        return rehydrated;
    }

    private static ToolApprovalRequestContent RehydratePendingApproval(PendingToolApprovalRecord record)
    {
        var arguments = MafToolInvocationArgumentFormatter.DeserializeArguments(record.ArgumentsJson);
        ToolCallContent toolCall = record.ToolKind switch
        {
            "mcp" or "hosted-mcp" => new McpServerToolCallContent(record.CallId, record.ToolName, record.Details)
            {
                Arguments = arguments
            },
            _ => new FunctionCallContent(record.CallId, record.ToolName, arguments)
        };

        return new ToolApprovalRequestContent(record.ApprovalId, toolCall);
    }

    private static string RequireStableIdentifier(string? identifier, string identifierKind)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException(
                $"Microsoft Agent Framework returned an {identifierKind} without a stable correlation identifier.");
        }

        return identifier;
    }
}
