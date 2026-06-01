using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessTemplateGovernanceTests
{
    private static readonly string[] BlazorTemplateKeys =
    [
        "blazor-app-delivery",
        "blazor-app-repair-fix",
        "blazor-backend-feature",
        "blazor-frontend-feature",
        "blazor-fullstack-feature"
    ];

    private static readonly string[] DemoTopicTerms =
    [
        "tetris",
        "tetromino",
        "falling block",
        "gameplay",
        "simple game loop"
    ];

    private static readonly (string TemplateKey, string ResolveStepKey, string CaptureStepKey, string WritebackStepKey)[] ScreenshotTemplateSteps =
    [
        ("app-page-screenshot", "resolve-single-page-target", "capture-page-screenshot", "review-and-store-screenshot"),
        ("app-pages-screenshot-set", "resolve-page-set-targets", "capture-page-screenshot-set", "review-and-store-screenshot-set")
    ];

    private static readonly (string ScenarioKey, string TemplateKey)[] RequiredTypedBaselineScenarios =
    [
        ("baseline-blazor-wasm-pwa-app", "blazor-app-delivery"),
        ("baseline-customer-onboarding", "customer-onboarding"),
        ("baseline-business-plan-development", "business-plan-development"),
        ("baseline-incident-response", "incident-response"),
        ("baseline-release-readiness-and-deployment", "release-readiness-and-deployment"),
        ("baseline-architecture-decision-governance", "architecture-decision-governance"),
        ("baseline-agent-training-and-improvement", "ai-assisted-change-delivery")
    ];

    [Fact]
    public async Task Blazor_process_templates_SB04_INV_001_constrain_product_mutation_to_implementation_and_repair_steps()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        foreach (var templateKey in BlazorTemplateKeys)
        {
            var definition = projectionService.GetProjectedEnvelope(templateKey).Definition;
            var mutableStepKeys = definition.Steps
                .Where(AllowsProductMutation)
                .Select(step => step.Key)
                .OrderBy(stepKey => stepKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(
                ["implement-blazor-change", "repair-blazor-findings"],
                mutableStepKeys);

            AssertReadOnlyContractStep(definition, "resolve-blazor-contract");
            AssertImplementationOrRepairStep(definition, "implement-blazor-change");
            AssertImplementationOrRepairStep(definition, "repair-blazor-findings");
            AssertValidationStep(definition, "validate-blazor-runtime");
            AssertValidationStep(definition, "revalidate-blazor-repair");
            AssertWritebackStep(definition, "record-blazor-results");
            AssertWritebackStep(definition, "record-blazor-results-after-repair");
            AssertEscalationStep(definition, "escalate-blazor-unresolved-repair");
        }
    }

    [Fact]
    public async Task Default_process_warmup_includes_blazor_process_templates()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();

        var pack = packLoader.Load();
        var defaultProcessKeys = ProcessCatalogDefaultTemplates.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var templateKey in BlazorTemplateKeys)
        {
            Assert.Contains(templateKey, defaultProcessKeys);
        }

        var missingPackTemplates = defaultProcessKeys
            .Where(key => !pack.Processes.ContainsKey(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(missingPackTemplates);
    }

    [Fact]
    public async Task Dotnet_software_delivery_template_hardens_parent_permissions_and_writeback_subprocesses()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var definition = projectionService.GetProjectedEnvelope("software-delivery").Definition;
        var mutableStepKeys = definition.Steps
            .Where(AllowsProductMutation)
            .Select(step => step.Key)
            .OrderBy(stepKey => stepKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(["quality-repair"], mutableStepKeys);
        AssertReadOnlyContractStep(definition, "feature-intake");
        AssertSubprocessStep(
            definition,
            "architecture-review",
            ".NET architecture design and review subprocess");
        AssertSubprocessStep(
            definition,
            "implementation",
            ".NET implementation slice with atomic validation");
        AssertSubprocessStep(
            definition,
            "record-runtime-commands",
            ".NET runtime command project-structure writeback");
        AssertSubprocessStep(
            definition,
            "capture-ui-screenshots",
            ".NET UI screenshot project-structure writeback");
        AssertWritebackStep(definition, "post-release-learning");
        AssertWritebackStep(definition, "post-release-learning-after-repair");

        var intakeStep = GetStep(definition, "feature-intake");
        Assert.Contains("backend-only/API/service", intakeStep.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blazor Server/SSR", intakeStep.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blazor WebAssembly", intakeStep.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blazor WASM PWA", intakeStep.Notes, StringComparison.OrdinalIgnoreCase);
        AssertPostReleaseLearningWritebackContract(GetStep(definition, "post-release-learning"));
        AssertPostReleaseLearningWritebackContract(GetStep(definition, "post-release-learning-after-repair"));

        var architectureDefinition = projectionService.GetProjectedEnvelope("dotnet-architecture-design-review").Definition;
        Assert.All(architectureDefinition.Steps, step => Assert.False(AllowsProductMutation(step)));
        var reviewStep = GetStep(architectureDefinition, "review-architecture-design");
        var reviewContractText = string.Join(
            " ",
            reviewStep.Notes,
            reviewStep.OutputContractSummary,
            reviewStep.EvidenceContractSummary,
            reviewStep.ArtifactExpectations.Single().ValidationRequirementSummary);

        Assert.Contains("logic properly split", reviewContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("models", reviewContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user stories", reviewContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("service functions", reviewContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("testable", reviewContractText, StringComparison.OrdinalIgnoreCase);

        var runtimeDefinition = projectionService.GetProjectedEnvelope("dotnet-runtime-command-writeback").Definition;
        Assert.All(runtimeDefinition.Steps, step => Assert.False(AllowsProductMutation(step)));
        AssertWritebackStep(runtimeDefinition, "write-run-command-nodes");
        var runtimeContractText = string.Join(
            " ",
            GetStep(runtimeDefinition, "resolve-dotnet-run-commands").Notes,
            GetStep(runtimeDefinition, "write-run-command-nodes").Notes,
            GetStep(runtimeDefinition, "runtime-command-handoff").EvidenceContractSummary);

        Assert.Contains("Run command", runtimeContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Run app", runtimeContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Run tests", runtimeContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("process run node", runtimeContractText, StringComparison.OrdinalIgnoreCase);

        var screenshotDefinition = projectionService.GetProjectedEnvelope("dotnet-ui-screenshot-writeback").Definition;
        Assert.All(screenshotDefinition.Steps, step => Assert.False(AllowsProductMutation(step)));
        AssertWritebackStep(screenshotDefinition, "store-ui-screenshots");
        var screenshotContractText = string.Join(
            " ",
            GetStep(screenshotDefinition, "resolve-ui-screenshot-applicability").Notes,
            GetStep(screenshotDefinition, "store-ui-screenshots").Notes,
            GetStep(screenshotDefinition, "screenshot-handoff").EvidenceContractSummary);

        Assert.Contains("Screenshots", screenshotContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("process run node", screenshotContractText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-UI", screenshotContractText, StringComparison.OrdinalIgnoreCase);

        var sliceDefinition = projectionService.GetProjectedEnvelope("dotnet-development-slice").Definition;
        var sliceValidationStep = GetStep(sliceDefinition, "add-tests-and-proof");
        Assert.Equal(ProcessStepTargetScope.ExternalProductTargetReadOnly, sliceValidationStep.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.RunValidation, sliceValidationStep.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, sliceValidationStep.AllowedOperations);
        Assert.Contains("route back to implementation", sliceValidationStep.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Blazor_wasm_pwa_baseline_SB05_INV_001_keeps_app_topic_generic_in_scenario_data()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var pack = packLoader.Load();
        var scenario = Assert.Single(
            pack.BaselineScenarios,
            item => string.Equals(item.Key, "baseline-blazor-wasm-pwa-app", StringComparison.Ordinal));
        var projectedDefinition = projectionService.GetProjectedEnvelope(scenario.ProcessTemplateKey).Definition;

        Assert.Equal("blazor-app-delivery", scenario.ProcessTemplateKey);
        Assert.Contains("requested app topic", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("responsive route-level UI", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("core interactive workflow", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PWA manifest/service-worker offline readiness", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("build/test proof", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browser screenshot", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("console proof", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project-structure writeback", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        AssertNoDemoTopicTerms(scenario.RunName);
        AssertNoDemoTopicTerms(scenario.Summary);
        AssertNoDemoTopicTerms(scenario.TriggerReason);

        Assert.Contains(scenario.Transitions, transition =>
            string.Equals(transition.StepKey, "resolve-blazor-contract", StringComparison.Ordinal) &&
            transition.Reason.Contains("without implementation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scenario.Artifacts, artifact =>
            string.Equals(artifact.StepKey, "validate-blazor-runtime", StringComparison.Ordinal) &&
            artifact.ReviewSummary.Contains("interactive workflow assertions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scenario.Artifacts, artifact =>
            string.Equals(artifact.StepKey, "record-blazor-results", StringComparison.Ordinal) &&
            artifact.ReviewSummary.Contains("project-structure writeback", StringComparison.OrdinalIgnoreCase));

        Assert.False(AllowsProductMutation(GetStep(projectedDefinition, "resolve-blazor-contract")));
        Assert.True(AllowsProductMutation(GetStep(projectedDefinition, "implement-blazor-change")));
        Assert.False(AllowsProductMutation(GetStep(projectedDefinition, "validate-blazor-runtime")));
    }

    [Fact]
    public async Task Blazor_wasm_pwa_live_run_profile_SB02_INV_001_starts_fresh_and_takes_topic_from_run_request()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();

        var pack = packLoader.Load();
        var profile = Assert.Single(
            pack.LiveRunProfiles,
            item => string.Equals(item.Key, "generic-blazor-wasm-pwa-app", StringComparison.Ordinal));

        Assert.Equal("blazor-app-delivery", profile.ProcessTemplateKey);
        Assert.Equal("GovernedLive", profile.OperatingMode);
        Assert.Contains("{AppTopic}", profile.RunNameTemplate, StringComparison.Ordinal);
        Assert.Contains("{AppTopic}", profile.TriggerReasonTemplate, StringComparison.Ordinal);
        Assert.NotEmpty(profile.Assignments);
        Assert.NotEmpty(profile.AcceptanceCriteria);
        Assert.NotEmpty(profile.RequiredProofKinds);
        Assert.True(profile.FreshRunPolicy.RequiresFreshRun);
        Assert.False(profile.FreshRunPolicy.AllowsSeededTransitions);
        Assert.False(profile.FreshRunPolicy.AllowsSeededArtifacts);
        Assert.NotEmpty(profile.FreshRunPolicy.RequiredPreDispatchChecks);
        Assert.NotEmpty(profile.FreshRunPolicy.RequiredEvidenceChecks);
        Assert.Contains(
            profile.FreshRunPolicy.RequiredPreDispatchChecks,
            check => check.Contains("no baseline scenario transitions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            profile.FreshRunPolicy.RequiredPreDispatchChecks,
            check => check.Contains("no baseline scenario artifacts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            profile.FreshRunPolicy.RequiredEvidenceChecks,
            check => check.Contains("current-run evidence", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            "current-run managed output",
            profile.FreshRunPolicy.ProjectStructureWritebackGuidance,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(typeof(ProcessTemplateLiveRunProfile).GetProperty("Transitions"));
        Assert.Null(typeof(ProcessTemplateLiveRunProfile).GetProperty("Artifacts"));
        AssertNoDemoTopicTerms(profile.RunNameTemplate);
        AssertNoDemoTopicTerms(profile.Summary);
        AssertNoDemoTopicTerms(profile.TriggerReasonTemplate);
        AssertNoDemoTopicTerms(profile.FreshRunPolicy.ProjectStructureWritebackGuidance);
    }

    [Fact]
    public async Task Project_structure_templates_SB07_INV_001_require_execute_external_action_for_project_structure_writeback()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        foreach (var (templateKey, resolveStepKey, captureStepKey, writebackStepKey) in ScreenshotTemplateSteps)
        {
            var definition = projectionService.GetProjectedEnvelope(templateKey).Definition;

            AssertReadOnlyContractStep(definition, resolveStepKey);
            AssertRuntimeProofStep(definition, captureStepKey);
            AssertWritebackStep(definition, writebackStepKey);
        }

        var layoutDefinition = projectionService.GetProjectedEnvelope("app-layout-image-generation").Definition;

        AssertReadOnlyContractStep(layoutDefinition, "resolve-layout-sources");
        AssertWritebackStep(layoutDefinition, "generate-and-store-layout-recommendation");
        AssertReadOnlyContractStep(layoutDefinition, "layout-generation-handoff");
    }

    [Fact]
    public async Task Baseline_scenarios_SB14_INV_001_cover_typed_contracts_branching_and_recovery_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var pack = packLoader.Load();
        foreach (var (scenarioKey, templateKey) in RequiredTypedBaselineScenarios)
        {
            var scenario = Assert.Single(
                pack.BaselineScenarios,
                item => string.Equals(item.Key, scenarioKey, StringComparison.Ordinal));
            var projectedDefinition = projectionService.GetProjectedEnvelope(scenario.ProcessTemplateKey).Definition;

            Assert.Equal(templateKey, scenario.ProcessTemplateKey);
            Assert.NotEmpty(scenario.Artifacts);

            var selectedBranchTransitions = scenario.Transitions
                .Where(transition => !string.IsNullOrWhiteSpace(transition.SelectedBranchOutcomeKey))
                .ToList();
            Assert.NotEmpty(selectedBranchTransitions);
            foreach (var transition in selectedBranchTransitions)
            {
                var step = GetStep(projectedDefinition, transition.StepKey);
                Assert.Contains(
                    step.BranchOutcomes,
                    outcome => string.Equals(outcome.Key, transition.SelectedBranchOutcomeKey, StringComparison.Ordinal));
            }

            Assert.NotEmpty(scenario.ContractExercises);
            foreach (var exercise in scenario.ContractExercises)
            {
                var step = GetStep(projectedDefinition, exercise.StepKey);
                Assert.Equal(exercise.ExpectedTargetScope, step.OperationTargetScope);
                Assert.Equal(
                    exercise.ExpectedAllowedOperations.OrderBy(item => item).ToArray(),
                    step.AllowedOperations.OrderBy(item => item).ToArray());
            }

            Assert.NotEmpty(scenario.RecoveryExercises);
            foreach (var exercise in scenario.RecoveryExercises)
            {
                var classification = ProcessBlockStateClassifier.Classify(exercise.Diagnostic, exercise.BlockCause);
                Assert.Equal(exercise.BlockCause, classification.BlockCause);
                foreach (var expectedOption in exercise.ExpectedRecoveryOptions)
                {
                    Assert.Contains(expectedOption, classification.RecoveryOptions);
                }
            }
        }
    }

    [Fact]
    public async Task Manifest_process_templates_SB08_INV_001_all_steps_declare_typed_operation_contracts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();

        var pack = packLoader.Load();
        var missingContracts = pack.Processes.Values
            .SelectMany(process => process.Steps.Select(step => new
            {
                TemplateKey = process.Key,
                StepKey = step.Key,
                HasAllowedOperations = step.AllowedOperations.Count > 0,
                HasTargetScope = step.OperationTargetScope.HasValue
            }))
            .Where(item => !item.HasAllowedOperations || !item.HasTargetScope)
            .Select(item => $"{item.TemplateKey}/{item.StepKey}")
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missingContracts);

        var invalidContracts = new List<string>();
        foreach (var process in pack.Processes.Values)
        {
            foreach (var step in process.Steps)
            {
                if (!Enum.TryParse<ProcessStepKind>(step.StepKind, ignoreCase: true, out var stepKind))
                {
                    invalidContracts.Add($"{process.Key}/{step.Key}: unknown step kind {step.StepKind}");
                    continue;
                }

                var normalization = ProcessStepOperationContractState.NormalizeDeclaredContract(
                    stepKind,
                    step.AllowedOperations,
                    step.OperationTargetScope);
                invalidContracts.AddRange(normalization.Issues.Select(issue => $"{process.Key}/{step.Key}: {issue.Code}"));
            }
        }

        Assert.Empty(invalidContracts.OrderBy(item => item, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Process_template_artifact_projection_SB09_INV_001_preserves_explicit_output_mappings()
    {
        var childExpectationId = Guid.NewGuid();

        var artifact = ProcessTemplateEditorModelFactory.CreateArtifactExpectationFromTemplate(
            new ProcessTemplateArtifactExpectation
            {
                Key = "qa-report",
                Title = "QA report",
                ArtifactKind = "Deliverable",
                IsRequired = true,
                ValidationRequirementSummary = "Must include validation result.",
                WorkflowOutputId = "qa-report-json",
                WorkflowOutputName = "QA report",
                WorkflowOutputKind = "Json",
                SubprocessChildArtifactExpectationId = childExpectationId,
                SubprocessChildStepKey = "child-qa",
                SubprocessChildArtifactTitle = "Child QA report"
            },
            resource: null,
            id: Guid.NewGuid());

        Assert.Equal("qa-report-json", artifact.WorkflowOutputId);
        Assert.Equal("QA report", artifact.WorkflowOutputName);
        Assert.Equal(WorkflowArtifactKind.Json, artifact.WorkflowOutputKind);
        Assert.Equal(childExpectationId, artifact.SubprocessChildArtifactExpectationId);
        Assert.Equal("child-qa", artifact.SubprocessChildStepKey);
        Assert.Equal("Child QA report", artifact.SubprocessChildArtifactTitle);
    }

    [Fact]
    public async Task Process_template_vocabulary_SB01_INV_001_maps_to_supported_ui_and_domain_options()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();

        var pack = packLoader.Load();
        var executorOptionValues = ProcessRoleExecutorKindOptions.Options
            .Select(item => item.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unsupportedExecutorKinds = EnumerateRoleExecutorKinds(pack)
            .Where(value => !executorOptionValues.Contains(ProcessRoleExecutorKindOptions.NormalizeForSelection(value)))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unsupportedResponsibilityKinds = EnumerateResponsibilityKinds(pack)
            .Where(value => EnumValueParser.ParseNullable<ProcessResponsibilityKind>(value) is null)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unsupportedArtifactKinds = EnumerateArtifactKinds(pack)
            .Where(value => EnumValueParser.ParseNullable<ProcessArtifactKind>(value) is null)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unsupportedTrustRequirements = EnumerateTrustRequirements(pack)
            .Where(value => EnumValueParser.ParseNullable<ProcessArtifactTrustRequirement>(value) is null)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(unsupportedExecutorKinds);
        Assert.Empty(unsupportedResponsibilityKinds);
        Assert.Empty(unsupportedArtifactKinds);
        Assert.Empty(unsupportedTrustRequirements);
        Assert.Equal(ProcessResponsibilityKind.Accountable, EnumValueParser.ParseNullable<ProcessResponsibilityKind>("Accountable"));
        Assert.Equal(ProcessArtifactKind.DecisionRecord, EnumValueParser.ParseNullable<ProcessArtifactKind>("DecisionRecord"));
        Assert.Equal(ProcessArtifactTrustRequirement.ApprovalRequired, EnumValueParser.ParseNullable<ProcessArtifactTrustRequirement>("ApprovalRequired"));
    }

    [Fact]
    public async Task Dotnet_feature_template_SB01_INV_002_preserves_accountable_decision_record_and_approval_required()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var definition = projectionService.GetProjectedEnvelope("dotnet-feature-function-implementation").Definition;
        var validationStep = GetStep(definition, "targeted-validation");

        Assert.Contains(
            validationStep.RoleAssignments,
            assignment => assignment.ResponsibilityKind == ProcessResponsibilityKind.Accountable);
        Assert.Contains(
            definition.Steps.SelectMany(step => step.ArtifactExpectations),
            artifact => artifact.ArtifactKind == ProcessArtifactKind.DecisionRecord);
        Assert.Contains(
            definition.Steps.SelectMany(step => step.ArtifactExpectations),
            artifact => artifact.TrustRequirement == ProcessArtifactTrustRequirement.ApprovalRequired);
    }

    private static bool AllowsProductMutation(ProcessStepImportExportModel step)
    {
        return step.AllowedOperations.Contains(ProcessStepOperation.MutateProductTarget) ||
            step.OperationTargetScope is ProcessStepTargetScope.ManagedOutputProduct or ProcessStepTargetScope.ExternalProductTargetMutable;
    }

    private static ProcessStepImportExportModel GetStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        return Assert.Single(
            definition.Steps,
            step => string.Equals(step.Key, stepKey, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertReadOnlyContractStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepTargetScope.ExternalProductTargetReadOnly, step.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.ReadProjectStructure, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, step.AllowedOperations);
    }

    private static void AssertValidationStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepTargetScope.ExternalProductTargetReadOnly, step.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.RunValidation, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.LaunchRuntime, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.CaptureRuntimeProof, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, step.AllowedOperations);
    }

    private static void AssertImplementationOrRepairStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepTargetScope.ExternalProductTargetMutable, step.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.MutateProductTarget, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.RunValidation, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.LaunchRuntime, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.CaptureRuntimeProof, step.AllowedOperations);
    }

    private static void AssertRuntimeProofStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepTargetScope.ExternalProductTargetReadOnly, step.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.CaptureRuntimeProof, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.ExecuteExternalAction, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, step.AllowedOperations);
    }

    private static void AssertWritebackStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepTargetScope.ExternalActionControlled, step.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.ExecuteExternalAction, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, step.AllowedOperations);
    }

    private static void AssertPostReleaseLearningWritebackContract(ProcessStepImportExportModel step)
    {
        var artifactExpectation = Assert.Single(step.ArtifactExpectations);
        var contractText = string.Join(
            " ",
            step.EvidenceContractSummary,
            artifactExpectation.ValidationRequirementSummary);

        Assert.Contains("project_structure_node_create", contractText, StringComparison.Ordinal);
        Assert.Contains("decision node id", contractText, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSubprocessStep(
        ProcessDefinitionImportExportModel definition,
        string stepKey,
        string expectedSnapshotName)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepKind.Subprocess, step.StepKind);
        Assert.Equal(expectedSnapshotName, step.SubprocessDefinitionSnapshotName);
        AssertWritebackStep(definition, stepKey);
    }

    private static void AssertEscalationStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepTargetScope.ManagedProcessArtifactsOnly, step.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.EscalateOrDecide, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, step.AllowedOperations);
    }

    private static void AssertNoDemoTopicTerms(string value)
    {
        foreach (var term in DemoTopicTerms)
        {
            Assert.DoesNotContain(term, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> EnumerateRoleExecutorKinds(ProcessTemplatePack pack)
    {
        foreach (var roleTemplate in pack.RoleTemplates)
        {
            yield return roleTemplate.PreferredExecutorKind;
        }

        foreach (var process in pack.Processes.Values)
        {
            foreach (var usage in process.RoleUsages)
            {
                yield return usage.PreferredExecutorKind;
            }

            foreach (var role in process.LocalRoles)
            {
                yield return role.PreferredExecutorKind;
            }
        }

        foreach (var role in pack.SharedRoles.Values)
        {
            yield return role.PreferredExecutorKind;
        }
    }

    private static IEnumerable<string> EnumerateResponsibilityKinds(ProcessTemplatePack pack)
    {
        return pack.Processes.Values
            .SelectMany(process => process.Steps)
            .SelectMany(step => step.RoleAssignments)
            .Select(assignment => assignment.ResponsibilityKind)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateArtifactKinds(ProcessTemplatePack pack)
    {
        return EnumerateTemplateArtifacts(pack)
            .Select(artifact => artifact.ArtifactKind)
            .Concat(pack.SharedArtifacts.Values.Select(artifact => artifact.ArtifactKind))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateTrustRequirements(ProcessTemplatePack pack)
    {
        return EnumerateTemplateArtifacts(pack)
            .Select(artifact => artifact.TrustRequirement)
            .Concat(pack.SharedArtifacts.Values.Select(artifact => artifact.DefaultTrustRequirement))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<ProcessTemplateArtifactExpectation> EnumerateTemplateArtifacts(ProcessTemplatePack pack)
    {
        return pack.StepTemplates
            .SelectMany(template => template.Template.ArtifactExpectations)
            .Concat(pack.Processes.Values
                .SelectMany(process => process.Steps)
                .SelectMany(step => step.ArtifactExpectations));
    }
}
