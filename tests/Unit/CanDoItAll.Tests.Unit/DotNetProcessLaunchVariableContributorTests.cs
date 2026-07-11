using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetProcessLaunchVariableContributorTests
{
    [Fact]
    public void Enrich_adds_feature_validation_required_receipt_map()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly calculator app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "dotnet-feature-function-implementation",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        AssertValidationReceipts(receiptMap, "targeted-validation");
        AssertValidationReceipts(receiptMap, "feature-repair");
        AssertValidationReceipts(receiptMap, "targeted-recheck");

        var routeMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep]);

        Assert.NotNull(routeMap);
        var incompleteImplementationRoute = Assert.Single(Assert.Contains("code-change", routeMap));
        Assert.Equal(
            ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing,
            incompleteImplementationRoute.GetProperty("issueCode").GetString());
        Assert.Empty(incompleteImplementationRoute.GetProperty("sourceBranchOutcomeKeys").EnumerateArray());
        Assert.Equal(
            "implementation-attempt-incomplete",
            incompleteImplementationRoute.GetProperty("targetBranchOutcomeKey").GetString());
        Assert.False(incompleteImplementationRoute.GetProperty("requiresDefectEvidence").GetBoolean());
        var incompleteRepairRoutes = Assert.Contains("feature-repair", routeMap);
        Assert.Equal(4, incompleteRepairRoutes.Length);
        Assert.Equal(
            [
                ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing,
                ProcessCompletionDiagnosticCodes.ManagedArtifactWriteReceiptMissing,
                ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing,
                ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing
            ],
            incompleteRepairRoutes.Select(route => route.GetProperty("issueCode").GetString()));
        Assert.All(incompleteRepairRoutes, incompleteRepairRoute =>
        {
            Assert.Equal(
                "repair-attempt-incomplete",
                incompleteRepairRoute.GetProperty("targetBranchOutcomeKey").GetString());
            Assert.False(incompleteRepairRoute.GetProperty("requiresDefectEvidence").GetBoolean());
            Assert.Equal(
                string.Equals(
                    incompleteRepairRoute.GetProperty("issueCode").GetString(),
                    ProcessCompletionDiagnosticCodes.ManagedArtifactWriteReceiptMissing,
                    StringComparison.Ordinal),
                incompleteRepairRoute.GetProperty("onlyAfterAutomaticRetry").GetBoolean());
        });

        var sourceInspectionSteps = JsonSerializer.Deserialize<string[]>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys]);

        Assert.NotNull(sourceInspectionSteps);
        Assert.Equal(
            ["code-change", "targeted-validation", "feature-repair", "targeted-recheck"],
            sourceInspectionSteps);

        var excludedSourceFragments = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep]);
        Assert.NotNull(excludedSourceFragments);
        Assert.Contains("/Program.cs", excludedSourceFragments["code-change"]);
        Assert.Contains("/Program.cs", excludedSourceFragments["targeted-validation"]);
        Assert.Contains(".csproj", excludedSourceFragments["targeted-validation"]);
        Assert.Contains("/Program.cs", excludedSourceFragments["feature-repair"]);
        Assert.Contains("/Layout/", excludedSourceFragments["targeted-recheck"]);

        var sourceInspectionBranchMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep]);
        Assert.NotNull(sourceInspectionBranchMap);
        Assert.Equal(["feature-accepted"], sourceInspectionBranchMap["targeted-validation"]);
        Assert.Equal(["feature-repair-applied"], sourceInspectionBranchMap["feature-repair"]);
        Assert.Equal(["feature-accepted"], sourceInspectionBranchMap["targeted-recheck"]);

        var mutationBranchMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep]);

        Assert.NotNull(mutationBranchMap);
        Assert.Equal(["feature-repair-applied"], mutationBranchMap["feature-repair"]);

        var mutationBeforeHandoffSteps = JsonSerializer.Deserialize<string[]>(
            variables[ProcessRuntimeLaunchVariables.ProductMutationBeforeManagedOutputRequiredStepKeys]);
        Assert.NotNull(mutationBeforeHandoffSteps);
        Assert.Equal(["code-change", "feature-repair"], mutationBeforeHandoffSteps);

        var runtimeRoutedBranchMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep]);
        Assert.NotNull(runtimeRoutedBranchMap);
        Assert.Equal(["implementation-attempt-incomplete"], runtimeRoutedBranchMap["code-change"]);
        Assert.Equal(["repair-attempt-incomplete"], runtimeRoutedBranchMap["feature-repair"]);
        var specializationTags = JsonSerializer.Deserialize<string[]>(
            variables[ProcessRuntimeLaunchVariables.ExecutorPreferredSpecializationTags]);
        Assert.NotNull(specializationTags);
        Assert.Equal(["blazor", "wasm", "frontend"], specializationTags);

        var scaffoldContract = variables["DotNetScaffoldContract"];
        Assert.Contains("BlazorWasmNamespaceRule", scaffoldContract);
        Assert.Contains("using Calculator;", scaffoldContract);
        Assert.Contains("@using Calculator.Layout", scaffoldContract);
        Assert.Contains("CS0246", scaffoldContract);
    }

    [Fact]
    public void Enrich_adds_setup_receipt_map_with_create_membership_script_receipt()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly calculator app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "dotnet-solution-setup",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        var createReceipts = Assert.Contains("create-dotnet-project", receiptMap);
        Assert.Contains("template=sln", createReceipts);
        Assert.Contains("template=blazorwasm", createReceipts);
        Assert.Contains("workspace_pwsh_run_script", createReceipts);
        Assert.Contains("workspace_pwsh_run_script", Assert.Contains("add-test-project", receiptMap));
        Assert.Contains("workspace_pwsh_run_script", Assert.Contains("repair-solution-setup", receiptMap));
        AssertValidationReceipts(receiptMap, "validate-first-build");
        AssertValidationReceipts(receiptMap, "validate-first-build-after-repair");

        var executionPlan = variables["DotNetCreateProjectExecutionPlan"];
        Assert.Contains("DotNetCreateProjectScript", executionPlan, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", executionPlan, StringComparison.Ordinal);
        Assert.Contains("solution app-membership readback", executionPlan, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Do not write Status: InProgress", executionPlan, StringComparison.Ordinal);

        Assert.Equal(
            "artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.wire-solution.ps1",
            variables["DotNetCreateProjectScriptRef"]);
        variables["CurrentProcessRunId"] = "9e544598-47f3-4a65-83e5-e28dc9cef38a";
        var resolved = new LaunchVariableTemplateResolver().Resolve(variables);
        Assert.False(resolved.HasBlockingDiagnostics);
        Assert.Equal(
            "artifacts/process-runs/9e544598-47f3-4a65-83e5-e28dc9cef38a/scripts/create-dotnet-project.wire-solution.ps1",
            resolved.Variables["DotNetCreateProjectScriptRef"]);
        Assert.Equal(
            "artifacts/process-runs/9e544598-47f3-4a65-83e5-e28dc9cef38a/scripts/add-test-project.wire-solution.ps1",
            resolved.Variables["DotNetAddTestProjectScriptRef"]);
        Assert.DoesNotContain("{CurrentProcessRunId}", resolved.Variables["DotNetCreateProjectExecutionPlan"], StringComparison.Ordinal);
        Assert.DoesNotContain("{CurrentProcessRunId}", resolved.Variables["DotNetAddTestProjectExecutionPlan"], StringComparison.Ordinal);
        Assert.Contains("Invoke-Dotnet @('sln', $SolutionFile, 'add', $AppProjectFile)", variables["DotNetCreateProjectScript"], StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"ProductMutation\"", variables["DotNetCreateProjectSideEffectManifest"], StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_adds_root_software_delivery_validation_required_receipt_map()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly calculator app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "software-delivery",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: false),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        AssertValidationReceipts(receiptMap, "qa-validation");
        AssertValidationReceipts(receiptMap, "qa-recheck");
        AssertBrowserRuntimeProofReceipts(receiptMap, "qa-validation", expectsVisualComparison: false);
        AssertBrowserRuntimeProofReceipts(receiptMap, "qa-recheck", expectsVisualComparison: false);
        Assert.DoesNotContain("quality-repair", receiptMap);
        AssertAcceptanceBranchReceiptRules(receiptMap, "qa-validation");
        AssertAcceptanceBranchReceiptRules(receiptMap, "qa-recheck");

        var routeMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep]);

        Assert.NotNull(routeMap);
        AssertBranchRoute(routeMap, "qa-validation", "repair-required");
        AssertBranchRoute(routeMap, "qa-recheck", "repair-escalation");
        Assert.False(variables.ContainsKey(ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix));
        Assert.False(variables.ContainsKey(ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys));

        var sourceInspectionSteps = JsonSerializer.Deserialize<string[]>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys]);
        Assert.NotNull(sourceInspectionSteps);
        Assert.Equal(["peer-review", "qa-validation", "qa-recheck"], sourceInspectionSteps);
        var excludedSourceFragments = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep]);
        Assert.NotNull(excludedSourceFragments);
        Assert.Contains("/Program.cs", excludedSourceFragments["peer-review"]);
        Assert.Contains(".csproj", excludedSourceFragments["qa-validation"]);

        var fileContentMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep]);

        Assert.NotNull(fileContentMap);
        var qaChecks = Assert.Contains("qa-validation", fileContentMap);
        Assert.Contains(qaChecks, IsQualityAcceptedScaffoldRemovalCheck);
        AssertFileContentEvidenceBranch(fileContentMap, "qa-validation", "repair-required");
        Assert.DoesNotContain("quality-repair", fileContentMap);
        var recheckChecks = Assert.Contains("qa-recheck", fileContentMap);
        Assert.Contains(recheckChecks, IsQualityAcceptedScaffoldRemovalCheck);
        AssertFileContentEvidenceBranch(fileContentMap, "qa-recheck", "repair-escalation");

        Assert.Equal(@"C:\temp\CanDoItAll\Calculator", variables["ProductRoot"]);
        Assert.Equal(@"C:\temp\CanDoItAll\Calculator\Calculator.slnx", variables["DotNetSolutionFile"]);
        Assert.Contains("external-target/", variables["DotNetSolutionFileAlias"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(@"C:\temp\CanDoItAll\Calculator\src\Calculator\Calculator.csproj", variables["DotNetAppProjectFile"]);
        Assert.Contains("external-target/", variables["DotNetAppProjectFileAlias"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/src/Calculator/Calculator.csproj", variables["DotNetAppProjectFileAlias"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(@"C:\temp\CanDoItAll\Calculator\tests\Calculator.Tests\Calculator.Tests.csproj", variables["DotNetTestProjectFile"]);
        Assert.Contains("external-target/", variables["DotNetTestProjectFileAlias"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/tests/Calculator.Tests/Calculator.Tests.csproj", variables["DotNetTestProjectFileAlias"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("blazorwasm", variables["DotNetAppTemplate"]);
        Assert.Contains("BlazorWasmNamespaceRule", variables["DotNetScaffoldContract"]);
        Assert.Contains("SolutionValidationTargetRule", variables["DotNetScaffoldContract"]);
        Assert.Contains("Test validation must target DotNetTestProjectFileAlias or DotNetTestProjectFile", variables["DotNetScaffoldContract"]);
        Assert.Contains("fall back to the solution target only when no test project target exists", variables["DotNetScaffoldContract"]);
        Assert.Contains("DotNetRunProjectTargetRule", variables["DotNetScaffoldContract"]);
        Assert.Contains("workspace_dotnet_run targetPath must be DotNetAppProjectFileAlias", variables["DotNetScaffoldContract"]);
        Assert.Contains("Never call workspace_dotnet_run with DotNetSolutionFile", variables["DotNetScaffoldContract"]);
        Assert.Contains("Do not infer <SolutionName>.sln", variables["DotNetScaffoldContract"]);

        static bool IsQualityAcceptedScaffoldRemovalCheck(JsonElement check)
            => IsScaffoldRemovalCheck(check) &&
               check.TryGetProperty("enforceBranchOutcomeKeys", out var branchOutcomeKeys) &&
               branchOutcomeKeys.EnumerateArray().Any(value =>
                   string.Equals(value.GetString(), "quality-accepted", StringComparison.Ordinal));

        static bool IsScaffoldRemovalCheck(JsonElement check)
            => check.GetProperty("mustExist").GetBoolean() == false &&
               check.GetProperty("forbiddenTextAny").EnumerateArray().Any(value =>
                   string.Equals(value.GetString(), "@page \"/counter\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Enrich_adds_quality_repair_receipts_routes_and_content_gates_for_child_steps()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "BusinessApp", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\BusinessApp",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly business app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "dotnet-quality-repair",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);
        Assert.NotNull(receiptMap);
        AssertValidationReceipts(receiptMap, "implement-quality-repair");
        AssertValidationReceipts(receiptMap, "validate-quality-repair");
        AssertValidationReceipts(receiptMap, "implement-bughunt-repair");
        AssertValidationReceipts(receiptMap, "revalidate-bughunt-repair");
        AssertBrowserRuntimeProofReceipts(receiptMap, "validate-quality-repair", expectsVisualComparison: false);
        AssertBrowserRuntimeProofReceipts(receiptMap, "revalidate-bughunt-repair", expectsVisualComparison: false);
        AssertAcceptanceBranchReceiptRules(receiptMap, "validate-quality-repair", "quality-repair-accepted");
        AssertAcceptanceBranchReceiptRules(receiptMap, "revalidate-bughunt-repair", "quality-repair-accepted");

        var mutationBranchMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep]);
        Assert.NotNull(mutationBranchMap);
        Assert.Equal(["product-repair-applied"], mutationBranchMap["implement-quality-repair"]);
        Assert.Equal(["product-repair-applied"], mutationBranchMap["implement-bughunt-repair"]);

        var runtimeRoutedBranchMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep]);
        Assert.NotNull(runtimeRoutedBranchMap);
        Assert.Equal(["repair-attempt-incomplete"], runtimeRoutedBranchMap["implement-quality-repair"]);
        Assert.Equal(["repair-attempt-incomplete"], runtimeRoutedBranchMap["implement-bughunt-repair"]);

        var sourceInspectionSteps = JsonSerializer.Deserialize<string[]>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys]);
        Assert.NotNull(sourceInspectionSteps);
        Assert.Contains("diagnose-quality-failure", sourceInspectionSteps);
        Assert.Contains("implement-quality-repair", sourceInspectionSteps);
        Assert.Contains("diagnose-persistent-failure", sourceInspectionSteps);
        Assert.Contains("implement-bughunt-repair", sourceInspectionSteps);

        var excludedSourceFragments = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep]);
        Assert.NotNull(excludedSourceFragments);
        Assert.Contains("/Program.cs", excludedSourceFragments["diagnose-quality-failure"]);
        Assert.Contains("/App.razor", excludedSourceFragments["diagnose-quality-failure"]);
        Assert.Contains(".csproj", excludedSourceFragments["diagnose-quality-failure"]);
        Assert.Contains("/Pages/Counter.razor", excludedSourceFragments["diagnose-persistent-failure"]);

        Assert.Equal(
            "artifacts/process-runs/{CurrentProcessRunId}/scripts/remove-default-blazor-scaffold.ps1",
            variables["DotNetScaffoldRepairScriptRef"]);
        Assert.Contains("$appDirectory = 'C:/temp/CanDoItAll/BusinessApp/src/BusinessApp'", variables["DotNetScaffoldRepairScript"], StringComparison.Ordinal);
        Assert.Contains("currentCount", variables["DotNetScaffoldRepairScript"], StringComparison.Ordinal);
        Assert.Contains("WeatherForecast", variables["DotNetScaffoldRepairScript"], StringComparison.Ordinal);
        Assert.Contains("#blazor-error-ui", variables["DotNetScaffoldRepairScript"], StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", variables["DotNetScaffoldRepairExecutionPlan"], StringComparison.Ordinal);
        Assert.Contains("fingerprint", variables["DotNetScaffoldRepairExecutionPlan"], StringComparison.OrdinalIgnoreCase);
        using (var manifest = JsonDocument.Parse(variables["DotNetScaffoldRepairSideEffectManifest"]))
        {
            Assert.Equal("ProductMutation", manifest.RootElement.GetProperty("mode").GetString());
            Assert.Contains(
                manifest.RootElement.GetProperty("declaredWritePaths").EnumerateArray(),
                path => path.GetString()!.EndsWith("Pages\\Counter.razor", StringComparison.Ordinal));
        }

        var routeMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep]);
        Assert.NotNull(routeMap);
        AssertIncompleteRepairRoute(routeMap, "implement-quality-repair");
        AssertBranchRoute(routeMap, "validate-quality-repair", "bughunt-required");
        AssertIncompleteRepairRoute(routeMap, "implement-bughunt-repair");
        AssertBranchRoute(routeMap, "revalidate-bughunt-repair", "quality-repair-no-go");

        var fileContentMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep]);
        Assert.NotNull(fileContentMap);
        Assert.DoesNotContain("implement-quality-repair", fileContentMap);
        Assert.DoesNotContain("implement-bughunt-repair", fileContentMap);
        AssertFileContentEvidenceBranch(fileContentMap, "validate-quality-repair", "bughunt-required");
        AssertFileContentEvidenceBranch(fileContentMap, "revalidate-bughunt-repair", "quality-repair-no-go");
    }

    [Fact]
    public void Enrich_routes_development_slice_no_go_evidence_to_repair_branch()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\BusinessApp",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly business app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                new ProjectStructureSurface(projectId, "BusinessApp", [node], [], null),
                node,
                "dotnet-development-slice",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var routeMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep]);

        Assert.NotNull(routeMap);
        var route = Assert.Single(routeMap["add-tests-and-proof"]);
        Assert.Equal(
            ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
            route.GetProperty("issueCode").GetString());
        Assert.Equal("slice-repair-required", route.GetProperty("targetBranchOutcomeKey").GetString());
        Assert.False(route.GetProperty("requiresDefectEvidence").GetBoolean());

        var recheckRoute = Assert.Single(routeMap["add-tests-recheck"]);
        Assert.Equal(
            ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
            recheckRoute.GetProperty("issueCode").GetString());
        Assert.Equal("slice-repair-escalation", recheckRoute.GetProperty("targetBranchOutcomeKey").GetString());
        Assert.False(recheckRoute.GetProperty("requiresDefectEvidence").GetBoolean());
    }

    [Fact]
    public void Enrich_adds_acceptance_criteria_matrix_for_complex_software_delivery_project()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateComplexNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Tetris", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Tetris",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly game with explicit acceptance criteria."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "software-delivery",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: false),
            variables);

        Assert.True(variables.TryGetValue(ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix, out var rawMatrix));
        Assert.Equal("quality-accepted", variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys]);
        Assert.True(ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(rawMatrix, out var matrix));
        Assert.Collection(
            matrix.RequiredCriteria,
            criterion =>
            {
                Assert.Equal("AC-001", criterion.Id);
                Assert.Equal(node.Id, criterion.SourceNodeId);
                Assert.Contains("falling tetromino", criterion.Summary, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("browser-proof", criterion.VerificationMethods);
            },
            criterion =>
            {
                Assert.Equal("AC-002", criterion.Id);
                Assert.Contains("completed lines", criterion.Summary, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("unit-test", criterion.VerificationMethods);
            },
            criterion =>
            {
                Assert.Equal("AC-003", criterion.Id);
                Assert.Contains("pause and resume", criterion.Summary, StringComparison.OrdinalIgnoreCase);
            });
        Assert.Contains("AC-001", variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract], StringComparison.Ordinal);
        Assert.Contains("criterion id", variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enrich_preserves_normative_project_requirements_without_acceptance_heading_in_feature_subprocess()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var selectedNode = CreateNode(projectId);
        var requirementsNode = CreateRequirementNode(
            projectId,
            "custom:requirements",
            "Application requirements",
            "Use browser database storage as the only persistence mechanism. The UI must allow keyboard input. The dashboard must fit within the viewport without scrolling.");
        var surface = new ProjectStructureSurface(projectId, "WorkLogger", [selectedNode, requirementsNode], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\WorkLogger",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly work logger with local persistence.",
            [ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys] = "quality-accepted"
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                selectedNode,
                "dotnet-feature-function-implementation",
                ProcessDefinitionId: null,
                ParentRunId: ProcessRunId.New(),
                ParentStepId: ProcessStepInstanceId.New(),
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        Assert.True(variables.TryGetValue(ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix, out var rawMatrix));
        Assert.Equal("feature-accepted", variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys]);
        Assert.True(ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(rawMatrix, out var matrix));
        Assert.Equal(3, matrix.RequiredCriteria.Count);
        Assert.Contains(matrix.RequiredCriteria, criterion =>
            criterion.Summary.Contains("only persistence mechanism", StringComparison.OrdinalIgnoreCase) &&
            criterion.VerificationMethods.Contains("browser-proof"));
        Assert.Contains(matrix.RequiredCriteria, criterion =>
            criterion.Summary.Contains("keyboard input", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(matrix.RequiredCriteria, criterion =>
            criterion.Summary.Contains("without scrolling", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("blazor-app-delivery")]
    [InlineData("blazor-app-repair-fix")]
    [InlineData("blazor-backend-feature")]
    [InlineData("blazor-frontend-feature")]
    [InlineData("blazor-fullstack-feature")]
    public void Enrich_adds_root_blazor_delivery_branch_aware_validation_metadata(string definitionKey)
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly calculator app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                definitionKey,
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: false),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        AssertValidationReceipts(receiptMap, "validate-blazor-runtime");
        AssertValidationReceipts(receiptMap, "repair-blazor-findings");
        AssertValidationReceipts(receiptMap, "revalidate-blazor-repair");
        AssertBrowserRuntimeProofReceipts(receiptMap, "validate-blazor-runtime", expectsVisualComparison: false);
        AssertBrowserRuntimeProofReceipts(receiptMap, "repair-blazor-findings", expectsVisualComparison: false);
        AssertBrowserRuntimeProofReceipts(receiptMap, "revalidate-blazor-repair", expectsVisualComparison: false);
        AssertAcceptanceBranchReceiptRules(receiptMap, "validate-blazor-runtime");
        AssertAcceptanceBranchReceiptRules(receiptMap, "revalidate-blazor-repair");

        var routeMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep]);

        Assert.NotNull(routeMap);
        AssertBranchRoute(routeMap, "validate-blazor-runtime", "repair-required");
        AssertBranchRoute(routeMap, "revalidate-blazor-repair", "repair-escalation");

        var sourceInspectionSteps = JsonSerializer.Deserialize<string[]>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys]);
        Assert.NotNull(sourceInspectionSteps);
        Assert.Equal(["validate-blazor-runtime", "revalidate-blazor-repair"], sourceInspectionSteps);
        var excludedSourceFragments = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep]);
        Assert.NotNull(excludedSourceFragments);
        Assert.Contains("/Program.cs", excludedSourceFragments["validate-blazor-runtime"]);

        var fileContentMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep]);

        Assert.NotNull(fileContentMap);
        Assert.Contains("validate-blazor-runtime", fileContentMap);
        Assert.DoesNotContain("repair-blazor-findings", fileContentMap);
        Assert.Contains("revalidate-blazor-repair", fileContentMap);
        AssertFileContentEvidenceBranch(fileContentMap, "validate-blazor-runtime", "repair-required");
        AssertFileContentEvidenceBranch(fileContentMap, "revalidate-blazor-repair", "repair-escalation");
        Assert.False(variables.ContainsKey(ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix));
    }

    [Fact]
    public void Enrich_adds_acceptance_criteria_matrix_for_complex_blazor_delivery_project()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateComplexNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Tetris", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Tetris",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly game with explicit acceptance criteria."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "blazor-app-delivery",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: false),
            variables);

        Assert.True(variables.TryGetValue(ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix, out var rawMatrix));
        Assert.Equal("quality-accepted", variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys]);
        Assert.True(ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(rawMatrix, out var matrix));
        Assert.Equal(["AC-001", "AC-002", "AC-003"], matrix.RequiredCriteria.Select(criterion => criterion.Id));
        Assert.Contains("AC-001", variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract], StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_adds_ui_screenshot_writeback_required_project_structure_receipts_for_browser_app()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly calculator app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "dotnet-ui-screenshot-writeback",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        var captureReceipts = Assert.Contains("capture-ui-screenshots", receiptMap);
        Assert.Contains("workspace_dotnet_run", captureReceipts);
        Assert.Contains("browser_navigate", captureReceipts);
        Assert.Contains("browser_snapshot", captureReceipts);
        Assert.Contains("browser_take_screenshot", captureReceipts);
        Assert.Contains("browser_console_messages", captureReceipts);
        Assert.Contains("workspace_dotnet_stop", captureReceipts);

        var storeReceipts = Assert.Contains("store-ui-screenshots", receiptMap);
        Assert.Contains("workspace_inspect_image", storeReceipts);
        Assert.Contains("workspace_analyze_image", storeReceipts);
        Assert.DoesNotContain("workspace_analyze_images", storeReceipts);
        Assert.Contains("project_structure_node_create", storeReceipts);
        Assert.Contains("project_structure_asset_create", storeReceipts);
        Assert.Equal("blazorwasm", variables["DotNetAppTemplate"]);
        Assert.Equal(@"C:\temp\CanDoItAll\Calculator\src\Calculator\Calculator.csproj", variables["DotNetAppProjectFile"]);
        Assert.Contains("external-target/", variables["DotNetAppProjectFileAlias"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/src/Calculator/Calculator.csproj", variables["DotNetAppProjectFileAlias"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DotNetRunProjectTargetRule", variables["DotNetScaffoldContract"]);
        Assert.Contains("workspace_dotnet_run targetPath must be DotNetAppProjectFileAlias", variables["DotNetScaffoldContract"]);
    }

    [Fact]
    public void Enrich_adds_visual_target_comparison_receipt_for_ui_screenshot_writeback_when_source_image_assets_exist()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var visualTarget = CreateVisualTargetNode(projectId, node.Id);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node, visualTarget], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = """
                Blazor WebAssembly app with xUnit tests.
                Visual target assets:
                - Application layout proposal (custom:visual-target) [ImageAsset/generated; image/png; media=managed-files/project-media/images/project/proposal.png; file=proposal.png; parent=custom:calculator]: source visual target.
                Visual target rule: implementation and QA must fetch or analyze the relevant asset content before accepting visual alignment.
                """
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "dotnet-ui-screenshot-writeback",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        var storeReceipts = Assert.Contains("store-ui-screenshots", receiptMap);
        Assert.Contains("workspace_inspect_image", storeReceipts);
        Assert.Contains("workspace_analyze_image", storeReceipts);
        Assert.Contains("workspace_analyze_images", storeReceipts);
        Assert.Contains("project_structure_node_create", storeReceipts);
        Assert.Contains("project_structure_asset_create", storeReceipts);
    }

    [Fact]
    public void Enrich_adds_visual_target_comparison_receipts_for_root_ui_qa_when_source_image_assets_exist()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var visualTarget = CreateVisualTargetNode(projectId, node.Id);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node, visualTarget], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly app with xUnit tests and a source visual target proposal."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "software-delivery",
                ProcessDefinitionId: null,
                ParentRunId: null,
                ParentStepId: null,
                ParentAssignment: null,
                IsSubprocess: false),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        AssertBrowserRuntimeProofReceipts(receiptMap, "qa-validation", expectsVisualComparison: true);
        AssertBrowserRuntimeProofReceipts(receiptMap, "qa-recheck", expectsVisualComparison: true);
    }

    [Fact]
    public void Enrich_adds_runtime_command_writeback_required_project_structure_receipts()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly calculator app with xUnit tests."
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "dotnet-runtime-command-writeback",
                ProcessDefinitionId: null,
                ParentRunId: ProcessRunId.New(),
                ParentStepId: ProcessStepInstanceId.New(),
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        var writeReceipts = Assert.Contains("write-run-command-nodes", receiptMap);
        Assert.Contains("project_structure_node_create", writeReceipts);
        Assert.Contains("project_structure_read", writeReceipts);
        Assert.Equal("blazorwasm", variables["DotNetAppTemplate"]);
    }

    [Fact]
    public void Enrich_replaces_parent_required_receipt_map_for_ui_screenshot_subprocess()
    {
        var contributor = new DotNetProcessLaunchVariableContributor();
        var projectId = Guid.NewGuid();
        var node = CreateNode(projectId);
        var surface = new ProjectStructureSurface(projectId, "Calculator", [node], [], null);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = @"C:\temp\CanDoItAll\Calculator",
            ["ProjectStructureContextSummary"] = "Blazor WebAssembly calculator app with xUnit tests.",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = JsonSerializer.Serialize(
                new Dictionary<string, string[]>
                {
                    ["qa-validation"] = ["workspace_dotnet_restore", "workspace_dotnet_build", "workspace_dotnet_test"]
                })
        };

        contributor.Enrich(
            new ProjectStructureProcessLaunchVariableContext(
                projectId,
                surface,
                node,
                "dotnet-ui-screenshot-writeback",
                ProcessDefinitionId: null,
                ParentRunId: ProcessRunId.New(),
                ParentStepId: ProcessStepInstanceId.New(),
                ParentAssignment: null,
                IsSubprocess: true),
            variables);

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        Assert.DoesNotContain("qa-validation", receiptMap.Keys);
        Assert.Contains("workspace_dotnet_run", Assert.Contains("capture-ui-screenshots", receiptMap));
        Assert.Contains("project_structure_asset_create", Assert.Contains("store-ui-screenshots", receiptMap));
    }

    private static void AssertValidationReceipts(IReadOnlyDictionary<string, string[]> receiptMap, string stepKey)
    {
        var receipts = Assert.Contains(stepKey, receiptMap);
        Assert.Contains("workspace_dotnet_restore", receipts);
        Assert.Contains("workspace_dotnet_build", receipts);
        Assert.Contains("workspace_dotnet_test", receipts);
    }

    private static void AssertValidationReceipts(IReadOnlyDictionary<string, JsonElement> receiptMap, string stepKey)
    {
        var receipts = ReadReceiptToolNames(Assert.Contains(stepKey, receiptMap));
        Assert.Contains("workspace_dotnet_restore", receipts);
        Assert.Contains("workspace_dotnet_build", receipts);
        Assert.Contains("workspace_dotnet_test", receipts);
    }

    private static void AssertBrowserRuntimeProofReceipts(
        IReadOnlyDictionary<string, string[]> receiptMap,
        string stepKey,
        bool expectsVisualComparison)
    {
        var receipts = Assert.Contains(stepKey, receiptMap);
        Assert.Contains("workspace_dotnet_run", receipts);
        Assert.Contains("browser_navigate", receipts);
        Assert.Contains("browser interaction proof", receipts);
        Assert.Contains("browser_evaluate", receipts);
        Assert.Contains("browser_snapshot", receipts);
        Assert.Contains("browser_take_screenshot", receipts);
        Assert.Contains("browser_console_messages", receipts);
        Assert.Contains("workspace_dotnet_stop", receipts);
        if (expectsVisualComparison)
        {
            Assert.Contains("workspace_inspect_image", receipts);
            Assert.Contains("workspace_analyze_image", receipts);
            Assert.Contains("workspace_analyze_images", receipts);
        }
        else
        {
            Assert.DoesNotContain("workspace_analyze_images", receipts);
        }
    }

    private static void AssertBrowserRuntimeProofReceipts(
        IReadOnlyDictionary<string, JsonElement> receiptMap,
        string stepKey,
        bool expectsVisualComparison)
    {
        var receipts = ReadReceiptToolNames(Assert.Contains(stepKey, receiptMap));
        Assert.Contains("workspace_dotnet_run", receipts);
        Assert.Contains("browser_navigate", receipts);
        Assert.Contains("browser interaction proof", receipts);
        Assert.Contains("browser_evaluate", receipts);
        Assert.Contains("browser_snapshot", receipts);
        Assert.Contains("browser_take_screenshot", receipts);
        Assert.Contains("browser_console_messages", receipts);
        Assert.Contains("workspace_dotnet_stop", receipts);
        if (expectsVisualComparison)
        {
            Assert.Contains("workspace_inspect_image", receipts);
            Assert.Contains("workspace_analyze_image", receipts);
            Assert.Contains("workspace_analyze_images", receipts);
        }
        else
        {
            Assert.DoesNotContain("workspace_analyze_images", receipts);
        }
    }

    private static void AssertAcceptanceBranchReceiptRules(
        IReadOnlyDictionary<string, JsonElement> receiptMap,
        string stepKey)
        => AssertAcceptanceBranchReceiptRules(receiptMap, stepKey, "quality-accepted");

    private static void AssertAcceptanceBranchReceiptRules(
        IReadOnlyDictionary<string, JsonElement> receiptMap,
        string stepKey,
        string acceptedBranchOutcomeKey)
    {
        var element = Assert.Contains(stepKey, receiptMap);
        Assert.Equal(JsonValueKind.Array, element.ValueKind);
        Assert.All(element.EnumerateArray(), rule =>
        {
            Assert.Equal(JsonValueKind.Object, rule.ValueKind);
            Assert.True(rule.TryGetProperty("toolName", out _));
            var branchKeys = rule.GetProperty("enforceBranchOutcomeKeys")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();
            Assert.Contains(acceptedBranchOutcomeKey, branchKeys);
        });

        Assert.Contains(
            "workspace_dotnet_run",
            ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(element));
    }

    private static void AssertBranchRoute(
        IReadOnlyDictionary<string, JsonElement[]> routeMap,
        string stepKey,
        string targetBranchOutcomeKey)
    {
        var routes = Assert.Contains(stepKey, routeMap);
        var expectedIssueCodes = new[]
        {
            ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing,
            "process.adapter.product_required_file_content_missing",
            ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
            ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing,
            ProcessCompletionDiagnosticCodes.UiInteractionEvidenceMissing,
            ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing
        };
        Assert.Equal(expectedIssueCodes.Length, routes.Length);
        Assert.All(expectedIssueCodes, expectedIssueCode =>
            Assert.Contains(routes, route =>
                string.Equals(
                    route.GetProperty("issueCode").GetString(),
                    expectedIssueCode,
                    StringComparison.Ordinal)));
        Assert.All(routes, route =>
        {
            Assert.Equal(targetBranchOutcomeKey, route.GetProperty("targetBranchOutcomeKey").GetString());
            var isPersistentMissingReceiptRoute = string.Equals(
                route.GetProperty("issueCode").GetString(),
                ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing,
                StringComparison.Ordinal);
            Assert.Equal(!isPersistentMissingReceiptRoute, route.GetProperty("requiresDefectEvidence").GetBoolean());
            Assert.Equal(isPersistentMissingReceiptRoute, route.GetProperty("onlyAfterAutomaticRetry").GetBoolean());
        });
    }

    private static void AssertIncompleteRepairRoute(
        IReadOnlyDictionary<string, JsonElement[]> routeMap,
        string stepKey)
    {
        var routes = Assert.Contains(stepKey, routeMap);
        Assert.Equal(3, routes.Length);
        Assert.Equal(
            [
                ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing,
                ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing,
                ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing
            ],
            routes.Select(route => route.GetProperty("issueCode").GetString()));
        Assert.All(routes, route =>
        {
            Assert.Equal("repair-attempt-incomplete", route.GetProperty("targetBranchOutcomeKey").GetString());
            Assert.False(route.GetProperty("requiresDefectEvidence").GetBoolean());
        });
    }

    private static void AssertFileContentEvidenceBranch(
        IReadOnlyDictionary<string, JsonElement[]> checkMap,
        string stepKey,
        string expectedBranchOutcomeKey)
    {
        var checks = Assert.Contains(stepKey, checkMap);
        Assert.NotEmpty(checks);
        Assert.All(checks, check =>
        {
            var branchOutcomeKeys = check.GetProperty("evidenceBranchOutcomeKeys")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();
            Assert.Contains(expectedBranchOutcomeKey, branchOutcomeKeys);
        });
    }

    private static IReadOnlyList<string> ReadReceiptToolNames(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : item.ValueKind == JsonValueKind.Object && item.TryGetProperty("toolName", out var toolName)
                    ? toolName.GetString() ?? string.Empty
                    : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static ProjectStructureNode CreateNode(Guid projectId)
        => new(
            "custom:calculator",
            $"project:{projectId:D}",
            ProjectObjectType.ProjectBlock,
            "delivery",
            "Main App",
            "Calculator",
            "Planned",
            "Build a Blazor WebAssembly calculator.",
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "calculator", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);

    private static ProjectStructureNode CreateComplexNode(Guid projectId)
        => new(
            "custom:tetris",
            $"project:{projectId:D}",
            ProjectObjectType.ProjectBlock,
            "delivery",
            "Main Game",
            "Tetris",
            "Planned",
            """
            Build a Blazor WebAssembly Tetris game.

            Acceptance criteria:
            - support falling tetromino gameplay with keyboard movement and rotation.
            - clear completed lines and update the score.
            - allow pause and resume without losing board state.
            """,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "gamepad-2", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);

    private static ProjectStructureNode CreateRequirementNode(
        Guid projectId,
        string id,
        string title,
        string notes)
        => new(
            id,
            $"project:{projectId:D}",
            ProjectObjectType.ProjectBlock,
            "architecture",
            title,
            "Required behavior",
            "Planned",
            notes,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "clipboard-check", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);

    private static ProjectStructureNode CreateVisualTargetNode(Guid projectId, string parentId)
        => new(
            "custom:visual-target",
            parentId,
            ProjectObjectType.ImageAsset,
            "generated",
            "Application layout proposal",
            "Source visual target",
            "Planned",
            "Reference visual target for implementation and QA.",
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            "project://visual-target",
            "reference",
            0,
            0,
            new ProjectObjectVisualProfile("rect", "danger", "image", string.Empty),
            [],
            "managed-files/project-media/images/project/proposal.png",
            0,
            "image/png",
            "proposal.png",
            string.Empty,
            [],
            0);
}
