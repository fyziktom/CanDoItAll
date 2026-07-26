using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static CanDoItAll.Processes.Persistence.ProcessRunRecordPersistenceCodec;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRunRecordStore(ProcessPersistenceDbContext dbContext) : IProcessRunRecordStore
{
    private static readonly SemaphoreSlim NonRelationalMutationGate = new(1, 1);

    public async Task<bool> UpsertSeedAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken = default)
    {
        ValidateSeed(seed);

        if (!dbContext.Database.IsRelational())
        {
            return await UpsertSeedNonRelationalAsync(seed, cancellationToken).ConfigureAwait(false);
        }

        if (seed.Validation == ProcessRunRecordSeedValidation.CurrentReportableSource)
        {
            return await UpsertValidatedSeedRelationalAsync(seed, cancellationToken).ConfigureAwait(false);
        }

        return await UpsertLockedSeedRelationalAsync(seed, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> UpsertSeedRelationalAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken)
    {
        var updated = await TryResetExistingSeedRelationalAsync(seed, cancellationToken).ConfigureAwait(false);
        if (updated is not null)
        {
            return updated.Value;
        }

        dbContext.RunRecords.Add(CreateSeedEntity(seed));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            dbContext.ChangeTracker.Clear();
            return await TryResetExistingSeedRelationalAsync(seed, cancellationToken).ConfigureAwait(false) ?? false;
        }
    }

    private async Task<bool> UpsertValidatedSeedRelationalAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await AcquireRunMutationLockAsync(seed.Identity.RunId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (!await IsCurrentReportableSourceAsync(seed, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var insertedOrRevised = await UpsertSeedRelationalAsync(seed, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return insertedOrRevised;
    }

    private async Task<bool> UpsertLockedSeedRelationalAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await AcquireRunMutationLockAsync(seed.Identity.RunId.Value, cancellationToken)
            .ConfigureAwait(false);
        var insertedOrRevised = await UpsertSeedRelationalAsync(seed, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return insertedOrRevised;
    }

    public async Task<bool> SupersedeAsync(
        ProcessRunRecordSupersession supersession,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supersession);
        ValidateSourceSequence(supersession.SourceGlobalSequence, nameof(supersession.SourceGlobalSequence));
        ValidateSourceSequence(supersession.SourceRootSequence, nameof(supersession.SourceRootSequence));

        if (!dbContext.Database.IsRelational())
        {
            return await SupersedeNonRelationalAsync(supersession, cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await AcquireRunMutationLockAsync(supersession.RunId.Value, cancellationToken)
            .ConfigureAwait(false);
        var updatedRows = await dbContext.RunRecords
            .Where(record =>
                record.RunId == supersession.RunId.Value &&
                record.SourceGlobalSequence < supersession.SourceGlobalSequence)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.LifecycleState, ProcessRunRecordLifecycleState.Superseded)
                .SetProperty(record => record.SourceGlobalSequence, supersession.SourceGlobalSequence)
                .SetProperty(record => record.SourceRootSequence, supersession.SourceRootSequence)
                .SetProperty(
                    record => record.FactsStatus,
                    record => record.FactsStatus == ProcessRunFactsStatus.Assembling
                        ? ProcessRunFactsStatus.Pending
                        : record.FactsStatus)
                .SetProperty(record => record.FactsLeaseToken, (Guid?)null)
                .SetProperty(record => record.FactsLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(
                    record => record.NarrativeStatus,
                    record => record.NarrativeStatus == ProcessRunNarrativeStatus.Generating
                        ? ProcessRunNarrativeStatus.Pending
                        : record.NarrativeStatus)
                .SetProperty(record => record.NarrativeLeaseToken, (Guid?)null)
                .SetProperty(record => record.NarrativeLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.UpdatedAtUtc, supersession.SupersededAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return updatedRows > 0;
    }

    public async Task<ProcessRunRecord?> GetAsync(
        ProcessRunId runId,
        bool includeSuperseded = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.RunRecords
            .AsNoTracking()
            .Where(record => record.RunId == runId.Value);
        if (!includeSuperseded)
        {
            query = query.Where(record => record.LifecycleState == ProcessRunRecordLifecycleState.Current);
        }

        var entity = await query.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return entity is null
            ? null
            : MapRecord(entity);
    }

    public async Task<ProcessRunRecordPage> ListAsync(
        ProcessRunRecordListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidatePageSize(query.Take);
        ValidateRunIdFilter(query.RunIds);
        if (!Enum.IsDefined(query.Payload))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.Payload),
                query.Payload,
                "Process run record list payload is not supported.");
        }

        var recordsQuery = ApplyListFilters(dbContext.RunRecords.AsNoTracking(), query);
        var rows = await SelectSummaryColumns(recordsQuery)
            .OrderByDescending(record => record.EndedAtUtc)
            .ThenByDescending(record => record.RunId)
            .Take(query.Take + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > query.Take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        if (query.Payload == ProcessRunRecordListPayload.Full)
        {
            await HydrateFullSummaryPayloadAsync(rows, cancellationToken).ConfigureAwait(false);
        }

        var summaries = new List<ProcessRunRecordSummary>(rows.Count);
        foreach (var row in rows)
        {
            summaries.Add(MapSummary(row));
        }

        var nextCursor = hasMore && rows.Count > 0
            ? new ProcessRunRecordCursor(
                rows[^1].EndedAtUtc,
                new ProcessRunId(rows[^1].RunId))
            : null;
        return new ProcessRunRecordPage(summaries, nextCursor);
    }

    public async Task<ProcessRunRecordAnalytics> ReadAnalyticsAsync(
        ProcessRunRecordAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ToUtc <= query.FromUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.ToUtc,
                "Process run analytics end time must be later than the start time.");
        }

        var recordsQuery = dbContext.RunRecords
            .AsNoTracking()
            .Where(record =>
                record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                record.EndedAtUtc >= query.FromUtc &&
                record.EndedAtUtc < query.ToUtc);
        if (query.ProjectId is { } projectId)
        {
            recordsQuery = recordsQuery.Where(record => record.ProjectId == projectId);
        }

        if (query.DefinitionId is { } definitionId)
        {
            recordsQuery = recordsQuery.Where(record => record.DefinitionId == definitionId.Value);
        }

        if (query.RootRunId is { } rootRunId)
        {
            recordsQuery = recordsQuery.Where(record => record.RootRunId == rootRunId.Value);
        }

        if (query.ParticipantId is { } participantId)
        {
            var participantRunIds = dbContext.RunRecordParticipants
                .AsNoTracking()
                .Where(participant => participant.ParticipantId == participantId.Value)
                .Select(participant => participant.RunId);
            recordsQuery = recordsQuery.Where(record => participantRunIds.Contains(record.RunId));
        }

        var groups = await recordsQuery
            .GroupBy(record => new
            {
                record.Disposition,
                FactsAvailable = record.FactsStatus == ProcessRunFactsStatus.Completed,
                record.Completeness
            })
            .Select(group => new ProcessRunRecordAnalyticsGroup(
                group.Key.Disposition,
                group.Key.FactsAvailable,
                group.Key.Completeness,
                group.Count(),
                group.Max(record => record.EndedAtUtc),
                group.Max(record => record.SourceGlobalSequence),
                group.Sum(record => record.DurationMilliseconds ?? 0),
                group.Sum(record => record.InputTokenCount),
                group.Sum(record => record.CachedInputTokenCount),
                group.Sum(record => record.OutputTokenCount),
                group.Sum(record => record.ReasoningTokenCount),
                group.Sum(record => record.TotalTokenCount),
                group.Sum(record => record.EstimatedCost),
                group.Sum(record => record.ActualCost),
                group.Sum(record => record.RepetitionCount),
                group.Sum(record => record.ExecutionCount),
                group.Sum(record => record.ReworkCount),
                group.Sum(record => record.IncidentCount),
                group.Sum(record => record.EscalationCount),
                group.Sum(record => record.ToolCallCount),
                group.Sum(record => record.ArtifactCount)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var dispositions = groups
            .GroupBy(group => group.Disposition)
            .OrderBy(group => group.Key)
            .Select(group => new ProcessRunDispositionAnalytics(
                group.Key,
                group.Sum(item => item.MatchingRunCount)))
            .ToArray();
        var factsAvailableGroups = groups
            .Where(group => group.FactsAvailable)
            .ToArray();
        var matchingRunCount = groups.Sum(group => group.MatchingRunCount);
        var factsAvailableRunCount = factsAvailableGroups.Sum(group => group.MatchingRunCount);

        return new ProcessRunRecordAnalytics(
            matchingRunCount,
            factsAvailableRunCount,
            factsAvailableGroups
                .Where(group => group.Completeness == ProcessRunRecordCompleteness.Complete)
                .Sum(group => group.MatchingRunCount),
            factsAvailableGroups
                .Where(group => group.Completeness == ProcessRunRecordCompleteness.Partial)
                .Sum(group => group.MatchingRunCount),
            matchingRunCount - factsAvailableRunCount,
            groups.Count == 0
                ? null
                : groups.Max(group => group.LatestEndedAtUtc),
            groups.Count == 0
                ? null
                : groups.Max(group => group.MaximumSourceGlobalSequence),
            factsAvailableGroups.Sum(group => group.DurationMilliseconds),
            factsAvailableGroups.Sum(group => group.InputTokenCount),
            factsAvailableGroups.Sum(group => group.CachedInputTokenCount),
            factsAvailableGroups.Sum(group => group.OutputTokenCount),
            factsAvailableGroups.Sum(group => group.ReasoningTokenCount),
            factsAvailableGroups.Sum(group => group.TotalTokenCount),
            factsAvailableGroups.Sum(group => group.EstimatedCost),
            factsAvailableGroups.Sum(group => group.ActualCost),
            factsAvailableGroups.Sum(group => group.RepetitionCount),
            factsAvailableGroups.Sum(group => group.ExecutionCount),
            factsAvailableGroups.Sum(group => group.ReworkCount),
            factsAvailableGroups.Sum(group => group.IncidentCount),
            factsAvailableGroups.Sum(group => group.EscalationCount),
            factsAvailableGroups.Sum(group => group.ToolCallCount),
            factsAvailableGroups.Sum(group => group.ArtifactCount),
            dispositions);
    }

    public async Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateClaimRequest(request);
        if (dbContext.Database.IsNpgsql())
        {
            return await ClaimFactsPostgreSqlAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (!dbContext.Database.IsRelational())
        {
            return await ClaimFactsNonRelationalAsync(request, cancellationToken).ConfigureAwait(false);
        }

        throw new NotSupportedException(
            $"Atomic process run facts claims are not implemented for provider '{dbContext.Database.ProviderName}'.");
    }

    public async Task<bool> CompleteFactsAsync(
        ProcessRunFactsCompletion completion,
        CancellationToken cancellationToken = default)
    {
        ValidateFactsCompletion(completion);
        var factsJson = SerializeBounded(
            completion.Facts,
            ProcessRunRecordPayloadLimits.MaximumFactsPayloadBytes,
            nameof(completion.Facts));
        var participantIds = completion.Facts.ParticipantIds
            .Select(participant => participant.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var participantIdsJson = Serialize(completion.Facts.ParticipantIds);
        var completenessWarningsJson = Serialize(completion.CompletenessWarnings);

        if (!dbContext.Database.IsRelational())
        {
            return await CompleteFactsNonRelationalAsync(
                    completion,
                    factsJson,
                    participantIdsJson,
                    completenessWarningsJson,
                    participantIds,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var metrics = completion.Metrics;
        var identity = completion.Identity;
        var updatedRows = await dbContext.RunRecords
            .Where(record =>
                record.RunId == identity.RunId.Value &&
                record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                record.SourceGlobalSequence == completion.SourceGlobalSequence &&
                record.FactsStatus == ProcessRunFactsStatus.Assembling &&
                record.FactsLeaseToken == completion.ClaimToken.Value)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.RootRunId, identity.RootRunId.Value)
                .SetProperty(record => record.ParentRunId, identity.ParentRunId == null ? null : identity.ParentRunId.Value.Value)
                .SetProperty(record => record.PlanId, identity.PlanId == null ? null : identity.PlanId.Value.Value)
                .SetProperty(record => record.DefinitionId, identity.DefinitionId == null ? null : identity.DefinitionId.Value.Value)
                .SetProperty(
                    record => record.DefinitionVersionId,
                    identity.DefinitionVersionId == null ? null : identity.DefinitionVersionId.Value.Value)
                .SetProperty(record => record.ProjectId, identity.ProjectId)
                .SetProperty(record => record.Completeness, completion.Completeness)
                .SetProperty(record => record.AvailableEvidenceSources, completion.AvailableEvidenceSources)
                .SetProperty(record => record.MissingEvidenceSources, completion.MissingEvidenceSources)
                .SetProperty(record => record.CompletenessWarningsJson, completenessWarningsJson)
                .SetProperty(record => record.StartedAtUtc, metrics.StartedAtUtc)
                .SetProperty(record => record.EndedAtUtc, metrics.EndedAtUtc)
                .SetProperty(record => record.DurationMilliseconds, metrics.DurationMilliseconds)
                .SetProperty(record => record.TotalStepCount, metrics.TotalStepCount)
                .SetProperty(record => record.ExecutableStepCount, metrics.ExecutableStepCount)
                .SetProperty(record => record.CompletedStepCount, metrics.CompletedStepCount)
                .SetProperty(record => record.FailedStepCount, metrics.FailedStepCount)
                .SetProperty(record => record.CancelledStepCount, metrics.CancelledStepCount)
                .SetProperty(record => record.RepetitionCount, metrics.RepetitionCount)
                .SetProperty(record => record.ExecutionCount, metrics.ExecutionCount)
                .SetProperty(record => record.ReworkCount, metrics.ReworkCount)
                .SetProperty(record => record.IncidentCount, metrics.IncidentCount)
                .SetProperty(record => record.EscalationCount, metrics.EscalationCount)
                .SetProperty(record => record.InputTokenCount, metrics.InputTokenCount)
                .SetProperty(record => record.CachedInputTokenCount, metrics.CachedInputTokenCount)
                .SetProperty(record => record.OutputTokenCount, metrics.OutputTokenCount)
                .SetProperty(record => record.ReasoningTokenCount, metrics.ReasoningTokenCount)
                .SetProperty(record => record.TotalTokenCount, metrics.TotalTokenCount)
                .SetProperty(record => record.EstimatedCost, metrics.EstimatedCost)
                .SetProperty(record => record.ActualCost, metrics.ActualCost)
                .SetProperty(record => record.ToolCallCount, metrics.ToolCallCount)
                .SetProperty(record => record.ArtifactCount, metrics.ArtifactCount)
                .SetProperty(record => record.SubprocessCount, metrics.SubprocessCount)
                .SetProperty(record => record.FactsJson, factsJson)
                .SetProperty(record => record.ParticipantIdsJson, participantIdsJson)
                .SetProperty(record => record.FactsStatus, ProcessRunFactsStatus.Completed)
                .SetProperty(record => record.FactsLeaseToken, (Guid?)null)
                .SetProperty(record => record.FactsLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.FactsNextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.FactsLastErrorClass, (string?)null)
                .SetProperty(record => record.FactsLastErrorDiagnosticReference, (string?)null)
                .SetProperty(record => record.UpdatedAtUtc, completion.CompletedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await dbContext.RunRecordParticipants
            .Where(participant => participant.RunId == identity.RunId.Value)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.RunRecordParticipants.AddRange(participantIds.Select(participantId =>
            new ProcessRunRecordParticipantEntity
            {
                ParticipantId = participantId,
                RunId = identity.RunId.Value
            }));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return true;
    }

    public Task<bool> FailFactsAsync(
        ProcessRunStageFailure failure,
        CancellationToken cancellationToken = default)
    {
        ValidateStageFailure(failure);
        return FailStageAsync(failure, ProcessRunRecordStage.Facts, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateClaimRequest(request);
        if (dbContext.Database.IsNpgsql())
        {
            return await ClaimNarrativesPostgreSqlAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (!dbContext.Database.IsRelational())
        {
            return await ClaimNarrativesNonRelationalAsync(request, cancellationToken).ConfigureAwait(false);
        }

        throw new NotSupportedException(
            $"Atomic process run narrative claims are not implemented for provider '{dbContext.Database.ProviderName}'.");
    }

    public async Task<bool> CompleteNarrativeAsync(
        ProcessRunNarrativeCompletion completion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ValidateSourceSequence(completion.SourceGlobalSequence, nameof(completion.SourceGlobalSequence));
        ValidateNarrative(completion.Narrative);
        var narrativeJson = SerializeBounded(
            completion.Narrative,
            ProcessRunRecordPayloadLimits.MaximumNarrativePayloadBytes,
            nameof(completion.Narrative));

        if (!dbContext.Database.IsRelational())
        {
            return await CompleteNarrativeNonRelationalAsync(completion, narrativeJson, cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedRows = await dbContext.RunRecords
            .Where(record =>
                record.RunId == completion.RunId.Value &&
                record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                record.SourceGlobalSequence == completion.SourceGlobalSequence &&
                record.FactsStatus == ProcessRunFactsStatus.Completed &&
                record.NarrativeStatus == ProcessRunNarrativeStatus.Generating &&
                record.NarrativeLeaseToken == completion.ClaimToken.Value)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.NarrativeJson, narrativeJson)
                .SetProperty(record => record.NarrativeStatus, ProcessRunNarrativeStatus.Completed)
                .SetProperty(record => record.NarrativeLeaseToken, (Guid?)null)
                .SetProperty(record => record.NarrativeLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.NarrativeNextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.NarrativeLastErrorClass, (string?)null)
                .SetProperty(record => record.NarrativeLastErrorDiagnosticReference, (string?)null)
                .SetProperty(record => record.UpdatedAtUtc, completion.CompletedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
        return updatedRows > 0;
    }

    public Task<bool> FailNarrativeAsync(
        ProcessRunStageFailure failure,
        CancellationToken cancellationToken = default)
    {
        ValidateStageFailure(failure);
        return FailStageAsync(failure, ProcessRunRecordStage.Narrative, cancellationToken);
    }

    private async Task<bool?> TryResetExistingSeedRelationalAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await TryResetExistingSeedRelationalCoreAsync(seed, cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = await TryResetExistingSeedRelationalCoreAsync(seed, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<bool?> TryResetExistingSeedRelationalCoreAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken)
    {
        var updatedRows = await dbContext.RunRecords
            .Where(record =>
                record.RunId == seed.Identity.RunId.Value &&
                record.SourceGlobalSequence < seed.SourceGlobalSequence &&
                (record.LifecycleState == ProcessRunRecordLifecycleState.Superseded ||
                 record.Disposition != seed.Disposition))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.RootRunId, seed.Identity.RootRunId.Value)
                .SetProperty(
                    record => record.ParentRunId,
                    seed.Identity.ParentRunId == null ? null : seed.Identity.ParentRunId.Value.Value)
                .SetProperty(
                    record => record.PlanId,
                    seed.Identity.PlanId == null ? null : seed.Identity.PlanId.Value.Value)
                .SetProperty(
                    record => record.DefinitionId,
                    seed.Identity.DefinitionId == null ? null : seed.Identity.DefinitionId.Value.Value)
                .SetProperty(
                    record => record.DefinitionVersionId,
                    seed.Identity.DefinitionVersionId == null ? null : seed.Identity.DefinitionVersionId.Value.Value)
                .SetProperty(record => record.ProjectId, seed.Identity.ProjectId)
                .SetProperty(record => record.Disposition, seed.Disposition)
                .SetProperty(record => record.LifecycleState, ProcessRunRecordLifecycleState.Current)
                .SetProperty(record => record.Completeness, ProcessRunRecordCompleteness.SeedOnly)
                .SetProperty(record => record.AvailableEvidenceSources, ProcessRunEvidenceSource.None)
                .SetProperty(record => record.MissingEvidenceSources, ProcessRunEvidenceSource.All)
                .SetProperty(record => record.CompletenessWarningsJson, "[]")
                .SetProperty(record => record.StartedAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.EndedAtUtc, seed.EndedAtUtc)
                .SetProperty(record => record.DurationMilliseconds, (long?)null)
                .SetProperty(record => record.TotalStepCount, 0)
                .SetProperty(record => record.ExecutableStepCount, 0)
                .SetProperty(record => record.CompletedStepCount, 0)
                .SetProperty(record => record.FailedStepCount, 0)
                .SetProperty(record => record.CancelledStepCount, 0)
                .SetProperty(record => record.RepetitionCount, 0)
                .SetProperty(record => record.ExecutionCount, 0)
                .SetProperty(record => record.ReworkCount, 0)
                .SetProperty(record => record.IncidentCount, 0)
                .SetProperty(record => record.EscalationCount, 0)
                .SetProperty(record => record.InputTokenCount, 0L)
                .SetProperty(record => record.CachedInputTokenCount, 0L)
                .SetProperty(record => record.OutputTokenCount, 0L)
                .SetProperty(record => record.ReasoningTokenCount, 0L)
                .SetProperty(record => record.TotalTokenCount, 0L)
                .SetProperty(record => record.EstimatedCost, 0m)
                .SetProperty(record => record.ActualCost, 0m)
                .SetProperty(record => record.ToolCallCount, 0)
                .SetProperty(record => record.ArtifactCount, 0)
                .SetProperty(record => record.SubprocessCount, 0)
                .SetProperty(record => record.FactsJson, (string?)null)
                .SetProperty(record => record.ParticipantIdsJson, "[]")
                .SetProperty(record => record.FactsStatus, ProcessRunFactsStatus.Pending)
                .SetProperty(record => record.FactsLeaseToken, (Guid?)null)
                .SetProperty(record => record.FactsLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.FactsAttemptCount, 0)
                .SetProperty(record => record.FactsNextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.FactsLastErrorClass, (string?)null)
                .SetProperty(record => record.FactsLastErrorDiagnosticReference, (string?)null)
                .SetProperty(record => record.NarrativeJson, (string?)null)
                .SetProperty(record => record.NarrativeStatus, ProcessRunNarrativeStatus.Pending)
                .SetProperty(record => record.NarrativeLeaseToken, (Guid?)null)
                .SetProperty(record => record.NarrativeLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.NarrativeAttemptCount, 0)
                .SetProperty(record => record.NarrativeNextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(record => record.NarrativeLastErrorClass, (string?)null)
                .SetProperty(record => record.NarrativeLastErrorDiagnosticReference, (string?)null)
                .SetProperty(record => record.SourceGlobalSequence, seed.SourceGlobalSequence)
                .SetProperty(record => record.SourceRootSequence, seed.SourceRootSequence)
                .SetProperty(record => record.SchemaVersion, seed.SchemaVersion)
                .SetProperty(record => record.UpdatedAtUtc, seed.ObservedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedRows > 0)
        {
            await dbContext.RunRecordParticipants
                .Where(participant => participant.RunId == seed.Identity.RunId.Value)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return true;
        }

        var exists = await dbContext.RunRecords
            .AsNoTracking()
            .AnyAsync(record => record.RunId == seed.Identity.RunId.Value, cancellationToken)
            .ConfigureAwait(false);
        return exists
            ? false
            : null;
    }

    private async Task<bool> UpsertSeedNonRelationalAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken)
    {
        await NonRelationalMutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (seed.Validation == ProcessRunRecordSeedValidation.CurrentReportableSource &&
                !await IsCurrentReportableSourceAsync(seed, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            var existing = await dbContext.RunRecords
                .SingleOrDefaultAsync(record => record.RunId == seed.Identity.RunId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                dbContext.RunRecords.Add(CreateSeedEntity(seed));
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (existing.SourceGlobalSequence >= seed.SourceGlobalSequence ||
                (existing.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                 existing.Disposition == seed.Disposition))
            {
                return false;
            }

            ResetSeedEntity(existing, seed);
            var participants = await dbContext.RunRecordParticipants
                .Where(participant => participant.RunId == seed.Identity.RunId.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            dbContext.RunRecordParticipants.RemoveRange(participants);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            NonRelationalMutationGate.Release();
        }
    }

    private async Task<bool> SupersedeNonRelationalAsync(
        ProcessRunRecordSupersession supersession,
        CancellationToken cancellationToken)
    {
        await NonRelationalMutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entity = await dbContext.RunRecords
                .SingleOrDefaultAsync(record => record.RunId == supersession.RunId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (entity is null || entity.SourceGlobalSequence >= supersession.SourceGlobalSequence)
            {
                return false;
            }

            entity.LifecycleState = ProcessRunRecordLifecycleState.Superseded;
            entity.SourceGlobalSequence = supersession.SourceGlobalSequence;
            entity.SourceRootSequence = supersession.SourceRootSequence;
            if (entity.FactsStatus == ProcessRunFactsStatus.Assembling)
            {
                entity.FactsStatus = ProcessRunFactsStatus.Pending;
            }

            entity.FactsLeaseToken = null;
            entity.FactsLeaseExpiresAtUtc = null;
            if (entity.NarrativeStatus == ProcessRunNarrativeStatus.Generating)
            {
                entity.NarrativeStatus = ProcessRunNarrativeStatus.Pending;
            }

            entity.NarrativeLeaseToken = null;
            entity.NarrativeLeaseExpiresAtUtc = null;
            entity.UpdatedAtUtc = supersession.SupersededAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            NonRelationalMutationGate.Release();
        }
    }

    private async Task<bool> IsCurrentReportableSourceAsync(
        ProcessRunRecordSeed seed,
        CancellationToken cancellationToken)
    {
        var (expectedStatus, expectedEventType) = seed.Disposition switch
        {
            ProcessRunDisposition.Succeeded =>
                (ProcessRuntimeStatus.Completed, ProcessRuntimeEventTypes.ProcessRunCompleted.Value),
            ProcessRunDisposition.Failed =>
                (ProcessRuntimeStatus.Failed, ProcessRuntimeEventTypes.ProcessRunFailed.Value),
            ProcessRunDisposition.Cancelled =>
                (ProcessRuntimeStatus.Cancelled, ProcessRuntimeEventTypes.ProcessRunCancelled.Value),
            ProcessRunDisposition.Blocked =>
                (ProcessRuntimeStatus.Blocked, ProcessRuntimeEventTypes.ProcessRunBlocked.Value),
            ProcessRunDisposition.Escalated => throw new InvalidOperationException(
                "A backfill seed cannot validate an explicit escalated disposition without a canonical run escalation event."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(seed),
                seed.Disposition,
                "Process run record seed disposition is not supported.")
        };
        var runId = seed.Identity.RunId.Value;
        var stateMatches = await dbContext.RuntimeStates
            .AsNoTracking()
            .AnyAsync(
                state => state.RunId == runId && state.Status == expectedStatus,
                cancellationToken)
            .ConfigureAwait(false);
        if (!stateMatches)
        {
            return false;
        }

        var completedEventType = ProcessRuntimeEventTypes.ProcessRunCompleted.Value;
        var failedEventType = ProcessRuntimeEventTypes.ProcessRunFailed.Value;
        var cancelledEventType = ProcessRuntimeEventTypes.ProcessRunCancelled.Value;
        var blockedEventType = ProcessRuntimeEventTypes.ProcessRunBlocked.Value;
        var reactivatedEventType = ProcessRuntimeEventTypes.ProcessRunReactivated.Value;
        var latestLifecycleEvent = await dbContext.RuntimeEvents
            .AsNoTracking()
            .Where(runtimeEvent =>
                runtimeEvent.RunId == runId &&
                (runtimeEvent.EventType == completedEventType ||
                 runtimeEvent.EventType == failedEventType ||
                 runtimeEvent.EventType == cancelledEventType ||
                 runtimeEvent.EventType == blockedEventType ||
                 runtimeEvent.EventType == reactivatedEventType))
            .OrderByDescending(runtimeEvent => runtimeEvent.GlobalSequence)
            .Select(runtimeEvent => new
            {
                runtimeEvent.EventType,
                runtimeEvent.GlobalSequence,
                runtimeEvent.RootSequence
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return latestLifecycleEvent is not null &&
               string.Equals(
                   latestLifecycleEvent.EventType,
                   expectedEventType,
                   StringComparison.Ordinal) &&
               latestLifecycleEvent.GlobalSequence == seed.SourceGlobalSequence &&
               latestLifecycleEvent.RootSequence == seed.SourceRootSequence;
    }

    private static long CreateAdvisoryLockKey(Guid runId)
    {
        Span<byte> bytes = stackalloc byte[16];
        runId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^
               BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
    }

    private async Task AcquireRunMutationLockAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        var lockKey = CreateAdvisoryLockKey(runId);
        await dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProcessRunRecordEntity CreateSeedEntity(ProcessRunRecordSeed seed)
    {
        var entity = new ProcessRunRecordEntity
        {
            RunId = seed.Identity.RunId.Value
        };
        ResetSeedEntity(entity, seed);
        return entity;
    }

    private static void ResetSeedEntity(ProcessRunRecordEntity entity, ProcessRunRecordSeed seed)
    {
        var identity = seed.Identity;
        entity.RootRunId = identity.RootRunId.Value;
        entity.ParentRunId = identity.ParentRunId?.Value;
        entity.PlanId = identity.PlanId?.Value;
        entity.DefinitionId = identity.DefinitionId?.Value;
        entity.DefinitionVersionId = identity.DefinitionVersionId?.Value;
        entity.ProjectId = identity.ProjectId;
        entity.Disposition = seed.Disposition;
        entity.LifecycleState = ProcessRunRecordLifecycleState.Current;
        entity.Completeness = ProcessRunRecordCompleteness.SeedOnly;
        entity.AvailableEvidenceSources = ProcessRunEvidenceSource.None;
        entity.MissingEvidenceSources = ProcessRunEvidenceSource.All;
        entity.CompletenessWarningsJson = "[]";
        entity.StartedAtUtc = null;
        entity.EndedAtUtc = seed.EndedAtUtc;
        entity.DurationMilliseconds = null;
        entity.TotalStepCount = 0;
        entity.ExecutableStepCount = 0;
        entity.CompletedStepCount = 0;
        entity.FailedStepCount = 0;
        entity.CancelledStepCount = 0;
        entity.RepetitionCount = 0;
        entity.ExecutionCount = 0;
        entity.ReworkCount = 0;
        entity.IncidentCount = 0;
        entity.EscalationCount = 0;
        entity.InputTokenCount = 0;
        entity.CachedInputTokenCount = 0;
        entity.OutputTokenCount = 0;
        entity.ReasoningTokenCount = 0;
        entity.TotalTokenCount = 0;
        entity.EstimatedCost = 0;
        entity.ActualCost = 0;
        entity.ToolCallCount = 0;
        entity.ArtifactCount = 0;
        entity.SubprocessCount = 0;
        entity.FactsJson = null;
        entity.ParticipantIdsJson = "[]";
        entity.FactsStatus = ProcessRunFactsStatus.Pending;
        entity.FactsLeaseToken = null;
        entity.FactsLeaseExpiresAtUtc = null;
        entity.FactsAttemptCount = 0;
        entity.FactsNextAttemptAtUtc = null;
        entity.FactsLastErrorClass = null;
        entity.FactsLastErrorDiagnosticReference = null;
        entity.NarrativeJson = null;
        entity.NarrativeStatus = ProcessRunNarrativeStatus.Pending;
        entity.NarrativeLeaseToken = null;
        entity.NarrativeLeaseExpiresAtUtc = null;
        entity.NarrativeAttemptCount = 0;
        entity.NarrativeNextAttemptAtUtc = null;
        entity.NarrativeLastErrorClass = null;
        entity.NarrativeLastErrorDiagnosticReference = null;
        entity.SourceGlobalSequence = seed.SourceGlobalSequence;
        entity.SourceRootSequence = seed.SourceRootSequence;
        entity.SchemaVersion = seed.SchemaVersion;
        entity.UpdatedAtUtc = seed.ObservedAtUtc;
    }

    private IQueryable<ProcessRunRecordEntity> ApplyListFilters(
        IQueryable<ProcessRunRecordEntity> recordsQuery,
        ProcessRunRecordListQuery query)
    {
        if (!query.IncludeSuperseded)
        {
            recordsQuery = recordsQuery.Where(record => record.LifecycleState == ProcessRunRecordLifecycleState.Current);
        }

        if (query.RunIds.Count > 0)
        {
            var runIds = query.RunIds.Select(runId => runId.Value).Distinct().ToArray();
            recordsQuery = recordsQuery.Where(record => runIds.Contains(record.RunId));
        }

        if (query.RootRunsOnly)
        {
            recordsQuery = recordsQuery.Where(record => record.RunId == record.RootRunId);
        }

        if (query.ProjectId is { } projectId)
        {
            recordsQuery = recordsQuery.Where(record => record.ProjectId == projectId);
        }

        if (query.DefinitionId is { } definitionId)
        {
            recordsQuery = recordsQuery.Where(record => record.DefinitionId == definitionId.Value);
        }

        if (query.RootRunId is { } rootRunId)
        {
            recordsQuery = recordsQuery.Where(record => record.RootRunId == rootRunId.Value);
        }

        if (query.Disposition is { } disposition)
        {
            recordsQuery = recordsQuery.Where(record => record.Disposition == disposition);
        }

        if (query.ParticipantId is { } participantId)
        {
            var participantRunIds = dbContext.RunRecordParticipants
                .AsNoTracking()
                .Where(participant => participant.ParticipantId == participantId.Value)
                .Select(participant => participant.RunId);
            recordsQuery = recordsQuery.Where(record => participantRunIds.Contains(record.RunId));
        }

        if (query.EndedFromUtc is { } endedFromUtc)
        {
            recordsQuery = recordsQuery.Where(record => record.EndedAtUtc >= endedFromUtc);
        }

        if (query.EndedBeforeUtc is { } endedBeforeUtc)
        {
            recordsQuery = recordsQuery.Where(record => record.EndedAtUtc < endedBeforeUtc);
        }

        if (query.Cursor is { } cursor)
        {
            recordsQuery = recordsQuery.Where(record =>
                record.EndedAtUtc < cursor.EndedAtUtc ||
                (record.EndedAtUtc == cursor.EndedAtUtc &&
                 record.RunId.CompareTo(cursor.RunId.Value) < 0));
        }

        return recordsQuery;
    }

    private static IQueryable<ProcessRunRecordEntity> SelectSummaryColumns(
        IQueryable<ProcessRunRecordEntity> recordsQuery)
    {
        return recordsQuery.Select(record => new ProcessRunRecordEntity
        {
            RunId = record.RunId,
            RootRunId = record.RootRunId,
            ParentRunId = record.ParentRunId,
            PlanId = record.PlanId,
            DefinitionId = record.DefinitionId,
            DefinitionVersionId = record.DefinitionVersionId,
            ProjectId = record.ProjectId,
            Disposition = record.Disposition,
            LifecycleState = record.LifecycleState,
            Completeness = record.Completeness,
            AvailableEvidenceSources = record.AvailableEvidenceSources,
            MissingEvidenceSources = record.MissingEvidenceSources,
            CompletenessWarningsJson = record.CompletenessWarningsJson,
            StartedAtUtc = record.StartedAtUtc,
            EndedAtUtc = record.EndedAtUtc,
            DurationMilliseconds = record.DurationMilliseconds,
            TotalStepCount = record.TotalStepCount,
            ExecutableStepCount = record.ExecutableStepCount,
            CompletedStepCount = record.CompletedStepCount,
            FailedStepCount = record.FailedStepCount,
            CancelledStepCount = record.CancelledStepCount,
            RepetitionCount = record.RepetitionCount,
            ExecutionCount = record.ExecutionCount,
            ReworkCount = record.ReworkCount,
            IncidentCount = record.IncidentCount,
            EscalationCount = record.EscalationCount,
            InputTokenCount = record.InputTokenCount,
            CachedInputTokenCount = record.CachedInputTokenCount,
            OutputTokenCount = record.OutputTokenCount,
            ReasoningTokenCount = record.ReasoningTokenCount,
            TotalTokenCount = record.TotalTokenCount,
            EstimatedCost = record.EstimatedCost,
            ActualCost = record.ActualCost,
            ToolCallCount = record.ToolCallCount,
            ArtifactCount = record.ArtifactCount,
            SubprocessCount = record.SubprocessCount,
            ParticipantIdsJson = "[]",
            FactsStatus = record.FactsStatus,
            FactsAttemptCount = record.FactsAttemptCount,
            FactsNextAttemptAtUtc = record.FactsNextAttemptAtUtc,
            FactsLastErrorClass = record.FactsLastErrorClass,
            FactsLastErrorDiagnosticReference = record.FactsLastErrorDiagnosticReference,
            NarrativeJson = null,
            NarrativeStatus = record.NarrativeStatus,
            NarrativeAttemptCount = record.NarrativeAttemptCount,
            NarrativeNextAttemptAtUtc = record.NarrativeNextAttemptAtUtc,
            NarrativeLastErrorClass = record.NarrativeLastErrorClass,
            NarrativeLastErrorDiagnosticReference = record.NarrativeLastErrorDiagnosticReference,
            SourceGlobalSequence = record.SourceGlobalSequence,
            SourceRootSequence = record.SourceRootSequence,
            SchemaVersion = record.SchemaVersion,
            UpdatedAtUtc = record.UpdatedAtUtc
        });
    }

    private async Task HydrateFullSummaryPayloadAsync(
        IReadOnlyList<ProcessRunRecordEntity> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var runIds = rows.Select(row => row.RunId).ToArray();
        var payloads = await dbContext.RunRecords
            .AsNoTracking()
            .Where(record => runIds.Contains(record.RunId))
            .Select(record => new
            {
                record.RunId,
                record.ParticipantIdsJson,
                record.NarrativeJson
            })
            .ToDictionaryAsync(payload => payload.RunId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            if (!payloads.TryGetValue(row.RunId, out var payload))
            {
                throw new InvalidOperationException(
                    $"Process run record '{row.RunId}' disappeared while its full list payload was loading.");
            }

            row.ParticipantIdsJson = payload.ParticipantIdsJson;
            row.NarrativeJson = payload.NarrativeJson;
        }
    }

    private async Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsPostgreSqlAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken)
    {
        const string commandText =
            """
            WITH due AS (
                SELECT record."RunId"
                FROM process_run_records AS record
                WHERE record."LifecycleState" = @currentLifecycle
                  AND (
                      (
                          record."FactsStatus" = @pendingStatus
                          AND (
                              record."FactsNextAttemptAtUtc" IS NULL
                              OR record."FactsNextAttemptAtUtc" <= @now
                          )
                      )
                      OR (
                          record."FactsStatus" = @failedStatus
                          AND record."FactsNextAttemptAtUtc" IS NOT NULL
                          AND record."FactsNextAttemptAtUtc" <= @now
                      )
                      OR (
                          record."FactsStatus" = @activeStatus
                          AND (
                              record."FactsLeaseExpiresAtUtc" IS NULL
                              OR record."FactsLeaseExpiresAtUtc" <= @now
                          )
                      )
                  )
                ORDER BY COALESCE(record."FactsNextAttemptAtUtc", record."EndedAtUtc"),
                         record."EndedAtUtc",
                         record."RunId"
                FOR UPDATE SKIP LOCKED
                LIMIT @take
            )
            UPDATE process_run_records AS record
            SET "FactsStatus" = @activeStatus,
                "FactsLeaseToken" = @leaseToken,
                "FactsLeaseExpiresAtUtc" = @leaseExpiresAtUtc,
                "FactsAttemptCount" = record."FactsAttemptCount" + 1,
                "FactsNextAttemptAtUtc" = NULL,
                "UpdatedAtUtc" = @now
            FROM due
            WHERE record."RunId" = due."RunId"
            RETURNING record."RunId",
                      record."SourceGlobalSequence",
                      record."FactsAttemptCount";
            """;
        var token = ProcessRunRecordClaimToken.New();
        var leaseExpiresAtUtc = request.NowUtc.Add(request.LeaseDuration);
        var claims = new List<ProcessRunFactsClaim>();
        await ExecuteClaimCommandAsync(
            commandText,
            request,
            token,
            leaseExpiresAtUtc,
            ProcessRunRecordStage.Facts,
            reader =>
            {
                claims.Add(new ProcessRunFactsClaim(
                    new ProcessRunId(reader.GetGuid(0)),
                    reader.GetInt64(1),
                    token,
                    leaseExpiresAtUtc,
                    reader.GetInt32(2)));
            },
            cancellationToken).ConfigureAwait(false);
        return claims;
    }

    private async Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesPostgreSqlAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken)
    {
        const string commandText =
            """
            WITH due AS (
                SELECT record."RunId"
                FROM process_run_records AS record
                WHERE record."LifecycleState" = @currentLifecycle
                  AND record."FactsStatus" = @factsCompletedStatus
                  AND (
                      (
                          record."NarrativeStatus" = @pendingStatus
                          AND (
                              record."NarrativeNextAttemptAtUtc" IS NULL
                              OR record."NarrativeNextAttemptAtUtc" <= @now
                          )
                      )
                      OR (
                          record."NarrativeStatus" = @failedStatus
                          AND record."NarrativeNextAttemptAtUtc" IS NOT NULL
                          AND record."NarrativeNextAttemptAtUtc" <= @now
                      )
                      OR (
                          record."NarrativeStatus" = @activeStatus
                          AND (
                              record."NarrativeLeaseExpiresAtUtc" IS NULL
                              OR record."NarrativeLeaseExpiresAtUtc" <= @now
                          )
                      )
                  )
                ORDER BY COALESCE(record."NarrativeNextAttemptAtUtc", record."EndedAtUtc"),
                         record."EndedAtUtc",
                         record."RunId"
                FOR UPDATE SKIP LOCKED
                LIMIT @take
            )
            UPDATE process_run_records AS record
            SET "NarrativeStatus" = @activeStatus,
                "NarrativeLeaseToken" = @leaseToken,
                "NarrativeLeaseExpiresAtUtc" = @leaseExpiresAtUtc,
                "NarrativeAttemptCount" = record."NarrativeAttemptCount" + 1,
                "NarrativeNextAttemptAtUtc" = NULL,
                "UpdatedAtUtc" = @now
            FROM due
            WHERE record."RunId" = due."RunId"
            RETURNING record."RunId",
                      record."SourceGlobalSequence",
                      record."NarrativeAttemptCount";
            """;
        var token = ProcessRunRecordClaimToken.New();
        var leaseExpiresAtUtc = request.NowUtc.Add(request.LeaseDuration);
        var claims = new List<ProcessRunNarrativeClaim>();
        await ExecuteClaimCommandAsync(
            commandText,
            request,
            token,
            leaseExpiresAtUtc,
            ProcessRunRecordStage.Narrative,
            reader =>
            {
                claims.Add(new ProcessRunNarrativeClaim(
                    new ProcessRunId(reader.GetGuid(0)),
                    reader.GetInt64(1),
                    token,
                    leaseExpiresAtUtc,
                    reader.GetInt32(2)));
            },
            cancellationToken).ConfigureAwait(false);
        return claims;
    }

    private async Task ExecuteClaimCommandAsync(
        string commandText,
        ProcessRunRecordClaimRequest request,
        ProcessRunRecordClaimToken token,
        DateTimeOffset leaseExpiresAtUtc,
        ProcessRunRecordStage stage,
        Action<DbDataReader> readRow,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            AddParameter(command, "@currentLifecycle", ProcessRunRecordLifecycleState.Current.ToString());
            if (stage == ProcessRunRecordStage.Narrative)
            {
                AddParameter(command, "@factsCompletedStatus", ProcessRunFactsStatus.Completed.ToString());
            }

            AddParameter(
                command,
                "@pendingStatus",
                stage == ProcessRunRecordStage.Facts
                    ? ProcessRunFactsStatus.Pending.ToString()
                    : ProcessRunNarrativeStatus.Pending.ToString());
            AddParameter(
                command,
                "@failedStatus",
                stage == ProcessRunRecordStage.Facts
                    ? ProcessRunFactsStatus.Failed.ToString()
                    : ProcessRunNarrativeStatus.Failed.ToString());
            AddParameter(
                command,
                "@activeStatus",
                stage == ProcessRunRecordStage.Facts
                    ? ProcessRunFactsStatus.Assembling.ToString()
                    : ProcessRunNarrativeStatus.Generating.ToString());
            AddParameter(command, "@now", request.NowUtc);
            AddParameter(command, "@leaseToken", token.Value);
            AddParameter(command, "@leaseExpiresAtUtc", leaseExpiresAtUtc);
            AddParameter(command, "@take", request.Take);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                readRow(reader);
            }
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<IReadOnlyList<ProcessRunFactsClaim>> ClaimFactsNonRelationalAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken)
    {
        await NonRelationalMutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var due = await dbContext.RunRecords
                .Where(record =>
                    record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                    ((record.FactsStatus == ProcessRunFactsStatus.Pending &&
                      (record.FactsNextAttemptAtUtc == null || record.FactsNextAttemptAtUtc <= request.NowUtc)) ||
                     (record.FactsStatus == ProcessRunFactsStatus.Failed &&
                      record.FactsNextAttemptAtUtc != null &&
                      record.FactsNextAttemptAtUtc <= request.NowUtc) ||
                     (record.FactsStatus == ProcessRunFactsStatus.Assembling &&
                      (record.FactsLeaseExpiresAtUtc == null || record.FactsLeaseExpiresAtUtc <= request.NowUtc))))
                .OrderBy(record => record.FactsNextAttemptAtUtc ?? record.EndedAtUtc)
                .ThenBy(record => record.EndedAtUtc)
                .ThenBy(record => record.RunId)
                .Take(request.Take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var leaseExpiresAtUtc = request.NowUtc.Add(request.LeaseDuration);
            var claims = new List<ProcessRunFactsClaim>(due.Count);
            foreach (var entity in due)
            {
                var token = ProcessRunRecordClaimToken.New();
                entity.FactsStatus = ProcessRunFactsStatus.Assembling;
                entity.FactsLeaseToken = token.Value;
                entity.FactsLeaseExpiresAtUtc = leaseExpiresAtUtc;
                entity.FactsAttemptCount++;
                entity.FactsNextAttemptAtUtc = null;
                entity.UpdatedAtUtc = request.NowUtc;
                claims.Add(new ProcessRunFactsClaim(
                    new ProcessRunId(entity.RunId),
                    entity.SourceGlobalSequence,
                    token,
                    leaseExpiresAtUtc,
                    entity.FactsAttemptCount));
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return claims;
        }
        finally
        {
            NonRelationalMutationGate.Release();
        }
    }

    private async Task<IReadOnlyList<ProcessRunNarrativeClaim>> ClaimNarrativesNonRelationalAsync(
        ProcessRunRecordClaimRequest request,
        CancellationToken cancellationToken)
    {
        await NonRelationalMutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var due = await dbContext.RunRecords
                .Where(record =>
                    record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                    record.FactsStatus == ProcessRunFactsStatus.Completed &&
                    ((record.NarrativeStatus == ProcessRunNarrativeStatus.Pending &&
                      (record.NarrativeNextAttemptAtUtc == null || record.NarrativeNextAttemptAtUtc <= request.NowUtc)) ||
                     (record.NarrativeStatus == ProcessRunNarrativeStatus.Failed &&
                      record.NarrativeNextAttemptAtUtc != null &&
                      record.NarrativeNextAttemptAtUtc <= request.NowUtc) ||
                     (record.NarrativeStatus == ProcessRunNarrativeStatus.Generating &&
                      (record.NarrativeLeaseExpiresAtUtc == null ||
                       record.NarrativeLeaseExpiresAtUtc <= request.NowUtc))))
                .OrderBy(record => record.NarrativeNextAttemptAtUtc ?? record.EndedAtUtc)
                .ThenBy(record => record.EndedAtUtc)
                .ThenBy(record => record.RunId)
                .Take(request.Take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var leaseExpiresAtUtc = request.NowUtc.Add(request.LeaseDuration);
            var claims = new List<ProcessRunNarrativeClaim>(due.Count);
            foreach (var entity in due)
            {
                var token = ProcessRunRecordClaimToken.New();
                entity.NarrativeStatus = ProcessRunNarrativeStatus.Generating;
                entity.NarrativeLeaseToken = token.Value;
                entity.NarrativeLeaseExpiresAtUtc = leaseExpiresAtUtc;
                entity.NarrativeAttemptCount++;
                entity.NarrativeNextAttemptAtUtc = null;
                entity.UpdatedAtUtc = request.NowUtc;
                claims.Add(new ProcessRunNarrativeClaim(
                    new ProcessRunId(entity.RunId),
                    entity.SourceGlobalSequence,
                    token,
                    leaseExpiresAtUtc,
                    entity.NarrativeAttemptCount));
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return claims;
        }
        finally
        {
            NonRelationalMutationGate.Release();
        }
    }

    private async Task<bool> CompleteFactsNonRelationalAsync(
        ProcessRunFactsCompletion completion,
        string factsJson,
        string participantIdsJson,
        string completenessWarningsJson,
        IReadOnlyList<string> participantIds,
        CancellationToken cancellationToken)
    {
        await NonRelationalMutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entity = await dbContext.RunRecords
                .SingleOrDefaultAsync(
                    record =>
                        record.RunId == completion.Identity.RunId.Value &&
                        record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                        record.SourceGlobalSequence == completion.SourceGlobalSequence &&
                        record.FactsStatus == ProcessRunFactsStatus.Assembling &&
                        record.FactsLeaseToken == completion.ClaimToken.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return false;
            }

            ApplyFactsCompletion(
                entity,
                completion,
                factsJson,
                participantIdsJson,
                completenessWarningsJson);
            var existingParticipants = await dbContext.RunRecordParticipants
                .Where(participant => participant.RunId == completion.Identity.RunId.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            dbContext.RunRecordParticipants.RemoveRange(existingParticipants);
            dbContext.RunRecordParticipants.AddRange(participantIds.Select(participantId =>
                new ProcessRunRecordParticipantEntity
                {
                    ParticipantId = participantId,
                    RunId = completion.Identity.RunId.Value
                }));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            NonRelationalMutationGate.Release();
        }
    }

    private static void ApplyFactsCompletion(
        ProcessRunRecordEntity entity,
        ProcessRunFactsCompletion completion,
        string factsJson,
        string participantIdsJson,
        string completenessWarningsJson)
    {
        var identity = completion.Identity;
        var metrics = completion.Metrics;
        entity.RootRunId = identity.RootRunId.Value;
        entity.ParentRunId = identity.ParentRunId?.Value;
        entity.PlanId = identity.PlanId?.Value;
        entity.DefinitionId = identity.DefinitionId?.Value;
        entity.DefinitionVersionId = identity.DefinitionVersionId?.Value;
        entity.ProjectId = identity.ProjectId;
        entity.Completeness = completion.Completeness;
        entity.AvailableEvidenceSources = completion.AvailableEvidenceSources;
        entity.MissingEvidenceSources = completion.MissingEvidenceSources;
        entity.CompletenessWarningsJson = completenessWarningsJson;
        entity.StartedAtUtc = metrics.StartedAtUtc;
        entity.EndedAtUtc = metrics.EndedAtUtc;
        entity.DurationMilliseconds = metrics.DurationMilliseconds;
        entity.TotalStepCount = metrics.TotalStepCount;
        entity.ExecutableStepCount = metrics.ExecutableStepCount;
        entity.CompletedStepCount = metrics.CompletedStepCount;
        entity.FailedStepCount = metrics.FailedStepCount;
        entity.CancelledStepCount = metrics.CancelledStepCount;
        entity.RepetitionCount = metrics.RepetitionCount;
        entity.ExecutionCount = metrics.ExecutionCount;
        entity.ReworkCount = metrics.ReworkCount;
        entity.IncidentCount = metrics.IncidentCount;
        entity.EscalationCount = metrics.EscalationCount;
        entity.InputTokenCount = metrics.InputTokenCount;
        entity.CachedInputTokenCount = metrics.CachedInputTokenCount;
        entity.OutputTokenCount = metrics.OutputTokenCount;
        entity.ReasoningTokenCount = metrics.ReasoningTokenCount;
        entity.TotalTokenCount = metrics.TotalTokenCount;
        entity.EstimatedCost = metrics.EstimatedCost;
        entity.ActualCost = metrics.ActualCost;
        entity.ToolCallCount = metrics.ToolCallCount;
        entity.ArtifactCount = metrics.ArtifactCount;
        entity.SubprocessCount = metrics.SubprocessCount;
        entity.FactsJson = factsJson;
        entity.ParticipantIdsJson = participantIdsJson;
        entity.FactsStatus = ProcessRunFactsStatus.Completed;
        entity.FactsLeaseToken = null;
        entity.FactsLeaseExpiresAtUtc = null;
        entity.FactsNextAttemptAtUtc = null;
        entity.FactsLastErrorClass = null;
        entity.FactsLastErrorDiagnosticReference = null;
        entity.UpdatedAtUtc = completion.CompletedAtUtc;
    }

    private async Task<bool> CompleteNarrativeNonRelationalAsync(
        ProcessRunNarrativeCompletion completion,
        string narrativeJson,
        CancellationToken cancellationToken)
    {
        await NonRelationalMutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entity = await dbContext.RunRecords
                .SingleOrDefaultAsync(
                    record =>
                        record.RunId == completion.RunId.Value &&
                        record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                        record.SourceGlobalSequence == completion.SourceGlobalSequence &&
                        record.FactsStatus == ProcessRunFactsStatus.Completed &&
                        record.NarrativeStatus == ProcessRunNarrativeStatus.Generating &&
                        record.NarrativeLeaseToken == completion.ClaimToken.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return false;
            }

            entity.NarrativeJson = narrativeJson;
            entity.NarrativeStatus = ProcessRunNarrativeStatus.Completed;
            entity.NarrativeLeaseToken = null;
            entity.NarrativeLeaseExpiresAtUtc = null;
            entity.NarrativeNextAttemptAtUtc = null;
            entity.NarrativeLastErrorClass = null;
            entity.NarrativeLastErrorDiagnosticReference = null;
            entity.UpdatedAtUtc = completion.CompletedAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            NonRelationalMutationGate.Release();
        }
    }

    private async Task<bool> FailStageAsync(
        ProcessRunStageFailure failure,
        ProcessRunRecordStage stage,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await FailStageNonRelationalAsync(failure, stage, cancellationToken).ConfigureAwait(false);
        }

        var recordsQuery = dbContext.RunRecords.Where(record =>
            record.RunId == failure.RunId.Value &&
            record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
            record.SourceGlobalSequence == failure.SourceGlobalSequence);
        int updatedRows;
        if (stage == ProcessRunRecordStage.Facts)
        {
            updatedRows = await recordsQuery
                .Where(record =>
                    record.FactsStatus == ProcessRunFactsStatus.Assembling &&
                    record.FactsLeaseToken == failure.ClaimToken.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(record => record.FactsStatus, ProcessRunFactsStatus.Failed)
                    .SetProperty(record => record.FactsLeaseToken, (Guid?)null)
                    .SetProperty(record => record.FactsLeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(
                        record => record.FactsAttemptCount,
                        record => failure.ConsumesAttempt || record.FactsAttemptCount == 0
                            ? record.FactsAttemptCount
                            : record.FactsAttemptCount - 1)
                    .SetProperty(record => record.FactsNextAttemptAtUtc, failure.NextAttemptAtUtc)
                    .SetProperty(record => record.FactsLastErrorClass, failure.ErrorClass)
                    .SetProperty(
                        record => record.FactsLastErrorDiagnosticReference,
                        failure.DiagnosticReference)
                    .SetProperty(record => record.UpdatedAtUtc, failure.FailedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            updatedRows = await recordsQuery
                .Where(record =>
                    record.NarrativeStatus == ProcessRunNarrativeStatus.Generating &&
                    record.NarrativeLeaseToken == failure.ClaimToken.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(record => record.NarrativeStatus, ProcessRunNarrativeStatus.Failed)
                    .SetProperty(record => record.NarrativeLeaseToken, (Guid?)null)
                    .SetProperty(record => record.NarrativeLeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(
                        record => record.NarrativeAttemptCount,
                        record => failure.ConsumesAttempt || record.NarrativeAttemptCount == 0
                            ? record.NarrativeAttemptCount
                            : record.NarrativeAttemptCount - 1)
                    .SetProperty(record => record.NarrativeNextAttemptAtUtc, failure.NextAttemptAtUtc)
                    .SetProperty(record => record.NarrativeLastErrorClass, failure.ErrorClass)
                    .SetProperty(
                        record => record.NarrativeLastErrorDiagnosticReference,
                        failure.DiagnosticReference)
                    .SetProperty(record => record.UpdatedAtUtc, failure.FailedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        dbContext.ChangeTracker.Clear();
        return updatedRows > 0;
    }

    private async Task<bool> FailStageNonRelationalAsync(
        ProcessRunStageFailure failure,
        ProcessRunRecordStage stage,
        CancellationToken cancellationToken)
    {
        await NonRelationalMutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entity = await dbContext.RunRecords
                .SingleOrDefaultAsync(
                    record =>
                        record.RunId == failure.RunId.Value &&
                        record.LifecycleState == ProcessRunRecordLifecycleState.Current &&
                        record.SourceGlobalSequence == failure.SourceGlobalSequence,
                    cancellationToken)
                .ConfigureAwait(false);
            if (entity is null)
            {
                return false;
            }

            if (stage == ProcessRunRecordStage.Facts)
            {
                if (entity.FactsStatus != ProcessRunFactsStatus.Assembling ||
                    entity.FactsLeaseToken != failure.ClaimToken.Value)
                {
                    return false;
                }

                entity.FactsStatus = ProcessRunFactsStatus.Failed;
                entity.FactsLeaseToken = null;
                entity.FactsLeaseExpiresAtUtc = null;
                if (!failure.ConsumesAttempt && entity.FactsAttemptCount > 0)
                {
                    entity.FactsAttemptCount--;
                }

                entity.FactsNextAttemptAtUtc = failure.NextAttemptAtUtc;
                entity.FactsLastErrorClass = failure.ErrorClass;
                entity.FactsLastErrorDiagnosticReference = failure.DiagnosticReference;
            }
            else
            {
                if (entity.NarrativeStatus != ProcessRunNarrativeStatus.Generating ||
                    entity.NarrativeLeaseToken != failure.ClaimToken.Value)
                {
                    return false;
                }

                entity.NarrativeStatus = ProcessRunNarrativeStatus.Failed;
                entity.NarrativeLeaseToken = null;
                entity.NarrativeLeaseExpiresAtUtc = null;
                if (!failure.ConsumesAttempt && entity.NarrativeAttemptCount > 0)
                {
                    entity.NarrativeAttemptCount--;
                }

                entity.NarrativeNextAttemptAtUtc = failure.NextAttemptAtUtc;
                entity.NarrativeLastErrorClass = failure.ErrorClass;
                entity.NarrativeLastErrorDiagnosticReference = failure.DiagnosticReference;
            }

            entity.UpdatedAtUtc = failure.FailedAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            NonRelationalMutationGate.Release();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private enum ProcessRunRecordStage
    {
        Facts,
        Narrative
    }

    private sealed record ProcessRunRecordAnalyticsGroup(
        ProcessRunDisposition Disposition,
        bool FactsAvailable,
        ProcessRunRecordCompleteness Completeness,
        int MatchingRunCount,
        DateTimeOffset LatestEndedAtUtc,
        long MaximumSourceGlobalSequence,
        long DurationMilliseconds,
        long InputTokenCount,
        long CachedInputTokenCount,
        long OutputTokenCount,
        long ReasoningTokenCount,
        long TotalTokenCount,
        decimal EstimatedCost,
        decimal ActualCost,
        int RepetitionCount,
        int ExecutionCount,
        int ReworkCount,
        int IncidentCount,
        int EscalationCount,
        int ToolCallCount,
        int ArtifactCount);

}
