using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessRunRecordPersistenceCodec
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static ProcessRunRecord MapRecord(ProcessRunRecordEntity entity)
    {
        var facts = entity.FactsJson is null
            ? null
            : Deserialize<ProcessRunHardFacts>(entity.FactsJson, nameof(entity.FactsJson));
        return new ProcessRunRecord(MapSummary(entity), facts);
    }

    public static ProcessRunRecordSummary MapSummary(ProcessRunRecordEntity entity)
    {
        var identity = new ProcessRunRecordIdentity(
            new ProcessRunId(entity.RunId),
            new ProcessRunId(entity.RootRunId),
            entity.ParentRunId is { } parentRunId ? new ProcessRunId(parentRunId) : null,
            entity.PlanId is { } planId ? new ProcessInstancePlanId(planId) : null,
            entity.DefinitionId is { } definitionId ? new ProcessDefinitionId(definitionId) : null,
            entity.DefinitionVersionId is { } definitionVersionId
                ? new ProcessDefinitionVersionId(definitionVersionId)
                : null,
            entity.ProjectId);
        var metrics = new ProcessRunRecordMetrics(
            entity.StartedAtUtc,
            entity.EndedAtUtc,
            entity.DurationMilliseconds,
            entity.TotalStepCount,
            entity.ExecutableStepCount,
            entity.CompletedStepCount,
            entity.FailedStepCount,
            entity.CancelledStepCount,
            entity.RepetitionCount,
            entity.ExecutionCount,
            entity.ReworkCount,
            entity.IncidentCount,
            entity.EscalationCount,
            entity.InputTokenCount,
            entity.CachedInputTokenCount,
            entity.OutputTokenCount,
            entity.ReasoningTokenCount,
            entity.TotalTokenCount,
            entity.EstimatedCost,
            entity.ActualCost,
            entity.ToolCallCount,
            entity.ArtifactCount,
            entity.SubprocessCount);
        var participants = Deserialize<IReadOnlyList<ProcessRunParticipantId>>(
            entity.ParticipantIdsJson,
            nameof(entity.ParticipantIdsJson));
        var warnings = Deserialize<IReadOnlyList<ProcessRunRecordWarningCode>>(
            entity.CompletenessWarningsJson,
            nameof(entity.CompletenessWarningsJson));
        var narrative = entity.NarrativeJson is null
            ? null
            : Deserialize<ProcessRunNarrative>(entity.NarrativeJson, nameof(entity.NarrativeJson));

        return new ProcessRunRecordSummary(
            identity,
            entity.Disposition,
            entity.LifecycleState,
            entity.Completeness,
            entity.AvailableEvidenceSources,
            entity.MissingEvidenceSources,
            warnings,
            entity.FactsStatus,
            entity.FactsAttemptCount,
            entity.FactsNextAttemptAtUtc,
            entity.FactsLastErrorClass,
            entity.FactsLastErrorDiagnosticReference,
            entity.NarrativeStatus,
            entity.NarrativeAttemptCount,
            entity.NarrativeNextAttemptAtUtc,
            entity.NarrativeLastErrorClass,
            entity.NarrativeLastErrorDiagnosticReference,
            metrics,
            participants,
            narrative,
            entity.SourceGlobalSequence,
            entity.SourceRootSequence,
            entity.SchemaVersion,
            entity.UpdatedAtUtc);
    }

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    public static string SerializeBounded<T>(T value, int maximumBytes, string fieldName)
    {
        var json = Serialize(value);
        var byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > maximumBytes)
        {
            throw new ArgumentOutOfRangeException(
                fieldName,
                byteCount,
                $"Process run record {fieldName} JSON cannot exceed {maximumBytes} UTF-8 bytes.");
        }

        return json;
    }

    public static void ValidateSeed(ProcessRunRecordSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(seed.Identity);
        ValidateSourceSequence(seed.SourceGlobalSequence, nameof(seed.SourceGlobalSequence));
        ValidateSourceSequence(seed.SourceRootSequence, nameof(seed.SourceRootSequence));
        if (!Enum.IsDefined(seed.Validation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(seed),
                seed.Validation,
                "Process run record seed validation mode is not supported.");
        }
        if (string.IsNullOrWhiteSpace(seed.SchemaVersion) || seed.SchemaVersion.Length > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seed.SchemaVersion),
                seed.SchemaVersion,
                "Process run record schema version must contain at most 64 characters.");
        }
    }

    public static void ValidateSourceSequence(long sequence, string parameterName)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                sequence,
                "Process run record source sequence must be positive.");
        }
    }

    public static void ValidatePageSize(int take)
    {
        if (take is <= 0 or > ProcessRunRecordPayloadLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Process run record page size must be between 1 and {ProcessRunRecordPayloadLimits.MaximumPageSize}.");
        }
    }

    public static void ValidateRunIdFilter(IReadOnlyList<ProcessRunId> runIds)
    {
        ArgumentNullException.ThrowIfNull(runIds);
        if (runIds.Count > ProcessRunRecordPayloadLimits.MaximumRunIdFilterCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runIds),
                runIds.Count,
                $"Process run record RunIds filter cannot exceed {ProcessRunRecordPayloadLimits.MaximumRunIdFilterCount} items.");
        }
    }

    public static void ValidateClaimRequest(ProcessRunRecordClaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Take is <= 0 or > ProcessRunRecordPayloadLimits.MaximumClaimBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Take),
                request.Take,
                $"Process run record claim size must be between 1 and {ProcessRunRecordPayloadLimits.MaximumClaimBatchSize}.");
        }

        if (request.LeaseDuration <= TimeSpan.Zero || request.LeaseDuration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.LeaseDuration),
                request.LeaseDuration,
                "Process run record lease duration must be positive and cannot exceed one hour.");
        }
    }

    public static void ValidateFactsCompletion(ProcessRunFactsCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(completion.Identity);
        ArgumentNullException.ThrowIfNull(completion.Metrics);
        ArgumentNullException.ThrowIfNull(completion.Facts);
        ValidateSourceSequence(completion.SourceGlobalSequence, nameof(completion.SourceGlobalSequence));
        if (completion.Completeness == ProcessRunRecordCompleteness.SeedOnly)
        {
            throw new ArgumentException(
                "Completed hard facts cannot retain seed-only completeness.",
                nameof(completion));
        }

        if ((completion.AvailableEvidenceSources & completion.MissingEvidenceSources) != 0)
        {
            throw new ArgumentException(
                "Available and missing process run evidence sources cannot overlap.",
                nameof(completion));
        }

        ValidateCollection(
            completion.CompletenessWarnings,
            ProcessRunRecordPayloadLimits.MaximumCompletenessWarnings,
            nameof(completion.CompletenessWarnings));
        ValidateMetrics(completion.Metrics);
        ValidateFacts(completion.Facts);
    }

    public static void ValidateNarrative(ProcessRunNarrative narrative)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ValidateText(
            narrative.Overview,
            ProcessRunRecordPayloadLimits.MaximumNarrativeOverviewLength,
            nameof(narrative.Overview));
        ValidateText(
            narrative.Outcome,
            ProcessRunRecordPayloadLimits.MaximumNarrativeOverviewLength,
            nameof(narrative.Outcome));
        ValidateNarrativeItems(narrative.WorkCompleted, nameof(narrative.WorkCompleted));
        ValidateNarrativeItems(narrative.Problems, nameof(narrative.Problems));
        ValidateNarrativeItems(narrative.Decisions, nameof(narrative.Decisions));
        ValidateNarrativeItems(narrative.FollowUps, nameof(narrative.FollowUps));
        ArgumentNullException.ThrowIfNull(narrative.Provenance);
        if (narrative.Provenance.NarrativeExecutionRunId == Guid.Empty)
        {
            throw new ArgumentException("Narrative execution run identifier cannot be empty.", nameof(narrative));
        }

        ValidateText(narrative.Provenance.GenerationPolicyId, 256, nameof(narrative.Provenance.GenerationPolicyId));
        ValidateText(narrative.Provenance.ModelId, 256, nameof(narrative.Provenance.ModelId));
    }

    public static void ValidateStageFailure(ProcessRunStageFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ValidateSourceSequence(failure.SourceGlobalSequence, nameof(failure.SourceGlobalSequence));
        ValidateText(failure.ErrorClass, 256, nameof(failure.ErrorClass));
        ValidateText(failure.DiagnosticReference, 512, nameof(failure.DiagnosticReference));
        if (failure.NextAttemptAtUtc is { } nextAttemptAtUtc && nextAttemptAtUtc <= failure.FailedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure.NextAttemptAtUtc),
                nextAttemptAtUtc,
                "Next process run record attempt must be later than the failed attempt.");
        }
    }

    private static T Deserialize<T>(string json, string fieldName)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Process run record {fieldName} payload was null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Process run record {fieldName} payload is invalid JSON.",
                exception);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ProcessRunParticipantIdJsonConverter());
        options.Converters.Add(new GuidIdentifierJsonConverter<ProcessRunId>(
            value => new ProcessRunId(value),
            identifier => identifier.Value));
        options.Converters.Add(new GuidIdentifierJsonConverter<ProcessStepInstanceId>(
            value => new ProcessStepInstanceId(value),
            identifier => identifier.Value));
        options.Converters.Add(new GuidIdentifierJsonConverter<ProcessStepDefinitionId>(
            value => new ProcessStepDefinitionId(value),
            identifier => identifier.Value));
        options.Converters.Add(new GuidIdentifierJsonConverter<ArtifactInstanceId>(
            value => new ArtifactInstanceId(value),
            identifier => identifier.Value));
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void ValidateMetrics(ProcessRunRecordMetrics metrics)
    {
        if (metrics.StartedAtUtc is { } startedAtUtc && metrics.EndedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Process run end time cannot precede its start time.", nameof(metrics));
        }

        ValidateNullableNonNegative(metrics.DurationMilliseconds, nameof(metrics.DurationMilliseconds));
        ValidateNonNegative(metrics.TotalStepCount, nameof(metrics.TotalStepCount));
        ValidateNonNegative(metrics.ExecutableStepCount, nameof(metrics.ExecutableStepCount));
        ValidateNonNegative(metrics.CompletedStepCount, nameof(metrics.CompletedStepCount));
        ValidateNonNegative(metrics.FailedStepCount, nameof(metrics.FailedStepCount));
        ValidateNonNegative(metrics.CancelledStepCount, nameof(metrics.CancelledStepCount));
        ValidateNonNegative(metrics.RepetitionCount, nameof(metrics.RepetitionCount));
        ValidateNonNegative(metrics.ExecutionCount, nameof(metrics.ExecutionCount));
        ValidateNonNegative(metrics.ReworkCount, nameof(metrics.ReworkCount));
        ValidateNonNegative(metrics.IncidentCount, nameof(metrics.IncidentCount));
        ValidateNonNegative(metrics.EscalationCount, nameof(metrics.EscalationCount));
        ValidateNonNegative(metrics.InputTokenCount, nameof(metrics.InputTokenCount));
        ValidateNonNegative(metrics.CachedInputTokenCount, nameof(metrics.CachedInputTokenCount));
        ValidateNonNegative(metrics.OutputTokenCount, nameof(metrics.OutputTokenCount));
        ValidateNonNegative(metrics.ReasoningTokenCount, nameof(metrics.ReasoningTokenCount));
        ValidateNonNegative(metrics.TotalTokenCount, nameof(metrics.TotalTokenCount));
        ValidateNonNegative(metrics.EstimatedCost, nameof(metrics.EstimatedCost));
        ValidateNonNegative(metrics.ActualCost, nameof(metrics.ActualCost));
        ValidateNonNegative(metrics.ToolCallCount, nameof(metrics.ToolCallCount));
        ValidateNonNegative(metrics.ArtifactCount, nameof(metrics.ArtifactCount));
        ValidateNonNegative(metrics.SubprocessCount, nameof(metrics.SubprocessCount));
        if (metrics.ExecutableStepCount > metrics.TotalStepCount ||
            metrics.CompletedStepCount + metrics.FailedStepCount + metrics.CancelledStepCount > metrics.TotalStepCount)
        {
            throw new ArgumentException("Process run step metrics are internally inconsistent.", nameof(metrics));
        }
    }

    private static void ValidateFacts(ProcessRunHardFacts facts)
    {
        ValidateCollection(facts.Steps, ProcessRunRecordPayloadLimits.MaximumSteps, nameof(facts.Steps));
        ValidateCollection(
            facts.ParticipantIds,
            ProcessRunRecordPayloadLimits.MaximumParticipants,
            nameof(facts.ParticipantIds));
        ValidateCollection(
            facts.WorkflowIds,
            ProcessRunRecordPayloadLimits.MaximumWorkflowIds,
            nameof(facts.WorkflowIds));
        ValidateCollection(
            facts.SubprocessRunIds,
            ProcessRunRecordPayloadLimits.MaximumSubprocessRunIds,
            nameof(facts.SubprocessRunIds));
        ValidateCollection(
            facts.ExecutionRunIds,
            ProcessRunRecordPayloadLimits.MaximumExecutionRunIds,
            nameof(facts.ExecutionRunIds));
        ValidateCollection(
            facts.ArtifactIds,
            ProcessRunRecordPayloadLimits.MaximumArtifactIds,
            nameof(facts.ArtifactIds));
        ValidateCollection(
            facts.RuntimeEventMinuteBuckets,
            ProcessRunRecordPayloadLimits.MaximumRuntimeEventMinuteBuckets,
            nameof(facts.RuntimeEventMinuteBuckets));
        ValidateCollection(
            facts.RuntimeEventCategories,
            ProcessRunRecordPayloadLimits.MaximumRuntimeEventCategories,
            nameof(facts.RuntimeEventCategories));
        EnsureUnique(facts.ParticipantIds, nameof(facts.ParticipantIds));
        EnsureUnique(facts.WorkflowIds, nameof(facts.WorkflowIds));
        EnsureUnique(facts.SubprocessRunIds, nameof(facts.SubprocessRunIds));
        EnsureUnique(facts.ExecutionRunIds, nameof(facts.ExecutionRunIds));
        EnsureUnique(facts.ArtifactIds, nameof(facts.ArtifactIds));
        foreach (var step in facts.Steps)
        {
            ValidateStepFact(step);
        }

        ValidateRuntimeEventAggregates(facts);
    }

    private static void ValidateRuntimeEventAggregates(ProcessRunHardFacts facts)
    {
        ValidateNonNegative(
            facts.TotalRuntimeEventCount,
            nameof(facts.TotalRuntimeEventCount));
        ValidateNonNegative(
            facts.ManagerRuntimeEventCount,
            nameof(facts.ManagerRuntimeEventCount));
        if (facts.ManagerRuntimeEventCount > facts.TotalRuntimeEventCount)
        {
            throw new ArgumentException(
                "Manager runtime event count cannot exceed total runtime event count.",
                nameof(facts));
        }

        var previousMinuteUtc = DateTimeOffset.MinValue;
        long bucketEventCount = 0;
        long bucketManagerEventCount = 0;
        foreach (var bucket in facts.RuntimeEventMinuteBuckets)
        {
            ArgumentNullException.ThrowIfNull(bucket);
            if (bucket.MinuteUtc.Offset != TimeSpan.Zero ||
                bucket.MinuteUtc.Second != 0 ||
                bucket.MinuteUtc.Millisecond != 0 ||
                bucket.MinuteUtc.Ticks % TimeSpan.TicksPerSecond != 0)
            {
                throw new ArgumentException(
                    "Runtime event minute buckets must use whole UTC minutes.",
                    nameof(facts));
            }

            if (bucket.MinuteUtc <= previousMinuteUtc)
            {
                throw new ArgumentException(
                    "Runtime event minute buckets must be unique and sorted.",
                    nameof(facts));
            }

            previousMinuteUtc = bucket.MinuteUtc;
            if (bucket.EventCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(facts),
                    bucket.EventCount,
                    "Runtime event minute bucket count must be positive.");
            }

            ValidateNonNegative(
                bucket.ManagerEventCount,
                nameof(bucket.ManagerEventCount));
            ValidateNonNegative(
                bucket.DurationMilliseconds,
                nameof(bucket.DurationMilliseconds));
            if (bucket.ManagerEventCount > bucket.EventCount ||
                bucket.DurationMilliseconds >= 60_000)
            {
                throw new ArgumentException(
                    "Runtime event minute bucket aggregates are inconsistent.",
                    nameof(facts));
            }

            bucketEventCount += bucket.EventCount;
            bucketManagerEventCount += bucket.ManagerEventCount;
        }

        if (bucketEventCount > facts.TotalRuntimeEventCount ||
            bucketManagerEventCount > facts.ManagerRuntimeEventCount ||
            (facts.TotalRuntimeEventCount > 0 &&
             facts.RuntimeEventMinuteBuckets.Count == 0))
        {
            throw new ArgumentException(
                "Runtime event minute buckets do not match the persisted event totals.",
                nameof(facts));
        }

        var categories = new HashSet<ProcessRunRuntimeEventCategory>();
        long categoryEventCount = 0;
        var categoryManagerEventCount = 0;
        foreach (var category in facts.RuntimeEventCategories)
        {
            ArgumentNullException.ThrowIfNull(category);
            if (!Enum.IsDefined(category.Category) ||
                !categories.Add(category.Category))
            {
                throw new ArgumentException(
                    "Runtime event categories must be known and unique.",
                    nameof(facts));
            }

            if (category.EventCount <= 0 ||
                category.FirstOccurredAtUtc.Offset != TimeSpan.Zero ||
                category.LastOccurredAtUtc.Offset != TimeSpan.Zero ||
                category.LastOccurredAtUtc < category.FirstOccurredAtUtc)
            {
                throw new ArgumentException(
                    "Runtime event category aggregates are inconsistent.",
                    nameof(facts));
            }

            categoryEventCount += category.EventCount;
            if (category.Category == ProcessRunRuntimeEventCategory.Manager)
            {
                categoryManagerEventCount = category.EventCount;
            }
        }

        if (categoryEventCount != facts.TotalRuntimeEventCount ||
            categoryManagerEventCount != facts.ManagerRuntimeEventCount)
        {
            throw new ArgumentException(
                "Runtime event categories do not match the persisted event totals.",
                nameof(facts));
        }
    }

    private static void ValidateStepFact(ProcessRunStepFact step)
    {
        ArgumentNullException.ThrowIfNull(step);
        ValidateText(step.StepKey, ProcessRunRecordPayloadLimits.MaximumStepKeyLength, nameof(step.StepKey));
        ValidateCollection(
            step.DependencyStepIds,
            ProcessRunRecordPayloadLimits.MaximumStepDependencyIds,
            nameof(step.DependencyStepIds));
        ValidateCollection(
            step.ExecutionRunIds,
            ProcessRunRecordPayloadLimits.MaximumExecutionRunIds,
            nameof(step.ExecutionRunIds));
        ValidateNonNegative(step.AttemptCount, nameof(step.AttemptCount));
        ValidateNullableNonNegative(step.DurationMilliseconds, nameof(step.DurationMilliseconds));
        ValidateNonNegative(step.InputTokenCount, nameof(step.InputTokenCount));
        ValidateNonNegative(step.CachedInputTokenCount, nameof(step.CachedInputTokenCount));
        ValidateNonNegative(step.OutputTokenCount, nameof(step.OutputTokenCount));
        ValidateNonNegative(step.ReasoningTokenCount, nameof(step.ReasoningTokenCount));
        ValidateNonNegative(step.TotalTokenCount, nameof(step.TotalTokenCount));
        ValidateNonNegative(step.EstimatedCost, nameof(step.EstimatedCost));
        ValidateNonNegative(step.ActualCost, nameof(step.ActualCost));
        ValidateNonNegative(step.ToolCallCount, nameof(step.ToolCallCount));
        ValidateNonNegative(step.ArtifactCount, nameof(step.ArtifactCount));
        if (step.StartedAtUtc is { } startedAtUtc &&
            step.EndedAtUtc is { } endedAtUtc &&
            endedAtUtc < startedAtUtc)
        {
            throw new ArgumentException("Process step end time cannot precede its start time.", nameof(step));
        }
    }

    private static void ValidateNarrativeItems(IReadOnlyList<string> items, string parameterName)
    {
        ValidateCollection(
            items,
            ProcessRunRecordPayloadLimits.MaximumNarrativeItemsPerSection,
            parameterName);
        foreach (var item in items)
        {
            ValidateText(
                item,
                ProcessRunRecordPayloadLimits.MaximumNarrativeItemLength,
                parameterName);
        }
    }

    private static void ValidateText(
        string value,
        int maximumLength,
        string parameterName,
        bool allowEmpty = false)
    {
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) ||
            (allowEmpty && value is null) ||
            value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value?.Length ?? 0,
                $"{parameterName} must contain at most {maximumLength} characters.");
        }
    }

    private static void ValidateCollection<T>(
        IReadOnlyList<T> items,
        int maximumCount,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count > maximumCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                items.Count,
                $"{parameterName} cannot exceed {maximumCount} items.");
        }
    }

    private static void EnsureUnique<T>(IReadOnlyList<T> items, string parameterName)
    {
        if (items.Distinct().Count() != items.Count)
        {
            throw new ArgumentException($"{parameterName} cannot contain duplicates.", parameterName);
        }
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} cannot be negative.");
        }
    }

    private static void ValidateNonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} cannot be negative.");
        }
    }

    private static void ValidateNullableNonNegative(long? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} cannot be negative.");
        }
    }

    private static void ValidateNonNegative(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} cannot be negative.");
        }
    }

    private sealed class ProcessRunParticipantIdJsonConverter : JsonConverter<ProcessRunParticipantId>
    {
        public override ProcessRunParticipantId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return new ProcessRunParticipantId(
                reader.GetString()
                ?? throw new JsonException("Process run participant identifier cannot be null."));
        }

        public override void Write(
            Utf8JsonWriter writer,
            ProcessRunParticipantId value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }

    private sealed class GuidIdentifierJsonConverter<TIdentifier>(
        Func<Guid, TIdentifier> create,
        Func<TIdentifier, Guid> getValue) : JsonConverter<TIdentifier>
    {
        public override TIdentifier Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return create(reader.GetGuid());
        }

        public override void Write(
            Utf8JsonWriter writer,
            TIdentifier value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(getValue(value));
        }
    }
}
