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
}
