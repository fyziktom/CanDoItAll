namespace CanDoItAll.Git;

public sealed class GitRepositoryClient
{
    private readonly GitRepositoryPath repositoryPath;
    private readonly IGitCommandExecutor executor;

    public GitRepositoryClient(
        GitRepositoryPath repositoryPath,
        IGitCommandExecutor executor)
    {
        this.repositoryPath = repositoryPath;
        this.executor = executor;
    }

    public Task<GitCommandResult> StatusAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync([new("status"), new("--short")], cancellationToken);
    }

    public Task<GitCommandResult> DiffAsync(
        GitPathSpec? path = null,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<GitCommandArgument>
        {
            new("diff"),
            new("--")
        };

        if (path is not null)
        {
            arguments.Add(new GitCommandArgument(path.RepositoryRelativePath));
        }

        return ExecuteAsync(arguments, cancellationToken);
    }

    public Task<GitCommandResult> AddAsync(
        IReadOnlyList<GitPathSpec> paths,
        CancellationToken cancellationToken = default)
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
        return ExecuteAsync(arguments, cancellationToken);
    }

    public Task<GitCommandResult> CommitAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Commit message cannot be empty.", nameof(message));
        }

        return ExecuteAsync(
            [
                new("commit"),
                new("-m"),
                new(message, IsSensitive: true)
            ],
            cancellationToken);
    }

    public Task<GitCommandResult> CreateBranchAsync(
        GitBranchName branchName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync([new("branch"), new(branchName.Value)], cancellationToken);
    }

    public Task<GitCommandResult> SwitchAsync(
        GitBranchName branchName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync([new("switch"), new(branchName.Value)], cancellationToken);
    }

    public Task<GitCommandResult> MergeAsync(
        GitBranchName branchName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync([new("merge"), new("--no-ff"), new(branchName.Value)], cancellationToken);
    }

    public Task<GitCommandResult> AbortMergeAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync([new("merge"), new("--abort")], cancellationToken);
    }

    public Task<GitCommandResult> ListConflictsAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync([new("diff"), new("--name-only"), new("--diff-filter=U")], cancellationToken);
    }

    public Task<GitCommandResult> LogAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Log count must be positive.");
        }

        return ExecuteAsync([new("log"), new($"-{count}"), new("--oneline")], cancellationToken);
    }

    public Task<GitCommandResult> ShowAsync(
        string revision,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(revision))
        {
            throw new ArgumentException("Revision cannot be empty.", nameof(revision));
        }

        return ExecuteAsync([new("show"), new("--stat"), new(revision.Trim())], cancellationToken);
    }

    private Task<GitCommandResult> ExecuteAsync(
        IReadOnlyList<GitCommandArgument> arguments,
        CancellationToken cancellationToken)
    {
        return executor.ExecuteAsync(new GitCommandSpec(repositoryPath, arguments), cancellationToken);
    }
}
