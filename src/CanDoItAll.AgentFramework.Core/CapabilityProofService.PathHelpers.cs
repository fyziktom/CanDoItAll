namespace CanDoItAll.AgentFramework.Core;

public sealed partial class CapabilityProofService
{
    private static async Task<string> ReadPreviewAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        var buffer = new char[512];
        var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        return new string(buffer, 0, read);
    }

    private static bool TryResolveFilePath(string value, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = ExpandPortablePath(value);

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
        {
            fullPath = absoluteUri.LocalPath;
            return true;
        }

        if (!LooksLikeFilePath(value))
        {
            return false;
        }

        fullPath = Path.GetFullPath(value);
        return true;
    }

    private static bool TryCreateUri(string value, out Uri uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri!);
    }

    private static bool LooksLikeFilePath(string value)
    {
        if (value.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            value.StartsWith("~" + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ||
            string.Equals(value, "~", StringComparison.Ordinal))
        {
            return true;
        }

        return Path.IsPathRooted(value)
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar)
            || value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpandPortablePath(string value)
    {
        var expanded = Environment.ExpandEnvironmentVariables(value.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(homeDirectory)
                ? expanded
                : homeDirectory;
        }

        if (!expanded.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !expanded.StartsWith("~" + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return expanded;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return expanded;
        }

        var relativePath = expanded[2..]
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.Combine(home, relativePath);
    }

    private static bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedRoot = Path.GetFullPath(rootPath);
        if (string.Equals(normalizedFullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) || normalizedRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedFullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
