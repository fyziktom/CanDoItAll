using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDefinitionCatalogProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Catalog_query_filters_definitions_and_selects_requested_item()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var selectedKey = new ProcessDefinitionCatalogItemKey("architecture-review");

        var catalog = await service.GetCatalogAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogQueryProjection("architecture", selectedKey, ProcessDefinitionCatalogScopeKind.All, Take: 20));

        var item = Assert.Single(catalog.Items);
        Assert.Equal(selectedKey, item.Key);
        Assert.Equal(selectedKey, catalog.SelectedDefinitionKey);
        Assert.Equal("Architecture review", catalog.SelectedItem?.Name);
        Assert.Equal(2, catalog.PublishedDefinitionCount);
        Assert.Contains(catalog.ScopeGroups, group => group.ScopeKind == ProcessDefinitionCatalogScopeKind.Global && group.Count == 2);
    }

    [Fact]
    public async Task Catalog_query_uses_ordinal_search_independent_of_current_culture()
    {
        using var culture = UseCulture("tr-TR");
        using var pack = TemporaryProcessTemplatePack.Create(
            ("culture-case", "culture-case", "INDIGO delivery", "Default flow"));
        var service = new ProcessDefinitionCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var catalog = await service.GetCatalogAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogQueryProjection("indigo", SelectedDefinitionKey: null, ProcessDefinitionCatalogScopeKind.All, Take: 20));

        var item = Assert.Single(catalog.Items);
        Assert.Equal("INDIGO delivery", item.Name);
    }

    [Fact]
    public async Task Template_catalog_query_uses_ordinal_search_independent_of_current_culture()
    {
        using var culture = UseCulture("tr-TR");
        using var pack = TemporaryProcessTemplatePack.Create(
            ("culture-case", "culture-case", "INDIGO delivery", "Default flow"));
        var service = new ProcessTemplateCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var catalog = await service.GetCatalogAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("culture-case"),
            new ProcessTemplateCatalogQueryProjection(
                "indigo",
                ProcessTemplateCatalogCategoryKind.All,
                SelectedItemKey: null,
                ProcessTemplateCatalogPreviewTabKind.Overview,
                Take: 20),
            stepEditor: null);

        var item = Assert.Single(catalog.Items);
        Assert.Equal(ProcessTemplateCatalogItemKind.Process, item.Kind);
        Assert.Equal("INDIGO delivery", item.Title);
    }

    [Fact]
    public async Task Feed_defaults_returns_command_receipt_and_refresh_token()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCatalogProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var receipt = await service.FeedDefaultDefinitionsAsync(
            new ProcessDefinitionFeedDefaultsCommand(ProcessWorkspaceShellScope.Global));

        Assert.Equal(ProcessDefinitionCatalogCommandStatus.Accepted, receipt.Status);
        Assert.Equal(ProcessDefinitionCatalogCommandKind.FeedDefaults, receipt.CommandKind);
        Assert.Equal(2, receipt.AffectedDefinitionCount);
        Assert.StartsWith("feed-defaults:test-pack:", receipt.RefreshToken.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Loader_rejects_manifest_definition_key_mismatch()
    {
        using var pack = TemporaryProcessTemplatePack.Create(
            ("manifest-key", "definition-key", "Mismatched definition", "Mismatch summary"));
        var loader = new ProcessTemplatePackLoader(pack.RootPath);

        var exception = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("does not match manifest key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_feature_code_change_keeps_browser_proof_out_of_atomic_targeted_validation_step()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-feature-function-implementation");

        var codeChange = Assert.Single(definition.Steps, step => string.Equals(step.Key, "code-change", StringComparison.Ordinal));
        var implementationApproach = Assert.Single(definition.Steps, step => string.Equals(step.Key, "implementation-approach", StringComparison.Ordinal));
        var targetedValidation = Assert.Single(definition.Steps, step => string.Equals(step.Key, "targeted-validation", StringComparison.Ordinal));
        var featureRepair = Assert.Single(definition.Steps, step => string.Equals(step.Key, "feature-repair", StringComparison.Ordinal));
        var testContract = Assert.Single(definition.Steps, step => string.Equals(step.Key, "test-contract", StringComparison.Ordinal));
        var targetedRecheck = Assert.Single(definition.Steps, step => string.Equals(step.Key, "targeted-recheck", StringComparison.Ordinal));
        var codeChangeDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-feature-function-implementation",
            "steps",
            "code-change.md"));
        var targetedValidationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-feature-function-implementation",
            "steps",
            "targeted-validation.md"));
        var targetedRecheckDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-feature-function-implementation",
            "steps",
            "targeted-recheck.md"));

        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, codeChange.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, codeChange.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, targetedValidation.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, targetedValidation.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, featureRepair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, featureRepair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, targetedRecheck.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, targetedRecheck.AllowedOperations);
        Assert.Contains("does not own runtime launch", codeChange.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not block this step only because browser proof is missing", codeChange.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing restore/build/test receipts are not a manager escalation reason", codeChange.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not return NeedsManager", codeChange.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Return Succeeded / Completed once product mutation and the grounded change-set artifact are complete", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("expected values and recorded history follow from the exact action sequence", testContract.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Source-text, markup-content, selector-presence, or copied-label assertions may prove content hygiene only", testContract.Notes, StringComparison.Ordinal);
        Assert.Contains("A visible control or product shell without the assigned event wiring and observable state transition is incomplete", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains(
            codeChange.BranchOutcomes,
            branch => string.Equals(branch.Key, "implementation-attempt-incomplete", StringComparison.Ordinal));
        Assert.Contains("stop repeating that strategy", codeChangeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime will select `implementation-attempt-incomplete`", codeChangeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not send the same implementation strategy back for another blind retry", targetedValidationDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("choose `feature-repair-required`", targetedValidationDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Choose feature-repair-required when an acceptance-critical behavior is unwired, unexecuted, replaced by fabricated test-only state, or deferred to a later slice", targetedValidation.Notes, StringComparison.Ordinal);
        Assert.Contains("Budget the tool loop around product mutation", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("one bounded current-run product mutation operation", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("scoped to the grounded target", codeChange.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Http.Json", codeChange.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("@inherits <BaseClass>", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("invalid generated test", codeChange.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not cite stale source document paths", codeChange.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("ComponentBase", codeChange.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("shared _Imports.razor repairs", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains(
            featureRepair.BranchOutcomes,
            branch => string.Equals(branch.Key, "repair-attempt-incomplete", StringComparison.Ordinal));
        Assert.Contains(
            featureRepair.BranchOutcomes,
            branch => string.Equals(branch.Key, "feature-repair-applied", StringComparison.Ordinal));
        Assert.Equal("feature-repair-applied", targetedRecheck.DependsOnBranchOutcomeKey);
        Assert.Contains(
            targetedRecheck.Dependencies,
            dependency => dependency.DependsOnStepKey == "feature-repair" &&
                          dependency.DependsOnBranchOutcomeKey == "feature-repair-applied");
        Assert.Contains("derive the owning source, symbol, import, registration, and runtime boundary", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("without assuming a stock scaffold layout or generated component", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("current-run build/test receipts prove the corrected validation target no longer reproduces the defect", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("do not write Status: Completed by relying on inspection", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("failing generated test", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not cite stale source document paths", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not copy SourceDocName, SourceDocLink", implementationApproach.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not select feature-repair-required from feature-repair", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("ResolvePackageAssets", targetedValidation.Notes, StringComparison.Ordinal);
        Assert.Contains("Return Status: Completed", targetedValidation.Notes, StringComparison.Ordinal);
        Assert.Contains("Branch outcome key: feature-accepted", targetedValidation.Notes, StringComparison.Ordinal);
        Assert.Contains("feature-repair-required", targetedValidation.Notes, StringComparison.Ordinal);
        Assert.Contains("product proof failure is a completed validation decision", targetedValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parent runtime-command and screenshot writeback steps", targetedRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Branch outcome key: feature-repair-escalation", targetedRecheck.Notes, StringComparison.Ordinal);
        Assert.Contains("repair-attempt-incomplete", targetedRecheckDoc, StringComparison.Ordinal);
        Assert.Contains("missing build/test receipts are not a reason to escalate", targetedRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required current-run focused proof was attempted", targetedRecheck.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation tool or product root is unavailable", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sequential same-validation-target build/test blockers", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not treat a later compiler or test failure as outside scope", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not escalate same-validation-target compile or test failures", featureRepair.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not choose `feature-repair-escalation` only because runtime/browser proof", targetedRecheckDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Software_delivery_feature_intake_uses_grounded_scope_packet_and_ignores_stale_cross_domain_source_docs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("software-delivery");

        var featureIntake = Assert.Single(definition.Steps, step => string.Equals(step.Key, "feature-intake", StringComparison.Ordinal));

        Assert.Contains("artifacts/process-runs/<current-process-run-id>/steps/feature-intake.md", featureIntake.Notes, StringComparison.Ordinal);
        Assert.Contains("do not write native absolute product paths", featureIntake.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SourceDocName values, SourceDocLink values", featureIntake.Notes, StringComparison.Ordinal);
        Assert.Contains("managed-files paths", featureIntake.Notes, StringComparison.Ordinal);
        Assert.Contains("Treat the active launch request and selected project node as authoritative", featureIntake.Notes, StringComparison.Ordinal);
        Assert.Contains("do not carry that other domain into scope", featureIntake.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Return Succeeded / Completed once the grounded scope packet is written", featureIntake.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not return NeedsManager", featureIntake.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_development_slice_keeps_browser_proof_out_of_slice_test_validation_step()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-development-slice");

        var addTestsAndProof = Assert.Single(definition.Steps, step => string.Equals(step.Key, "add-tests-and-proof", StringComparison.Ordinal));
        var addTestsRecheck = Assert.Single(definition.Steps, step => string.Equals(step.Key, "add-tests-recheck", StringComparison.Ordinal));

        Assert.Contains(ProcessOperationContractNames.RunValidation, addTestsAndProof.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, addTestsAndProof.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, addTestsAndProof.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, addTestsRecheck.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, addTestsRecheck.AllowedOperations);
        Assert.DoesNotContain("read-only", addTestsAndProof.Subtitle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("read-only", addTestsRecheck.Subtitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typed dotnet-solution-context artifact first", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typed dotnet-solution-context artifact first", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without constructing conventional names or layouts", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without constructing conventional names or layouts", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<SolutionName>", addTestsAndProof.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("<SolutionName>", addTestsRecheck.Notes, StringComparison.Ordinal);
        Assert.Contains("root runtime-command and screenshot writeback steps", addTestsRecheck.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProcessOperationContractNames.ExecuteExternalAction, addTestsAndProof.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.ExecuteExternalAction, addTestsRecheck.AllowedOperations);
    }

    [Fact]
    public void Dotnet_architecture_classifier_forbids_invented_source_document_paths()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-architecture-design-review");

        var classify = Assert.Single(definition.Steps, step => string.Equals(step.Key, "classify-dotnet-application", StringComparison.Ordinal));
        var classifyDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-architecture-design-review",
            "steps",
            "classify-dotnet-application.md"));

        Assert.NotNull(classify);
        Assert.Contains("project structure and scope identify the intended app type", classify.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cite stable project-structure node ids", classify.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("DotNetAppArchetype", classify.Notes, StringComparison.Ordinal);
        Assert.Contains("Classify the intended app type only from the project structure", classifyDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stable project-structure node ids, artifact refs, titles", classifyDoc, StringComparison.Ordinal);
        Assert.DoesNotContain("DotNetAppArchetype", classifyDoc, StringComparison.Ordinal);
        Assert.Contains("Do not cite source document paths, native absolute paths", classifyDoc, StringComparison.Ordinal);
        Assert.Contains("Cite stable project-structure node ids, artifact refs, titles, or current-run workspace tool receipts", classifyDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Software_delivery_qa_runs_validation_before_routing_missing_receipts_to_repair()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("software-delivery");

        var qaValidation = Assert.Single(definition.Steps, step => string.Equals(step.Key, "qa-validation", StringComparison.Ordinal));
        var qaRecheck = Assert.Single(definition.Steps, step => string.Equals(step.Key, "qa-recheck", StringComparison.Ordinal));

        Assert.Contains(ProcessOperationContractNames.RunValidation, qaValidation.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.RunValidation, qaRecheck.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.LaunchRuntime, qaValidation.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.CaptureRuntimeProof, qaValidation.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.LaunchRuntime, qaRecheck.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.CaptureRuntimeProof, qaRecheck.AllowedOperations);
        Assert.Contains("runtime-forwarded typed child context", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without constructing conventional names or layouts", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing restore/build/test receipts must trigger validation execution first", qaValidation.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not probe a guessed product-root <App>.csproj", qaValidation.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ask a manager to assign another agent", qaValidation.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact restore/build/test receipt refs", qaValidation.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime-forwarded typed child context", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without constructing conventional names or layouts", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required current-run repaired validation was attempted", qaRecheck.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact restore/build/test receipt refs", qaRecheck.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not probe a guessed product-root <App>.csproj", qaRecheck.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ask a manager to assign another agent", qaRecheck.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_analyze_images", qaValidation.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_analyze_images", qaRecheck.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Software_delivery_propagates_architecture_and_validation_plan_to_implementation_qa_and_repair()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var delivery = loader.LoadDefinition("software-delivery");
        var architecture = loader.LoadDefinition("dotnet-architecture-design-review");

        var architectureReview = Assert.Single(delivery.Steps, step => string.Equals(step.Key, "architecture-review", StringComparison.Ordinal));
        var implementation = Assert.Single(delivery.Steps, step => string.Equals(step.Key, "implementation", StringComparison.Ordinal));
        var qaValidation = Assert.Single(delivery.Steps, step => string.Equals(step.Key, "qa-validation", StringComparison.Ordinal));
        var qualityRepair = Assert.Single(delivery.Steps, step => string.Equals(step.Key, "quality-repair", StringComparison.Ordinal));
        var qaRecheck = Assert.Single(delivery.Steps, step => string.Equals(step.Key, "qa-recheck", StringComparison.Ordinal));
        var validationPlan = Assert.Single(architecture.Steps, step => string.Equals(step.Key, "design-validation-plan", StringComparison.Ordinal));
        var sliceIntakeDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice",
            "steps",
            "slice-intake.md"));

        Assert.Contains(architectureReview.ArtifactExpectations, artifact =>
            string.Equals(artifact.Key, "acceptance-validation-plan", StringComparison.Ordinal) &&
            string.Equals(artifact.SubprocessChildStepKey, "design-validation-plan", StringComparison.Ordinal));
        Assert.Equal("implementation-plan", validationPlan.ArtifactExpectations.Single(artifact =>
            string.Equals(artifact.Key, "acceptance-validation-plan", StringComparison.Ordinal)).TemplateKey);

        var architectureContract = Assert.IsType<ProcessSubprocessContract>(architectureReview.SubprocessContract);
        Assert.Contains(architectureContract.AcceptedChildOutputs, output =>
            string.Equals(output.StepKey, "design-validation-plan", StringComparison.Ordinal) &&
            string.Equals(output.ArtifactExpectationKey, "acceptance-validation-plan", StringComparison.Ordinal));

        foreach (var step in new[] { implementation, qaValidation, qualityRepair, qaRecheck })
        {
            Assert.Contains(step.ArtifactInputs, input =>
                string.Equals(input.ArtifactExpectationKey, "architecture-decision-record", StringComparison.Ordinal) &&
                string.Equals(input.SourceStepKey, "architecture-review", StringComparison.Ordinal));
            Assert.Contains(step.ArtifactInputs, input =>
                string.Equals(input.ArtifactExpectationKey, "acceptance-validation-plan", StringComparison.Ordinal) &&
                string.Equals(input.SourceStepKey, "architecture-review", StringComparison.Ordinal));
        }

        Assert.Contains("ProductAcceptanceCriteriaContract", qaValidation.InputContractSummary, StringComparison.Ordinal);
        Assert.Contains("acceptance-driven validation plan", qaRecheck.InputContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("acceptance-driven validation plan", qualityRepair.InputContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("For a game", sliceIntakeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("local best score", sliceIntakeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gameplay", sliceIntakeDoc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dotnet_solution_setup_routes_repairable_first_build_failures_through_setup_repair()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-solution-setup");

        var validate = Assert.Single(definition.Steps, step => string.Equals(step.Key, "validate-first-build", StringComparison.Ordinal));
        var create = Assert.Single(definition.Steps, step => string.Equals(step.Key, "create-dotnet-project", StringComparison.Ordinal));
        var addTest = Assert.Single(definition.Steps, step => string.Equals(step.Key, "add-test-project", StringComparison.Ordinal));
        var repair = Assert.Single(definition.Steps, step => string.Equals(step.Key, "repair-solution-setup", StringComparison.Ordinal));
        var revalidate = Assert.Single(definition.Steps, step => string.Equals(step.Key, "validate-first-build-after-repair", StringComparison.Ordinal));
        var handoff = Assert.Single(definition.Steps, step => string.Equals(step.Key, "setup-handoff", StringComparison.Ordinal));
        var repairedHandoff = Assert.Single(definition.Steps, step => string.Equals(step.Key, "setup-handoff-after-repair", StringComparison.Ordinal));
        var repairEscalation = Assert.Single(definition.Steps, step => string.Equals(step.Key, "setup-repair-escalation", StringComparison.Ordinal));

        Assert.Contains(validate.BranchOutcomes, outcome => string.Equals(outcome.Key, "setup-validated", StringComparison.Ordinal));
        Assert.Contains(validate.BranchOutcomes, outcome => string.Equals(outcome.Key, "setup-repair-required", StringComparison.Ordinal));
        Assert.Contains("Select setup-repair-required", validate.ExceptionPolicySummary, StringComparison.Ordinal);
        Assert.Contains("ProductCompletionRequiredToolReceipts", validate.Notes, StringComparison.Ordinal);
        Assert.Contains("successful current-run receipts for restore, build, and test", validate.Notes, StringComparison.Ordinal);

        Assert.Contains("ProductCompletionRequiredToolReceipts", create.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", create.Notes, StringComparison.Ordinal);
        Assert.Contains("ensure the solution contains the app project", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("target-framework file-content checks", create.Notes, StringComparison.Ordinal);
        Assert.Contains("DotNetCreateProjectScript", create.Notes, StringComparison.Ordinal);
        Assert.Contains("solution app-membership file-content check", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Create the grounded product root and contracted app parent directory", create.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not write native absolute paths", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scoped storage paths under artifacts/scopes", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tool-run stdout/stderr paths", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProcessTemplateStepExecutionClasses.RuntimeOwnedToolPlan, create.ExecutionClass);
        Assert.Equal("dotnet.solution-setup", create.ExecutionContract?.RuntimeOwnedExecutorKey);
        Assert.Equal("dotnet.create-project", create.ExecutionContract?.DeterministicToolPlan?.PlanKey);
        Assert.Equal("DotNetCreateProjectExecutionPlan", create.ExecutionContract?.DeterministicToolPlan?.ExecutionPlanLaunchVariable);
        Assert.True(create.ExecutionContract?.DeterministicToolPlan?.RequiresReadbackChecks);

        Assert.Contains("ProductCompletionRequiredToolReceipts", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("sideEffectManifest mode ProductMutation", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("dotnet add <test-project-file> reference <app-project-file>", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("convert command output to scalar strings before membership or ProjectReference regex checks", addTest.Notes, StringComparison.Ordinal);
        Assert.Equal(ProcessTemplateStepExecutionClasses.RuntimeOwnedToolPlan, addTest.ExecutionClass);
        Assert.Equal("dotnet.solution-setup", addTest.ExecutionContract?.RuntimeOwnedExecutorKey);
        Assert.Equal("dotnet.add-test-project", addTest.ExecutionContract?.DeterministicToolPlan?.PlanKey);
        Assert.True(addTest.ExecutionContract?.DeterministicToolPlan?.RequiresReadbackChecks);

        Assert.Equal("validate-first-build", repair.DependsOnStepKey);
        Assert.Equal("setup-repair-required", repair.DependsOnBranchOutcomeKey);
        Assert.Contains(ProcessOperationContractNames.MutateProductTarget, repair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.RunValidation, repair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, repair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, repair.AllowedOperations);
        Assert.Contains("Do not implement feature behavior", repair.Notes, StringComparison.Ordinal);
        Assert.Contains("sideEffectManifest mode ProductMutation", repair.Notes, StringComparison.Ordinal);
        Assert.Contains("join them before matching", repair.Notes, StringComparison.Ordinal);
        Assert.Contains("prefer that deterministic helper plan", repair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DotNetAddTestProjectScript", repair.Notes, StringComparison.Ordinal);
        Assert.Contains("System.IO.Path.GetRelativePath", repair.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not hardcode escaped relative ProjectReference strings", repair.Notes, StringComparison.Ordinal);
        Assert.Equal(ProcessTemplateStepExecutionClasses.RuntimeOwnedToolPlan, repair.ExecutionClass);
        Assert.Equal("dotnet.solution-setup", repair.ExecutionContract?.RuntimeOwnedExecutorKey);
        Assert.Equal("dotnet.repair-solution-setup", repair.ExecutionContract?.DeterministicToolPlan?.PlanKey);
        Assert.True(repair.ExecutionContract?.DeterministicToolPlan?.RequiresReadbackChecks);

        Assert.Equal("repair-solution-setup", revalidate.DependsOnStepKey);
        Assert.Contains(revalidate.BranchOutcomes, outcome => string.Equals(outcome.Key, "setup-validated", StringComparison.Ordinal));
        Assert.Contains(revalidate.BranchOutcomes, outcome => string.Equals(outcome.Key, "setup-repair-escalation", StringComparison.Ordinal));
        Assert.Contains(ProcessOperationContractNames.RunValidation, revalidate.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, revalidate.AllowedOperations);
        Assert.Contains("ProductCompletionRequiredToolReceipts", revalidate.Notes, StringComparison.Ordinal);
        Assert.Contains("successful current-run receipts for restore, build, and test", revalidate.Notes, StringComparison.Ordinal);

        Assert.Equal("validate-first-build", handoff.DependsOnStepKey);
        Assert.Equal("setup-validated", handoff.DependsOnBranchOutcomeKey);
        Assert.Equal("validate-first-build-after-repair", repairedHandoff.DependsOnStepKey);
        Assert.Equal("setup-validated", repairedHandoff.DependsOnBranchOutcomeKey);
        Assert.Equal("validate-first-build-after-repair", repairEscalation.DependsOnStepKey);
        Assert.Equal("setup-repair-escalation", repairEscalation.DependsOnBranchOutcomeKey);

        var setupContract = string.Join(
            Environment.NewLine,
            definition.Steps.Select(step => string.Join(
                Environment.NewLine,
                step.Title,
                step.Notes,
                step.ExceptionPolicySummary,
                string.Join(Environment.NewLine, step.ArtifactExpectations.Select(expectation => expectation.ValidationRequirementSummary)))));
        Assert.DoesNotContain("tetris", setupContract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tetromino", setupContract, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dotnet_feature_implementation_approach_requires_current_run_artifact_evidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-feature-function-implementation");
        var implementationApproach = Assert.Single(definition.Steps, step => string.Equals(step.Key, "implementation-approach", StringComparison.Ordinal));
        var implementationApproachDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-feature-function-implementation",
            "steps",
            "implementation-approach.md"));

        Assert.Contains("current-run feature intake artifact", implementationApproach.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not invent or require", implementationApproach.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifacts/process-runs/<current-process-run-id>/steps/implementation-approach.md", implementationApproach.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain(
            implementationApproach.ArtifactInputs,
            input => string.Equals(input.ArtifactExpectationKey, "feature-acceptance-criteria", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("intake artifact path", implementationApproach.ArtifactExpectations[0].ValidationRequirementSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current-run feature intake artifact", implementationApproachDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not invent or require", implementationApproachDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not copy `SourceDocName`, `SourceDocLink`", implementationApproachDoc, StringComparison.Ordinal);
        Assert.Contains("Do not block only because there is no prior assistant prose", implementationApproachDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_feature_targeted_validation_requires_finalized_evidence_refs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-feature-function-implementation");
        var targetedValidation = Assert.Single(definition.Steps, step => string.Equals(step.Key, "targeted-validation", StringComparison.Ordinal));
        var targetedValidationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-feature-function-implementation",
            "steps",
            "targeted-validation.md"));

        Assert.Contains("do not leave the artifact as in progress", targetedValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not return Blocked with empty evidence refs", targetedValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("command receipt/output refs", targetedValidation.ArtifactExpectations[0].ValidationRequirementSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not leave this artifact as `in progress`", targetedValidationDoc, StringComparison.Ordinal);
        Assert.Contains("Branch outcome key: feature-accepted", targetedValidationDoc, StringComparison.Ordinal);
        Assert.Contains("do not return `Blocked` with empty evidence refs", targetedValidationDoc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Full_app_templates_reject_scaffold_only_mvp_slices()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var softwareDelivery = loader.LoadDefinition("software-delivery");
        var developmentSlice = loader.LoadDefinition("dotnet-development-slice");
        var featureImplementation = loader.LoadDefinition("dotnet-feature-function-implementation");

        var deliveryImplementation = Assert.Single(softwareDelivery.Steps, step => string.Equals(step.Key, "implementation", StringComparison.Ordinal));
        var deliveryQa = Assert.Single(softwareDelivery.Steps, step => string.Equals(step.Key, "qa-validation", StringComparison.Ordinal));
        var deliveryRepair = Assert.Single(softwareDelivery.Steps, step => string.Equals(step.Key, "quality-repair", StringComparison.Ordinal));
        var deliveryRecheck = Assert.Single(softwareDelivery.Steps, step => string.Equals(step.Key, "qa-recheck", StringComparison.Ordinal));
        var sliceIntake = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-intake", StringComparison.Ordinal));
        var sliceImplementation = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "implement-code-change", StringComparison.Ordinal));
        var sliceValidation = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "add-tests-and-proof", StringComparison.Ordinal));
        var sliceRepair = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-repair-code-change", StringComparison.Ordinal));
        var sliceRecheck = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "add-tests-recheck", StringComparison.Ordinal));
        var repairedHandoff = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-handoff-after-repair", StringComparison.Ordinal));
        var initialManagerRepair = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "initial-manager-assisted-repair", StringComparison.Ordinal));
        var initialManagerRepairedHandoff = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-handoff-after-initial-manager-repair", StringComparison.Ordinal));
        var managerRepair = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-manager-assisted-repair", StringComparison.Ordinal));
        var managerRepairedHandoff = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-handoff-after-manager-repair", StringComparison.Ordinal));
        var featureIntake = Assert.Single(featureImplementation.Steps, step => string.Equals(step.Key, "feature-slice-intake", StringComparison.Ordinal));

        var deliveryImplementationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "software-delivery",
            "steps",
            "implementation.md"));
        var sliceIntakeDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice",
            "steps",
            "slice-intake.md"));
        var sliceImplementationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice",
            "steps",
            "implement-code-change.md"));
        var featureIntakeDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-feature-function-implementation",
            "steps",
            "feature-slice-intake.md"));

        Assert.Contains("must not be scaffold-only", deliveryImplementation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be scaffold-only", deliveryImplementationDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be scaffold-only", sliceIntake.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be scaffold-only", sliceIntakeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not launch this child", sliceImplementation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not launch the child", sliceImplementationDoc, StringComparison.OrdinalIgnoreCase);
        var initialContract = Assert.IsType<ProcessSubprocessContract>(sliceImplementation.SubprocessContract);
        Assert.Contains(initialContract.AcceptedChildOutputs, output =>
            output.StepKey == "feature-handoff" &&
            output.ParentBranchOutcomeKey == "implementation-ready");
        Assert.Contains(initialContract.NoGoChildOutputs, output =>
            output.StepKey == "feature-repair-escalation" &&
            output.ParentBranchOutcomeKey == "implementation-needs-manager-repair");
        Assert.Contains(
            initialContract.NoGoChildOutputs,
            output => output.StepKey == "targeted-recheck" &&
                      output.BranchOutcomeKey == "feature-repair-escalation" &&
                      output.ParentBranchOutcomeKey == "implementation-needs-manager-repair");
        Assert.Contains(sliceImplementation.BranchOutcomes, outcome => outcome.Key == "implementation-ready");
        Assert.Contains(sliceImplementation.BranchOutcomes, outcome => outcome.Key == "implementation-needs-manager-repair");
        Assert.Contains(
            sliceValidation.Dependencies,
            dependency => dependency.DependsOnStepKey == "implement-code-change" &&
                          dependency.DependsOnBranchOutcomeKey == "implementation-ready");
        Assert.Contains("select `implementation-needs-manager-repair`", sliceImplementationDoc, StringComparison.Ordinal);
        Assert.Contains("must not be scaffold-only", featureIntake.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a valid derived behavior", featureIntakeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("every explicitly named core MVP behavior", sliceIntake.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not silently invent a later slice", sliceIntakeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit remaining-slice schedule", featureIntake.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("complete runnable core workflow", featureIntakeDoc, StringComparison.OrdinalIgnoreCase);
        foreach (var step in new[] { sliceImplementation, sliceValidation, sliceRepair, sliceRecheck, repairedHandoff, initialManagerRepair, initialManagerRepairedHandoff, managerRepair, managerRepairedHandoff })
        {
            Assert.Contains(
                step.ArtifactInputs,
                input => string.Equals(input.ArtifactExpectationKey, "slice-scope-packet", StringComparison.Ordinal) &&
                         string.Equals(input.SourceStepKey, "slice-intake", StringComparison.Ordinal));
        }

        var repairContract = Assert.IsType<ProcessSubprocessContract>(sliceRepair.SubprocessContract);
        Assert.Contains(repairContract.AcceptedChildOutputs, output => output.StepKey == "feature-repair-escalation");
        Assert.Empty(repairContract.NoGoChildOutputs);

        var managerRepairContract = Assert.IsType<ProcessSubprocessContract>(managerRepair.SubprocessContract);
        Assert.Equal("dotnet-quality-repair", managerRepairContract.DefinitionKey);
        Assert.Contains(managerRepairContract.AcceptedChildOutputs, output => output.StepKey == "quality-repair-handoff");
        Assert.Contains(managerRepairContract.AcceptedChildOutputs, output => output.StepKey == "quality-repair-handoff-after-bughunt");
        Assert.Contains(managerRepairContract.NoGoChildOutputs, output => output.StepKey == "quality-repair-no-go");

        var deliveryContract = Assert.IsType<ProcessSubprocessContract>(deliveryImplementation.SubprocessContract);
        Assert.Contains(deliveryContract.AcceptedChildOutputs, output => output.StepKey == "slice-handoff-after-initial-manager-repair");
        Assert.Contains(deliveryContract.AcceptedChildOutputs, output => output.StepKey == "slice-handoff-after-manager-repair");
        Assert.DoesNotContain(deliveryContract.NoGoChildOutputs, output => output.StepKey == "slice-repair-escalation");

        foreach (var step in new[] { deliveryQa, deliveryRepair, deliveryRecheck })
        {
            Assert.Contains(
                step.ArtifactInputs,
                input => string.Equals(input.ArtifactExpectationKey, "scope-boundary-packet", StringComparison.Ordinal) &&
                         string.Equals(input.SourceStepKey, "feature-intake", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Dotnet_repair_subprocesses_preserve_inherited_repair_target()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var developmentSlice = loader.LoadDefinition("dotnet-development-slice");
        var featureImplementation = loader.LoadDefinition("dotnet-feature-function-implementation");

        var sliceValidation = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "add-tests-and-proof", StringComparison.Ordinal));
        var sliceRepair = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-repair-code-change", StringComparison.Ordinal));
        var sliceRecheck = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "add-tests-recheck", StringComparison.Ordinal));
        var featureIntake = Assert.Single(featureImplementation.Steps, step => string.Equals(step.Key, "feature-slice-intake", StringComparison.Ordinal));
        var targetedValidation = Assert.Single(featureImplementation.Steps, step => string.Equals(step.Key, "targeted-validation", StringComparison.Ordinal));
        var targetedRecheck = Assert.Single(featureImplementation.Steps, step => string.Equals(step.Key, "targeted-recheck", StringComparison.Ordinal));

        var sliceRepairDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice",
            "steps",
            "slice-repair-code-change.md"));
        var featureIntakeDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-feature-function-implementation",
            "steps",
            "feature-slice-intake.md"));

        Assert.Contains(sliceValidation.ArtifactInputs, input =>
            string.Equals(input.ArtifactExpectationKey, "slice-architecture-decision", StringComparison.Ordinal) &&
            string.Equals(input.SourceStepKey, "slice-architecture-check", StringComparison.Ordinal));
        Assert.Contains("Do not ask the child subprocess to select a fresh MVP behavior", sliceRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("original repair target", sliceRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not derive a new MVP behavior", featureIntake.Notes, StringComparison.Ordinal);
        Assert.Contains("inherited repair target", targetedValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before/after metric", targetedRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("this validation step execution", targetedValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not satisfy this step's current-execution proof contract", targetedRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not ask the child subprocess to select a fresh MVP behavior", sliceRepairDoc, StringComparison.Ordinal);
        Assert.Contains("slice-scope-packet` remains authoritative across repair", sliceRepairDoc, StringComparison.Ordinal);
        Assert.Contains("If the parent request is a repair request", featureIntakeDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_development_slice_uses_engineer_for_technical_subprocess_steps()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-development-slice");

        var setup = Assert.Single(definition.Steps, step => string.Equals(step.Key, "prepare-solution-skeleton", StringComparison.Ordinal));
        var implementation = Assert.Single(definition.Steps, step => string.Equals(step.Key, "implement-code-change", StringComparison.Ordinal));
        var repair = Assert.Single(definition.Steps, step => string.Equals(step.Key, "slice-repair-code-change", StringComparison.Ordinal));

        AssertResponsibleRole(setup, "software-engineer");
        AssertResponsibleRole(implementation, "software-engineer");
        AssertResponsibleRole(repair, "software-engineer");
    }

    [Fact]
    public void Dotnet_development_slice_subprocess_steps_explicitly_launch_child_runs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-development-slice");

        var implementation = Assert.Single(definition.Steps, step => string.Equals(step.Key, "implement-code-change", StringComparison.Ordinal));
        var repair = Assert.Single(definition.Steps, step => string.Equals(step.Key, "slice-repair-code-change", StringComparison.Ordinal));
        var implementationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice",
            "steps",
            "implement-code-change.md"));
        var repairDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice",
            "steps",
            "slice-repair-code-change.md"));

        foreach (var step in new[] { implementation, repair })
        {
            Assert.Equal(ProcessOperationContractNames.ExternalActionControlled, step.OperationTargetScope);
            Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, step.AllowedOperations);
            Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, step.AllowedOperations);
            Assert.Contains("project_structure_process_subprocess_launch", step.Notes, StringComparison.Ordinal);
            Assert.Contains("dotnet-feature-function-implementation", step.Notes, StringComparison.Ordinal);
            Assert.Contains("ParentDeferredOutcomeJson", step.Notes, StringComparison.Ordinal);
            Assert.Contains("do not wait silently", step.Notes, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("project_structure_process_subprocess_launch", implementationDoc, StringComparison.Ordinal);
        Assert.Contains("ParentDeferredOutcomeJson", implementationDoc, StringComparison.Ordinal);
        Assert.Contains("project_structure_process_subprocess_launch", repairDoc, StringComparison.Ordinal);
        Assert.Contains("ParentDeferredOutcomeJson", repairDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_solution_setup_requires_the_architecture_target_framework_in_app_readback()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-solution-setup");
        var activation = Assert.Single(definition.LaunchDriverActivations);
        var serializedChecks = activation.Settings["ProductCompletionRequiredFileContentChecksByStep"];
        using var document = JsonDocument.Parse(serializedChecks);
        var createChecks = document.RootElement
            .GetProperty("create-dotnet-project")
            .EnumerateArray()
            .ToArray();

        Assert.Contains(createChecks, check =>
            check.GetProperty("pathCandidates")
                .EnumerateArray()
                .Select(candidate => candidate.GetString())
                .Contains("${DotNetAppProjectFileForwardSlash}", StringComparer.Ordinal) &&
            check.GetProperty("requiredTextAnyGroups")
                .EnumerateArray()
                .SelectMany(group => group.EnumerateArray())
                .Select(value => value.GetString())
                .Contains("<TargetFramework>${DotNetTargetFramework}</TargetFramework>", StringComparer.Ordinal));
    }

    [Fact]
    public void Dotnet_solution_setup_accepts_platform_path_variants_for_project_reference_readback()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-solution-setup");
        var activation = Assert.Single(definition.LaunchDriverActivations);
        var serializedChecks = activation.Settings["ProductCompletionRequiredFileContentChecksByStep"];
        using var document = JsonDocument.Parse(serializedChecks);

        foreach (var stepKey in new[] { "add-test-project", "repair-solution-setup" })
        {
            var checks = document.RootElement.GetProperty(stepKey).EnumerateArray().ToArray();
            var testProjectCheck = checks.Single(check =>
                check.GetProperty("pathCandidates")
                    .EnumerateArray()
                    .Select(candidate => candidate.GetString())
                    .Contains("${DotNetTestProjectFileForwardSlash}", StringComparer.Ordinal));
            var referenceGroup = testProjectCheck
                .GetProperty("requiredTextAnyGroups")
                .EnumerateArray()
                .Select(group => group.EnumerateArray().Select(value => value.GetString()).ToArray())
                .Single(group => group.Contains("${DotNetAppProjectReferenceRelativePath}", StringComparer.Ordinal));

            Assert.Contains("${DotNetAppProjectReferenceRelativePath}", referenceGroup, StringComparer.Ordinal);
            Assert.Contains("${DotNetAppProjectReferenceRelativePathWindows}", referenceGroup, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void Dotnet_development_slice_routes_repeated_repair_failure_through_manager_quality_repair()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-development-slice");

        var diagnosis = Assert.Single(definition.Steps, step => step.Key == "slice-repair-escalation");
        var managerRepair = Assert.Single(definition.Steps, step => step.Key == "slice-manager-assisted-repair");
        var handoff = Assert.Single(definition.Steps, step => step.Key == "slice-handoff-after-manager-repair");
        var managerRepairDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-development-slice",
            "steps",
            "slice-manager-assisted-repair.md"));

        Assert.Equal("Review", diagnosis.StepKind);
        Assert.Contains("manager repair diagnosis", diagnosis.OutputContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProcessTemplateStepKinds.Subprocess, managerRepair.StepKind);
        Assert.Equal(ProcessTemplateStepExecutionClasses.RuntimeOwnedSubprocess, managerRepair.ExecutionClass);
        Assert.Equal("dotnet-quality-repair", managerRepair.SubprocessProcessKey);
        Assert.Equal(ProcessOperationContractNames.ExternalActionControlled, managerRepair.OperationTargetScope);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, managerRepair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, managerRepair.AllowedOperations);
        Assert.Contains("dotnet-quality-repair", managerRepairDoc, StringComparison.Ordinal);
        Assert.Contains("project_structure_process_subprocess_launch", managerRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("ParentDeferredOutcomeJson", managerRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("project_structure_process_subprocess_launch", managerRepairDoc, StringComparison.Ordinal);
        Assert.Contains("ParentDeferredOutcomeJson", managerRepairDoc, StringComparison.Ordinal);
        Assert.Equal("slice-manager-assisted-repair", handoff.DependsOnStepKey);
    }

    [Fact]
    public void Software_delivery_quality_repair_delegates_diagnosis_mutation_and_validation_to_typed_subprocess()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("software-delivery");

        var qualityRepair = Assert.Single(definition.Steps, step => string.Equals(step.Key, "quality-repair", StringComparison.Ordinal));
        var qualityRepairDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "software-delivery",
            "steps",
            "quality-repair.md"));

        Assert.Equal(ProcessTemplateStepKinds.Subprocess, qualityRepair.StepKind);
        Assert.Equal(ProcessTemplateStepExecutionClasses.RuntimeOwnedSubprocess, qualityRepair.ExecutionClass);
        Assert.Equal("dotnet-quality-repair", qualityRepair.SubprocessProcessKey);
        Assert.Equal(ProcessOperationContractNames.ExternalActionControlled, qualityRepair.OperationTargetScope);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, qualityRepair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, qualityRepair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, qualityRepair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, qualityRepair.AllowedOperations);
        Assert.Contains("project_structure_process_subprocess_launch", qualityRepairDoc, StringComparison.Ordinal);
        Assert.Contains("dotnet-quality-repair", qualityRepairDoc, StringComparison.Ordinal);
        Assert.Contains("ParentDeferredOutcomeJson", qualityRepairDoc, StringComparison.Ordinal);
        var contract = Assert.IsType<ProcessSubprocessContract>(qualityRepair.SubprocessContract);
        Assert.Contains(contract.AcceptedChildOutputs, output => output.StepKey == "quality-repair-handoff");
        Assert.Contains(contract.AcceptedChildOutputs, output => output.StepKey == "quality-repair-handoff-after-bughunt");
        Assert.Contains(contract.NoGoChildOutputs, output => output.StepKey == "quality-repair-no-go");
    }

    [Fact]
    public void Dotnet_quality_repair_separates_manager_diagnosis_mutation_independent_qa_and_bughunt()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-quality-repair");

        var diagnosis = Assert.Single(definition.Steps, step => step.Key == "diagnose-quality-failure");
        var repair = Assert.Single(definition.Steps, step => step.Key == "implement-quality-repair");
        var validation = Assert.Single(definition.Steps, step => step.Key == "validate-quality-repair");
        var bughunt = Assert.Single(definition.Steps, step => step.Key == "diagnose-persistent-failure");
        var secondRepair = Assert.Single(definition.Steps, step => step.Key == "implement-bughunt-repair");
        var revalidation = Assert.Single(definition.Steps, step => step.Key == "revalidate-bughunt-repair");
        var noGo = Assert.Single(definition.Steps, step => step.Key == "quality-repair-no-go");

        AssertResponsibleRole(diagnosis, "bughunt-specialist");
        Assert.Contains(diagnosis.RoleAssignments, assignment =>
            string.Equals(assignment.RoleKey, "repair-manager", StringComparison.Ordinal) &&
            string.Equals(assignment.ResponsibilityKind, "Reviewer", StringComparison.Ordinal));
        AssertResponsibleRole(repair, "dotnet-repair-engineer");
        AssertResponsibleRole(validation, "quality-reviewer");
        AssertResponsibleRole(bughunt, "bughunt-specialist");
        AssertResponsibleRole(secondRepair, "dotnet-repair-engineer");
        AssertResponsibleRole(revalidation, "quality-reviewer");
        Assert.Contains(ProcessOperationContractNames.MutateProductTarget, repair.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.MutateProductTarget, secondRepair.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, diagnosis.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, validation.AllowedOperations);
        Assert.Equal(ProcessTemplateStepExecutionClasses.BranchDecision, repair.ExecutionClass);
        Assert.Contains(repair.BranchOutcomes, outcome => outcome.Key == "product-repair-applied");
        Assert.Contains(repair.BranchOutcomes, outcome => outcome.Key == "proof-only-revalidation-prepared");
        Assert.Equal(ProcessTemplateStepExecutionClasses.BranchDecision, secondRepair.ExecutionClass);
        Assert.Contains(secondRepair.BranchOutcomes, outcome => outcome.Key == "product-repair-applied");
        Assert.Contains(secondRepair.BranchOutcomes, outcome => outcome.Key == "proof-only-revalidation-prepared");
        Assert.Contains(validation.BranchOutcomes, outcome => outcome.Key == "quality-repair-accepted");
        Assert.Contains(validation.BranchOutcomes, outcome => outcome.Key == "bughunt-required");
        Assert.Contains(revalidation.BranchOutcomes, outcome => outcome.Key == "quality-repair-accepted");
        Assert.Contains(revalidation.BranchOutcomes, outcome => outcome.Key == "quality-repair-no-go");
        Assert.Contains(validation.BranchOutcomes, outcome =>
            outcome.Key == "bughunt-required" && outcome.AllowsCompletedOutcomeWithOpenIssues);
        Assert.Contains(revalidation.BranchOutcomes, outcome =>
            outcome.Key == "quality-repair-no-go" && outcome.AllowsCompletedOutcomeWithOpenIssues);
        Assert.True(noGo.AllowsCompletedOutcomeWithOpenIssues);
        Assert.Contains("read representative current product source", diagnosis.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("visible UI error", diagnosis.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read representative current product source", bughunt.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read the diagnosed current product source", repair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read the diagnosed current product source", secondRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(bughunt.Dependencies, dependency =>
            dependency.DependsOnStepKey == validation.Key &&
            dependency.DependsOnBranchOutcomeKey == "bughunt-required");
        Assert.Contains(noGo.Dependencies, dependency =>
            dependency.DependsOnStepKey == revalidation.Key &&
            dependency.DependsOnBranchOutcomeKey == "quality-repair-no-go");

        var validationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-quality-repair",
            "steps",
            "validate-quality-repair.md"));
        Assert.Contains("known failed proof", validationDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not residual risk", validationDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact failing evidence", validationDoc, StringComparison.OrdinalIgnoreCase);

        var repairDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-quality-repair",
            "steps",
            "implement-quality-repair.md"));
        var bughuntRepairDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-quality-repair",
            "steps",
            "implement-bughunt-repair.md"));
        Assert.DoesNotContain("DotNetScaffoldRepairExecutionPlan", repairDoc, StringComparison.Ordinal);
        Assert.Contains("successful product-target mutation receipt", repairDoc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DotNetScaffoldRepairExecutionPlan", bughuntRepairDoc, StringComparison.Ordinal);
        Assert.Contains("successful product-target mutation receipt", bughuntRepairDoc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Quality_delivery_templates_own_completion_policy_without_workbench_named_workflow_builder()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));

        var qualityRepair = loader.LoadDefinition("dotnet-quality-repair");
        var diagnosis = Assert.Single(qualityRepair.Steps, step => step.Key == "diagnose-quality-failure");
        var repair = Assert.Single(qualityRepair.Steps, step => step.Key == "implement-quality-repair");
        var validation = Assert.Single(qualityRepair.Steps, step => step.Key == "validate-quality-repair");
        var bughuntRepair = Assert.Single(qualityRepair.Steps, step => step.Key == "implement-bughunt-repair");
        var revalidation = Assert.Single(qualityRepair.Steps, step => step.Key == "revalidate-bughunt-repair");
        var diagnosisPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(diagnosis.CompletionPolicy);
        var repairPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(repair.CompletionPolicy);
        var validationPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(validation.CompletionPolicy);
        var bughuntRepairPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(bughuntRepair.CompletionPolicy);
        var revalidationPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(revalidation.CompletionPolicy);

        Assert.True(diagnosisPolicy.RequiresProductSourceInspection);
        Assert.False(diagnosisPolicy.RequiresProductMutationBeforeManagedOutput);
        Assert.Contains(repairPolicy.ProductMutationRequiredBranchOutcomeKeys, key => key == "product-repair-applied");
        Assert.True(repairPolicy.RequiresProductMutationBeforeManagedOutput);
        Assert.Contains(repairPolicy.ProductMutationToolNames, tool => tool == "workspace_write_file");
        Assert.Contains(repairPolicy.RuntimeRoutedBranchOutcomeKeys, key => key == "repair-attempt-incomplete");
        Assert.Contains(repairPolicy.CompletionIssueRoutes, route =>
            route.TargetBranchOutcomeKey == "repair-attempt-incomplete" &&
            route.IssueCode == "process.adapter.product_mutation_receipt_missing");
        Assert.Contains(validationPolicy.RequiredProductToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_build");
        Assert.Contains(validationPolicy.AcceptanceCriteriaRequiredBranchOutcomeKeys, key => key == "quality-repair-accepted");
        Assert.DoesNotContain(validationPolicy.RequiredProductToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_run");
        Assert.Contains(validationPolicy.CompletionIssueRoutes, route => route.TargetBranchOutcomeKey == "bughunt-required");
        Assert.False(validationPolicy.RequiresProductMutationBeforeManagedOutput);
        Assert.Empty(validationPolicy.ProductMutationToolNames);
        Assert.Contains(bughuntRepairPolicy.ProductMutationRequiredBranchOutcomeKeys, key => key == "product-repair-applied");
        Assert.True(bughuntRepairPolicy.RequiresProductMutationBeforeManagedOutput);
        Assert.Contains(bughuntRepairPolicy.ProductMutationToolNames, tool => tool == "workspace_write_file");
        Assert.Contains(revalidationPolicy.CompletionIssueRoutes, route => route.TargetBranchOutcomeKey == "quality-repair-no-go");

        var softwareDelivery = loader.LoadDefinition("software-delivery");
        var peerReview = Assert.Single(softwareDelivery.Steps, step => step.Key == "peer-review");
        var qaValidation = Assert.Single(softwareDelivery.Steps, step => step.Key == "qa-validation");
        var qaRecheck = Assert.Single(softwareDelivery.Steps, step => step.Key == "qa-recheck");
        var peerReviewPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(peerReview.CompletionPolicy);
        var qaValidationPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(qaValidation.CompletionPolicy);
        var qaRecheckPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(qaRecheck.CompletionPolicy);

        Assert.True(peerReviewPolicy.RequiresProductSourceInspection);
        Assert.Contains(qaValidationPolicy.CompletionIssueRoutes, route => route.TargetBranchOutcomeKey == "repair-required");
        Assert.Contains(qaRecheckPolicy.CompletionIssueRoutes, route => route.TargetBranchOutcomeKey == "repair-escalation");
        Assert.Contains(qaValidationPolicy.AcceptanceCriteriaRequiredBranchOutcomeKeys, key => key == "quality-accepted");
        Assert.Contains(qaRecheckPolicy.AcceptanceCriteriaRequiredBranchOutcomeKeys, key => key == "quality-accepted");

        foreach (var definitionKey in new[]
                 {
                     "blazor-app-delivery",
                     "blazor-app-repair-fix",
                     "blazor-backend-feature",
                     "blazor-frontend-feature",
                     "blazor-fullstack-feature"
                 })
        {
            var blazorDefinition = loader.LoadDefinition(definitionKey);
            var blazorValidation = Assert.Single(blazorDefinition.Steps, step => step.Key == "validate-blazor-runtime");
            var blazorRepair = Assert.Single(blazorDefinition.Steps, step => step.Key == "repair-blazor-findings");
            var blazorRevalidation = Assert.Single(blazorDefinition.Steps, step => step.Key == "revalidate-blazor-repair");
            var blazorValidationPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(blazorValidation.CompletionPolicy);
            var blazorRepairPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(blazorRepair.CompletionPolicy);
            var blazorRevalidationPolicy = Assert.IsType<ProcessTemplateStepCompletionPolicyDocument>(blazorRevalidation.CompletionPolicy);

            Assert.Contains(blazorValidationPolicy.RequiredProductToolReceipts, receipt => receipt.ToolName == "browser interaction proof");
            Assert.Contains(blazorValidationPolicy.CompletionIssueRoutes, route => route.TargetBranchOutcomeKey == "repair-required");
            Assert.Contains(blazorValidationPolicy.AcceptanceCriteriaRequiredBranchOutcomeKeys, key => key == "quality-accepted");
            Assert.Contains(blazorRepairPolicy.RequiredProductToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_test");
            Assert.Contains(blazorRevalidationPolicy.CompletionIssueRoutes, route => route.TargetBranchOutcomeKey == "repair-escalation");
        }

        Assert.False(File.Exists(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "DotNetDeliveryQualityLaunchPolicyBuilder.cs")));
    }

    [Fact]
    public void Software_delivery_parent_subprocess_steps_only_launch_and_observe_children()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("software-delivery");

        var subprocessSteps = new[]
        {
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "architecture-review", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "implementation", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "quality-repair", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "capture-ui-screenshots", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "capture-ui-screenshots-after-repair", StringComparison.Ordinal))
        };

        foreach (var step in subprocessSteps)
        {
            Assert.Equal(ProcessOperationContractNames.ExternalActionControlled, step.OperationTargetScope);
            Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, step.AllowedOperations);
            Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, step.AllowedOperations);
            Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, step.AllowedOperations);
            Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, step.AllowedOperations);
        }
    }

    [Fact]
    public void Dotnet_ui_screenshot_writeback_capture_step_can_launch_and_capture_browser_proof()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-ui-screenshot-writeback");

        var applicabilityStep = Assert.Single(definition.Steps, step => string.Equals(step.Key, "resolve-ui-screenshot-applicability", StringComparison.Ordinal));
        var step = Assert.Single(definition.Steps, step => string.Equals(step.Key, "capture-ui-screenshots", StringComparison.Ordinal));
        var captureDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-ui-screenshot-writeback",
            "steps",
            "capture-ui-screenshots.md"));

        Assert.Equal(ProcessOperationContractNames.ExternalProductTargetReadOnly, step.OperationTargetScope);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, step.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.LaunchRuntime, step.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.CaptureRuntimeProof, step.AllowedOperations);
        Assert.Contains("launch-required manifest", applicabilityStep.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime-forwarded typed bootstrap context", step.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not construct a conventional solution, project, or directory path", step.Notes, StringComparison.Ordinal);
        Assert.Contains("Missing base URL or missing Run app node is not a successful capture result", captureDoc, StringComparison.Ordinal);
        Assert.Contains("required receipts only for the `ui-capture-complete` outcome", captureDoc, StringComparison.Ordinal);
        Assert.Contains("Do not return `Blocked` only because the base URL is absent", captureDoc, StringComparison.Ordinal);
        Assert.Contains("Do not write `Status: Completed`", captureDoc, StringComparison.Ordinal);
        Assert.Contains("visible defect is evidence for QA", step.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a capture blocker", captureDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("materially inconsistent with a named source visual target", captureDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DotNetAppProjectFileAlias", captureDoc, StringComparison.Ordinal);
        Assert.Contains(".csproj", captureDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_run requires a declared runnable project file", step.Notes, StringComparison.Ordinal);
        Assert.Contains("current-run browser navigation, snapshot, screenshot, console, and cleanup receipts", step.Notes, StringComparison.Ordinal);
        Assert.Contains("browser_take_screenshot", step.EvidenceContractSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_ui_screenshot_writeback_store_step_requires_project_structure_asset_writeback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-ui-screenshot-writeback");

        var step = Assert.Single(definition.Steps, step => string.Equals(step.Key, "store-ui-screenshots", StringComparison.Ordinal));
        var storeDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-ui-screenshot-writeback",
            "steps",
            "store-ui-screenshots.md"));

        Assert.Equal(ProcessOperationContractNames.ExternalActionControlled, step.OperationTargetScope);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, step.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, step.AllowedOperations);
        Assert.Contains("project_structure_node_create", step.Notes, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", step.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_inspect_image", step.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", step.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", step.Notes, StringComparison.Ordinal);
        Assert.Contains("Visual target comparison", step.Notes, StringComparison.Ordinal);
        Assert.Contains("required runtime tool receipts only for ui-evidence-stored", step.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not infer these tools are unavailable from memory", step.Notes, StringComparison.Ordinal);
        Assert.Contains("sourceWorkspacePath", step.Notes, StringComparison.Ordinal);
        Assert.Contains("sourceFileName", step.Notes, StringComparison.Ordinal);
        Assert.Contains("sourceContentType", step.Notes, StringComparison.Ordinal);
        Assert.Contains("invalid base64", step.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not write Status: Completed", step.Notes, StringComparison.Ordinal);
        Assert.Contains("only a managed receipt", step.Notes, StringComparison.Ordinal);
        Assert.Contains("return Blocked", step.ExceptionPolicySummary, StringComparison.Ordinal);
        Assert.Contains("project_structure_node_create", step.EvidenceContractSummary, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", step.EvidenceContractSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_inspect_image", step.EvidenceContractSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", step.EvidenceContractSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", step.EvidenceContractSummary, StringComparison.Ordinal);
        Assert.Contains("Visual target comparison", step.EvidenceContractSummary, StringComparison.Ordinal);
        Assert.Contains("call `project_structure_node_create`", storeDoc, StringComparison.Ordinal);
        Assert.Contains("call `project_structure_asset_create`", storeDoc, StringComparison.Ordinal);
        Assert.Contains("workspace_inspect_image", storeDoc, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image", storeDoc, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_images", storeDoc, StringComparison.Ordinal);
        Assert.Contains("Visual target comparison", storeDoc, StringComparison.Ordinal);
        Assert.Contains("required receipts only for `ui-evidence-stored`", storeDoc, StringComparison.Ordinal);
        Assert.Contains("do not infer the writeback tools are unavailable", storeDoc, StringComparison.Ordinal);
        Assert.Contains("Do not write `Status: Completed`", storeDoc, StringComparison.Ordinal);
        Assert.Contains("sourceWorkspacePath", storeDoc, StringComparison.Ordinal);
        Assert.Contains("invalid base64", storeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("return `Blocked`", storeDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Screenshot_visual_defects_are_completed_evidence_for_qa_not_child_no_go()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var screenshotDefinition = loader.LoadDefinition("dotnet-ui-screenshot-writeback");
        var deliveryDefinition = loader.LoadDefinition("software-delivery");

        var assessment = Assert.Single(
            screenshotDefinition.Steps,
            step => string.Equals(step.Key, "assess-ui-screenshot-evidence", StringComparison.Ordinal));
        var handoff = Assert.Single(
            screenshotDefinition.Steps,
            step => string.Equals(step.Key, "screenshot-handoff", StringComparison.Ordinal));
        var assessmentContract = Assert.IsType<ProcessTemplateStepExecutionContractDocument>(assessment.ExecutionContract);

        Assert.Equal("Decision", assessment.StepKind);
        Assert.Equal(ProcessTemplateStepExecutionClasses.BranchDecision, assessment.ExecutionClass);
        Assert.Contains(
            assessment.BranchOutcomes,
            outcome => outcome.Key == "visual-accepted");
        Assert.Contains(
            assessment.BranchOutcomes,
            outcome => outcome.Key == "visual-defect-observed" && outcome.AllowsCompletedOutcomeWithOpenIssues);
        Assert.Contains(
            handoff.BranchOutcomes,
            outcome => outcome.Key == "visual-defect-observed" && outcome.AllowsCompletedOutcomeWithOpenIssues);
        Assert.Contains(
            assessment.BranchOutcomes,
            outcome => outcome.Key == "no-ui-evidence-recorded" &&
                       outcome.Description.Contains("inapplicable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            handoff.BranchOutcomes,
            outcome => outcome.Key == "no-ui-evidence-recorded" &&
                       outcome.Description.Contains("inapplicable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(assessment.ArtifactInputs, input =>
            input.SourceStepKey == "store-ui-screenshots" &&
            input.ArtifactExpectationKey == "screenshots-node-storage-receipts");
        Assert.Contains(handoff.ArtifactInputs, input =>
            input.SourceStepKey == "assess-ui-screenshot-evidence" &&
            input.ArtifactExpectationKey == "ui-screenshot-evidence-disposition");
        Assert.Empty(assessmentContract.RequiredRuntimeToolNames);
        Assert.Contains(
            assessment.CapabilityScope.RequiredReceipts,
            receipt => string.Equals(receipt.ToolName, "workspace_analyze_image", StringComparison.Ordinal) &&
                       receipt.ApplicableBranchOutcomeKeys.Contains("visual-accepted", StringComparer.OrdinalIgnoreCase) &&
                       receipt.ApplicableBranchOutcomeKeys.Contains("visual-defect-observed", StringComparer.OrdinalIgnoreCase) &&
                       !receipt.ApplicableBranchOutcomeKeys.Contains("no-ui-evidence-recorded", StringComparer.OrdinalIgnoreCase));

        var captureEvidence = Assert.Single(
            screenshotDefinition.Steps,
            step => string.Equals(step.Key, "capture-ui-screenshots", StringComparison.Ordinal));
        var storageEvidence = Assert.Single(
            screenshotDefinition.Steps,
            step => string.Equals(step.Key, "store-ui-screenshots", StringComparison.Ordinal));

        Assert.Contains(captureEvidence.BranchOutcomes, outcome => outcome.Key == "ui-capture-complete");
        Assert.Contains(captureEvidence.BranchOutcomes, outcome => outcome.Key == "no-ui-evidence-recorded");
        Assert.Contains(storageEvidence.BranchOutcomes, outcome => outcome.Key == "ui-evidence-stored");
        Assert.Contains(storageEvidence.BranchOutcomes, outcome => outcome.Key == "no-ui-evidence-recorded");
        Assert.All(
            captureEvidence.CapabilityScope.RequiredReceipts,
            receipt => Assert.DoesNotContain(
                "no-ui-evidence-recorded",
                receipt.ApplicableBranchOutcomeKeys,
                StringComparer.OrdinalIgnoreCase));
        Assert.All(
            storageEvidence.CapabilityScope.RequiredReceipts,
            receipt => Assert.DoesNotContain(
                "no-ui-evidence-recorded",
                receipt.ApplicableBranchOutcomeKeys,
                StringComparer.OrdinalIgnoreCase));

        var initialCapture = Assert.Single(
            deliveryDefinition.Steps,
            step => string.Equals(step.Key, "capture-ui-screenshots", StringComparison.Ordinal));
        var repairedCapture = Assert.Single(
            deliveryDefinition.Steps,
            step => string.Equals(step.Key, "capture-ui-screenshots-after-repair", StringComparison.Ordinal));
        var qaValidation = Assert.Single(
            deliveryDefinition.Steps,
            step => string.Equals(step.Key, "qa-validation", StringComparison.Ordinal));
        var qaRecheck = Assert.Single(
            deliveryDefinition.Steps,
            step => string.Equals(step.Key, "qa-recheck", StringComparison.Ordinal));

        foreach (var capture in new[] { initialCapture, repairedCapture })
        {
            var contract = Assert.IsType<ProcessSubprocessContract>(capture.SubprocessContract);

            Assert.Empty(contract.NoGoChildOutputs);
            Assert.Empty(contract.RequiredChildReceipts);
            Assert.Contains(contract.AcceptedChildOutputs, output =>
                output.StepKey == "screenshot-handoff" &&
                output.ArtifactExpectationKey == "ui-screenshot-writeback-handoff" &&
                output.BranchOutcomeKey == "visual-accepted" &&
                string.IsNullOrWhiteSpace(output.ParentBranchOutcomeKey));
            Assert.Contains(contract.AcceptedChildOutputs, output =>
                output.StepKey == "screenshot-handoff" &&
                output.ArtifactExpectationKey == "ui-screenshot-writeback-handoff" &&
                output.BranchOutcomeKey == "visual-defect-observed" &&
                string.IsNullOrWhiteSpace(output.ParentBranchOutcomeKey));
            Assert.Contains(contract.AcceptedChildOutputs, output =>
                output.StepKey == "screenshot-handoff" &&
                output.ArtifactExpectationKey == "ui-screenshot-writeback-handoff" &&
                output.BranchOutcomeKey == "no-ui-evidence-recorded" &&
                string.IsNullOrWhiteSpace(output.ParentBranchOutcomeKey));
        }

        Assert.Contains(initialCapture.Dependencies, dependency => dependency.DependsOnStepKey == "implementation");
        Assert.Contains(initialCapture.Dependencies, dependency => dependency.DependsOnStepKey == "architecture-review");
        Assert.Contains(initialCapture.Dependencies, dependency => dependency.DependsOnStepKey == "peer-review");
        Assert.DoesNotContain(initialCapture.Dependencies, dependency => dependency.DependsOnStepKey == "qa-validation");
        Assert.DoesNotContain(initialCapture.Dependencies, dependency => dependency.DependsOnStepKey == "record-runtime-commands");
        Assert.Contains(repairedCapture.Dependencies, dependency => dependency.DependsOnStepKey == "quality-repair");
        Assert.Contains(repairedCapture.Dependencies, dependency =>
            dependency.DependsOnStepKey == "qa-validation" && dependency.DependsOnBranchOutcomeKey == "repair-required");
        Assert.DoesNotContain(repairedCapture.Dependencies, dependency => dependency.DependsOnStepKey == "qa-recheck");
        Assert.DoesNotContain(repairedCapture.Dependencies, dependency => dependency.DependsOnStepKey == "record-runtime-commands-after-repair");
        Assert.Contains(qaValidation.Dependencies, dependency => dependency.DependsOnStepKey == "capture-ui-screenshots");
        Assert.Contains(qaRecheck.Dependencies, dependency => dependency.DependsOnStepKey == "capture-ui-screenshots-after-repair");
        Assert.Contains(qaValidation.ArtifactInputs, input =>
            input.SourceStepKey == "capture-ui-screenshots" && input.ArtifactExpectationKey == "ui-screenshot-writeback");
        Assert.Contains(qaRecheck.ArtifactInputs, input =>
            input.SourceStepKey == "capture-ui-screenshots-after-repair" &&
            input.ArtifactExpectationKey == "ui-screenshot-writeback-after-repair");
        Assert.Contains(
            qaValidation.CapabilityScope.InstructionFragments,
            fragment => fragment.Content.Contains("visual-defect-observed", StringComparison.Ordinal) &&
                        fragment.Content.Contains("repair-required", StringComparison.Ordinal) &&
                        fragment.Content.Contains("no-ui-evidence-recorded", StringComparison.Ordinal));
        Assert.Contains(
            qaRecheck.CapabilityScope.InstructionFragments,
            fragment => fragment.Content.Contains("visual-defect-observed", StringComparison.Ordinal) &&
                        fragment.Content.Contains("repair-escalation", StringComparison.Ordinal) &&
                        fragment.Content.Contains("no-ui-evidence-recorded", StringComparison.Ordinal));

        var qaValidationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "software-delivery",
            "steps",
            "qa-validation.md"));
        var qaRecheckDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "software-delivery",
            "steps",
            "qa-recheck.md"));

        Assert.Contains("visual-defect-observed", qaValidationDoc, StringComparison.Ordinal);
        Assert.Contains("repair-required", qaValidationDoc, StringComparison.Ordinal);
        Assert.Contains("no-ui-evidence-recorded", qaValidationDoc, StringComparison.Ordinal);
        Assert.Contains("visual-defect-observed", qaRecheckDoc, StringComparison.Ordinal);
        Assert.Contains("repair-escalation", qaRecheckDoc, StringComparison.Ordinal);
        Assert.Contains("no-ui-evidence-recorded", qaRecheckDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Software_delivery_visual_quality_routes_image_analysis_to_runtime_proof_steps_without_domain_coupling()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("software-delivery");

        static string BuildStepContract(ProcessTemplateDefinitionStepDocument step)
        {
            var contractParts = new[]
            {
                step.Notes,
                step.InputContractSummary,
                step.OutputContractSummary,
                step.EvidenceContractSummary
            }.Concat(step.ArtifactExpectations.Select(expectation => expectation.ValidationRequirementSummary));

            return string.Join(Environment.NewLine, contractParts);
        }

        var visualAnalysisSteps = new[]
        {
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "capture-ui-screenshots", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "capture-ui-screenshots-after-repair", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "release-approval", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "release-approval-after-repair", StringComparison.Ordinal))
        };

        foreach (var step in visualAnalysisSteps)
        {
            var stepContract = BuildStepContract(step);

            Assert.Contains("workspace_analyze_image", stepContract, StringComparison.Ordinal);
            Assert.Contains("provider-backed image-analysis", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tetris", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tetromino", stepContract, StringComparison.OrdinalIgnoreCase);
        }

        var qaSteps = new[]
        {
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "qa-validation", StringComparison.Ordinal)),
            Assert.Single(definition.Steps, step => string.Equals(step.Key, "qa-recheck", StringComparison.Ordinal))
        };

        foreach (var step in qaSteps)
        {
            var stepContract = BuildStepContract(step);

            Assert.Contains(ProcessOperationContractNames.LaunchRuntime, step.AllowedOperations);
            Assert.Contains(ProcessOperationContractNames.CaptureRuntimeProof, step.AllowedOperations);
            Assert.Contains("workspace_dotnet_run", stepContract, StringComparison.Ordinal);
            Assert.Contains("workspace_analyze_image", stepContract, StringComparison.Ordinal);
            Assert.Contains("workspace_analyze_images", stepContract, StringComparison.Ordinal);
            Assert.Contains("runtime-forwarded typed child context", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("without constructing conventional names or layouts", stepContract, StringComparison.OrdinalIgnoreCase);
        }

        var stepDocs = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(repositoryRoot, "Templates", "Processes", "processes", "software-delivery", "steps", "qa-validation.md")),
            File.ReadAllText(Path.Combine(repositoryRoot, "Templates", "Processes", "processes", "software-delivery", "steps", "qa-recheck.md")),
            File.ReadAllText(Path.Combine(repositoryRoot, "Templates", "Processes", "processes", "software-delivery", "steps", "capture-ui-screenshots.md")),
            File.ReadAllText(Path.Combine(repositoryRoot, "Templates", "Processes", "processes", "software-delivery", "steps", "capture-ui-screenshots-after-repair.md")),
            File.ReadAllText(Path.Combine(repositoryRoot, "Templates", "Processes", "processes", "software-delivery", "steps", "release-approval.md")),
            File.ReadAllText(Path.Combine(repositoryRoot, "Templates", "Processes", "processes", "software-delivery", "steps", "release-approval-after-repair.md")));

        Assert.Contains("workspace_analyze_images", stepDocs, StringComparison.Ordinal);
        Assert.Contains("Visual target comparison", stepDocs, StringComparison.Ordinal);
        Assert.Contains("source ImageAsset", stepDocs, StringComparison.Ordinal);
        Assert.Contains("media path", stepDocs, StringComparison.Ordinal);
        Assert.Contains("screenshot ref", stepDocs, StringComparison.Ordinal);
        Assert.Contains("stock scaffold UI", stepDocs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing interaction proof", stepDocs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact current-run receipt refs", stepDocs, StringComparison.Ordinal);
        Assert.Contains("same `workspace_dotnet_run` startup receipt", stepDocs, StringComparison.Ordinal);
        Assert.Contains("current-run lifecycle gate", stepDocs, StringComparison.Ordinal);
        Assert.Contains("do not write native absolute product paths", stepDocs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tetris", stepDocs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tetromino", stepDocs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Software_delivery_peer_review_can_run_read_only_validation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("software-delivery");

        var peerReview = Assert.Single(definition.Steps, step => string.Equals(step.Key, "peer-review", StringComparison.Ordinal));
        var peerReviewDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "software-delivery",
            "steps",
            "peer-review.md"));

        Assert.Equal(ProcessOperationContractNames.ExternalProductTargetReadOnly, peerReview.OperationTargetScope);
        Assert.Equal("review-lead", peerReview.RoleAssignments[0].RoleKey);
        Assert.Contains(definition.RoleUsages, role =>
            string.Equals(role.Key, "review-lead", StringComparison.Ordinal) &&
            string.Equals(role.RoleResourceKey, "review-lead", StringComparison.Ordinal));
        Assert.Contains(ProcessOperationContractNames.RunValidation, peerReview.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, peerReview.AllowedOperations);
        Assert.Contains("current-run tool receipt refs", peerReview.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("final evidenceRefs", peerReview.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not put native absolute product paths", peerReview.Notes, StringComparison.Ordinal);
        Assert.Contains("ungrounded external-target child paths", peerReview.Notes, StringComparison.Ordinal);
        Assert.Contains("peer-review managed artifact ref", peerReview.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact current-run receipt refs", peerReview.ArtifactExpectations[0].ValidationRequirementSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current-run tool receipt refs", peerReviewDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not put native absolute product paths", peerReviewDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_architecture_review_accepts_project_structure_scope_evidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-architecture-design-review");

        var draft = Assert.Single(definition.Steps, step => string.Equals(step.Key, "draft-architecture-design", StringComparison.Ordinal));
        var validationPlan = Assert.Single(definition.Steps, step => string.Equals(step.Key, "design-validation-plan", StringComparison.Ordinal));
        var review = Assert.Single(definition.Steps, step => string.Equals(step.Key, "review-architecture-design", StringComparison.Ordinal));
        var handoff = Assert.Single(definition.Steps, step => string.Equals(step.Key, "architecture-handoff", StringComparison.Ordinal));
        var validationPlanDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-architecture-design-review",
            "steps",
            "design-validation-plan.md"));
        var reviewDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-architecture-design-review",
            "steps",
            "review-architecture-design.md"));

        Assert.Contains("ProjectStructureContextSummary", draft.Notes, StringComparison.Ordinal);
        AssertResponsibleRole(validationPlan, "qa-reviewer");
        Assert.Contains(validationPlan.ArtifactExpectations, artifact =>
            string.Equals(artifact.Key, "acceptance-validation-plan", StringComparison.Ordinal));
        Assert.Contains("criterion-to-proof matrix", validationPlan.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browser or visual proof only", validationPlanDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("available scope/acceptance evidence", review.InputContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(review.ArtifactInputs, artifact =>
            string.Equals(artifact.SourceStepKey, "design-validation-plan", StringComparison.Ordinal) &&
            string.Equals(artifact.ArtifactExpectationKey, "acceptance-validation-plan", StringComparison.Ordinal));
        Assert.Contains(handoff.ArtifactInputs, artifact =>
            string.Equals(artifact.SourceStepKey, "design-validation-plan", StringComparison.Ordinal) &&
            string.Equals(artifact.ArtifactExpectationKey, "acceptance-validation-plan", StringComparison.Ordinal));
        Assert.Contains("Missing standalone acceptance/user-story files are not a hard block", review.DecisionRightsSummary, StringComparison.Ordinal);
        Assert.Contains("Do not block solely because a separate acceptance-criteria or user-story artifact is absent", review.ExceptionPolicySummary, StringComparison.Ordinal);
        Assert.Contains("Do not hard-block solely because a standalone acceptance-criteria or user-story file is absent", reviewDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void Critical_agent_steps_attach_execution_guidance_and_qa_supports_planning_mode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var expectedGuidance = new[]
        {
            ("dotnet-architecture-design-review", "classify-dotnet-application", "processes/dotnet-architecture-design-review/steps/classify-dotnet-application.md"),
            ("dotnet-architecture-design-review", "draft-architecture-design", "processes/dotnet-architecture-design-review/steps/draft-architecture-design.md"),
            ("dotnet-architecture-design-review", "design-validation-plan", "processes/dotnet-architecture-design-review/steps/design-validation-plan.md"),
            ("dotnet-architecture-design-review", "review-architecture-design", "processes/dotnet-architecture-design-review/steps/review-architecture-design.md"),
            ("dotnet-architecture-design-review", "architecture-handoff", "processes/dotnet-architecture-design-review/steps/architecture-handoff.md"),
            ("dotnet-feature-function-implementation", "test-contract", "processes/dotnet-feature-function-implementation/steps/test-contract.md"),
            ("dotnet-feature-function-implementation", "code-change", "processes/dotnet-feature-function-implementation/steps/code-change.md"),
            ("dotnet-feature-function-implementation", "targeted-validation", "processes/dotnet-feature-function-implementation/steps/targeted-validation.md"),
            ("dotnet-feature-function-implementation", "feature-repair", "processes/dotnet-feature-function-implementation/steps/feature-repair.md"),
            ("dotnet-feature-function-implementation", "targeted-recheck", "processes/dotnet-feature-function-implementation/steps/targeted-recheck.md"),
            ("dotnet-quality-repair", "revalidate-bughunt-repair", "processes/dotnet-quality-repair/steps/revalidate-bughunt-repair.md"),
            ("dotnet-quality-repair", "quality-repair-no-go", "processes/dotnet-quality-repair/steps/quality-repair-no-go.md"),
            ("software-delivery", "peer-review", "processes/software-delivery/steps/peer-review.md"),
            ("software-delivery", "repair-escalation", "processes/software-delivery/steps/repair-escalation.md")
        };

        foreach (var (definitionKey, stepKey, guidanceReference) in expectedGuidance)
        {
            var definition = loader.LoadDefinition(definitionKey);
            var step = Assert.Single(definition.Steps, candidate => string.Equals(candidate.Key, stepKey, StringComparison.Ordinal));
            var configuredReference = Assert.Single(step.ExecutionGuidanceRefs);
            var resolvedGuidance = Assert.Single(step.ResolvedExecutionGuidance);

            Assert.Equal(guidanceReference, configuredReference);
            Assert.Equal(guidanceReference, resolvedGuidance.Reference);
            Assert.StartsWith("sha256:", resolvedGuidance.ContentHash, StringComparison.Ordinal);
        }

        var qaInstructions = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Agents",
            "teams",
            "dotnet-delivery",
            "members",
            "dotnet-qa-review-lead",
            "instructions.md"));

        Assert.Contains("read-only architecture or validation-plan step", qaInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not require a generated host", qaInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void E2e_delivery_dependency_closure_direct_agent_steps_resolve_their_document_guidance()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definitionKeys = new[]
        {
            "software-delivery",
            "dotnet-development-slice",
            "dotnet-solution-setup",
            "dotnet-feature-function-implementation",
            "dotnet-quality-repair",
            "dotnet-runtime-command-writeback",
            "dotnet-ui-screenshot-writeback",
            "dotnet-architecture-design-review"
        };

        foreach (var definitionKey in definitionKeys)
        {
            var definition = loader.LoadDefinition(definitionKey);
            var definitionPath = Path.Combine(
                repositoryRoot,
                "Templates",
                "Processes",
                "processes",
                definitionKey,
                "definition.json");
            using var rawDefinition = JsonDocument.Parse(File.ReadAllText(definitionPath));
            foreach (var rawStep in rawDefinition.RootElement.GetProperty("Steps").EnumerateArray())
            {
                var executionClass = rawStep.TryGetProperty("ExecutionClass", out var executionClassElement)
                    ? executionClassElement.GetString()
                    : string.Empty;
                var documentReferences = ReadStringArray(rawStep, "DocRefs");
                if ((executionClass ?? string.Empty).StartsWith("RuntimeOwned", StringComparison.Ordinal) ||
                    documentReferences.Count == 0)
                {
                    continue;
                }

                var stepKey = rawStep.GetProperty("Key").GetString();
                Assert.False(string.IsNullOrWhiteSpace(stepKey));
                var step = Assert.Single(definition.Steps, candidate =>
                    string.Equals(candidate.Key, stepKey, StringComparison.Ordinal));
                var configuredGuidanceReferences = ReadStringArray(rawStep, "ExecutionGuidanceRefs");

                Assert.Equal(documentReferences, configuredGuidanceReferences);
                Assert.Equal(configuredGuidanceReferences, step.ExecutionGuidanceRefs);
                Assert.Equal(
                    configuredGuidanceReferences,
                    step.ResolvedExecutionGuidance.Select(guidance => guidance.Reference).ToArray());
                Assert.All(step.ResolvedExecutionGuidance, guidance =>
                {
                    Assert.StartsWith("sha256:", guidance.ContentHash, StringComparison.Ordinal);
                    Assert.False(string.IsNullOrWhiteSpace(guidance.Content));
                });
            }
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static void AssertResponsibleRole(ProcessTemplateDefinitionStepDocument step, string expectedRoleKey)
    {
        var responsible = Assert.Single(
            step.RoleAssignments,
            assignment => string.Equals(assignment.ResponsibilityKind, "Responsible", StringComparison.Ordinal));

        Assert.Equal(expectedRoleKey, responsible.RoleKey);
    }

    private static CultureScope UseCulture(string cultureName)
        => new(cultureName);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo originalCulture;
        private readonly CultureInfo originalUICulture;

        public CultureScope(string cultureName)
        {
            originalCulture = CultureInfo.CurrentCulture;
            originalUICulture = CultureInfo.CurrentUICulture;
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public async Task Editor_projection_reads_authoring_sections_from_template_metadata()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        Assert.Equal("Architecture review", editor.Identity.Name);
        Assert.Equal("Architecture owner", editor.Identity.OwnerName);
        Assert.Equal(ProcessDefinitionCriticalityLevel.High, editor.Governance.Criticality);
        Assert.Equal(ProcessDefinitionAutonomyLevel.Guarded, editor.Governance.AutonomyLevel);
        Assert.Equal(ProcessDefinitionOperatingModeKind.GovernedLive, editor.Governance.OperatingMode);
        Assert.Equal("Manager override.", editor.Governance.ManagerOverrideSummary);
        Assert.Equal("Interface contract.", editor.Contracts.InterfaceContractSummary);
        Assert.Equal(1, editor.Simulation.StepCount);
        Assert.Equal(1, editor.Simulation.RequiredRoleCount);
        Assert.Equal(1, editor.Simulation.RequiredArtifactExpectationCount);
        Assert.False(editor.Lint.HasBlockingIssues);
    }

    [Fact]
    public async Task Role_editor_projection_reads_roles_templates_and_step_bindings()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var role = Assert.Single(editor.Roles);
        var templateAction = Assert.Single(editor.TemplateActions);
        var binding = Assert.Single(editor.StepRoleBindings);
        Assert.Equal(new ProcessDefinitionRoleKey("solution-architect"), role.RoleKey);
        Assert.Equal("Solution architect", role.DisplayName);
        Assert.Equal(ProcessDefinitionRoleExecutorKind.PersonOrAgent, role.Draft.PreferredExecutorKind);
        Assert.Equal(ProcessDefinitionRoleProjectAssignmentKind.Architect, role.Draft.PreferredProjectAssignmentRole);
        Assert.True(role.Draft.IsRequired);
        Assert.True(role.Draft.AllowsFallback);
        Assert.True(role.Draft.RequiresExplicitApproval);
        Assert.Equal("process-role-template/solution-architect", role.Draft.RoleTemplateSourceKey);
        Assert.Equal(ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate, role.Draft.OverrideStatus);
        Assert.Equal(new ProcessDefinitionRoleTemplateActionKey("role-template.solution-architect"), templateAction.ActionKey);
        Assert.Equal(ProcessDefinitionRoleExecutorKind.PersonOrAgent, templateAction.PreferredExecutorKind);
        Assert.Equal(role.RoleKey, binding.RoleKey);
        Assert.Equal(ProcessStepRoleResponsibilityKind.Approver, binding.ResponsibilityKind);
        Assert.False(editor.Lint.HasBlockingIssues);
        Assert.All(editor.Commands, command => Assert.True(command.IsEnabled));
    }

    [Fact]
    public async Task Role_editor_save_rejects_invalid_executor_or_allocation()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var invalidDraft = editor.SelectedRole!.Draft with
        {
            PreferredExecutorKind = ProcessDefinitionRoleExecutorKind.Unspecified,
            DefaultAllocationPercent = 101
        };

        var result = await service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            ProcessDefinitionRoleCommandKind.SaveRole,
            editor.VersionToken,
            invalidDraft,
            TemplateActionKey: null));

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, result.Receipt.Status);
        Assert.True(result.Projection.Lint.HasBlockingIssues);
        Assert.Contains(result.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.execution.executor-required");
        Assert.Contains(result.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.execution.allocation-out-of-range");
    }

    [Fact]
    public async Task Role_editor_add_apply_save_and_delete_follow_typed_command_boundary()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var templateActionKey = editor.TemplateActions.Single().ActionKey;

        var added = await ExecuteRoleCommandAsync(
            service,
            editor,
            ProcessDefinitionRoleCommandKind.AddRole,
            editor.SelectedRole!.Draft,
            templateActionKey);
        var savedDraft = added.Projection.SelectedRole!.Draft with
        {
            DisplayName = "Principal architecture steward",
            PreferredProjectAssignmentRole = ProcessDefinitionRoleProjectAssignmentKind.Manager
        };
        var saved = await ExecuteRoleCommandAsync(
            service,
            added.Projection,
            ProcessDefinitionRoleCommandKind.SaveRole,
            savedDraft,
            templateActionKey: null);
        var applied = await ExecuteRoleCommandAsync(
            service,
            saved.Projection,
            ProcessDefinitionRoleCommandKind.ApplyTemplate,
            saved.Projection.SelectedRole!.Draft,
            templateActionKey);
        var deleted = await ExecuteRoleCommandAsync(
            service,
            applied.Projection,
            ProcessDefinitionRoleCommandKind.DeleteRole,
            applied.Projection.SelectedRole!.Draft,
            templateActionKey: null);

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, added.Receipt.Status);
        Assert.Equal(2, added.Projection.Roles.Count);
        Assert.Equal("Principal architecture steward", saved.Projection.SelectedRole?.DisplayName);
        Assert.Equal(ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate, applied.Projection.SelectedRole?.Draft.OverrideStatus);
        Assert.DoesNotContain(deleted.Projection.Roles, role => role.RoleKey == applied.Projection.SelectedRole!.RoleKey);
    }

    [Fact]
    public async Task Role_editor_rejects_stale_version_tokens()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionRoleEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var templateActionKey = editor.TemplateActions.Single().ActionKey;
        var added = await ExecuteRoleCommandAsync(
            service,
            editor,
            ProcessDefinitionRoleCommandKind.AddRole,
            editor.SelectedRole!.Draft,
            templateActionKey);

        var staleAdd = await service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            ProcessDefinitionRoleCommandKind.AddRole,
            editor.VersionToken,
            editor.SelectedRole.Draft,
            templateActionKey));
        var staleSave = await service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            added.Projection.DefinitionKey,
            ProcessDefinitionRoleCommandKind.SaveRole,
            editor.VersionToken,
            added.Projection.SelectedRole!.Draft,
            TemplateActionKey: null));

        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, staleAdd.Receipt.Status);
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, staleSave.Receipt.Status);
        Assert.Contains(staleAdd.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.version-conflict");
        Assert.Contains(staleSave.Projection.Lint.Issues, issue => issue.Code == "processes.definition.role.version-conflict");
    }

    [Fact]
    public async Task Canvas_projection_reads_steps_routes_roles_artifacts_and_toolbox()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        Assert.Contains(canvas.Nodes, node => node.Kind == ProcessDefinitionCanvasNodeKind.Step && node.StepKey == new ProcessDefinitionStepKey("architecture-decision"));
        Assert.Contains(canvas.Nodes, node => node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter);
        Assert.Contains(canvas.Nodes, node => node.Kind == ProcessDefinitionCanvasNodeKind.Role && node.RoleKey == new ProcessDefinitionRoleKey("solution-architect"));
        Assert.Contains(canvas.Nodes, node => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact && node.ArtifactKey == "architecture-decision-record");
        Assert.Contains(canvas.Edges, edge => edge.Kind == ProcessDefinitionCanvasEdgeKind.BranchRoute);
        Assert.Contains(canvas.Edges, edge => edge.Kind == ProcessDefinitionCanvasEdgeKind.RoleBinding);
        Assert.Contains(canvas.Edges, edge => edge.Kind == ProcessDefinitionCanvasEdgeKind.ArtifactExpectation);
        Assert.Contains(canvas.ToolboxActions, action => action.ActionKey == new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"));
        Assert.Equal(ProcessDefinitionCanvasSelectionKind.Step, canvas.Selection.Kind);
        Assert.Contains(canvas.Commands, command => command.Kind == ProcessDefinitionCanvasCommandKind.Recompose && command.IsEnabled);
    }

    [Fact]
    public async Task Canvas_projection_prefers_canonical_branch_target_over_stale_dependency_mirror()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var definitionPath = Path.Combine(
            pack.RootPath,
            "processes",
            "architecture-review",
            "definition.json");
        var definition = JsonSerializer.Deserialize<ProcessTemplateDefinitionDocument>(
            File.ReadAllText(definitionPath));
        Assert.NotNull(definition);
        var decision = Assert.Single(definition.Steps);
        var outcome = Assert.Single(decision.BranchOutcomes);
        outcome.RouteTargetKind = nameof(ProcessDefinitionRouteTargetKind.SpecificStep);
        outcome.RouteTargetStepKey = "canonical-target";
        definition.Steps.Add(new ProcessTemplateDefinitionStepDocument
        {
            Order = 1,
            Key = "stale-target",
            Title = "Stale dependency target",
            StepKind = nameof(ProcessDefinitionStepKind.Work),
            DependsOnStepKey = decision.Key,
            DependsOnBranchOutcomeKey = outcome.Key,
            CanvasX = 680,
            CanvasY = 100
        });
        definition.Steps.Add(new ProcessTemplateDefinitionStepDocument
        {
            Order = 2,
            Key = "canonical-target",
            Title = "Canonical route target",
            StepKind = nameof(ProcessDefinitionStepKind.End),
            CanvasX = 680,
            CanvasY = 360
        });
        File.WriteAllText(definitionPath, JsonSerializer.Serialize(definition));
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var route = Assert.Single(canvas.Edges, edge =>
            edge.Kind == ProcessDefinitionCanvasEdgeKind.BranchRoute &&
            edge.FromNodeKey == new ProcessDefinitionCanvasNodeKey("branch:architecture-decision") &&
            string.Equals(edge.Label, "approved", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new ProcessDefinitionCanvasNodeKey("step:canonical-target"), route.ToNodeKey);
    }

    [Fact]
    public async Task Canvas_projection_preserves_authored_coordinates_until_explicit_recomposition()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var step = Assert.Single(canvas.Nodes, node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.Step &&
            node.StepKey == new ProcessDefinitionStepKey("architecture-decision"));
        var router = Assert.Single(canvas.Nodes, node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter);
        Assert.Equal(160d, step.X);
        Assert.Equal(220d, step.Y);
        Assert.Equal(ProcessDefinitionStepKind.Decision, step.StepKind);
        Assert.Equal(420d, router.X);
        Assert.Equal(120d, router.Y);
    }

    [Fact]
    public async Task Canvas_toolbox_commands_add_elements_and_recompose()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var addedStep = await ExecuteCanvasCommandAsync(
            service,
            canvas,
            ProcessDefinitionCanvasCommandKind.AddStep,
            new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
            canvas.Selection.NodeKey);
        var addedArtifact = await ExecuteCanvasCommandAsync(
            service,
            addedStep.Projection,
            ProcessDefinitionCanvasCommandKind.AddArtifactExpectation,
            new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-artifact-expectation"),
            addedStep.Projection.Selection.NodeKey);
        var recomposed = await ExecuteCanvasCommandAsync(
            service,
            addedArtifact.Projection,
            ProcessDefinitionCanvasCommandKind.Recompose,
            ToolboxActionKey: null,
            addedArtifact.Projection.Selection.NodeKey);

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, addedStep.Receipt.Status);
        Assert.Contains(addedStep.Projection.Nodes, node => node.Title == "Implementation");
        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, addedArtifact.Receipt.Status);
        Assert.True(addedArtifact.Projection.Nodes.Count(node => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact) >= 2);
        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, recomposed.Receipt.Status);
        Assert.Contains("recomposed", recomposed.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Canvas_automatic_addition_places_locally_without_moving_existing_nodes()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var beforePositions = canvas.Nodes.ToDictionary(node => node.NodeKey, node => (node.X, node.Y));

        var added = await ExecuteCanvasCommandAsync(
            service,
            canvas,
            ProcessDefinitionCanvasCommandKind.AddStep,
            new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
            canvas.Selection.NodeKey);

        Assert.All(canvas.Nodes, node =>
        {
            var after = Assert.Single(added.Projection.Nodes, candidate => candidate.NodeKey == node.NodeKey);
            Assert.Equal(beforePositions[node.NodeKey], (after.X, after.Y));
        });
        var newStep = Assert.Single(added.Projection.Nodes, node => node.Title == "Implementation");
        var anchor = Assert.Single(canvas.Nodes, node => node.NodeKey == canvas.Selection.NodeKey);
        Assert.NotEqual(anchor.Y, newStep.Y);
        Assert.DoesNotContain(
            canvas.Nodes,
            node => ProcessDefinitionCanvasPlacementPolicy.Intersects(
                ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(node),
                ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(newStep)));
    }

    [Fact]
    public async Task Canvas_automatic_addition_requires_an_explicit_structural_parent()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var result = await ExecuteCanvasCommandAsync(
            service,
            canvas,
            ProcessDefinitionCanvasCommandKind.AddStep,
            new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
            selectedNodeKey: null);

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Rejected, result.Receipt.Status);
        Assert.Contains("structural parent", result.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(canvas.Nodes, result.Projection.Nodes);
    }

    [Fact]
    public async Task Canvas_clone_artifact_reference_adds_same_artifact_key_without_extra_edge()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var artifact = canvas.Nodes.First(node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.Artifact &&
            node.ArtifactKey == "architecture-decision-record");

        var cloned = await ExecuteCanvasCommandAsync(
            service,
            canvas,
            ProcessDefinitionCanvasCommandKind.CloneArtifactReference,
            ToolboxActionKey: null,
            artifact.NodeKey);

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, cloned.Receipt.Status);
        Assert.Equal(canvas.Edges.Count, cloned.Projection.Edges.Count);
        var references = cloned.Projection.Nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact &&
                           node.ArtifactKey == artifact.ArtifactKey)
            .ToArray();
        Assert.Equal(2, references.Length);
        Assert.Contains(references, node => node.NodeKey != artifact.NodeKey && node.StepKey is null);
        Assert.Equal(ProcessDefinitionCanvasSelectionKind.Artifact, cloned.Projection.Selection.Kind);
        Assert.Contains("shared key", cloned.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Canvas_rejects_stale_version_tokens_for_mutating_commands()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var added = await ExecuteCanvasCommandAsync(
            service,
            canvas,
            ProcessDefinitionCanvasCommandKind.AddStep,
            new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
            canvas.Selection.NodeKey);

        var stale = await service.ExecuteCommandAsync(new ProcessDefinitionCanvasCommand(
            ProcessWorkspaceShellScope.Global,
            added.Projection.DefinitionKey,
            ProcessDefinitionCanvasCommandKind.AddArtifactExpectation,
            canvas.VersionToken,
            new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-artifact-expectation"),
            added.Projection.Selection.NodeKey,
            SelectedEdgeKey: null,
            ProcessDefinitionCanvasRecompositionMode.BalancedFlow));

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Rejected, stale.Receipt.Status);
        Assert.Contains("changed before submission", stale.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Canvas_rejects_stale_recompose_to_avoid_overwriting_current_projection()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var added = await ExecuteCanvasCommandAsync(
            service,
            canvas,
            ProcessDefinitionCanvasCommandKind.AddStep,
            new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
            canvas.Selection.NodeKey);

        var stale = await service.ExecuteCommandAsync(new ProcessDefinitionCanvasCommand(
            ProcessWorkspaceShellScope.Global,
            added.Projection.DefinitionKey,
            ProcessDefinitionCanvasCommandKind.Recompose,
            canvas.VersionToken,
            ToolboxActionKey: null,
            added.Projection.Selection.NodeKey,
            SelectedEdgeKey: null,
            ProcessDefinitionCanvasRecompositionMode.BalancedFlow));

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Rejected, stale.Receipt.Status);
        Assert.Contains(stale.Projection.Nodes, node => node.Title == "Implementation");
        Assert.Contains("changed before submission", stale.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Canvas_state_preserves_moves_and_added_nodes_through_recomposition()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var loader = new ProcessTemplatePackLoader(pack.RootPath);
        var clock = new FixedProcessProjectionClock(Now);
        var service = new ProcessDefinitionCanvasEditorProjectionService(loader, clock);
        var initial = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var added = await ExecuteCanvasCommandAsync(
            service,
            initial,
            ProcessDefinitionCanvasCommandKind.AddStep,
            new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
            initial.Selection.NodeKey);
        var addedStep = Assert.Single(added.Projection.Nodes, node => node.Title == "Implementation");
        var moved = await service.ExecuteCommandAsync(new ProcessDefinitionCanvasCommand(
            ProcessWorkspaceShellScope.Global,
            added.Projection.DefinitionKey,
            ProcessDefinitionCanvasCommandKind.MoveNodes,
            added.Projection.VersionToken,
            ToolboxActionKey: null,
            addedStep.NodeKey,
            SelectedEdgeKey: null,
            ProcessDefinitionCanvasRecompositionMode.PreserveProjection,
            [new ProcessDefinitionCanvasNodePosition(addedStep.NodeKey, 1840d, 760d)]));

        var reloaded = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            initial.DefinitionKey);
        var reloadedStep = Assert.Single(reloaded.Nodes, node => node.NodeKey == addedStep.NodeKey);
        Assert.Equal((1840d, 760d), (reloadedStep.X, reloadedStep.Y));

        var recomposed = await ExecuteCanvasCommandAsync(
            service,
            reloaded,
            ProcessDefinitionCanvasCommandKind.Recompose,
            ToolboxActionKey: null,
            reloadedStep.NodeKey);

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, moved.Receipt.Status);
        Assert.Contains(recomposed.Projection.Nodes, node => node.NodeKey == addedStep.NodeKey);
    }

    [Fact]
    public async Task Canvas_move_preserves_the_command_selection_in_the_returned_projection()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var artifact = Assert.Single(canvas.Nodes, node => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact);

        var moved = await service.ExecuteCommandAsync(new ProcessDefinitionCanvasCommand(
            ProcessWorkspaceShellScope.Global,
            canvas.DefinitionKey,
            ProcessDefinitionCanvasCommandKind.MoveNodes,
            canvas.VersionToken,
            ToolboxActionKey: null,
            artifact.NodeKey,
            SelectedEdgeKey: null,
            ProcessDefinitionCanvasRecompositionMode.PreserveProjection,
            [new ProcessDefinitionCanvasNodePosition(artifact.NodeKey, 640d, 480d)]));

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, moved.Receipt.Status);
        Assert.Equal(artifact.NodeKey, moved.Projection.Selection.NodeKey);
    }

    [Fact]
    public async Task Canvas_recomposition_supports_every_default_process_template()
    {
        var loader = new ProcessTemplatePackLoader();
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            loader,
            new FixedProcessProjectionClock(Now));

        foreach (var definition in loader.Load().Definitions)
        {
            var canvas = await service.GetCanvasAsync(
                ProcessWorkspaceShellScope.Global,
                new ProcessDefinitionCatalogItemKey(definition.Key));

            var recomposed = await ExecuteCanvasCommandAsync(
                service,
                canvas,
                ProcessDefinitionCanvasCommandKind.Recompose,
                ToolboxActionKey: null,
                canvas.Selection.NodeKey);

            Assert.True(
                recomposed.Receipt.Status == ProcessDefinitionCanvasCommandStatus.Accepted,
                $"Process template '{definition.Key}' was not recomposed: {recomposed.Receipt.Summary}");
            AssertNoCanvasNodeOverlaps(recomposed.Projection.Nodes, definition.Key);
        }
    }

    [Fact]
    public async Task Canvas_recomposition_keeps_shipped_success_lane_primary_when_failure_is_typed_end()
    {
        var service = new ProcessDefinitionCanvasEditorProjectionService(
            new ProcessTemplatePackLoader(),
            new FixedProcessProjectionClock(Now));
        var canvas = await service.GetCanvasAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("branching-code-review"));

        var recomposed = await ExecuteCanvasCommandAsync(
            service,
            canvas,
            ProcessDefinitionCanvasCommandKind.Recompose,
            ToolboxActionKey: null,
            canvas.Selection.NodeKey);

        var start = Assert.Single(recomposed.Projection.Nodes, node => node.StepKind == ProcessDefinitionStepKind.Start);
        var successfulTerminal = Assert.Single(recomposed.Projection.Nodes, node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.Step &&
            node.StepKey == new ProcessDefinitionStepKey("approve-merge-after-qa"));
        var failure = Assert.Single(recomposed.Projection.Nodes, node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.Step &&
            node.StepKey == new ProcessDefinitionStepKey("capture-workflow-failure"));
        Assert.Equal(start.Y, successfulTerminal.Y);
        Assert.NotEqual(start.Y, failure.Y);
    }

    [Fact]
    public async Task Step_editor_projection_reads_operation_routes_artifacts_roles_and_subprocess_options()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionStepEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));

        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var step = editor.SelectedStep;
        Assert.NotNull(step);
        Assert.Equal(new ProcessDefinitionStepKey("architecture-decision"), step.Basic.StepKey);
        Assert.Equal(ProcessDefinitionStepKind.Decision, step.Basic.StepKind);
        Assert.Equal(12, step.Basic.TargetLeadHours);
        Assert.Equal(ProcessDefinitionStepTargetScopeKind.ExternalArtifactDestination, step.OperationContract.TargetScope);
        Assert.Contains(ProcessDefinitionStepOperationKind.WriteExternalArtifactDestination, step.OperationContract.AllowedOperations);
        var route = Assert.Single(step.BranchOutcomes);
        Assert.Equal(ProcessDefinitionRouteTargetKind.NextStep, route.RouteTarget.Kind);
        var artifact = Assert.Single(step.ArtifactExpectations);
        Assert.Equal(ProcessDefinitionArtifactKind.Deliverable, artifact.ArtifactKind);
        Assert.Equal(ProcessDefinitionArtifactTrustRequirement.ReviewRequired, artifact.TrustRequirement);
        Assert.Equal(ProcessDefinitionArtifactSensitivityLevel.Internal, artifact.SensitivityLevel);
        Assert.Equal(365, artifact.RetentionDays);
        Assert.Equal("adr-output", artifact.WorkflowOutputId);
        Assert.Single(step.RoleBindings);
        Assert.Contains(editor.SubprocessOptions, option => option.DefinitionKey == new ProcessDefinitionCatalogItemKey("delivery-default"));
        Assert.False(editor.Lint.HasBlockingIssues);
    }

    [Fact]
    public async Task Step_editor_commands_save_add_branch_artifact_and_map_subprocess()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionStepEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var savedDraft = editor.SelectedStep! with
        {
            Basic = editor.SelectedStep.Basic with
            {
                Title = "Architecture decision checkpoint"
            }
        };

        var saved = await ExecuteStepCommandAsync(
            service,
            editor,
            ProcessDefinitionStepCommandKind.SaveStep,
            savedDraft);
        var addedRoute = await ExecuteStepCommandAsync(
            service,
            saved.Projection,
            ProcessDefinitionStepCommandKind.AddBranchOutcome,
            saved.Projection.SelectedStep!);
        var addedArtifact = await ExecuteStepCommandAsync(
            service,
            addedRoute.Projection,
            ProcessDefinitionStepCommandKind.AddArtifactExpectation,
            addedRoute.Projection.SelectedStep!);
        var subprocessDraft = addedArtifact.Projection.SelectedStep! with
        {
            Basic = addedArtifact.Projection.SelectedStep.Basic with
            {
                StepKind = ProcessDefinitionStepKind.Subprocess
            },
            SubprocessMapping = addedArtifact.Projection.SelectedStep.SubprocessMapping with
            {
                ProcessKey = "delivery-default",
                DefinitionSnapshotName = "Delivery default"
            }
        };
        var mapped = await ExecuteStepCommandAsync(
            service,
            addedArtifact.Projection,
            ProcessDefinitionStepCommandKind.MapSubprocess,
            subprocessDraft);

        Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, saved.Receipt.Status);
        Assert.Equal("Architecture decision checkpoint", saved.Projection.SelectedStep?.Basic.Title);
        Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, addedRoute.Receipt.Status);
        Assert.Equal(2, addedRoute.Projection.SelectedStep?.BranchOutcomes.Count);
        Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, addedArtifact.Receipt.Status);
        Assert.Equal(2, addedArtifact.Projection.SelectedStep?.ArtifactExpectations.Count);
        Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, mapped.Receipt.Status);
        Assert.Equal("delivery-default", mapped.Projection.SelectedStep?.SubprocessMapping.ProcessKey);
    }

    [Fact]
    public async Task Step_editor_rejects_backward_route_without_loop_budget()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionStepEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var route = editor.SelectedStep!.BranchOutcomes.Single() with
        {
            RouteTarget = editor.SelectedStep.BranchOutcomes.Single().RouteTarget with
            {
                Kind = ProcessDefinitionRouteTargetKind.PreviousStep
            },
            IsBackwardRoute = true,
            LoopBudget = editor.SelectedStep.BranchOutcomes.Single().LoopBudget with
            {
                MaximumRepeats = 0
            }
        };
        var invalidDraft = editor.SelectedStep with
        {
            BranchOutcomes = [route]
        };

        var result = await ExecuteStepCommandAsync(
            service,
            editor,
            ProcessDefinitionStepCommandKind.SaveStep,
            invalidDraft);

        Assert.Equal(ProcessDefinitionStepCommandStatus.Rejected, result.Receipt.Status);
        Assert.Contains(result.Projection.Lint.Issues, issue =>
            issue.Code == "processes.definition.step.routing.backward-loop-budget-required");
    }

    [Fact]
    public async Task Step_editor_rejects_stale_version_tokens()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionStepEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var saved = await ExecuteStepCommandAsync(
            service,
            editor,
            ProcessDefinitionStepCommandKind.SaveStep,
            editor.SelectedStep!);

        var stale = await service.ExecuteCommandAsync(new ProcessDefinitionStepEditorCommand(
            ProcessWorkspaceShellScope.Global,
            saved.Projection.DefinitionKey,
            ProcessDefinitionStepCommandKind.SaveStep,
            editor.VersionToken,
            saved.Projection.SelectedStep!));

        Assert.Equal(ProcessDefinitionStepCommandStatus.Rejected, stale.Receipt.Status);
        Assert.Contains(stale.Projection.Lint.Issues, issue => issue.Code == "processes.definition.step.version-conflict");
    }

    [Fact]
    public async Task Template_catalog_projection_uses_canonical_json_and_generated_previews()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var loader = new ProcessTemplatePackLoader(pack.RootPath);
        var stepEditorService = new ProcessDefinitionStepEditorProjectionService(
            loader,
            new FixedProcessProjectionClock(Now));
        var templateService = new ProcessTemplateCatalogProjectionService(
            loader,
            new FixedProcessProjectionClock(Now));
        var definitionKey = new ProcessDefinitionCatalogItemKey("architecture-review");
        var stepEditor = await stepEditorService.GetEditorAsync(ProcessWorkspaceShellScope.Global, definitionKey);

        var catalog = await templateService.GetCatalogAsync(
            ProcessWorkspaceShellScope.Global,
            definitionKey,
            new ProcessTemplateCatalogQueryProjection(
                "architecture",
                ProcessTemplateCatalogCategoryKind.All,
                SelectedItemKey: null,
                ProcessTemplateCatalogPreviewTabKind.Json,
                Take: 25),
            stepEditor);

        Assert.Equal(definitionKey, catalog.TargetDefinitionKey);
        Assert.StartsWith("templates:architecture-review:", catalog.VersionToken.Value, StringComparison.Ordinal);
        Assert.Contains(catalog.Categories, category => category.Kind == ProcessTemplateCatalogCategoryKind.Processes && category.Count == 2);
        Assert.Contains(catalog.Categories, category => category.Kind == ProcessTemplateCatalogCategoryKind.Roles && category.Count == 2);
        Assert.Contains(catalog.Categories, category => category.Kind == ProcessTemplateCatalogCategoryKind.Artifacts && category.Count == 2);
        Assert.Contains(catalog.Items, item => item.Kind == ProcessTemplateCatalogItemKind.Process && item.SourceDefinitionKey == "architecture-review");
        Assert.Contains(catalog.Items, item => item.Kind == ProcessTemplateCatalogItemKind.Role && item.SourceComponentKey == "solution-architect");
        Assert.Contains(catalog.Items, item => item.Kind == ProcessTemplateCatalogItemKind.Artifact && item.SourceComponentKey == "architecture-decision-record");
        Assert.NotNull(catalog.Preview);
        Assert.StartsWith("sha256:", catalog.Preview!.SourceJsonHash, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"architecture-review\"", catalog.Preview.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("# Architecture review", catalog.Preview.GeneratedMarkdown, StringComparison.Ordinal);
        Assert.StartsWith("flowchart TD", catalog.Preview.GeneratedMermaid, StringComparison.Ordinal);
        Assert.Contains(catalog.Preview.Structure, node => node.Kind == ProcessTemplateStructureNodeKind.Step && node.Title == "Architecture decision");
        Assert.Contains(catalog.ImportTargets, target => target.StepKey == new ProcessDefinitionStepKey("architecture-decision"));
    }

    [Fact]
    public async Task Template_catalog_imports_process_role_and_artifact_with_target_validation()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var loader = new ProcessTemplatePackLoader(pack.RootPath);
        var stepEditorService = new ProcessDefinitionStepEditorProjectionService(
            loader,
            new FixedProcessProjectionClock(Now));
        var templateService = new ProcessTemplateCatalogProjectionService(
            loader,
            new FixedProcessProjectionClock(Now));
        var definitionKey = new ProcessDefinitionCatalogItemKey("architecture-review");
        var stepEditor = await stepEditorService.GetEditorAsync(ProcessWorkspaceShellScope.Global, definitionKey);
        var query = new ProcessTemplateCatalogQueryProjection(
            SearchText: null,
            ProcessTemplateCatalogCategoryKind.All,
            SelectedItemKey: null,
            ProcessTemplateCatalogPreviewTabKind.Overview,
            Take: 50);
        var catalog = await templateService.GetCatalogAsync(ProcessWorkspaceShellScope.Global, definitionKey, query, stepEditor);

        var processImport = await templateService.ExecuteCommandAsync(
            new ProcessTemplateImportCommand(
                ProcessWorkspaceShellScope.Global,
                definitionKey,
                ProcessTemplateImportCommandKind.ImportProcess,
                new ProcessTemplateCatalogItemKey("process:architecture-review"),
                catalog.VersionToken,
                catalog.Query,
                TargetStepKey: null),
            stepEditor);
        var roleImport = await templateService.ExecuteCommandAsync(
            new ProcessTemplateImportCommand(
                ProcessWorkspaceShellScope.Global,
                definitionKey,
                ProcessTemplateImportCommandKind.ImportRole,
                new ProcessTemplateCatalogItemKey("role:architecture-review:solution-architect"),
                processImport.Projection.VersionToken,
                processImport.Projection.Query,
                TargetStepKey: null),
            stepEditor);
        var rejectedArtifact = await templateService.ExecuteCommandAsync(
            new ProcessTemplateImportCommand(
                ProcessWorkspaceShellScope.Global,
                definitionKey,
                ProcessTemplateImportCommandKind.ImportArtifact,
                new ProcessTemplateCatalogItemKey("artifact:architecture-review:architecture-decision:architecture-decision-record"),
                roleImport.Projection.VersionToken,
                roleImport.Projection.Query,
                TargetStepKey: null),
            stepEditor);
        var artifactImport = await templateService.ExecuteCommandAsync(
            new ProcessTemplateImportCommand(
                ProcessWorkspaceShellScope.Global,
                definitionKey,
                ProcessTemplateImportCommandKind.ImportArtifact,
                new ProcessTemplateCatalogItemKey("artifact:architecture-review:architecture-decision:architecture-decision-record"),
                roleImport.Projection.VersionToken,
                roleImport.Projection.Query,
                new ProcessDefinitionStepKey("architecture-decision")),
            stepEditor);

        Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, processImport.Receipt.Status);
        Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, roleImport.Receipt.Status);
        Assert.Equal(ProcessTemplateImportCommandStatus.Rejected, rejectedArtifact.Receipt.Status);
        Assert.Contains("target step", rejectedArtifact.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, artifactImport.Receipt.Status);
        Assert.Contains(artifactImport.Projection.ImportedComponents, component => component.Kind == ProcessTemplateCatalogItemKind.Process);
        Assert.Contains(artifactImport.Projection.ImportedComponents, component => component.Kind == ProcessTemplateCatalogItemKind.Role);
        Assert.Contains(artifactImport.Projection.ImportedComponents, component =>
            component.Kind == ProcessTemplateCatalogItemKind.Artifact &&
            component.SourceDefinitionKey == "architecture-review" &&
            component.SourceComponentKey == "architecture-decision-record" &&
            component.SourceJsonHash.StartsWith("sha256:", StringComparison.Ordinal) &&
            component.TargetStepKey == new ProcessDefinitionStepKey("architecture-decision"));
    }

    [Fact]
    public async Task Template_catalog_rejects_stale_import_version_tokens()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var loader = new ProcessTemplatePackLoader(pack.RootPath);
        var stepEditorService = new ProcessDefinitionStepEditorProjectionService(
            loader,
            new FixedProcessProjectionClock(Now));
        var templateService = new ProcessTemplateCatalogProjectionService(
            loader,
            new FixedProcessProjectionClock(Now));
        var definitionKey = new ProcessDefinitionCatalogItemKey("architecture-review");
        var stepEditor = await stepEditorService.GetEditorAsync(ProcessWorkspaceShellScope.Global, definitionKey);
        var catalog = await templateService.GetCatalogAsync(
            ProcessWorkspaceShellScope.Global,
            definitionKey,
            new ProcessTemplateCatalogQueryProjection(SearchText: null, ProcessTemplateCatalogCategoryKind.All, SelectedItemKey: null, ProcessTemplateCatalogPreviewTabKind.Overview, Take: 50),
            stepEditor);
        var accepted = await templateService.ExecuteCommandAsync(
            new ProcessTemplateImportCommand(
                ProcessWorkspaceShellScope.Global,
                definitionKey,
                ProcessTemplateImportCommandKind.ImportProcess,
                new ProcessTemplateCatalogItemKey("process:architecture-review"),
                catalog.VersionToken,
                catalog.Query,
                TargetStepKey: null),
            stepEditor);

        var stale = await templateService.ExecuteCommandAsync(
            new ProcessTemplateImportCommand(
                ProcessWorkspaceShellScope.Global,
                definitionKey,
                ProcessTemplateImportCommandKind.ImportRole,
                new ProcessTemplateCatalogItemKey("role:architecture-review:solution-architect"),
                catalog.VersionToken,
                accepted.Projection.Query,
                TargetStepKey: null),
            stepEditor);

        Assert.Equal(ProcessTemplateImportCommandStatus.Rejected, stale.Receipt.Status);
        Assert.Contains("changed before submission", stale.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_rejects_blocking_lint_and_returns_actionable_projection()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var invalidDraft = CreateDraft(editor) with
        {
            Identity = editor.Identity with { Name = string.Empty }
        };

        var result = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Publish,
            editor.VersionToken,
            invalidDraft));

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, result.Receipt.Status);
        Assert.Equal(ProcessDefinitionEditorCommandKind.Publish, result.Receipt.CommandKind);
        Assert.True(result.Projection.Lint.HasBlockingIssues);
        Assert.Contains(result.Projection.Lint.Issues, issue =>
            issue.Code == "processes.definition.identity.name-required" &&
            issue.Severity == ProcessDefinitionEditorLintSeverity.Error);
        Assert.Equal(string.Empty, result.Projection.Identity.Name);
    }

    [Fact]
    public async Task Save_publish_archive_and_delete_follow_typed_status_transitions()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));

        var saved = await ExecuteEditorCommandAsync(service, editor, ProcessDefinitionEditorCommandKind.SaveDraft);
        var published = await ExecuteEditorCommandAsync(service, saved.Projection, ProcessDefinitionEditorCommandKind.Publish);
        var archived = await ExecuteEditorCommandAsync(service, published.Projection, ProcessDefinitionEditorCommandKind.Archive);
        var deleted = await ExecuteEditorCommandAsync(service, archived.Projection, ProcessDefinitionEditorCommandKind.Delete);

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, saved.Receipt.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, saved.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Published, published.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Archived, archived.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.TemplateDefault, deleted.Projection.Status);
        Assert.Contains("template default remains available", deleted.Receipt.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Archive_and_delete_reject_stale_version_tokens()
    {
        using var pack = TemporaryProcessTemplatePack.CreateDefault();
        var service = new ProcessDefinitionEditorProjectionService(
            new ProcessTemplatePackLoader(pack.RootPath),
            new FixedProcessProjectionClock(Now));
        var editor = await service.GetEditorAsync(
            ProcessWorkspaceShellScope.Global,
            new ProcessDefinitionCatalogItemKey("architecture-review"));
        var saved = await ExecuteEditorCommandAsync(service, editor, ProcessDefinitionEditorCommandKind.SaveDraft);
        var staleDraft = CreateDraft(saved.Projection);

        var archived = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            saved.Projection.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Archive,
            editor.VersionToken,
            staleDraft));
        var deleted = await service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            saved.Projection.DefinitionKey,
            ProcessDefinitionEditorCommandKind.Delete,
            editor.VersionToken,
            staleDraft));

        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, archived.Receipt.Status);
        Assert.Equal(ProcessDefinitionEditorCommandStatus.Rejected, deleted.Receipt.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, archived.Projection.Status);
        Assert.Equal(ProcessDefinitionAuthoringStatus.Draft, deleted.Projection.Status);
        Assert.Contains(archived.Projection.Lint.Issues, issue => issue.Code == "processes.definition.version-conflict");
        Assert.Contains(deleted.Projection.Lint.Issues, issue => issue.Code == "processes.definition.version-conflict");
    }

    private static Task<ProcessDefinitionEditorCommandResult> ExecuteEditorCommandAsync(
        ProcessDefinitionEditorProjectionService service,
        ProcessDefinitionEditorProjection editor,
        ProcessDefinitionEditorCommandKind commandKind)
        => service.ExecuteCommandAsync(new ProcessDefinitionEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            commandKind,
            editor.VersionToken,
            CreateDraft(editor)));

    private static Task<ProcessDefinitionRoleEditorCommandResult> ExecuteRoleCommandAsync(
        ProcessDefinitionRoleEditorProjectionService service,
        ProcessDefinitionRoleEditorProjection editor,
        ProcessDefinitionRoleCommandKind commandKind,
        ProcessDefinitionRoleDraftProjection draft,
        ProcessDefinitionRoleTemplateActionKey? templateActionKey)
        => service.ExecuteCommandAsync(new ProcessDefinitionRoleEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            commandKind,
            editor.VersionToken,
            draft,
            templateActionKey));

    private static Task<ProcessDefinitionCanvasCommandResult> ExecuteCanvasCommandAsync(
        ProcessDefinitionCanvasEditorProjectionService service,
        ProcessDefinitionCanvasEditorProjection canvas,
        ProcessDefinitionCanvasCommandKind commandKind,
        ProcessDefinitionCanvasToolboxActionKey? ToolboxActionKey,
        ProcessDefinitionCanvasNodeKey? selectedNodeKey)
        => service.ExecuteCommandAsync(new ProcessDefinitionCanvasCommand(
            ProcessWorkspaceShellScope.Global,
            canvas.DefinitionKey,
            commandKind,
            canvas.VersionToken,
            ToolboxActionKey,
            selectedNodeKey,
            SelectedEdgeKey: null,
            ProcessDefinitionCanvasRecompositionMode.BalancedFlow));

    private static void AssertNoCanvasNodeOverlaps(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        string definitionKey)
    {
        for (var leftIndex = 0; leftIndex < nodes.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < nodes.Count; rightIndex++)
            {
                var left = nodes[leftIndex];
                var right = nodes[rightIndex];
                Assert.False(
                    ProcessDefinitionCanvasPlacementPolicy.Intersects(
                        ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(left),
                        ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(right)),
                    $"Process template '{definitionKey}' has overlapping canvas nodes '{left.NodeKey.Value}' and '{right.NodeKey.Value}'.");
            }
        }
    }

    private static Task<ProcessDefinitionStepEditorCommandResult> ExecuteStepCommandAsync(
        ProcessDefinitionStepEditorProjectionService service,
        ProcessDefinitionStepEditorProjection editor,
        ProcessDefinitionStepCommandKind commandKind,
        ProcessDefinitionStepDraftProjection draft)
        => service.ExecuteCommandAsync(new ProcessDefinitionStepEditorCommand(
            ProcessWorkspaceShellScope.Global,
            editor.DefinitionKey,
            commandKind,
            editor.VersionToken,
            draft));

    private static ProcessDefinitionEditorDraftProjection CreateDraft(
        ProcessDefinitionEditorProjection editor)
        => new(
            editor.DefinitionKey,
            editor.Identity,
            editor.Governance,
            editor.Contracts,
            editor.Simulation);

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TemporaryProcessTemplatePack : IDisposable
    {
        private TemporaryProcessTemplatePack(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static TemporaryProcessTemplatePack CreateDefault()
            => Create(
                ("delivery-default", "delivery-default", "Delivery default", "Default delivery flow"),
                ("architecture-review", "architecture-review", "Architecture review", "Architecture governance flow"));

        public static TemporaryProcessTemplatePack Create(
            params (string ManifestKey, string DefinitionKey, string DisplayName, string Summary)[] definitions)
        {
            var root = Directory.CreateTempSubdirectory("process-template-pack-").FullName;
            var processes = definitions
                .Select(definition => $$"""
                    {
                      "Key": "{{definition.ManifestKey}}",
                      "RelativePath": "processes/{{definition.ManifestKey}}"
                    }
                    """)
                .ToArray();
            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                $$"""
                {
                  "PackKey": "test-pack",
                  "Name": "Test process template pack",
                  "Version": "test-pack",
                  "GeneratedAtUtc": "2026-06-16T00:00:00Z",
                  "Processes": [
                    {{string.Join("," + Environment.NewLine, processes)}}
                  ]
                }
                """);

            foreach (var definition in definitions)
            {
                var directory = Path.Combine(root, "processes", definition.ManifestKey);
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    Path.Combine(directory, "definition.json"),
                    $$"""
                    {
                      "Kind": "process-template-definition",
                      "Key": "{{definition.DefinitionKey}}",
                      "DisplayName": "{{definition.DisplayName}}",
                      "Summary": "{{definition.Summary}}",
                      "ValueStatement": "Template value statement.",
                      "CustomerName": "Architecture customer",
                      "OwnerName": "Architecture owner",
                      "InterfaceContractSummary": "Interface contract.",
                      "ManagerOverrideSummary": "Manager override.",
                      "GovernanceNotes": "Governance notes.",
                      "ChangeSummary": "Change summary.",
                      "GovernancePolicySummary": "Governance policy.",
                      "ConstitutionRuleSummary": "Constitution rule.",
                      "OperatingModeSummary": "Operating mode summary.",
                      "SimulationReadinessSummary": "Simulation readiness.",
                      "Criticality": "High",
                      "OperatingMode": "GovernedLive",
                      "AutonomyLevel": "Guarded",
                      "RoleUsages": [
                        {
                          "Key": "solution-architect",
                          "RoleResourceKey": "solution-architect",
                          "DisplayName": "Solution architect",
                          "Purpose": "Own architecture decisions and technical tradeoffs.",
                          "StaffingIntent": "Assign a senior architecture owner before launch planning.",
                          "PreferredExecutorKind": "person-or-agent",
                          "PreferredProjectAssignmentRole": "Architect",
                          "IsRequired": true,
                          "AllowsFallback": true,
                          "RequiresExplicitApproval": true,
                          "DefaultAllocationPercent": 60,
                          "RoleTemplateSourceKey": "process-role-template/solution-architect",
                          "RoleTemplateSnapshotName": "Solution architect v1",
                          "SnapshotSummary": "Architecture role template snapshot.",
                          "CanvasX": 160,
                          "CanvasY": 40,
                          "Notes": "Coordinates architecture choices."
                        }
                      ],
                      "Steps": [
                        {
                          "Order": 0,
                          "Key": "architecture-decision",
                          "Title": "Architecture decision",
                          "Subtitle": "Governed decision",
                          "Notes": "Choose an architecture route from typed outcomes.",
                          "StepKind": "Decision",
                          "AllowsManualSkip": false,
                          "AllowsSafeRefusal": true,
                          "RequiresApproval": true,
                          "RequiresDecisionRecord": true,
                          "InputContractSummary": "Architecture concern, project context, and decision trigger.",
                          "OutputContractSummary": "Architecture decision record and approved implementation lane.",
                          "EvidenceContractSummary": "Decision evidence with source constraints and route rationale.",
                          "DecisionRightsSummary": "Solution architect can approve or route back for redesign.",
                          "ExceptionPolicySummary": "Escalate when route evidence is missing or contradictory.",
                          "TargetLeadHours": 12,
                          "CanvasX": 160,
                          "CanvasY": 220,
                          "BranchCanvasX": 420,
                          "BranchCanvasY": 120,
                          "DecisionRoleKey": "solution-architect",
                          "RoleAssignments": [
                            {
                              "RoleKey": "solution-architect",
                              "ResponsibilityKind": "Approver",
                              "IsRequired": true,
                              "FallbackOrder": 1,
                              "RebindPolicySummary": "Rebind to the architecture board when the primary owner is unavailable."
                            }
                          ],
                          "ArtifactExpectations": [
                            {
                              "Key": "architecture-decision-record",
                              "TemplateKey": "architecture-decision-record",
                              "Title": "Architecture decision record",
                              "ArtifactKind": "Deliverable",
                              "IsRequired": true,
                              "TrustRequirement": "ReviewRequired",
                              "SensitivityLevel": "Internal",
                              "RetentionDays": 365,
                              "WorkflowOutputId": "adr-output",
                              "WorkflowOutputName": "Architecture decision record",
                              "WorkflowOutputKind": "Artifact",
                              "AllowedFutureUsageSummary": "Reusable for implementation planning and route replay.",
                              "ValidationRequirementSummary": "Must include selected option, rejected options, rationale, and follow-up route."
                            }
                          ],
                          "BranchOutcomes": [
                            {
                              "Key": "approved",
                              "Title": "Approved",
                              "Description": "Route to the approved implementation lane.",
                              "RouteTargetKind": "NextStep",
                              "RouteTargetStepKey": "",
                              "RouteTargetArtifactExpectationKey": "",
                              "IsBackwardRoute": false,
                              "LoopBudgetMaximumRepeats": 0,
                              "LoopFingerprintPolicyKey": "",
                              "LoopEscalationTargetKind": "Escalate"
                            }
                          ],
                          "AllowedOperations": [
                            "ReadProcessContext",
                            "ReadProjectStructure",
                            "ReadUpstreamArtifacts",
                            "WriteExternalArtifactDestination",
                            "EscalateOrDecide"
                          ],
                          "OperationTargetScope": "ExternalArtifactDestination"
                        }
                      ]
                    }
                    """);
            }

            Directory.CreateDirectory(Path.Combine(root, "toolbox"));
            File.WriteAllText(
                Path.Combine(root, "toolbox", "role-templates.json"),
                """
                [
                  {
                    "ActionId": "role-template.solution-architect",
                    "Label": "Solution architect template",
                    "Summary": "Owns architecture decisions and technical tradeoffs.",
                    "TemplateRoleKey": "solution-architect",
                    "KeyPrefix": "solution-architect",
                    "DisplayNameTemplate": "Solution architect {ordinal}",
                    "PreferredExecutorKind": "person-or-agent",
                    "DefaultAllocationPercent": 60
                  }
                ]
                """);

            File.WriteAllText(
                Path.Combine(root, "toolbox", "step-templates.json"),
                """
                [
                  {
                    "ActionId": "process-step.implementation",
                    "Label": "Implementation",
                    "Summary": "Perform implementation and produce reviewable evidence.",
                    "Template": {
                      "Key": "implementation",
                      "Title": "Implementation",
                      "StepKind": "Work"
                    }
                  },
                  {
                    "ActionId": "process-step.decision",
                    "Label": "Decision router",
                    "Summary": "Route work explicitly through governed follow-up lanes.",
                    "Template": {
                      "Key": "review-disposition",
                      "Title": "Route review disposition",
                      "StepKind": "Decision"
                    }
                  },
                  {
                    "ActionId": "process-step.subprocess",
                    "Label": "Subprocess",
                    "Summary": "Run another process definition as an observed child run.",
                    "Template": {
                      "Key": "subprocess",
                      "Title": "Run subprocess",
                      "StepKind": "Subprocess"
                    }
                  }
                ]
                """);

            return new TemporaryProcessTemplatePack(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
