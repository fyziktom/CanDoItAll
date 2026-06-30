using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;

public sealed class DelayWorkflowExecutor : IWorkflowExecutor
{
    private const int AbsoluteMaxDelayMilliseconds = 30000;

    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.Delay;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowDelayExecutorSettings>(context.SettingsJson);
        var configuredMax = Math.Clamp(settings.MaxDelayMilliseconds, 1, AbsoluteMaxDelayMilliseconds);
        if (settings.DelayMilliseconds < 0 || settings.DelayMilliseconds > configuredMax)
        {
            throw new InvalidOperationException($"Delay executor supports only in-process delays between 0 and {configuredMax} millisecond(s). Use a durable workflow backend before enabling longer waits.");
        }

        await Task.Delay(settings.DelayMilliseconds, cancellationToken);
        return WorkflowExecutorJson.Result(context, new
        {
            delayedMilliseconds = settings.DelayMilliseconds,
            durableScheduling = false,
            inputPayload = input.PayloadJson
        });
    }
}

public sealed class HumanApprovalWorkflowExecutor : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.ApprovalRequest;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowApprovalExecutorSettings>(context.SettingsJson);
        var runId = context.RunId
            ?? WorkflowExecutorExecutionAuditScope.CurrentRunId
            ?? throw new InvalidOperationException($"Approval workflow node '{context.Node.Id}' requires an active workflow run id.");
        var now = DateTimeOffset.UtcNow;
        var requestRecord = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            runId,
            WorkflowExternalRequestKind.Approval,
            context.Node.Id,
            EventName: $"approval:{context.Node.Id.Value}",
            RequestJson: JsonSerializer.Serialize(
                new ApprovalRequestPayload(
                    string.IsNullOrWhiteSpace(settings.Prompt)
                        ? $"Approve workflow node '{context.Node.Id}'."
                        : settings.Prompt.Trim(),
                    settings.IncludeInputPayload ? input.PayloadJson : string.Empty),
                WorkflowExecutorJson.Options),
            ResponseJson: string.Empty,
            CreatedAtUtc: now,
            RespondedAtUtc: null);

        WorkflowExternalRequestCaptureScope.Record(requestRecord);
        throw new WorkflowExternalRequestPendingException(
            requestRecord,
            $"Workflow is waiting for approval at node '{context.Node.Id}'.");
    }

    private sealed record ApprovalRequestPayload(
        string Prompt,
        string InputPayloadJson);
}
