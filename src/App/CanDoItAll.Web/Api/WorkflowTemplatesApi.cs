using CanDoItAll.AgentFramework.Models;
using System.ComponentModel;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Web.Api;

internal static class WorkflowTemplatesApi {
    public static void MapWorkflowTemplates(this RouteGroupBuilder workflows) {
        workflows.MapGet("/templates", (WorkflowTemplateDraftService service) => service.List())
            .WithName("ListWorkflowTemplates").Produces<IReadOnlyList<WorkflowTemplateCatalogItem>>().ProducesApiErrors(401, 403, 503)
            .DescribeApi("List workflow templates", "Read template-pack metadata and display summaries. Requires workflow read capability; makes no model request.", "Available workflow templates and their graph/input counts.");
        workflows.MapPost("/templates/{templateKey}/drafts", CreateAsync)
            .WithName("AddWorkflowTemplateToDrafts").Produces<WorkflowDefinition>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("Create a workflow template draft", "Create a new draft and its component using an enabled structured-output provider and available configured model. Requires workflow write capability. Every request creates fresh identities; retries can create another draft. External-call approval remains required and no model executes.", "Persisted draft workflow definition; its dependent component was also saved.");
    }

    private static async Task<IResult> CreateAsync([Description("Workflow template-pack key from the template list; case-insensitive, nonempty and at most 256 characters.")] string templateKey, WorkflowTemplateDraftService service, CancellationToken cancellationToken) {
        try {
            return Results.Ok(await service.CreateAsync(templateKey, cancellationToken));
        } catch (KeyNotFoundException) {
            return ApiEndpointResults.NotFound("The workflow template was not found.", "workflows.template-not-found");
        } catch (ArgumentException) {
            return ApiEndpointResults.BadRequest("The template key is invalid.", "workflows.template-invalid");
        }
    }
}
