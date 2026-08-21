using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Web.Api;

internal static class WorkflowApiSafeProjection
{
    private const int MaximumSafeTextLength = 4_096;

    public static WorkflowRunApiResponse Map(WorkflowRunSnapshot run)
        => new(
            run.RunId.Value,
            run.WorkflowId.Value,
            run.VersionId.Value,
            run.State,
            run.Backend,
            Bound(run.Summary),
            run.CreatedAtUtc,
            run.UpdatedAtUtc,
            run.TerminalAtUtc);

    public static WorkflowEventApiResponse Map(WorkflowEventRecord workflowEvent)
        => new(
            workflowEvent.Id,
            workflowEvent.RunId.Value,
            workflowEvent.Kind,
            workflowEvent.NodeId?.Value,
            Bound(workflowEvent.Message),
            workflowEvent.CreatedAtUtc);

    public static WorkflowArtifactApiResponse Map(WorkflowArtifactRecord artifact)
        => new(
            artifact.Id.Value,
            artifact.RunId.Value,
            artifact.Kind,
            artifact.NodeId?.Value,
            Bound(artifact.Name),
            Bound(artifact.ContentType),
            Bound(artifact.Summary),
            artifact.CreatedAtUtc);

    public static WorkflowCheckpointApiResponse Map(WorkflowCheckpointRecord checkpoint)
        => new(
            checkpoint.Id.Value,
            checkpoint.RunId.Value,
            checkpoint.WorkflowId.Value,
            checkpoint.VersionId.Value,
            checkpoint.Backend,
            checkpoint.Kind,
            checkpoint.TrustBoundary,
            checkpoint.ResumeAvailability,
            checkpoint.NodeId?.Value,
            checkpoint.ExternalRequestId?.Value,
            Bound(checkpoint.Summary),
            Bound(checkpoint.ResumeUnavailableReason),
            checkpoint.CreatedAtUtc,
            checkpoint.ResumedAtUtc);

    public static WorkflowPendingExternalRequestApiResponse Map(WorkflowExternalRequestRecord request)
        => new(
            request.Id.Value,
            request.RunId.Value,
            request.Kind,
            request.NodeId.Value,
            Bound(request.EventName),
            request.Version.Value,
            request.EffectiveState,
            request.CreatedAtUtc,
            request.RespondedAtUtc);

    public static WorkflowListPage<WorkflowRunApiResponse> Map(
        WorkflowListPage<WorkflowRunSnapshot> page)
        => new(
            page.Items.Select(Map).ToArray(),
            page.PageIndex,
            page.PageSize,
            page.TotalCount);

    public static WorkflowListPage<WorkflowEventApiResponse> Map(
        WorkflowListPage<WorkflowEventRecord> page)
        => new(
            page.Items.Select(Map).ToArray(),
            page.PageIndex,
            page.PageSize,
            page.TotalCount);

    public static WorkflowExternalResponseApiResponse Map(
        WorkflowExternalResponseServiceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var operation = result.Operation;
        return new WorkflowExternalResponseApiResponse(
            operation?.Id.Value,
            operation?.RequestId.Value ?? result.Request?.Id.Value,
            operation?.ExpectedRequestVersion.Value ?? result.Request?.Version.Value,
            operation?.RunId.Value ?? result.Run?.RunId.Value,
            result.Outcome,
            operation?.State,
            operation?.OutcomeCode,
            result.Run?.State,
            operation?.AcceptedAtUtc,
            operation?.StartedAtUtc,
            operation?.CompletedAtUtc,
            result.Replayed,
            Bound(result.SafeMessage),
            result.NextRequest is null ? null : Map(result.NextRequest));
    }

    private static string Bound(string? value)
    {
        var safe = WorkflowExecutorRedaction.RedactText(value).Trim();
        return safe.Length <= MaximumSafeTextLength
            ? safe
            : safe[..MaximumSafeTextLength];
    }
}
