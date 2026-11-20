using System.ComponentModel;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal static class RemoteAgentTemplatesApi {
    public static void MapRemoteAgentTemplates(this IEndpointRouteBuilder endpoints) {
        var catalog = endpoints.MapGroup(RemoteAgentTemplateRoutes.Catalog)
            .WithTags("Shared Agent Templates")
            .WithMetadata(new ApiPermissionMetadata(ApiAccessScopeNames.ReadSharedAgentTemplates))
            .RequireAuthorization(ApiAuthorizationPolicies.ReadSharedAgentTemplates);
        catalog.MapGet(string.Empty, async (IAgentFrameworkWorkspaceService workspace, CancellationToken cancellationToken) =>
            RemoteAgentTemplateCatalog.List((await workspace.LoadCatalogSnapshotAsync(cancellationToken)).Snapshot.Catalog))
            .WithName("ListRemoteAgentTemplates").Produces<IReadOnlyList<RemoteAgentTemplateSummary>>().ProducesApiErrors(401, 403)
            .DescribeApi("List shared agent templates", "List template summaries in the current publisher workspace. Requires the shared agent template read scope.", "Portable template summaries.");
        catalog.MapGet("/{id:guid}", ReadAsync)
            .WithName("GetRemoteAgentTemplate").Produces<RemoteAgentTemplate>().ProducesApiErrors(401, 403, 404, 409)
            .DescribeApi("Read a shared agent template", "Read portable instructions, inline skills and local capability requirements for one template.", "A portable definition for local import.");
    }

    private static async Task<IResult> ReadAsync([Description("Publisher-local template ID from the shared catalog.")] Guid id,
        IAgentFrameworkWorkspaceService workspace, CancellationToken cancellationToken) {
        var snapshot = await workspace.LoadCatalogSnapshotAsync(cancellationToken);
        try {
            var template = RemoteAgentTemplateCatalog.Find(snapshot.Snapshot.Catalog, id);
            return template is null
                ? ApiEndpointResults.NotFound("The agent template was not found.", "shared-agent-templates.not-found")
                : Results.Ok(template);
        } catch (Exception exception) when (exception is JsonException or InvalidOperationException) {
            return ApiEndpointResults.Conflict("The template has invalid or unresolved capability declarations.", "shared-agent-templates.invalid-capability");
        }
    }
}
