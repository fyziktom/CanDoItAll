using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal sealed record ApiAck(bool Ok);

internal sealed record ApiErrorResponse(IReadOnlyList<ApiErrorItem> Errors);

internal sealed record ApiErrorItem(string Code, string Message, ErrorSeverity Severity);

internal static class ApiEndpointMetadata
{
    public static RouteHandlerBuilder ProducesApiErrors(
        this RouteHandlerBuilder builder,
        params int[] statusCodes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(statusCodes);

        foreach (var statusCode in statusCodes.Distinct())
        {
            builder.Produces<ApiErrorResponse>(statusCode);
        }

        return builder;
    }
}

internal static class ApiEndpointResults
{
    public static IResult FromResult(Result result)
    {
        return result.IsSuccess
            ? Results.Ok(new ApiAck(true))
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult FromResult<T>(Result<T> result)
    {
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult FromResult(Result result, params string[] notFoundCodes)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(new ApiAck(true));
        }

        return ContainsCode(result.Errors, notFoundCodes)
            ? Results.NotFound(MapErrors(result.Errors))
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult FromResult<T>(Result<T> result, params string[] notFoundCodes)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ContainsCode(result.Errors, notFoundCodes)
            ? Results.NotFound(MapErrors(result.Errors))
            : Results.BadRequest(MapErrors(result.Errors));
    }

    public static IResult NotFound(string message, string code)
    {
        return Results.NotFound(MapErrors([Error.Validation(message, code)]));
    }

    public static IResult BadRequest(string message, string code)
    {
        return Results.BadRequest(MapErrors([Error.Validation(message, code)]));
    }

    public static IResult Conflict(string message, string code)
    {
        return Results.Conflict(MapErrors([Error.Failure(message, code)]));
    }

    public static IResult Unauthorized(string message, string code)
    {
        return Results.Json(
            MapErrors([Error.Validation(message, code)]),
            statusCode: StatusCodes.Status401Unauthorized);
    }

    public static IResult Forbidden(string message, string code)
    {
        return Results.Json(
            MapErrors([Error.Validation(message, code)]),
            statusCode: StatusCodes.Status403Forbidden);
    }

    public static IResult FromException(Exception exception)
    {
        return Results.BadRequest(MapErrors([Error.Failure(exception.Message, "api.request-failed")]));
    }

    private static ApiErrorResponse MapErrors(IReadOnlyList<Error> errors)
    {
        return new ApiErrorResponse(
            errors
                .Select(error => new ApiErrorItem(error.Code, error.Message, error.Severity))
                .ToList());
    }

    private static bool ContainsCode(IReadOnlyList<Error> errors, IReadOnlyCollection<string> codes)
        => codes.Count > 0 && errors.Any(error => codes.Contains(error.Code, StringComparer.Ordinal));
}
