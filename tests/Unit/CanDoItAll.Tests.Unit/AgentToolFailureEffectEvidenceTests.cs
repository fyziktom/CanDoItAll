using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentToolFailureEffectEvidenceTests
{
    [Fact]
    public void Input_validation_rejection_maps_to_a_proven_no_effect_failure()
    {
        var exception = AgentToolInputValidationException.Create(
            "Spreadsheet range 'A1:L8' has 8 row(s), but 10 row(s) were supplied. Use a range with at least 10 rows, then retry.");

        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Equal(AgentToolEffectState.None, result.EffectState);
        Assert.True(result.CanRetryWithCorrectedInput);
    }

    [Fact]
    public void Conflict_rejection_maps_to_a_proven_no_effect_failure()
    {
        var exception = AgentToolConflictException.Create(
            "The output workbook already exists and overwrite is false. Choose another outputWorkbookPath or set overwrite to true, then retry.");

        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.Equal(AgentToolConflictException.FailureCode, result.ErrorCode);
        Assert.Equal(AgentToolEffectState.None, result.EffectState);
        Assert.True(result.CanRetryWithCorrectedInput);
    }

    [Theory]
    [InlineData("file-read-disabled")]
    [InlineData("file-write-disabled")]
    [InlineData("external-target-read-only")]
    [InlineData("external-target-not-authorized")]
    [InlineData("recursive-delete-read-only-ancestor")]
    [InlineData("grounded-target-root-delete")]
    [InlineData("protected-product-directory-delete")]
    [InlineData("read-only-ancestor-mutation")]
    public void Access_guard_denials_raised_before_the_operation_map_to_a_proven_no_effect_failure(string denial)
    {
        var mapped = MafAgentToolFailureMapper.TryMap(CreateAccessDenial(denial), out var result);

        Assert.True(mapped, denial);
        Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, result.ErrorCode);
        Assert.Equal(AgentToolEffectState.None, result.EffectState);
    }

    [Theory]
    [InlineData("inaccessible-path")]
    [InlineData("inaccessible-paths")]
    public void Access_failures_observed_while_the_operation_ran_keep_an_unknown_effect_state(string denial)
    {
        var mapped = MafAgentToolFailureMapper.TryMap(CreateAccessDenial(denial), out var result);

        Assert.True(mapped, denial);
        Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, result.ErrorCode);
        Assert.Equal(AgentToolEffectState.Unknown, result.EffectState);
    }

    private static WorkspaceToolAccessDeniedException CreateAccessDenial(string denial)
        => denial switch
        {
            "file-read-disabled" => WorkspaceToolAccessDeniedException.FileReadDisabled(),
            "file-write-disabled" => WorkspaceToolAccessDeniedException.FileWriteDisabled(),
            "external-target-read-only" => WorkspaceToolAccessDeniedException.ExternalTargetReadOnly("external-target/app"),
            "external-target-not-authorized" => WorkspaceToolAccessDeniedException.ExternalTargetNotAuthorized("external-target/app"),
            "recursive-delete-read-only-ancestor" => WorkspaceToolAccessDeniedException.RecursiveDeleteReadOnlyAncestor("external-target/app"),
            "grounded-target-root-delete" => WorkspaceToolAccessDeniedException.GroundedTargetRootDelete("external-target/app"),
            "protected-product-directory-delete" => WorkspaceToolAccessDeniedException.ProtectedProductDirectoryDelete("external-target/app"),
            "read-only-ancestor-mutation" => WorkspaceToolAccessDeniedException.ReadOnlyAncestorMutation(
                WorkspaceReadOnlyAncestorMutationOperation.CopyOrReplace,
                "external-target/app"),
            "inaccessible-path" => WorkspaceToolAccessDeniedException.InaccessiblePath("docs/reports"),
            "inaccessible-paths" => WorkspaceToolAccessDeniedException.InaccessiblePaths("docs/reports", "docs/archive"),
            _ => throw new ArgumentOutOfRangeException(nameof(denial), denial, "Unknown access denial case.")
        };

    [Fact]
    public void Workspace_spreadsheet_retry_for_the_same_workbook_shares_the_operation_identity()
    {
        var rejected = CreateSpreadsheetArguments("garden/watering-plan.xlsx", "A1:L8");
        var corrected = CreateSpreadsheetArguments("garden/watering-plan.xlsx", "A1:L10");
        var otherWorkbook = CreateSpreadsheetArguments("garden/production-plan.xlsx", "A1:L10");
        var otherOutput = CreateSpreadsheetArguments("garden/watering-plan.xlsx", "A1:L10");
        otherOutput["outputWorkbookPath"] = "garden/watering-plan-v2.xlsx";

        var rejectedKey = MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteSpreadsheet, rejected);
        var correctedKey = MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteSpreadsheet, corrected);
        var otherWorkbookKey = MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteSpreadsheet, otherWorkbook);
        var otherOutputKey = MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteSpreadsheet, otherOutput);

        Assert.NotEmpty(rejectedKey);
        Assert.Equal(rejectedKey, correctedKey);
        Assert.NotEqual(correctedKey, otherWorkbookKey);
        Assert.NotEqual(correctedKey, otherOutputKey);
    }

    [Fact]
    public void Workspace_file_tools_correlate_by_their_target_paths()
    {
        var firstWrite = new AIFunctionArguments { ["path"] = "notes/summary.md", ["content"] = "draft" };
        var secondWrite = new AIFunctionArguments { ["path"] = "notes/summary.md", ["content"] = "final" };
        var otherWrite = new AIFunctionArguments { ["path"] = "notes/other.md", ["content"] = "final" };
        var copy = new AIFunctionArguments { ["sourcePath"] = "notes/summary.md", ["destinationPath"] = "archive/summary.md" };
        var reversedCopy = new AIFunctionArguments { ["sourcePath"] = "archive/summary.md", ["destinationPath"] = "notes/summary.md" };

        Assert.Equal(
            MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteFile, firstWrite),
            MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteFile, secondWrite));
        Assert.NotEqual(
            MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteFile, firstWrite),
            MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceWriteFile, otherWrite));
        Assert.NotEmpty(MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceCopyPath, copy));
        Assert.NotEqual(
            MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceCopyPath, copy),
            MafToolInvocationCorrelationKey.Create(ToolContractCatalog.WorkspaceCopyPath, reversedCopy));
        Assert.Empty(MafToolInvocationCorrelationKey.Create(
            ToolContractCatalog.WorkspaceCommandRun,
            new AIFunctionArguments { ["command"] = "dotnet --info" }));
    }

    [Fact]
    public void Different_workspace_tools_on_the_same_target_do_not_share_an_operation_identity()
    {
        const string target = "notes/plan.xlsx";
        var write = MafToolInvocationCorrelationKey.Create(
            ToolContractCatalog.WorkspaceWriteFile,
            new AIFunctionArguments { ["path"] = target, ["content"] = "draft" });
        var delete = MafToolInvocationCorrelationKey.Create(
            ToolContractCatalog.WorkspaceDeletePath,
            new AIFunctionArguments { ["path"] = target });
        var spreadsheet = MafToolInvocationCorrelationKey.Create(
            ToolContractCatalog.WorkspaceWriteSpreadsheet,
            CreateSpreadsheetArguments(target, "A1:B2"));

        Assert.NotEmpty(write);
        Assert.NotEmpty(delete);
        Assert.NotEmpty(spreadsheet);
        Assert.NotEqual(write, delete);
        Assert.NotEqual(write, spreadsheet);
        Assert.NotEqual(delete, spreadsheet);
    }

    [Fact]
    public void Path_spelling_variants_are_distinct_operation_identities()
    {
        // The key trims only; separator and case variants stay distinct (a conservative false negative), so a corrected
        // retry must repeat the exact target spelling to resolve its earlier rejection.
        static string Key(string path) => MafToolInvocationCorrelationKey.Create(
            ToolContractCatalog.WorkspaceWriteFile,
            new AIFunctionArguments { ["path"] = path, ["content"] = "final" });

        Assert.Equal(Key("notes/summary.md"), Key("  notes/summary.md  "));
        Assert.NotEqual(Key("notes/summary.md"), Key("notes\\summary.md"));
        Assert.NotEqual(Key("notes/summary.md"), Key("Notes/Summary.md"));
    }

    [Fact]
    public void Path_arguments_do_not_change_the_identity_of_non_workspace_tools()
    {
        var projectId = Guid.NewGuid();
        var first = new AIFunctionArguments { ["projectId"] = projectId, ["parentNodeKey"] = "main", ["path"] = "docs/a.md" };
        var second = new AIFunctionArguments { ["projectId"] = projectId, ["parentNodeKey"] = "main", ["path"] = "docs/b.md" };

        var firstKey = MafToolInvocationCorrelationKey.Create("project_structure_node_create", first);
        var secondKey = MafToolInvocationCorrelationKey.Create("project_structure_node_create", second);

        Assert.NotEmpty(firstKey);
        Assert.Equal(firstKey, secondKey);
    }

    private static AIFunctionArguments CreateSpreadsheetArguments(string workbookPath, string rangeAddress)
        => new()
        {
            ["workbookPath"] = workbookPath,
            ["worksheetName"] = "Plan",
            ["createWorkbookIfMissing"] = true,
            ["overwrite"] = true,
            ["rangeWrites"] = JsonSerializer.SerializeToElement(new[]
            {
                new { rangeAddress, values = new[] { new[] { "Plant", "Watering" } } }
            })
        };
}
