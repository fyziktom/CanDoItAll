namespace CanDoItAll.Git;

public readonly record struct GitRepositoryPath
{
    public GitRepositoryPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Git repository path cannot be empty.", nameof(value));
        }

        Value = Path.GetFullPath(value);
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct GitBranchName
{
    public GitBranchName(string value)
    {
        Value = GitReferenceValue.Normalize(value, "Git branch name");
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct GitRevision
{
    public GitRevision(string value)
    {
        Value = GitReferenceValue.Normalize(value, "Git revision");
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record GitPathSpec
{
    public GitPathSpec(string repositoryRelativePath, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryRelativePath))
        {
            throw new ArgumentException("Git path cannot be empty.", nameof(repositoryRelativePath));
        }

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("Git full path cannot be empty.", nameof(fullPath));
        }

        var candidateRelativePath = repositoryRelativePath.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(candidateRelativePath) ||
            candidateRelativePath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Git path must be repository-relative.", nameof(repositoryRelativePath));
        }

        var normalizedRelativePath = candidateRelativePath.Trim('/');
        if (GitPathRules.IsForbiddenRepositoryRelativePath(normalizedRelativePath))
        {
            throw new ArgumentException("Git path is not an allowed repository-relative path.", nameof(repositoryRelativePath));
        }

        RepositoryRelativePath = normalizedRelativePath;
        FullPath = Path.GetFullPath(fullPath);
    }

    public string RepositoryRelativePath { get; }

    public string FullPath { get; }
}

public sealed record GitPathAuthorizationResult(
    bool IsAuthorized,
    GitPathSpec? Path,
    string? ErrorCode,
    string? ErrorMessage);

public static class GitPathAuthorizer
{
    public static GitPathAuthorizationResult Authorize(GitRepositoryPath repositoryPath, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return Denied("GitPath.Empty", "Git path cannot be empty.");
        }

        var root = EnsureTrailingSeparator(repositoryPath.Value);
        var fullPath = Path.IsPathRooted(candidatePath)
            ? Path.GetFullPath(candidatePath)
            : Path.GetFullPath(Path.Combine(repositoryPath.Value, candidatePath));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, repositoryPath.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Denied("GitPath.OutsideRepository", "Git path must stay inside the authorized repository root.");
        }

        var relativePath = Path.GetRelativePath(repositoryPath.Value, fullPath).Replace('\\', '/');
        if (GitPathRules.IsForbiddenRepositoryRelativePath(relativePath))
        {
            return Denied("GitPath.ForbiddenPath", "Git path is not an allowed repository-relative path.");
        }

        return new GitPathAuthorizationResult(
            true,
            new GitPathSpec(relativePath, fullPath),
            null,
            null);
    }

    private static GitPathAuthorizationResult Denied(string code, string message)
    {
        return new GitPathAuthorizationResult(false, null, code, message);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}

internal static class GitPathRules
{
    public static bool IsForbiddenRepositoryRelativePath(string relativePath)
    {
        return relativePath == "." ||
            relativePath.StartsWith("../", StringComparison.Ordinal) ||
            relativePath.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class GitReferenceValue
{
    public static string Normalize(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{description} cannot be empty.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{description} cannot start with '-'.", nameof(value));
        }

        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"{description} cannot contain whitespace.", nameof(value));
        }

        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException($"{description} cannot contain control characters.", nameof(value));
        }

        return normalized;
    }
}
