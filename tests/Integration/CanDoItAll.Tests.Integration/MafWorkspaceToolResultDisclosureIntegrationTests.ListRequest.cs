using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafWorkspaceToolResultDisclosureIntegrationTests {
    [Theory]
    [InlineData("sources/**", "sources", false)]
    [InlineData("sources/**", "sources", true)]
    [InlineData("sources**", "sources", false)]
    [InlineData("sources**", "sources", true)]
    [InlineData("**/*.txt", ".", false)]
    [InlineData("**/*.txt", ".", true)]
    public async Task Approved_registered_listing_preserves_shorthand_through_durable_result_restart_and_current_target_recheck(
        string requested, string normalizedRoot, bool removeOriginal) {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceListFiles);
        fixture.ReadPath = requested;
        var folder = Path.Combine(fixture.Journal.WorkspaceRoot, "sources", "nested");
        Directory.CreateDirectory(folder);
        var original = Path.Combine(folder, "retained.txt");
        await File.WriteAllTextAsync(original, "original retained selection");
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(ExecutionApprovalStatus.Approved, saved.ApprovalStatus);
        Assert.Equal(saved.Payload.Digest, saved.ApprovedDigest);
        Assert.Contains(requested, saved.Payload.ArgumentsJson, StringComparison.Ordinal);
        var evidence = WorkspaceToolResultEvidence.Read(saved.DisclosureEvidence!);
        Assert.Equal(2, saved.DisclosureEvidence!.Version);
        Assert.Equal(WorkspaceToolResultEvidenceState.Complete, evidence.State);
        Assert.Equal(normalizedRoot, evidence.Selection!.Root.RequestPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(fixture.Journal.WorkspaceRoot, normalizedRoot)), evidence.Selection.Root.FullPath);
        Assert.Contains(original, evidence.Selection.FullPaths);
        if (removeOriginal) {
            File.Delete(original);
        } else {
            await File.WriteAllTextAsync(original, "later edit");
            await File.WriteAllTextAsync(Path.Combine(folder, "added-after-result.txt"), "not in original selection");
        }
        var client = new ToolClient(ToolContractCatalog.WorkspaceListFiles, readPath: requested);
        if (removeOriginal) {
            var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(client));
            Assert.Contains("workspace.result-disclosure-denied", Codes(denied));
            Assert.Equal(0, client.Requests);
        } else {
            var restored = await fixture.ExecuteAsync(client);
            Assert.Contains("completed", restored.ResponseText, StringComparison.Ordinal);
            Assert.Equal(1, client.Requests);
            Assert.Contains("retained.txt", Assert.Single(client.Inputs), StringComparison.Ordinal);
            Assert.DoesNotContain("added-after-result.txt", Assert.Single(client.Inputs), StringComparison.Ordinal);
        }
        var reopened = await fixture.ProposalAsync();
        Assert.Equal(saved, reopened);
        Assert.Equal(saved.Payload.ArgumentsJson, reopened.Payload.ArgumentsJson);
        Assert.Equal(saved.DisclosureEvidence, reopened.DisclosureEvidence);
        Assert.Equal(saved.Result, reopened.Result);
    }
}
