using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
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

        Assert.Equal(9, subprocessParents.Length);
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
        Assert.Contains("each listed tool must produce a current execution-run tool receipt", prompt, StringComparison.Ordinal);
        Assert.Contains("markdown statement", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not replace those invocations with manual shell commands", prompt, StringComparison.Ordinal);
        Assert.Contains("current execution-run receipt from invoking that exact tool", prompt, StringComparison.Ordinal);
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
            new FakeWorkspaceFileService([acceptedRef]));

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.AcceptedChildOutputBridged, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.NotNull(result.AcceptedOutcome);
        Assert.Contains(acceptedRef, result.AcceptedOutcome.EvidenceRefs);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.AcceptedOutcome.Output.Status);
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
            new FakeWorkspaceFileService([acceptedRef]));

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.Null(result.AcceptedOutcome);
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

                {AgentFrameworkProcessExecutionAdapter.ManagedOutcomeArtifactCapturedHeading}

                Completion gates have not accepted this output yet.
                """
            }));

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.Null(result.AcceptedOutcome);
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
            new FakeWorkspaceFileService([noGoRef]));

        var result = await bridge.ResolveExistingAsync(assignment);

        Assert.Equal(ParentSubprocessArtifactBridgeResultKind.NoGoChildOutputFound, result.Kind);
        Assert.Equal(childRunId, result.ChildRunId);
        Assert.Contains(noGoRef, result.EvidenceRefs);
        Assert.Null(result.AcceptedOutcome);
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
            new FakeWorkspaceFileService([]));

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

    private static ProcessRuntimeStateSnapshot NewRuntimeState(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessRuntimeStatus status,
        ProcessRuntimeStepAssignment? stepAssignment = null,
        ProcessRuntimeStepStatus stepStatus = ProcessRuntimeStepStatus.Completed,
        IReadOnlyList<StrategyResultReceipt>? appliedResults = null)
        => new(
            rootRunId,
            runId,
            PlanId,
            "sha256:plan",
            status,
            [
                new ProcessRuntimeStepState(
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
                    ProducedArtifactSlots = stepAssignment?.ProducedArtifactSlotIds.ToHashSet() ?? new HashSet<ArtifactSlotId>()
                }
            ],
            Claims: [],
            AppliedResults: appliedResults ?? [],
            AvailableArtifactSlots: appliedResults?
                .SelectMany(receipt => receipt.ProducedArtifacts)
                .Select(artifact => artifact.SlotId)
                .ToHashSet() ?? new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);

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
