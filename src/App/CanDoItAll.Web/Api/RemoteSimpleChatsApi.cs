using System.ComponentModel;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Web.Api;

internal static class RemoteSimpleChatsApi {
    public static void MapRemoteSimpleChats(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(RemoteSimpleChatRoutes.Catalog).WithTags("Shared Simple Chats")
            .WithMetadata(new ApiPermissionMetadata(ApiAccessScopeNames.ReadSharedSimpleChats))
            .RequireAuthorization(ApiAuthorizationPolicies.ReadSharedSimpleChats);
        group.MapGet(string.Empty, ListAsync).WithName("ListRemoteSimpleChatTemplates")
            .Produces<IReadOnlyList<RemoteTemplateRow>>().ProducesApiErrors(401, 403, 409)
            .DescribeApi("List shared Simple Chats", "List explicitly published draft and active definitions with their avatars. Requires the shared Simple Chat read scope.", "Visible Simple Chat template summaries.");
        group.MapGet("/{id:guid}", ReadAsync).WithName("GetRemoteSimpleChatTemplate")
            .Produces<RemoteSimpleChatTemplate>().ProducesApiErrors(401, 403, 404, 409)
            .DescribeApi("Read a shared Simple Chat", "Read the published definition, system instructions, model preference and output settings. Requires the shared Simple Chat read scope.", "The current portable Simple Chat revision.");
        group.MapGet("/{id:guid}/avatar", ReadAvatarAsync).WithName("GetRemoteSimpleChatAvatar")
            .Produces<RemoteTemplateEmbeddedAvatar>().ProducesApiErrors(401, 403, 404, 409)
            .DescribeApi("Read a shared Simple Chat avatar", "Read the embedded avatar of the currently shared definition with its publisher and revision.", "The bounded Simple Chat avatar.");
    }

    private static async Task<IResult> ListAsync(RemoteSimpleChatPublisher publisher, CancellationToken token) {
        try {
            return Results.Ok(await publisher.ListAsync(token));
        } catch (RemoteInstanceReadException) {
            return ApiEndpointResults.Conflict("The Simple Chat catalog changed. Refresh it.", "shared-simple-chats.workspace-changed");
        }
    }

    private static async Task<IResult> ReadAsync([Description("Publisher-local ID of an explicitly shared Simple Chat definition.")] Guid id,
        RemoteSimpleChatPublisher publisher, CancellationToken token) {
        try {
            var template = await publisher.FindAsync(id, token);
            return template is null
                ? ApiEndpointResults.NotFound("The shared Simple Chat was not found.", "shared-simple-chats.not-found")
                : Results.Ok(template);
        } catch (RemoteInstanceReadException) {
            return ApiEndpointResults.Conflict("The shared Simple Chat revision changed or is unavailable. Refresh it.", "shared-simple-chats.unavailable");
        }
    }

    private static async Task<IResult> ReadAvatarAsync([Description("Publisher-local ID of the Simple Chat with an embedded avatar.")] Guid id,
        RemoteSimpleChatPublisher publisher, CancellationToken token) {
        try {
            return await publisher.FindAvatarAsync(id, token) is { } avatar ? Results.Ok(avatar) :
                ApiEndpointResults.NotFound("The shared Simple Chat avatar was not found.", "shared-simple-chats.avatar-not-found");
        } catch (RemoteInstanceReadException) {
            return ApiEndpointResults.Conflict("The Simple Chat avatar changed. Refresh the catalog.", "shared-simple-chats.workspace-changed");
        }
    }
}
