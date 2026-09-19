using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class WorkflowRunReadEndpoints
{
    public static RouteGroupBuilder MapWorkflowRunReadApi(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/runs", ListRunsAsync)
            .WithName("ListWorkflowRuns")
            .Produces<WorkflowRunApiResponse[]>();

        workflows.MapGet("/runs/page", ListRunPageAsync)
            .WithName("ListWorkflowRunPage")
            .Produces<WorkflowListPage<WorkflowRunApiResponse>>();

        workflows.MapGet("/runs/{runId:guid}", GetRunAsync)
            .WithName("GetWorkflowRun")
            .Produces<WorkflowRunApiResponse>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapGet("/runs/{runId:guid}/detail", GetRunDetailAsync)
            .WithName("GetWorkflowRunDetail")
            .Produces<WorkflowRunDetailApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        workflows.MapGet("/runs/{runId:guid}/events", ListRunEventsAsync)
            .WithName("ListWorkflowRunEvents")
            .Produces<WorkflowEventApiResponse[]>();

        workflows.MapGet("/runs/{runId:guid}/events/page", ListRunEventPageAsync)
            .WithName("ListWorkflowRunEventPage")
            .Produces<WorkflowListPage<WorkflowEventApiResponse>>();

        workflows.MapGet("/runs/{runId:guid}/artifacts", ListRunArtifactsAsync)
            .WithName("ListWorkflowRunArtifacts")
            .Produces<WorkflowArtifactApiResponse[]>();

        workflows.MapGet("/runs/{runId:guid}/artifacts/{artifactId:guid}/content", GetRunArtifactContentAsync)
            .WithName("GetWorkflowRunArtifactContent")
            .Produces<string>(
                StatusCodes.Status200OK,
                "application/json",
                "text/plain",
                "text/markdown",
                "application/octet-stream")
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapGet("/runs/{runId:guid}/checkpoints", ListRunCheckpointsAsync)
            .WithName("ListWorkflowRunCheckpoints")
            .Produces<WorkflowCheckpointApiResponse[]>();

        workflows.MapGet("/runs/{runId:guid}/pending-requests", ListRunPendingRequestsAsync)
            .WithName("ListWorkflowRunPendingRequests")
            .Produces<WorkflowPendingExternalRequestApiResponse[]>();

        return workflows;
    }

    /// <summary>
    /// List recent workflow runs, most recently updated first.
    /// </summary>
    /// <remarks>
    /// Returns up to <c>Take</c> runs (default 50, at most 500) that match every supplied filter, as safe run
    /// projections. <c>Search</c> matches the run summary, backend run identifier or run identifier
    /// case-insensitively. This list has no total count and no paging; use <c>GET /api/workflows/runs/page</c> to page
    /// through all runs. <c>PageIndex</c> and <c>PageSize</c> are ignored here.
    ///
    /// A run is one execution of one workflow version; it is not the definition and not an agent execution run. Read
    /// one run with <c>GET /api/workflows/runs/{runId}</c> or with its events, artifacts, pending requests and
    /// checkpoints with <c>GET /api/workflows/runs/{runId}/detail</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="query">Optional filters and the maximum number of runs.</param>
    /// <response code="200">The matching runs; an empty array when none match.</response>
    internal static async Task<IResult> ListRunsAsync(
        [AsParameters] WorkflowRunListApiQuery query,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => Results.Ok(FilterRuns(
                await runStore.ListRunsAsync(
                    query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                    cancellationToken),
                query)
            .Select(WorkflowApiSafeProjection.Map)
            .ToArray());

    /// <summary>
    /// Read one page of workflow runs, most recently updated first.
    /// </summary>
    /// <remarks>
    /// Returns the runs that match every supplied filter, ordered by last update (newest first, ties by run
    /// identifier), as safe run projections with the total number of matches. Paging is zero-based: request the next
    /// page while <c>hasNextPage</c> is true. The page size is <c>PageSize</c>, else <c>Take</c>, else 50, limited to 1
    /// through 100; the response states the page index and size actually used. Pages are computed per request, so
    /// runs updated between two requests can move between pages.
    ///
    /// <c>Search</c> here is a case-sensitive substring of the run summary or backend run identifier; it does not match
    /// the run identifier, unlike <c>GET /api/workflows/runs</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="query">Optional filters and the page to read.</param>
    /// <response code="200">
    /// The requested page. An empty <c>items</c> array means no run matched or the page is beyond the last page.
    /// </response>
    internal static async Task<IResult> ListRunPageAsync(
        [AsParameters] WorkflowRunListApiQuery query,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => Results.Ok(WorkflowApiSafeProjection.Map(
            await runStore.ListRunPageAsync(
                new WorkflowRunPageRequest(
                    query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                    query.State,
                    query.Backend,
                    query.Search ?? string.Empty,
                    query.PageIndex.GetValueOrDefault(),
                    query.PageSize ?? query.Take ?? 50),
                cancellationToken)));

    /// <summary>
    /// Read the current state of one workflow run.
    /// </summary>
    /// <remarks>
    /// Returns the safe projection of the run: its identity, the workflow and version it runs, its state, backend,
    /// bounded summary and timestamps. Poll this operation, or subscribe to
    /// <c>GET /api/workflows/runs/{runId}/events/stream</c>, to follow a run; read
    /// <c>GET /api/workflows/runs/{runId}/detail</c> for its events, artifacts, pending requests and checkpoints.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the run, as returned in <c>run.runId</c> by a start operation or in <c>runId</c> by the run lists.
    /// </param>
    /// <response code="200">The run.</response>
    /// <response code="404">No run has this identifier (<c>workflows.run-not-found</c>).</response>
    internal static async Task<IResult> GetRunAsync(
        Guid runId,
        IWorkflowRuntimeManager runtimeManager,
        CancellationToken cancellationToken)
    {
        var run = await runtimeManager.GetRunAsync(new WorkflowRunId(runId), cancellationToken);
        return run is null
            ? ApiEndpointResults.NotFound("Workflow run was not found.", "workflows.run-not-found")
            : Results.Ok(WorkflowApiSafeProjection.Map(run));
    }

    /// <summary>
    /// Read a workflow run together with all its events, artifacts, pending external requests and checkpoints.
    /// </summary>
    /// <remarks>
    /// Returns the same detail a start operation returns: the run, every event in time order, the artifact metadata,
    /// the external requests that still have no response, and the checkpoint metadata. All parts are safe projections:
    /// event payloads, artifact storage paths and content, raw external request and response JSON, checkpoint payload
    /// references and hashes and the run's launch origin are never included, and texts are bounded and redacted. The
    /// parts are read one after another, so a run that is still executing can show a slightly newer part next to an
    /// older one. Read artifact content with
    /// <c>GET /api/workflows/runs/{runId}/artifacts/{artifactId}/content</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the run, as returned in <c>run.runId</c> by a start operation or in <c>runId</c> by the run lists.
    /// </param>
    /// <response code="200">The run and its related records.</response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Reserved for a bearer token that does not authorize the operation (<c>api.authorization-forbidden</c>). The
    /// <c>/api</c> group currently accepts any valid token, so this route does not return it today.
    /// </response>
    /// <response code="404">No run has this identifier (<c>workflows.run-not-found</c>).</response>
    internal static async Task<IResult> GetRunDetailAsync(
        Guid runId,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => await GetRunDetailResultAsync(
            new WorkflowRunId(runId),
            runtimeManager,
            runStore,
            cancellationToken);

    /// <summary>
    /// List all recorded events of a workflow run in time order.
    /// </summary>
    /// <remarks>
    /// Returns every stored event of the run, oldest first, as safe projections: the kind, node, bounded and redacted
    /// message and time of each event, without its payload. Internal provider-read evidence records are not listed.
    /// For long runs use <c>GET /api/workflows/runs/{runId}/events/page</c>; to be notified of new events use
    /// <c>GET /api/workflows/runs/{runId}/events/stream</c>.
    ///
    /// An unknown run identifier returns an empty array, not HTTP 404.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">Identifier of the run whose events are listed.</param>
    /// <response code="200">
    /// The events of the run; an empty array for a run without events or an unknown run.
    /// </response>
    internal static async Task<IResult> ListRunEventsAsync(
        Guid runId,
        IWorkflowRuntimeManager runtimeManager,
        CancellationToken cancellationToken)
        => Results.Ok((await runtimeManager.ListEventsAsync(
                new WorkflowRunId(runId),
                cancellationToken))
            .Select(WorkflowApiSafeProjection.Map)
            .ToArray());

    /// <summary>
    /// Read one page of the recorded events of a workflow run in time order.
    /// </summary>
    /// <remarks>
    /// Returns the run's events, oldest first (ties by event identifier), as safe projections with the total number
    /// of events. Paging is zero-based: request the next page while <c>hasNextPage</c> is true. Internal
    /// provider-read evidence records are not counted or listed. An unknown run identifier returns an empty page, not
    /// HTTP 404.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">Identifier of the run whose events are listed.</param>
    /// <param name="query">The page to read.</param>
    /// <response code="200">The requested page of events.</response>
    internal static async Task<IResult> ListRunEventPageAsync(
        Guid runId,
        [AsParameters] WorkflowEventListApiQuery query,
        IWorkflowRuntimeManager runtimeManager,
        CancellationToken cancellationToken)
        => Results.Ok(WorkflowApiSafeProjection.Map(
            await runtimeManager.ListEventPageAsync(
                new WorkflowEventPageRequest(
                    new WorkflowRunId(runId),
                    query.PageIndex.GetValueOrDefault(),
                    query.PageSize.GetValueOrDefault(50)),
                cancellationToken)));

    /// <summary>
    /// List the artifacts recorded by a workflow run.
    /// </summary>
    /// <remarks>
    /// Returns the metadata of every artifact of the run, oldest first: its kind, node, name, content type and
    /// bounded summary. Artifacts hold node outputs, tool receipts, preview simulations, payloads too large to keep
    /// inline, and files written by file operations. The storage location and the content are not included; read the
    /// content with <c>GET /api/workflows/runs/{runId}/artifacts/{artifactId}/content</c>.
    ///
    /// An unknown run identifier returns an empty array, not HTTP 404.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">Identifier of the run whose artifacts are listed.</param>
    /// <response code="200">The artifacts of the run; an empty array when there are none.</response>
    internal static async Task<IResult> ListRunArtifactsAsync(
        Guid runId,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => Results.Ok((await runStore.ListArtifactsAsync(
                new WorkflowRunId(runId),
                cancellationToken))
            .Select(WorkflowApiSafeProjection.Map)
            .ToArray());

    /// <summary>
    /// Download the text content of one artifact of a workflow run.
    /// </summary>
    /// <remarks>
    /// Returns the stored content as text, with the artifact's recorded <c>contentType</c> as the response
    /// <c>Content-Type</c>. Payload artifacts (node outputs, receipts, preview simulations and payloads too large to
    /// keep inline) use <c>application/json</c>, <c>text/plain</c> or <c>application/octet-stream</c>, and their
    /// content is the complete payload after credential-like values were redacted. File artifacts return the current
    /// text of the workspace file the run wrote, read at request time, and only when their content type is textual
    /// (for example <c>text/plain</c> or <c>text/markdown</c>); other File artifacts, such as spreadsheets and
    /// downloads, have no content here. The content is not bounded like the metadata projections and can be large.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">Identifier of the run that recorded the artifact.</param>
    /// <param name="artifactId">
    /// Identifier of the artifact, as returned in <c>id</c> by <c>GET /api/workflows/runs/{runId}/artifacts</c>.
    /// </param>
    /// <response code="200">The artifact content as text, with the artifact's content type.</response>
    /// <response code="404">
    /// The run has no artifact with this identifier (<c>workflows.artifact-not-found</c>), or the artifact has no
    /// readable content (<c>workflows.artifact-content-not-found</c>), for example because its file was removed or is
    /// not text.
    /// </response>
    internal static async Task<IResult> GetRunArtifactContentAsync(
        Guid runId,
        Guid artifactId,
        IWorkflowRunStore runStore,
        IWorkflowArtifactContentStore artifactContentStore,
        CancellationToken cancellationToken)
        => await GetArtifactContentResultAsync(
            new WorkflowRunId(runId),
            new WorkflowArtifactId(artifactId),
            runStore,
            artifactContentStore,
            cancellationToken);

    /// <summary>
    /// List the checkpoints recorded for a workflow run.
    /// </summary>
    /// <remarks>
    /// Returns the metadata of every checkpoint of the run in the order they were created: where and why the runtime
    /// recorded it, whether it holds trusted runtime state, and whether the run can be resumed from it. Checkpoint
    /// payload references, hashes and backend identifiers are never returned. Checkpoints are informational for
    /// clients: resuming a run that waits for input happens through
    /// <c>POST /api/workflows/external-requests/{requestId}/response</c>, not through a checkpoint operation.
    ///
    /// An unknown run identifier returns an empty array, not HTTP 404.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">Identifier of the run whose checkpoints are listed.</param>
    /// <response code="200">The checkpoints of the run; an empty array when there are none.</response>
    internal static async Task<IResult> ListRunCheckpointsAsync(
        Guid runId,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => Results.Ok((await runStore.ListCheckpointsAsync(
                new WorkflowRunId(runId),
                cancellationToken))
            .Select(WorkflowApiSafeProjection.Map)
            .ToArray());

    /// <summary>
    /// List the external requests of a workflow run that have no recorded response yet.
    /// </summary>
    /// <remarks>
    /// Read this before answering a human-input or approval request. Each item carries the request identity, kind,
    /// node, current <c>version</c>, <c>state</c>, a bounded and redacted <c>prompt</c> and the
    /// <c>responseContract</c> that a response must satisfy. Only an item whose <c>state</c> is Pending accepts a
    /// response: submit it with <c>POST /api/workflows/external-requests/{requestId}/response</c>, sending
    /// <c>version</c> as <c>expectedRequestVersion</c>.
    ///
    /// The projection deliberately never returns the request's prior-node context, the raw request JSON, its
    /// authorization policy, executor arguments or checkpoint material. Do not derive a user interface from artifact
    /// file names; use <c>prompt</c> and <c>responseContract</c>. Items are ordered by creation time. An unknown run
    /// identifier returns an empty array, not HTTP 404.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open. Answering a request requires the separate
    /// <c>api.workflows.respond</c> scope.
    /// </remarks>
    /// <param name="runId">Identifier of the run whose open external requests are listed.</param>
    /// <response code="200">The external requests without a response; an empty array when there are none.</response>
    internal static async Task<IResult> ListRunPendingRequestsAsync(
        Guid runId,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => Results.Ok((await runStore.ListPendingExternalRequestsAsync(
                new WorkflowRunId(runId),
                cancellationToken))
            .Select(WorkflowApiSafeProjection.Map)
            .ToArray());

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

/// <summary>
/// Query-string filters shared by <c>GET /api/workflows/runs</c> (a bounded list) and
/// <c>GET /api/workflows/runs/page</c> (a counted page). Every filter is optional and supplied filters are combined.
/// Some members apply to only one of the two operations, as stated on each.
/// </summary>
internal sealed class WorkflowRunListApiQuery
{
    /// <summary>
    /// Only runs of this workflow, from any of its versions. Omitted means runs of all workflows.
    /// </summary>
    public Guid? WorkflowId { get; set; }

    /// <summary>
    /// Only runs in this state, as an integer: 0 NotStarted, 1 Running, 2 WaitingForInput, 3 Idle, 4 Completed,
    /// 5 Failed, 6 Cancelled.
    /// </summary>
    public WorkflowRunState? State { get; set; }

    /// <summary>
    /// Only runs executed on this runtime backend, as an integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
    /// </summary>
    public WorkflowRuntimeBackendKind? Backend { get; set; }

    /// <summary>
    /// Text filter; surrounding whitespace is ignored and omitted or blank applies no filter. For
    /// <c>GET /api/workflows/runs</c> it is a case-insensitive substring of the run summary, backend run identifier or
    /// run identifier. For <c>GET /api/workflows/runs/page</c> it is a case-sensitive substring of the run summary or
    /// backend run identifier only.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// For <c>GET /api/workflows/runs</c>: the maximum number of runs returned, default 50; values outside 1 through
    /// 500 are clamped to that range. For <c>GET /api/workflows/runs/page</c>: the page size used when
    /// <c>PageSize</c> is omitted.
    /// </summary>
    public int? Take { get; set; }

    /// <summary>
    /// Only for <c>GET /api/workflows/runs/page</c>: zero-based page number, default 0; a negative value is
    /// treated as 0. Ignored by <c>GET /api/workflows/runs</c>.
    /// </summary>
    public int? PageIndex { get; set; }

    /// <summary>
    /// Only for <c>GET /api/workflows/runs/page</c>: number of runs per page; values outside 1 through 100 are
    /// clamped to that range. Omitted means <c>Take</c>, or 50 when that is omitted too. Ignored by
    /// <c>GET /api/workflows/runs</c>.
    /// </summary>
    public int? PageSize { get; set; }
}

/// <summary>
/// Query-string paging of <c>GET /api/workflows/runs/{runId}/events/page</c>.
/// </summary>
internal sealed class WorkflowEventListApiQuery
{
    /// <summary>
    /// Zero-based page number, default 0; a negative value is treated as 0.
    /// </summary>
    public int? PageIndex { get; set; }

    /// <summary>
    /// Number of events per page, default 50; values outside 1 through 100 are clamped to that range.
    /// </summary>
    public int? PageSize { get; set; }
}
