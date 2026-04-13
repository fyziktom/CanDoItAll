namespace CanDoItAll.SharedKernel;

public static class AncestorFileLocator
{
    public static string? FindContainingDirectory(string relativeFilePath, params string?[] startPaths)
    {
        if (string.IsNullOrWhiteSpace(relativeFilePath))
        {
            throw new ArgumentException("A relative file path is required.", nameof(relativeFilePath));
        }

        foreach (var startPath in startPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(path => Path.GetFullPath(path!))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relativeFilePath);
                if (File.Exists(candidate))
                {
                    return Path.GetDirectoryName(candidate);
                }

                current = current.Parent;
            }
        }

        return null;
    }
}
