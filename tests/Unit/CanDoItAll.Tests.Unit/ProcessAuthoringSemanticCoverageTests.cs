using System.Reflection;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessAuthoringSemanticCoverageTests {
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Partial_child_mapping_is_refused_instead_of_becoming_a_default_mapping(bool hasTitle, bool hasLegacyId) {
        ProcessTemplateDefinitionDocument child = new() { Key = "child", Steps = [new() { Key = "outcome",
            ArtifactExpectations = [new() { Key = "result", Title = "Child result", IsRequired = true }] }] };
        ProcessTemplateDefinitionStepDocument parent = new() { Key = "dispatch", ArtifactExpectations = [new() {
            Key = "handoff", SubprocessChildArtifactTitle = hasTitle ? "Child result" : string.Empty,
            SubprocessChildArtifactExpectationId = hasLegacyId ? Guid.NewGuid() : null
        }] };
        Assert.Throws<InvalidOperationException>(() => ProcessAuthoringSubprocessPatch.Apply(parent, child));
        Assert.Null(parent.SubprocessContract);
    }

    [Fact]
    public void Editing_child_mapping_keeps_hidden_output_policy_and_stable_artifact_key() {
        ProcessTemplateDefinitionDocument child = new() { Key = "child", Steps = [new() { Key = "outcome",
            ArtifactExpectations = [new() { Key = "result", Title = "Renamed result", IsRequired = true }],
            BranchOutcomes = [new() { Key = "accepted" }] }] };
        ProcessTemplateDefinitionStepDocument parent = new() { Key = "dispatch", ArtifactExpectations = [new() {
            Key = "handoff", SubprocessChildStepKey = "outcome", SubprocessChildArtifactTitle = "Renamed result"
        }], SubprocessContract = new() { DefinitionKey = "child", ParentProducedArtifactExpectationKey = "handoff",
            AcceptedChildOutputs = [new() { StepKey = "outcome", ArtifactExpectationKey = "result", BranchOutcomeKey = "accepted",
                ParentBranchOutcomeKey = "continue", Description = "Retained execution policy" }] } };
        ProcessAuthoringSubprocessPatch.Apply(parent, child);
        var output = Assert.Single(parent.SubprocessContract.AcceptedChildOutputs);
        Assert.Equal("result", output.ArtifactExpectationKey);
        Assert.Equal("Renamed result", output.ArtifactTitle);
        Assert.Equal("accepted", output.BranchOutcomeKey);
        Assert.Equal("continue", output.ParentBranchOutcomeKey);
        Assert.Equal("Retained execution policy", output.Description);
    }

    [Fact]
    public void Unchanged_defaults_keep_legacy_definition_version_content_and_artifact_identity() {
        var templates = new ProcessTemplatePackLoader();
        var pack = templates.Load();
        foreach (var item in pack.Definitions) {
            var definition = templates.LoadDefinition(item.Key);
            ProcessAuthoringContent content = new(ProcessAuthoringContent.CurrentSchemaVersion, definition,
                definition.Steps.ToDictionary(step => step.Key, step => step.ResolvedExecutionGuidance), templates.LoadRoleResources(item.Key),
                new("distributed-template", pack.Manifest.Version, ProcessAuthoringCodec.Hash(item.Key)), [], []);
            var json = ProcessAuthoringCodec.Write(content);
            var profile = Guid.NewGuid();
            ProcessExecutableDefinitionClosure closure = new(profile, Guid.Empty, Guid.Empty, item.Key,
                new Dictionary<string, ProcessExecutableDefinitionSource> { [item.Key] = new(profile, Guid.Empty, Guid.Empty, item.Key, 0, null, null,
                    ProcessAuthoringCodec.Hash(json), json) });
            Assert.Null(ProcessExecutableDefinitionResolver.ExecutableIdentity(closure));
            StrategyId strategy = new("authoring-compatibility-strategy");
            var previous = ProcessTemplateKernelBuilder.Build(definition, pack.Manifest.Version, strategy);
            var current = ProcessTemplateKernelBuilder.Build(ProcessExecutableDefinitionResolver.Decode(closure, item.Key).Definition,
                pack.Manifest.Version, strategy, ProcessExecutableDefinitionResolver.ExecutableIdentity(closure));
            Assert.Equal(previous.Definition.DefinitionId, current.Definition.DefinitionId);
            Assert.Equal(previous.Definition.VersionId, current.Definition.VersionId);
            Assert.Equal(previous.DefinitionContentHash, current.DefinitionContentHash);
            Assert.Equal(previous.ArtifactSlotByStepExpectation, current.ArtifactSlotByStepExpectation);
        }
    }

    [Fact]
    public void Native_model_expansion_requires_an_explicit_preservation_review() {
        RequireReviewedFields<ProcessTemplateDefinitionDocument>("AutonomyLevel|ChangeSummary|ConstitutionRuleSummary|Criticality|CustomerName|DisplayName|GovernanceNotes|GovernancePolicySummary|InterfaceContractSummary|Key|LaunchDriverActivations|ManagerOverrideSummary|OperatingMode|OperatingModeSummary|OwnerName|RoleUsages|SimulationReadinessSummary|Steps|Summary|ValueStatement");
        RequireReviewedFields<ProcessTemplateDefinitionRoleUsageDocument>("AllowsFallback|CanvasX|CanvasY|DefaultAllocationPercent|DisplayName|IsRequired|Key|Notes|PreferredExecutorKind|PreferredProjectAssignmentRole|Purpose|RequiresExplicitApproval|RoleResourceKey|RoleTemplateSnapshotName|RoleTemplateSourceKey|SnapshotSummary|StaffingIntent|WorkflowBinding");
        RequireReviewedFields<ProcessTemplateDefinitionStepDocument>("AllowedOperations|AllowsCompletedOutcomeWithOpenIssues|AllowsManualSkip|AllowsSafeRefusal|ArtifactExpectations|ArtifactInputs|BranchCanvasX|BranchCanvasY|BranchOutcomes|CanvasX|CanvasY|CapabilityScope|CompletionPolicy|DecisionRightsSummary|DecisionRoleKey|Dependencies|DependsOnBranchOutcomeKey|DependsOnStepKey|EvidenceContractSummary|ExceptionPolicySummary|ExecutionClass|ExecutionContract|ExecutionGuidanceRefs|ExecutorPreferredSpecializationTags|InputContractSummary|Key|Notes|OperationTargetScope|Order|OutputContractSummary|RequiresApproval|RequiresDecisionRecord|ResolvedExecutionGuidance|RoleAssignments|StepKind|SubprocessContract|SubprocessDefinitionSnapshotName|SubprocessProcessKey|Subtitle|TargetLeadHours|Title");
        RequireReviewedFields<ProcessTemplateDefinitionArtifactExpectationDocument>("AllowedFutureUsageSummary|ArtifactKind|IsRequired|Key|PayloadSchema|RetentionDays|SensitivityLevel|SubprocessChildArtifactExpectationId|SubprocessChildArtifactTitle|SubprocessChildStepKey|TemplateKey|Title|TrustRequirement|ValidationRequirementSummary|WorkflowOutputId|WorkflowOutputKind|WorkflowOutputName");
        RequireReviewedFields<ProcessTemplateDefinitionArtifactInputDocument>("ArtifactExpectationKey|SourceStepKey");
        RequireReviewedFields<ProcessTemplateDefinitionStepDependencyDocument>("DependsOnBranchOutcomeKey|DependsOnStepKey");
        RequireReviewedFields<ProcessTemplateDefinitionStepBranchOutcomeDocument>("AllowsCompletedOutcomeWithOpenIssues|Description|IsBackwardRoute|Key|LoopBudgetMaximumRepeats|LoopEscalationTargetKind|LoopFingerprintPolicyKey|RouteTargetArtifactExpectationKey|RouteTargetKind|RouteTargetStepKey|Title");
        RequireReviewedFields<ProcessTemplateDefinitionStepRoleAssignmentDocument>("FallbackOrder|IsRequired|RebindPolicySummary|ResponsibilityKind|RoleKey");
        RequireReviewedFields<ProcessTemplateStepExecutionContractDocument>("DeterministicToolPlan|ExecutionClass|ProducedArtifactSlots|RequiredHostCapabilities|RequiredReceipts|RequiredRuntimeToolNames|RuntimeOwnedExecutorKey");
        RequireReviewedFields<ProcessTemplateStepCompletionPolicyDocument>("AcceptanceCriteriaRequiredBranchOutcomeKeys|CompletionIssueRoutes|ProductMutationRequiredBranchOutcomeKeys|ProductMutationToolNames|ProductSourceInspectionRequiredBranchOutcomeKeys|RequiredProductToolReceipts|RequiresProductMutationBeforeManagedOutput|RequiresProductSourceInspection|RuntimeRoutedBranchOutcomeKeys");
        RequireReviewedFields<ProcessTemplateDriverActivationDocument>("DriverKey|InputArtifactBindings|Settings");
        RequireReviewedFields<ProcessTemplateDriverArtifactBindingDocument>("ArtifactExpectationKey|BindingKey|PayloadSchema|SourceStepKey");
        RequireReviewedFields<ProcessTemplateRoleResourceDocument>("DisplayName|Key|PreferredExecutorKind|PreferredProjectAssignmentRole|Purpose|RoleTemplateSnapshotName|RoleTemplateSourceKey|SnapshotSummary|StaffingIntent|Summary");
        RequireReviewedFields<ProcessSubprocessContract>("AcceptedChildOutputs|AlreadySatisfiedOutput|DefinitionKey|ForwardedChildContextArtifacts|LaunchMode|MaterializationMode|NoGoChildOutputs|ParentProducedArtifactExpectationKey|RequiredChildReceipts");
        RequireReviewedFields<ProcessSubprocessChildOutputContract>("ArtifactExpectationKey|ArtifactTitle|BranchOutcomeKey|Description|ParentBranchOutcomeKey|StepKey");
        RequireReviewedFields<ProcessSubprocessRequiredReceiptContract>("Description|RuntimeToolProviderKey|ToolName");
        RequireReviewedFields<ProcessSubprocessForwardedChildContextArtifactContract>("ArtifactExpectationKey|BindingKey|PayloadSchema|SourceStepKey");
    }

    private static void RequireReviewedFields<T>(string reviewed)
        => Assert.Equal(reviewed.Split('|'), typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name).Order(StringComparer.Ordinal));
}
