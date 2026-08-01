using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessMafHardeningRegressionTests
{
    private static readonly ProcessInstancePlanId PlanId = new(new Guid("11111111-1111-1111-1111-111111111111"));
    private static readonly ProcessStepInstanceId ParentStepId = new(new Guid("22222222-2222-2222-2222-222222222222"));
    private static readonly ProcessStepDefinitionId ParentStepDefinitionId = new(new Guid("33333333-3333-3333-3333-333333333333"));
    private static readonly ArtifactSlotId ParentArtifactSlotId = new(new Guid("44444444-4444-4444-4444-444444444444"));
    private static readonly ArtifactSlotId ChildArtifactSlotId = new(new Guid("55555555-5555-5555-5555-555555555555"));
    private const string DefaultAcceptedChildArtifactContent =
        "## Runtime Accepted Completion Gates\nStatus: Completed";

    [Fact]
    public void Template_pack_loads_with_typed_subprocess_contracts()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var pack = loader.Load();

        Assert.Contains(pack.Definitions, definition => definition.Key == "dotnet-development-slice");
        Assert.Contains(pack.Definitions, definition => definition.Key == "software-delivery");
    }

    [Fact]
    public void Template_subprocess_contract_rejects_overlapping_accepted_and_no_go_outputs()
    {
        var contract = new ProcessSubprocessContract
        {
            AcceptedChildOutputs =
            [
                new ProcessSubprocessChildOutputContract
                {
                    StepKey = "handoff",
                    ArtifactExpectationKey = "handoff-packet"
                }
            ],
            NoGoChildOutputs =
            [
                new ProcessSubprocessChildOutputContract
                {
                    StepKey = "handoff",
                    ArtifactExpectationKey = "handoff-packet"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProcessTemplatePackLoader.ValidateChildOutputDiscriminators(
                contract,
                "test-process.test-step"));

        Assert.Contains("overlapping accepted/no-go child outputs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_owned_parent_templates_have_machine_readable_subprocess_contracts()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var developmentSlice = loader.LoadDefinition("dotnet-development-slice");
        var softwareDelivery = loader.LoadDefinition("software-delivery");

        var subprocessParents = developmentSlice.Steps
            .Concat(softwareDelivery.Steps)
            .Where(step => string.Equals(step.StepKind, ProcessTemplateStepKinds.Subprocess, StringComparison.OrdinalIgnoreCase) ||
                           !string.IsNullOrWhiteSpace(step.SubprocessProcessKey))
            .ToArray();

        Assert.Equal(12, subprocessParents.Length);
        foreach (var step in subprocessParents)
        {
            Assert.NotNull(step.SubprocessContract);
            Assert.Equal(ProcessSubprocessLaunchMode.RuntimeOwned, step.SubprocessContract.LaunchMode);
            Assert.Equal(ProcessSubprocessMaterializationMode.RuntimeSynthesizedParentHandoff, step.SubprocessContract.MaterializationMode);
            Assert.False(string.IsNullOrWhiteSpace(step.SubprocessContract.ParentProducedArtifactExpectationKey));
            Assert.NotEmpty(step.SubprocessContract.AcceptedChildOutputs);
            Assert.All(step.SubprocessContract.AcceptedChildOutputs, output =>
            {
                Assert.False(string.IsNullOrWhiteSpace(output.StepKey));
                Assert.False(string.IsNullOrWhiteSpace(output.ArtifactExpectationKey));
            });
        }

        var prepareSkeleton = developmentSlice.Steps.Single(step => step.Key == "prepare-solution-skeleton");
        var prepareSkeletonContract = Assert.IsType<ProcessSubprocessContract>(prepareSkeleton.SubprocessContract);
        Assert.False(prepareSkeleton.AllowsManualSkip);
        Assert.Contains(
            prepareSkeletonContract.NoGoChildOutputs,
            output => output.StepKey == "setup-repair-escalation" &&
                      output.ArtifactExpectationKey == "setup-repair-escalation-packet");
        Assert.Contains(
            prepareSkeletonContract.AcceptedChildOutputs,
            output => output.StepKey == "setup-handoff-after-repair" &&
                      output.ArtifactExpectationKey == "setup-handoff-packet-after-repair");
    }

    [Fact]
    public void Step_contract_prompt_renders_semantic_artifact_descriptors_and_subprocess_mappings()
    {
        var stepContract = new ProcessStepExecutionContract(
            RequiredArtifacts: [],
            ExpectedProducedArtifacts: [new ExpectedProducedArtifactRef(ParentArtifactSlotId)],
            RequiredRuntimeToolNames: [],
            ContractHash: "sha256:test")
        {
            ArtifactDescriptors =
            [
                new ProcessArtifactSlotDescriptor(
                    ParentArtifactSlotId,
                    "prepare-solution-skeleton:solution-skeleton-evidence",
                    "prepare-solution-skeleton",
                    "solution-skeleton-evidence",
                    "Solution skeleton evidence",
                    "ManagedMarkdown",
                    "artifacts/process-runs/parent/steps/prepare-solution-skeleton.md",
                    ProcessArtifactMaterializationMode.RuntimeSynthesizedParentHandoff)
                {
                    PayloadSchema = "opaque.example/v1"
                }
            ],
            SubprocessArtifactMappings =
            [
                new SubprocessArtifactMappingDescriptor(
                    ParentArtifactSlotId,
                    "solution-skeleton-evidence",
                    "dotnet-solution-setup",
                    [
                        new SubprocessChildArtifactMappingDescriptor(
                            "setup-handoff",
                            "setup-handoff-packet",
                            "Setup handoff packet",
                            "setup-complete")
                    ],
                    [
                        new SubprocessChildArtifactMappingDescriptor(
                            "setup-repair-escalation",
                            "setup-repair-escalation-packet",
                            "Setup repair escalation packet",
                            "setup-repair-escalated")
                    ])
            ]
        };

        var prompt = ProcessStepContractPromptBuilder.Build("Do the work.", stepContract);

        Assert.Contains("solution-skeleton-evidence - Solution skeleton evidence", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/parent/steps/prepare-solution-skeleton.md", prompt, StringComparison.Ordinal);
        Assert.Contains("payload schema opaque.example/v1", prompt, StringComparison.Ordinal);
        Assert.Contains(nameof(ProcessArtifactMaterializationMode.RuntimeSynthesizedParentHandoff), prompt, StringComparison.Ordinal);
        Assert.Contains("setup-handoff", prompt, StringComparison.Ordinal);
        Assert.Contains("setup-repair-escalation", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_contract_prompt_distinguishes_required_runtime_tool_receipts_from_markdown_claims()
    {
        var stepContract = new ProcessStepExecutionContract(
            RequiredArtifacts: [],
            ExpectedProducedArtifacts: [],
            RequiredRuntimeToolNames:
            [
                "workspace_dotnet_restore",
                "workspace_dotnet_build",
                "workspace_dotnet_test"
            ],
            ContractHash: "sha256:validation-tools");

        var prompt = ProcessStepContractPromptBuilder.Build("Validate the scaffold.", stepContract);

        Assert.Contains("workspace_dotnet_restore", prompt, StringComparison.Ordinal);
        Assert.Contains("each listed tool must produce a receipt whose execution id is this exact execution attempt", prompt, StringComparison.Ordinal);
        Assert.Contains("markdown statement", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not replace those invocations with manual shell commands", prompt, StringComparison.Ordinal);
        Assert.Contains("current execution-run receipt from invoking that exact tool", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_contract_prompt_does_not_embed_domain_specific_source_inspection_policy()
    {
        var stepContract = new ProcessStepExecutionContract(
            RequiredArtifacts: [],
            ExpectedProducedArtifacts: [],
            RequiredRuntimeToolNames: [],
            ContractHash: "sha256:source-inspection");
        var launchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys] =
                JsonSerializer.Serialize(new[] { "targeted-recheck" })
        };

        var prompt = ProcessStepContractPromptBuilder.Build(
            "Recheck the repair.",
            stepContract,
            launchVariables,
            "targeted-recheck");

        Assert.Equal("Recheck the repair.", prompt);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_accepts_only_typed_child_outputs()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var childInternalRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/implementation.md";
        var acceptedContent = $"""
            {DefaultAcceptedChildArtifactContent}

            Opaque accepted child payload.
            Internal child trace: `{childInternalRef}`.
            SourceDocLink: managed-files/project-media/child-proof.md
            """;
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId, acceptedContent)])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [acceptedRef] = acceptedContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.AcceptedChildOutputBridged, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        var bridgedOutcome = Assert.IsType<ParentSubprocessBridgedOutcome>(result.BridgedOutcome);
        Assert.Contains(acceptedRef, bridgedOutcome.EvidenceRefs);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, bridgedOutcome.Output.Status);
        Assert.Equal(ChildOutputDisposition.Accepted, bridgedOutcome.Disposition);
        Assert.Equal("setup-handoff", bridgedOutcome.ChildStepKey);
        Assert.Equal("setup-handoff-packet", bridgedOutcome.ChildArtifactExpectationKey);
        Assert.Equal(acceptedContent, bridgedOutcome.VerifiedChildOutput.Content);
        var summary = Assert.IsType<string>(bridgedOutcome.Output.HumanReadableSummaryMarkdown);
        Assert.Contains("Opaque accepted child payload.", summary, StringComparison.Ordinal);
        Assert.Contains(childInternalRef, summary, StringComparison.Ordinal);
        Assert.Contains(
            "SourceDocLink: managed-files/project-media/child-proof.md",
            summary,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            summary.Split(
                ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker,
                StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData(16_000, "AcceptedChildOutputBridged", null)]
    [InlineData(
        50_000,
        "ChildForwardedContextUnavailable",
        "process.adapter.subprocess_handoff_size_limit_exceeded")]
    [InlineData(
        64_000,
        "ChildForwardedContextUnavailable",
        "process.adapter.subprocess_forwarded_context_read_failed")]
    public async Task Parent_subprocess_bridge_enforces_shared_limits_for_hash_verified_declared_child_input(
        int paddingCharacters,
        string expectedKind,
        string? expectedIssueCode)
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var forwardedSlotId = ArtifactSlotId.New();
        var forwardedArtifactId = ArtifactInstanceId.New();
        var forwardedContent =
            "  \r\n## Bootstrap decision\r\n\r\n```json\r\n" +
            "{ \"schema\": \"opaque.example/v1\", \"value\": \"preserve exact content\" }\r\n" +
            "```\r\n  " +
            new string('x', paddingCharacters);
        var assignment = WithSubprocessContract(
            CreateParentAssignment(parentRunId),
            new ProcessSubprocessContract
            {
                DefinitionKey = "dotnet-solution-setup",
                ParentProducedArtifactExpectationKey = "solution-skeleton-evidence",
                AcceptedChildOutputs =
                [
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        ArtifactTitle = "Setup handoff packet"
                    }
                ],
                ForwardedChildContextArtifacts =
                [
                    new ProcessSubprocessForwardedChildContextArtifactContract
                    {
                        BindingKey = "opaque-bootstrap",
                        SourceStepKey = "architecture",
                        ArtifactExpectationKey = "opaque-decision",
                        PayloadSchema = "opaque.example/v1"
                    }
                ]
            });
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var childState = WithForwardedChildInput(
            NewRuntimeState(
                parentRunId,
                childRunId,
                ProcessRuntimeStatus.Completed,
                childAssignment,
                ProcessRuntimeStepStatus.Completed,
                [CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId)]),
            childAssignment,
            childRunId,
            forwardedSlotId,
            forwardedArtifactId,
            forwardedContent,
            ComputeContentHash(forwardedContent));
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var forwardedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/architecture.md";
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                childState),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [acceptedRef] = DefaultAcceptedChildArtifactContent,
                [forwardedRef] = forwardedContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.True(forwardedContent.Length > 16_000);
        Assert.Equal(expectedKind, result.Kind.ToString());
        if (expectedIssueCode is not null)
        {
            Assert.Equal(expectedIssueCode, result.ForwardedContextIssue?.Code);
            Assert.Null(result.BridgedOutcome);
            return;
        }

        var outcome = Assert.IsType<ParentSubprocessBridgedOutcome>(result.BridgedOutcome);
        var forwardedArtifact = Assert.Single(outcome.ForwardedContextArtifacts);
        Assert.Equal("opaque-bootstrap", forwardedArtifact.BindingKey);
        Assert.Equal(forwardedContent, forwardedArtifact.Content);
        Assert.Contains("Runtime-forwarded child context", outcome.Output.HumanReadableSummaryMarkdown, StringComparison.Ordinal);
        Assert.Contains(forwardedContent, outcome.Output.HumanReadableSummaryMarkdown, StringComparison.Ordinal);
        Assert.Contains("````", outcome.Output.HumanReadableSummaryMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Outcome_grounding_accepts_one_sanitized_verified_forwarded_context_envelope()
    {
        var fixture = CreateForwardedContextGroundingFixture();

        Assert.Equal(fixture.RawEnvelope, fixture.VerifiedEnvelope);
        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.NormalizedOutput,
            [fixture.TrustedReceipt],
            fixture.BridgedOutcome);

        Assert.Null(issue);
    }

    [Fact]
    public void Grounding_accepts_path_from_hash_verified_required_artifact_content()
    {
        const string groundedPath =
            "artifacts/process-runs/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/steps/child-handoff.md";
        var fixture = CreateRequiredArtifactGroundingFixture(groundedPath);
        var validator = new ProcessOutcomeGroundingValidator(fixture.WorkspaceFiles);

        var outcomeIssue = validator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            fixture.StepContract);
        var bodyIssue = validator.ValidateManagedArtifactBodyReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            stepContract: fixture.StepContract);

        Assert.Null(outcomeIssue);
        Assert.Null(bodyIssue);
    }

    [Theory]
    [InlineData("hash-mismatch")]
    [InlineData("unavailable")]
    public void Grounding_rejects_unverified_required_artifact_content(string scenario)
    {
        const string groundedPath =
            "artifacts/process-runs/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/steps/child-handoff.md";
        var fixture = CreateRequiredArtifactGroundingFixture(
            groundedPath,
            availability: scenario == "unavailable"
                ? ProcessArtifactInputAvailability.Missing
                : ProcessArtifactInputAvailability.Available,
            contentHashOverride: scenario == "hash-mismatch"
                ? "sha256:" + new string('0', 64)
                : null);
        var validator = new ProcessOutcomeGroundingValidator(fixture.WorkspaceFiles);

        var outcomeIssue = validator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            fixture.StepContract);
        var bodyIssue = validator.ValidateManagedArtifactBodyReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            stepContract: fixture.StepContract);

        Assert.NotNull(outcomeIssue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", outcomeIssue.Code);
        Assert.NotNull(bodyIssue);
        Assert.Equal("process.adapter.ungrounded_managed_artifact_reference", bodyIssue.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Grounding_rejects_ambiguous_required_artifact_descriptors(
        bool duplicateIdenticalRef)
    {
        const string groundedPath =
            "artifacts/process-runs/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/steps/child-handoff.md";
        var fixture = CreateRequiredArtifactGroundingFixture(groundedPath);
        var descriptor = Assert.Single(fixture.StepContract.ArtifactDescriptors);
        var duplicateDescriptor = descriptor with
        {
            SlotKey = "duplicate-upstream-evidence",
            PrimaryManagedRef = duplicateIdenticalRef
                ? descriptor.PrimaryManagedRef
                : "managed-files/project-media/not-managed-process-evidence.png"
        };
        var stepContract = fixture.StepContract with
        {
            ArtifactDescriptors = [descriptor, duplicateDescriptor]
        };
        var validator = new ProcessOutcomeGroundingValidator(fixture.WorkspaceFiles);

        var outcomeIssue = validator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            stepContract);
        var bodyIssue = validator.ValidateManagedArtifactBodyReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            stepContract: stepContract);

        Assert.NotNull(outcomeIssue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", outcomeIssue.Code);
        Assert.NotNull(bodyIssue);
        Assert.Equal("process.adapter.ungrounded_managed_artifact_reference", bodyIssue.Code);
    }

    [Fact]
    public void Grounding_does_not_trust_prompt_discovered_artifact_body_without_typed_contract()
    {
        const string groundedPath =
            "artifacts/process-runs/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/steps/child-handoff.md";
        var fixture = CreateRequiredArtifactGroundingFixture(
            groundedPath,
            promptNamesUpstreamArtifact: true);
        var validator = new ProcessOutcomeGroundingValidator(fixture.WorkspaceFiles);

        var outcomeIssue = validator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            ProcessStepExecutionContract.Empty);
        var bodyIssue = validator.ValidateManagedArtifactBodyReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            stepContract: ProcessStepExecutionContract.Empty);

        Assert.NotNull(outcomeIssue);
        Assert.NotNull(bodyIssue);
    }

    [Fact]
    public void Grounding_rejects_mixed_run_reference_despite_verified_upstream_content()
    {
        const string groundedPath =
            "artifacts/process-runs/640df2d5-6168-4a52-b1c3-c6418c3a66c8/steps/store-ui-screenshots.md";
        const string mixedRunPath =
            "artifacts/process-runs/640df2d5-2fea-4242-8d66-6ce6edea2ef9/steps/store-ui-screenshots.md";
        var fixture = CreateRequiredArtifactGroundingFixture(
            groundedPath,
            candidatePath: mixedRunPath);
        var validator = new ProcessOutcomeGroundingValidator(fixture.WorkspaceFiles);

        var outcomeIssue = validator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            fixture.StepContract);
        var bodyIssue = validator.ValidateManagedArtifactBodyReferences(
            fixture.Assignment,
            fixture.Output,
            [],
            stepContract: fixture.StepContract);

        Assert.NotNull(outcomeIssue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", outcomeIssue.Code);
        Assert.NotNull(bodyIssue);
        Assert.Equal("process.adapter.ungrounded_managed_artifact_reference", bodyIssue.Code);
    }

    [Fact]
    public void Completion_blocker_gate_ignores_authenticated_child_defect_text()
    {
        const string childDefectText =
            "Accepted image asset node ids: none. No current-run screenshot was accepted as target-aligned visual proof.";
        var fixture = CreateForwardedContextGroundingFixture(
            childOutputContent:
                $"{DefaultAcceptedChildArtifactContent}{Environment.NewLine}{Environment.NewLine}{childDefectText}");

        var issue = ProcessProductCompletionStateGate.ValidateCompletedOutcomeDoesNotDeclareBlockers(
            fixture.Assignment,
            fixture.NormalizedOutput,
            [fixture.TrustedReceipt],
            fixture.BridgedOutcome);

        Assert.Null(issue);
    }

    [Fact]
    public void Completion_blocker_gate_does_not_trust_child_defect_text_without_exact_bridge_receipt()
    {
        const string childDefectText =
            "Accepted image asset node ids: none. No current-run screenshot was accepted as target-aligned visual proof.";
        var fixture = CreateForwardedContextGroundingFixture(
            childOutputContent:
                $"{DefaultAcceptedChildArtifactContent}{Environment.NewLine}{Environment.NewLine}{childDefectText}");
        var mismatchedReceipt = fixture.TrustedReceipt with
        {
            Id = Guid.NewGuid()
        };

        var issue = ProcessProductCompletionStateGate.ValidateCompletedOutcomeDoesNotDeclareBlockers(
            fixture.Assignment,
            fixture.NormalizedOutput,
            [mismatchedReceipt],
            fixture.BridgedOutcome);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.completed_outcome_declares_unresolved_blocker", issue.Code);
    }

    [Fact]
    public void Completion_blocker_gate_still_rejects_parent_owned_defect_text()
    {
        const string parentDefectText =
            "No current-run screenshot was accepted as target-aligned visual proof.";
        var fixture = CreateForwardedContextGroundingFixture();
        var output = CopyWithReason(fixture.NormalizedOutput, parentDefectText);

        var issue = ProcessProductCompletionStateGate.ValidateCompletedOutcomeDoesNotDeclareBlockers(
            fixture.Assignment,
            output,
            [fixture.TrustedReceipt],
            fixture.BridgedOutcome);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.completed_outcome_declares_unresolved_blocker", issue.Code);
    }

    [Fact]
    public void Outcome_grounding_requires_the_same_runtime_bridge_receipt_identity()
    {
        var fixture = CreateForwardedContextGroundingFixture();
        var semanticallyEquivalentReceipt = fixture.TrustedReceipt with
        {
            Id = Guid.NewGuid()
        };

        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.NormalizedOutput,
            [semanticallyEquivalentReceipt],
            fixture.BridgedOutcome);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", issue.Code);
        Assert.Contains("reserved runtime child-output or forwarded-context envelope", issue.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("begin-only")]
    [InlineData("end-only")]
    [InlineData("reversed")]
    [InlineData("mismatched")]
    public void Forwarded_context_envelope_removal_rejects_noncanonical_structure(string scenario)
    {
        var fixture = CreateForwardedContextGroundingFixture();
        var invalidContent = scenario switch
        {
            "duplicate" => $"{fixture.VerifiedEnvelope}{Environment.NewLine}{fixture.VerifiedEnvelope}",
            "begin-only" => ParentSubprocessForwardedContextEnvelope.BeginMarker,
            "end-only" => ParentSubprocessForwardedContextEnvelope.EndMarker,
            "reversed" => $"{ParentSubprocessForwardedContextEnvelope.EndMarker}{Environment.NewLine}{ParentSubprocessForwardedContextEnvelope.BeginMarker}",
            "mismatched" => fixture.VerifiedEnvelope.Replace(
                "runtime-project/v1",
                "runtime-project/v2",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown envelope scenario.")
        };

        var result = ParentSubprocessForwardedContextEnvelope.TryRemoveSingleVerified(
            invalidContent,
            fixture.VerifiedEnvelope,
            out var unchangedContent);

        Assert.Equal(ParentSubprocessForwardedContextEnvelope.MatchResult.Invalid, result);
        Assert.Equal(invalidContent, unchangedContent);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("begin-only")]
    [InlineData("end-only")]
    [InlineData("reversed")]
    [InlineData("mismatched")]
    public void Verified_child_output_envelope_removal_rejects_noncanonical_structure(string scenario)
    {
        var fixture = CreateForwardedContextGroundingFixture();
        var invalidContent = scenario switch
        {
            "duplicate" => $"{fixture.VerifiedChildOutputEnvelope}{Environment.NewLine}{fixture.VerifiedChildOutputEnvelope}",
            "begin-only" => ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker,
            "end-only" => ParentSubprocessVerifiedChildOutputEnvelope.EndMarker,
            "reversed" => $"{ParentSubprocessVerifiedChildOutputEnvelope.EndMarker}{Environment.NewLine}{ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker}",
            "mismatched" => fixture.VerifiedChildOutputEnvelope.Replace(
                "Status: Completed",
                "Status: Tampered",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown envelope scenario.")
        };

        var result = ParentSubprocessVerifiedChildOutputEnvelope.TryRemoveSingleVerified(
            invalidContent,
            fixture.VerifiedChildOutputEnvelope,
            out var unchangedContent);

        Assert.Equal(ParentSubprocessVerifiedChildOutputEnvelope.MatchResult.Invalid, result);
        Assert.Equal(invalidContent, unchangedContent);
    }

    [Fact]
    public void Verified_child_output_envelope_removal_rejects_payload_line_ending_tampering()
    {
        const string originalContent = "first payload line\r\nsecond payload line";
        const string tamperedContent = "first payload line\nsecond payload line";
        var verifiedEnvelope = ParentSubprocessVerifiedChildOutputEnvelope.Format(
            new ProcessSubprocessVerifiedChildArtifact(
                "artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/child.md",
                "child-step",
                "child-artifact",
                "sha256:original-content",
                originalContent));
        var tamperedEnvelope = verifiedEnvelope.Replace(
            originalContent,
            tamperedContent,
            StringComparison.Ordinal);

        var result = ParentSubprocessVerifiedChildOutputEnvelope.TryRemoveSingleVerified(
            tamperedEnvelope,
            verifiedEnvelope,
            out var unchangedContent);

        Assert.NotEqual(verifiedEnvelope, tamperedEnvelope);
        Assert.Equal(ParentSubprocessVerifiedChildOutputEnvelope.MatchResult.Invalid, result);
        Assert.Equal(tamperedEnvelope, unchangedContent);
    }

    [Fact]
    public void Managed_branch_reader_ignores_branch_keys_inside_commonmark_info_fences()
    {
        const string content = """
            ```text
            Branch outcome key: hidden-backtick-key
            ```

            ~~~json
            Branch outcome key: hidden-tilde-key
            ~~~
            """;

        var keys = ProcessManagedArtifactBranchOutcomeReader.ReadKeys(content);

        Assert.Empty(keys);
    }

    [Fact]
    public void Managed_branch_reader_reads_canonical_key_after_commonmark_fence()
    {
        const string content = """
            ~~~json
            Branch outcome key: hidden-key
            ~~~~

            ## Branch Outcome
            - Key: expected-key
            """;

        var keys = ProcessManagedArtifactBranchOutcomeReader.ReadKeys(content);

        Assert.Equal(["expected-key"], keys);
    }

    [Theory]
    [InlineData("    ", "```")]
    [InlineData("\t", "~~~")]
    public void Managed_branch_reader_does_not_hide_keys_behind_over_indented_fence_lines(
        string indentation,
        string fence)
    {
        var content = string.Join(
            "\n",
            "Branch outcome key: accepted-key",
            $"{indentation}{fence}text",
            "Branch outcome key: conflicting-key",
            $"{indentation}{fence}");

        var keys = ProcessManagedArtifactBranchOutcomeReader.ReadKeys(content);

        Assert.Equal(["accepted-key", "conflicting-key"], keys);
    }

    [Fact]
    public void Outcome_grounding_rejects_tampered_verified_child_output_envelope()
    {
        var fixture = CreateForwardedContextGroundingFixture();
        var tamperedSummary = Assert.IsType<string>(fixture.NormalizedOutput.HumanReadableSummaryMarkdown)
            .Replace("Status: Completed", "Status: Tampered", StringComparison.Ordinal);
        var output = CopyWithSummary(fixture.NormalizedOutput, tamperedSummary);

        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            output,
            [fixture.TrustedReceipt],
            fixture.BridgedOutcome);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", issue.Code);
        Assert.Contains("reserved runtime child-output or forwarded-context envelope", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Outcome_grounding_accepts_forwarded_envelope_nested_in_verified_child_payload()
    {
        var nestedForwardedEnvelope = ParentSubprocessForwardedContextEnvelope.Format(
        [
            new ParentSubprocessForwardedContextArtifact(
                "nested-context",
                "nested-source",
                "nested-artifact",
                "nested/v1",
                "artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/nested.md",
                "Nested runtime context.")
        ]);
        var fixture = CreateForwardedContextGroundingFixture(
            childOutputContent:
                $"{DefaultAcceptedChildArtifactContent}{Environment.NewLine}{nestedForwardedEnvelope}");

        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.NormalizedOutput,
            [fixture.TrustedReceipt],
            fixture.BridgedOutcome);

        Assert.Null(issue);
        Assert.Contains(
            nestedForwardedEnvelope,
            fixture.NormalizedOutput.HumanReadableSummaryMarkdown,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Outcome_grounding_accepts_verified_child_envelope_nested_in_forwarded_context()
    {
        var nestedChildEnvelope = ParentSubprocessVerifiedChildOutputEnvelope.Format(
            new ProcessSubprocessVerifiedChildArtifact(
                "artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/nested.md",
                "nested-step",
                "nested-artifact",
                "sha256:nested",
                DefaultAcceptedChildArtifactContent));
        var fixture = CreateForwardedContextGroundingFixture(
            forwardedPayload:
                $"Nested runtime context.{Environment.NewLine}{nestedChildEnvelope}");

        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            fixture.NormalizedOutput,
            [fixture.TrustedReceipt],
            fixture.BridgedOutcome);

        Assert.Null(issue);
        Assert.Contains(
            nestedChildEnvelope,
            fixture.NormalizedOutput.HumanReadableSummaryMarkdown,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Outcome_grounding_does_not_hide_ungrounded_ref_outside_verified_envelope()
    {
        var fixture = CreateForwardedContextGroundingFixture();
        const string outsideRef =
            "artifacts/process-runs/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/steps/untrusted.md";
        var output = CopyWithSummary(
            fixture.NormalizedOutput,
            $"{fixture.NormalizedOutput.HumanReadableSummaryMarkdown}{Environment.NewLine}{Environment.NewLine}Outside ref: `{outsideRef}`");

        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            fixture.Assignment,
            output,
            [fixture.TrustedReceipt],
            fixture.BridgedOutcome);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", issue.Code);
    }

    [Fact]
    public void Runtime_subprocess_envelope_budget_reserves_managed_artifact_readback_headroom()
    {
        var smallChildOutput = new ProcessSubprocessVerifiedChildArtifact(
            "artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/handoff.md",
            "handoff",
            "handoff-packet",
            "sha256:small",
            DefaultAcceptedChildArtifactContent);
        var oversizedChildOutput = smallChildOutput with
        {
            Content = new string(
                'x',
                ParentSubprocessRuntimeEnvelopeBudget.MaxCombinedEnvelopeCharacters)
        };

        Assert.True(ParentSubprocessRuntimeEnvelopeBudget.IsWithinLimit(
            smallChildOutput,
            [],
            out var smallEnvelopeCharacters));
        Assert.True(
            smallEnvelopeCharacters <
            ParentSubprocessRuntimeEnvelopeBudget.MaxCombinedEnvelopeCharacters);
        Assert.False(ParentSubprocessRuntimeEnvelopeBudget.IsWithinLimit(
            oversizedChildOutput,
            [],
            out var oversizedEnvelopeCharacters));
        Assert.True(
            oversizedEnvelopeCharacters >
            ParentSubprocessRuntimeEnvelopeBudget.MaxCombinedEnvelopeCharacters);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_rejects_forwarded_child_input_with_mismatched_content_hash()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var forwardedSlotId = ArtifactSlotId.New();
        var assignment = WithSubprocessContract(
            CreateParentAssignment(parentRunId),
            new ProcessSubprocessContract
            {
                DefinitionKey = "dotnet-solution-setup",
                ParentProducedArtifactExpectationKey = "solution-skeleton-evidence",
                AcceptedChildOutputs =
                [
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        ArtifactTitle = "Setup handoff packet"
                    }
                ],
                ForwardedChildContextArtifacts =
                [
                    new ProcessSubprocessForwardedChildContextArtifactContract
                    {
                        BindingKey = "opaque-bootstrap",
                        SourceStepKey = "architecture",
                        ArtifactExpectationKey = "opaque-decision",
                        PayloadSchema = "opaque.example/v1"
                    }
                ]
            });
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        const string forwardedContent = "opaque child content";
        var childState = WithForwardedChildInput(
            NewRuntimeState(
                parentRunId,
                childRunId,
                ProcessRuntimeStatus.Completed,
                childAssignment,
                ProcessRuntimeStepStatus.Completed,
                [CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId)]),
            childAssignment,
            childRunId,
            forwardedSlotId,
            ArtifactInstanceId.New(),
            forwardedContent,
            "sha256:not-the-content");
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var forwardedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/architecture.md";
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                childState),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [acceptedRef] = DefaultAcceptedChildArtifactContent,
                [forwardedRef] = forwardedContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildForwardedContextUnavailable, result.Kind);
        var issue = Assert.IsType<ParentSubprocessForwardedContextIssue>(result.ForwardedContextIssue);
        Assert.Equal("process.adapter.subprocess_forwarded_context_hash_mismatch", issue.Code);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_rejects_physical_child_output_without_accepted_ledger()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Completed, childAssignment)),
            new FakeWorkspaceFileService([acceptedRef]),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.Null(result.BridgedOutcome);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_rejects_child_output_with_mismatched_ledger_hash()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [CreateProducedArtifactReceipt(
                        childAssignment,
                        ChildArtifactSlotId,
                        DefaultAcceptedChildArtifactContent + "\ntampered-after-ledger")])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [acceptedRef] = DefaultAcceptedChildArtifactContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Null(result.BridgedOutcome);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_uses_only_the_current_completed_result_receipt()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var staleReceipt = CreateProducedArtifactReceipt(
            childAssignment,
            ChildArtifactSlotId,
            DefaultAcceptedChildArtifactContent);
        var currentReceipt = CreateProducedArtifactReceipt(
            childAssignment,
            ChildArtifactSlotId,
            DefaultAcceptedChildArtifactContent + "\ncurrent-result-content");
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [staleReceipt, currentReceipt])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [acceptedRef] = DefaultAcceptedChildArtifactContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Null(result.BridgedOutcome);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_rejects_duplicate_current_slot_artifacts()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var receipt = CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId) with
        {
            ProducedArtifacts =
            [
                new StrategyResultArtifactReceipt(
                    ChildArtifactSlotId,
                    ArtifactInstanceId.New(),
                    ComputeContentHash(DefaultAcceptedChildArtifactContent)),
                new StrategyResultArtifactReceipt(
                    ChildArtifactSlotId,
                    ArtifactInstanceId.New(),
                    ComputeContentHash(DefaultAcceptedChildArtifactContent))
            ]
        };
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [receipt])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [acceptedRef] = DefaultAcceptedChildArtifactContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Null(result.BridgedOutcome);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_rejects_staged_child_output_without_gate_acceptance()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var stagedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var stagedContent = $"""
            # setup-handoff Process Step Outcome

            {ProcessManagedArtifactService.ManagedOutcomeArtifactCapturedHeading}

            Completion gates have not accepted this output yet.
            """;
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId, stagedContent)])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [stagedRef] = stagedContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.Null(result.BridgedOutcome);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_rejects_typed_no_go_child_outputs()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            StepKey = "setup-repair-escalation",
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var noGoRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-repair-escalation.md";
        var noGoContent = $"""
            {ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}

            ### Branch Outcome
            - Key: setup-no-go

            ### Summary
            The child produced a bounded no-go packet.
            """;
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId)])),
            new FakeWorkspaceFileService([noGoRef]),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.NoGoChildOutputFound, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.Contains(noGoRef, result.EvidenceRefs);
        Assert.Null(result.BridgedOutcome);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_routes_declared_no_go_output_to_parent_branch()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = WithSubprocessContract(
            CreateParentAssignment(parentRunId),
            new ProcessSubprocessContract
            {
                DefinitionKey = "dotnet-solution-setup",
                ParentProducedArtifactExpectationKey = "solution-skeleton-evidence",
                AcceptedChildOutputs =
                [
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        ArtifactTitle = "Setup handoff packet"
                    }
                ],
                NoGoChildOutputs =
                [
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-repair-escalation",
                        ArtifactExpectationKey = "setup-repair-escalation-packet",
                        ArtifactTitle = "Setup repair escalation packet",
                        BranchOutcomeKey = "setup-no-go",
                        ParentBranchOutcomeKey = "manager-assisted-repair-required"
                    }
                ]
            });
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            StepKey = "setup-repair-escalation",
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var noGoRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-repair-escalation.md";
        var noGoOutput = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Manager-assisted repair is required.",
            BranchOutcomeKey = "setup-no-go",
            BranchOutcomeTitle = "Setup no-go",
            EvidenceRefs = [],
            NextActions = [],
            HumanReadableSummaryMarkdown = "Opaque routed no-go payload."
        };
        var childExecutionRunId = Guid.NewGuid();
        var noGoContent =
            ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactContent(
                childAssignment,
                noGoOutput,
                childExecutionRunId,
                noGoRef) +
            ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactAcceptanceContent(
                childAssignment,
                noGoOutput,
                childExecutionRunId,
                noGoRef);
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId, noGoContent)])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [noGoRef] = noGoContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.NoGoChildOutputBridged, result.Kind);
        var bridgedOutcome = Assert.IsType<ParentSubprocessBridgedOutcome>(result.BridgedOutcome);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, bridgedOutcome.Output.Status);
        Assert.Equal("manager-assisted-repair-required", bridgedOutcome.Output.BranchOutcomeKey);
        Assert.Contains("no-go", bridgedOutcome.Output.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(noGoRef, bridgedOutcome.EvidenceRefs);
        Assert.Equal(noGoContent, bridgedOutcome.VerifiedChildOutput.Content);
        Assert.Contains(
            "Opaque routed no-go payload.",
            bridgedOutcome.Output.HumanReadableSummaryMarkdown,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            bridgedOutcome.Output.HumanReadableSummaryMarkdown!.Split(
                ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker,
                StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_rejects_ambiguous_child_branch_keys()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = WithSubprocessContract(
            CreateParentAssignment(parentRunId),
            new ProcessSubprocessContract
            {
                DefinitionKey = "dotnet-solution-setup",
                ParentProducedArtifactExpectationKey = "solution-skeleton-evidence",
                AcceptedChildOutputs =
                [
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        BranchOutcomeKey = "setup-ready"
                    }
                ]
            });
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var acceptedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var ambiguousContent =
            $"""
            {ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}

            ## Branch Outcome

            - Key: setup-ready
            - Key: setup-no-go
            """;
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [CreateProducedArtifactReceipt(
                        childAssignment,
                        ChildArtifactSlotId,
                        ambiguousContent)])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [acceptedRef] = ambiguousContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Null(result.BridgedOutcome);
    }

    [Theory]
    [InlineData("implementation-attempt-incomplete", "NoGoChildOutputFound")]
    [InlineData("feature-accepted", "AcceptedChildOutputBridged")]
    [InlineData("visual-defect-observed", "AcceptedChildOutputBridged")]
    [InlineData("no-ui-evidence-recorded", "AcceptedChildOutputBridged")]
    public async Task Parent_subprocess_bridge_honors_typed_child_output_branch(
        string artifactBranchOutcomeKey,
        string expectedKind)
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = WithSubprocessContract(
            CreateParentAssignment(parentRunId),
            new ProcessSubprocessContract
            {
                DefinitionKey = "dotnet-solution-setup",
                ParentProducedArtifactExpectationKey = "solution-skeleton-evidence",
                AcceptedChildOutputs =
                [
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        ArtifactTitle = "Setup handoff packet",
                        BranchOutcomeKey = "feature-accepted"
                    },
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        ArtifactTitle = "Observed visual evidence handoff",
                        BranchOutcomeKey = "visual-defect-observed"
                    },
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        ArtifactTitle = "No-UI evidence handoff",
                        BranchOutcomeKey = "no-ui-evidence-recorded"
                    }
                ],
                NoGoChildOutputs =
                [
                    new ProcessSubprocessChildOutputContract
                    {
                        StepKey = "setup-handoff",
                        ArtifactExpectationKey = "setup-handoff-packet",
                        ArtifactTitle = "Incomplete implementation attempt",
                        BranchOutcomeKey = "implementation-attempt-incomplete"
                    }
                ]
            });
        var childAssignment = CreateChildAssignment(childRunId, assignment) with
        {
            ProducedArtifactSlotIds = [ChildArtifactSlotId]
        };
        var artifactRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md";
        var nestedEnvelope = ParentSubprocessVerifiedChildOutputEnvelope.Format(
            new ProcessSubprocessVerifiedChildArtifact(
                "artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/nested.md",
                "nested",
                "nested-packet",
                "sha256:nested",
                "Branch outcome key: nested-branch"));
        var artifactContent = $"""
            Status: Completed
            Branch outcome key: {artifactBranchOutcomeKey}

            {ProcessManagedArtifactService.ManagedOutcomeArtifactCapturedHeading}

            {ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}

            {nestedEnvelope}
            """;
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Completed,
                    childAssignment,
                    ProcessRuntimeStepStatus.Completed,
                    [CreateProducedArtifactReceipt(childAssignment, ChildArtifactSlotId, artifactContent)])),
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [artifactRef] = artifactContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(expectedKind, result.Kind.ToString());
    }

    [Fact]
    public async Task Parent_subprocess_bridge_returns_child_stopped_blocked_with_latest_child_diagnostics()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(childRunId, assignment);
        var bridge = new ParentSubprocessArtifactBridge(
            new InMemoryAssignmentStore(childAssignment),
            new InMemoryStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Blocked,
                    childAssignment,
                    ProcessRuntimeStepStatus.Blocked,
                    [CreateBlockedDiagnosticReceipt(childAssignment)])),
            new FakeWorkspaceFileService([]),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildStoppedBlocked, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        var stoppedChild = Assert.IsType<ParentSubprocessStoppedChild>(result.StoppedChild);
        Assert.Equal(ProcessRuntimeStatus.Blocked, stoppedChild.ChildStatus);
        Assert.Equal(childAssignment.StepKey, stoppedChild.ChildStepKey);
        Assert.Equal(childAssignment.StepInstanceId, stoppedChild.ChildStepInstanceId);
        var diagnostic = Assert.Single(stoppedChild.Diagnostics);
        Assert.Equal("process.adapter.product_required_file_content_missing", diagnostic.Code);
        Assert.Contains("workspace_pwsh_run_script", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(stoppedChild.RecoveryDecision);
    }

    private static ForwardedContextGroundingFixture CreateForwardedContextGroundingFixture(
        string? childOutputContent = null,
        string? forwardedPayload = null)
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateParentAssignment(parentRunId);
        var childArtifactRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/architecture.md";
        var childInternalRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/intake.md";
        var forwardedContent = forwardedPayload ?? string.Join(
            Environment.NewLine,
            "## Runtime project",
            string.Empty,
            string.Empty,
            $"Internal child ref: `{childInternalRef}`");
        var forwardedArtifact = new ParentSubprocessForwardedContextArtifact(
            "runtime-project",
            "architecture",
            "runtime-project",
            "runtime-project/v1",
            childArtifactRef,
            forwardedContent);
        var resolvedChildOutputContent = childOutputContent ?? DefaultAcceptedChildArtifactContent;
        var verifiedChildOutput = new ProcessSubprocessVerifiedChildArtifact(
            $"artifacts/process-runs/{childRunId.Value:D}/steps/setup-handoff.md",
            "setup-handoff",
            "setup-handoff-packet",
            ComputeContentHash(resolvedChildOutputContent),
            resolvedChildOutputContent);
        var rawChildOutputEnvelope = ParentSubprocessVerifiedChildOutputEnvelope.Format(
            verifiedChildOutput);
        var verifiedChildOutputEnvelope = rawChildOutputEnvelope;
        var rawEnvelope = ParentSubprocessForwardedContextEnvelope.Format([forwardedArtifact]);
        var verifiedEnvelope = rawEnvelope;
        var rawOutput = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The runtime bridged a verified child subprocess output.",
            EvidenceRefs = [],
            NextActions = [],
            HumanReadableSummaryMarkdown =
                $"{rawChildOutputEnvelope}{Environment.NewLine}{Environment.NewLine}{rawEnvelope}"
        };
        var normalizedOutput = ProcessOutcomeCitationSanitizer.RemoveNonCitableSourceMetadataFromOutcome(rawOutput);
        var syntheticExecutionRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var trustedReceipt = new ToolExecutionReceiptRecord(
            Guid.NewGuid(),
            syntheticExecutionRunId,
            "process-runtime",
            ProcessSubprocessState.SubprocessLaunchToolName,
            "ProcessRuntime",
            "NotRequired",
            "Runtime-owned subprocess bridge.",
            $"parentRunId={parentRunId.Value:D}; childRunId={childRunId.Value:D}",
            ".",
            $"Succeeded: matching child run {childRunId.Value:D} completed with accepted typed evidence.",
            now,
            now);
        var bridgedOutcome = new ParentSubprocessBridgedOutcome(
            childRunId,
            now,
            ChildOutputDisposition.Accepted,
            "setup-handoff",
            "setup-handoff-packet",
            string.Empty,
            verifiedChildOutput,
            rawOutput,
            [],
            [forwardedArtifact],
            "sha256:verified-forwarded-context",
            syntheticExecutionRunId,
            [trustedReceipt]);
        return new ForwardedContextGroundingFixture(
            assignment,
            normalizedOutput,
            bridgedOutcome,
            trustedReceipt,
            rawEnvelope,
            verifiedEnvelope,
            verifiedChildOutputEnvelope);
    }

    private static ProcessStepOutcomeResult CopyWithSummary(
        ProcessStepOutcomeResult output,
        string summary)
        => new()
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            AcceptanceCriteriaEvidence = output.AcceptanceCriteriaEvidence,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = summary
        };

    private static ProcessStepOutcomeResult CopyWithReason(
        ProcessStepOutcomeResult output,
        string reason)
        => new()
        {
            Status = output.Status,
            Reason = reason,
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            AcceptanceCriteriaEvidence = output.AcceptanceCriteriaEvidence,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };

    private static ProcessSubprocessContractResolver CreateSubprocessContractResolver()
        => new();

    private static ProcessRuntimeStepAssignment CreateParentAssignment(ProcessRunId runId)
    {
        var contract = new ProcessSubprocessContract
        {
            DefinitionKey = "dotnet-solution-setup",
            ParentProducedArtifactExpectationKey = "solution-skeleton-evidence",
            AcceptedChildOutputs =
            [
                new ProcessSubprocessChildOutputContract
                {
                    StepKey = "setup-handoff",
                    ArtifactExpectationKey = "setup-handoff-packet",
                    ArtifactTitle = "Setup handoff packet"
                }
            ],
            NoGoChildOutputs =
            [
                new ProcessSubprocessChildOutputContract
                {
                    StepKey = "setup-repair-escalation",
                    ArtifactExpectationKey = "setup-repair-escalation-packet",
                    ArtifactTitle = "Setup repair escalation packet"
                }
            ]
        };
        var launchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepKind] = ProcessTemplateStepKinds.Subprocess,
            [ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey] = "dotnet-solution-setup",
            [ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepSubprocessContract(contract)
        };

        return new ProcessRuntimeStepAssignment(
            runId,
            PlanId,
            ParentStepId,
            "prepare-solution-skeleton",
            "dotnet-architect",
            string.Empty,
            ".NET Architect",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Architect",
            "Prompt",
            "sha256:ready",
            "test",
            [ParentArtifactSlotId],
            [],
            [ProcessOperationContractNames.ExecuteExternalAction],
            ProcessOperationContractNames.ExternalActionControlled,
            launchVariables,
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStepAssignment CreateChildAssignment(
        ProcessRunId childRunId,
        ProcessRuntimeStepAssignment parentAssignment)
    {
        var launchVariables = new Dictionary<string, string>(
            ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                parentAssignment.RunId,
                parentAssignment.StepInstanceId),
            StringComparer.Ordinal);

        return parentAssignment with
        {
            RunId = childRunId,
            StepInstanceId = ProcessStepInstanceId.New(),
            StepKey = "setup-handoff",
            ProducedArtifactSlotIds = [],
            LaunchVariables = launchVariables,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1)
        };
    }

    private static ProcessRuntimeStepAssignment WithSubprocessContract(
        ProcessRuntimeStepAssignment assignment,
        ProcessSubprocessContract contract)
    {
        var launchVariables = new Dictionary<string, string>(assignment.LaunchVariables, StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepSubprocessContract(contract)
        };
        return assignment with { LaunchVariables = launchVariables };
    }

    private static ProcessRuntimeStateSnapshot NewRuntimeState(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessRuntimeStatus status,
        ProcessRuntimeStepAssignment? stepAssignment = null,
        ProcessRuntimeStepStatus stepStatus = ProcessRuntimeStepStatus.Completed,
        IReadOnlyList<StrategyResultReceipt>? appliedResults = null)
    {
        var producedArtifactSlots = stepAssignment?.ProducedArtifactSlotIds.ToHashSet() ?? new HashSet<ArtifactSlotId>();
        var outputStepKey = stepAssignment?.StepKey ?? "parent";
        var outputExpectationKey = outputStepKey switch
        {
            "setup-handoff" => "setup-handoff-packet",
            "setup-repair-escalation" => "setup-repair-escalation-packet",
            _ => "artifact"
        };
        IReadOnlyList<ProcessArtifactSlotDescriptor> descriptors = stepAssignment is null
            ? []
            : producedArtifactSlots
                .Select(slotId => new ProcessArtifactSlotDescriptor(
                    slotId,
                    $"{outputStepKey}:{outputExpectationKey}",
                    outputStepKey,
                    outputExpectationKey,
                    "Child output",
                    "ManagedMarkdown",
                    $"artifacts/process-runs/{runId.Value:D}/steps/{outputStepKey}.md",
                    ProcessArtifactMaterializationMode.AgentWritten))
                .ToArray();
        var completedResultKey = stepStatus == ProcessRuntimeStepStatus.Completed
            ? appliedResults?
                .Where(receipt =>
                    receipt.StepInstanceId == (stepAssignment?.StepInstanceId ?? ParentStepId) &&
                    receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Completed)
                .LastOrDefault()
                ?.IdempotencyKey
            : null;
        var step = new ProcessRuntimeStepState(
            stepAssignment?.StepInstanceId ?? ParentStepId,
            ParentStepDefinitionId,
            stepStatus,
            IsExecutable: true,
            AttemptNumber: 1,
            DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
            RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
            ActiveClaimToken: null,
            CompletedResultKey: completedResultKey)
        {
            ProducedArtifactSlots = producedArtifactSlots,
            ArtifactDescriptors = descriptors
        };

        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            PlanId,
            "sha256:plan",
            status,
            [step],
            Claims: [],
            AppliedResults: appliedResults ?? [],
            AvailableArtifactSlots: appliedResults?
                .SelectMany(receipt => receipt.ProducedArtifacts)
                .Select(artifact => artifact.SlotId)
                .ToHashSet() ?? new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);
    }

    private static StrategyResultReceipt CreateProducedArtifactReceipt(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId,
        string content = DefaultAcceptedChildArtifactContent)
        => new(
            assignment.StepInstanceId,
            new StrategyId("strategy.adapter.workflow.execute"),
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.Succeeded,
            ProcessRuntimeStepStatus.Completed,
            "sha256:accepted-child-output",
            diagnostics: [],
            producedArtifacts:
            [
                new StrategyResultArtifactReceipt(
                    slotId,
                    ArtifactInstanceId.New(),
                    ComputeContentHash(content))
            ]);

    private static StrategyResultReceipt CreateBlockedDiagnosticReceipt(ProcessRuntimeStepAssignment assignment)
        => new(
            assignment.StepInstanceId,
            new StrategyId("strategy.adapter.workflow.execute"),
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked,
            "sha256:blocked-child",
            diagnostics:
            [
                new StrategyResultDiagnosticReceipt(
                    "process.adapter.product_required_file_content_missing",
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:child-diagnostic",
                    "Calculator.slnx does not contain src/Calculator/Calculator.csproj and the required workspace_pwsh_run_script receipt is missing.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            producedArtifacts: [],
            recoveryDecision: new ProcessRecoveryDecisionReceipt(
                ProcessFailureCategory.ProductCompletionGate,
                ProcessRecoveryDecisionKind.ManagerRequired,
                "process.adapter.product_required_file_content_missing",
                "process.current-step-safe-retry-budget-exhausted",
                "Child retry budget exhausted.")
            {
                RouteKind = ProcessRecoveryRouteKind.ManagerAction,
                ResponsibleStepInstanceId = assignment.StepInstanceId,
                DiagnosticFingerprint = "sha256:child-diagnostic",
                AutomaticRetryAttempt = 3,
                MaximumAutomaticRetryAttempts = 3,
                SameDiagnosticFingerprintAttempt = 1,
                MaximumSameDiagnosticFingerprintAttempts = 1
            });

    private static ProcessRuntimeStateSnapshot WithForwardedChildInput(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepAssignment childAssignment,
        ProcessRunId childRunId,
        ArtifactSlotId forwardedSlotId,
        ArtifactInstanceId forwardedArtifactId,
        string content,
        string contentHash)
    {
        var childOutputStep = Assert.Single(state.Steps);
        var forwardedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/architecture.md";
        var updatedOutputStep = childOutputStep with
        {
            RequiredArtifactSlots = new HashSet<ArtifactSlotId> { forwardedSlotId },
            ArtifactDescriptors =
            [
                .. childOutputStep.ArtifactDescriptors,
                new ProcessArtifactSlotDescriptor(
                    forwardedSlotId,
                    "architecture:opaque-decision",
                    "architecture",
                    "opaque-decision",
                    "Opaque decision",
                    "ManagedMarkdown",
                    forwardedRef,
                    ProcessArtifactMaterializationMode.AgentWritten)
            ]
        };
        return state with
        {
            Steps = [updatedOutputStep],
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    childAssignment.StepInstanceId,
                    forwardedSlotId,
                    ProcessArtifactInputAvailability.Available,
                    ProcessStepInstanceId.New(),
                    forwardedArtifactId,
                    contentHash,
                    $"sha256:connection:{content.Length}")
            ]
        };
    }

    private static string ComputeContentHash(string value)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static RequiredArtifactGroundingFixture CreateRequiredArtifactGroundingFixture(
        string groundedPath,
        string? candidatePath = null,
        ProcessArtifactInputAvailability availability = ProcessArtifactInputAvailability.Available,
        string? contentHashOverride = null,
        bool promptNamesUpstreamArtifact = false)
    {
        var runId = ProcessRunId.New();
        var requiredSlotId = ArtifactSlotId.New();
        var producedSlotId = ArtifactSlotId.New();
        var upstreamRef =
            $"artifacts/process-runs/{ProcessRunId.New().Value:D}/steps/upstream-evidence.md";
        var upstreamContent = $"# Upstream evidence{Environment.NewLine}{Environment.NewLine}- `{groundedPath}`";
        var assignment = CreateParentAssignment(runId) with
        {
            StepKey = "evidence-review",
            Prompt = promptNamesUpstreamArtifact
                ? $"Read required evidence at {upstreamRef}."
                : "Review the authenticated required evidence.",
            ProducedArtifactSlotIds = [producedSlotId],
            RequiredArtifactSlotIds = [requiredSlotId],
            AllowedOperations = [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly
        };
        var primaryRef =
            $"artifacts/process-runs/{runId.Value:D}/steps/{assignment.StepKey}.md";
        var citedPath = candidatePath ?? groundedPath;
        var primaryContent =
            $"# Evidence review{Environment.NewLine}{Environment.NewLine}Status: Completed{Environment.NewLine}{Environment.NewLine}- `{citedPath}`";
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Reviewed evidence at {citedPath}.",
            EvidenceRefs = [primaryRef, citedPath],
            NextActions = [],
            HumanReadableSummaryMarkdown = "Authenticated evidence reviewed."
        };
        var requiredArtifact = new RequiredArtifactInputRef(
            requiredSlotId,
            availability,
            ProducerStepId: availability == ProcessArtifactInputAvailability.Available
                ? ProcessStepInstanceId.New()
                : null,
            ArtifactId: availability == ProcessArtifactInputAvailability.Available
                ? ArtifactInstanceId.New()
                : null,
            ContentHash: contentHashOverride ?? ComputeContentHash(upstreamContent),
            ConnectionHash: "sha256:required-artifact-connection");
        var stepContract = new ProcessStepExecutionContract(
            [requiredArtifact],
            [new ExpectedProducedArtifactRef(producedSlotId)],
            [],
            "sha256:required-artifact-grounding-contract")
        {
            ArtifactDescriptors =
            [
                new ProcessArtifactSlotDescriptor(
                    requiredSlotId,
                    "upstream-evidence",
                    "upstream",
                    "upstream-evidence",
                    "Upstream evidence",
                    "ManagedMarkdown",
                    upstreamRef,
                    ProcessArtifactMaterializationMode.AgentWritten)
            ]
        };
        var workspaceFiles = new FakeWorkspaceFileService(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [upstreamRef] = upstreamContent,
                [primaryRef] = primaryContent
            });

        return new RequiredArtifactGroundingFixture(
            assignment,
            output,
            stepContract,
            workspaceFiles);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Templates", "Processes")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }

    private sealed record ForwardedContextGroundingFixture(
        ProcessRuntimeStepAssignment Assignment,
        ProcessStepOutcomeResult NormalizedOutput,
        ParentSubprocessBridgedOutcome BridgedOutcome,
        ToolExecutionReceiptRecord TrustedReceipt,
        string RawEnvelope,
        string VerifiedEnvelope,
        string VerifiedChildOutputEnvelope);

    private sealed record RequiredArtifactGroundingFixture(
        ProcessRuntimeStepAssignment Assignment,
        ProcessStepOutcomeResult Output,
        ProcessStepExecutionContract StepContract,
        IWorkspaceFileService WorkspaceFiles);

    private sealed class InMemoryStateStore(params ProcessRuntimeStateSnapshot[] states) : IProcessRuntimeStateStore
    {
        private readonly IReadOnlyDictionary<ProcessRunId, ProcessRuntimeStateSnapshot> states = states.ToDictionary(state => state.RunId);

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(states.GetValueOrDefault(runId));
    }

    private sealed class InMemoryAssignmentStore(params ProcessRuntimeStepAssignment[] assignments) : IProcessRuntimeStepAssignmentStore
    {
        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(
                assignments.Where(assignment => assignment.RunId == runId).ToArray());

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
        {
            var result = assignments
                .Where(assignment => requiredVariables.All(required =>
                    assignment.LaunchVariables.TryGetValue(required.Key, out var value) &&
                    string.Equals(value, required.Value, StringComparison.Ordinal)))
                .ToArray();
            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(result);
        }

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(assignments.FirstOrDefault(assignment =>
                assignment.RunId == runId &&
                assignment.StepInstanceId == stepInstanceId));
    }

    private sealed class FakeWorkspaceFileService : IWorkspaceFileService
    {
        private readonly IReadOnlyDictionary<string, string> files;

        public FakeWorkspaceFileService(IReadOnlyList<string> existingPaths)
            : this(existingPaths.ToDictionary(
                path => path,
                _ => DefaultAcceptedChildArtifactContent,
                StringComparer.OrdinalIgnoreCase))
        {
        }

        public FakeWorkspaceFileService(IReadOnlyDictionary<string, string> files)
        {
            this.files = files;
        }

        public WorkspaceFileListResult ListDirectory(string? relativePath = null, int maxResults = 100) => throw new NotSupportedException();

        public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100) => throw new NotSupportedException();

        public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20) => throw new NotSupportedException();

        public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
        {
            if (!files.TryGetValue(path, out var content))
            {
                return new WorkspaceTextFileReadResult(
                    Succeeded: false,
                    Message: "missing",
                    Receipt: Receipt(),
                    Path: path,
                    Content: string.Empty,
                    TotalCharacters: 0,
                    IsTruncated: false);
            }

            var isTruncated = content.Length > maxCharacters;
            return new WorkspaceTextFileReadResult(
                Succeeded: true,
                Message: "read",
                Receipt: Receipt(),
                Path: path,
                Content: isTruncated ? content[..maxCharacters] : content,
                TotalCharacters: content.Length,
                IsTruncated: isTruncated);
        }

        public WorkspacePathStatResult StatPath(string path)
            => new(
                Succeeded: true,
                Message: files.ContainsKey(path) ? "exists" : "missing",
                Receipt: Receipt(),
                Path: path,
                Exists: files.ContainsKey(path),
                PathKind: files.ContainsKey(path) ? "file" : "missing",
                SizeBytes: files.ContainsKey(path) ? 1 : null,
                LastWriteTimeUtc: files.ContainsKey(path) ? DateTimeOffset.UtcNow : null,
                ChildCount: null);

        public WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760) => throw new NotSupportedException();

        public WorkspaceFileMutationResult CreateDirectory(string path) => throw new NotSupportedException();

        public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true) => throw new NotSupportedException();

        public WorkspaceFileMutationResult AppendTextFile(string path, string content) => throw new NotSupportedException();

        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false) => throw new NotSupportedException();

        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false) => throw new NotSupportedException();

        public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false) => throw new NotSupportedException();

        public WorkspaceArchiveMutationResult ZipPath(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760) => throw new NotSupportedException();

        public WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760) => throw new NotSupportedException();

        public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160) => throw new NotSupportedException();

        private static WorkspaceToolReceipt Receipt()
            => new(
                Operation: "stat",
                MutatesWorkspace: false,
                Boundary: "test",
                Outcome: "Succeeded",
                Message: "test",
                ReceiptRelativePath: string.Empty,
                TargetPaths: [],
                ArtifactReferences: [],
                StartedAtUtc: DateTimeOffset.UtcNow,
                CompletedAtUtc: DateTimeOffset.UtcNow);
    }
}
