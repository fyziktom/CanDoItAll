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

    [Fact]
    public void Template_pack_loads_with_typed_subprocess_contracts()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var pack = loader.Load();

        Assert.Contains(pack.Definitions, definition => definition.Key == "dotnet-development-slice");
        Assert.Contains(pack.Definitions, definition => definition.Key == "software-delivery");
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
            new FakeWorkspaceFileService([acceptedRef]),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.AcceptedChildOutputBridged, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.NotNull(result.BridgedOutcome);
        Assert.Contains(acceptedRef, result.BridgedOutcome.EvidenceRefs);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.BridgedOutcome.Output.Status);
    }

    [Fact]
    public async Task Parent_subprocess_bridge_forwards_only_hash_verified_declared_child_input()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var forwardedSlotId = ArtifactSlotId.New();
        var forwardedArtifactId = ArtifactInstanceId.New();
        const string forwardedContent = """
            ## Bootstrap decision

            ```json
            { "schema": "opaque.example/v1", "value": "preserve exact content" }
            ```
            """;
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
                [acceptedRef] = $"{ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}\nStatus: Completed",
                [forwardedRef] = forwardedContent
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.AcceptedChildOutputBridged, result.Kind);
        var outcome = Assert.IsType<ParentSubprocessBridgedOutcome>(result.BridgedOutcome);
        var forwardedArtifact = Assert.Single(outcome.ForwardedContextArtifacts);
        Assert.Equal("opaque-bootstrap", forwardedArtifact.BindingKey);
        Assert.Equal(forwardedContent.Trim(), forwardedArtifact.Content);
        Assert.Contains("Runtime-forwarded child context", outcome.Output.HumanReadableSummaryMarkdown, StringComparison.Ordinal);
        Assert.Contains(forwardedContent, outcome.Output.HumanReadableSummaryMarkdown, StringComparison.Ordinal);
        Assert.Contains("````", outcome.Output.HumanReadableSummaryMarkdown, StringComparison.Ordinal);
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
                [acceptedRef] = $"{ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}\nStatus: Completed",
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
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [stagedRef] = $"""
                # setup-handoff Process Step Outcome

                {ProcessManagedArtifactService.ManagedOutcomeArtifactCapturedHeading}

                Completion gates have not accepted this output yet.
                """
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
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [noGoRef] = $"""
                {ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}

                ### Branch Outcome
                - Key: setup-no-go

                ### Summary
                The child produced a bounded no-go packet.
                """
            }),
            CreateSubprocessContractResolver());

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.NoGoChildOutputBridged, result.Kind);
        var bridgedOutcome = Assert.IsType<ParentSubprocessBridgedOutcome>(result.BridgedOutcome);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, bridgedOutcome.Output.Status);
        Assert.Equal("manager-assisted-repair-required", bridgedOutcome.Output.BranchOutcomeKey);
        Assert.Contains("no-go", bridgedOutcome.Output.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(noGoRef, bridgedOutcome.EvidenceRefs);
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
            new FakeWorkspaceFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [artifactRef] = $"""
                Status: Completed
                Branch outcome key: {artifactBranchOutcomeKey}

                {ProcessManagedArtifactService.ManagedOutcomeArtifactCapturedHeading}

                {ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}
                """
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
        var step = new ProcessRuntimeStepState(
            stepAssignment?.StepInstanceId ?? ParentStepId,
            ParentStepDefinitionId,
            stepStatus,
            IsExecutable: true,
            AttemptNumber: 1,
            DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
            RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
            ActiveClaimToken: null,
            CompletedResultKey: null)
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
        ArtifactSlotId slotId)
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
                    "sha256:child-artifact")
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
            : this(existingPaths.ToDictionary(path => path, _ => string.Empty, StringComparer.OrdinalIgnoreCase))
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

            return new WorkspaceTextFileReadResult(
                Succeeded: true,
                Message: "read",
                Receipt: Receipt(),
                Path: path,
                Content: content,
                TotalCharacters: content.Length,
                IsTruncated: false);
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
