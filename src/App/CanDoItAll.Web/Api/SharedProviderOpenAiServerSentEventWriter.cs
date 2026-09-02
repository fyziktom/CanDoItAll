using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace CanDoItAll.Web.Api;

internal static class SharedProviderOpenAiServerSentEventWriter
{
    private static readonly ReadOnlyMemory<byte> FrameTerminator = "\n\n"u8.ToArray();

    public static void Prepare(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        SharedProviderApiResponseWriter.ApplyInferenceHeaders(httpContext);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
        httpContext.Response.Headers[HeaderNames.Pragma] = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        httpContext.Response.ContentLength = null;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    public static async Task WriteFrameAsync(
        HttpResponse response,
        SharedProviderRelayStreamFrame frame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.EventName is { } eventName)
        {
            await response.WriteAsync($"event: {eventName}\n", cancellationToken);
        }

        await response.WriteAsync($"data: {frame.Data}", cancellationToken);
        await response.Body.WriteAsync(FrameTerminator, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
