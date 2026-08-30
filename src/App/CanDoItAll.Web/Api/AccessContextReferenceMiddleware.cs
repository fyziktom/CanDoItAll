using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Web.Api;

internal sealed class AccessContextReferenceMiddleware(RequestDelegate next)
{
    internal const string InvalidAccessContextErrorCode = "api.access-context-invalid";

    public async Task InvokeAsync(
        HttpContext httpContext,
        AccessContextReferenceState state)
    {
        var hasReference = httpContext.Request.Headers.TryGetValue(
            SharedProviderHeaders.AccessContextReference,
            out var referenceValues);
        var hasType = httpContext.Request.Headers.TryGetValue(
            SharedProviderHeaders.AccessContextReferenceType,
            out var typeValues);
        if (!hasReference && !hasType)
        {
            await next(httpContext);
            return;
        }

        string? rawReference = referenceValues.Count == 1
            ? referenceValues[0]
            : null;
        if (!AccessContextReference.TryParse(rawReference, out var reference))
        {
            var message = hasReference
                ? $"{SharedProviderHeaders.AccessContextReference} must contain exactly one value matching [A-Za-z0-9._~:-] with 1 to {AccessContextReference.MaximumLength} characters."
                : $"{SharedProviderHeaders.AccessContextReferenceType} requires {SharedProviderHeaders.AccessContextReference}.";
            await WriteInvalidAccessContextAsync(
                httpContext,
                SharedProviderHeaders.AccessContextReference,
                message);
            return;
        }

        AccessContextReferenceType? type = null;
        if (hasType)
        {
            string? rawType = typeValues.Count == 1
                ? typeValues[0]
                : null;
            if (!AccessContextReferenceType.TryParse(rawType, out var parsedType))
            {
                await WriteInvalidAccessContextAsync(
                    httpContext,
                    SharedProviderHeaders.AccessContextReferenceType,
                    $"{SharedProviderHeaders.AccessContextReferenceType} must contain exactly one canonical lowercase value matching [a-z0-9._-] with 1 to {AccessContextReferenceType.MaximumLength} characters.");
                return;
            }

            type = parsedType;
        }

        state.Set(reference, type);
        await next(httpContext);
    }

    private static Task WriteInvalidAccessContextAsync(
        HttpContext httpContext,
        string parameter,
        string message)
    {
        return SharedProviderApiResponseWriter.WriteInvalidAccessContextAsync(
            httpContext,
            parameter,
            message);
    }
}
