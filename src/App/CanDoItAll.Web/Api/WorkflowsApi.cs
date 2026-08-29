using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedKernel;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class WorkflowsApi
{
    public static RouteGroupBuilder MapWorkflowsApi(this RouteGroupBuilder group)
    {
        var workflows = group.MapGroup("/workflows")
            .WithTags("Workflows")
            .DisableAntiforgery();

        workflows.MapWorkflowRunControlApi();
        workflows.MapWorkflowRunReadApi();
        workflows.MapWorkflowRunIdempotencyApi();
        workflows.MapWorkflowStableIdentityApi();
        workflows.MapWorkflowExternalResponseApi();

        workflows.MapGet("/contract", () => Results.Ok(new WorkflowApiContractResponse(
            [
                "GET /api/workflows/contract",
                "GET /api/workflows/settings",
                "POST /api/workflows/settings",
                "GET /api/workflows/runtime-backends",
                "GET /api/workflows/executor-catalog",
                "GET /api/workflows/definitions",
                "GET /api/workflows/definitions?externalNamespace={namespace}&externalKey={key}",
                "GET /api/workflows/definitions/by-template-key/{templateKey}",
                "GET /api/workflows/definitions/by-external-key/{externalNamespace}/{externalKey}",
                "POST /api/workflows/definitions",
                "GET /api/workflows/definitions/{workflowId}",
                "GET /api/workflows/definitions/{workflowId}/versions/{versionId}",
                "GET /api/workflows/definitions/{workflowId}/export",
                "POST /api/workflows/definitions/import",
                "POST /api/workflows/definitions/{workflowId}/publish",
                "POST /api/workflows/definitions/{workflowId}/suspend",
                "POST /api/workflows/definitions/{workflowId}/archive",
                "DELETE /api/workflows/definitions/{workflowId}",
                "POST /api/workflows/definitions/{workflowId}/validate",
                "POST /api/workflows/definitions/{workflowId}/runs/start",
                "POST /api/workflows/validate",
                "GET /api/workflows/provider-options",
                "GET /api/workflows/components",
                "POST /api/workflows/components",
                "GET /api/workflows/components/{componentId}",
                "DELETE /api/workflows/components/{componentId}",
                "POST /api/workflows/test-runs",
                "POST /api/workflows/runs/start",
                "GET /api/workflows/runs/by-idempotency-key/{key}",
                "GET /api/workflows/runs",
                "GET /api/workflows/runs/page",
                "GET /api/workflows/runs/{runId}",
                "GET /api/workflows/runs/{runId}/detail",
                "POST /api/workflows/runs/{runId}/cancel",
                "GET /api/workflows/runs/{runId}/events",
                "GET /api/workflows/runs/{runId}/events/page",
                "GET /api/workflows/events/stream",
                "GET /api/workflows/runs/{runId}/events/stream",
                "GET /api/workflows/runs/{runId}/artifacts",
                "GET /api/workflows/runs/{runId}/artifacts/{artifactId}/content",
                "GET /api/workflows/runs/{runId}/checkpoints",
                "GET /api/workflows/runs/{runId}/pending-requests",
                "POST /api/workflows/external-requests/{requestId}/response",
                "GET /api/workflows/external-response-operations/{operationId}",
                "GET /api/workflows/analytics"
            ],
            "Workflow control remains HTTP/API-driven. Executor catalogs expose tool side-effect contracts; agent skill, tool, and MCP capability setup is validated through /api/agents/capabilities.")))
        .WithName("GetWorkflowsApiContract");

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

        workflows.MapGet("/executor-catalog", async (
                IWorkflowExecutorRuntimeAvailabilityCatalog executorCatalog,
                CancellationToken cancellationToken) =>
            Results.Ok(await executorCatalog.ListExecutorsAsync(cancellationToken)))
            .WithName("ListWorkflowExecutorCatalog");

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

        workflows.MapGet("/definitions/{workflowId:guid}/export", async (
                Guid workflowId,
                Guid? versionId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
        {
            var envelope = await catalogService.ExportDefinitionAsync(
                new WorkflowId(workflowId),
                versionId.HasValue ? new WorkflowVersionId(versionId.Value) : null,
                cancellationToken);
            return envelope is null
                ? ApiEndpointResults.NotFound("Workflow definition was not found.", "workflows.definition-not-found")
                : Results.Ok(envelope);
        })
        .WithName("ExportWorkflowDefinition");

        workflows.MapPost("/definitions", async (
                WorkflowDefinitionSaveRequest request,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => catalogService.SaveDefinitionAsync(request, cancellationToken)))
            .WithName("SaveWorkflowDefinition");

        workflows.MapPost("/definitions/import", async (
                WorkflowDefinitionImportRequest request,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => catalogService.ImportDefinitionAsync(request, cancellationToken)))
            .WithName("ImportWorkflowDefinition");

        workflows.MapPost("/definitions/{workflowId:guid}/publish", async (
                Guid workflowId,
                Guid? expectedVersionId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ChangeDefinitionStatusAsync(
                workflowId,
                expectedVersionId,
                WorkflowLifecycleStatus.Active,
                catalogService,
                cancellationToken))
            .WithName("PublishWorkflowDefinition");

        workflows.MapPost("/definitions/{workflowId:guid}/suspend", async (
                Guid workflowId,
                Guid? expectedVersionId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ChangeDefinitionStatusAsync(
                workflowId,
                expectedVersionId,
                WorkflowLifecycleStatus.Suspended,
                catalogService,
                cancellationToken))
            .WithName("SuspendWorkflowDefinition");

        workflows.MapPost("/definitions/{workflowId:guid}/archive", async (
                Guid workflowId,
                Guid? expectedVersionId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ChangeDefinitionStatusAsync(
                workflowId,
                expectedVersionId,
                WorkflowLifecycleStatus.Archived,
                catalogService,
                cancellationToken))
            .WithName("ArchiveWorkflowDefinition");

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

        workflows.MapGet("/analytics", async (
                [AsParameters] WorkflowAnalyticsApiQuery query,
                IWorkflowAnalyticsQueryService analyticsQueryService,
                CancellationToken cancellationToken) =>
            await GetWorkflowAnalyticsResultAsync(query, analyticsQueryService, cancellationToken))
            .WithName("GetWorkflowAnalytics");

        return group;
    }

    internal static RouteGroupBuilder MapWorkflowRunControlApi(this RouteGroupBuilder workflows)
    {
        workflows.MapPost("/definitions/{workflowId:guid}/runs/start", async (
                Guid workflowId,
                WorkflowRunStartApiRequest request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                HttpContext httpContext,
                IWorkflowLaunchService launchService,
                IWorkflowRuntimeManager runtimeManager,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            await StartWorkflowRunAsync(
                workflowId,
                request,
                idempotencyKey,
                httpContext,
                launchService,
                runtimeManager,
                runStore,
                cancellationToken))
            .WithName("StartWorkflowDefinitionRun")
            .Produces<WorkflowRunStartApiResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapPost("/runs/start", async (
                WorkflowRunStartApiRequest request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                HttpContext httpContext,
                IWorkflowLaunchService launchService,
                IWorkflowRuntimeManager runtimeManager,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            await StartWorkflowRunAsync(
                routeWorkflowId: null,
                request,
                idempotencyKey,
                httpContext,
                launchService,
                runtimeManager,
                runStore,
                cancellationToken))
            .WithName("StartWorkflowRun")
            .Produces<WorkflowRunStartApiResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapPost("/runs/{runId:guid}/cancel", async (
                Guid runId,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            MapCancellationResult(await runtimeManager.RequestCancellationAsync(
                new WorkflowRunId(runId),
                cancellationToken)))
            .WithName("CancelWorkflowRun");

        return workflows;
    }

    private static async Task<IResult> ChangeDefinitionStatusAsync(
        Guid workflowId,
        Guid? expectedVersionId,
        WorkflowLifecycleStatus status,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        return await ToApiResultAsync(() => catalogService.ChangeDefinitionStatusAsync(
            new WorkflowDefinitionStatusChangeRequest(
                new WorkflowId(workflowId),
                expectedVersionId.HasValue ? new WorkflowVersionId(expectedVersionId.Value) : null,
                status),
            cancellationToken));
    }

    private static async Task<IResult> StartWorkflowRunAsync(
        Guid? routeWorkflowId,
        WorkflowRunStartApiRequest request,
        string? idempotencyKey,
        HttpContext httpContext,
        IWorkflowLaunchService launchService,
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

        try
        {
            var workflowId = new WorkflowId(requestedWorkflowId.Value);
            WorkflowDefinitionSelection selection = request.VersionId.HasValue
                ? new WorkflowDefinitionSelection.ExactSavedVersion(
                    workflowId,
                    new WorkflowVersionId(request.VersionId.Value))
                : new WorkflowDefinitionSelection.LatestActive(workflowId);
            var launchResult = await launchService.LaunchAsync(
                new WorkflowLaunchIntent(
                    selection,
                    WorkflowLaunchMode.Production,
                    new WorkflowLaunchOrigin.Api(
                        ResolveApiActor(httpContext.User),
                        new WorkflowLaunchCorrelationId(httpContext.TraceIdentifier)) {
                        HistoryCaller = ProviderHistoryRequestContext.Caller(httpContext)
                    },
                    request.InputJson ?? "{}",
                    WorkflowLaunchCompletionPolicy.WaitForStopped,
                    ResolveLaunchIdempotency(httpContext, idempotencyKey))
                {
                    RequestedBackend = request.RequestedBackend
                },
                cancellationToken);
            var detail = await WorkflowRunReadEndpoints.BuildRunDetailAsync(
                launchResult.Run,
                runtimeManager,
                runStore,
                cancellationToken);
            return Results.Ok(WorkflowRunStartApiResponse.From(
                detail,
                launchResult.IdempotencyDisposition));
        }
        catch (WorkflowLaunchValidationException exception)
        {
            var errors = exception.Validation.Issues.Count == 0
                ?
                [
                    new ApiErrorItem(
                        "workflows.validation-failed",
                        exception.Message,
                        ErrorSeverity.Error)
                ]
                : exception.Validation.Issues
                    .Select(issue => new ApiErrorItem(
                        $"workflows.validation.{issue.Code}",
                        issue.Message,
                        ErrorSeverity.Error))
                    .ToArray();
            return Results.BadRequest(new ApiErrorResponse(errors));
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (WorkflowLaunchIdempotencyConflictException)
        {
            return ApiEndpointResults.Conflict(
                "The Idempotency-Key was already used for a different workflow launch request.",
                "workflows.idempotency-key-conflict");
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

    private static WorkflowLaunchIdempotency ResolveLaunchIdempotency(
        HttpContext httpContext,
        string? idempotencyKey)
    {
        const string headerName = "Idempotency-Key";
        if (!httpContext.Request.Headers.TryGetValue(headerName, out var values))
        {
            return new WorkflowLaunchIdempotency.NotRequested();
        }

        if (values.Count != 1 ||
            string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{headerName} must contain exactly one non-empty value.",
                headerName);
        }

        return new WorkflowLaunchIdempotency.CallerSupplied(
            new WorkflowLaunchIdempotencyKey(idempotencyKey));
    }

    private static WorkflowLaunchActor ResolveApiActor(ClaimsPrincipal principal)
    {
        var subjectId = principal.FindFirst("sub")?.Value ??
                        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                        principal.Identity?.Name;
        return string.IsNullOrWhiteSpace(subjectId)
            ? new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "candoitall-api")
            : new WorkflowLaunchActor(WorkflowLaunchActorKind.User, subjectId);
    }

    private static IResult MapCancellationResult(WorkflowRunCancellationResult result)
        => result.Outcome switch
        {
            WorkflowRunCancellationOutcome.CancellationRequested => Results.Ok(result),
            WorkflowRunCancellationOutcome.NotFound => Results.Json(result, statusCode: StatusCodes.Status404NotFound),
            WorkflowRunCancellationOutcome.AlreadyTerminal or
                WorkflowRunCancellationOutcome.NotActive or
                WorkflowRunCancellationOutcome.TransitionRejected =>
                Results.Json(result, statusCode: StatusCodes.Status409Conflict),
            WorkflowRunCancellationOutcome.BackendNotCancellable =>
                Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unknown workflow cancellation outcome.")
        };

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

    private static int NormalizeAnalyticsRecentTake(int? take)
    {
        if (take is null)
        {
            return 8;
        }

        if (take is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                "Workflow analytics recent take must be between 1 and 500.");
        }

        return take.Value;
    }

    internal static Task<IResult> GetWorkflowAnalyticsResultAsync(
        WorkflowAnalyticsApiQuery query,
        IWorkflowAnalyticsQueryService analyticsQueryService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(analyticsQueryService);
        return ToApiResultAsync(() => analyticsQueryService.QueryAsync(
            new WorkflowAnalyticsQuery(
                query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                query.State,
                query.Backend,
                query.Search ?? string.Empty,
                NormalizeAnalyticsRecentTake(query.Take)),
            cancellationToken));
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

internal sealed record WorkflowApiContractResponse(
    IReadOnlyList<string> Endpoints,
    string BoundarySummary);

internal sealed class WorkflowAnalyticsApiQuery
{
    public Guid? WorkflowId { get; set; }

    public WorkflowRunState? State { get; set; }

    public WorkflowRuntimeBackendKind? Backend { get; set; }

    public string? Search { get; set; }

    public int? Take { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorkflowRunStartApiRequest
{
    public Guid? WorkflowId { get; set; }

    public Guid? VersionId { get; set; }

    public string? InputJson { get; set; }

    public WorkflowRuntimeBackendKind? RequestedBackend { get; set; }
}
