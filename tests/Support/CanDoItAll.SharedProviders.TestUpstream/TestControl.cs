namespace CanDoItAll.SharedProviders.TestUpstream;

public enum FixtureFailureMode
{
    None,
    BadRequest,
    Unauthorized,
    RateLimited,
    InternalServerError,
    Timeout
}

public enum FixtureStreamMode
{
    Complete,
    HoldAfterFirstFrame
}

public enum FixtureSurface
{
    All,
    Models,
    ChatCompletions,
    Responses,
    ImageGenerations,
    ComfyUiSystemStats,
    ComfyUiPrompt,
    ComfyUiHistory,
    ComfyUiView
}

public sealed record TestControlRequest(
    FixtureFailureMode FailureMode,
    FixtureSurface Surface = FixtureSurface.All,
    FixtureStreamMode StreamMode = FixtureStreamMode.Complete);

public sealed record TestControlSnapshot(
    FixtureFailureMode FailureMode,
    FixtureSurface Surface,
    FixtureStreamMode StreamMode)
{
    public FixtureStreamMode ResolveStreamMode(FixtureSurface surface)
        => Surface is FixtureSurface.All || Surface == surface
            ? StreamMode
            : FixtureStreamMode.Complete;
}

internal sealed class TestControlState
{
    private readonly object sync = new();
    private TestControlSnapshot snapshot = Default;

    public static TestControlSnapshot Default { get; } = new(
        FixtureFailureMode.None,
        FixtureSurface.All,
        FixtureStreamMode.Complete);

    public TestControlSnapshot Get()
    {
        lock (sync)
        {
            return snapshot;
        }
    }

    public TestControlSnapshot Set(TestControlRequest request)
    {
        var updated = new TestControlSnapshot(
            request.FailureMode,
            request.Surface,
            request.StreamMode);
        lock (sync)
        {
            snapshot = updated;
        }

        return updated;
    }

    public TestControlSnapshot Reset()
    {
        lock (sync)
        {
            snapshot = Default;
            return snapshot;
        }
    }

}

internal static class TestFailureResponder
{
    public static ValueTask<bool> TryWriteAsync(
        HttpContext context,
        TestControlState state,
        FixtureSurface surface)
        => TryWriteAsync(context, state.Get(), surface);

    public static async ValueTask<bool> TryWriteAsync(
        HttpContext context,
        TestControlSnapshot control,
        FixtureSurface surface)
    {
        if (control.FailureMode == FixtureFailureMode.None ||
            control.Surface is not FixtureSurface.All && control.Surface != surface)
        {
            return false;
        }

        if (control.FailureMode == FixtureFailureMode.Timeout)
        {
            try
            {
                await Task.Delay(FixtureLimits.MaximumControlledTimeout, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                new FixtureErrorEnvelope(new FixtureError(
                    "The deterministic upstream timeout ceiling elapsed.",
                    "fixture_timeout",
                    "fixture_timeout")),
                FixtureJson.Options,
                context.RequestAborted);
            return true;
        }

        var (statusCode, type, code, message) = control.FailureMode switch
        {
            FixtureFailureMode.BadRequest => (
                StatusCodes.Status400BadRequest,
                "fixture_bad_request",
                "fixture_bad_request",
                "The deterministic upstream rejected the request."),
            FixtureFailureMode.Unauthorized => (
                StatusCodes.Status401Unauthorized,
                "fixture_unauthorized",
                "fixture_unauthorized",
                "The deterministic upstream rejected the credential."),
            FixtureFailureMode.RateLimited => (
                StatusCodes.Status429TooManyRequests,
                "fixture_rate_limited",
                "fixture_rate_limited",
                "The deterministic upstream rate limit was reached."),
            FixtureFailureMode.InternalServerError => (
                StatusCodes.Status500InternalServerError,
                "fixture_internal_error",
                "fixture_internal_error",
                "The deterministic upstream failed the request."),
            _ => throw new InvalidOperationException(
                $"Unsupported fixture failure mode '{control.FailureMode}'.")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers["x-request-id"] = "fixture-failure-request";
        if (statusCode == StatusCodes.Status429TooManyRequests)
        {
            context.Response.Headers.RetryAfter = "2";
        }

        await context.Response.WriteAsJsonAsync(
            new FixtureErrorEnvelope(new FixtureError(message, type, code)),
            FixtureJson.Options,
            context.RequestAborted);
        return true;
    }
}

internal static class TestControlEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTestControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/_test");
        group.MapGet("/control", (TestControlState state) => TypedResults.Ok(state.Get()));
        group.MapPut("/control", (TestControlRequest request, TestControlState state) =>
        {
            var errors = Validate(request);
            return errors.Count == 0
                ? Results.Ok(state.Set(request))
                : Results.ValidationProblem(errors);
        });
        group.MapDelete("/control", (TestControlState state) => TypedResults.Ok(state.Reset()));
        group.MapGet("/captures", (RequestCaptureStore store) => TypedResults.Ok(store.GetSnapshot()));
        group.MapDelete("/captures", (RequestCaptureStore store) =>
            TypedResults.Ok(new CaptureResetResponse(store.Reset())));
        return endpoints;
    }

    private static Dictionary<string, string[]> Validate(TestControlRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!Enum.IsDefined(request.FailureMode))
        {
            errors[nameof(request.FailureMode)] = ["The failure mode is invalid."];
        }

        if (!Enum.IsDefined(request.Surface))
        {
            errors[nameof(request.Surface)] = ["The fixture surface is invalid."];
        }

        if (!Enum.IsDefined(request.StreamMode))
        {
            errors[nameof(request.StreamMode)] = ["The stream mode is invalid."];
        }
        else if (request.StreamMode == FixtureStreamMode.HoldAfterFirstFrame &&
            request.Surface is not (FixtureSurface.ChatCompletions or FixtureSurface.Responses))
        {
            errors[nameof(request.StreamMode)] =
                ["The hold stream mode requires a streaming OpenAI-compatible surface."];
        }
        else if (request.StreamMode == FixtureStreamMode.HoldAfterFirstFrame &&
            request.FailureMode != FixtureFailureMode.None)
        {
            errors[nameof(request.FailureMode)] =
                ["The hold stream mode cannot be combined with a failure mode."];
        }

        return errors;
    }
}
