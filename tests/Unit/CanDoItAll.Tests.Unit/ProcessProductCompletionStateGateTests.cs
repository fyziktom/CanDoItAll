using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessProductCompletionStateGateTests
{
    [Theory]
    [InlineData("The required current-run write receipt is captured for the primary managed ref.")]
    [InlineData("All required validation receipts were produced successfully.")]
    [InlineData("Applied the focused deterministic persistence-proof repair, verified the changed test source, and obtained current-execution restore, build, and test receipts. The xUnit project now reports 8 passed, 0 failed, including the revised unavailable IndexedDB-store path.")]
    public void Captured_required_receipt_is_not_an_unresolved_blocker(string line)
    {
        Assert.False(ProcessProductCompletionStateGate.DeclaresUnresolvedBlocker(line));
    }

    [Theory]
    [InlineData("The required current-run write receipt is missing.")]
    [InlineData("Required restore, build, and test receipts are not yet captured.")]
    [InlineData("Missing required screenshot receipt browser_take_screenshot.")]
    [InlineData("The process receipt is unavailable.")]
    [InlineData("The validation receipts remain absent.")]
    [InlineData("The required browser receipt was not yet recorded.")]
    public void Missing_required_receipt_is_an_unresolved_blocker(string line)
    {
        Assert.True(ProcessProductCompletionStateGate.DeclaresUnresolvedBlocker(line));
    }

    [Fact]
    public void Planning_step_does_not_treat_future_receipts_as_a_current_blocker()
    {
        Assert.False(ProcessProductCompletionStateGate.DeclaresUnresolvedBlocker(
            "The generated-product validation receipts are not yet captured and remain a downstream proof prerequisite.",
            requiresCurrentRunProof: false));
    }

    [Fact]
    public void Planning_step_still_rejects_an_explicit_unresolved_blocker()
    {
        Assert.True(ProcessProductCompletionStateGate.DeclaresUnresolvedBlocker(
            "The plan has an unresolved blocker: contradictory authoritative scope.",
            requiresCurrentRunProof: false));
    }
}
