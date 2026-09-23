using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed record ApiPermissionMetadata(string Scope);

internal static class ApiSectionAuthorization {
    public static RouteGroupBuilder WithApiSection(this RouteGroupBuilder group, string readScope, string writeScope) {
        var secure = ((IEndpointRouteBuilder)group).ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value.Authorization.Enabled;
        ((IEndpointConventionBuilder)group).Finally(endpoint => {
            if (endpoint.Metadata.OfType<IAllowAnonymous>().Any() ||
                endpoint.Metadata.OfType<IAuthorizeData>().Any(data => !string.IsNullOrEmpty(data.Policy))) {
                return;
            }
            var explicitPermission = endpoint.Metadata.OfType<ApiPermissionMetadata>().LastOrDefault();
            var methods = endpoint.Metadata.OfType<HttpMethodMetadata>().LastOrDefault()?.HttpMethods;
            var scope = explicitPermission?.Scope ?? (methods?.All(method => HttpMethods.IsGet(method) || HttpMethods.IsHead(method)) == true ? readScope : writeScope);
            if (explicitPermission is null) {
                endpoint.Metadata.Add(new ApiPermissionMetadata(scope));
            }
            if (secure) {
                endpoint.Metadata.Add(new AuthorizeAttribute(scope));
            }
        });
        return group;
    }

    public static RouteHandlerBuilder WithApiPermission(this RouteHandlerBuilder endpoint, string scope) =>
        endpoint.WithMetadata(new ApiPermissionMetadata(scope));
}
