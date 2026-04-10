using Bunit;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasSelectionPanelTests
{
    [Fact]
    public void Runtime_actions_disable_invalid_transitions_for_ready_steps()
    {
        using var context = new TestContext();
        var runtimeStep = new ProcessStepRunViewModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            1,
            "Route requested revision",
            ProcessStepKind.Decision,
            ProcessStepRunStatus.Ready,
            "Routing owner",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            0,
            0,
            0,
            0,
            ProcessCapabilityGapSeverity.None,
            []);

        var cut = context.RenderComponent<ProcessCanvasSelectionPanel>(
            parameters => parameters
                .Add(component => component.IsRuntime, true)
                .Add(component => component.RuntimeStep, runtimeStep));

        var buttons = cut.FindAll("button");
        var startButton = Assert.Single(buttons, button => button.TextContent.Trim() == "Start");
        var completeButton = Assert.Single(buttons, button => button.TextContent.Trim() == "Complete");
        var blockButton = Assert.Single(buttons, button => button.TextContent.Trim() == "Block");

        Assert.False(startButton.HasAttribute("disabled"));
        Assert.True(completeButton.HasAttribute("disabled"));
        Assert.False(blockButton.HasAttribute("disabled"));
    }
}
