using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessPersistenceMappers
{
    private const int MaximumBlockedRecoveryActions = 1024;
    private const int MaximumRecoveryCounter = 1024;
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
            ConcurrencyToken = Guid.NewGuid(),
            BlockedRecoveryActionsJson = SerializeBlockedRecoveryActions(state.BlockedRecoveryActions)
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
                RequiredRuntimeToolNamesJson = SerializeRequiredRuntimeToolNames(step.RequiredRuntimeToolNames),
                RequiredHostCapabilitiesJson = SerializeHostCapabilityIds(step.RequiredHostCapabilities),
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

        var appliedResultSequences = ResolveAppliedResultSequences(state.AppliedResults);
        for (var index = 0; index < state.AppliedResults.Count; index++)
        {
            var receipt = state.AppliedResults[index];
            EnsureValidStrategyResultReceipt(receipt);
            entity.ResultReceipts.Add(new ProcessStrategyResultReceiptEntity
            {
                RunId = state.RunId.Value,
                StepInstanceId = receipt.StepInstanceId.Value,
                StrategyId = receipt.StrategyId.Value,
                IdempotencyKey = receipt.IdempotencyKey.Value,
                Outcome = receipt.Outcome.ToString(),
                AppliedStepStatus = receipt.AppliedStepStatus,
                ResultHash = receipt.ResultHash,
                UserSafeSummary = NormalizeNullableText(receipt.UserSafeSummary),
                AppliedSequence = appliedResultSequences[index],
                DiagnosticsJson = SerializeDiagnostics(
                    receipt.Diagnostics,
                    receipt.ExecutionRunId,
                    receipt.HostCapabilityEvidence),
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
                RequiredRuntimeToolNames = DeserializeRequiredRuntimeToolNames(step.RequiredRuntimeToolNamesJson),
                RequiredHostCapabilities = DeserializeHostCapabilityIds(step.RequiredHostCapabilitiesJson),
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
        ValidatePersistedReceiptSequences(entity.ResultReceipts);
        foreach (var receipt in entity.ResultReceipts
                     .OrderBy(item => item.AppliedSequence))
        {
            if (!TryParseExactEnum<StrategyOutcome>(receipt.Outcome, out var outcome))
            {
                throw new InvalidOperationException("Persisted process strategy result receipt is invalid.");
            }

            var diagnosticSet = DeserializeDiagnostics(receipt.DiagnosticsJson);
            var deserializedReceipt = new StrategyResultReceipt(
                new ProcessStepInstanceId(receipt.StepInstanceId),
                new StrategyId(receipt.StrategyId),
                new StrategyResultIdempotencyKey(receipt.IdempotencyKey),
                outcome,
                receipt.AppliedStepStatus,
                receipt.ResultHash,
                diagnosticSet.Diagnostics,
                DeserializeProducedArtifacts(receipt.ProducedArtifactsJson),
                DeserializeRecoveryDecision(receipt.RecoveryDecisionJson))
            {
                UserSafeSummary = NormalizeOptionalText(receipt.UserSafeSummary),
                AppliedSequence = receipt.AppliedSequence,
                ExecutionRunId = diagnosticSet.ExecutionRunId,
                HostCapabilityEvidence = diagnosticSet.HostCapabilityEvidence
            };
            EnsureValidStrategyResultReceipt(deserializedReceipt);
            receipts.Add(deserializedReceipt);
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
            ConnectedInputArtifacts = connectedInputArtifacts,
            BlockedRecoveryActions = DeserializeBlockedRecoveryActions(
                entity.BlockedRecoveryActionsJson)
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

    private static string SerializeRequiredRuntimeToolNames(IReadOnlyList<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);
        if (toolNames.Count > 64 ||
            toolNames.Any(toolName =>
                toolName.Length > 128 ||
                !ProcessRequiredRuntimeToolNames.IsCanonicalRuntimeToolName(toolName)))
        {
            throw new InvalidOperationException(
                "Process runtime step tool requirements are invalid or exceed the bounded contract.");
        }

        return SerializeStringList(toolNames);
    }

    private static IReadOnlyList<string> DeserializeRequiredRuntimeToolNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var persisted = JsonSerializer.Deserialize<string[]>(value, ReceiptJsonOptions) ??
            throw new InvalidOperationException(
                "Persisted process runtime step tool requirements must be a JSON array.");
        if (persisted.Length > 64 ||
            persisted.Any(toolName =>
                toolName.Length > 128 ||
                !ProcessRequiredRuntimeToolNames.IsCanonicalRuntimeToolName(toolName)))
        {
            throw new InvalidOperationException(
                "Persisted process runtime step tool requirements are invalid or exceed the bounded contract.");
        }

        return persisted
            .Select(toolName => toolName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string SerializeHostCapabilityIds(
        IReadOnlySet<ProcessHostCapabilityId> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        if (capabilities.Count > 32 || capabilities.Any(capability => string.IsNullOrWhiteSpace(capability.Value)))
        {
            throw new InvalidOperationException(
                "Process runtime step host capability requirements are invalid or exceed the bounded contract.");
        }

        return JsonSerializer.Serialize(
            capabilities
                .OrderBy(capability => capability.Value, StringComparer.Ordinal)
                .Select(capability => capability.Value)
                .ToArray(),
            ReceiptJsonOptions);
    }

    private static IReadOnlySet<ProcessHostCapabilityId> DeserializeHostCapabilityIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Persisted process runtime step host capability requirements must be a JSON array.");
        }

        var persisted = JsonSerializer.Deserialize<string[]>(value, ReceiptJsonOptions) ??
            throw new InvalidOperationException(
                "Persisted process runtime step host capability requirements must be a JSON array.");
        if (persisted.Length > 32)
        {
            throw new InvalidOperationException(
                "Persisted process runtime step host capability requirements exceed the bounded contract.");
        }

        var capabilities = new HashSet<ProcessHostCapabilityId>();
        foreach (var item in persisted)
        {
            if (!ProcessHostCapabilityId.TryParse(item, out var capabilityId) ||
                !capabilities.Add(capabilityId))
            {
                throw new InvalidOperationException(
                    "Persisted process runtime step host capability requirements are invalid or ambiguous.");
            }
        }

        return capabilities;
    }

    private static string SerializeDiagnostics(
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics,
        ProcessExecutionRunId? executionRunId,
        ProcessHostCapabilityEvaluationEvidence? hostCapabilityEvidence)
    {
        if (diagnostics.Count > ProcessStrategyResultLimits.MaximumDiagnostics ||
            diagnostics.Any(diagnostic => !IsValidDiagnosticReceipt(diagnostic)))
        {
            throw new InvalidOperationException("Process strategy result diagnostics are invalid or exceed the bounded contract.");
        }

        var persistedDiagnostics = diagnostics.Select(diagnostic => new PersistedStrategyResultDiagnostic(
                diagnostic.Code,
                diagnostic.Sensitivity,
                diagnostic.EvidenceHash,
                diagnostic.SafeSummary,
                diagnostic.RestrictedEvidenceReference,
                diagnostic.RetrySafety,
                diagnostic.Idempotency,
                diagnostic.RelatedChildRunId?.Value,
                SerializeExecutionSafetyAttestation(diagnostic.ExecutionSafetyAttestation),
                executionRunId?.Value)).ToArray();
        if (hostCapabilityEvidence is null)
        {
            return JsonSerializer.Serialize(persistedDiagnostics, ReceiptJsonOptions);
        }

        return JsonSerializer.Serialize(
            new PersistedStrategyResultPayload(
                SchemaVersion: 1,
                persistedDiagnostics,
                executionRunId?.Value,
                SerializeHostCapabilityEvidence(hostCapabilityEvidence)),
            ReceiptJsonOptions);
    }

    private static DeserializedStrategyResultDiagnostics DeserializeDiagnostics(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new DeserializedStrategyResultDiagnostics([], null, null);
        }

        using var document = JsonDocument.Parse(value);
        PersistedStrategyResultDiagnostic[] persistedDiagnostics;
        Guid? payloadExecutionRunId = null;
        PersistedProcessHostCapabilityEvidence? persistedHostCapabilityEvidence = null;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            persistedDiagnostics =
                JsonSerializer.Deserialize<PersistedStrategyResultDiagnostic[]>(value, ReceiptJsonOptions) ?? [];
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            var payload = JsonSerializer.Deserialize<PersistedStrategyResultPayload>(value, ReceiptJsonOptions);
            if (payload is null || payload.SchemaVersion != 1 || payload.Diagnostics is null)
            {
                throw new InvalidOperationException("Persisted process strategy result metadata is invalid.");
            }

            persistedDiagnostics = payload.Diagnostics;
            payloadExecutionRunId = payload.ResultExecutionRunId;
            persistedHostCapabilityEvidence = payload.HostCapabilityEvidence;
        }
        else
        {
            throw new InvalidOperationException("Persisted process strategy result metadata is invalid.");
        }

        if (persistedDiagnostics.Length > ProcessStrategyResultLimits.MaximumDiagnostics ||
            persistedDiagnostics.Any(diagnostic =>
                diagnostic is null ||
                string.IsNullOrWhiteSpace(diagnostic.Code) ||
                diagnostic.Code.Length > ProcessStrategyResultLimits.MaximumIdentifierLength ||
                !Enum.IsDefined(diagnostic.Sensitivity) ||
                !ProcessStrategyReceiptValuePolicy.IsSha256Digest(diagnostic.EvidenceHash) ||
                diagnostic.SafeSummary is null ||
                diagnostic.SafeSummary.Length > ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength ||
                !ProcessStrategyReceiptValuePolicy.IsRestrictedEvidenceReference(diagnostic.RestrictedEvidenceReference) ||
                !Enum.IsDefined(diagnostic.RetrySafety) ||
                !Enum.IsDefined(diagnostic.Idempotency)))
        {
            throw new InvalidOperationException("Persisted process strategy result diagnostics are invalid or exceed the bounded contract.");
        }

        var diagnostics = persistedDiagnostics
            .Select(diagnostic => new StrategyResultDiagnosticReceipt(
                diagnostic.Code.Trim(),
                diagnostic.Sensitivity,
                diagnostic.EvidenceHash.Trim(),
                diagnostic.SafeSummary.Trim(),
                string.IsNullOrWhiteSpace(diagnostic.RestrictedEvidenceReference)
                    ? null
                    : diagnostic.RestrictedEvidenceReference.Trim(),
                diagnostic.RetrySafety,
                diagnostic.Idempotency)
            {
                RelatedChildRunId = diagnostic.RelatedChildRunId is null
                    ? null
                    : new ProcessRunId(diagnostic.RelatedChildRunId.Value),
                ExecutionSafetyAttestation =
                    DeserializeExecutionSafetyAttestation(diagnostic.ExecutionSafetyAttestation)
            })
            .ToArray();
        return new DeserializedStrategyResultDiagnostics(
            diagnostics,
            ResolvePersistedExecutionRunId(persistedDiagnostics, diagnostics, payloadExecutionRunId),
            DeserializeHostCapabilityEvidence(persistedHostCapabilityEvidence));
    }

    private static ProcessExecutionRunId? ResolvePersistedExecutionRunId(
        IReadOnlyList<PersistedStrategyResultDiagnostic> persistedDiagnostics,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics,
        Guid? payloadExecutionRunId)
    {
        if (payloadExecutionRunId is { } persistedPayloadExecutionRunId)
        {
            if (persistedPayloadExecutionRunId == Guid.Empty ||
                persistedDiagnostics.Any(diagnostic =>
                    diagnostic.ResultExecutionRunId is { } diagnosticExecutionRunId &&
                    diagnosticExecutionRunId != persistedPayloadExecutionRunId))
            {
                throw new InvalidOperationException("Persisted process execution run identity is invalid.");
            }

            var typedPayloadExecutionRunId = new ProcessExecutionRunId(persistedPayloadExecutionRunId);
            return diagnostics.All(diagnostic =>
                       diagnostic.ExecutionSafetyAttestation is not { } attestation ||
                       attestation.ExecutionRunId == typedPayloadExecutionRunId)
                ? typedPayloadExecutionRunId
                : throw new InvalidOperationException("Persisted process execution run identity is invalid.");
        }

        if (persistedDiagnostics.Count == 0)
        {
            return null;
        }

        var persistedExecutionRunIds = persistedDiagnostics
            .Select(diagnostic => diagnostic.ResultExecutionRunId)
            .Distinct()
            .ToArray();
        if (persistedExecutionRunIds is not [{ } executionRunId] ||
            executionRunId == Guid.Empty)
        {
            return null;
        }

        var typedExecutionRunId = new ProcessExecutionRunId(executionRunId);
        return diagnostics.All(diagnostic =>
                   diagnostic.ExecutionSafetyAttestation is not { } attestation ||
                   attestation.ExecutionRunId == typedExecutionRunId)
            ? typedExecutionRunId
            : null;
    }

    private static PersistedProcessHostCapabilityEvidence SerializeHostCapabilityEvidence(
        ProcessHostCapabilityEvaluationEvidence evidence)
    {
        if (evidence.Capabilities.Count > 32 ||
            evidence.Capabilities.Any(capability => !capability.IsStructurallyValid()) ||
            evidence.Capabilities.Select(capability => capability.Id).Distinct().Count() != evidence.Capabilities.Count)
        {
            throw new InvalidOperationException("Process host capability evidence is invalid or exceeds its bounded contract.");
        }

        return new PersistedProcessHostCapabilityEvidence(
            evidence.ProfileId.Value,
            evidence.Capabilities
                .OrderBy(capability => capability.Id.Value, StringComparer.Ordinal)
                .Select(capability => new PersistedProcessHostCapabilityFact(
                    capability.Id.Value,
                    capability.Availability,
                    capability.Reason,
                    capability.ExecutionPort))
                .ToArray());
    }

    private static ProcessHostCapabilityEvaluationEvidence? DeserializeHostCapabilityEvidence(
        PersistedProcessHostCapabilityEvidence? persisted)
    {
        if (persisted is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(persisted.ProfileId) ||
            persisted.Capabilities is null ||
            persisted.Capabilities.Count > 32)
        {
            throw new InvalidOperationException("Persisted process host capability evidence is invalid.");
        }

        ProcessHostProfileId profileId;
        try
        {
            profileId = new ProcessHostProfileId(persisted.ProfileId);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("Persisted process host capability evidence is invalid.", exception);
        }

        var capabilities = new List<ProcessHostCapabilityFact>(persisted.Capabilities.Count);
        var ids = new HashSet<ProcessHostCapabilityId>();
        foreach (var persistedCapability in persisted.Capabilities)
        {
            if (!ProcessHostCapabilityId.TryParse(persistedCapability.Id, out var id) ||
                !Enum.IsDefined(persistedCapability.Availability) ||
                !Enum.IsDefined(persistedCapability.Reason) ||
                !Enum.IsDefined(persistedCapability.ExecutionPort))
            {
                throw new InvalidOperationException("Persisted process host capability evidence is invalid.");
            }

            var capability = new ProcessHostCapabilityFact(
                id,
                persistedCapability.Availability,
                persistedCapability.Reason,
                persistedCapability.ExecutionPort);
            if (!ids.Add(id) || !capability.IsStructurallyValid())
            {
                throw new InvalidOperationException("Persisted process host capability evidence is invalid.");
            }

            capabilities.Add(capability);
        }

        return new ProcessHostCapabilityEvaluationEvidence(
            profileId,
            capabilities.OrderBy(capability => capability.Id.Value, StringComparer.Ordinal).ToArray());
    }

    private static PersistedProcessExecutionSafetyAttestation? SerializeExecutionSafetyAttestation(
        ProcessExecutionSafetyAttestation? attestation)
    {
        return attestation is null
            ? null
            : new PersistedProcessExecutionSafetyAttestation(
                attestation.Kind.ToString(),
                attestation.Attestor.ToString(),
                attestation.SchemaVersion,
                attestation.ExecutionRunId.Value,
                attestation.ProcessRunId.Value,
                attestation.StepInstanceId.Value,
                attestation.ExecutorId.Value,
                attestation.DurableEvidenceDigest,
                attestation.EvidenceHash);
    }

    private static ProcessExecutionSafetyAttestation? DeserializeExecutionSafetyAttestation(
        PersistedProcessExecutionSafetyAttestation? persisted)
    {
        if (persisted is null ||
            !TryParseExactEnum(persisted.Kind, out ProcessExecutionSafetyAttestationKind kind) ||
            !TryParseExactEnum(persisted.Attestor, out ProcessExecutionSafetyAttestor attestor) ||
            persisted.SchemaVersion is null ||
            persisted.ExecutionRunId is null ||
            persisted.ExecutionRunId.Value == Guid.Empty ||
            persisted.ProcessRunId is null ||
            persisted.ProcessRunId.Value == Guid.Empty ||
            persisted.StepInstanceId is null ||
            persisted.StepInstanceId.Value == Guid.Empty ||
            persisted.ExecutorId is null ||
            persisted.ExecutorId.Value == Guid.Empty)
        {
            return null;
        }

        var attestation = new ProcessExecutionSafetyAttestation(
            kind,
            attestor,
            persisted.SchemaVersion.Value,
            new ProcessExecutionRunId(persisted.ExecutionRunId.Value),
            new ProcessRunId(persisted.ProcessRunId.Value),
            new ProcessStepInstanceId(persisted.StepInstanceId.Value),
            new ProcessExecutionExecutorId(persisted.ExecutorId.Value),
            persisted.DurableEvidenceDigest ?? string.Empty,
            persisted.EvidenceHash ?? string.Empty);
        return attestation.IsStructurallyValid() ? attestation : null;
    }

    private static bool TryParseExactEnum<TEnum>(
        string? value,
        out TEnum result)
        where TEnum : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: false, out result) &&
               Enum.IsDefined(result) &&
               string.Equals(value, result.ToString(), StringComparison.Ordinal);
    }

    private static string NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? NormalizeNullableText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SerializeProducedArtifacts(IReadOnlyList<StrategyResultArtifactReceipt> artifacts)
    {
        if (artifacts.Count > ProcessStrategyResultLimits.MaximumArtifacts ||
            artifacts.Any(artifact => !IsValidArtifactReceipt(artifact)))
        {
            throw new InvalidOperationException("Process strategy result artifacts are invalid or exceed the bounded contract.");
        }

        return JsonSerializer.Serialize(
            artifacts.Select(artifact => new PersistedStrategyResultArtifact(
                artifact.SlotId.Value,
                artifact.ArtifactId.Value,
                artifact.ContentHash)).ToArray(),
            ReceiptJsonOptions);
    }

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
        if (artifacts.Length > ProcessStrategyResultLimits.MaximumArtifacts ||
            artifacts.Any(artifact =>
                artifact is null ||
                artifact.SlotId == Guid.Empty ||
                artifact.ArtifactId == Guid.Empty ||
                !ProcessStrategyReceiptValuePolicy.IsSha256Digest(artifact.ContentHash)))
        {
            throw new InvalidOperationException("Persisted process strategy result artifacts are invalid or exceed the bounded contract.");
        }

        return artifacts
            .Select(artifact => new StrategyResultArtifactReceipt(
                new ArtifactSlotId(artifact.SlotId),
                new ArtifactInstanceId(artifact.ArtifactId),
                artifact.ContentHash.Trim()))
            .ToArray();
    }

    private static void EnsureValidStrategyResultReceipt(StrategyResultReceipt receipt)
    {
        if (receipt is null ||
            !ProcessStrategyReceiptValuePolicy.IsStableIdentifier(receipt.StrategyId.Value) ||
            receipt.IdempotencyKey.Value == Guid.Empty ||
            !Enum.IsDefined(receipt.Outcome) ||
            !Enum.IsDefined(receipt.AppliedStepStatus) ||
            !ProcessStrategyReceiptValuePolicy.IsSha256Digest(receipt.ResultHash) ||
            !ProcessPublicReceiptTextPolicy.IsSafe(
                receipt.UserSafeSummary,
                ProcessStrategyResultLimits.MaximumUserSafeSummaryLength) ||
            receipt.Diagnostics is null ||
            receipt.Diagnostics.Count > ProcessStrategyResultLimits.MaximumDiagnostics ||
            receipt.Diagnostics.Any(diagnostic => !IsValidDiagnosticReceipt(diagnostic)) ||
            receipt.ProducedArtifacts is null ||
            receipt.ProducedArtifacts.Count > ProcessStrategyResultLimits.MaximumArtifacts ||
            receipt.ProducedArtifacts.Any(artifact => !IsValidArtifactReceipt(artifact)) ||
            receipt.RecoveryDecision is not null && !IsValidRecoveryDecision(receipt.RecoveryDecision))
        {
            throw new InvalidOperationException("Process strategy result receipt is invalid or exceeds the bounded contract.");
        }
    }

    private static bool IsValidDiagnosticReceipt(StrategyResultDiagnosticReceipt diagnostic)
        => diagnostic is not null &&
           ProcessStrategyReceiptValuePolicy.IsStableIdentifier(diagnostic.Code) &&
           Enum.IsDefined(diagnostic.Sensitivity) &&
           ProcessStrategyReceiptValuePolicy.IsSha256Digest(diagnostic.EvidenceHash) &&
           ProcessPublicReceiptTextPolicy.IsSafe(
               diagnostic.SafeSummary,
               ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength) &&
           ProcessStrategyReceiptValuePolicy.IsRestrictedEvidenceReference(diagnostic.RestrictedEvidenceReference) &&
           Enum.IsDefined(diagnostic.RetrySafety) &&
           Enum.IsDefined(diagnostic.Idempotency) &&
           (diagnostic.ExecutionSafetyAttestation is null ||
            diagnostic.ExecutionSafetyAttestation.IsStructurallyValid());

    private static bool IsValidArtifactReceipt(StrategyResultArtifactReceipt artifact)
        => artifact is not null &&
           artifact.SlotId.Value != Guid.Empty &&
           artifact.ArtifactId.Value != Guid.Empty &&
           ProcessStrategyReceiptValuePolicy.IsSha256Digest(artifact.ContentHash);

    private static string? SerializeRecoveryDecision(ProcessRecoveryDecisionReceipt? decision)
    {
        if (decision is not null && !IsValidRecoveryDecision(decision))
        {
            throw new InvalidOperationException(
                "Process recovery decision receipt is invalid or exceeds the bounded contract.");
        }

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
                    decision.MaximumSameDiagnosticFingerprintAttempts,
                    decision.RelatedChildRunId?.Value),
                ReceiptJsonOptions);
    }

    private static string SerializeBlockedRecoveryActions(
        IReadOnlyList<ProcessRuntimeBlockedRecoveryActionReceipt> actions)
    {
        var persistedActions = actions
            .Select(action => new PersistedBlockedRecoveryAction(
                action.SourceResultIdempotencyKey.Value,
                action.SourceBlockedStepInstanceId.Value,
                action.TargetStepInstanceId.Value,
                action.DiagnosticFingerprint,
                action.RecoveryRouteKind,
                action.Phase,
                action.AppliedAtUtc,
                action.RelatedChildRunId?.Value,
                action.RelatedChildUpdatedAtUtc))
            .ToArray();
        ValidateBlockedRecoveryActions(persistedActions);
        return JsonSerializer.Serialize(
            persistedActions,
            ReceiptJsonOptions);
    }

    private static IReadOnlyList<long> ResolveAppliedResultSequences(
        IReadOnlyList<StrategyResultReceipt> receipts)
    {
        if (receipts.Count == 0)
        {
            return [];
        }

        if (receipts.All(receipt => receipt.AppliedSequence == 0))
        {
            return Enumerable.Range(1, receipts.Count)
                .Select(index => (long)index)
                .ToArray();
        }

        if (receipts.Any(receipt => receipt.AppliedSequence <= 0))
        {
            throw new InvalidOperationException(
                "Process result receipt sequences cannot mix canonical positive values with legacy zero values.");
        }

        var sequences = receipts
            .Select(receipt => receipt.AppliedSequence)
            .ToArray();
        if (sequences.Distinct().Count() != sequences.Length ||
            !sequences.SequenceEqual(sequences.OrderBy(sequence => sequence)))
        {
            throw new InvalidOperationException(
                "Process result receipt sequences must be unique and ordered in the runtime state snapshot.");
        }

        return sequences;
    }

    private static void ValidatePersistedReceiptSequences(
        IReadOnlyList<ProcessStrategyResultReceiptEntity> receipts)
    {
        if (receipts.Any(receipt => receipt.AppliedSequence <= 0) ||
            receipts.Select(receipt => receipt.AppliedSequence).Distinct().Count() != receipts.Count)
        {
            throw new InvalidOperationException(
                "Persisted process result receipt sequences must be positive and unique.");
        }
    }

    private static IReadOnlyList<ProcessRuntimeBlockedRecoveryActionReceipt> DeserializeBlockedRecoveryActions(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var actions = JsonSerializer.Deserialize<PersistedBlockedRecoveryAction[]>(
            value,
            ReceiptJsonOptions) ?? [];
        ValidateBlockedRecoveryActions(actions);

        return actions
            .Select(action => new ProcessRuntimeBlockedRecoveryActionReceipt(
                new StrategyResultIdempotencyKey(action.SourceResultIdempotencyKey),
                new ProcessStepInstanceId(action.SourceBlockedStepInstanceId),
                new ProcessStepInstanceId(action.TargetStepInstanceId),
                action.DiagnosticFingerprint.Trim(),
                action.RecoveryRouteKind,
                action.Phase,
                action.AppliedAtUtc)
            {
                RelatedChildRunId = action.RelatedChildRunId is null
                    ? null
                    : new ProcessRunId(action.RelatedChildRunId.Value),
                RelatedChildUpdatedAtUtc = action.RelatedChildUpdatedAtUtc
            })
            .ToArray();
    }

    private static void ValidateBlockedRecoveryActions(
        IReadOnlyList<PersistedBlockedRecoveryAction> actions)
    {
        if (actions.Count > MaximumBlockedRecoveryActions ||
            actions.Any(action =>
                action.SourceResultIdempotencyKey == Guid.Empty ||
                action.SourceBlockedStepInstanceId == Guid.Empty ||
                action.TargetStepInstanceId == Guid.Empty ||
                !ProcessStrategyReceiptValuePolicy.IsSha256Digest(action.DiagnosticFingerprint) ||
                !Enum.IsDefined(action.RecoveryRouteKind) ||
                action.RecoveryRouteKind == ProcessRecoveryRouteKind.None ||
                !Enum.IsDefined(action.Phase) ||
                action.AppliedAtUtc == default ||
                action.AppliedAtUtc.Offset != TimeSpan.Zero ||
                !IsBlockedRecoveryRouteCoherent(action)))
        {
            throw new InvalidOperationException(
                "Persisted blocked-recovery action ledger contains an invalid entry.");
        }

        var duplicateIdentity = actions
            .GroupBy(action => (
                action.SourceBlockedStepInstanceId,
                action.SourceResultIdempotencyKey,
                action.Phase,
                action.TargetStepInstanceId))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateIdentity is not null)
        {
            throw new InvalidOperationException(
                "Persisted blocked-recovery action ledger contains a duplicate phase identity.");
        }
    }

    private static ProcessRecoveryDecisionReceipt? DeserializeRecoveryDecision(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decision = JsonSerializer.Deserialize<PersistedProcessRecoveryDecision>(value, ReceiptJsonOptions) ??
            throw new InvalidOperationException("Persisted process recovery decision must be a JSON object.");
        var receipt = new ProcessRecoveryDecisionReceipt(
                decision.FailureCategory,
                decision.DecisionKind,
                decision.SourceDiagnosticCode?.Trim() ?? string.Empty,
                decision.Policy?.Trim() ?? string.Empty,
                decision.SafeReason?.Trim() ?? string.Empty)
            {
                RouteKind = decision.RouteKind,
                ResponsibleStepInstanceId = decision.ResponsibleStepInstanceId is null
                    ? null
                    : new ProcessStepInstanceId(decision.ResponsibleStepInstanceId.Value),
                DiagnosticFingerprint = decision.DiagnosticFingerprint ?? string.Empty,
                AutomaticRetryAttempt = decision.AutomaticRetryAttempt,
                MaximumAutomaticRetryAttempts = decision.MaximumAutomaticRetryAttempts,
                SameDiagnosticFingerprintAttempt = decision.SameDiagnosticFingerprintAttempt,
                MaximumSameDiagnosticFingerprintAttempts = decision.MaximumSameDiagnosticFingerprintAttempts,
                RelatedChildRunId = decision.RelatedChildRunId is null
                    ? null
                    : new ProcessRunId(decision.RelatedChildRunId.Value)
            };
        if (!IsValidRecoveryDecision(receipt))
        {
            throw new InvalidOperationException(
                "Persisted process recovery decision is invalid or exceeds the bounded contract.");
        }

        return receipt;
    }

    private static bool IsValidRecoveryDecision(ProcessRecoveryDecisionReceipt decision)
    {
        var fingerprintIsValid = string.IsNullOrEmpty(decision.DiagnosticFingerprint) ||
                                 ProcessStrategyReceiptValuePolicy.IsSha256Digest(decision.DiagnosticFingerprint);
        return Enum.IsDefined(decision.FailureCategory) &&
               Enum.IsDefined(decision.DecisionKind) &&
               decision.DecisionKind != ProcessRecoveryDecisionKind.None &&
               Enum.IsDefined(decision.RouteKind) &&
               decision.RouteKind != ProcessRecoveryRouteKind.None &&
               IsBoundedRecoveryToken(decision.SourceDiagnosticCode) &&
               IsBoundedRecoveryToken(decision.Policy) &&
               IsSafeRecoveryText(decision.SafeReason) &&
               fingerprintIsValid &&
               AreRecoveryCountersValid(decision) &&
               decision.ResponsibleStepInstanceId is { } responsibleStepInstanceId &&
               responsibleStepInstanceId.Value != Guid.Empty &&
               IsRecoveryRouteCoherent(decision);
    }

    private static bool IsBoundedRecoveryToken(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Length <= ProcessStrategyResultLimits.MaximumIdentifierLength &&
           string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
           value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ':');

    private static bool IsSafeRecoveryText(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           ProcessPublicReceiptTextPolicy.IsSafe(
               value,
               ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength);

    private static bool AreRecoveryCountersValid(ProcessRecoveryDecisionReceipt value)
        => IsRecoveryCounterPairValid(value.AutomaticRetryAttempt, value.MaximumAutomaticRetryAttempts) &&
           IsRecoveryCounterPairValid(
               value.SameDiagnosticFingerprintAttempt,
               value.MaximumSameDiagnosticFingerprintAttempts) &&
           (value.DiagnosticFingerprint.Length > 0 ||
            value.SameDiagnosticFingerprintAttempt == 0 &&
            value.MaximumSameDiagnosticFingerprintAttempts == 0) &&
           (value.DecisionKind != ProcessRecoveryDecisionKind.SafeRetry ||
            value.RouteKind == ProcessRecoveryRouteKind.CurrentStepRetry) &&
           (value.DecisionKind != ProcessRecoveryDecisionKind.TerminalBlocked ||
            value.RouteKind == ProcessRecoveryRouteKind.TerminalBlock) &&
           (value.DiagnosticFingerprint.Length > 0 ||
            value.AutomaticRetryAttempt == 0 &&
            value.MaximumAutomaticRetryAttempts == 0);

    private static bool IsRecoveryRouteCoherent(ProcessRecoveryDecisionReceipt decision)
    {
        if (decision.DecisionKind == ProcessRecoveryDecisionKind.SafeRetry &&
            decision.RouteKind != ProcessRecoveryRouteKind.CurrentStepRetry ||
            decision.DecisionKind == ProcessRecoveryDecisionKind.TerminalBlocked &&
            decision.RouteKind != ProcessRecoveryRouteKind.TerminalBlock ||
            decision.DecisionKind == ProcessRecoveryDecisionKind.ManagerRequired &&
            decision.RouteKind == ProcessRecoveryRouteKind.TerminalBlock)
        {
            return false;
        }

        return decision.RouteKind == ProcessRecoveryRouteKind.ChildRunPropagation
            ? decision.RelatedChildRunId is { } childRunId && childRunId.Value != Guid.Empty
            : decision.RelatedChildRunId is null;
    }

    private static bool IsRecoveryCounterPairValid(int attempt, int maximum)
        => attempt >= 0 &&
           maximum >= 0 &&
           attempt <= MaximumRecoveryCounter &&
           maximum <= MaximumRecoveryCounter &&
           (maximum > 0 || attempt == 0) &&
           attempt <= maximum + 1;

    private static bool IsBlockedRecoveryRouteCoherent(PersistedBlockedRecoveryAction action)
        => action.Phase switch
        {
            ProcessRuntimeBlockedRecoveryPhase.CurrentStep =>
                action.TargetStepInstanceId == action.SourceBlockedStepInstanceId &&
                action.RecoveryRouteKind is ProcessRecoveryRouteKind.ManagerAction or
                    ProcessRecoveryRouteKind.CurrentStepRetry &&
                action.RelatedChildRunId is null &&
                action.RelatedChildUpdatedAtUtc is null,
            ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer =>
                action.TargetStepInstanceId != action.SourceBlockedStepInstanceId &&
                action.RecoveryRouteKind == ProcessRecoveryRouteKind.UpstreamStepRework &&
                action.RelatedChildRunId is null &&
                action.RelatedChildUpdatedAtUtc is null,
            ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer =>
                action.TargetStepInstanceId == action.SourceBlockedStepInstanceId &&
                action.RecoveryRouteKind == ProcessRecoveryRouteKind.UpstreamStepRework &&
                action.RelatedChildRunId is null &&
                action.RelatedChildUpdatedAtUtc is null,
            ProcessRuntimeBlockedRecoveryPhase.CompletedChildConsumer =>
                action.TargetStepInstanceId == action.SourceBlockedStepInstanceId &&
                action.RecoveryRouteKind == ProcessRecoveryRouteKind.ChildRunPropagation &&
                action.RelatedChildRunId is { } relatedChildRunId &&
                relatedChildRunId != Guid.Empty &&
                action.RelatedChildUpdatedAtUtc is { } childUpdatedAtUtc &&
                childUpdatedAtUtc.Offset == TimeSpan.Zero,
            _ => false
        };

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
        ProcessDiagnosticIdempotencyClassification Idempotency,
        Guid? RelatedChildRunId = null,
        PersistedProcessExecutionSafetyAttestation? ExecutionSafetyAttestation = null,
        Guid? ResultExecutionRunId = null);

    private sealed record PersistedStrategyResultPayload(
        int SchemaVersion,
        PersistedStrategyResultDiagnostic[] Diagnostics,
        Guid? ResultExecutionRunId,
        PersistedProcessHostCapabilityEvidence? HostCapabilityEvidence);

    private sealed record DeserializedStrategyResultDiagnostics(
        IReadOnlyList<StrategyResultDiagnosticReceipt> Diagnostics,
        ProcessExecutionRunId? ExecutionRunId,
        ProcessHostCapabilityEvaluationEvidence? HostCapabilityEvidence);

    private sealed record PersistedProcessHostCapabilityEvidence(
        string ProfileId,
        IReadOnlyList<PersistedProcessHostCapabilityFact> Capabilities);

    private sealed record PersistedProcessHostCapabilityFact(
        string Id,
        ProcessHostCapabilityAvailability Availability,
        ProcessHostCapabilityReason Reason,
        ProcessHostExecutionPort ExecutionPort);

    private sealed record PersistedProcessExecutionSafetyAttestation(
        string? Kind = null,
        string? Attestor = null,
        int? SchemaVersion = null,
        Guid? ExecutionRunId = null,
        Guid? ProcessRunId = null,
        Guid? StepInstanceId = null,
        Guid? ExecutorId = null,
        string? DurableEvidenceDigest = null,
        string? EvidenceHash = null);

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
        int MaximumSameDiagnosticFingerprintAttempts = 0,
        Guid? RelatedChildRunId = null);

    private sealed record PersistedBlockedRecoveryAction(
        Guid SourceResultIdempotencyKey,
        Guid SourceBlockedStepInstanceId,
        Guid TargetStepInstanceId,
        string DiagnosticFingerprint,
        ProcessRecoveryRouteKind RecoveryRouteKind,
        ProcessRuntimeBlockedRecoveryPhase Phase,
        DateTimeOffset AppliedAtUtc,
        Guid? RelatedChildRunId = null,
        DateTimeOffset? RelatedChildUpdatedAtUtc = null);
}
