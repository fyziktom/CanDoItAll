using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Templates;
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
        workflows.MapWorkflowRunIdempotencyApi();
        workflows.MapWorkflowStableIdentityApi();

        workflows.MapGet("/contract", () => Results.Ok(new WorkflowApiContractResponse(
            [
                "GET /api/workflows/contract",
                "GET /api/workflows/settings",
                "POST /api/workflows/settings",
                "GET /api/workflows/runtime-backends",
                "GET /api/workflows/executor-catalog",
                "GET /api/workflows/templates",
                "POST /api/workflows/templates/{templateKey}/drafts",
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
            .WithName("ListWorkflowExecutorCatalog")
            .Produces<IReadOnlyList<WorkflowExecutorDescriptor>>(StatusCodes.Status200OK);

        workflows.MapGet("/templates", (WorkflowTemplatePackLoader templatePackLoader) =>
        {
            var pack = templatePackLoader.Load();
            return Results.Ok(pack.Workflows.Select(template => new WorkflowTemplateCatalogItem(
                template.Key,
                template.Name,
                template.Description,
                template.Graph.Nodes.Count,
                template.Graph.Edges.Count,
                pack.CreateInputParameters(template).Count,
                pack.RuntimePolicy.PreferredBackend,
                BuildTemplateFlowShape(template))).ToArray());
        })
        .WithName("ListWorkflowTemplates")
        .Produces<WorkflowTemplateCatalogItem[]>(StatusCodes.Status200OK);

        workflows.MapPost("/templates/{templateKey}/drafts", async (
                string templateKey,
                WorkflowTemplatePackLoader templatePackLoader,
                IWorkflowCatalogService catalogService,
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
        {
            var pack = templatePackLoader.Load();
            var template = pack.Workflows.FirstOrDefault(item =>
                string.Equals(item.Key, templateKey, StringComparison.OrdinalIgnoreCase));
            if (template is null)
            {
                return ApiEndpointResults.NotFound("Workflow template was not found.", "workflows.template-not-found");
            }

            var definitions = await catalogService.ListDefinitionsAsync(cancellationToken);
            var draftName = ResolveTemplateDraftName(template.Name, definitions);
            var providerOptions = await componentLibrary.ListProviderOptionsAsync(cancellationToken);
            var provider = providerOptions.FirstOrDefault(option => option.IsEnabled && option.SupportsStructuredOutput &&
                               option.ModelOptions.Contains(ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparer.OrdinalIgnoreCase))
                           ?? providerOptions.FirstOrDefault(option => option.IsEnabled && option.SupportsStructuredOutput)
                           ?? providerOptions.FirstOrDefault(option => option.IsEnabled);
            var model = provider?.ModelOptions.FirstOrDefault(option =>
                            string.Equals(option, ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparison.OrdinalIgnoreCase))
                        ?? provider?.DefaultModel
                        ?? ManagedSeedProviderFallbacks.OpenAiDefaultModel;
            var component = await componentLibrary.SaveComponentAsync(new LlmCallComponentSaveRequest(
                Id: null,
                Name: $"Draft LLM: {draftName}",
                ProviderProfileId: provider?.ProviderProfileId,
                Model: model,
                Modality: WorkflowModality.Text,
                ModelSettings: pack.CreateModelSettings(),
                Instructions: pack.CreateComponentInstructions(template),
                InputShape: pack.JsonShape,
                ResultShape: pack.JsonShape,
                Permissions: AgentPermissionsPolicy.Default with
                {
                    CanUseTools = false,
                    CanAskOtherAgents = false,
                    CanEscalateToHuman = false,
                    RequiresApprovalForExternalCalls = false
                }), cancellationToken);
            var definition = pack.CreateDefinition(template, component) with
            {
                Name = draftName,
                Description = template.Description,
                Status = WorkflowLifecycleStatus.Draft,
                RuntimePolicy = pack.RuntimePolicy,
                InputParameters = pack.CreateInputParameters(template)
            };
            var saved = await catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
                Id: null,
                ExpectedVersionId: null,
                Name: definition.Name,
                Description: definition.Description,
                Status: definition.Status,
                Graph: definition.Graph,
                RuntimePolicy: definition.RuntimePolicy)
            {
                InputParameters = definition.InputParameters
            }, cancellationToken);
            return Results.Ok(saved);
        })
        .WithName("AddWorkflowTemplateToDrafts")
        .Produces<WorkflowDefinition>(StatusCodes.Status200OK)
        .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapGet("/definitions/{workflowId:guid}", async (
                Guid workflowId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await GetDefinitionResultAsync(workflowId, versionId: null, catalogService, cancellationToken))
            .WithName("GetWorkflowDefinition")
            .Produces<WorkflowDefinitionDetail>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapGet("/definitions/{workflowId:guid}/versions/{versionId:guid}", async (
                Guid workflowId,
                Guid versionId,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await GetDefinitionResultAsync(workflowId, versionId, catalogService, cancellationToken))
            .WithName("GetWorkflowDefinitionVersion")
            .Produces<WorkflowDefinitionDetail>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

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
            .WithName("SaveWorkflowDefinition")
            .Produces<WorkflowDefinition>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapPost("/definitions/import", async (
                WorkflowDefinitionImportRequest request,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => catalogService.ImportDefinitionAsync(request, cancellationToken)))
            .WithName("ImportWorkflowDefinition")
            .Produces<WorkflowDefinition>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

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
        .WithName("ValidateSavedWorkflowDefinition")
        .Produces<WorkflowValidationResult>(StatusCodes.Status200OK)
        .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapPost("/validate", async (
                WorkflowDefinition request,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalogService.ValidateDefinitionAsync(request, cancellationToken)))
            .WithName("ValidateDraftWorkflowDefinition")
            .Produces<WorkflowValidationResult>(StatusCodes.Status200OK);

        workflows.MapGet("/provider-options", async (
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
            Results.Ok(await componentLibrary.ListProviderOptionsAsync(cancellationToken)))
            .WithName("ListWorkflowProviderOptions")
            .Produces<IReadOnlyList<WorkflowProviderOption>>(StatusCodes.Status200OK);

        workflows.MapGet("/components", async (
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
            Results.Ok(await componentLibrary.ListComponentsAsync(cancellationToken)))
            .WithName("ListWorkflowComponents")
            .Produces<IReadOnlyList<LlmCallComponent>>(StatusCodes.Status200OK);

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
        .WithName("GetWorkflowComponent")
        .Produces<LlmCallComponent>(StatusCodes.Status200OK)
        .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapPost("/components", async (
                LlmCallComponentSaveRequest request,
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(() => componentLibrary.SaveComponentAsync(request, cancellationToken)))
            .WithName("SaveWorkflowComponent")
            .Produces<LlmCallComponent>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapDelete("/components/{componentId:guid}", async (
                Guid componentId,
                IWorkflowComponentLibraryService componentLibrary,
                CancellationToken cancellationToken) =>
        {
            await componentLibrary.DeleteComponentAsync(new WorkflowComponentId(componentId), cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("DeleteWorkflowComponent")
        .Produces<ApiAck>(StatusCodes.Status200OK);

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
        .WithName("RunWorkflowTest")
        .Produces<WorkflowTestRunResult>(StatusCodes.Status200OK)
        .Produces<WorkflowTestRunResult>(StatusCodes.Status400BadRequest);

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

        workflows.MapGet("/runs/page", async (
                [AsParameters] WorkflowRunListApiQuery query,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(await runStore.ListRunPageAsync(
                new WorkflowRunPageRequest(
                    query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                    query.State,
                    query.Backend,
                    query.Search ?? string.Empty,
                    query.PageIndex.GetValueOrDefault(),
                    query.PageSize ?? query.Take ?? 50),
                cancellationToken)))
            .WithName("ListWorkflowRunPage");

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
            Results.Ok(await runtimeManager.ListEventsAsync(new WorkflowRunId(runId), cancellationToken)))
            .WithName("ListWorkflowRunEvents");

        workflows.MapGet("/runs/{runId:guid}/events/page", async (
                Guid runId,
                [AsParameters] WorkflowEventListApiQuery query,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            Results.Ok(await runtimeManager.ListEventPageAsync(
                new WorkflowEventPageRequest(
                    new WorkflowRunId(runId),
                    query.PageIndex.GetValueOrDefault(),
                    query.PageSize.GetValueOrDefault(50)),
                cancellationToken)))
            .WithName("ListWorkflowRunEventPage");

        workflows.MapGet("/runs/{runId:guid}/artifacts", async (
                Guid runId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(await runStore.ListArtifactsAsync(new WorkflowRunId(runId), cancellationToken)))
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
            Results.Ok(await runStore.ListCheckpointsAsync(new WorkflowRunId(runId), cancellationToken)))
            .WithName("ListWorkflowRunCheckpoints");

        workflows.MapGet("/runs/{runId:guid}/pending-requests", async (
                Guid runId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(await runStore.ListPendingExternalRequestsAsync(new WorkflowRunId(runId), cancellationToken)))
            .WithName("ListWorkflowRunPendingRequests");

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

        workflows.MapPost("/external-requests/{requestId:guid}/response", async (
                Guid requestId,
                WorkflowExternalRequestResponseApiRequest request,
                IWorkflowRuntimeManager runtimeManager,
                CancellationToken cancellationToken) =>
            MapExternalResponseResult(await runtimeManager.SubmitExternalResponseAsync(
                new WorkflowExternalRequestId(requestId),
                request.ResponseJson,
                cancellationToken)))
            .WithName("RespondToWorkflowExternalRequest");

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
                        new WorkflowLaunchCorrelationId(httpContext.TraceIdentifier)),
                    request.InputJson ?? "{}",
                    WorkflowLaunchCompletionPolicy.WaitForStopped,
                    ResolveLaunchIdempotency(httpContext, idempotencyKey))
                {
                    RequestedBackend = request.RequestedBackend
                },
                cancellationToken);
            var detail = await BuildRunDetailAsync(
                launchResult.Run,
                runtimeManager,
                runStore,
                cancellationToken);
            var keyHash = launchResult.ResolvedRequest.Idempotency is
                WorkflowLaunchIdempotency.CallerSupplied keyed
                    ? WorkflowLaunchIdempotencyRequestFactory.CreateKeyHash(keyed.Key)
                    : null;
            return Results.Ok(WorkflowRunStartApiResponse.From(
                detail,
                launchResult.IdempotencyDisposition,
                keyHash));
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

    private static IResult MapExternalResponseResult(WorkflowExternalResponseResult result)
        => result.Outcome switch
        {
            WorkflowExternalResponseOutcome.Accepted => Results.Ok(result),
            WorkflowExternalResponseOutcome.RequestNotFound or WorkflowExternalResponseOutcome.RunNotFound =>
                Results.Json(result, statusCode: StatusCodes.Status404NotFound),
            WorkflowExternalResponseOutcome.AlreadyResponded or
                WorkflowExternalResponseOutcome.RunNotWaiting or
                WorkflowExternalResponseOutcome.TransitionRejected =>
                Results.Json(result, statusCode: StatusCodes.Status409Conflict),
            WorkflowExternalResponseOutcome.UnsupportedResume =>
                Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity),
            WorkflowExternalResponseOutcome.BackendUnavailable =>
                Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable),
            WorkflowExternalResponseOutcome.ResumeFailed =>
                Results.Json(result, statusCode: StatusCodes.Status502BadGateway),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unknown workflow external-response outcome.")
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
            return ApiEndpointResults.NotFound("Workflow artifact was not found for this run.", "workflows.artifact-not-found");
        }

        var content = await artifactContentStore.ReadContentAsync(artifact, cancellationToken);
        if (content is null)
        {
            return ApiEndpointResults.NotFound("Workflow artifact content was not found for this artifact.", "workflows.artifact-content-not-found");
        }

        return Results.Text(content.Content, artifact.ContentType);
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
            await runStore.ListPendingExternalRequestsAsync(run.RunId, cancellationToken),
            await runStore.ListCheckpointsAsync(run.RunId, cancellationToken));
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

    private static bool Contains(string value, string? search)
    {
        return !string.IsNullOrWhiteSpace(search) &&
               value.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizeTake(int? take)
    {
        return Math.Clamp(take.GetValueOrDefault(50), 1, 500);
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

    private static string BuildTemplateFlowShape(WorkflowTemplateDefinition template)
        => template.Graph.Nodes.Count == 0
            ? "No nodes"
            : string.Join(", ", template.Graph.Nodes
                .GroupBy(node => string.IsNullOrWhiteSpace(node.Kind) ? "Unknown" : node.Kind)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => $"{group.Count()} {group.Key}"));

    private static string ResolveTemplateDraftName(
        string templateName,
        IReadOnlyList<WorkflowCatalogItem> definitions)
    {
        var baseName = templateName.Trim();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new InvalidOperationException("Workflow template name is required before it can be added to drafts.");
        }

        var names = definitions.Select(definition => definition.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!names.Contains(baseName))
        {
            return baseName;
        }

        for (var index = 1; index <= 999; index++)
        {
            var candidate = $"{index:00} {baseName}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"No available draft name remains for template '{baseName}'.");
    }
}

internal sealed record WorkflowExternalRequestResponseApiRequest(string ResponseJson);

internal sealed record WorkflowApiContractResponse(
    IReadOnlyList<string> Endpoints,
    string BoundarySummary);

internal sealed record WorkflowTemplateCatalogItem(
    string Key,
    string Name,
    string Description,
    int NodeCount,
    int EdgeCount,
    int InputCount,
    WorkflowRuntimeBackendKind PreferredBackend,
    string FlowShape);

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

internal sealed record WorkflowRunDetailApiResponse(
    WorkflowRunSnapshot Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts,
    IReadOnlyList<WorkflowExternalRequestRecord> PendingExternalRequests,
    IReadOnlyList<WorkflowCheckpointRecord> Checkpoints);

internal sealed record WorkflowRunStartApiResponse(
    WorkflowRunSnapshot Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts,
    IReadOnlyList<WorkflowExternalRequestRecord> PendingExternalRequests,
    IReadOnlyList<WorkflowCheckpointRecord> Checkpoints,
    WorkflowLaunchIdempotencyDisposition IdempotencyDisposition,
    string? IdempotencyKeyHash,
    bool Created,
    bool Replayed)
{
    public static WorkflowRunStartApiResponse From(
        WorkflowRunDetailApiResponse detail,
        WorkflowLaunchIdempotencyDisposition disposition,
        string? idempotencyKeyHash) => new(
            detail.Run,
            detail.Events,
            detail.Artifacts,
            detail.PendingExternalRequests,
            detail.Checkpoints,
            disposition,
            idempotencyKeyHash,
            Created: disposition is not WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun,
            Replayed: disposition is WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun);
}
