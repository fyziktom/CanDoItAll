namespace CanDoItAll.AgentFramework.Core;

public readonly record struct WorkspaceScriptArgumentPathCandidate(
    string OriginalArgument,
    string Prefix,
    string Path)
{
    public string ReplacePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return $"{Prefix}{path}";
    }
}

public static class WorkspaceScriptArgumentPathParser
{
    private static readonly HashSet<string> RelativePathOptionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "destination",
        "destinationdirectory",
        "destinationfile",
        "destinationfolder",
        "destinationpath",
        "directory",
        "file",
        "folder",
        "input",
        "inputdirectory",
        "inputfile",
        "inputfolder",
        "inputpath",
        "literalpath",
        "output",
        "outputdirectory",
        "outputfile",
        "outputfolder",
        "outputpath",
        "path",
        "project",
        "projectfile",
        "projectpath",
        "root",
        "script",
        "solution",
        "solutionfile",
        "solutionpath",
        "source",
        "sourcedirectory",
        "sourcefile",
        "sourcefolder",
        "sourcepath",
        "target",
        "targetdirectory",
        "targetfile",
        "targetfolder",
        "targetpath",
        "workingdirectory"
    };

    public static bool TryParse(
        string? argument,
        out WorkspaceScriptArgumentPathCandidate candidate)
    {
        candidate = default;
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        var normalizedArgument = argument.Trim();
        if (TrySplitNamedValue(
                normalizedArgument,
                out var prefix,
                out var namedValue,
                out var optionName))
        {
            return TryCreateCandidate(
                normalizedArgument,
                prefix,
                namedValue,
                IsRelativePathOption(optionName),
                out candidate);
        }

        if (TrySplitColonValue(
                normalizedArgument,
                out prefix,
                out namedValue,
                out optionName) &&
            IsRelativePathOption(optionName))
        {
            return TryCreateCandidate(
                normalizedArgument,
                prefix,
                namedValue,
                allowNamedRelativePath: true,
                out candidate);
        }

        if (TrySplitAttachedShortOption(normalizedArgument, out prefix, out namedValue))
        {
            return TryCreateCandidate(normalizedArgument, prefix, namedValue, allowNamedRelativePath: false, out candidate);
        }

        if (normalizedArgument.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        return TryCreateCandidate(
            normalizedArgument,
            string.Empty,
            normalizedArgument,
            allowNamedRelativePath: false,
            out candidate);
    }

    public static bool ContainsParentTraversal(string path)
    {
        return path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    public static bool IsExternalTargetAliasPath(string? path)
    {
        return WorkspacePathPolicy.IsExternalTargetAliasPath(path);
    }

    private static bool TryCreateCandidate(
        string originalArgument,
        string prefix,
        string path,
        bool allowNamedRelativePath,
        out WorkspaceScriptArgumentPathCandidate candidate)
    {
        candidate = default;
        var normalizedPath = path.Trim();
        if (!LooksLikePath(normalizedPath, allowNamedRelativePath))
        {
            return false;
        }

        candidate = new WorkspaceScriptArgumentPathCandidate(
            originalArgument,
            prefix,
            normalizedPath);
        return true;
    }

    private static bool LooksLikePath(string value, bool allowNamedRelativePath = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return false;
        }

        var expandedValue = WorkspacePathPolicy.ExpandPortablePath(value);
        return WorkspacePathPolicy.IsExternalTargetAliasPath(value) ||
               Path.IsPathRooted(expandedValue) ||
               string.Equals(value, ".", StringComparison.Ordinal) ||
               string.Equals(value, "..", StringComparison.Ordinal) ||
               value.StartsWith("./", StringComparison.Ordinal) ||
               value.StartsWith(@".\", StringComparison.Ordinal) ||
               value.StartsWith("../", StringComparison.Ordinal) ||
               value.StartsWith(@"..\", StringComparison.Ordinal) ||
               ContainsParentTraversal(value) ||
               allowNamedRelativePath && ContainsDirectorySeparator(value);
    }

    private static bool TrySplitNamedValue(
        string argument,
        out string prefix,
        out string value,
        out string optionName)
    {
        prefix = string.Empty;
        value = string.Empty;
        optionName = string.Empty;
        if (!argument.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = argument.IndexOf('=');
        if (separatorIndex <= 1 || separatorIndex == argument.Length - 1)
        {
            return false;
        }

        prefix = argument[..(separatorIndex + 1)];
        value = argument[(separatorIndex + 1)..];
        optionName = argument[1..separatorIndex].TrimStart('-');
        return true;
    }

    private static bool TrySplitColonValue(
        string argument,
        out string prefix,
        out string value,
        out string optionName)
    {
        prefix = string.Empty;
        value = string.Empty;
        optionName = string.Empty;
        if (!argument.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = argument.IndexOf(':', 1);
        if (separatorIndex <= 1 || separatorIndex == argument.Length - 1)
        {
            return false;
        }

        prefix = argument[..(separatorIndex + 1)];
        value = argument[(separatorIndex + 1)..];
        optionName = argument[1..separatorIndex].TrimStart('-');
        return true;
    }

    private static bool TrySplitAttachedShortOption(
        string argument,
        out string prefix,
        out string value)
    {
        prefix = string.Empty;
        value = string.Empty;
        if (!argument.StartsWith("-", StringComparison.Ordinal) ||
            argument.StartsWith("--", StringComparison.Ordinal) ||
            argument.Length < 3)
        {
            return false;
        }

        var candidateValue = argument[2..];
        if (LooksLikePath(candidateValue))
        {
            prefix = argument[..2];
            value = candidateValue;
            return true;
        }

        if (argument[2] != ':' || argument.Length == 3)
        {
            return false;
        }

        candidateValue = argument[3..];
        if (!LooksLikePath(candidateValue))
        {
            return false;
        }

        prefix = argument[..3];
        value = candidateValue;
        return true;
    }

    private static bool IsRelativePathOption(string optionName)
    {
        var normalizedName = optionName
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        return RelativePathOptionNames.Contains(normalizedName);
    }

    private static bool ContainsDirectorySeparator(string value)
        => value.Contains('/') || value.Contains('\\');
}
