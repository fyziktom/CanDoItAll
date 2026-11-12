using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessResultReceiptMappingTests {
    private static readonly ProcessExecutionRunId Execution = new(new("b76be922-5abe-4aee-b119-48dbe293b574"));
    private static readonly string Digest = "sha256:" + new string('a', 64);

    [Fact]
    public void Empty_diagnostics_preserve_actual_execution_without_inventing_host_or_safety_evidence() {
        var entity = ProcessPersistenceMappers.ToEntity(State(Execution, []));
        var stored = Assert.Single(entity.ResultReceipts);
        using var document = JsonDocument.Parse(stored.DiagnosticsJson);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(Execution.Value, document.RootElement.GetProperty("resultExecutionRunId").GetGuid());
        Assert.False(document.RootElement.TryGetProperty("hostCapabilityEvidence", out _));
        Assert.Equal(Execution, ProcessPersistenceMappers.ReadReceiptExecutionRunId(stored.DiagnosticsJson));
        var loaded = Assert.Single(ProcessPersistenceMappers.ToSnapshot(entity).AppliedResults);
        Assert.Equal(Execution, loaded.ExecutionRunId);
        Assert.Empty(loaded.Diagnostics);
        Assert.Null(loaded.HostCapabilityEvidence);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Existing_legacy_array_shapes_keep_identity_and_diagnostics(bool hasExecution, bool hasDiagnostic) {
        ProcessExecutionRunId? execution = hasExecution ? Execution : null;
        var entity = ProcessPersistenceMappers.ToEntity(State(execution, hasDiagnostic ? [Diagnostic()] : []));
        var stored = Assert.Single(entity.ResultReceipts);
        using var document = JsonDocument.Parse(stored.DiagnosticsJson);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        if (!hasDiagnostic) {
            Assert.Equal("[]", stored.DiagnosticsJson);
        }
        var loaded = Assert.Single(ProcessPersistenceMappers.ToSnapshot(entity).AppliedResults);
        Assert.Equal(execution, loaded.ExecutionRunId);
        Assert.Equal(execution, ProcessPersistenceMappers.ReadReceiptExecutionRunId(stored.DiagnosticsJson));
        Assert.Equal(hasDiagnostic ? [Diagnostic()] : [], loaded.Diagnostics);
        Assert.Null(loaded.HostCapabilityEvidence);
    }

    [Theory]
    [InlineData(Corruption.EmptyIdentity)]
    [InlineData(Corruption.ConflictingDiagnosticIdentity)]
    [InlineData(Corruption.UnsupportedSchema)]
    public void Corrupt_payload_cannot_expose_a_receipt_or_execution_lookup(Corruption corruption) {
        var entity = ProcessPersistenceMappers.ToEntity(State(Execution, []));
        var stored = Assert.Single(entity.ResultReceipts);
        var payload = JsonNode.Parse(stored.DiagnosticsJson)!.AsObject();
        switch (corruption) {
            case Corruption.EmptyIdentity:
                payload["resultExecutionRunId"] = Guid.Empty;
                break;
            case Corruption.ConflictingDiagnosticIdentity:
                var legacy = ProcessPersistenceMappers.ToEntity(State(new(Guid.NewGuid()), [Diagnostic()]));
                payload["diagnostics"] = JsonNode.Parse(Assert.Single(legacy.ResultReceipts).DiagnosticsJson);
                break;
            case Corruption.UnsupportedSchema:
                payload["schemaVersion"] = 2;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        stored.DiagnosticsJson = payload.ToJsonString();
        Assert.Throws<InvalidOperationException>(() => ProcessPersistenceMappers.ToSnapshot(entity));
        Assert.Throws<InvalidOperationException>(() => ProcessPersistenceMappers.ReadReceiptExecutionRunId(stored.DiagnosticsJson));
    }

    private static StrategyResultDiagnosticReceipt Diagnostic()
        => new("process.result.completed", StrategyDiagnosticSensitivity.Normal, Digest, "Execution completed.",
            null, ProcessDiagnosticRetrySafety.UnsafeToRetry, ProcessDiagnosticIdempotencyClassification.Idempotent);

    private static ProcessRuntimeStateSnapshot State(ProcessExecutionRunId? execution,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics) {
        var run = ProcessRunId.New();
        var step = ProcessStepInstanceId.New();
        var key = StrategyResultIdempotencyKey.New();
        return new(run, run, ProcessInstancePlanId.New(), Digest, ProcessRuntimeStatus.Completed,
            [new(step, ProcessStepDefinitionId.New(), ProcessRuntimeStepStatus.Completed, true, 1,
                new HashSet<ProcessStepInstanceId>(), new HashSet<ArtifactSlotId>(), null, key)],
            [], [new(step, new("mapping"), key, StrategyOutcome.Succeeded, ProcessRuntimeStepStatus.Completed, Digest) {
                ExecutionRunId = execution, Diagnostics = diagnostics
            }], new HashSet<ArtifactSlotId>(), new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    }

    public enum Corruption { EmptyIdentity, ConflictingDiagnosticIdentity, UnsupportedSchema }
}
