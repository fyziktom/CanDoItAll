using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowUsageObservationStore(
    IDbContextFactory<AppDbContext> dbContextFactory) :
    IWorkflowUsageObservationStore,
    IWorkflowUsageAnalyticsStore
{
    public Task AppendAsync(
        WorkflowUsageObservation observation,
        CancellationToken cancellationToken = default)
        => AppendRangeAsync([observation], cancellationToken);

    public async Task AppendRangeAsync(
        IReadOnlyList<WorkflowUsageObservation> observations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);
        cancellationToken.ThrowIfCancellationRequested();
        var canonical = EnsureCanonicalBatch(observations);
        if (canonical.Count == 0)
        {
            return;
        }

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var ids = canonical.Keys.Select(id => id.Value).ToArray();
            var existing = await dbContext.Set<WorkflowUsageObservationRecordEntity>()
                .AsNoTracking()
                .Where(record => ids.Contains(record.Id))
                .ToListAsync(cancellationToken);
            foreach (var storedRecord in existing)
            {
                var stored = storedRecord.ToObservation();
                var candidate = canonical[new WorkflowUsageObservationId(storedRecord.Id)];
                if (stored != candidate)
                {
                    throw new WorkflowUsageObservationConflictException(candidate.Id);
                }

                canonical.Remove(candidate.Id);
            }

            if (canonical.Count == 0)
            {
                return;
            }

            dbContext.Set<WorkflowUsageObservationRecordEntity>().AddRange(
                canonical.Values.Select(WorkflowUsageObservationRecordEntity.FromObservation));
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt == 1)
            {
                // A concurrent writer may have won the immutable-id insert race. Re-read once and
                // either accept identical facts or report the actual conflict on the second pass.
            }
        }
    }

    public async Task<IReadOnlyList<WorkflowUsageObservation>> ListAsync(
        WorkflowUsageObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await ApplyQuery(
                dbContext.Set<WorkflowUsageObservationRecordEntity>().AsNoTracking(),
                query)
            .OrderBy(record => record.RecordedAtUtc)
            .ThenBy(record => record.Id)
            .ToListAsync(cancellationToken);
        return records.Select(record => record.ToObservation()).ToArray();
    }

    public async Task<WorkflowListPage<WorkflowUsageObservation>> ListPageAsync(
        WorkflowUsageObservationPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Query);
        if (request.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Workflow usage page index cannot be negative.");
        }

        if (request.PageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Workflow usage page size must be greater than zero.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = ApplyQuery(
            dbContext.Set<WorkflowUsageObservationRecordEntity>().AsNoTracking(),
            request.Query);
        var totalCount = await query.CountAsync(cancellationToken);
        var records = await query
            .OrderByDescending(record => record.RecordedAtUtc)
            .ThenBy(record => record.Id)
            .Skip(checked(request.PageIndex * request.PageSize))
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new WorkflowListPage<WorkflowUsageObservation>(
            records.Select(record => record.ToObservation()).ToArray(),
            request.PageIndex,
            request.PageSize,
            totalCount);
    }

    public async Task<WorkflowUsageAnalyticsStoreSnapshot> AggregateAsync(
        WorkflowUsageAnalyticsStoreQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var runIds = query.RunIds.Select(id => id.Value).Distinct().ToArray();
        if (runIds.Length == 0)
        {
            return WorkflowUsageAnalyticsEmptySnapshot.Value;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var observations = dbContext.Set<WorkflowUsageObservationRecordEntity>()
            .AsNoTracking()
            .Where(record => runIds.Contains(record.RunId));
        var total = await observations
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ObservationCount = group.Count(),
                UsageKnownObservationCount = group.Count(record =>
                    record.UsageStatus == WorkflowUsageStatus.Observed ||
                    record.UsageStatus == WorkflowUsageStatus.Estimated),
                UsageUnknownObservationCount = group.Count(record =>
                    record.UsageStatus != WorkflowUsageStatus.Observed &&
                    record.UsageStatus != WorkflowUsageStatus.Estimated),
                PricingKnownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Known),
                PricingUnknownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Unknown),
                InputTokens = group.Sum(record => (long)record.InputTokens),
                CachedInputTokens = group.Sum(record => (long)record.CachedInputTokens),
                OutputTokens = group.Sum(record => (long)record.OutputTokens),
                ReasoningTokens = group.Sum(record => (long)record.ReasoningTokens),
                TotalTokens = group.Sum(record => (long)record.TotalTokens),
                ToolCallCount = group.Sum(record => (long)record.ToolCallCount),
                KnownCostUsd = group.Sum(record =>
                    record.PricingStatus == WorkflowPricingStatus.Known ? record.CostUsd ?? 0m : 0m)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var runs = await observations
            .GroupBy(record => record.RunId)
            .Select(group => new
            {
                RunId = group.Key,
                ObservationCount = group.Count(),
                UsageKnownObservationCount = group.Count(record =>
                    record.UsageStatus == WorkflowUsageStatus.Observed ||
                    record.UsageStatus == WorkflowUsageStatus.Estimated),
                UsageUnknownObservationCount = group.Count(record =>
                    record.UsageStatus != WorkflowUsageStatus.Observed &&
                    record.UsageStatus != WorkflowUsageStatus.Estimated),
                PricingKnownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Known),
                PricingUnknownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Unknown),
                InputTokens = group.Sum(record => (long)record.InputTokens),
                CachedInputTokens = group.Sum(record => (long)record.CachedInputTokens),
                OutputTokens = group.Sum(record => (long)record.OutputTokens),
                ReasoningTokens = group.Sum(record => (long)record.ReasoningTokens),
                TotalTokens = group.Sum(record => (long)record.TotalTokens),
                ToolCallCount = group.Sum(record => (long)record.ToolCallCount),
                KnownCostUsd = group.Sum(record =>
                    record.PricingStatus == WorkflowPricingStatus.Known ? record.CostUsd ?? 0m : 0m)
            })
            .ToListAsync(cancellationToken);
        var providerModels = await observations
            .GroupBy(record => new { record.ProviderNameKey, record.ProviderKind, record.ModelKey })
            .Select(group => new
            {
                ProviderName = group.Min(record => record.ProviderName),
                group.Key.ProviderKind,
                Model = group.Min(record => record.Model),
                ObservationCount = group.Count(),
                UsageKnownObservationCount = group.Count(record =>
                    record.UsageStatus == WorkflowUsageStatus.Observed ||
                    record.UsageStatus == WorkflowUsageStatus.Estimated),
                UsageUnknownObservationCount = group.Count(record =>
                    record.UsageStatus != WorkflowUsageStatus.Observed &&
                    record.UsageStatus != WorkflowUsageStatus.Estimated),
                PricingKnownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Known),
                PricingUnknownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Unknown),
                InputTokens = group.Sum(record => (long)record.InputTokens),
                CachedInputTokens = group.Sum(record => (long)record.CachedInputTokens),
                OutputTokens = group.Sum(record => (long)record.OutputTokens),
                ReasoningTokens = group.Sum(record => (long)record.ReasoningTokens),
                TotalTokens = group.Sum(record => (long)record.TotalTokens),
                ToolCallCount = group.Sum(record => (long)record.ToolCallCount),
                KnownCostUsd = group.Sum(record =>
                    record.PricingStatus == WorkflowPricingStatus.Known ? record.CostUsd ?? 0m : 0m)
            })
            .ToListAsync(cancellationToken);
        var nodes = await observations
            .GroupBy(record => new { record.NodeId, record.ExecutorId })
            .Select(group => new
            {
                group.Key.NodeId,
                group.Key.ExecutorId,
                ObservationCount = group.Count(),
                UsageKnownObservationCount = group.Count(record =>
                    record.UsageStatus == WorkflowUsageStatus.Observed ||
                    record.UsageStatus == WorkflowUsageStatus.Estimated),
                UsageUnknownObservationCount = group.Count(record =>
                    record.UsageStatus != WorkflowUsageStatus.Observed &&
                    record.UsageStatus != WorkflowUsageStatus.Estimated),
                PricingKnownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Known),
                PricingUnknownObservationCount = group.Count(record => record.PricingStatus == WorkflowPricingStatus.Unknown),
                InputTokens = group.Sum(record => (long)record.InputTokens),
                CachedInputTokens = group.Sum(record => (long)record.CachedInputTokens),
                OutputTokens = group.Sum(record => (long)record.OutputTokens),
                ReasoningTokens = group.Sum(record => (long)record.ReasoningTokens),
                TotalTokens = group.Sum(record => (long)record.TotalTokens),
                ToolCallCount = group.Sum(record => (long)record.ToolCallCount),
                KnownCostUsd = group.Sum(record =>
                    record.PricingStatus == WorkflowPricingStatus.Known ? record.CostUsd ?? 0m : 0m)
            })
            .ToListAsync(cancellationToken);

        return new WorkflowUsageAnalyticsStoreSnapshot(
            total is null
                ? WorkflowUsageAnalyticsTotals.Empty
                : CreateTotals(
                    total.ObservationCount,
                    total.UsageKnownObservationCount,
                    total.UsageUnknownObservationCount,
                    total.PricingKnownObservationCount,
                    total.PricingUnknownObservationCount,
                    total.InputTokens,
                    total.CachedInputTokens,
                    total.OutputTokens,
                    total.ReasoningTokens,
                    total.TotalTokens,
                    total.ToolCallCount,
                    total.KnownCostUsd),
            runs.ToDictionary(
                row => new WorkflowRunId(row.RunId),
                row => CreateTotals(
                    row.ObservationCount,
                    row.UsageKnownObservationCount,
                    row.UsageUnknownObservationCount,
                    row.PricingKnownObservationCount,
                    row.PricingUnknownObservationCount,
                    row.InputTokens,
                    row.CachedInputTokens,
                    row.OutputTokens,
                    row.ReasoningTokens,
                    row.TotalTokens,
                    row.ToolCallCount,
                    row.KnownCostUsd)),
            providerModels
                .Select(row => new WorkflowProviderModelAnalyticsRow(
                    RequireStoredIdentity(row.ProviderName, nameof(WorkflowUsageObservation.ProviderName)),
                    row.ProviderKind,
                    RequireStoredIdentity(row.Model, nameof(WorkflowUsageObservation.Model)),
                    CreateTotals(
                        row.ObservationCount,
                        row.UsageKnownObservationCount,
                        row.UsageUnknownObservationCount,
                        row.PricingKnownObservationCount,
                        row.PricingUnknownObservationCount,
                        row.InputTokens,
                        row.CachedInputTokens,
                        row.OutputTokens,
                        row.ReasoningTokens,
                        row.TotalTokens,
                        row.ToolCallCount,
                        row.KnownCostUsd)))
                .OrderBy(row => row.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Model, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            nodes
                .Select(row => new WorkflowNodeUsageAnalyticsRow(
                    new WorkflowNodeId(row.NodeId),
                    string.IsNullOrWhiteSpace(row.ExecutorId) ? null : new WorkflowExecutorId(row.ExecutorId),
                    CreateTotals(
                        row.ObservationCount,
                        row.UsageKnownObservationCount,
                        row.UsageUnknownObservationCount,
                        row.PricingKnownObservationCount,
                        row.PricingUnknownObservationCount,
                        row.InputTokens,
                        row.CachedInputTokens,
                        row.OutputTokens,
                        row.ReasoningTokens,
                        row.TotalTokens,
                        row.ToolCallCount,
                        row.KnownCostUsd)))
                .OrderBy(row => row.NodeId.Value, StringComparer.Ordinal)
                .ThenBy(row => row.ExecutorId?.Value, StringComparer.Ordinal)
                .ToArray());
    }

    private static Dictionary<WorkflowUsageObservationId, WorkflowUsageObservation> EnsureCanonicalBatch(
        IReadOnlyList<WorkflowUsageObservation> observations)
    {
        var canonical = new Dictionary<WorkflowUsageObservationId, WorkflowUsageObservation>();
        foreach (var observation in observations)
        {
            WorkflowUsageObservationValidator.ThrowIfNotPersistable(observation);
            if (canonical.TryGetValue(observation.Id, out var stored) && stored != observation)
            {
                throw new WorkflowUsageObservationConflictException(observation.Id);
            }

            canonical[observation.Id] = observation;
        }

        return canonical;
    }

    private static IQueryable<WorkflowUsageObservationRecordEntity> ApplyQuery(
        IQueryable<WorkflowUsageObservationRecordEntity> source,
        WorkflowUsageObservationQuery query)
    {
        if (query.RunIds.Count > 0)
        {
            var runIds = query.RunIds.Select(id => id.Value).Distinct().ToArray();
            source = source.Where(record => runIds.Contains(record.RunId));
        }

        if (query.OriginProcessRunIds.Count > 0)
        {
            var processRunIds = query.OriginProcessRunIds.Select(runId => runId.Value).Distinct().ToArray();
            source = source.Where(record =>
                record.OriginKind == WorkflowLaunchOriginKind.ProcessAssignment &&
                record.OriginProcessRunId.HasValue &&
                processRunIds.Contains(record.OriginProcessRunId.Value));
        }

        if (query.WorkflowId is { } workflowId)
        {
            source = source.Where(record => record.WorkflowId == workflowId.Value);
        }

        if (query.VersionId is { } versionId)
        {
            source = source.Where(record => record.VersionId == versionId.Value);
        }

        if (query.NodeId is { } nodeId)
        {
            source = source.Where(record => record.NodeId == nodeId.Value);
        }

        if (query.ExecutorId is { } executorId)
        {
            source = source.Where(record => record.ExecutorId == executorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ProviderName))
        {
            var providerNameKey = query.ProviderName.Trim().ToUpperInvariant();
            source = source.Where(record => record.ProviderNameKey == providerNameKey);
        }

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            var modelKey = query.Model.Trim().ToUpperInvariant();
            source = source.Where(record => record.ModelKey == modelKey);
        }

        if (query.RecordedFromUtc is { } recordedFromUtc)
        {
            source = source.Where(record => record.RecordedAtUtc >= recordedFromUtc);
        }

        if (query.RecordedToUtc is { } recordedToUtc)
        {
            source = source.Where(record => record.RecordedAtUtc <= recordedToUtc);
        }

        return source;
    }

    private static WorkflowUsageAnalyticsTotals CreateTotals(
        int observationCount,
        int usageKnownObservationCount,
        int usageUnknownObservationCount,
        int pricingKnownObservationCount,
        int pricingUnknownObservationCount,
        long inputTokens,
        long cachedInputTokens,
        long outputTokens,
        long reasoningTokens,
        long totalTokens,
        long toolCallCount,
        decimal knownCostUsd)
        => new(
            observationCount,
            usageKnownObservationCount,
            usageUnknownObservationCount,
            pricingKnownObservationCount,
            pricingUnknownObservationCount,
            inputTokens,
            cachedInputTokens,
            outputTokens,
            reasoningTokens,
            totalTokens,
            toolCallCount,
            decimal.Round(knownCostUsd, 6, MidpointRounding.AwayFromZero));

    private static string RequireStoredIdentity(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Persisted workflow usage aggregate contains an empty {fieldName} identity.");
        }

        return value;
    }
}

