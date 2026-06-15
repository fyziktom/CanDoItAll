using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessPersistenceMappers
{
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
                ResultHash = receipt.ResultHash
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
                step.CompletedResultKey is null ? null : new StrategyResultIdempotencyKey(step.CompletedResultKey.Value)));
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
                receipt.ResultHash));
        }

        var availableSlots = new HashSet<ArtifactSlotId>();
        foreach (var slot in entity.AvailableArtifactSlots)
        {
            availableSlots.Add(new ArtifactSlotId(slot.SlotId));
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
            entity.UpdatedAtUtc);
    }

    public static ProcessRuntimeEventEntity ToEventEntity(
        ProcessRuntimeEventEnvelope envelope,
        long globalSequence,
        long rootSequence)
    {
        return new ProcessRuntimeEventEntity
        {
            GlobalSequence = globalSequence,
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
}
