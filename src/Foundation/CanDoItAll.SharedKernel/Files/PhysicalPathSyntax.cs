namespace CanDoItAll.SharedKernel;

public enum PhysicalPathSyntax
{
    Relative,
    UnixAbsolute,
    WindowsDriveAbsolute,
    WindowsDriveRelative,
    WindowsUnc,
    WindowsDevice,
    Uri
}

public static class PhysicalPathSyntaxClassifier
{
    public static PhysicalPathSyntax Classify(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (LooksLikeUri(path))
        {
            return PhysicalPathSyntax.Uri;
        }

        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            path.StartsWith("//?/", StringComparison.Ordinal) ||
            path.StartsWith("//./", StringComparison.Ordinal))
        {
            return PhysicalPathSyntax.WindowsDevice;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
        {
            return PhysicalPathSyntax.WindowsUnc;
        }

        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            return path.Length >= 3 && path[2] is '\\' or '/'
                ? PhysicalPathSyntax.WindowsDriveAbsolute
                : PhysicalPathSyntax.WindowsDriveRelative;
        }

        return path.Length > 0 && path[0] == '/'
            ? PhysicalPathSyntax.UnixAbsolute
            : PhysicalPathSyntax.Relative;
    }

    private static bool LooksLikeUri(string value)
    {
        var separatorIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (separatorIndex <= 0 || !char.IsAsciiLetter(value[0]))
        {
            return false;
        }

        return value[..separatorIndex]
            .Skip(1)
            .All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.');
    }
}
