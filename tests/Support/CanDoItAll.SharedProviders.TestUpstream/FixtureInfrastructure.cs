using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedProviders.TestUpstream;

internal static class FixtureLimits
{
    public const long MaximumRequestBodyBytes = 1_048_576;
    public const int MaximumCapturedBodyBytes = 65_536;
    public const int MaximumCaptures = 128;
    public const int MaximumComfyUiPrompts = 128;
    public const long MaximumConcurrentConnections = 64;
    public static readonly TimeSpan StreamChunkDelay = TimeSpan.FromMilliseconds(75);
    public static readonly TimeSpan MaximumControlledTimeout = TimeSpan.FromSeconds(60);
}

internal static class FixtureJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.SnakeCaseLower,
            allowIntegerValues: false));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}

public sealed record FixtureHealthResponse(string Status);

public sealed record FixtureError(string Message, string Type, string Code);

public sealed record FixtureErrorEnvelope(FixtureError Error);
