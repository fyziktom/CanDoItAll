using System.Text.Json;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRequiredRuntimeToolNamesTests
{
    [Fact]
    public void FromProductCompletionRequiredToolReceipts_filters_receipt_predicates_from_runtime_tool_names()
    {
        var requiredReceipts = JsonSerializer.Serialize(new[]
        {
            "template=sln",
            "template=blazorwasm",
            "workspace_dotnet_new|name=Calculator,parentDirectory=external-target/C/programovani/dotnet,template=sln",
            "workspace-pwsh-run-script",
            "browser_take_screenshot",
            "project_structure_asset_create",
            "exit=0"
        });

        var toolNames = ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(requiredReceipts);

        Assert.Equal(
            [
                "browser_take_screenshot",
                "project_structure_asset_create",
                "workspace_dotnet_new",
                "workspace_pwsh_run_script"
            ],
            toolNames);
    }

    [Fact]
    public void FromUnconditionalProductCompletionRequiredToolReceipts_excludes_branch_scoped_rules()
    {
        var requiredReceipts = JsonSerializer.Serialize(new object[]
        {
            new
            {
                ToolReceipt = "workspace_dotnet_restore|exit=0"
            },
            new
            {
                ToolReceipt = "workspace_dotnet_test|exit=0",
                ApplicableBranchOutcomeKeys = new[] { "quality-accepted" }
            },
            new
            {
                ToolReceipt = "browser_take_screenshot",
                SkippedBranchOutcomeKeys = new[] { "repair-required" }
            }
        });

        var toolNames = ProcessRequiredRuntimeToolNames
            .FromUnconditionalProductCompletionRequiredToolReceipts(requiredReceipts);

        Assert.Equal(["workspace_dotnet_restore"], toolNames);
    }

    [Fact]
    public void FromUnconditionalCapabilityScope_excludes_branch_scoped_receipts()
    {
        var capabilityScope = new ProcessCapabilityScope
        {
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    Key = "restore",
                    ToolName = "workspace_dotnet_restore"
                },
                new ProcessRequiredToolReceipt
                {
                    Key = "quality-screenshot",
                    ToolName = "browser_take_screenshot",
                    ApplicableBranchOutcomeKeys = ["quality-accepted"]
                }
            ]
        };

        var allToolNames = ProcessRequiredRuntimeToolNames.FromCapabilityScope(capabilityScope);
        var unconditionalToolNames = ProcessRequiredRuntimeToolNames
            .FromUnconditionalCapabilityScope(capabilityScope);

        Assert.Equal(["browser_take_screenshot", "workspace_dotnet_restore"], allToolNames);
        Assert.Equal(["workspace_dotnet_restore"], unconditionalToolNames);
    }
}
