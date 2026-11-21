using System.ComponentModel;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Web.Api;

internal static class RemoteWorkflowsApi {
    public static void MapRemoteWorkflows(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(RemoteWorkflowRoutes.Catalog).WithTags("Shared workflows")
            .WithMetadata(new ApiPermissionMetadata(ApiAccessScopeNames.ReadSharedWorkflows))
            .RequireAuthorization(ApiAuthorizationPolicies.ReadSharedWorkflows);
        group.MapGet(string.Empty, ListAsync).WithName("ListRemoteWorkflowTemplates")
            .Produces<IReadOnlyList<RemoteTemplateRow>>().ProducesApiErrors(401, 403, 409)
            .DescribeApi("List shared workflows", "Read the explicitly shared active and draft workflows on this publisher. Requires shared workflow read access.", "The current shareable workflow catalog.");
        group.MapGet("/{id:guid}", ReadAsync).WithName("GetRemoteWorkflowTemplate")
            .Produces<RemoteWorkflowDocument>().ProducesApiErrors(401, 403, 404, 409)
            .DescribeApi("Read a shared workflow", "Read a saved workflow graph, input descriptors, portable LLM components and model requirements. Requires shared workflow read access.", "The current portable workflow definition.");
    }

    private static async Task<IResult> ListAsync(RemoteWorkflowCatalog catalog, CancellationToken token) {
        try {
            return Results.Ok(await catalog.ListAsync(token));
        } catch (RemoteInstanceReadException) {
            return ApiEndpointResults.Conflict("The workflow catalog changed. Refresh it.", "shared-workflows.workspace-changed");
        }
    }

    private static async Task<IResult> ReadAsync([Description("Publisher-local identity of an explicitly published workflow.")] Guid id,
        RemoteWorkflowCatalog catalog, CancellationToken token) {
        try {
            var document = await catalog.FindAsync(id, token);
            return document is null ? ApiEndpointResults.NotFound("The shared workflow was not found.", "shared-workflows.not-found") : Results.Ok(document);
        } catch (RemoteInstanceReadException) {
            return ApiEndpointResults.Conflict("The shared workflow changed or needs its publisher to review a dependency.", "shared-workflows.unavailable");
        }
    }
}