public sealed class WorkflowUsageObservationRecordEntity
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid VersionId { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string? ExecutorId { get; set; }

    public Guid? ComponentId { get; set; }

    public WorkflowUsageProducerKind ProducerKind { get; set; }

    public Guid InvocationId { get; set; }

    public int Attempt { get; set; }

    public Guid? ProviderProfileId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderNameKey { get; set; } = string.Empty;

    public ProviderKind? ProviderKind { get; set; }

    public ProviderTransportKind? TransportKind { get; set; }

    public string Model { get; set; } = string.Empty;

    public string ModelKey { get; set; } = string.Empty;

    public string SourcePhase { get; set; } = string.Empty;

    public WorkflowUsageStatus UsageStatus { get; set; }

    public WorkflowPricingStatus PricingStatus { get; set; }

    public WorkflowUsagePricingProvenance PricingProvenance { get; set; }

    public int InputTokens { get; set; }

    public int CachedInputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }

    public int ToolCallCount { get; set; }

    public decimal? CostUsd { get; set; }

    public string PricingProfileHash { get; set; } = string.Empty;

    public string PricingVersion { get; set; } = string.Empty;

    public string ProviderRequestId { get; set; } = string.Empty;

    public string ProviderResponseId { get; set; } = string.Empty;

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public string OriginJson { get; set; } = string.Empty;

    public WorkflowLaunchOriginKind? OriginKind { get; set; }

    public Guid? OriginProcessRunId { get; set; }

    public Guid? OriginProcessAssignmentId { get; set; }

    public static WorkflowUsageObservationRecordEntity FromObservation(WorkflowUsageObservation observation) => new()
    {
        Id = observation.Id.Value,
        RunId = observation.RunId?.Value ?? throw new WorkflowUsageObservationCorrelationException(observation.Id),
        WorkflowId = observation.WorkflowId.Value,
        VersionId = observation.VersionId.Value,
        NodeId = observation.NodeId.Value,
        ExecutorId = observation.ExecutorId?.Value,
        ComponentId = observation.ComponentId?.Value,
        ProducerKind = observation.ProducerKind,
        InvocationId = observation.InvocationId,
        Attempt = observation.Attempt,
        ProviderProfileId = observation.ProviderProfileId,
        ProviderName = observation.ProviderName,
        ProviderNameKey = observation.ProviderName.ToUpperInvariant(),
        ProviderKind = observation.ProviderKind,
        TransportKind = observation.TransportKind,
        Model = observation.Model,
        ModelKey = observation.Model.ToUpperInvariant(),
        SourcePhase = observation.SourcePhase,
        UsageStatus = observation.UsageStatus,
        PricingStatus = observation.PricingStatus,
        PricingProvenance = observation.PricingProvenance,
        InputTokens = observation.InputTokens,
        CachedInputTokens = observation.CachedInputTokens,
        OutputTokens = observation.OutputTokens,
        ReasoningTokens = observation.ReasoningTokens,
        TotalTokens = observation.TotalTokens,
        ToolCallCount = observation.ToolCallCount,
        CostUsd = observation.CostUsd,
        PricingProfileHash = observation.PricingProfileHash,
        PricingVersion = observation.PricingVersion,
        ProviderRequestId = observation.ProviderRequestId,
        ProviderResponseId = observation.ProviderResponseId,
        StartedAtUtc = observation.StartedAtUtc,
        CompletedAtUtc = observation.CompletedAtUtc,
        RecordedAtUtc = observation.RecordedAtUtc,
        OriginKind = observation.Origin?.Kind,
        OriginProcessRunId = (observation.Origin as WorkflowLaunchOrigin.ProcessAssignment)?.ProcessRun.Value,
        OriginProcessAssignmentId = (observation.Origin as WorkflowLaunchOrigin.ProcessAssignment)?.Assignment.Value,
        OriginJson = observation.Origin is null
            ? string.Empty
            : JsonSerializer.Serialize(observation.Origin, JsonOptions)
    };

    public WorkflowUsageObservation ToObservation()
        => new(
            new WorkflowUsageObservationId(Id),
            new WorkflowRunId(RunId),
            new WorkflowId(WorkflowId),
            new WorkflowVersionId(VersionId),
            new WorkflowNodeId(NodeId),
            string.IsNullOrWhiteSpace(ExecutorId) ? null : new WorkflowExecutorId(ExecutorId),
            ComponentId.HasValue ? new WorkflowComponentId(ComponentId.Value) : null,
            ProducerKind,
            InvocationId,
            Attempt,
            ProviderProfileId,
            ProviderName,
            ProviderKind,
            TransportKind,
            Model,
            SourcePhase,
            UsageStatus,
            PricingStatus,
            PricingProvenance,
            InputTokens,
            CachedInputTokens,
            OutputTokens,
            ReasoningTokens,
            TotalTokens,
            ToolCallCount,
            CostUsd,
            PricingProfileHash,
            PricingVersion,
            ProviderRequestId,
            ProviderResponseId,
            StartedAtUtc,
            CompletedAtUtc,
            RecordedAtUtc,
            string.IsNullOrWhiteSpace(OriginJson)
                ? null
                : JsonSerializer.Deserialize<WorkflowLaunchOrigin>(OriginJson, JsonOptions));
}

