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
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Git branch name cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record GitPathSpec(string RepositoryRelativePath, string FullPath);

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
        if (relativePath == "." || relativePath.StartsWith("../", StringComparison.Ordinal) || relativePath.StartsWith(".git/", StringComparison.Ordinal))
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
