using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryLedgerLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly MemoryProviderInstanceId ProviderId = MemoryProviderInstanceId.Parse("provider.programming");
    private static readonly MemoryCapabilityId SyncQuery = MemoryCapabilityId.Parse("context.query.sync");

    [Fact]
    public void SB03_LG001_Operation_transition_rules_allow_valid_lifecycle_and_reject_regression()
    {
        var pending = CreateOperation();

        var accepted = MemoryLedgerTransitionRules.TransitionOperation(pending, MemoryLedgerStatus.Accepted, Now.AddSeconds(1), "provider accepted");
        var running = MemoryLedgerTransitionRules.TransitionOperation(accepted, MemoryLedgerStatus.Running, Now.AddSeconds(2), "worker polling");
        var completed = MemoryLedgerTransitionRules.TransitionOperation(running, MemoryLedgerStatus.Completed, Now.AddSeconds(3), "context delivered");

        Assert.Equal(MemoryLedgerStatus.Completed, completed.Status);
        Assert.Equal(3, completed.TransitionCount);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MemoryLedgerTransitionRules.TransitionOperation(completed, MemoryLedgerStatus.Running, Now.AddSeconds(4), "regression"));
        Assert.Contains("Cannot transition memory operation", exception.Message);
    }

    [Fact]
    public void SB03_LG002_Context_delivery_links_operation_requester_and_delayed_feedback()
    {
        var operation = CreateOperation();
        var delivery = MemoryContextDeliveryRecord.Create(
            MemoryContextDeliveryId.New(),
            operation.OperationId,
            MemoryContextPackId.New(),
            operation.ProviderInstanceId,
            operation.Requester,
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(30), Now.AddDays(180)),
            Now.AddMinutes(1));

        var feedback = MemoryFeedbackRecord.CreateMatched(
            MemoryFeedbackRecordId.New(),
            delivery.ContextDeliveryId,
            delivery.OperationId,
            delivery.ProviderInstanceId,
            MemoryFeedbackStage.EconomicImpact,
            MemoryFeedbackOutcome.Useful,
            delivery.Requester,
            economicImpact: new MemoryEconomicImpact("USD", 1200m),
            retention: MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(30), Now.AddDays(180)),
            createdAtUtc: Now.AddDays(7));

        Assert.Equal(operation.Requester.AgentId, delivery.Requester.AgentId);
        Assert.Equal(delivery.ContextDeliveryId, feedback.ContextDeliveryId);
        Assert.Equal(MemoryFeedbackMatchState.Matched, feedback.MatchState);
        Assert.Equal(1200m, feedback.EconomicImpact?.Amount);
    }

    [Fact]
    public void SB03_LG003_Feedback_without_delivery_id_is_rejected_unless_marked_unmatched()
    {
        var requester = CreateRequester();

        var exception = Assert.Throws<ArgumentException>(() =>
            MemoryFeedbackRecord.CreateMatched(
                MemoryFeedbackRecordId.New(),
                default,
                MemoryOperationId.New(),
                ProviderId,
                MemoryFeedbackStage.LaterCorrection,
                MemoryFeedbackOutcome.Corrected,
                requester,
                economicImpact: null,
                retention: MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(1), Now.AddDays(2)),
                createdAtUtc: Now));
        Assert.Contains("context delivery id", exception.Message);

        var unmatched = MemoryFeedbackRecord.CreateUnmatched(
            MemoryFeedbackRecordId.New(),
            ProviderId,
            MemoryFeedbackStage.LaterCorrection,
            MemoryFeedbackOutcome.Corrected,
            requester,
            unmatchedReason: "provider sent delayed correction without delivery correlation",
            retention: MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(1), Now.AddDays(2)),
            createdAtUtc: Now);

        Assert.Equal(MemoryFeedbackMatchState.Unmatched, unmatched.MatchState);
        Assert.Null(unmatched.ContextDeliveryId);
        Assert.Contains("without delivery correlation", unmatched.UnmatchedReason);
    }

    [Fact]
    public void SB03_LG004_Event_admission_rejects_duplicates_and_memory_agent_memory_loops()
    {
        var providerEvent = CreateEventInboxRecord(MemoryEventLoopContext.ProviderOrigin(ProviderId));

        var duplicate = MemoryEventAdmissionRules.EvaluateIncoming(
            providerEvent,
            [providerEvent.DedupeKey],
            MemoryEventLoopGuardPolicy.Default);

        var recursiveEvent = CreateEventInboxRecord(
            new MemoryEventLoopContext(
                MemoryEventOrigin.MemoryProvider,
                HopCount: 4,
                ProviderHops: [ProviderId, ProviderId, ProviderId, ProviderId],
                LastAgentId: "agent-memory"));
        var loopRejected = MemoryEventAdmissionRules.EvaluateIncoming(
            recursiveEvent,
            [],
            MemoryEventLoopGuardPolicy.Default with
            {
                MaxHopCount = 3,
                MaxProviderReentryCount = 2
            });

        Assert.Equal(MemoryEventAdmissionStatus.Duplicate, duplicate.Status);
        Assert.Equal(MemoryEventAdmissionStatus.LoopRejected, loopRejected.Status);
        Assert.False(loopRejected.DispatchAllowed);
    }

    [Fact]
    public void SB03_LG005_Retention_expiry_and_ipfs_unpin_metadata_are_explicit()
    {
        var retention = MemoryLedgerRetentionPolicy.Expiring(
            expiresAtUtc: Now.AddHours(1),
            forgetAtUtc: Now.AddHours(2));
        var ipfs = new MemoryIpfsSnapshotMetadata(
            SnapshotUri: "ipfs://bafy-memory-ledger",
            PinState: MemoryIpfsPinState.Pinned,
            PinnedAtUtc: Now,
            UnpinRequestedAtUtc: null,
            UnpinReason: null);

        var active = MemoryLedgerRetentionRules.Evaluate(retention, Now.AddMinutes(30));
        var expired = MemoryLedgerRetentionRules.Evaluate(retention, Now.AddHours(3));
        var unpin = ipfs.RequestUnpin(Now.AddHours(3), "retention expired");

        Assert.Equal(MemoryLedgerRetentionDecision.Active, active);
        Assert.Equal(MemoryLedgerRetentionDecision.Forget, expired);
        Assert.Equal(MemoryIpfsPinState.UnpinRequested, unpin.PinState);
        Assert.Equal("retention expired", unpin.UnpinReason);
    }

    private static MemoryOperationRecord CreateOperation()
    {
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            ProviderId,
            SyncQuery,
            MemoryOperationKind.ContextQuery,
            CreateRequester(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            MemoryLedgerRetentionPolicy.Expiring(Now.AddHours(1), Now.AddDays(7)),
            Now);
    }

    private static MemoryEventInboxRecord CreateEventInboxRecord(MemoryEventLoopContext loopContext)
    {
        return MemoryEventInboxRecord.Create(
            MemoryEventInboxRecordId.New(),
            ProviderId,
            MemoryProviderEventId.New(),
            MemoryProviderEventKind.VerificationRequest,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            MemoryEventPriority.Normal,
            loopContext,
            MemoryLedgerRetentionPolicy.Expiring(Now.AddHours(1), Now.AddDays(7)),
            Now);
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: "agent-dev",
            AgentRole: "developer",
            SessionId: "session-7",
            WorkflowId: "workflow-1",
            WorkflowNodeId: "node-query",
            ProcessId: "process-1",
            ProcessStepId: "step-2");
    }
}
