using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Threading.Channels;

namespace CanDoItAll.Infrastructure.BackgroundJobs;

public sealed record BackgroundJobRequest(
    string JobType,
    Guid CorrelationId,
    string Description,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(BackgroundJobRequest job, CancellationToken cancellationToken = default);

    ValueTask<BackgroundJobRequest> DequeueAsync(CancellationToken cancellationToken = default);
}

public enum BackgroundJobState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public sealed class BackgroundJobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string JobType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string State { get; set; } = BackgroundJobState.Queued.ToString();

    public string MetadataJson { get; set; } = "{}";

    public string? ErrorSummary { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class BackgroundJobRecordConfiguration : IEntityTypeConfiguration<BackgroundJobRecord>
{
    public void Configure(EntityTypeBuilder<BackgroundJobRecord> builder)
    {
        builder.ToTable("Infrastructure_BackgroundJobRecords");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.JobType).HasMaxLength(120).IsRequired();
        builder.Property(job => job.Description).HasMaxLength(300).IsRequired();
        builder.Property(job => job.State).HasMaxLength(40).IsRequired();
        builder.Property(job => job.MetadataJson).HasColumnType("TEXT");
        builder.Property(job => job.ErrorSummary).HasColumnType("TEXT");
    }
}

public sealed record BackgroundJobSummary(
    Guid Id,
    string JobType,
    string Description,
    BackgroundJobState State,
    Guid CorrelationId,
    string? ErrorSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public interface IBackgroundJobTracker
{
    Task<Guid> EnqueueTrackedAsync(
        string jobType,
        string description,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task MarkQueuedAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkRunningAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkSucceededAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid id, string errorSummary, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackgroundJobSummary>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<BackgroundJobRequest> _channel = Channel.CreateUnbounded<BackgroundJobRequest>();

    public ValueTask EnqueueAsync(BackgroundJobRequest job, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(job, cancellationToken);

    public ValueTask<BackgroundJobRequest> DequeueAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken);
}

/* codex-capsule
kind: service
name: BackgroundJobTracker
summary: Tracks queued and completed background work for UI diagnostics and module-level workflows.
owns: background-job-records, queue-correlation
deps: AppDbContext, IBackgroundJobQueue, IClock
risks: stale-running-state, lost-error-summary
tests: integration:BackgroundJobTrackerTests
inputs: job requests and state transitions
outputs: BackgroundJobSummary list
*/
public sealed class BackgroundJobTracker(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IBackgroundJobQueue queue,
    IClock clock) : IBackgroundJobTracker
{
    public async Task<Guid> EnqueueTrackedAsync(
        string jobType,
        string description,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var record = new BackgroundJobRecord
        {
            JobType = jobType.Trim(),
            Description = description.Trim(),
            CorrelationId = Guid.NewGuid(),
            MetadataJson = SerializeMetadata(metadata),
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<BackgroundJobRecord>().AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(new BackgroundJobRequest(record.JobType, record.CorrelationId, record.Description, metadata), cancellationToken);
        return record.Id;
    }

    public Task MarkRunningAsync(Guid id, CancellationToken cancellationToken = default)
        => UpdateStateAsync(id, BackgroundJobState.Running, null, cancellationToken);

    public Task MarkQueuedAsync(Guid id, CancellationToken cancellationToken = default)
        => UpdateStateAsync(id, BackgroundJobState.Queued, null, cancellationToken);

    public Task MarkSucceededAsync(Guid id, CancellationToken cancellationToken = default)
        => UpdateStateAsync(id, BackgroundJobState.Succeeded, null, cancellationToken);

    public Task MarkFailedAsync(Guid id, string errorSummary, CancellationToken cancellationToken = default)
        => UpdateStateAsync(id, BackgroundJobState.Failed, errorSummary, cancellationToken);

    public async Task<IReadOnlyList<BackgroundJobSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<BackgroundJobRecord>().ToListAsync(cancellationToken);

        return records
            .OrderByDescending(job => job.UpdatedAtUtc)
            .Take(50)
            .Select(job => new BackgroundJobSummary(
                job.Id,
                job.JobType,
                job.Description,
                Enum.TryParse<BackgroundJobState>(job.State, out var state) ? state : BackgroundJobState.Queued,
                job.CorrelationId,
                job.ErrorSummary,
                job.CreatedAtUtc,
                job.UpdatedAtUtc))
            .ToList();
    }

    private async Task UpdateStateAsync(
        Guid id,
        BackgroundJobState state,
        string? errorSummary,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<BackgroundJobRecord>().FirstOrDefaultAsync(job => job.Id == id, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.State = state.ToString();
        record.ErrorSummary = errorSummary;
        record.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return "{}";
        }

        return System.Text.Json.JsonSerializer.Serialize(metadata);
    }
}