internal sealed class WorkflowUsageObservationRecordEntityConfiguration :
    IEntityTypeConfiguration<WorkflowUsageObservationRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowUsageObservationRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowUsageObservations");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.RunId).IsRequired();
        builder.Property(record => record.NodeId).HasMaxLength(200).IsRequired();
        builder.Property(record => record.ExecutorId).HasMaxLength(240);
        builder.Property(record => record.ProducerKind).HasConversion<int>();
        builder.Property(record => record.ProviderName).HasMaxLength(240).IsRequired();
        builder.Property(record => record.ProviderNameKey).HasMaxLength(240).IsRequired();
        builder.Property(record => record.ProviderKind).HasConversion<int?>();
        builder.Property(record => record.TransportKind).HasConversion<int?>();
        builder.Property(record => record.Model).HasMaxLength(240).IsRequired();
        builder.Property(record => record.ModelKey).HasMaxLength(240).IsRequired();
        builder.Property(record => record.SourcePhase).HasMaxLength(240).IsRequired();
        builder.Property(record => record.UsageStatus).HasConversion<int>();
        builder.Property(record => record.PricingStatus).HasConversion<int>();
        builder.Property(record => record.PricingProvenance).HasConversion<int>();
        builder.Property(record => record.CostUsd).HasPrecision(28, 12);
        builder.Property(record => record.PricingProfileHash).HasMaxLength(128);
        builder.Property(record => record.PricingVersion).HasMaxLength(120);
        builder.Property(record => record.ProviderRequestId).HasMaxLength(500);
        builder.Property(record => record.ProviderResponseId).HasMaxLength(500);
        builder.Property(record => record.OriginJson).HasColumnType("TEXT");
        builder.Property(record => record.OriginKind).HasConversion<int?>();
        builder.HasIndex(record => new { record.RunId, record.RecordedAtUtc });
        builder.HasIndex(record => new { record.WorkflowId, record.RecordedAtUtc });
        builder.HasIndex(record => new { record.ProviderNameKey, record.ModelKey });
        builder.HasIndex(record => new { record.NodeId, record.ExecutorId });
        builder.HasIndex(record => new { record.OriginProcessRunId, record.RecordedAtUtc });
    }
}

internal static class WorkflowUsageAnalyticsEmptySnapshot
{
    public static WorkflowUsageAnalyticsStoreSnapshot Value { get; } = new(
        WorkflowUsageAnalyticsTotals.Empty,
        new Dictionary<WorkflowRunId, WorkflowUsageAnalyticsTotals>(),
        [],
        []);
}
