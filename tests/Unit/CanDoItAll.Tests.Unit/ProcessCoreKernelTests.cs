using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessCoreKernelTests
{
    [Fact]
    public void Identifiers_reject_empty_values_and_trim_tokens()
    {
        Assert.Throws<ArgumentException>(() => new ProcessDefinitionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new CapabilityTag(" "));

        var tag = new CapabilityTag(" capability.alpha ");

        Assert.Equal("capability.alpha", tag.Value);
    }

    [Fact]
    public void Runtime_event_envelope_requires_version_actor_sensitivity_utc_and_payload_hash()
    {
        var envelope = ValidEnvelope() with
        {
            SchemaVersion = "unexpected",
            Actor = new ProcessEventActor(ProcessEventActorKind.Unknown, new ProcessActorId("system")),
            Sensitivity = ProcessEventSensitivity.Unspecified,
            OccurredAtUtc = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(1)),
            PayloadHash = ""
        };

        var result = ProcessRuntimeEventRules.Validate(envelope);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Code == "RuntimeEvent.UnsupportedSchema");
        Assert.Contains(result.Failures, failure => failure.Code == "RuntimeEvent.UnknownActor");
        Assert.Contains(result.Failures, failure => failure.Code == "RuntimeEvent.MissingSensitivity");
        Assert.Contains(result.Failures, failure => failure.Code == "RuntimeEvent.TimestampNotUtc");
        Assert.Contains(result.Failures, failure => failure.Code == "RuntimeEvent.MissingPayloadHash");
    }

    [Fact]
    public void Runtime_event_envelope_accepts_explicit_current_contract_shape()
    {
        var result = ProcessRuntimeEventRules.Validate(ValidEnvelope());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Graph_validation_rejects_duplicate_keys_unknown_edges_and_unmarked_cycles()
    {
        var first = ProcessStepDefinitionId.New();
        var second = ProcessStepDefinitionId.New();
        var missing = ProcessStepDefinitionId.New();
        var definition = NewDefinition(
            [
                new ProcessGraphNode(first, "start", ProcessStepKind.Start),
                new ProcessGraphNode(second, "start", ProcessStepKind.Activity)
            ],
            [
                new ProcessGraphEdge(first, second),
                new ProcessGraphEdge(second, first),
                new ProcessGraphEdge(first, missing)
            ]);

        var result = ProcessGraphKernel.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Code == "Definition.DuplicateStepKey");
        Assert.Contains(result.Failures, failure => failure.Code == "Definition.EdgeTargetMissing");
        Assert.Contains(result.Failures, failure => failure.Code == "Definition.ForwardCycle");
    }

    [Fact]
    public void Backward_graph_edges_require_loop_budget()
    {
        var first = ProcessStepDefinitionId.New();
        var second = ProcessStepDefinitionId.New();
        var definition = NewDefinition(
            [
                new ProcessGraphNode(first, "start", ProcessStepKind.Start),
                new ProcessGraphNode(second, "review", ProcessStepKind.Activity)
            ],
            [
                new ProcessGraphEdge(first, second),
                new ProcessGraphEdge(second, first, IsBackwardRoute: true)
            ]);

        var result = ProcessGraphKernel.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Code == "Definition.BackwardEdgeMissingBudget");
    }

    [Fact]
    public void Backward_graph_edges_with_budget_do_not_count_as_forward_cycles()
    {
        var first = ProcessStepDefinitionId.New();
        var second = ProcessStepDefinitionId.New();
        var definition = NewDefinition(
            [
                new ProcessGraphNode(first, "start", ProcessStepKind.Start),
                new ProcessGraphNode(second, "review", ProcessStepKind.Activity)
            ],
            [
                new ProcessGraphEdge(first, second),
                new ProcessGraphEdge(second, first, IsBackwardRoute: true, LoopBudget: NewLoopBudget())
            ]);

        var result = ProcessGraphKernel.Validate(definition);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Artifact_rules_reject_missing_sensitivity_unknown_artifacts_and_unapproved_boundary_slots()
    {
        var artifact = ArtifactDefinitionId.New();
        var unknownArtifact = ArtifactDefinitionId.New();

        var result = ProcessArtifactRules.Validate(
            [new ProcessArtifactDefinition(artifact, "primary", ProcessArtifactSensitivity.Unspecified)],
            [
                new ProcessArtifactSlotDefinition(
                    ArtifactSlotId.New(),
                    "parent-input",
                    unknownArtifact,
                    ProcessArtifactRequirementMode.Required,
                    ProcessArtifactScope.Parent,
                    HasBoundaryPolicy: false)
            ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Code == "Artifact.MissingSensitivity");
        Assert.Contains(result.Failures, failure => failure.Code == "ArtifactSlot.UnknownArtifact");
        Assert.Contains(result.Failures, failure => failure.Code == "ArtifactSlot.MissingBoundaryPolicy");
    }

    [Fact]
    public void Branch_rules_reject_step_targets_without_step_and_backward_routes_without_budget()
    {
        var branch = new ProcessBranchDefinition(
            ProcessStepDefinitionId.New(),
            new BranchFamilyId("decision"),
            [],
            [
                new BranchOutcomeDefinition(
                    new BranchOutcomeId("specific"),
                    "Specific",
                    BranchOutcomeCategory.Continue,
                    new ProcessRouteTarget(ProcessRouteTargetKind.SpecificStep)),
                new BranchOutcomeDefinition(
                    new BranchOutcomeId("repeat"),
                    "Repeat",
                    BranchOutcomeCategory.Repeat,
                    new ProcessRouteTarget(ProcessRouteTargetKind.PreviousStep))
            ]);

        var result = ProcessBranchRules.Validate([branch]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Code == "BranchRoute.MissingStepTarget");
        Assert.Contains(result.Failures, failure => failure.Code == "BranchRoute.BackwardMissingBudget");
    }

    [Fact]
    public void Branch_rules_accept_typed_outcomes_independent_of_display_label()
    {
        var branch = new ProcessBranchDefinition(
            ProcessStepDefinitionId.New(),
            new BranchFamilyId("decision"),
            [new BranchInputRequirement("input-a", BranchInputRequirementKind.Artifact, IsRequired: true)],
            [
                new BranchOutcomeDefinition(
                    new BranchOutcomeId("continue"),
                    "User-facing text can change",
                    BranchOutcomeCategory.Continue,
                    new ProcessRouteTarget(ProcessRouteTargetKind.NextStep))
            ]);

        var result = ProcessBranchRules.Validate([branch]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Loop_fingerprint_is_stable_and_uses_typed_route_inputs()
    {
        var rootRun = ProcessRunId.New();
        var step = ProcessStepDefinitionId.New();
        var first = new LoopFingerprintInput(
            rootRun,
            step,
            new BranchFamilyId("decision"),
            new BranchOutcomeId("repeat"),
            new LoopFingerprintPolicyId("path-and-evidence"),
            ["beta", "alpha"]);
        var second = first with
        {
            EvidenceKeys = ["alpha", "beta"]
        };
        var changed = first with
        {
            OutcomeId = new BranchOutcomeId("escalate")
        };

        Assert.Equal(ProcessLoopFingerprint.Create(first), ProcessLoopFingerprint.Create(second));
        Assert.NotEqual(ProcessLoopFingerprint.Create(first), ProcessLoopFingerprint.Create(changed));
    }

    [Fact]
    public void State_transition_rules_prevent_terminal_mutation()
    {
        Assert.True(ProcessStateTransitionRules.CanTransition(ProcessRunState.Planned, ProcessRunState.Running));
        Assert.False(ProcessStateTransitionRules.CanTransition(ProcessRunState.Completed, ProcessRunState.Running));
        Assert.True(ProcessStateTransitionRules.Validate(ProcessStepState.Pending, ProcessStepState.Ready).IsValid);

        var result = ProcessStateTransitionRules.Validate(ProcessStepState.Completed, ProcessStepState.Running);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Code == "Runtime.InvalidStepTransition");
    }

    private static ProcessRuntimeEventEnvelope ValidEnvelope()
    {
        return new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            ProcessRunId.New(),
            ProcessRunId.New(),
            new ProcessCorrelationId("correlation-1"),
            null,
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("system")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
            new ProcessEventType("run.started"),
            "sha256:1234");
    }

    private static ProcessDefinitionKernel NewDefinition(
        IReadOnlyList<ProcessGraphNode> steps,
        IReadOnlyList<ProcessGraphEdge> edges)
    {
        return new ProcessDefinitionKernel(
            ProcessDefinitionId.New(),
            ProcessDefinitionVersionId.New(),
            steps,
            edges,
            [],
            [],
            []);
    }

    private static LoopBudgetDefinition NewLoopBudget()
    {
        return new LoopBudgetDefinition(
            2,
            new LoopFingerprintPolicyId("path-and-evidence"),
            new ProcessRouteTarget(ProcessRouteTargetKind.Escalate));
    }
}
