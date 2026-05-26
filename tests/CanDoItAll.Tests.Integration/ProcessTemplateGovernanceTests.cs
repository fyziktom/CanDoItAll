using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
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

    private static readonly (string TemplateKey, string ResolveStepKey, string CaptureStepKey, string WritebackStepKey)[] ScreenshotTemplateSteps =
    [
        ("app-page-screenshot", "resolve-single-page-target", "capture-page-screenshot", "review-and-store-screenshot"),
        ("app-pages-screenshot-set", "resolve-page-set-targets", "capture-page-screenshot-set", "review-and-store-screenshot-set")
    ];

    private static readonly (string ScenarioKey, string TemplateKey)[] RequiredTypedBaselineScenarios =
    [
        ("baseline-blazor-wasm-pwa-tetris", "blazor-app-delivery"),
        ("baseline-customer-onboarding", "customer-onboarding"),
        ("baseline-business-plan-development", "business-plan-development"),
        ("baseline-incident-response", "incident-response"),
        ("baseline-release-readiness-and-deployment", "release-readiness-and-deployment"),
        ("baseline-architecture-decision-governance", "architecture-decision-governance")
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
            AssertValidationStep(definition, "validate-blazor-runtime");
            AssertValidationStep(definition, "revalidate-blazor-repair");
            AssertWritebackStep(definition, "record-blazor-results");
            AssertWritebackStep(definition, "record-blazor-results-after-repair");
            AssertEscalationStep(definition, "escalate-blazor-unresolved-repair");
        }
    }

    [Fact]
    public async Task Tetris_wasm_pwa_baseline_SB05_INV_001_keeps_sample_specific_requirements_in_scenario_data()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var pack = packLoader.Load();
        var scenario = Assert.Single(
            pack.BaselineScenarios,
            item => string.Equals(item.Key, "baseline-blazor-wasm-pwa-tetris", StringComparison.Ordinal));
        var projectedDefinition = projectionService.GetProjectedEnvelope(scenario.ProcessTemplateKey).Definition;

        Assert.Equal("blazor-app-delivery", scenario.ProcessTemplateKey);
        Assert.Contains("Blazor WebAssembly PWA Tetris", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playable Tetris board", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("falling tetrominoes", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keyboard controls", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scoring", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line clear", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("game over", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("restart", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pause/resume", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PWA manifest/service-worker offline readiness", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("build/test proof", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browser screenshot", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("console proof", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project-structure writeback", scenario.TriggerReason, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(scenario.Transitions, transition =>
            string.Equals(transition.StepKey, "resolve-blazor-contract", StringComparison.Ordinal) &&
            transition.Reason.Contains("without implementation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scenario.Artifacts, artifact =>
            string.Equals(artifact.StepKey, "validate-blazor-runtime", StringComparison.Ordinal) &&
            artifact.ReviewSummary.Contains("gameplay assertions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scenario.Artifacts, artifact =>
            string.Equals(artifact.StepKey, "record-blazor-results", StringComparison.Ordinal) &&
            artifact.ReviewSummary.Contains("project-structure writeback", StringComparison.OrdinalIgnoreCase));

        Assert.False(AllowsProductMutation(GetStep(projectedDefinition, "resolve-blazor-contract")));
        Assert.True(AllowsProductMutation(GetStep(projectedDefinition, "implement-blazor-change")));
        Assert.False(AllowsProductMutation(GetStep(projectedDefinition, "validate-blazor-runtime")));
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
                SubprocessChildArtifactExpectationId = childExpectationId
            },
            resource: null,
            id: Guid.NewGuid());

        Assert.Equal("qa-report-json", artifact.WorkflowOutputId);
        Assert.Equal("QA report", artifact.WorkflowOutputName);
        Assert.Equal(WorkflowArtifactKind.Json, artifact.WorkflowOutputKind);
        Assert.Equal(childExpectationId, artifact.SubprocessChildArtifactExpectationId);
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

    private static void AssertEscalationStep(ProcessDefinitionImportExportModel definition, string stepKey)
    {
        var step = GetStep(definition, stepKey);

        Assert.Equal(ProcessStepTargetScope.ManagedProcessArtifactsOnly, step.OperationTargetScope);
        Assert.Contains(ProcessStepOperation.EscalateOrDecide, step.AllowedOperations);
        Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts, step.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, step.AllowedOperations);
    }
}
