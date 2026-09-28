using System.Net;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

public sealed class ApiTransportMiddleware(RequestDelegate next, ILogger<ApiTransportMiddleware> logger) {
    public static bool IsApiRequest(HttpContext context) => context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/authorized-files");

    public async Task InvokeAsync(HttpContext context, IOptions<ApiAccessOptions> options) {
        if (!IsApiRequest(context)) {
            await next(context);
            return;
        }
        if (context.Request.Path.StartsWithSegments("/api/access")) {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size) {
                size.MaxRequestBodySize = 16384;
            }
        }
        if (options.Value.UserAuthentication.Enabled && !context.Request.IsHttps && !IsPermittedLoopback(context, options.Value)) {
            await ApiAccessEndpoints.Error(400, "HTTPS is required for API credentials.", "api.https-required").ExecuteAsync(context);
            return;
        }
        try {
            await next(context);
            if (!context.Response.HasStarted && context.Response.StatusCode >= 400 && string.IsNullOrEmpty(context.Response.ContentType)) {
                await ApiAccessEndpoints.Error(context.Response.StatusCode, "The API request could not be completed.", "api.request-failed").ExecuteAsync(context);
            }
        } catch (BadHttpRequestException exception) when (!context.Response.HasStarted) {
            await ApiAccessEndpoints.Error(exception.StatusCode, "The API request is malformed or too large.", "api.request-invalid").ExecuteAsync(context);
        } catch (Exception exception) when (exception is not OperationCanceledException && !context.Response.HasStarted) {
            logger.LogError("API request {TraceId} failed: {ErrorType}.", context.TraceIdentifier, exception.GetType().Name);
            await ApiAccessEndpoints.Error(StatusCodes.Status500InternalServerError, "The API operation failed.", "api.internal-error").ExecuteAsync(context);
        }
    }

    private static bool IsPermittedLoopback(HttpContext context, ApiAccessOptions options) {
        var original = context.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] as IPAddress ?? context.Connection.RemoteIpAddress;
        return options.UserAuthentication.AllowLoopbackHttp && original is not null && IPAddress.IsLoopback(original) &&
            context.Connection.RemoteIpAddress is { } effective && IPAddress.IsLoopback(effective) &&
            !context.Request.Headers.ContainsKey("X-Forwarded-For") && !context.Request.Headers.ContainsKey("X-Forwarded-Proto") &&
            !context.Request.Headers.ContainsKey("Forwarded") && !context.Request.Headers.ContainsKey("X-Forwarded-Host") &&
            !context.Request.Headers.ContainsKey("X-Original-For") && !context.Request.Headers.ContainsKey("X-Original-Proto");
    }
}
