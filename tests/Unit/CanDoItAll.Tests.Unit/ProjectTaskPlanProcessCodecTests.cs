using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanProcessCodecTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_run_or_step_material_restores_exact_approved_inherited_publication(bool step) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create(step);
        using var target = JsonDocument.Parse(fixture.Decision.OriginalTarget.PayloadJson);
        Assert.False(target.RootElement.GetProperty("session").TryGetProperty("reference", out _));
        Assert.False(target.RootElement.GetProperty("session").TryGetProperty("chatSessionId", out _));
        var prepared = fixture.Prepare();
        using var arguments = JsonDocument.Parse(prepared.Payload.ArgumentsJson);
        Assert.Equal(prepared.Payload, ProjectTaskPlanAgentCodec.Restore(arguments.RootElement, prepared.Payload.SourcePreparation!));
        var restored = ProjectTaskPlanProcessCodec.Resolve(prepared.Observation, fixture.Target.Session, prepared.Payload, prepared.Binding);
        Assert.Equal(ProjectTaskPlanSourceKind.ProcessAgentDecision, restored.Command.Source.Kind);
        Assert.Equal(ProjectTaskPlanSource.ActiveWorkVersion, restored.Command.Source.Version);
        Assert.Equal(fixture.Target.WriteContext.CallerBinding, restored.Command.Operation.CallerBinding);
        Assert.Equal(fixture.Target.WriteContext.Epoch, restored.Command.Operation.Epoch);
        Assert.Equal(fixture.Decision.Policy, restored.Command.Policy);
        Assert.Equal(ReadBasisMode.RunSnapshot, restored.Command.Policy.ReadBasisMode);
        Assert.Equal(OnBasisChange.PauseForReview, restored.Command.Policy.OnBasisChange);
        Assert.Equal(TimeSpan.FromSeconds(30), restored.Command.Policy.Budget.MaximumWorkDuration);
        Assert.Equal(0, restored.Command.Policy.Budget.MaximumReplans);
        Assert.Equal(Encoding.UTF8.GetBytes(fixture.Material.PayloadJson), ProjectTaskPlanBasisCodec.Encode(restored.Command.Basis));
        Assert.Equal(fixture.Material.Stamp, ProjectTaskPlanProcessCodec.Parent(fixture.Origin).Material);
    }

    [Fact]
    public void Children_share_parent_bytes_but_never_each_others_write_context_or_publication() {
        var first = ProjectTaskPlanProcessCodecFixture.Create();
        var second = ProjectTaskPlanProcessCodecFixture.Create(shared: first.Material);
        Assert.Equal(first.Basis, second.Basis);
        Assert.Equal(first.Material.PayloadJson, second.Material.PayloadJson);
        Assert.NotEqual(first.Target.WriteContext.CallerBinding, second.Target.WriteContext.CallerBinding);
        var one = first.Prepare();
        var two = second.Prepare();
        var commandOne = ProjectTaskPlanProcessCodec.Resolve(one.Observation, first.Target.Session, one.Payload, one.Binding).Command;
        var commandTwo = ProjectTaskPlanProcessCodec.Resolve(two.Observation, second.Target.Session, two.Payload, two.Binding).Command;
        Assert.NotEqual(commandOne.Operation.Id, commandTwo.Operation.Id);
        Assert.NotEqual(ProjectTaskPlanOperationCodec.Digest(commandOne), ProjectTaskPlanOperationCodec.Digest(commandTwo));
        Refused(() => ProjectTaskPlanProcessCodec.Resolve(two.Observation, second.Target.Session, one.Payload, one.Binding));
        Refused(() => ProjectTaskPlanProcessCodec.Target(second.Decision with {
            OriginalTarget = ProjectTaskPlanProcessCodecFixture.TargetEnvelope(second.Target with { WriteContext = first.Target.WriteContext })
        }));
    }

    [Fact]
    public void Interactive_and_inherited_target_interpretations_remain_disjoint() {
        var inherited = ProjectTaskPlanProcessCodecFixture.Create();
        Assert.Throws<AgentToolAdmissionException>(() => ProjectTaskPlanAgentCodec.Target(inherited.Decision));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectTaskPlanProcessCodec.Target(AgentPlanningDecisionTestData.Create().PlanningDecision!));
        Refused(() => ProjectTaskPlanProcessCodec.Target(inherited.Decision with {
            OriginalTarget = AgentToolProtocolEnvelope.Create(ProjectTaskPlanAgentCodec.TargetFormat, 1,
                inherited.Decision.OriginalTarget.PayloadJson)
        }));
    }

    public enum TargetChange {
        Session, Profile, Agent, Purpose, Chat, Authority, Execution, Executor, Run, Step, Root, Claim,
        DispatchDenied, NotCurrent, CreatedLater, SourceKey, Fingerprint, SourcePermission, Project, Lifetime,
        ReferenceRun, ReferenceStep, Readiness, Operation, ToolsDenied, TasksDenied, ContextNamespace, ContextEpoch,
        ContextCaller, ContextClosed, ContextReplay, ContextTimeZone
    }

    [Theory]
    [InlineData(TargetChange.Session)]
    [InlineData(TargetChange.Profile)]
    [InlineData(TargetChange.Agent)]
    [InlineData(TargetChange.Purpose)]
    [InlineData(TargetChange.Chat)]
    [InlineData(TargetChange.Authority)]
    [InlineData(TargetChange.Execution)]
    [InlineData(TargetChange.Executor)]
    [InlineData(TargetChange.Run)]
    [InlineData(TargetChange.Step)]
    [InlineData(TargetChange.Root)]
    [InlineData(TargetChange.Claim)]
    [InlineData(TargetChange.DispatchDenied)]
    [InlineData(TargetChange.NotCurrent)]
    [InlineData(TargetChange.CreatedLater)]
    [InlineData(TargetChange.SourceKey)]
    [InlineData(TargetChange.Fingerprint)]
    [InlineData(TargetChange.SourcePermission)]
    [InlineData(TargetChange.Project)]
    [InlineData(TargetChange.Lifetime)]
    [InlineData(TargetChange.ReferenceRun)]
    [InlineData(TargetChange.ReferenceStep)]
    [InlineData(TargetChange.Readiness)]
    [InlineData(TargetChange.Operation)]
    [InlineData(TargetChange.ToolsDenied)]
    [InlineData(TargetChange.TasksDenied)]
    [InlineData(TargetChange.ContextNamespace)]
    [InlineData(TargetChange.ContextEpoch)]
    [InlineData(TargetChange.ContextCaller)]
    [InlineData(TargetChange.ContextClosed)]
    [InlineData(TargetChange.ContextReplay)]
    [InlineData(TargetChange.ContextTimeZone)]
    public void Replaced_original_session_source_ceiling_or_finite_context_is_refused(TargetChange change) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var target = fixture.Target;
        var session = target.Session;
        var source = target.Source;
        var evidence = source.Evidence;
        var context = target.WriteContext;
        if (change is TargetChange.Chat or TargetChange.Authority) {
            var json = JsonNode.Parse(fixture.Decision.OriginalTarget.PayloadJson)!;
            var reference = json["session"]!.AsObject();
            if (change == TargetChange.Chat) {
                reference["chatSessionId"] = Guid.NewGuid();
            } else {
                reference["authorityId"] = new JsonObject { ["value"] = Guid.NewGuid() };
            }
            Refused(() => ProjectTaskPlanProcessCodec.Target(fixture.Decision with {
                OriginalTarget = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.TargetFormat, 1, json.ToJsonString())
            }));
            return;
        }
        target = change switch {
            TargetChange.Session => target with { Session = session with { Reference = new(Guid.NewGuid(), Guid.Empty, default, session.Reference.BackgroundSource) } },
            TargetChange.Profile => target with { Session = session with { Profile = new(Guid.NewGuid(), "other", new(1)) } },
            TargetChange.Agent => target with { Session = session with { AgentId = Guid.NewGuid() } },
            TargetChange.Purpose => target with { Session = session with { Purpose = AgentRuntimeContextPurpose.InteractiveChat } },
            TargetChange.Execution => target with { Source = source with { Evidence = evidence with { ExecutionRunId = Guid.NewGuid() } } },
            TargetChange.Executor => target with { Source = source with { Evidence = evidence with { ExecutorAgentId = Guid.NewGuid() } } },
            TargetChange.Run => target with { Source = source with { Evidence = evidence with { RunId = new(Guid.NewGuid()) } } },
            TargetChange.Step => target with { Source = source with { Evidence = evidence with { StepInstanceId = new(Guid.NewGuid()) } } },
            TargetChange.Root => target with { Source = source with { RootRunId = new(Guid.NewGuid()) } },
            TargetChange.Claim => target with { Source = source with { Evidence = evidence with { DispatchClaimToken = Guid.Empty } } },
            TargetChange.DispatchDenied => target with { Source = source with { Evidence = evidence with { ExecutionMayDispatch = false } } },
            TargetChange.NotCurrent => target with { Source = source with { ObservedCurrentDispatch = false } },
            TargetChange.CreatedLater => target with { Source = source with { Evidence = evidence with { ExecutionCreatedAtUtc = fixture.Decision.StartedAtUtc.AddSeconds(1) } } },
            TargetChange.SourceKey => target with { Source = source with { Evidence = evidence with { StepKey = "another-step" } } },
            TargetChange.Fingerprint => target with { Source = source with { OwnerFingerprint = "another-owner" } },
            TargetChange.SourcePermission => target with { Source = source with { SourceAuthority = source.SourceAuthority! with { CanCreateTasks = false } } },
            TargetChange.Project => target with { Source = source with { ProjectId = Guid.NewGuid() } },
            TargetChange.Lifetime => target with { Project = new(target.Project.DatabaseProfileId, target.Project.ProjectId, Guid.NewGuid()) },
            TargetChange.ReferenceRun => target with { Source = source with { ProjectReference = source.ProjectReference! with { RunId = new(Guid.NewGuid()) } } },
            TargetChange.ReferenceStep => target with { Source = source with { ProjectReference = source.ProjectReference! with { StepInstanceId = new(Guid.NewGuid()) } } },
            TargetChange.Readiness => target with { Source = source with { ReadinessHash = "another-readiness" } },
            TargetChange.Operation => target with { Source = source with { AllowedOperations = [] } },
            TargetChange.ToolsDenied => target with { Executor = new(false, true) },
            TargetChange.TasksDenied => target with { Executor = new(true, false) },
            TargetChange.ContextNamespace => target with { WriteContext = context with { Namespace = new(Guid.NewGuid()) } },
            TargetChange.ContextEpoch => target with { WriteContext = context with { Epoch = 0 } },
            TargetChange.ContextCaller => target with { WriteContext = context with { CallerBinding = new string('a', 64) } },
            TargetChange.ContextClosed => target with { WriteContext = context with { AdmissionClosesAtUtc = default } },
            TargetChange.ContextReplay => target with { WriteContext = context with { ReplayUntilUtc = context.AdmissionClosesAtUtc } },
            TargetChange.ContextTimeZone => target with { WriteContext = context with { ReplayUntilUtc = context.ReplayUntilUtc.ToOffset(TimeSpan.FromHours(1)) } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Refused(() => ProjectTaskPlanProcessCodec.Target(fixture.Decision with {
            OriginalTarget = ProjectTaskPlanProcessCodecFixture.TargetEnvelope(target)
        }));
    }

    public enum OriginChange { Profile, Namespace, Agent, SourceKind, BasisId, Budget, Allocation, Grant, ParentMode, Policy, ExecutionContract, Child }

    [Theory]
    [InlineData(OriginChange.Profile)]
    [InlineData(OriginChange.Namespace)]
    [InlineData(OriginChange.Agent)]
    [InlineData(OriginChange.SourceKind)]
    [InlineData(OriginChange.BasisId)]
    [InlineData(OriginChange.Budget)]
    [InlineData(OriginChange.Allocation)]
    [InlineData(OriginChange.Grant)]
    [InlineData(OriginChange.ParentMode)]
    [InlineData(OriginChange.Policy)]
    [InlineData(OriginChange.ExecutionContract)]
    [InlineData(OriginChange.Child)]
    public void Replaced_parent_contract_identity_policy_or_grant_is_refused(OriginChange change) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var original = fixture.Origin;
        var origin = change switch {
            OriginChange.Profile => original with { Profile = new(Guid.NewGuid(), "other", new(1)) },
            OriginChange.Namespace => original with { WorkspaceNamespaceId = Guid.NewGuid() },
            OriginChange.Agent => original with { AgentId = Guid.NewGuid() },
            OriginChange.SourceKind => original with { Source = new("another-owner", original.Source.SourceId,
                original.Source.OwnerFingerprint, original.Source.ExecutionFingerprint) },
            OriginChange.BasisId => original with { BasisId = Guid.NewGuid() },
            OriginChange.Budget => original with { Allocation = original.Allocation with { BudgetId = new(Guid.NewGuid()) } },
            OriginChange.Allocation => original with { Allocation = original.Allocation with { Id = new(Guid.NewGuid()) } },
            OriginChange.Grant => original with { Allocation = original.Allocation with { Grant = new(TimeSpan.FromSeconds(29).Ticks, 0) } },
            OriginChange.ParentMode => original with { ParentPolicy = new(ReadBasisMode.RunSnapshot,
                original.ParentPolicy.PublicationFreshness, original.ParentPolicy.OnBasisChange, original.ParentPolicy.Budget) },
            OriginChange.Policy => original with { ParentPolicy = new(original.ParentPolicy.ReadBasisMode,
                PublicationFreshness.PinnedObservation, original.ParentPolicy.OnBasisChange, original.ParentPolicy.Budget) },
            OriginChange.ExecutionContract => original with { ParentBinding = ProjectTaskPlanProcessCodecFixture.ParentEnvelope(fixture.Parent with {
                ExecutionContract = fixture.Parent.ExecutionContract with { ContractId = Guid.NewGuid() }
            }) },
            OriginChange.Child => original with { ParentBinding = ProjectTaskPlanProcessCodecFixture.ParentEnvelope(fixture.Parent with {
                Child = fixture.Parent.Child with { Address = fixture.Parent.Child.Address with { ChildId = new(Guid.NewGuid()) } }
            }) },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Refused(() => ProjectTaskPlanProcessCodec.Target(fixture.Decision with { InheritedOrigin = origin }));
    }

    public enum MaterialChange { Identity, Capture, Bytes, Version, Format, Noncanonical, Stamp, Contract, Scope, Oversize }

    [Theory]
    [InlineData(MaterialChange.Identity)]
    [InlineData(MaterialChange.Capture)]
    [InlineData(MaterialChange.Bytes)]
    [InlineData(MaterialChange.Version)]
    [InlineData(MaterialChange.Format)]
    [InlineData(MaterialChange.Noncanonical)]
    [InlineData(MaterialChange.Stamp)]
    [InlineData(MaterialChange.Contract)]
    [InlineData(MaterialChange.Scope)]
    [InlineData(MaterialChange.Oversize)]
    public void Replacement_material_is_refused_even_with_a_matching_agent_digest(MaterialChange change) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var basis = fixture.Basis;
        var original = fixture.Origin;
        if (change == MaterialChange.Contract || change == MaterialChange.Scope) {
            var material = fixture.Material;
            var changed = ProcessDecisionMaterial.FromContent(
                change == MaterialChange.Scope ? material.Scope with { WorkspaceNamespaceId = Guid.NewGuid() } : material.Scope,
                material.Reference, change == MaterialChange.Contract ? new(Guid.NewGuid(), 1) : material.ContentContract!,
                material.CapturedAtUtc, material.PayloadJson);
            Refused(() => ProjectTaskPlanProcessCodec.Basis(original, changed));
            return;
        }
        var json = basis.Value.PayloadJson;
        basis = change switch {
            MaterialChange.Identity => basis with { Id = Guid.NewGuid() },
            MaterialChange.Capture => basis with { CapturedAtUtc = basis.CapturedAtUtc.AddTicks(1) },
            MaterialChange.Bytes => basis with { Value = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.MaterialFormat, 1,
                json.Replace("Original project", "Replacement project", StringComparison.Ordinal)) },
            MaterialChange.Version => basis with { Value = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.MaterialFormat, 2, json) },
            MaterialChange.Format => basis with { Value = AgentToolProtocolEnvelope.Create(ProjectTaskPlanAgentCodec.BasisFormat, 1, json) },
            MaterialChange.Noncanonical => basis with { Value = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.MaterialFormat, 1, " " + json) },
            MaterialChange.Oversize => basis with { Value = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.MaterialFormat, 1,
                "\"" + new string('x', AgentPlanningProviderBounds.MaximumInputUtf8Bytes) + "\"") },
            MaterialChange.Stamp => basis,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        original = original with { BasisDigest = basis.Value.Digest };
        if (change == MaterialChange.Stamp) {
            original = original with { ParentBinding = ProjectTaskPlanProcessCodecFixture.ParentEnvelope(fixture.Parent with {
                Material = fixture.Parent.Material with { Fingerprint = new string('A', 64) }
            }) };
        }
        Refused(() => ProjectTaskPlanProcessCodec.Material(original, basis));
    }

    [Fact]
    public void Saved_publication_must_retain_approved_binding_and_exact_original_arguments() {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var prepared = fixture.Prepare();
        Refused(() => ProjectTaskPlanProcessCodec.Resolve(prepared.Observation, fixture.Target.Session, prepared.Payload,
            prepared.Binding with { IntentId = new(Guid.NewGuid()) }));
        Refused(() => ProjectTaskPlanProcessCodec.Resolve(prepared.Observation, fixture.Target.Session,
            new(prepared.Payload.ToolName, prepared.Payload.SemanticVersion, prepared.Payload.Digest,
                prepared.Payload.ArgumentsJson.Replace("Review", "Replacement", StringComparison.Ordinal), prepared.Payload.Effect,
                prepared.Payload.Recovery, prepared.Payload.SourcePreparation), prepared.Binding));
        Refused(() => ProjectTaskPlanProcessCodec.Resolve(prepared.Observation,
            fixture.Target.Session with { AgentId = Guid.NewGuid() }, prepared.Payload, prepared.Binding));
    }

    [Theory]
    [InlineData((int)ProjectTaskPlanRefusal.BasisChanged, AgentPlanningPublicationOutcome.BasisChanged)]
    [InlineData((int)ProjectTaskPlanRefusal.EpochUnavailable, AgentPlanningPublicationOutcome.Refused)]
    public void Definite_non_effect_retains_the_original_operation_outcome(int refusal, AgentPlanningPublicationOutcome outcome) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var prepared = fixture.Prepare();
        var command = ProjectTaskPlanProcessCodec.Resolve(prepared.Observation, fixture.Target.Session, prepared.Payload, prepared.Binding).Command;
        var result = ProjectTaskPlanAgentResult.From(new ProjectTaskPlanOutcome.Rejected(command.Operation, (ProjectTaskPlanRefusal)refusal));
        AgentSavedEffectDispatchResult response = new(prepared.Binding, AgentToolProposalState.Completed, AgentToolEffectState.NotCommitted, 1, 1, result.OwnerResult);
        Assert.Equal(outcome, ProjectTaskPlanAgentResult.Classify(command, response).Outcome);
        Refused(() => ProjectTaskPlanAgentResult.Classify(command, new(prepared.Binding, AgentToolProposalState.Completed,
            AgentToolEffectState.NotCommitted, 1, 1, (result with { OperationId = Guid.NewGuid() }).OwnerResult)));
    }

    [Fact]
    public void Committed_receipt_must_match_original_source_semantics_and_result_identity() {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var prepared = fixture.Prepare();
        var command = ProjectTaskPlanProcessCodec.Resolve(prepared.Observation, fixture.Target.Session, prepared.Payload, prepared.Binding).Command;
        ProjectTaskPlanTaskFact task = new(Guid.NewGuid(), "task:review", null, "Review", "", "", "Draft", 2, "", 0,
            null, null, null, "Original task snapshot", null, new(ProjectTaskExecutionState.NotStarted, null, null));
        ProjectTaskPlanAccepted accepted = new(command.Operation, command.Basis.Project.Expectation, command.Source,
            ProjectTaskPlanOperationCodec.Digest(command), ProjectTaskPlanProcessCodecFixture.Start,
            fixture.Target.WriteContext.ReplayUntilUtc, [new(0, task, "{}", 0, 0, ProjectTaskPlanProcessCodecFixture.Start)]);
        var receipt = ProjectTaskPlanAgentResult.Receipt(command, accepted);
        Assert.Equal(AgentPlanningPublicationOutcome.Committed, ProjectTaskPlanAgentResult.Classify(command,
            new(prepared.Binding, AgentToolProposalState.OwnerReceiptConfirmed, AgentToolEffectState.Committed, 1, 1, receipt.Receipt)).Outcome);
        ProjectTaskPlanCommand changed = new(command.Operation, command.Basis, command.Proposal, command.Policy,
            new(command.Source.DecisionId, command.Source.Generation, command.Source.DecisionStartedAtUtc,
                ProjectTaskPlanSourceKind.OrdinaryAgentDecision, command.Source.Version, command.Source.Fingerprint), command.SchemaVersion);
        Assert.Throws<InvalidDataException>(() => ProjectTaskPlanAgentResult.Receipt(changed, accepted));
        var inconsistent = new ProjectTaskPlanAgentResult(ProjectTaskPlanAgentResultKind.Published, command.Operation.Id, null, [], true, false);
        Refused(() => ProjectTaskPlanAgentResult.Classify(command, new(prepared.Binding, AgentToolProposalState.OwnerReceiptConfirmed,
            AgentToolEffectState.Committed, 1, 1, inconsistent.OwnerResult)));
    }

    private static void Refused(Action action) {
        var failure = Record.Exception(action);
        Assert.True(failure is AgentToolAdmissionException or InvalidDataException or ArgumentException or JsonException,
            $"Expected a protocol refusal, observed {failure?.GetType().Name ?? "successful admission"}: {failure?.Message}");
    }
}
