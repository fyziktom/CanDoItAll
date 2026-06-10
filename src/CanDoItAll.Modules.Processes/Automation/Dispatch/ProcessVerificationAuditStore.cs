using CanDoItAll.Infrastructure.Logging;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessVerificationAuditStore
{
    Task<ProcessVerificationAuditRecord> AppendAsync(
        ProcessVerificationAuditRecord record,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessVerificationAuditRecord>> ListAsync(CancellationToken cancellationToken = default);
}

internal interface IProcessVerificationAuditQueryService
{
    Task<ProcessVerificationAuditRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessVerificationAuditRecord>> ListAsync(
        ProcessVerificationAuditQuery query,
        CancellationToken cancellationToken = default);
}

internal sealed record ProcessVerificationAuditQuery
{
    public const int MaximumLimit = 500;

    public ProcessVerificationAuditQuery(
        Guid? processRunId = null,
        Guid? stepRunId = null,
        ProcessDriverVerificationGatewayLane? lane = null,
        int limit = 100,
        DateTimeOffset? recordedAtOrAfter = null,
        DateTimeOffset? recordedBefore = null)
    {
        if (limit <= 0 || limit > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Audit query limit must be between 1 and {MaximumLimit}.");
        }

        if (recordedAtOrAfter.HasValue &&
            recordedBefore.HasValue &&
            recordedAtOrAfter.Value >= recordedBefore.Value)
        {
            throw new ArgumentException("Audit query start time must be earlier than the exclusive end time.", nameof(recordedAtOrAfter));
        }

        ProcessRunId = processRunId;
        StepRunId = stepRunId;
        Lane = lane;
        Limit = limit;
        RecordedAtOrAfter = recordedAtOrAfter;
        RecordedBefore = recordedBefore;
    }

    public Guid? ProcessRunId { get; }

    public Guid? StepRunId { get; }

    public ProcessDriverVerificationGatewayLane? Lane { get; }

    public int Limit { get; }

    public DateTimeOffset? RecordedAtOrAfter { get; }

    public DateTimeOffset? RecordedBefore { get; }
}

internal sealed class InMemoryProcessVerificationAuditStore : IProcessVerificationAuditStore, IProcessVerificationAuditQueryService
{
    private readonly object gate = new();
    private readonly List<ProcessVerificationAuditRecord> records = [];

    public Task<ProcessVerificationAuditRecord> AppendAsync(
        ProcessVerificationAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            records.Add(record);
        }

        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<ProcessVerificationAuditRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<ProcessVerificationAuditRecord>>(Array.AsReadOnly(records.ToArray()));
        }
    }

    public Task<ProcessVerificationAuditRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(records.SingleOrDefault(record => record.Id == id));
        }
    }

    public Task<IReadOnlyList<ProcessVerificationAuditRecord>> ListAsync(
        ProcessVerificationAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var filtered = records
                .Where(record => !query.ProcessRunId.HasValue || record.ProcessRunId == query.ProcessRunId.Value)
                .Where(record => !query.StepRunId.HasValue || record.StepRunId == query.StepRunId.Value)
                .Where(record => !query.Lane.HasValue || record.Lane == query.Lane.Value)
                .Where(record => !query.RecordedAtOrAfter.HasValue || record.RecordedAt >= query.RecordedAtOrAfter.Value)
                .Where(record => !query.RecordedBefore.HasValue || record.RecordedAt < query.RecordedBefore.Value)
                .OrderByDescending(record => record.RecordedAt)
                .Take(query.Limit)
                .ToArray();

            return Task.FromResult<IReadOnlyList<ProcessVerificationAuditRecord>>(Array.AsReadOnly(filtered));
        }
    }
}

internal sealed class EfCoreProcessVerificationAuditStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISecretRedactor secretRedactor) : IProcessVerificationAuditStore, IProcessVerificationAuditQueryService
{
    public async Task<ProcessVerificationAuditRecord> AppendAsync(
        ProcessVerificationAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var sanitizedRecord = Sanitize(record);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<ProcessVerificationAuditEntry>().AddAsync(ToEntry(sanitizedRecord), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return sanitizedRecord;
    }

    public async Task<IReadOnlyList<ProcessVerificationAuditRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await ListAsync(new ProcessVerificationAuditQuery(limit: ProcessVerificationAuditQuery.MaximumLimit), cancellationToken);
    }

    public async Task<ProcessVerificationAuditRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await dbContext.Set<ProcessVerificationAuditEntry>()
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.Id == id, cancellationToken);

        return entry is null ? null : ToRecord(entry);
    }

    public async Task<IReadOnlyList<ProcessVerificationAuditRecord>> ListAsync(
        ProcessVerificationAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = dbContext.Set<ProcessVerificationAuditEntry>().AsNoTracking();

        if (query.ProcessRunId.HasValue)
        {
            records = records.Where(record => record.ProcessRunId == query.ProcessRunId.Value);
        }

        if (query.StepRunId.HasValue)
        {
            records = records.Where(record => record.StepRunId == query.StepRunId.Value);
        }

        if (query.Lane.HasValue)
        {
            records = records.Where(record => record.Lane == query.Lane.Value);
        }

        if (query.RecordedAtOrAfter.HasValue)
        {
            records = records.Where(record => record.RecordedAtUtc >= query.RecordedAtOrAfter.Value);
        }

        if (query.RecordedBefore.HasValue)
        {
            records = records.Where(record => record.RecordedAtUtc < query.RecordedBefore.Value);
        }

        var entries = await records
            .OrderByDescending(record => record.RecordedAtUtc)
            .ThenByDescending(record => record.Id)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

        return entries.Select(ToRecord).ToArray();
    }

    private ProcessVerificationAuditRecord Sanitize(ProcessVerificationAuditRecord record)
    {
        var requestedBy = secretRedactor.Redact(record.RequestedBy).Trim();
        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            requestedBy = "unknown";
        }

        return record with
        {
            RequestedBy = requestedBy
        };
    }

    private static ProcessVerificationAuditEntry ToEntry(ProcessVerificationAuditRecord record)
    {
        return new ProcessVerificationAuditEntry
        {
            Id = record.Id,
            RecordedAtUtc = record.RecordedAt.ToUniversalTime(),
            ProcessRunId = record.ProcessRunId,
            StepRunId = record.StepRunId,
            RequestedBy = record.RequestedBy,
            Lane = record.Lane,
            ResponseCount = record.ResponseCount,
            AcceptedCount = record.AcceptedCount,
            DeniedCount = record.DeniedCount,
            NoMutationPerformed = record.NoMutationPerformed,
            AllowsProcessMutation = record.AllowsProcessMutation,
            AllowsTransitionMutation = record.AllowsTransitionMutation,
            AllowsFinalizerMutation = record.AllowsFinalizerMutation,
            ObservationHash = record.ObservationHash
        };
    }

    private static ProcessVerificationAuditRecord ToRecord(ProcessVerificationAuditEntry entry)
    {
        return new ProcessVerificationAuditRecord(
            entry.Id,
            entry.RecordedAtUtc,
            entry.ProcessRunId,
            entry.StepRunId,
            entry.RequestedBy,
            entry.Lane,
            entry.ResponseCount,
            entry.AcceptedCount,
            entry.DeniedCount,
            entry.NoMutationPerformed,
            entry.AllowsProcessMutation,
            entry.AllowsTransitionMutation,
            entry.AllowsFinalizerMutation,
            entry.ObservationHash);
    }
}
