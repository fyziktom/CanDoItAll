using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Web.Api;

internal sealed class AccessContextReferenceMiddleware(RequestDelegate next)
{
    internal const string InvalidAccessContextErrorCode = "api.access-context-invalid";

    public async Task InvokeAsync(
        HttpContext httpContext,
        AccessContextReferenceState state)
    {
        if (!httpContext.Request.Headers.TryGetValue(
                SharedProviderHeaders.AccessContextReference,
                out var values))
        {
            await next(httpContext);
            return;
        }

        string? rawValue = values.Count == 1
            ? values[0]
            : null;
        if (!AccessContextReference.TryParse(rawValue, out var reference))
        {
            await WriteInvalidAccessContextAsync(httpContext);
            return;
        }

        state.Set(reference);
        await next(httpContext);
    }

    private static Task WriteInvalidAccessContextAsync(HttpContext httpContext)
    {
        return SharedProviderApiResponseWriter.WriteInvalidAccessContextAsync(
            httpContext,
            $"{SharedProviderHeaders.AccessContextReference} must contain exactly one value matching [A-Za-z0-9._~:-] with 1 to {AccessContextReference.MaximumLength} characters.");
    }
}
