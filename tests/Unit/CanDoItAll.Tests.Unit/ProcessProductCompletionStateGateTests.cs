using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessProductCompletionStateGateTests
{
    [Theory]
    [InlineData("The required current-run write receipt is captured for the primary managed ref.")]
    [InlineData("All required validation receipts were produced successfully.")]
    public void Captured_required_receipt_is_not_an_unresolved_blocker(string line)
    {
        Assert.False(ProcessProductCompletionStateGate.DeclaresUnresolvedBlocker(line));
    }

    [Theory]
    [InlineData("The required current-run write receipt is missing.")]
    [InlineData("Required restore, build, and test receipts are not yet captured.")]
    [InlineData("Missing required screenshot receipt browser_take_screenshot.")]
    public void Missing_required_receipt_is_an_unresolved_blocker(string line)
    {
        Assert.True(ProcessProductCompletionStateGate.DeclaresUnresolvedBlocker(line));
    }
}
