namespace CanDoItAll.Modules.Workbench;

internal static class ProjectAssetFileNamePolicy
{
    public const int MaximumFileNameCharacters = 260;

    private static readonly char[] InvalidFileNameCharacters =
    [
        '<',
        '>',
        ':',
        '"',
        '/',
        '\\',
        '|',
        '?',
        '*'
    ];

    public static string NormalizeLeafName(string? fileName)
    {
        var normalized = fileName?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw InvalidFileName("A file name is required.");
        }

        if (normalized.Length > MaximumFileNameCharacters)
        {
            throw InvalidFileName($"File names are limited to {MaximumFileNameCharacters} characters.");
        }

        if (normalized is "." or ".." ||
            normalized.EndsWith(".", StringComparison.Ordinal) ||
            normalized.IndexOfAny(InvalidFileNameCharacters) >= 0 ||
            normalized.Any(char.IsControl))
        {
            throw InvalidFileName("The file name must be a safe leaf name without path or control characters.");
        }

        return normalized;
    }

    private static ProjectAssetCreationException InvalidFileName(string message)
        => new(ProjectAssetCreationErrorCode.InvalidFileName, message);
}
