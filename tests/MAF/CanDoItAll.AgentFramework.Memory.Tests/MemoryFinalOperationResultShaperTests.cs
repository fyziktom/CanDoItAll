using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory.Tests;

public sealed class MemoryFinalOperationResultShaperTests
{
    [Fact]
    public void Completed_async_query_status_exposes_typed_context_pack_and_final_metadata()
    {
        var provider = CreateProvider();
        var operation = CreateOperation(provider.InstanceId);
        var pack = new MemoryContextPack(
            MemoryContextPackId.New(),
            "Persisted async context",
            [new MemoryContextSection(
                "Decision",
                "Use the persisted final result.",
                [new MemoryCitation("source://decision", "Decision")],
                0.95m)],
            [new MemoryWarning(MemoryWarningKind.ProviderPartial, "Partial context")],
            0.91m,
            MemoryFeedbackHandle.Parse("feedback-final"));
        var finalResult = new MemoryOperationResult(
            operation.OperationId,
            MemoryOperationStatus.Succeeded,
            MemoryPayload.FromJson(JsonSerializer.SerializeToElement(pack)),
            [new MemoryWarning(MemoryWarningKind.ProviderPartial, "Final warning")],
            [MemoryFeedbackHandle.Parse("feedback-final")],
            ["source://decision"]);
        operation = operation with
        {
            Status = MemoryLedgerStatus.Completed,
            Extensions = operation.Extensions.WithFinalOperationResult(
                operation.OperationId,
                operation.ProviderInstanceId,
                finalResult)
        };
        var handlerResult = new MemoryOperationHandlerResult<MemoryOperationRecord>(
            MemoryOperationHandlerStatus.Completed,
            MemoryProviderSelectionResult.Selected(
                provider,
                MemoryProviderSelectionReason.ExplicitProvider,
                MemoryCapabilityIds.OperationStatus),
            operation,
            operation,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            Diagnostic: "Completed.");

        var shaped = MemoryMafToolResultShaper.ToStatusResult(handlerResult);

        Assert.NotNull(shaped.FinalResult);
        Assert.True(shaped.FinalResult!.OutputIsReadable);
        Assert.Equal(MemoryOperationStatus.Succeeded, shaped.FinalResult.Status);
        Assert.Contains("UNTRUSTED MEMORY", shaped.FinalResult.TrustBoundary.Instruction, StringComparison.Ordinal);
        Assert.Equal("MEMORY-DATA | Persisted async context", shaped.FinalResult.ContextPack?.Summary);
        Assert.Equal("MEMORY-DATA | Use the persisted final result.", Assert.Single(shaped.FinalResult.ContextPack!.Sections).Text);
        Assert.Equal("MEMORY-DATA | Final warning", Assert.Single(shaped.FinalResult.Warnings).Message);
        Assert.Equal("feedback-final", Assert.Single(shaped.FinalResult.FeedbackHandles).Value);
        Assert.Equal(["MEMORY-DATA | source://decision"], shaped.FinalResult.SourceRefs);
    }

    private static MemoryOperationRecord CreateOperation(MemoryProviderInstanceId providerId) =>
        MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            providerId,
            MemoryCapabilityIds.ContextQueryAsync,
            MemoryOperationKind.ContextQuery,
            new MemoryLedgerRequester(
                "agent-test",
                AgentId: "agent-test",
                AgentRole: "Tester",
                SessionId: "session-test",
                WorkflowId: null,
                WorkflowNodeId: null,
                ProcessId: null,
                ProcessStepId: null),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [],
            MemoryLedgerRetentionPolicy.Expiring(
                DateTimeOffset.UtcNow.AddDays(1),
                DateTimeOffset.UtcNow.AddDays(7)),
            DateTimeOffset.UtcNow);

    private static MemoryProviderProfile CreateProvider() =>
        new(
            MemoryProviderInstanceId.Parse("memory.async"),
            "Async provider",
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("mock.memory"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "v1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
}
