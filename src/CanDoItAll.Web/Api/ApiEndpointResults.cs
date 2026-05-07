using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal sealed record ApiAck(bool Ok);

internal sealed record ApiErrorResponse(IReadOnlyList<ApiErrorItem> Errors);

internal sealed record ApiErrorItem(string Code, string Message, ErrorSeverity Severity);

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

    public static IResult NotFound(string message, string code)
    {
        return Results.NotFound(MapErrors([Error.Validation(message, code)]));
    }

    public static IResult BadRequest(string message, string code)
    {
        return Results.BadRequest(MapErrors([Error.Validation(message, code)]));
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
}
