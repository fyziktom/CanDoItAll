namespace CanDoItAll.Components.BaseLib;

public static class AvatarFallbackResolver
{
    private const string AssetRoot = "_content/CanDoItAll.Components.BaseLib/assets/identity/avatars";

    private static readonly string[] DefaultAssetFileNames =
    [
        "avatar-01.jpg",
        "avatar-02.jpg",
        "avatar-03.jpg",
        "avatar-04.jpg",
        "avatar-05.jpg",
        "avatar-06.jpg",
        "avatar-07.jpg",
        "avatar-08.jpg"
    ];

    public static IReadOnlyList<string> BundledAvatarFileNames => DefaultAssetFileNames;

    public static string? ResolveImageUrl(string? explicitImageUrl, string? defaultImageSeed, bool useDefaultImageWhenMissing)
    {
        if (!string.IsNullOrWhiteSpace(explicitImageUrl))
        {
            return explicitImageUrl.Trim();
        }

        if (!useDefaultImageWhenMissing)
        {
            return null;
        }

        return BuildBundledAssetUrl(defaultImageSeed);
    }

    public static string BuildBundledAssetUrl(string? defaultImageSeed)
    {
        return $"{AssetRoot}/{ResolveFileName(defaultImageSeed)}";
    }

    public static IReadOnlyList<string> GetBundledAvatarUrls()
    {
        return DefaultAssetFileNames
            .Select(fileName => $"{AssetRoot}/{fileName}")
            .ToList();
    }

    public static string ResolveFileName(string? defaultImageSeed)
    {
        var normalizedSeed = NormalizeSeed(defaultImageSeed);
        uint hash = 2166136261;

        foreach (var character in normalizedSeed)
        {
            hash ^= character;
            hash *= 16777619;
        }

        var index = (int)(hash % (uint)DefaultAssetFileNames.Length);
        return DefaultAssetFileNames[index];
    }

    private static string NormalizeSeed(string? defaultImageSeed)
    {
        return string.IsNullOrWhiteSpace(defaultImageSeed)
            ? "default-avatar"
            : defaultImageSeed.Trim().ToUpperInvariant();
    }
}
