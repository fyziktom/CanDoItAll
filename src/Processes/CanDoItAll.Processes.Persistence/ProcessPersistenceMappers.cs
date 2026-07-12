using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessPersistenceMappers
{
    private static readonly JsonSerializerOptions ReceiptJsonOptions = CreateReceiptJsonOptions();

    public static ProcessRuntimeStateEntity ToEntity(ProcessRuntimeStateSnapshot state)
    {
        var entity = new ProcessRuntimeStateEntity
        {
            RunId = state.RunId.Value,
            RootRunId = state.RootRunId.Value,
            PlanId = state.PlanId.Value,
            PlanHash = state.PlanHash,
            Status = state.Status,
            UpdatedAtUtc = state.UpdatedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };

        foreach (var step in state.Steps)
        {
            entity.Steps.Add(new ProcessRuntimeStepEntity
            {
                RunId = state.RunId.Value,
                StepInstanceId = step.StepInstanceId.Value,
                StepDefinitionId = step.StepDefinitionId.Value,
                Status = step.Status,
                IsExecutable = step.IsExecutable,
                AttemptNumber = step.AttemptNumber,
                DependencyStepIds = JoinGuids(step.DependencyStepIds, id => id.Value),
                RequiredArtifactSlotIds = JoinGuids(step.RequiredArtifactSlots, id => id.Value),
                ProducedArtifactSlotIds = JoinGuids(step.ProducedArtifactSlots, id => id.Value),
                RequiredRuntimeToolNamesJson = SerializeStringList(step.RequiredRuntimeToolNames),
                ArtifactDescriptorsJson = SerializeArtifactDescriptors(step.ArtifactDescriptors),
                SubprocessArtifactMappingsJson = SerializeSubprocessArtifactMappings(step.SubprocessArtifactMappings),
                ActiveClaimToken = step.ActiveClaimToken?.Value,
                CompletedResultKey = step.CompletedResultKey?.Value
            });
        }

        foreach (var claim in state.Claims)
        {
            entity.Claims.Add(new ProcessDispatchClaimEntity
            {
                RunId = state.RunId.Value,
                ClaimToken = claim.ClaimToken.Value,
                StepInstanceId = claim.StepInstanceId.Value,
                OwnerId = claim.OwnerId.Value,
                Status = claim.Status,
                AttemptNumber = claim.AttemptNumber,
                CreatedAtUtc = claim.CreatedAtUtc,
                ExpiresAtUtc = claim.ExpiresAtUtc,
                RenewedAtUtc = claim.RenewedAtUtc,
                ResultIdempotencyKey = claim.ResultIdempotencyKey?.Value
            });
        }

        foreach (var receipt in state.AppliedResults)
        {
            entity.ResultReceipts.Add(new ProcessStrategyResultReceiptEntity
            {
                RunId = state.RunId.Value,
                StepInstanceId = receipt.StepInstanceId.Value,
                StrategyId = receipt.StrategyId.Value,
                IdempotencyKey = receipt.IdempotencyKey.Value,
                Outcome = receipt.Outcome.ToString(),
                AppliedStepStatus = receipt.AppliedStepStatus,
                ResultHash = receipt.ResultHash,
                DiagnosticsJson = SerializeDiagnostics(receipt.Diagnostics),
                ProducedArtifactsJson = SerializeProducedArtifacts(receipt.ProducedArtifacts),
                RecoveryDecisionJson = SerializeRecoveryDecision(receipt.RecoveryDecision)
            });
        }

        foreach (var slotId in state.AvailableArtifactSlots)
        {
            entity.AvailableArtifactSlots.Add(new ProcessRuntimeAvailableArtifactSlotEntity
            {
                RunId = state.RunId.Value,
                SlotId = slotId.Value
            });
        }

        foreach (var receipt in state.ConnectedInputArtifacts)
        {
            entity.ConnectedInputArtifacts.Add(new ProcessRuntimeInputArtifactEntity
            {
                RunId = state.RunId.Value,
                ConsumerStepInstanceId = receipt.ConsumerStepInstanceId.Value,
                RequiredSlotId = receipt.RequiredSlotId.Value,
                Availability = receipt.Availability,
                ProducerStepInstanceId = receipt.ProducerStepInstanceId?.Value,
                ArtifactId = receipt.ArtifactId?.Value,
                ContentHash = receipt.ContentHash,
                ConnectionHash = receipt.ConnectionHash
            });
        }

        return entity;
    }

    public static ProcessRuntimeStateSnapshot ToSnapshot(ProcessRuntimeStateEntity entity)
    {
        var steps = new List<ProcessRuntimeStepState>(entity.Steps.Count);
        foreach (var step in entity.Steps)
        {
            steps.Add(new ProcessRuntimeStepState(
                new ProcessStepInstanceId(step.StepInstanceId),
                new ProcessStepDefinitionId(step.StepDefinitionId),
                step.Status,
                step.IsExecutable,
                step.AttemptNumber,
                ParseStepIds(step.DependencyStepIds),
                ParseArtifactSlotIds(step.RequiredArtifactSlotIds),
                step.ActiveClaimToken is null ? null : new DispatchClaimToken(step.ActiveClaimToken.Value),
                step.CompletedResultKey is null ? null : new StrategyResultIdempotencyKey(step.CompletedResultKey.Value))
            {
                ProducedArtifactSlots = ParseArtifactSlotIds(step.ProducedArtifactSlotIds),
                RequiredRuntimeToolNames = DeserializeStringList(step.RequiredRuntimeToolNamesJson),
                ArtifactDescriptors = DeserializeArtifactDescriptors(step.ArtifactDescriptorsJson),
                SubprocessArtifactMappings = DeserializeSubprocessArtifactMappings(step.SubprocessArtifactMappingsJson)
            });
        }

        var claims = new List<DispatchClaimState>(entity.Claims.Count);
        foreach (var claim in entity.Claims)
        {
            claims.Add(new DispatchClaimState(
                new DispatchClaimToken(claim.ClaimToken),
                new ProcessStepInstanceId(claim.StepInstanceId),
                new DispatcherOwnerId(claim.OwnerId),
                claim.Status,
                claim.AttemptNumber,
                claim.CreatedAtUtc,
                claim.ExpiresAtUtc,
                claim.RenewedAtUtc,
                claim.ResultIdempotencyKey is null ? null : new StrategyResultIdempotencyKey(claim.ResultIdempotencyKey.Value)));
        }

        var receipts = new List<StrategyResultReceipt>(entity.ResultReceipts.Count);
        foreach (var receipt in entity.ResultReceipts)
        {
            receipts.Add(new StrategyResultReceipt(
                new ProcessStepInstanceId(receipt.StepInstanceId),
                new StrategyId(receipt.StrategyId),
                new StrategyResultIdempotencyKey(receipt.IdempotencyKey),
                Enum.Parse<StrategyOutcome>(receipt.Outcome),
                receipt.AppliedStepStatus,
                receipt.ResultHash,
                DeserializeDiagnostics(receipt.DiagnosticsJson),
                DeserializeProducedArtifacts(receipt.ProducedArtifactsJson),
                DeserializeRecoveryDecision(receipt.RecoveryDecisionJson)));
        }

        var availableSlots = new HashSet<ArtifactSlotId>();
        foreach (var slot in entity.AvailableArtifactSlots)
        {
            availableSlots.Add(new ArtifactSlotId(slot.SlotId));
        }

        var connectedInputArtifacts = new List<ProcessRuntimeInputArtifactReceipt>(entity.ConnectedInputArtifacts.Count);
        foreach (var artifact in entity.ConnectedInputArtifacts)
        {
            connectedInputArtifacts.Add(new ProcessRuntimeInputArtifactReceipt(
                new ProcessStepInstanceId(artifact.ConsumerStepInstanceId),
                new ArtifactSlotId(artifact.RequiredSlotId),
                artifact.Availability,
                artifact.ProducerStepInstanceId is null ? null : new ProcessStepInstanceId(artifact.ProducerStepInstanceId.Value),
                artifact.ArtifactId is null ? null : new ArtifactInstanceId(artifact.ArtifactId.Value),
                artifact.ContentHash,
                artifact.ConnectionHash));
        }

        return new ProcessRuntimeStateSnapshot(
            new ProcessRunId(entity.RootRunId),
            new ProcessRunId(entity.RunId),
            new ProcessInstancePlanId(entity.PlanId),
            entity.PlanHash,
            entity.Status,
            steps,
            claims,
            receipts,
            availableSlots,
            entity.UpdatedAtUtc)
        {
            ConnectedInputArtifacts = connectedInputArtifacts
        };
    }

    public static ProcessRuntimeEventEntity ToEventEntity(
        ProcessRuntimeEventEnvelope envelope,
        long rootSequence)
    {
        return new ProcessRuntimeEventEntity
        {
            RootSequence = rootSequence,
            EventId = envelope.EventId.Value,
            RootRunId = envelope.RootRunId.Value,
            RunId = envelope.RunId.Value,
            CorrelationId = envelope.CorrelationId.Value,
            CausationId = envelope.CausationId?.Value,
            ActorKind = envelope.Actor.Kind.ToString(),
            ActorId = envelope.Actor.Id.Value,
            SchemaVersion = envelope.SchemaVersion,
            Sensitivity = envelope.Sensitivity.ToString(),
            OccurredAtUtc = envelope.OccurredAtUtc,
            EventType = envelope.EventType.Value,
            PayloadHash = envelope.PayloadHash
        };
    }

    public static ProcessStoredRuntimeEvent ToStoredEvent(ProcessRuntimeEventEntity entity)
    {
        return new ProcessStoredRuntimeEvent(
            entity.GlobalSequence,
            entity.RootSequence,
            new ProcessRuntimeEventEnvelope(
                new RuntimeEventId(entity.EventId),
                new ProcessRunId(entity.RootRunId),
                new ProcessRunId(entity.RunId),
                new ProcessCorrelationId(entity.CorrelationId),
                entity.CausationId is null ? null : new RuntimeEventId(entity.CausationId.Value),
                new ProcessEventActor(
                    Enum.Parse<ProcessEventActorKind>(entity.ActorKind),
                    new ProcessActorId(entity.ActorId)),
                entity.SchemaVersion,
                Enum.Parse<ProcessEventSensitivity>(entity.Sensitivity),
                entity.OccurredAtUtc,
                new ProcessEventType(entity.EventType),
                entity.PayloadHash));
    }

    public static ProcessOutboxMessageEntity ToOutboxEntity(
        ProcessOutboxMessage message,
        DateTimeOffset createdAtUtc)
    {
        return new ProcessOutboxMessageEntity
        {
            MessageId = message.MessageId.Value,
            EventId = message.EventId.Value,
            SubscriberKind = message.SubscriberKind,
            PayloadHash = message.PayloadHash,
            Status = ProcessOutboxDeliveryStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = createdAtUtc,
            AvailableAtUtc = createdAtUtc
        };
    }

    public static ProcessArtifactLedgerEventEntity ToLedgerEntity(ProcessArtifactLedgerEvent ledgerEvent)
    {
        return new ProcessArtifactLedgerEventEntity
        {
            LedgerEventId = ledgerEvent.LedgerEventId.Value,
            EventId = ledgerEvent.EventId.Value,
            SlotId = ledgerEvent.SlotId.Value,
            ArtifactId = ledgerEvent.ArtifactId.Value,
            ContentHash = ledgerEvent.ContentHash
        };
    }

    public static ProcessProjectionSnapshot ToProjectionSnapshot(ProcessProjectionSnapshotEntity entity)
    {
        return new ProcessProjectionSnapshot(
            new ProcessProjectorName(entity.ProjectorName),
            new ProcessProjectionKey(entity.ProjectionKey),
            entity.SchemaVersion,
            entity.PayloadJson,
            entity.PayloadHash,
            entity.UpdatedAtUtc);
    }

    public static ProcessProjectionHistoryEntity ToHistoryEntity(ProcessProjectionHistoryRecord history)
    {
        return new ProcessProjectionHistoryEntity
        {
            ProjectorName = history.ProjectorName.Value,
            ProjectionKey = history.ProjectionKey.Value,
            GlobalSequence = history.GlobalSequence,
            RootRunId = history.RootRunId.Value,
            RunId = history.RunId.Value,
            OccurredAtUtc = history.OccurredAtUtc,
            EventType = history.EventType,
            SchemaVersion = history.SchemaVersion,
            PayloadJson = history.PayloadJson,
            PayloadHash = history.PayloadHash,
            Sensitivity = history.Sensitivity
        };
    }

    public static ProcessProjectionHistoryRecord ToHistoryRecord(ProcessProjectionHistoryEntity entity)
    {
        return new ProcessProjectionHistoryRecord(
            new ProcessProjectorName(entity.ProjectorName),
            new ProcessProjectionKey(entity.ProjectionKey),
            entity.GlobalSequence,
            new ProcessRunId(entity.RootRunId),
            new ProcessRunId(entity.RunId),
            entity.OccurredAtUtc,
            entity.EventType,
            entity.SchemaVersion,
            entity.PayloadJson,
            entity.PayloadHash,
            entity.Sensitivity);
    }

    public static ProcessProjectionDeadLetter ToDeadLetter(ProcessProjectionDeadLetterEntity entity)
    {
        return new ProcessProjectionDeadLetter(
            new ProcessProjectionDeadLetterId(entity.DeadLetterId),
            new ProcessProjectorName(entity.ProjectorName),
            new ProcessProjectionShardKey(entity.ShardKey),
            new RuntimeEventId(entity.EventId),
            entity.GlobalSequence,
            entity.ErrorClass,
            entity.DiagnosticReference,
            entity.RetryPolicy,
            entity.DeadLetteredAtUtc);
    }

    private static string JoinGuids<T>(IEnumerable<T> values, Func<T, Guid> selector)
    {
        var guids = new List<Guid>();
        foreach (var value in values)
        {
            guids.Add(selector(value));
        }

        guids.Sort();
        return string.Join(';', guids);
    }

    private static IReadOnlySet<ProcessStepInstanceId> ParseStepIds(string value)
    {
        var ids = new HashSet<ProcessStepInstanceId>();
        foreach (var id in SplitGuids(value))
        {
            ids.Add(new ProcessStepInstanceId(id));
        }

        return ids;
    }

    private static IReadOnlySet<ArtifactSlotId> ParseArtifactSlotIds(string value)
    {
        var ids = new HashSet<ArtifactSlotId>();
        foreach (var id in SplitGuids(value))
        {
            ids.Add(new ArtifactSlotId(id));
        }

        return ids;
    }

    private static IEnumerable<Guid> SplitGuids(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Guid.Parse(part);
        }
    }

    private static string SerializeStringList(IReadOnlyList<string> values)
        => JsonSerializer.Serialize(
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ReceiptJsonOptions);

    private static IReadOnlyList<string> DeserializeStringList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(value, ReceiptJsonOptions)?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private static string SerializeDiagnostics(IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
        => JsonSerializer.Serialize(
            diagnostics.Select(diagnostic => new PersistedStrategyResultDiagnostic(
                diagnostic.Code,
                diagnostic.Sensitivity,
                diagnostic.EvidenceHash,
                diagnostic.SafeSummary,
                diagnostic.RestrictedEvidenceReference,
                diagnostic.RetrySafety,
                diagnostic.Idempotency)).ToArray(),
            ReceiptJsonOptions);

    private static IReadOnlyList<StrategyResultDiagnosticReceipt> DeserializeDiagnostics(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var diagnostics = JsonSerializer.Deserialize<PersistedStrategyResultDiagnostic[]>(value, ReceiptJsonOptions) ?? [];
        return diagnostics
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Code))
            .Select(diagnostic => new StrategyResultDiagnosticReceipt(
                diagnostic.Code.Trim(),
                diagnostic.Sensitivity,
                diagnostic.EvidenceHash.Trim(),
                diagnostic.SafeSummary.Trim(),
                string.IsNullOrWhiteSpace(diagnostic.RestrictedEvidenceReference)
                    ? null
                    : diagnostic.RestrictedEvidenceReference.Trim(),
                diagnostic.RetrySafety,
                diagnostic.Idempotency))
            .ToArray();
    }

    private static string SerializeProducedArtifacts(IReadOnlyList<StrategyResultArtifactReceipt> artifacts)
        => JsonSerializer.Serialize(
            artifacts.Select(artifact => new PersistedStrategyResultArtifact(
                artifact.SlotId.Value,
                artifact.ArtifactId.Value,
                artifact.ContentHash)).ToArray(),
            ReceiptJsonOptions);

    private static string SerializeArtifactDescriptors(IReadOnlyList<ProcessArtifactSlotDescriptor> descriptors)
        => JsonSerializer.Serialize(
            descriptors.Select(descriptor => new PersistedArtifactSlotDescriptor(
                descriptor.SlotId.Value,
                descriptor.SlotKey,
                descriptor.StepKey,
                descriptor.ArtifactExpectationKey,
                descriptor.ArtifactTitle,
                descriptor.ArtifactKind,
                descriptor.PrimaryManagedRef,
                descriptor.MaterializationMode,
                descriptor.PayloadSchema)).ToArray(),
            ReceiptJsonOptions);

    private static IReadOnlyList<ProcessArtifactSlotDescriptor> DeserializeArtifactDescriptors(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var descriptors = JsonSerializer.Deserialize<PersistedArtifactSlotDescriptor[]>(value, ReceiptJsonOptions) ?? [];
        return descriptors
            .Where(descriptor => descriptor.SlotId != Guid.Empty)
            .Select(descriptor => new ProcessArtifactSlotDescriptor(
                new ArtifactSlotId(descriptor.SlotId),
                descriptor.SlotKey.Trim(),
                descriptor.StepKey.Trim(),
                descriptor.ArtifactExpectationKey.Trim(),
                descriptor.ArtifactTitle.Trim(),
                descriptor.ArtifactKind.Trim(),
                descriptor.PrimaryManagedRef.Trim(),
                descriptor.MaterializationMode)
            {
                PayloadSchema = descriptor.PayloadSchema?.Trim() ?? string.Empty
            })
            .ToArray();
    }

    private static string SerializeSubprocessArtifactMappings(IReadOnlyList<SubprocessArtifactMappingDescriptor> mappings)
        => JsonSerializer.Serialize(
            mappings.Select(mapping => new PersistedSubprocessArtifactMappingDescriptor(
                mapping.ParentSlotId.Value,
                mapping.ParentArtifactExpectationKey,
                mapping.ChildProcessDefinitionKey,
                mapping.AcceptedChildOutputs.Select(ToPersistedChildMapping).ToArray(),
                mapping.NoGoChildOutputs.Select(ToPersistedChildMapping).ToArray())).ToArray(),
            ReceiptJsonOptions);

    private static IReadOnlyList<SubprocessArtifactMappingDescriptor> DeserializeSubprocessArtifactMappings(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var mappings = JsonSerializer.Deserialize<PersistedSubprocessArtifactMappingDescriptor[]>(value, ReceiptJsonOptions) ?? [];
        return mappings
            .Where(mapping => mapping.ParentSlotId != Guid.Empty)
            .Select(mapping => new SubprocessArtifactMappingDescriptor(
                new ArtifactSlotId(mapping.ParentSlotId),
                mapping.ParentArtifactExpectationKey.Trim(),
                mapping.ChildProcessDefinitionKey.Trim(),
                DeserializeChildMappings(mapping.AcceptedChildOutputs),
                DeserializeChildMappings(mapping.NoGoChildOutputs)))
            .ToArray();
    }

    private static PersistedSubprocessChildArtifactMappingDescriptor ToPersistedChildMapping(
        SubprocessChildArtifactMappingDescriptor mapping)
        => new(
            mapping.StepKey,
            mapping.ArtifactExpectationKey,
            mapping.ArtifactTitle,
            mapping.BranchOutcomeKey);

    private static IReadOnlyList<SubprocessChildArtifactMappingDescriptor> DeserializeChildMappings(
        IReadOnlyList<PersistedSubprocessChildArtifactMappingDescriptor>? mappings)
    {
        if (mappings is null || mappings.Count == 0)
        {
            return [];
        }

        return mappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.StepKey))
            .Select(mapping => new SubprocessChildArtifactMappingDescriptor(
                mapping.StepKey.Trim(),
                mapping.ArtifactExpectationKey.Trim(),
                mapping.ArtifactTitle.Trim(),
                mapping.BranchOutcomeKey.Trim()))
            .ToArray();
    }

    private static IReadOnlyList<StrategyResultArtifactReceipt> DeserializeProducedArtifacts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var artifacts = JsonSerializer.Deserialize<PersistedStrategyResultArtifact[]>(value, ReceiptJsonOptions) ?? [];
        return artifacts
            .Where(artifact => artifact.SlotId != Guid.Empty && artifact.ArtifactId != Guid.Empty)
            .Select(artifact => new StrategyResultArtifactReceipt(
                new ArtifactSlotId(artifact.SlotId),
                new ArtifactInstanceId(artifact.ArtifactId),
                artifact.ContentHash.Trim()))
            .ToArray();
    }

    private static string? SerializeRecoveryDecision(ProcessRecoveryDecisionReceipt? decision)
    {
        return decision is null
            ? null
            : JsonSerializer.Serialize(
                new PersistedProcessRecoveryDecision(
                    decision.FailureCategory,
                    decision.DecisionKind,
                    decision.SourceDiagnosticCode,
                    decision.Policy,
                    decision.SafeReason,
                    decision.RouteKind,
                    decision.ResponsibleStepInstanceId?.Value,
                    decision.DiagnosticFingerprint,
                    decision.AutomaticRetryAttempt,
                    decision.MaximumAutomaticRetryAttempts,
                    decision.SameDiagnosticFingerprintAttempt,
                    decision.MaximumSameDiagnosticFingerprintAttempts),
                ReceiptJsonOptions);
    }

    private static ProcessRecoveryDecisionReceipt? DeserializeRecoveryDecision(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decision = JsonSerializer.Deserialize<PersistedProcessRecoveryDecision>(value, ReceiptJsonOptions);
        return decision is null
            ? null
            : new ProcessRecoveryDecisionReceipt(
                decision.FailureCategory,
                decision.DecisionKind,
                decision.SourceDiagnosticCode.Trim(),
                decision.Policy.Trim(),
                decision.SafeReason.Trim())
            {
                RouteKind = decision.RouteKind == ProcessRecoveryRouteKind.None
                    ? ProcessRecoveryRouteKind.ManagerAction
                    : decision.RouteKind,
                ResponsibleStepInstanceId = decision.ResponsibleStepInstanceId is null
                    ? null
                    : new ProcessStepInstanceId(decision.ResponsibleStepInstanceId.Value),
                DiagnosticFingerprint = decision.DiagnosticFingerprint ?? string.Empty,
                AutomaticRetryAttempt = decision.AutomaticRetryAttempt,
                MaximumAutomaticRetryAttempts = decision.MaximumAutomaticRetryAttempts,
                SameDiagnosticFingerprintAttempt = decision.SameDiagnosticFingerprintAttempt,
                MaximumSameDiagnosticFingerprintAttempts = decision.MaximumSameDiagnosticFingerprintAttempts
            };
    }

    private static JsonSerializerOptions CreateReceiptJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    private sealed record PersistedStrategyResultDiagnostic(
        string Code,
        StrategyDiagnosticSensitivity Sensitivity,
        string EvidenceHash,
        string SafeSummary,
        string? RestrictedEvidenceReference,
        ProcessDiagnosticRetrySafety RetrySafety,
        ProcessDiagnosticIdempotencyClassification Idempotency);

    private sealed record PersistedStrategyResultArtifact(
        Guid SlotId,
        Guid ArtifactId,
        string ContentHash);

    private sealed record PersistedArtifactSlotDescriptor(
        Guid SlotId,
        string SlotKey,
        string StepKey,
        string ArtifactExpectationKey,
        string ArtifactTitle,
        string ArtifactKind,
        string PrimaryManagedRef,
        ProcessArtifactMaterializationMode MaterializationMode,
        string? PayloadSchema = null);

    private sealed record PersistedSubprocessArtifactMappingDescriptor(
        Guid ParentSlotId,
        string ParentArtifactExpectationKey,
        string ChildProcessDefinitionKey,
        IReadOnlyList<PersistedSubprocessChildArtifactMappingDescriptor> AcceptedChildOutputs,
        IReadOnlyList<PersistedSubprocessChildArtifactMappingDescriptor> NoGoChildOutputs);

    private sealed record PersistedSubprocessChildArtifactMappingDescriptor(
        string StepKey,
        string ArtifactExpectationKey,
        string ArtifactTitle,
        string BranchOutcomeKey);

    private sealed record PersistedProcessRecoveryDecision(
        ProcessFailureCategory FailureCategory,
        ProcessRecoveryDecisionKind DecisionKind,
        string SourceDiagnosticCode,
        string Policy,
        string SafeReason,
        ProcessRecoveryRouteKind RouteKind,
        Guid? ResponsibleStepInstanceId,
        string? DiagnosticFingerprint = null,
        int AutomaticRetryAttempt = 0,
        int MaximumAutomaticRetryAttempts = 0,
        int SameDiagnosticFingerprintAttempt = 0,
        int MaximumSameDiagnosticFingerprintAttempts = 0);
}
