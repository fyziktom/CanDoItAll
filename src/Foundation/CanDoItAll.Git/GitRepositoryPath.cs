using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Git;

public readonly record struct GitRepositoryPath
{
    public GitRepositoryPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Git repository path cannot be empty.", nameof(value));
        }

        if (GitPhysicalPathSyntaxPolicy.IsForeign(value))
        {
            throw new ArgumentException(
                "Git repository path uses physical path syntax from another host and requires explicit rebind or migration.",
                nameof(value));
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

        if (GitPhysicalPathSyntaxPolicy.IsForeign(fullPath))
        {
            throw new ArgumentException(
                "Git full path uses physical path syntax from another host and requires explicit rebind or migration.",
                nameof(fullPath));
        }

        LogicalPath logicalPath;
        try
        {
            logicalPath = LogicalPath.ParseLegacyWindowsLogicalPath(repositoryRelativePath);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "Git path must be a valid repository-relative logical path.",
                nameof(repositoryRelativePath),
                exception);
        }

        var normalizedRelativePath = logicalPath.Value;
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
        => AuthorizeCore(repositoryPath, candidatePath, physicalPathPolicyFactory: null);

    public static GitPathAuthorizationResult Authorize(
        GitRepositoryPath repositoryPath,
        string candidatePath,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
    {
        ArgumentNullException.ThrowIfNull(physicalPathPolicyFactory);
        return AuthorizeCore(repositoryPath, candidatePath, physicalPathPolicyFactory);
    }

    private static GitPathAuthorizationResult AuthorizeCore(
        GitRepositoryPath repositoryPath,
        string candidatePath,
        IPhysicalFileSystemPathPolicyFactory? physicalPathPolicyFactory)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return Denied("GitPath.Empty", "Git path cannot be empty.");
        }

        if (GitPhysicalPathSyntaxPolicy.IsForeign(candidatePath))
        {
            return Denied(
                "GitPath.ForeignHostPath",
                "Git path uses physical path syntax from another host and requires explicit rebind or migration.");
        }

        string fullPath;
        if (Path.IsPathRooted(candidatePath))
        {
            fullPath = Path.GetFullPath(candidatePath);
        }
        else
        {
            LogicalPath logicalPath;
            try
            {
                logicalPath = LogicalPath.ParseLegacyWindowsLogicalPath(candidatePath);
            }
            catch (ArgumentException)
            {
                return Denied("GitPath.InvalidLogicalPath", "Git path is not a valid repository-relative logical path.");
            }

            fullPath = Path.GetFullPath(Path.Combine(
                repositoryPath.Value,
                Path.Combine(logicalPath.Segments.ToArray())));
        }

        if (physicalPathPolicyFactory is not null)
        {
            try
            {
                IPhysicalFileSystemPathPolicy policy = physicalPathPolicyFactory.Create(repositoryPath.Value);
                if (!policy.IsWithinRoot(fullPath))
                {
                    return Denied("GitPath.OutsideRepository", "Git path must stay inside the authorized repository root.");
                }

                policy.EnsureSafePath(fullPath, allowMissingLeaf: true);
            }
            catch (IOException)
            {
                return Denied("GitPath.UnsafePhysicalPath", "Git path cannot be validated safely on the repository filesystem.");
            }
        }
        else if (!fullPath.StartsWith(EnsureTrailingSeparator(repositoryPath.Value), StringComparison.Ordinal) &&
                 !string.Equals(fullPath, repositoryPath.Value, StringComparison.Ordinal))
        {
            return Denied("GitPath.OutsideRepository", "Git path must stay inside the authorized repository root.");
        }

        var hostRelativePath = Path.GetRelativePath(repositoryPath.Value, fullPath);
        if (string.Equals(hostRelativePath, ".", StringComparison.Ordinal))
        {
            return Denied("GitPath.ForbiddenPath", "Git path is not an allowed repository-relative path.");
        }

        var relativePath = LogicalPath.ParseLegacyWindowsLogicalPath(hostRelativePath).Value;
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

internal static class GitPhysicalPathSyntaxPolicy
{
    public static bool IsForeign(string path)
    {
        var syntax = PhysicalPathSyntaxClassifier.Classify(path);
        return syntax switch
        {
            PhysicalPathSyntax.Relative => false,
            PhysicalPathSyntax.UnixAbsolute => OperatingSystem.IsWindows(),
            PhysicalPathSyntax.WindowsDriveAbsolute or
                PhysicalPathSyntax.WindowsUnc or
                PhysicalPathSyntax.WindowsDevice => !OperatingSystem.IsWindows(),
            _ => true
        };
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
