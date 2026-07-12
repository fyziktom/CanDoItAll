using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed class EfMemoryRetentionProjectionStore : IMemoryRetentionProjectionStore
{
    private readonly EfMemoryRetentionCandidateReader candidateReader;
    private readonly EfMemoryRetentionApplier retentionApplier;

    public EfMemoryRetentionProjectionStore(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        candidateReader = new EfMemoryRetentionCandidateReader(dbContextFactory);
        retentionApplier = new EfMemoryRetentionApplier(dbContextFactory);
    }

    public Task<IReadOnlyList<MemoryRetentionCandidate>> ListDueAsync(
        DateTimeOffset nowUtc,
        int take,
        CancellationToken cancellationToken = default) =>
        candidateReader.ListDueAsync(nowUtc, take, cancellationToken);

    public Task<MemoryRetentionApplicationResult> ApplyAsync(
        MemoryRetentionCandidate candidate,
        DateTimeOffset appliedAtUtc,
        string reason,
        CancellationToken cancellationToken = default) =>
        retentionApplier.ApplyAsync(candidate, appliedAtUtc, reason, cancellationToken);
}
