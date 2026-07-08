using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
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

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep]);

        Assert.NotNull(receiptMap);
        AssertValidationReceipts(receiptMap, "qa-validation");
        AssertValidationReceipts(receiptMap, "quality-repair");
        AssertValidationReceipts(receiptMap, "qa-recheck");
        AssertBrowserRuntimeProofReceipts(receiptMap, "qa-validation", expectsVisualComparison: false);
        AssertBrowserRuntimeProofReceipts(receiptMap, "qa-recheck", expectsVisualComparison: false);

        var fileContentMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement[]>>(
            variables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep]);

        Assert.NotNull(fileContentMap);
        var qaChecks = Assert.Contains("qa-validation", fileContentMap);
        Assert.Contains(qaChecks, check =>
            check.GetProperty("mustExist").GetBoolean() == false &&
            check.GetProperty("enforceBranchOutcomeKeys").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "quality-accepted", StringComparison.Ordinal)) &&
            check.GetProperty("forbiddenTextAny").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "@page \"/counter\"", StringComparison.Ordinal)));

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

        var receiptMap = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
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

    private static void AssertBrowserRuntimeProofReceipts(
        IReadOnlyDictionary<string, string[]> receiptMap,
        string stepKey,
        bool expectsVisualComparison)
    {
        var receipts = Assert.Contains(stepKey, receiptMap);
        Assert.Contains("workspace_dotnet_run", receipts);
        Assert.Contains("browser_navigate", receipts);
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
