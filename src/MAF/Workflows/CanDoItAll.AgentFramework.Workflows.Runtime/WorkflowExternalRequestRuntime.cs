using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExternalRequestPendingException : InvalidOperationException
{
    public WorkflowExternalRequestPendingException(
        WorkflowExternalRequestRecord request,
        string message)
        : base(message)
    {
        Request = request;
    }

    public WorkflowExternalRequestRecord Request { get; }

    public static bool TryFind(
        Exception exception,
        out WorkflowExternalRequestPendingException? pendingException)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is WorkflowExternalRequestPendingException pending)
        {
            pendingException = pending;
            return true;
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (TryFind(innerException, out pendingException))
                {
                    return true;
                }
            }
        }

        if (exception.InnerException is not null)
        {
            return TryFind(exception.InnerException, out pendingException);
        }

        pendingException = null;
        return false;
    }
}

public interface IWorkflowExternalRequestCapture
{
    void Record(WorkflowExternalRequestRecord request);
}

public sealed class WorkflowExternalRequestCaptureScope : IDisposable
{
    private static readonly AsyncLocal<IWorkflowExternalRequestCapture?> CurrentCapture = new();
    private readonly IWorkflowExternalRequestCapture? previousCapture;

    private WorkflowExternalRequestCaptureScope(IWorkflowExternalRequestCapture capture)
    {
        previousCapture = CurrentCapture.Value;
        CurrentCapture.Value = capture;
    }

    public static WorkflowExternalRequestCaptureScope Push(IWorkflowExternalRequestCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return new WorkflowExternalRequestCaptureScope(capture);
    }

    public static void Record(WorkflowExternalRequestRecord request)
    {
        CurrentCapture.Value?.Record(request);
    }

    public void Dispose()
    {
        CurrentCapture.Value = previousCapture;
    }
}

public sealed class WorkflowExternalRequestApprovalGate : IWorkflowExecutorApprovalGate
{
    public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
        WorkflowExecutorApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var runId = WorkflowExecutorExecutionAuditScope.CurrentRunId
            ?? throw new InvalidOperationException(
                $"Workflow executor '{request.Descriptor.Id}' on node '{request.Node.Id}' requires approval, but no workflow run id is active.");
        var now = DateTimeOffset.UtcNow;
        var requestRecord = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            runId,
            WorkflowExternalRequestKind.Approval,
            request.Node.Id,
            $"approval:{request.Descriptor.Id.Value}",
            JsonSerializer.Serialize(
                new WorkflowExecutorApprovalRequestPayload(
                    request.Definition.Id.ToString(),
                    request.Definition.VersionId.ToString(),
                    request.Node.Id.Value,
                    request.Descriptor.Id.Value,
                    request.Descriptor.PermissionPolicy.RequiredCapabilities.ToString(),
                    request.Descriptor.PermissionPolicy.ApprovalRequirement.ToString(),
                    request.RedactedSettingsSummary),
                WorkflowExternalRequestJson.Options),
            ResponseJson: string.Empty,
            CreatedAtUtc: now,
            RespondedAtUtc: null);
        WorkflowExternalRequestCaptureScope.Record(requestRecord);

        throw new WorkflowExternalRequestPendingException(
            requestRecord,
            $"Workflow executor '{request.Descriptor.Id}' on node '{request.Node.Id}' requires approval.");
    }

    private sealed record WorkflowExecutorApprovalRequestPayload(
        string WorkflowId,
        string WorkflowVersionId,
        string NodeId,
        string ExecutorId,
        string RequiredCapabilities,
        string ApprovalRequirement,
        string RedactedSettingsSummary);
}

internal static class WorkflowExternalRequestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
