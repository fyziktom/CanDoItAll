using System.ComponentModel;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal static class RemoteAgentTemplatesApi {
    public static void MapRemoteAgentTemplates(this IEndpointRouteBuilder endpoints) {
        var catalog = endpoints.MapGroup(RemoteAgentTemplateRoutes.Catalog)
            .WithTags("Shared Agent Templates")
            .WithMetadata(new ApiPermissionMetadata(ApiAccessScopeNames.ReadSharedAgentTemplates))
            .RequireAuthorization(ApiAuthorizationPolicies.ReadSharedAgentTemplates);
        catalog.MapGet(string.Empty, ListAsync)
            .WithName("ListRemoteAgentTemplates").Produces<IReadOnlyList<RemoteAgentTemplateSummary>>().ProducesApiErrors(401, 403, 409)
            .DescribeApi("List shared agent templates", "List template summaries in the current publisher workspace. Requires the shared agent template read scope.", "Portable template summaries.");
        catalog.MapGet("/{id:guid}", ReadAsync)
            .WithName("GetRemoteAgentTemplate").Produces<RemoteAgentTemplate>().ProducesApiErrors(401, 403, 404, 409)
            .DescribeApi("Read a shared agent template", "Read portable instructions, inline skills and local capability requirements for one template.", "A portable definition for local import.");
        catalog.MapGet("/{id:guid}/avatar", ReadAvatarAsync)
            .WithName("GetRemoteAgentTemplateAvatar").Produces<RemoteTemplateEmbeddedAvatar>().ProducesApiErrors(401, 403, 404, 409)
            .DescribeApi("Read a shared agent avatar", "Read the embedded raster avatar of a currently shared agent template at its catalog revision.", "A bounded template avatar.");
    }

    private static async Task<IResult> ListAsync(IAgentFrameworkWorkspaceService workspace, SharedProviderServiceIdentityStore identities,
        CancellationToken cancellationToken) {
        var (catalog, publisherId, _) = await ReadSnapshotAsync(workspace, identities, cancellationToken);
        return catalog is null ? WorkspaceChanged() : Results.Ok(RemoteAgentTemplateCatalog.List(catalog, publisherId));
    }

    private static async Task<IResult> ReadAsync([Description("Publisher-local template ID from the shared catalog.")] Guid id,
        IAgentFrameworkWorkspaceService workspace, SharedProviderServiceIdentityStore identities, RemoteTemplateProviderCatalog providers, CancellationToken cancellationToken) {
        var (catalog, publisherId, original) = await ReadSnapshotAsync(workspace, identities, cancellationToken);
        if (catalog is null) {
            return WorkspaceChanged();
        }
        try {
            var template = RemoteAgentTemplateCatalog.Find(catalog, publisherId, id);
            if (template is null) {
                return ApiEndpointResults.NotFound("The agent template was not found.", "shared-agent-templates.not-found");
            }
            var agent = catalog.Agents.Single(item => item.Id == id);
            var preferred = await providers.DescribeAsync(new(agent.Name, AgentFramework.Models.ProviderProfilePurpose.Chat,
                Tools: template.Capabilities.Any(capability => capability.Kind is CapabilityKind.McpServer or CapabilityKind.Tool)),
                agent.ProviderProfileId, agent.Model, cancellationToken);
            return await workspace.ReadAgentDecisionDataPolicyContextAsync(cancellationToken) == original
                ? Results.Ok(template with { PreferredModel = preferred }) : WorkspaceChanged();
        } catch (Exception exception) when (exception is JsonException or InvalidOperationException) {
            return ApiEndpointResults.Conflict("The template has invalid or unresolved capability declarations.", "shared-agent-templates.invalid-capability");
        }
    }

    private static async Task<IResult> ReadAvatarAsync([Description("Publisher-local ID of a shared template with an embedded avatar.")] Guid id,
        IAgentFrameworkWorkspaceService workspace, SharedProviderServiceIdentityStore identities, CancellationToken cancellationToken) {
        var (catalog, publisherId, _) = await ReadSnapshotAsync(workspace, identities, cancellationToken);
        if (catalog is null) {
            return WorkspaceChanged();
        }
        return RemoteAgentTemplateCatalog.FindAvatar(catalog, publisherId, id) is { } avatar ? Results.Ok(avatar) :
            ApiEndpointResults.NotFound("The shared template avatar was not found.", "shared-agent-templates.avatar-not-found");
    }

    private static async Task<(SandboxWorkspaceCatalog? Catalog, Guid PublisherId, AgentExecutionActivityWorkspaceIdentity Workspace)> ReadSnapshotAsync(IAgentFrameworkWorkspaceService workspace,
        SharedProviderServiceIdentityStore identities, CancellationToken cancellationToken) {
        var opening = await workspace.LoadCatalogSnapshotAsync(cancellationToken);
        var publisherId = await identities.GetOrCreateAsync(cancellationToken);
        var current = await workspace.ReadAgentDecisionDataPolicyContextAsync(cancellationToken);
        return opening.Workspace == current ? (opening.Snapshot.Catalog, publisherId.Value, current) : (null, Guid.Empty, current);
    }

    private static IResult WorkspaceChanged() =>
        ApiEndpointResults.Conflict("The publisher workspace changed. Refresh the catalog.", "shared-agent-templates.workspace-changed");
}
