using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IOfflineRepositoryStore
{
    Task UpsertRepositoryAsync(LocalRepositoryRecord repository, CancellationToken cancellationToken);
    Task UpsertRefsAsync(string repositoryId, IReadOnlyList<LocalRefRecord> refs, CancellationToken cancellationToken);
    Task UpsertCommitsAsync(string repositoryId, IReadOnlyList<LocalCommitRecord> commits, CancellationToken cancellationToken);
    Task<WorkingCopyRecord?> GetWorkingCopyAsync(string repositoryId, string branchName, CancellationToken cancellationToken);
    Task SaveWorkingCopyAsync(WorkingCopyRecord workingCopy, CancellationToken cancellationToken);
}

public sealed record LocalRepositoryRecord(string RepositoryId, string EntityType, string DefaultBranch, string? CurrentCommitHash);
public sealed record LocalRefRecord(string RepositoryId, string Scope, string Name, string? TipCommitHash);
public sealed record LocalCommitRecord(string RepositoryId, string CommitHash, string SnapshotHash, string Message);
public sealed record WorkingCopyRecord(string RepositoryId, string BranchName, string? BaseCommitHash, bool Dirty);
