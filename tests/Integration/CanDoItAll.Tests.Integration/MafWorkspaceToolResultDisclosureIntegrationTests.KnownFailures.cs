using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafWorkspaceToolResultDisclosureIntegrationTests {
    public enum RejectedReadPath {
        ParentTraversal,
        OutsideWorkspace,
        MissingFile
    }

    [Theory]
    [InlineData(RejectedReadPath.ParentTraversal)]
    [InlineData(RejectedReadPath.OutsideWorkspace)]
    [InlineData(RejectedReadPath.MissingFile)]
    public async Task A_rejected_workspace_read_is_replayed_after_its_approval_without_reading_again(RejectedReadPath rejected) {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceReadFile);
        fixture.ReadPath = rejected switch {
            RejectedReadPath.ParentTraversal => "../outside-the-workspace.txt",
            RejectedReadPath.OutsideWorkspace => Path.Combine(Path.GetTempPath(), $"candoitall-outside-{Guid.NewGuid():N}", "private.txt"),
            _ => "missing-source.txt"
        };

        await fixture.CompleteThroughLostFinalAcknowledgementAsync();

        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolEffectState.None, saved.EffectState);
        Assert.NotNull(saved.DisclosureEvidence);
        var restored = new ToolClient(fixture.ToolName, readPath: fixture.ReadPath);
        var replayed = await fixture.ExecuteAsync(restored);
        Assert.Contains("completed", replayed.ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, restored.Requests);
        Assert.DoesNotContain("original reviewed content", Assert.Single(restored.Inputs), StringComparison.Ordinal);
        Assert.Equal(saved, await fixture.ProposalAsync());

        await fixture.SaveActorAsync(toolAllowed: false);
        var deniedClient = new ToolClient(fixture.ToolName, readPath: fixture.ReadPath);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(deniedClient));
        Assert.Contains("workspace.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }
}
