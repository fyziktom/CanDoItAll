using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Core;

public enum GovernedScriptSideEffectMode
{
    Unspecified = 0,
    NoMutation,
    ManagedProcessArtifacts,
    ExternalArtifactDestination,
    ProductMutation
}

public sealed record GovernedScriptSideEffectManifest
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public const string ArgumentName = "sideEffectManifest";

    public int Version { get; init; } = 1;

    public GovernedScriptSideEffectMode Mode { get; init; }

    public string[] DeclaredReadPaths { get; init; } = [];

    public string[] DeclaredWritePaths { get; init; } = [];

    public string[] DeclaredChildScripts { get; init; } = [];

    public bool AllowShellDelegation { get; init; }

    public bool AllowEncodedCommands { get; init; }

    public static bool TryParse(
        string? manifestJson,
        out GovernedScriptSideEffectManifest manifest,
        out string failureMessage)
    {
        manifest = new GovernedScriptSideEffectManifest();
        failureMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            failureMessage = $"Script executions in governed non-mutating process steps must provide a `{ArgumentName}` JSON manifest.";
            return false;
        }

        try
        {
            manifest = JsonSerializer.Deserialize<GovernedScriptSideEffectManifest>(manifestJson, SerializerOptions)
                ?? new GovernedScriptSideEffectManifest();
        }
        catch (JsonException exception)
        {
            failureMessage = $"Script side-effect manifest is not valid JSON: {exception.Message}";
            return false;
        }

        manifest = manifest.Normalize();
        if (manifest.Version != 1)
        {
            failureMessage = $"Script side-effect manifest version {manifest.Version} is not supported.";
            return false;
        }

        if (manifest.Mode == GovernedScriptSideEffectMode.Unspecified)
        {
            failureMessage = "Script side-effect manifest must set a non-default mode.";
            return false;
        }

        return true;
    }

    private GovernedScriptSideEffectManifest Normalize()
    {
        return this with
        {
            DeclaredReadPaths = NormalizePaths(DeclaredReadPaths),
            DeclaredWritePaths = NormalizePaths(DeclaredWritePaths),
            DeclaredChildScripts = NormalizePaths(DeclaredChildScripts)
        };
    }

    private static string[] NormalizePaths(IEnumerable<string>? paths)
    {
        return paths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
