using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal sealed record DevelopmentApiAck(bool Ok);

internal sealed record DevelopmentApiErrorResponse(IReadOnlyList<DevelopmentApiErrorItem> Errors);

internal sealed record DevelopmentApiErrorItem(string Code, string Message, ErrorSeverity Severity);

internal static class DevelopmentApiEndpointResults
{
    public static IResult FromResult(Result result)
    {
        return result.IsSuccess
            ? Results.Ok(new DevelopmentApiAck(true))
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
        return Results.BadRequest(MapErrors([Error.Failure(exception.Message, "development-api.request-failed")]));
    }

    private static DevelopmentApiErrorResponse MapErrors(IReadOnlyList<Error> errors)
    {
        return new DevelopmentApiErrorResponse(
            errors
                .Select(error => new DevelopmentApiErrorItem(error.Code, error.Message, error.Severity))
                .ToList());
    }
}
