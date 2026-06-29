namespace CanDoItAll.Git;

public sealed class GitRepositoryClient
{
    private readonly GitRepositoryCommandBuilder commandBuilder;
    private readonly IGitCommandExecutor executor;

    public GitRepositoryClient(
        GitRepositoryPath repositoryPath,
        IGitCommandExecutor executor)
    {
        commandBuilder = new GitRepositoryCommandBuilder(repositoryPath);
        this.executor = executor;
    }

    public Task<GitCommandResult> StatusAsync(CancellationToken cancellationToken = default)
    {
        return StatusAsync(includeBranch: false, cancellationToken);
    }

    public Task<GitCommandResult> StatusAsync(
        bool includeBranch,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Status(includeBranch), cancellationToken);
    }

    public Task<GitCommandResult> DiffAsync(
        GitPathSpec? path = null,
        CancellationToken cancellationToken = default)
    {
        return DiffAsync(new GitDiffOptions(Path: path), cancellationToken);
    }

    public Task<GitCommandResult> DiffAsync(
        GitDiffOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ExecuteAsync(commandBuilder.Diff(options), cancellationToken);
    }

    public Task<GitCommandResult> AddAsync(
        IReadOnlyList<GitPathSpec> paths,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Add(paths), cancellationToken);
    }

    public Task<GitCommandResult> UnstageAsync(
        IReadOnlyList<GitPathSpec> paths,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Unstage(paths), cancellationToken);
    }

    public Task<GitCommandResult> CommitAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Commit(message), cancellationToken);
    }

    public Task<GitCommandResult> CreateBranchAsync(
        GitBranchName branchName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.CreateBranch(branchName), cancellationToken);
    }

    public Task<GitCommandResult> SwitchAsync(
        GitBranchName branchName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Switch(branchName), cancellationToken);
    }

    public Task<GitCommandResult> MergeAsync(
        GitBranchName branchName,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Merge(branchName), cancellationToken);
    }

    public Task<GitCommandResult> AbortMergeAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.AbortMerge(), cancellationToken);
    }

    public Task<GitCommandResult> ListConflictsAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.ListConflicts(), cancellationToken);
    }

    public Task<GitCommandResult> LogAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Log(count), cancellationToken);
    }

    public Task<GitCommandResult> ShowAsync(
        string revision,
        CancellationToken cancellationToken = default)
    {
        return ShowAsync(new GitRevision(revision), cancellationToken);
    }

    public Task<GitCommandResult> ShowAsync(
        GitRevision revision,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(commandBuilder.Show(revision), cancellationToken);
    }

    private Task<GitCommandResult> ExecuteAsync(
        GitCommandSpec spec,
        CancellationToken cancellationToken)
    {
        return executor.ExecuteAsync(spec, cancellationToken);
    }
}
