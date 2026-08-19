using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessBlockedStepPacketBuilderTests
{
    [Fact]
    public void Runtime_receipt_packet_uses_persisted_user_safe_summary()
    {
        var receipt = CreateReceipt() with
        {
            UserSafeSummary = "The architecture step completed, but its managed summary artifact was not available."
        };

        var packet = ProcessBlockedStepPacketBuilder.Create(
            "architecture",
            CreateBlockedStep(),
            assignment: null,
            receipt: receipt,
            expiredClaim: null,
            diagnostic: null);

        Assert.Equal(ProcessBlockedStepPacketKind.RuntimeSummary, packet.Kind);
        Assert.Contains(receipt.UserSafeSummary, packet.ProblemSummary, StringComparison.Ordinal);
        Assert.Contains(receipt.UserSafeSummary, packet.RecommendedInstruction, StringComparison.Ordinal);
        Assert.DoesNotContain("no exact AgentFramework result summary", packet.ProblemSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_receipt_packet_preserves_diagnostic_gap_for_legacy_empty_summary()
    {
        var packet = ProcessBlockedStepPacketBuilder.Create(
            "architecture",
            CreateBlockedStep(),
            assignment: null,
            receipt: CreateReceipt(),
            expiredClaim: null,
            diagnostic: null);

        Assert.Equal(ProcessBlockedStepPacketKind.RuntimeReceiptOnly, packet.Kind);
        Assert.Contains("no exact AgentFramework result summary", packet.ProblemSummary, StringComparison.Ordinal);
        Assert.Contains("Do not approve a blind retry", packet.RequiredOperatorDecision, StringComparison.Ordinal);
    }

    private static ProcessRuntimeStepState CreateBlockedStep()
    {
        return new ProcessRuntimeStepState(
            ProcessStepInstanceId.New(),
            ProcessStepDefinitionId.New(),
            ProcessRuntimeStepStatus.Blocked,
            IsExecutable: true,
            AttemptNumber: 1,
            DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
            RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
            ActiveClaimToken: null,
            CompletedResultKey: null);
    }

    private static StrategyResultReceipt CreateReceipt()
    {
        return new StrategyResultReceipt(
            ProcessStepInstanceId.New(),
            new StrategyId("strategy.test"),
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked,
            "sha256:blocked");
    }
}
