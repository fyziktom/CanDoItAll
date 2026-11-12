using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using System.ComponentModel;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal static class WorkflowTemplatesApi {
    public static void MapWorkflowTemplates(this RouteGroupBuilder workflows) {
        workflows.MapGet("/templates", (WorkflowTemplateDraftService service) => service.List())
            .WithName("ListWorkflowTemplates").Produces<IReadOnlyList<WorkflowTemplateCatalogItem>>().ProducesApiErrors(401, 403, 503)
            .DescribeApi("List workflow templates", "Read template-pack metadata and display summaries. Requires workflow read capability; makes no model request.", "Available workflow templates and their graph/input counts.");
        workflows.MapPost("/templates/{templateKey}/drafts", CreateAsync)
            .WithName("AddWorkflowTemplateToDrafts").Produces<WorkflowDefinition>().ProducesApiErrors(400, 401, 403, 404, 409)
            .Produces<WorkflowTemplateDraftCreationUnknownResponse>(StatusCodes.Status503ServiceUnavailable)
            .DescribeApi("Create a workflow template draft", "Create a new draft and its component using an enabled structured-output provider and available configured model. Requires workflow write capability. Every request creates fresh identities. On an unknown acknowledgement, 503 retains workflow/component IDs for read-only review; do not replay the command. A known precommit refusal cleans only its exact acknowledged component revision. External-call approval remains required and no model executes.", "Persisted draft workflow definition; its dependent component was also saved.");
    }

    private static async Task<IResult> CreateAsync([Description("Workflow template-pack key from the template list; case-insensitive, nonempty and at most 256 characters.")] string templateKey, WorkflowTemplateDraftService service, CancellationToken cancellationToken) {
        try {
            return Results.Ok(await service.CreateAsync(templateKey, cancellationToken));
        } catch (WorkflowDraftCreationUnknownException exception) {
            return Results.Json(new WorkflowTemplateDraftCreationUnknownResponse(exception.WorkflowId, exception.ComponentId,
                [new("workflows.template-outcome-unknown", exception.Message, ErrorSeverity.Error)]), statusCode: StatusCodes.Status503ServiceUnavailable);
        } catch (WorkflowComponentConcurrencyException) {
            return ApiEndpointResults.Conflict("The chosen component identity changed. No draft creation was acknowledged.", "workflows.component-conflict");
        } catch (WorkflowDefinitionConcurrencyException) {
            return ApiEndpointResults.Conflict("The chosen workflow identity changed. The known refusal did not replace it.", "workflows.definition-conflict");
        } catch (WorkflowDefinitionValidationException) {
            return ApiEndpointResults.BadRequest("The template definition was refused before commit.", "workflows.template-invalid");
        } catch (KeyNotFoundException) {
            return ApiEndpointResults.NotFound("The workflow template was not found.", "workflows.template-not-found");
        } catch (ArgumentException) {
            return ApiEndpointResults.BadRequest("The template key is invalid.", "workflows.template-invalid");
        }
    }
}

[Description("An unconfirmed template creation. Retained IDs are diagnostic targets for reads, not proof of commitment. Do not automatically replay this request.")]
internal sealed record WorkflowTemplateDraftCreationUnknownResponse(
    [property: Description("The workflow identity submitted by this attempt; its existence alone does not prove the attempt committed.")] WorkflowId WorkflowId,
    [property: Description("The component identity submitted by this attempt; it is retained when acknowledgement or conditional cleanup is unconfirmed.")] WorkflowComponentId ComponentId,
    [property: Description("The typed failure code and recovery message; template-outcome-unknown forbids blind command replay.")] IReadOnlyList<ApiErrorItem> Errors);
