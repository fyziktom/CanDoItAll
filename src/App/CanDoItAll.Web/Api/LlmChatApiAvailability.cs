using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Web.Api;

internal static class LlmChatApiAvailability {
    public static async ValueTask<object?> RequireAvailableDefinitionAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        var request = context.HttpContext;
        if (request.Request.RouteValues.TryGetValue("definitionId", out var routeId) &&
            Guid.TryParse(routeId?.ToString(), out var id) && id != Guid.Empty) {
            var publications = request.RequestServices.GetRequiredService<RemoteTemplatePublicationStore>();
            if (!await publications.IsApiAvailableAsync(RemoteTemplateKind.SimpleChat, id, request.RequestAborted)) {
                return LlmChatApiResults.UnavailableDefinition();
            }
        }
        return await next(context);
    }
}
