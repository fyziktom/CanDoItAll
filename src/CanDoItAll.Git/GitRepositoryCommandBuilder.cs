namespace CanDoItAll.Git;

public enum GitDiffOutputMode
{
    Full,
    Stat,
    NameOnly
}

public sealed record GitDiffOptions(
    GitDiffOutputMode OutputMode = GitDiffOutputMode.Full,
    GitPathSpec? Path = null);

public sealed class GitRepositoryCommandBuilder
{
    private readonly GitRepositoryPath repositoryPath;

    public GitRepositoryCommandBuilder(GitRepositoryPath repositoryPath)
    {
        this.repositoryPath = repositoryPath;
    }

    public GitCommandSpec Status(bool includeBranch = false)
    {
        var arguments = new List<GitCommandArgument>
        {
            new("status"),
            new("--short")
        };

        if (includeBranch)
        {
            arguments.Add(new GitCommandArgument("--branch"));
        }

        return Create(arguments);
    }

    public GitCommandSpec Diff(GitDiffOptions? options = null)
    {
        options ??= new GitDiffOptions();

        var arguments = new List<GitCommandArgument>
        {
            new("diff")
        };

        AppendDiffOutputMode(arguments, options.OutputMode);

        if (options.Path is not null)
        {
            arguments.Add(new GitCommandArgument("--"));
            arguments.Add(new GitCommandArgument(options.Path.RepositoryRelativePath));
        }

        return Create(arguments);
    }

    public GitCommandSpec Add(IReadOnlyList<GitPathSpec> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            throw new ArgumentException("At least one path is required.", nameof(paths));
        }

        var arguments = new List<GitCommandArgument>
        {
            new("add"),
            new("--")
        };

        arguments.AddRange(paths.Select(path => new GitCommandArgument(path.RepositoryRelativePath)));
        return Create(arguments);
    }

    public GitCommandSpec Unstage(IReadOnlyList<GitPathSpec> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            throw new ArgumentException("At least one path is required.", nameof(paths));
        }

        var arguments = new List<GitCommandArgument>
        {
            new("restore"),
            new("--staged"),
            new("--")
        };

        arguments.AddRange(paths.Select(path => new GitCommandArgument(path.RepositoryRelativePath)));
        return Create(arguments);
    }

    public GitCommandSpec Commit(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Commit message cannot be empty.", nameof(message));
        }

        return Create(
            [
                new("commit"),
                new("-m"),
                new(message, IsSensitive: true)
            ]);
    }

    public GitCommandSpec CreateBranch(GitBranchName branchName)
    {
        return Create([new("branch"), new(branchName.Value)]);
    }

    public GitCommandSpec Switch(GitBranchName branchName)
    {
        return Create([new("switch"), new(branchName.Value)]);
    }

    public GitCommandSpec Merge(GitBranchName branchName)
    {
        return Create([new("merge"), new("--no-ff"), new(branchName.Value)]);
    }

    public GitCommandSpec AbortMerge()
    {
        return Create([new("merge"), new("--abort")]);
    }

    public GitCommandSpec ListConflicts()
    {
        return Create([new("diff"), new("--name-only"), new("--diff-filter=U")]);
    }

    public GitCommandSpec Log(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Log count must be positive.");
        }

        return Create([new("log"), new($"-{count}"), new("--oneline")]);
    }

    public GitCommandSpec Show(GitRevision revision)
    {
        return Create([new("show"), new("--stat"), new(revision.Value)]);
    }

    private static void AppendDiffOutputMode(
        List<GitCommandArgument> arguments,
        GitDiffOutputMode outputMode)
    {
        switch (outputMode)
        {
            case GitDiffOutputMode.Full:
                return;
            case GitDiffOutputMode.Stat:
                arguments.Add(new GitCommandArgument("--stat"));
                return;
            case GitDiffOutputMode.NameOnly:
                arguments.Add(new GitCommandArgument("--name-only"));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(outputMode), outputMode, "Unsupported git diff output mode.");
        }
    }

    private GitCommandSpec Create(IReadOnlyList<GitCommandArgument> arguments)
    {
        return new GitCommandSpec(repositoryPath, arguments);
    }
}
