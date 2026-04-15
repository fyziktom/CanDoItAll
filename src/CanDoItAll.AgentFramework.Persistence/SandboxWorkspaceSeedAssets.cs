using System.Reflection;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class SandboxWorkspaceSeedAssets
{
    private const string ManifestResourceName = "CanDoItAll.AgentFramework.Persistence.SeedAssets.manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<SandboxWorkspaceSeedAssets> CurrentValue = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IReadOnlyDictionary<string, string> skillRoots;
    private readonly IReadOnlyDictionary<string, string> textAssets;

    private SandboxWorkspaceSeedAssets(
        IReadOnlyDictionary<string, string> skillRoots,
        IReadOnlyDictionary<string, string> textAssets)
    {
        this.skillRoots = skillRoots;
        this.textAssets = textAssets;
    }

    public static SandboxWorkspaceSeedAssets Current => CurrentValue.Value;

    public string GetSkillRoot(string key)
    {
        if (!skillRoots.TryGetValue(key, out var skillRoot))
        {
            throw new InvalidOperationException($"Seed skill root '{key}' is not defined.");
        }

        return skillRoot;
    }

    public string GetText(string key)
    {
        if (!textAssets.TryGetValue(key, out var text))
        {
            throw new InvalidOperationException($"Seed text asset '{key}' is not defined.");
        }

        return text;
    }

    private static SandboxWorkspaceSeedAssets Load()
    {
        using var stream = OpenRequiredResource(ManifestResourceName);
        var manifest = JsonSerializer.Deserialize<SeedAssetManifest>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Seed asset manifest could not be deserialized.");
        ValidateManifest(
            manifest,
            resourceName => Assembly.GetExecutingAssembly().GetManifestResourceInfo(resourceName) is not null);

        var skillRoots = manifest.SkillRoots?
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var textAssets = manifest.TextAssets?
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => ReadRequiredTextAsset(item.Value), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new SandboxWorkspaceSeedAssets(skillRoots, textAssets);
    }

    internal static void ValidateManifestJson(string manifestJson, Func<string, bool> resourceExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);
        ArgumentNullException.ThrowIfNull(resourceExists);

        var manifest = JsonSerializer.Deserialize<SeedAssetManifest>(manifestJson, SerializerOptions)
            ?? throw new InvalidOperationException("Seed asset manifest could not be deserialized.");
        ValidateManifest(manifest, resourceExists);
    }

    private static string ReadRequiredTextAsset(string relativePath)
    {
        using var stream = OpenRequiredResource(ToResourceName(relativePath));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Stream OpenRequiredResource(string resourceName)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded seed resource '{resourceName}' was not found.");
    }

    private static string ToResourceName(string relativePath)
    {
        var normalizedPath = relativePath
            .Replace('\\', '.')
            .Replace('/', '.');
        return $"CanDoItAll.AgentFramework.Persistence.SeedAssets.{normalizedPath}";
    }

    private static void ValidateManifest(
        SeedAssetManifest manifest,
        Func<string, bool> resourceExists)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(resourceExists);

        foreach (var textAsset in manifest.TextAssets ?? [])
        {
            if (string.IsNullOrWhiteSpace(textAsset.Key) || string.IsNullOrWhiteSpace(textAsset.Value))
            {
                continue;
            }

            var resourceName = ToResourceName(textAsset.Value);
            if (!resourceExists(resourceName))
            {
                throw new InvalidOperationException(
                    $"Seed text asset '{textAsset.Key}' references missing embedded resource '{resourceName}'.");
            }
        }
    }

    private sealed class SeedAssetManifest
    {
        public Dictionary<string, string>? SkillRoots { get; set; }

        public Dictionary<string, string>? TextAssets { get; set; }
    }
}
