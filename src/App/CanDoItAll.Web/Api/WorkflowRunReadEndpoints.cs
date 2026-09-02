using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class WorkflowRunReadEndpoints
{
    public static RouteGroupBuilder MapWorkflowRunReadApi(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/runs", async (
                [AsParameters] WorkflowRunListApiQuery query,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterRuns(
                    await runStore.ListRunsAsync(
                        query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                        cancellationToken),
                    query)
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray()))
            .WithName("ListWorkflowRuns");

        workflows.MapGet("/runs/page", async (
                [AsParameters] WorkflowRunListApiQuery query,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(WorkflowApiSafeProjection.Map(
                await runStore.ListRunPageAsync(
                    new WorkflowRunPageRequest(
                        query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                        query.State,
                        query.Backend,
                        query.Search ?? string.Empty,
                        query.PageIndex.GetValueOrDefault(),
                        query.PageSize ?? query.Take ?? 50),
                    cancellationToken))))
            .WithName("ListWorkflowRunPage");

        workflows.MapGet("/runs/{runId:guid}", async (
                Guid runId,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
        {
            var run = await runtimeManager.GetRunAsync(new WorkflowRunId(runId), cancellationToken);
            return run is null
                ? ApiEndpointResults.NotFound("Workflow run was not found.", "workflows.run-not-found")
                : Results.Ok(WorkflowApiSafeProjection.Map(run));
        })
        .WithName("GetWorkflowRun");

        workflows.MapGet("/runs/{runId:guid}/detail", async (
                Guid runId,
                IWorkflowRuntimeManager runtimeManager,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            await GetRunDetailResultAsync(
                new WorkflowRunId(runId),
                runtimeManager,
                runStore,
                cancellationToken))
            .WithName("GetWorkflowRunDetail")
            .Produces<WorkflowRunDetailApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        workflows.MapGet("/runs/{runId:guid}/events", async (
                Guid runId,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            Results.Ok((await runtimeManager.ListEventsAsync(
                    new WorkflowRunId(runId),
                    cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray()))
            .WithName("ListWorkflowRunEvents");

        workflows.MapGet("/runs/{runId:guid}/events/page", async (
                Guid runId,
                [AsParameters] WorkflowEventListApiQuery query,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            Results.Ok(WorkflowApiSafeProjection.Map(
                await runtimeManager.ListEventPageAsync(
                    new WorkflowEventPageRequest(
                        new WorkflowRunId(runId),
                        query.PageIndex.GetValueOrDefault(),
                        query.PageSize.GetValueOrDefault(50)),
                    cancellationToken))))
            .WithName("ListWorkflowRunEventPage");

        workflows.MapGet("/runs/{runId:guid}/artifacts", async (
                Guid runId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok((await runStore.ListArtifactsAsync(
                    new WorkflowRunId(runId),
                    cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray()))
            .WithName("ListWorkflowRunArtifacts");

        workflows.MapGet("/runs/{runId:guid}/artifacts/{artifactId:guid}/content", async (
                Guid runId,
                Guid artifactId,
                IWorkflowRunStore runStore,
                IWorkflowArtifactContentStore artifactContentStore,
                CancellationToken cancellationToken) =>
            await GetArtifactContentResultAsync(
                new WorkflowRunId(runId),
                new WorkflowArtifactId(artifactId),
                runStore,
                artifactContentStore,
                cancellationToken))
            .WithName("GetWorkflowRunArtifactContent");

        workflows.MapGet("/runs/{runId:guid}/checkpoints", async (
                Guid runId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok((await runStore.ListCheckpointsAsync(
                    new WorkflowRunId(runId),
                    cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray()))
            .WithName("ListWorkflowRunCheckpoints");

        workflows.MapGet("/runs/{runId:guid}/pending-requests", async (
                Guid runId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok((await runStore.ListPendingExternalRequestsAsync(
                    new WorkflowRunId(runId),
                    cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray()))
            .WithName("ListWorkflowRunPendingRequests");

        return workflows;
    }

    internal static async Task<WorkflowRunDetailApiResponse> BuildRunDetailAsync(
        WorkflowRunSnapshot run,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => new(
            WorkflowApiSafeProjection.Map(run),
            (await runtimeManager.ListEventsAsync(run.RunId, cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray(),
            (await runStore.ListArtifactsAsync(run.RunId, cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray(),
            (await runStore.ListPendingExternalRequestsAsync(run.RunId, cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray(),
            (await runStore.ListCheckpointsAsync(run.RunId, cancellationToken))
                .Select(WorkflowApiSafeProjection.Map)
                .ToArray());

    private static async Task<IResult> GetRunDetailResultAsync(
        WorkflowRunId runId,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
    {
        var run = await runtimeManager.GetRunAsync(runId, cancellationToken);
        return run is null
            ? ApiEndpointResults.NotFound("Workflow run was not found.", "workflows.run-not-found")
            : Results.Ok(await BuildRunDetailAsync(run, runtimeManager, runStore, cancellationToken));
    }

    private static async Task<IResult> GetArtifactContentResultAsync(
        WorkflowRunId runId,
        WorkflowArtifactId artifactId,
        IWorkflowRunStore runStore,
        IWorkflowArtifactContentStore artifactContentStore,
        CancellationToken cancellationToken)
    {
        var artifacts = await runStore.ListArtifactsAsync(runId, cancellationToken);
        var artifact = artifacts.SingleOrDefault(item => item.Id == artifactId);
        if (artifact is null)
        {
            return ApiEndpointResults.NotFound(
                "Workflow artifact was not found for this run.",
                "workflows.artifact-not-found");
        }

        var content = await artifactContentStore.ReadContentAsync(artifact, cancellationToken);
        return content is null
            ? ApiEndpointResults.NotFound(
                "Workflow artifact content was not found for this artifact.",
                "workflows.artifact-content-not-found")
            : Results.Text(content.Content, artifact.ContentType);
    }

    private static IReadOnlyList<WorkflowRunSnapshot> FilterRuns(
        IReadOnlyList<WorkflowRunSnapshot> runs,
        WorkflowRunListApiQuery query)
    {
        var filtered = runs.AsEnumerable();
        if (query.State.HasValue)
        {
            filtered = filtered.Where(item => item.State == query.State.Value);
        }

        if (query.Backend.HasValue)
        {
            filtered = filtered.Where(item => item.Backend == query.Backend.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(item =>
                item.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.BackendRunId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.RunId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(Math.Clamp(query.Take.GetValueOrDefault(50), 1, 500))
            .ToArray();
    }
}

internal sealed class WorkflowRunListApiQuery
{
    public Guid? WorkflowId { get; set; }

    public WorkflowRunState? State { get; set; }

    public WorkflowRuntimeBackendKind? Backend { get; set; }

    public string? Search { get; set; }

    public int? Take { get; set; }

    public int? PageIndex { get; set; }

    public int? PageSize { get; set; }
}

internal sealed class WorkflowEventListApiQuery
{
    public int? PageIndex { get; set; }

    public int? PageSize { get; set; }
}
