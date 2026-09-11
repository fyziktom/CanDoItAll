using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafWorkspaceToolResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory, CollectionTargetChange.None)]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory, CollectionTargetChange.Remove)]
    [InlineData(ToolContractCatalog.WorkspaceListDirectory, CollectionTargetChange.Reparse)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles, CollectionTargetChange.None)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles, CollectionTargetChange.Remove)]
    [InlineData(ToolContractCatalog.WorkspaceListFiles, CollectionTargetChange.Reparse)]
    [InlineData(ToolContractCatalog.WorkspaceSearch, CollectionTargetChange.None)]
    [InlineData(ToolContractCatalog.WorkspaceSearch, CollectionTargetChange.Remove)]
    [InlineData(ToolContractCatalog.WorkspaceSearch, CollectionTargetChange.Reparse)]
    public async Task Durable_registered_collection_replay_checks_original_physical_children_after_final_acknowledgement_loss(
        string toolName, CollectionTargetChange change) {
        await using var fixture = await Fixture.CreateAsync(toolName);
        fixture.ReadPath = "sources/nested";
        var directory = Path.Combine(fixture.Journal.WorkspaceRoot, "sources", "nested");
        Directory.CreateDirectory(directory);
        var original = Path.Combine(directory, OperatingSystem.IsWindows() ? "literal-name.txt" : "literal\\name.txt");
        await File.WriteAllTextAsync(original, "original retained collection text");
        var decoyDirectory = Path.Combine(directory, "literal");
        Directory.CreateDirectory(decoyDirectory);
        var decoy = Path.Combine(decoyDirectory, "name.txt");
        await File.WriteAllTextAsync(decoy, "unselected decoy");
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(2, saved.DisclosureEvidence!.Version);
        var originalBytes = saved.DisclosureEvidence.PayloadJson;
        var evidence = WorkspaceToolResultEvidence.Read(saved.DisclosureEvidence);
        Assert.Equal(WorkspaceToolResultEvidenceState.Complete, evidence.State);
        Assert.Contains(original, evidence.Selection!.FullPaths);
        Assert.DoesNotContain("readSelection", saved.Result!.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Journal.WorkspaceRoot, saved.Result.PayloadJson, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows()) {
            Assert.Contains("sources/nested/literal/name.txt", saved.Result.PayloadJson, StringComparison.Ordinal);
            Assert.Contains('\\', Path.GetFileName(original));
        }
        if (change == CollectionTargetChange.None) {
            await File.WriteAllTextAsync(original, "later human edit");
        } else {
            File.Delete(original);
            if (change == CollectionTargetChange.Reparse) {
                File.CreateSymbolicLink(original, decoy);
            }
        }
        try {
            var client = new ToolClient(toolName, readPath: fixture.ReadPath);
            if (change == CollectionTargetChange.None) {
                var response = await fixture.ExecuteAsync(client);
                Assert.Contains("completed", response.ResponseText, StringComparison.Ordinal);
                Assert.Equal(1, client.Requests);
                if (toolName == ToolContractCatalog.WorkspaceSearch) {
                    Assert.Contains("original retained collection text", Assert.Single(client.Inputs), StringComparison.Ordinal);
                    Assert.DoesNotContain("later human edit", Assert.Single(client.Inputs), StringComparison.Ordinal);
                }
            } else {
                var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(client));
                Assert.Contains("workspace.result-disclosure-denied", Codes(denied));
                Assert.Equal(0, client.Requests);
            }
            Assert.True(File.Exists(decoy));
            var reopened = await fixture.ProposalAsync();
            Assert.Equal(saved, reopened);
            Assert.Equal(originalBytes, reopened.DisclosureEvidence!.PayloadJson);
            Assert.Equal(saved.Result.PayloadJson, reopened.Result!.PayloadJson);
        } finally {
            if (change == CollectionTargetChange.Reparse) {
                File.Delete(original);
            }
        }
    }

    public enum CollectionTargetChange { None, Remove, Reparse }
}
