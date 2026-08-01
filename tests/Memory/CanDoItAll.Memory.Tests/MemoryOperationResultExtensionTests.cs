using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryOperationResultExtensionTests
{
    [Fact]
    public void Final_result_round_trips_every_provider_result_field()
    {
        var operation = CreateOperation();
        var result = CreateResult(operation.OperationId);
        var persisted = operation with
        {
            Extensions = operation.Extensions.WithFinalOperationResult(
                operation.OperationId,
                operation.ProviderInstanceId,
                result)
        };

        var restored = Assert.IsType<MemoryOperationResult>(persisted.GetFinalOperationResult());

        Assert.Equal(result.OperationId, restored.OperationId);
        Assert.Equal(result.Status, restored.Status);
        Assert.Equal(result.Output, restored.Output);
        Assert.Equal(result.Warnings.ToArray(), restored.Warnings.ToArray());
        Assert.Equal(result.FeedbackHandles.ToArray(), restored.FeedbackHandles.ToArray());
        Assert.Equal(result.SourceRefs.ToArray(), restored.SourceRefs.ToArray());
    }

    [Fact]
    public void Final_result_write_rejects_a_different_operation_id()
    {
        var operation = CreateOperation();
        var mismatched = CreateResult(MemoryOperationId.New());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            operation.Extensions.WithFinalOperationResult(
                operation.OperationId,
                operation.ProviderInstanceId,
                mismatched));

        Assert.Contains("host operation id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Final_result_read_rejects_a_different_provider()
    {
        var operation = CreateOperation();
        var persisted = operation with
        {
            Extensions = operation.Extensions.WithFinalOperationResult(
                operation.OperationId,
                MemoryProviderInstanceId.Parse("provider.other"),
                CreateResult(operation.OperationId))
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => persisted.GetFinalOperationResult());

        Assert.Contains("operation provider", exception.Message, StringComparison.Ordinal);
    }

    private static MemoryOperationRecord CreateOperation()
    {
        var now = DateTimeOffset.Parse("2026-07-12T12:00:00Z");
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            MemoryProviderInstanceId.Parse("provider.final-result"),
            MemoryCapabilityIds.ContextQueryAsync,
            MemoryOperationKind.ContextQuery,
            new MemoryLedgerRequester("agent-1", "agent-1", "developer", "session-1", null, null, null, null),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [],
            MemoryLedgerRetentionPolicy.Expiring(now.AddDays(1), now.AddDays(2)),
            now);
    }

    private static MemoryOperationResult CreateResult(MemoryOperationId operationId) =>
        new(
            operationId,
            MemoryOperationStatus.Succeeded,
            MemoryPayload.FromText("final context"),
            [new MemoryWarning(MemoryWarningKind.ProviderPartial, "partial source")],
            [MemoryFeedbackHandle.Parse("memory-feedback:final")],
            ["memory://source/final"]);
}
