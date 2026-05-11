using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CanDoItAll.Web.Api;

internal static class WorkflowsApi
{
    public static RouteGroupBuilder MapWorkflowsApi(this RouteGroupBuilder group)
    {
        var workflows = group.MapGroup("/workflows")
            .WithTags("Workflows")
            .DisableAntiforgery();

        workflows.MapGet("/settings", async (
                IWorkflowSettingsService settingsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await settingsService.GetSettingsAsync(cancellationToken)))
            .WithName("GetWorkflowSettings");

        workflows.MapPost("/settings", async (
                WorkflowSettings request,
                IWorkflowSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => settingsService.SaveSettingsAsync(request, cancellationToken)))
            .WithName("SaveWorkflowSettings");

        workflows.MapGet("/runtime-backends", (
                IWorkflowRuntimeBackendCatalog backendCatalog) =>
            Results.Ok(backendCatalog.ListBackends()))
            .WithName("ListWorkflowRuntimeBackends");

        workflows.MapGet("/executor-catalog", (
                IWorkflowExecutorCatalog executorCatalog) =>
            Results.Ok(executorCatalog.ListExecutors()))
            .WithName("ListWorkflowExecutorCatalog");

        workflows.MapGet("/definitions", async (
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalogService.ListDefinitionsAsync(cancellationToken)))
            .WithName("ListWorkflowDefinitions");

        workflows.MapGet("/definitions/{workflowId:guid}", async (
                Guid workflowId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await GetDefinitionResultAsync(workflowId, versionId: null, catalogService, cancellationToken))
            .WithName("GetWorkflowDefinition");

        workflows.MapGet("/definitions/{workflowId:guid}/versions/{versionId:guid}", async (
                Guid workflowId,
                Guid versionId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await GetDefinitionResultAsync(workflowId, versionId, catalogService, cancellationToken))
            .WithName("GetWorkflowDefinitionVersion");

        workflows.MapPost("/definitions", async (
                WorkflowDefinitionSaveRequest request,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => catalogService.SaveDefinitionAsync(request, cancellationToken)))
            .WithName("SaveWorkflowDefinition");

        workflows.MapDelete("/definitions/{workflowId:guid}", async (
                Guid workflowId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
        {
            await catalogService.DeleteDefinitionAsync(new WorkflowId(workflowId), cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("DeleteWorkflowDefinition");

        workflows.MapPost("/definitions/{workflowId:guid}/validate", async (
                Guid workflowId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
        {
            var detail = await catalogService.GetDefinitionAsync(new WorkflowId(workflowId), versionId: null, cancellationToken);
            return detail is null
                ? ApiEndpointResults.NotFound("Workflow definition was not found.", "workflows.definition-not-found")
                : Results.Ok(detail.Validation);
        })
        .WithName("ValidateSavedWorkflowDefinition");

        workflows.MapPost("/definitions/{workflowId:guid}/runs/start", async (
                Guid workflowId,
                WorkflowRunStartApiRequest request,
                IWorkflowCatalogService catalogService,
                IWorkflowRuntimeManager runtimeManager,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            await StartWorkflowRunAsync(
                workflowId,
                request,
                catalogService,
                runtimeManager,
                runStore,
                cancellationToken))
            .WithName("StartWorkflowDefinitionRun");

        workflows.MapPost("/validate", async (
                WorkflowDefinition request,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalogService.ValidateDefinitionAsync(request, cancellationToken)))
            .WithName("ValidateDraftWorkflowDefinition");

        workflows.MapGet("/provider-options", async (
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
            Results.Ok(await componentLibrary.ListProviderOptionsAsync(cancellationToken)))
            .WithName("ListWorkflowProviderOptions");

        workflows.MapGet("/components", async (
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
            Results.Ok(await componentLibrary.ListComponentsAsync(cancellationToken)))
            .WithName("ListWorkflowComponents");

        workflows.MapGet("/components/{componentId:guid}", async (
                Guid componentId,
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
        {
            var component = await componentLibrary.GetComponentAsync(new WorkflowComponentId(componentId), cancellationToken);
            return component is null
                ? ApiEndpointResults.NotFound("Workflow component was not found.", "workflows.component-not-found")
                : Results.Ok(component);
        })
        .WithName("GetWorkflowComponent");

        workflows.MapPost("/components", async (
                LlmCallComponentSaveRequest request,
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => componentLibrary.SaveComponentAsync(request, cancellationToken)))
            .WithName("SaveWorkflowComponent");

        workflows.MapDelete("/components/{componentId:guid}", async (
                Guid componentId,
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
        {
            await componentLibrary.DeleteComponentAsync(new WorkflowComponentId(componentId), cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("DeleteWorkflowComponent");

        workflows.MapPost("/test-runs", async (
                WorkflowTestRunRequest request,
                IWorkflowTestRunner testRunner,
                CancellationToken cancellationToken) =>
        {
            var result = await testRunner.RunAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
        .WithName("RunWorkflowTest");

        workflows.MapPost("/runs/start", async (
                WorkflowRunStartApiRequest request,
                IWorkflowCatalogService catalogService,
                IWorkflowRuntimeManager runtimeManager,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            await StartWorkflowRunAsync(
                routeWorkflowId: null,
                request,
                catalogService,
                runtimeManager,
                runStore,
                cancellationToken))
            .WithName("StartWorkflowRun");

        workflows.MapGet("/runs", async (
                [AsParameters] WorkflowRunListApiQuery query,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterRuns(
                await runStore.ListRunsAsync(
                    query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                    cancellationToken),
                query)))
            .WithName("ListWorkflowRuns");

        workflows.MapGet("/runs/{runId:guid}", async (
                Guid runId,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
        {
            var run = await runtimeManager.GetRunAsync(new WorkflowRunId(runId), cancellationToken);
            return run is null
                ? ApiEndpointResults.NotFound("Workflow run was not found.", "workflows.run-not-found")
                : Results.Ok(run);
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
            .WithName("GetWorkflowRunDetail");

        workflows.MapPost("/runs/{runId:guid}/cancel", async (
                Guid runId,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => runtimeManager.CancelAsync(new WorkflowRunId(runId), cancellationToken)))
            .WithName("CancelWorkflowRun");

        workflows.MapGet("/runs/{runId:guid}/events", async (
                Guid runId,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            Results.Ok(await runtimeManager.ListEventsAsync(new WorkflowRunId(runId), cancellationToken)))
            .WithName("ListWorkflowRunEvents");

        workflows.MapGet("/runs/{runId:guid}/artifacts", async (
                Guid runId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(await runStore.ListArtifactsAsync(new WorkflowRunId(runId), cancellationToken)))
            .WithName("ListWorkflowRunArtifacts");

        workflows.MapGet("/runs/{runId:guid}/pending-requests", async (
                Guid runId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(await runStore.ListPendingExternalRequestsAsync(new WorkflowRunId(runId), cancellationToken)))
            .WithName("ListWorkflowRunPendingRequests");

        workflows.MapPost("/external-requests/{requestId:guid}/response", async (
                Guid requestId,
                WorkflowExternalRequestResponseApiRequest request,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => runtimeManager.RespondToExternalRequestAsync(
                new WorkflowExternalRequestId(requestId),
                request.ResponseJson,
                cancellationToken)))
            .WithName("RespondToWorkflowExternalRequest");

        workflows.MapGet("/analytics", async (
                [AsParameters] WorkflowAnalyticsApiQuery query,
                IWorkflowCatalogService catalogService,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(await BuildAnalyticsAsync(query, catalogService, runStore, cancellationToken)))
            .WithName("GetWorkflowAnalytics");

        return group;
    }

    private static async Task<IResult> StartWorkflowRunAsync(
        Guid? routeWorkflowId,
        WorkflowRunStartApiRequest request,
        IWorkflowCatalogService catalogService,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (routeWorkflowId.HasValue &&
            request.WorkflowId.HasValue &&
            routeWorkflowId.Value != request.WorkflowId.Value)
        {
            return ApiEndpointResults.BadRequest(
                "Route workflow id does not match the request workflow id.",
                "workflows.workflow-id-mismatch");
        }

        var requestedWorkflowId = routeWorkflowId ?? request.WorkflowId;
        if (!requestedWorkflowId.HasValue)
        {
            return ApiEndpointResults.BadRequest(
                "Workflow id is required to start a workflow run.",
                "workflows.workflow-id-required");
        }

        var detail = await catalogService.GetDefinitionAsync(
            new WorkflowId(requestedWorkflowId.Value),
            request.VersionId.HasValue ? new WorkflowVersionId(request.VersionId.Value) : null,
            cancellationToken);
        if (detail is null)
        {
            return ApiEndpointResults.NotFound(
                "Workflow definition was not found.",
                "workflows.definition-not-found");
        }

        var validation = await catalogService.ValidateDefinitionAsync(detail.Definition, cancellationToken);
        if (!validation.Succeeded)
        {
            return Results.BadRequest(new WorkflowRunStartRejectedApiResponse(
                validation,
                "Workflow definition failed validation."));
        }

        string inputJson;
        try
        {
            inputJson = NormalizeInputJson(request.InputJson);
        }
        catch (JsonException exception)
        {
            return ApiEndpointResults.BadRequest(
                $"Workflow input JSON is invalid: {exception.Message}",
                "workflows.input-json-invalid");
        }

        try
        {
            var run = await runtimeManager.StartAsync(
                detail.Definition,
                new WorkflowRunStartRequest(
                    detail.Definition.Id,
                    detail.Definition.VersionId,
                    inputJson,
                    request.RequestedBackend,
                    request.SourceProcessRunId,
                    request.SourceProcessAssignmentId),
                cancellationToken);
            return Results.Ok(await BuildRunDetailAsync(run, runtimeManager, runStore, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (KeyNotFoundException exception)
        {
            return ApiEndpointResults.NotFound(exception.Message, "workflows.resource-not-found");
        }
    }

    private static async Task<IResult> GetDefinitionResultAsync(
        Guid workflowId,
        Guid? versionId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var detail = await catalogService.GetDefinitionAsync(
            new WorkflowId(workflowId),
            versionId.HasValue ? new WorkflowVersionId(versionId.Value) : null,
            cancellationToken);
        return detail is null
            ? ApiEndpointResults.NotFound("Workflow definition was not found.", "workflows.definition-not-found")
            : Results.Ok(detail);
    }

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

    private static async Task<WorkflowRunDetailApiResponse> BuildRunDetailAsync(
        WorkflowRunSnapshot run,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
    {
        return new WorkflowRunDetailApiResponse(
            run,
            await runtimeManager.ListEventsAsync(run.RunId, cancellationToken),
            await runStore.ListArtifactsAsync(run.RunId, cancellationToken),
            await runStore.ListPendingExternalRequestsAsync(run.RunId, cancellationToken));
    }

    private static async Task<WorkflowAnalyticsApiResponse> BuildAnalyticsAsync(
        WorkflowAnalyticsApiQuery query,
        IWorkflowCatalogService catalogService,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
    {
        var definitions = await catalogService.ListDefinitionsAsync(cancellationToken);
        var filteredDefinitions = query.WorkflowId.HasValue
            ? definitions.Where(item => item.Id == new WorkflowId(query.WorkflowId.Value)).ToArray()
            : definitions;
        var runs = await runStore.ListRunsAsync(
            query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
            cancellationToken);
        var filteredRuns = FilterRuns(
            runs,
            new WorkflowRunListApiQuery
            {
                WorkflowId = query.WorkflowId,
                State = query.State,
                Backend = query.Backend,
                Search = query.Search,
                Take = query.Take
            });

        return new WorkflowAnalyticsApiResponse(
            filteredDefinitions.Count,
            filteredDefinitions.Count(item => item.Status == WorkflowLifecycleStatus.Active),
            CountBy(filteredDefinitions, item => item.Status.ToString()),
            filteredRuns.Count,
            filteredRuns.Count(item => item.State == WorkflowRunState.Running),
            filteredRuns.Count(item => item.State == WorkflowRunState.WaitingForInput),
            filteredRuns.Count(item => item.State == WorkflowRunState.Failed),
            CountBy(filteredRuns, item => item.State.ToString()),
            CountBy(filteredRuns, item => item.Backend.ToString()),
            filteredRuns
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Take(NormalizeTake(query.Take))
                .ToArray());
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
            filtered = filtered.Where(item => Contains(item.Summary, query.Search) ||
                                             Contains(item.BackendRunId, query.Search) ||
                                             Contains(item.RunId.ToString(), query.Search));
        }

        return filtered
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> CountBy<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector)
    {
        return values
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeInputJson(string? inputJson)
    {
        var normalized = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson.Trim();
        using var _ = JsonDocument.Parse(normalized);
        return normalized;
    }

    private static bool Contains(string value, string? search)
    {
        return !string.IsNullOrWhiteSpace(search) &&
               value.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizeTake(int? take)
    {
        return Math.Clamp(take.GetValueOrDefault(50), 1, 500);
    }

    private static async Task<IResult> ToApiResultAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (KeyNotFoundException exception)
        {
            return ApiEndpointResults.NotFound(exception.Message, "workflows.resource-not-found");
        }
    }
}

internal sealed record WorkflowExternalRequestResponseApiRequest(string ResponseJson);

internal sealed class WorkflowRunListApiQuery
{
    public Guid? WorkflowId { get; set; }

    public WorkflowRunState? State { get; set; }

    public WorkflowRuntimeBackendKind? Backend { get; set; }

    public string? Search { get; set; }

    public int? Take { get; set; }
}

internal sealed class WorkflowAnalyticsApiQuery
{
    public Guid? WorkflowId { get; set; }

    public WorkflowRunState? State { get; set; }

    public WorkflowRuntimeBackendKind? Backend { get; set; }

    public string? Search { get; set; }

    public int? Take { get; set; }
}

internal sealed class WorkflowRunStartApiRequest
{
    public Guid? WorkflowId { get; set; }

    public Guid? VersionId { get; set; }

    public string? InputJson { get; set; }

    public WorkflowRuntimeBackendKind? RequestedBackend { get; set; }

    public Guid? SourceProcessRunId { get; set; }

    public Guid? SourceProcessAssignmentId { get; set; }
}

internal sealed record WorkflowRunDetailApiResponse(
    WorkflowRunSnapshot Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts,
    IReadOnlyList<WorkflowExternalRequestRecord> PendingExternalRequests);

internal sealed record WorkflowRunStartRejectedApiResponse(
    WorkflowValidationResult Validation,
    string ErrorMessage);

internal sealed record WorkflowAnalyticsApiResponse(
    int DefinitionCount,
    int ActiveDefinitionCount,
    IReadOnlyDictionary<string, int> DefinitionsByStatus,
    int RunCount,
    int RunningRunCount,
    int WaitingForInputRunCount,
    int FailedRunCount,
    IReadOnlyDictionary<string, int> RunsByState,
    IReadOnlyDictionary<string, int> RunsByBackend,
    IReadOnlyList<WorkflowRunSnapshot> RecentRuns);
