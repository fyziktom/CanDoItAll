using System.Net;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Web;

internal static class DevelopmentEndpointAccess
{
    public const string OriginalRemoteIpItemKey =
        "CanDoItAll.DevelopmentEndpointAccess.OriginalRemoteIp";

    public static RouteHandlerBuilder RequireLocalOrAuthorizedDevelopmentAccess(
        this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var originalRemoteIp = httpContext.Items[OriginalRemoteIpItemKey] as IPAddress
                ?? httpContext.Connection.RemoteIpAddress;
            var isLocal = IsAnonymousLocalAccessAllowed(
                originalRemoteIp,
                httpContext.Connection.RemoteIpAddress);
            var isAuthorizedOperator = httpContext.User.Identity?.IsAuthenticated == true &&
                                       ApiAuthorizationPolicies.HasScope(
                                           httpContext.User,
                                           ApiAccessScopeNames.IssueTokens);
            return isLocal || isAuthorizedOperator
                ? await next(context)
                : Results.NotFound();
        });
    }

    internal static bool IsAnonymousLocalAccessAllowed(
        IPAddress? originalRemoteIp,
        IPAddress? effectiveRemoteIp)
        => originalRemoteIp is not null &&
           effectiveRemoteIp is not null &&
           IPAddress.IsLoopback(originalRemoteIp) &&
           IPAddress.IsLoopback(effectiveRemoteIp);
}
