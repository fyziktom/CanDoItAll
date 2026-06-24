using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessManagerChatPromptClassifierTests
{
    [Fact]
    public void ShouldDisableRuntimeTools_returns_true_for_telemetry_prompt_with_already()
    {
        var result = ProcessManagerChatPromptClassifier.ShouldDisableRuntimeTools(
            "Report the selected run total tokens, input tokens, cached input tokens, output tokens, actual cost, completed status, and current operator actions. Use only the runtime telemetry already in this manager chat context.");

        Assert.True(result);
    }

    [Fact]
    public void ShouldDisableRuntimeTools_returns_false_when_prompt_asks_for_artifacts()
    {
        var result = ProcessManagerChatPromptClassifier.ShouldDisableRuntimeTools(
            "Read the screenshot artifact and then report the token usage.");

        Assert.False(result);
    }

    [Fact]
    public void ShouldDisableRuntimeTools_returns_false_for_non_telemetry_prompt()
    {
        var result = ProcessManagerChatPromptClassifier.ShouldDisableRuntimeTools(
            "What concrete next step should the manager take?");

        Assert.False(result);
    }
}
