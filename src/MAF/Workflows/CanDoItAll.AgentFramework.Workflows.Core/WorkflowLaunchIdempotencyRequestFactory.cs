using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowLaunchIdempotencyRequestFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkflowLaunchIdempotencyScope CreateScope(
        WorkflowLaunchIntent intent,
        WorkflowLaunchIdempotencyKey callerKey)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(intent.Selection);
        ArgumentNullException.ThrowIfNull(intent.Origin);

        var (workflowId, requestedVersionId) = intent.Selection switch
        {
            WorkflowDefinitionSelection.ExactSavedVersion exact => (exact.WorkflowId, exact.VersionId),
            WorkflowDefinitionSelection.LatestActive latest => (latest.WorkflowId, (WorkflowVersionId?)null),
            WorkflowDefinitionSelection.DraftPreview draft => (draft.Definition.Id, draft.Definition.VersionId),
            _ => throw new InvalidOperationException(
                $"Workflow definition selection '{intent.Selection.GetType().Name}' is not supported.")
        };

        return new WorkflowLaunchIdempotencyScope(
            callerKey,
            workflowId,
            intent.Selection.Kind,
            requestedVersionId,
            intent.Mode,
            intent.Origin.Kind,
            new WorkflowLaunchOriginScopeKey(Hash(CreateOriginScopePayload(intent.Origin))));
    }

    public static WorkflowLaunchRequestFingerprint CreateFingerprint(
        WorkflowLaunchIntent intent,
        string normalizedInputJson)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedInputJson);

        var payload = new RequestFingerprintPayload(
            intent.Selection,
            normalizedInputJson,
            intent.CompletionPolicy,
            intent.RequestedBackend,
            intent.PreviewSimulationPlan);
        return new WorkflowLaunchRequestFingerprint(Hash(payload));
    }

    private static object CreateOriginScopePayload(WorkflowLaunchOrigin origin)
        => origin switch
        {
            WorkflowLaunchOrigin.Api api => new ActorOriginScopePayload(
                api.Actor.Kind,
                api.Actor.SubjectId,
                SessionId: null),
            WorkflowLaunchOrigin.Preview preview => new ActorOriginScopePayload(
                preview.Actor.Kind,
                preview.Actor.SubjectId,
                SessionId: null),
            WorkflowLaunchOrigin.SchedulerPlanRun scheduler => new SchedulerOriginScopePayload(
                scheduler.PlanId),
            WorkflowLaunchOrigin.ProjectStructureNode project => new ProjectOriginScopePayload(
                project.ProjectId,
                project.NodeId.Value,
                project.RequestingActor.SubjectId,
                project.SessionId.Value),
            WorkflowLaunchOrigin.AgentRuntimeInvocation agent => new ActorOriginScopePayload(
                agent.Agent.Kind,
                agent.Agent.SubjectId,
                agent.RuntimeSessionId.Value),
            WorkflowLaunchOrigin.ProcessAssignment process => new ProcessOriginScopePayload(
                process.ProcessRunId,
                process.AssignmentId),
            _ => throw new InvalidOperationException(
                $"Workflow launch origin '{origin.GetType().Name}' is not supported.")
        };

    private static string Hash<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private sealed record RequestFingerprintPayload(
        WorkflowDefinitionSelection Selection,
        string InputJson,
        WorkflowLaunchCompletionPolicy CompletionPolicy,
        WorkflowRuntimeBackendKind? RequestedBackend,
        WorkflowPreviewSimulationPlan PreviewSimulationPlan);

    private sealed record ActorOriginScopePayload(
        WorkflowLaunchActorKind ActorKind,
        string SubjectId,
        string? SessionId);

    private sealed record SchedulerOriginScopePayload(Guid PlanId);

    private sealed record ProjectOriginScopePayload(
        Guid ProjectId,
        string NodeId,
        string AgentSubjectId,
        string SessionId);

    private sealed record ProcessOriginScopePayload(
        Guid ProcessRunId,
        Guid AssignmentId);
}
