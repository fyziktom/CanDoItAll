using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessToolSourcePersistenceTests {
    [Fact]
    public void Legacy_preparation_keeps_its_exact_serialized_shape_and_legacy_hash() {
        var preparation = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Guid.NewGuid()), new(Guid.NewGuid()));
        var entity = ProcessPreparedLaunchCodec.ToEntity(preparation);
        var normalized = preparation with { PreparedAtUtc = entity.PreparedAtUtc };
        var legacyJson = JsonSerializer.Serialize(new {
            normalized.AdmissionId, normalized.CallerIntentId, normalized.RequestFingerprint, normalized.Authority,
            normalized.Request, normalized.InitialCommit, normalized.Review, normalized.LinkTarget, normalized.PreparedAtUtc
        }, ProcessInstancePlanPersistenceMapper.CreateSerializerOptions());
        Assert.Equal(legacyJson, entity.PayloadJson);
        Assert.Equal(LegacyHash(legacyJson), entity.PreparationFingerprint);
        Assert.Null(ProcessPreparedLaunchCodec.Read(entity).Preparation.ToolSource);
    }

    [Fact]
    public void New_preparation_round_trips_nonempty_trusted_source_and_is_rejected_by_the_legacy_hash_algorithm() {
        var preparation = Create();
        var original = preparation.ToolSource!;
        var entity = ProcessPreparedLaunchCodec.ToEntity(preparation);
        Assert.NotEqual(LegacyHash(entity.PayloadJson), entity.PreparationFingerprint);
        var saved = ProcessPreparedLaunchCodec.Read(entity);
        var restored = Assert.IsType<ProcessLaunchToolSource>(saved.Preparation.ToolSource);
        Assert.Equal(original.IntentId, restored.IntentId);
        Assert.Equal(original.Execution.Evidence, restored.Execution.Evidence);
        Assert.NotEqual(Guid.Empty, restored.Execution.Evidence.ExecutionRunId);
        Assert.NotEqual(Guid.Empty, restored.Execution.Evidence.DispatchClaimToken);
        Assert.NotEqual(Guid.Empty, restored.Execution.Evidence.RunId.Value);
        Assert.NotEqual(Guid.Empty, restored.Execution.Evidence.StepInstanceId.Value);
        Assert.Equal(original.Execution.ProjectReference, restored.Execution.ProjectReference);
        Assert.Equal(original.Execution.SourceAuthority!.ProjectAdmission, restored.Execution.SourceAuthority!.ProjectAdmission);
        Assert.Null(saved.Preparation.Request.ToolSource);
        Assert.Null(saved.Preparation.Request.Authority);
        var request = ProcessLaunchProducerRequests.Restore(saved, saved.Preparation.Authority!, false);
        Assert.Same(saved.Preparation.ToolSource, request.ToolSource);
        Assert.Equal(preparation.RequestFingerprint, ProcessLaunchIntentFingerprint.Compute(request));
        Assert.True(entity.ReferencesProject());
        entity.PreparationFingerprint = LegacyHash(entity.PayloadJson);
        Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.Read(entity));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void Unsupported_tool_source_version_is_rejected_before_persistence(int version) {
        var preparation = Create();
        var source = preparation.ToolSource! with { SchemaVersion = version };
        Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.ToEntity(preparation with { ToolSource = source }));
    }

    [Theory]
    [InlineData(SourceChange.Intent)]
    [InlineData(SourceChange.Execution)]
    [InlineData(SourceChange.Executor)]
    [InlineData(SourceChange.Claim)]
    [InlineData(SourceChange.Step)]
    [InlineData(SourceChange.ExecutionFingerprint)]
    [InlineData(SourceChange.Proposal)]
    [InlineData(SourceChange.Target)]
    public void Retained_source_or_target_cannot_change_under_the_original_request_fingerprint(SourceChange change) {
        var preparation = Create();
        var source = preparation.ToolSource!;
        var evidence = source.Execution.Evidence;
        var changed = change switch {
            SourceChange.Intent => source with { IntentId = new(Guid.NewGuid()) },
            SourceChange.Execution => source with { Execution = source.Execution with { Evidence = evidence with { ExecutionRunId = Guid.NewGuid() } } },
            SourceChange.Executor => source with { Execution = source.Execution with { Evidence = evidence with { ExecutorAgentId = Guid.NewGuid() } } },
            SourceChange.Claim => source with { Execution = source.Execution with { Evidence = evidence with { DispatchClaimToken = Guid.NewGuid() } } },
            SourceChange.Step => source with { Execution = source.Execution with { Evidence = evidence with { StepInstanceId = new(Guid.NewGuid()) } } },
            SourceChange.ExecutionFingerprint => source with { ExecutionFingerprint = new('c', 64) },
            SourceChange.Proposal => source with { ProposalFingerprint = new('d', 64) },
            SourceChange.Target => source,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        preparation = preparation with {
            ToolSource = changed,
            LinkTarget = change == SourceChange.Target ? new(preparation.Request.ProjectId!.Value, "changed-node", "changed-binding") : preparation.LinkTarget
        };
        if (change == SourceChange.Step) {
            Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.ToEntity(preparation));
        } else {
            Assert.Throws<ProcessLaunchIntentConflictException>(() => ProcessPreparedLaunchCodec.ToEntity(preparation));
        }
    }

    [Fact]
    public void Observation_time_and_dispatch_liveness_do_not_mint_a_new_business_intent() {
        var preparation = Create();
        var source = preparation.ToolSource!;
        var observed = source with { Execution = source.Execution with {
            ObservedAtUtc = source.Execution.ObservedAtUtc.AddMinutes(1), ObservedCurrentDispatch = false,
            Evidence = source.Execution.Evidence with { ExecutionMayDispatch = false }
        } };
        Assert.Equal(source.SemanticFingerprint, observed.SemanticFingerprint);
        Assert.Equal(preparation.RequestFingerprint, ProcessLaunchIntentFingerprint.Compute(preparation.Request with { ToolSource = observed }));
    }

    private static ProcessPreparedLaunch Create() {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var authority = ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId, project);
        var preparation = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()));
        var parentRun = new ProcessRunId(Guid.NewGuid());
        var parentStep = new ProcessStepInstanceId(Guid.NewGuid());
        var evidence = new ProcessExecutionClaimEvidence(Guid.NewGuid(), Guid.NewGuid(), parentRun, parentStep,
            Guid.NewGuid(), "parent-step", ProcessProjectAdmissionFixture.Now, true);
        var execution = new ProcessExecutionDispatchAuthority(evidence, parentRun, project.ProjectId,
            "sha256:" + new string('a', 64), "parent-readiness", [], "project", new(), authority,
            new(new(Guid.NewGuid()), "sha256:" + new string('b', 64), parentRun, parentStep, "parent-readiness"),
            true, ProcessProjectAdmissionFixture.Now);
        var source = new ProcessLaunchToolSource(ProcessLaunchToolSource.CurrentSchemaVersion, execution, "process-step",
            evidence.StepKey, new('e', 64), preparation.CallerIntentId!.Value, new('f', 64));
        var request = preparation.Request with { ToolSource = source, ProducerInputFingerprint = source.ProposalFingerprint };
        return preparation with { Request = request, ToolSource = source, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(request) };
    }

    private static string LegacyHash(string value)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public enum SourceChange { Intent, Execution, Executor, Claim, Step, ExecutionFingerprint, Proposal, Target }
}
