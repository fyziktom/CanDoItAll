using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal static class WorkflowRuntimeTransitionRules
{
    public static void ValidateExternalResponse(
        WorkflowExternalRequestRecord request,
        string responseJson)
    {
        if (request.Kind is not (WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval))
        {
            return;
        }

        WorkflowExternalApprovalResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<WorkflowExternalApprovalResponse>(
                responseJson,
                WorkflowExternalRequestJson.Options);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Workflow approval response JSON must be an object with an approved boolean property.",
                nameof(responseJson),
                exception);
        }

        if (response?.Approved is null)
        {
            throw new ArgumentException(
                "Workflow approval response JSON must be an object with an approved boolean property.",
                nameof(responseJson));
        }
    }

    public static bool TryFindUsageObservationException(
        Exception exception,
        out WorkflowUsageObservationException? usageException)
    {
        if (exception is WorkflowUsageObservationException direct)
        {
            usageException = direct;
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var innerException in aggregate.Flatten().InnerExceptions)
            {
                if (TryFindUsageObservationException(innerException, out usageException))
                {
                    return true;
                }
            }
        }

        if (exception.InnerException is not null &&
            TryFindUsageObservationException(exception.InnerException, out usageException))
        {
            return true;
        }

        usageException = null;
        return false;
    }

    public static string CreateSafeFailureSummary(Exception exception)
        => NormalizeSummary($"Workflow backend failed: {exception.Message}");

    public static string NormalizeSummary(string summary)
    {
        var redacted = WorkflowExecutorRedaction.RedactText(summary);
        return redacted.Length <= 1_000 ? redacted : redacted[..1_000];
    }

    public static bool IsTerminal(WorkflowRunState state)
        => state is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;

    public static WorkflowEventRecord? FindBackendTransitionEvent(
        WorkflowRunState state,
        IReadOnlyList<WorkflowEventRecord> events)
    {
        var eventKind = state switch
        {
            WorkflowRunState.Completed => WorkflowEventKind.Completed,
            WorkflowRunState.Failed => WorkflowEventKind.Error,
            WorkflowRunState.Cancelled => WorkflowEventKind.Cancelled,
            WorkflowRunState.WaitingForInput => WorkflowEventKind.WaitingForInput,
            _ => (WorkflowEventKind?)null
        };
        return eventKind.HasValue
            ? events.LastOrDefault(workflowEvent => workflowEvent.Kind == eventKind.Value)
            : null;
    }

    private sealed record WorkflowExternalApprovalResponse(bool? Approved, string? Message);
}
