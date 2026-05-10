using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

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

        workflows.MapPost("/validate", async (
                WorkflowDefinition request,
                IWorkflowCatalogService catalogService,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalogService.ValidateDefinitionAsync(request, cancellationToken)))
            .WithName("ValidateDraftWorkflowDefinition");

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

        workflows.MapGet("/runs", async (
                Guid? workflowId,
                IWorkflowRunStore runStore,
                CancellationToken cancellationToken) =>
            Results.Ok(await runStore.ListRunsAsync(
                workflowId.HasValue ? new WorkflowId(workflowId.Value) : null,
                cancellationToken)))
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

        return group;
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
