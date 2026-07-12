using System.Globalization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
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
        Assert.Contains("arithmetically consistent", testContract.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("System.Net.Http.Json", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("@inherits <BaseClass>", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("invalid generated test", codeChange.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not cite stale source document paths", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not create a separate ComponentBase class", codeChange.Notes, StringComparison.Ordinal);
        Assert.Contains("shared _Imports.razor repairs", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("Razor component symbol errors", featureRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("same canonical solution no longer reproduces the defect", featureRepair.Notes, StringComparison.Ordinal);
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
        Assert.Contains("missing build/test receipts are not a reason to escalate", targetedRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required current-run focused proof was attempted", targetedRecheck.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation tool or product root is unavailable", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sequential same-solution build/test blockers", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not treat a later compiler or test failure as outside scope", featureRepair.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not escalate same-solution compile or test failures", featureRepair.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("validation-command ownership", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validation-command ownership", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_restore", addTestsAndProof.Notes, StringComparison.Ordinal);
        Assert.Contains("missing receipts are not a repair finding by themselves", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Managed artifact evidence rule", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not put product source/test file aliases", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grounded receipt-based proof instead of selecting slice-repair-required", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_test", addTestsRecheck.Notes, StringComparison.Ordinal);
        Assert.Contains("missing receipts are not an escalation finding by themselves", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Managed artifact evidence rule", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not put product source/test file aliases", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grounded receipt-based proof instead of selecting slice-repair-escalation", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("root runtime-command and screenshot writeback steps", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("root runtime-command and screenshot writeback steps", addTestsRecheck.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProcessOperationContractNames.ExecuteExternalAction, addTestsAndProof.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.ExecuteExternalAction, addTestsRecheck.AllowedOperations);
        Assert.Contains("current-run implement-code-change coordinator artifact", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trace links, not direct read requirements", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trace links, not direct read requirements", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("read-only QA step", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("read-only QA step", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet restore", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet build --no-restore", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet test --no-restore", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolve the actual solution target", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".slnx", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never synthesize or require <SolutionName>.sln", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rerun the current-run validation chain against the discovered .slnx or .sln target", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not call workspace_git_status as a validation precondition", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a git repository result", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("continue immediately with product-root file listing plus the restore/build/test validation chain", addTestsAndProof.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet restore", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet build --no-restore", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet test --no-restore", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolve the actual solution target", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".slnx", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never synthesize or require <SolutionName>.sln", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rerun the current-run validation chain against the discovered .slnx or .sln target", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not call workspace_git_status as a validation precondition", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a git repository result", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("continue immediately with product-root file listing plus the restore/build/test validation chain", addTestsRecheck.Notes, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("cite those launch variable names as source evidence", classify.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not cite source document paths, native absolute paths", classify.Notes, StringComparison.Ordinal);
        Assert.Contains("even when those path-like values appear in the current step brief", classify.Notes, StringComparison.Ordinal);
        Assert.Contains("cite those launch variable names as source evidence", classifyDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not cite source document paths, native absolute paths", classifyDoc, StringComparison.Ordinal);
        Assert.Contains("even when those path-like values appear in the current step brief", classifyDoc, StringComparison.Ordinal);
        Assert.Contains("stable document id, project-structure node id, title, or current-run workspace tool receipt", classifyDoc, StringComparison.Ordinal);
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
        Assert.Contains("missing restore/build/test receipts are not a reason to route repair", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("run restore and then rerun build/test", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing restore/build/test receipts must trigger validation execution first", qaValidation.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not call workspace_git_status as a validation precondition", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a git repository result", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("continue immediately with product-root file listing plus the restore/build/test validation chain", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grounded external-target aliases", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("final submit_process_step_outcome evidenceRefs", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact restore/build/test receipt refs", qaValidation.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing restore/build/test receipts are not a reason to escalate", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required current-run repaired validation was attempted", qaRecheck.ExceptionPolicySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not call workspace_git_status as a validation precondition", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a git repository result", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grounded external-target aliases", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("final submit_process_step_outcome evidenceRefs", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact restore/build/test receipt refs", qaRecheck.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("continue immediately with product-root file listing plus the restore/build/test validation chain", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_run", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_analyze_images", qaValidation.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stock scaffold UI", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing interaction proof", qaValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_run", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_analyze_images", qaRecheck.EvidenceContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stock scaffold UI", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing interaction proof", qaRecheck.Notes, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("DotNetCreateProjectScript", create.Notes, StringComparison.Ordinal);
        Assert.Contains("solution app-membership file-content check", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_create_directory", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not write native absolute paths", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scoped storage paths under artifacts/scopes", create.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tool-run stdout/stderr paths", create.Notes, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("ProductCompletionRequiredToolReceipts", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("sideEffectManifest mode ProductMutation", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("dotnet add <test-project-file> reference <app-project-file>", addTest.Notes, StringComparison.Ordinal);
        Assert.Contains("convert command output to scalar strings before membership or ProjectReference regex checks", addTest.Notes, StringComparison.Ordinal);

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
        var sliceIntake = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "slice-intake", StringComparison.Ordinal));
        var sliceImplementation = Assert.Single(developmentSlice.Steps, step => string.Equals(step.Key, "implement-code-change", StringComparison.Ordinal));
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
        Assert.Contains("must not be scaffold-only", featureIntake.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a valid derived behavior", featureIntakeDoc, StringComparison.OrdinalIgnoreCase);
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

        Assert.Contains("repair target packet", sliceValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not ask the child subprocess to select a fresh MVP behavior", sliceRepair.Notes, StringComparison.Ordinal);
        Assert.Contains("original repair target", sliceRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not derive a new MVP behavior", featureIntake.Notes, StringComparison.Ordinal);
        Assert.Contains("inherited repair target", targetedValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before/after metric", targetedRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("this validation step execution", targetedValidation.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not satisfy this step's current-execution proof contract", targetedRecheck.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not ask the child subprocess to select a fresh MVP behavior", sliceRepairDoc, StringComparison.Ordinal);
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
    public void Software_delivery_quality_repair_can_verify_runtime_browser_repairs()
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

        Assert.Contains(ProcessOperationContractNames.LaunchRuntime, qualityRepair.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.CaptureRuntimeProof, qualityRepair.AllowedOperations);
        Assert.Contains("runtime or browser proof", qualityRepairDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Capture current-run managed artifacts", qualityRepairDoc, StringComparison.Ordinal);
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
        Assert.Contains("Missing base URL or missing Run app node is not a successful capture result", step.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not write Status: Completed", step.Notes, StringComparison.Ordinal);
        Assert.Contains("Missing base URL or missing Run app node is not a successful capture result", captureDoc, StringComparison.Ordinal);
        Assert.Contains("declared required runtime tool receipts", captureDoc, StringComparison.Ordinal);
        Assert.Contains("Do not return `Blocked` only because the base URL is absent", captureDoc, StringComparison.Ordinal);
        Assert.Contains("Do not write `Status: Completed`", captureDoc, StringComparison.Ordinal);
        Assert.Contains("only scaffold chrome", step.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("materially inconsistent with a named source visual target", captureDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_run", step.Notes, StringComparison.Ordinal);
        Assert.Contains("DotNetAppProjectFileAlias", step.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run accepts project files only", step.Notes, StringComparison.Ordinal);
        Assert.Contains("never pass DotNetSolutionFile", step.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DotNetAppProjectFileAlias", captureDoc, StringComparison.Ordinal);
        Assert.Contains(".csproj", captureDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("declared as required runtime tool receipts", step.Notes, StringComparison.Ordinal);
        Assert.Contains("Do not return Blocked only because the base URL is absent", step.Notes, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", step.Notes, StringComparison.Ordinal);
        Assert.Contains("browser_snapshot", step.Notes, StringComparison.Ordinal);
        Assert.Contains("browser_take_screenshot", step.Notes, StringComparison.Ordinal);
        Assert.Contains("browser_console_messages", step.Notes, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_stop", step.Notes, StringComparison.Ordinal);
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
        Assert.Contains("declared as required runtime tool receipts", step.Notes, StringComparison.Ordinal);
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
        Assert.Contains("declared required runtime tool receipts", storeDoc, StringComparison.Ordinal);
        Assert.Contains("do not infer the writeback tools are unavailable", storeDoc, StringComparison.Ordinal);
        Assert.Contains("Do not write `Status: Completed`", storeDoc, StringComparison.Ordinal);
        Assert.Contains("sourceWorkspacePath", storeDoc, StringComparison.Ordinal);
        Assert.Contains("invalid base64", storeDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("return `Blocked`", storeDoc, StringComparison.Ordinal);
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
            Assert.Contains("stock scaffold UI", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("missing interaction proof", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dotnet restore", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dotnet build --no-restore", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dotnet test --no-restore", stepContract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("source ImageAsset", stepContract, StringComparison.Ordinal);
            Assert.Contains("media path", stepContract, StringComparison.Ordinal);
            Assert.Contains("screenshot/browser proof requirements", stepContract, StringComparison.Ordinal);
            Assert.Contains("final evidenceRefs", stepContract, StringComparison.Ordinal);
            Assert.Contains("native absolute product paths", stepContract, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("do not write native absolute product paths", stepDocs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tetris", stepDocs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tetromino", stepDocs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Software_delivery_peer_review_can_run_read_only_validation()
    {
        var loader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = loader.LoadDefinition("software-delivery");

        var peerReview = Assert.Single(definition.Steps, step => string.Equals(step.Key, "peer-review", StringComparison.Ordinal));

        Assert.Equal(ProcessOperationContractNames.ExternalProductTargetReadOnly, peerReview.OperationTargetScope);
        Assert.Contains(ProcessOperationContractNames.RunValidation, peerReview.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.MutateProductTarget, peerReview.AllowedOperations);
    }

    [Fact]
    public void Dotnet_architecture_review_accepts_project_structure_scope_evidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(repositoryRoot, "Templates", "Processes"));
        var definition = loader.LoadDefinition("dotnet-architecture-design-review");

        var draft = Assert.Single(definition.Steps, step => string.Equals(step.Key, "draft-architecture-design", StringComparison.Ordinal));
        var review = Assert.Single(definition.Steps, step => string.Equals(step.Key, "review-architecture-design", StringComparison.Ordinal));
        var reviewDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Templates",
            "Processes",
            "processes",
            "dotnet-architecture-design-review",
            "steps",
            "review-architecture-design.md"));

        Assert.Contains("ProjectStructureContextSummary", draft.Notes, StringComparison.Ordinal);
        Assert.Contains("available scope/acceptance evidence", review.InputContractSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Missing standalone acceptance/user-story files are not a hard block", review.DecisionRightsSummary, StringComparison.Ordinal);
        Assert.Contains("Do not block solely because a separate acceptance-criteria or user-story artifact is absent", review.ExceptionPolicySummary, StringComparison.Ordinal);
        Assert.Contains("Do not hard-block solely because a standalone acceptance-criteria or user-story file is absent", reviewDoc, StringComparison.Ordinal);
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
    public async Task Canvas_accepts_stale_recompose_against_current_projection()
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

        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, stale.Receipt.Status);
        Assert.Contains(stale.Projection.Nodes, node => node.Title == "Implementation");
        Assert.Contains("recomposed", stale.Receipt.Summary, StringComparison.OrdinalIgnoreCase);
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
