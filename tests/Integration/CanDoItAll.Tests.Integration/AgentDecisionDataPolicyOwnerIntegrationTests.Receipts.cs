using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class AgentDecisionDataPolicyOwnerIntegrationTests {
    [Fact]
    public Task Real_committed_write_with_lost_acknowledgement_recovers_after_restart_and_later_edit_without_replay() => RunAsync(async fixture => {
        var original = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, Policy());
        var commits = 0;
        var faultedStore = new FileSandboxWorkspaceStore(fixture.Root, null, null, null, null, null,
            fileWriter: new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
                if (stage == DurableFileWriteStage.Committed) {
                    commits++;
                    throw new IOException("Lost acknowledgement after the actual atomic catalog replacement.");
                }
            }));
        var owner = new AgentDecisionDataPolicyOwner(faultedStore, fixture.Workspace, fixture.Generation);
        var unknown = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Unconfirmed>(await owner.ReplaceAsync(command, fixture.Token));
        Assert.Equal(command, unknown.Command);
        Assert.Equal(1, commits);
        var restarted = new AgentDecisionDataPolicyOwner(fixture.NewStore(), fixture.Workspace, fixture.Generation);
        var historical = Assert.IsType<AgentDecisionDataPolicyLookup.Accepted>(await restarted.LookupAsync(command, fixture.Token)).Receipt;
        Assert.Equal(command, historical.Command);
        Assert.Equal(original.Expectation.CatalogRevision.Next(), historical.Snapshot.Expectation.CatalogRevision);
        Assert.Equal(historical.Snapshot, await restarted.ReadAsync(fixture.AgentId, fixture.Token));
        var later = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await restarted.ReplaceAsync(
            new(Guid.NewGuid(), historical.Snapshot.Expectation, new(ReadBasisMode.StepSnapshot)), fixture.Token));
        var bytes = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        Assert.Equal(historical, Assert.IsType<AgentDecisionDataPolicyLookup.Accepted>(
            await new AgentDecisionDataPolicyOwner(fixture.NewStore(), fixture.Workspace, fixture.Generation).LookupAsync(command, fixture.Token)).Receipt);
        Assert.NotEqual(later.Snapshot, historical.Snapshot);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
        Assert.Equal(AgentDecisionDataPolicyRejection.Conflict,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await restarted.ReplaceAsync(command, fixture.Token)).Reason);
        Assert.Equal(2, (await fixture.CatalogAsync()).AgentDecisionDataPolicyReceipts!.Receipts.Length);
    });

    [Fact]
    public Task Generic_catalog_full_document_and_delete_writers_cannot_inject_erase_or_rewrite_historical_receipts() => RunAsync(async fixture => {
        var original = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, Policy());
        Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(command, fixture.Token));
        var receipt = Assert.IsType<AgentDecisionDataPolicyLookup.Accepted>(await fixture.Owner.LookupAsync(command, fixture.Token)).Receipt;
        var injected = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), command.Expectation, new());
        await fixture.Store.UpdateCatalogAsync(current => current with {
            AgentDecisionDataPolicyReceipts = AgentDecisionDataPolicyReceiptLog.Empty.Append(injected, DateTimeOffset.UtcNow),
            Agents = current.Agents.Select(agent => agent.Id == fixture.AgentId ? agent with { Summary = "Ordinary write" } : agent).ToArray()
        }, fixture.Token);
        var document = await fixture.Store.LoadAsync(fixture.Token);
        await fixture.Store.SaveAsync(document with { AgentDecisionDataPolicyReceipts = null }, fixture.Token);
        var catalog = await fixture.CatalogAsync();
        await fixture.NewStore().SaveCatalogAsync(catalog with {
            AgentDecisionDataPolicyReceipts = AgentDecisionDataPolicyReceiptLog.Empty,
            Agents = catalog.Agents.Where(agent => agent.Id != fixture.AgentId).ToArray()
        }, fixture.Token);
        Assert.Equal(receipt, Assert.Single((await fixture.CatalogAsync()).AgentDecisionDataPolicyReceipts!.Receipts));
        Assert.Equal(receipt, Assert.IsType<AgentDecisionDataPolicyLookup.Accepted>(await fixture.Owner.LookupAsync(command, fixture.Token)).Receipt);
        Assert.IsType<AgentDecisionDataPolicyLookup.NotObserved>(await fixture.Owner.LookupAsync(injected, fixture.Token));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token));
    });

    [Fact]
    public Task New_active_generation_can_read_original_receipt_but_cannot_write_original_command_or_cross_workspace() => RunAsync(async fixture => {
        var original = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, Policy());
        var accepted = Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(command, fixture.Token));
        fixture.Generation.Advance();
        Assert.IsType<AgentDecisionDataPolicyLookup.ContextChanged>(await fixture.Owner.LookupAsync(command, fixture.Token));
        var identity = new AgentExecutionActivityWorkspaceIdentity(fixture.Workspace.DatabaseProfileId,
            fixture.Workspace.WorkspaceScope, fixture.Generation.GetGeneration());
        var restarted = new AgentDecisionDataPolicyOwner(fixture.NewStore(), identity, fixture.Generation);
        Assert.Equal(accepted.Snapshot, Assert.IsType<AgentDecisionDataPolicyLookup.Accepted>(await restarted.LookupAsync(command, fixture.Token)).Receipt.Snapshot);
        Assert.Equal(AgentDecisionDataPolicyRejection.ContextChanged,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await restarted.ReplaceAsync(command, fixture.Token)).Reason);
        foreach (var workspace in new[] {
            new AgentExecutionActivityWorkspaceIdentity(Guid.NewGuid(), identity.WorkspaceScope, identity.DatabaseProfileGeneration),
            new(identity.DatabaseProfileId, WorkspaceScopeDescriptor.Project("other"), identity.DatabaseProfileGeneration)
        }) {
            var other = new AgentDecisionDataPolicyOwner(fixture.NewStore(), workspace, fixture.Generation);
            Assert.IsType<AgentDecisionDataPolicyLookup.ContextChanged>(await other.LookupAsync(command, fixture.Token));
        }
    });

    [Fact]
    public Task Receipt_capacity_refuses_before_any_write_and_expired_evidence_is_pruned_only_by_dedicated_publication() => RunAsync(async fixture => {
        var original = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var now = DateTimeOffset.UtcNow;
        var log = new AgentDecisionDataPolicyReceiptLog(Enumerable.Range(0, AgentDecisionDataPolicyReceiptLog.MaximumReceipts)
            .Select(_ => new AgentDecisionDataPolicyReceipt(new(Guid.NewGuid(), original.Expectation, Policy()), now)).ToImmutableArray());
        var catalog = (await fixture.CatalogAsync()) with { AgentDecisionDataPolicyReceipts = log };
        var options = new FileSandboxWorkspaceJsonStore().SerializerOptions;
        await File.WriteAllTextAsync(fixture.Layout.CatalogPath, JsonSerializer.Serialize(catalog, options), fixture.Token);
        var bytes = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, Policy());
        Assert.Equal(AgentDecisionDataPolicyRejection.ReceiptCapacityExceeded,
            Assert.IsType<AgentDecisionDataPolicySaveOutcome.Rejected>(await fixture.Owner.ReplaceAsync(command, fixture.Token)).Reason);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
        var expired = new AgentDecisionDataPolicyReceiptLog(log.Receipts.Select(receipt => new AgentDecisionDataPolicyReceipt(
            receipt.Command, now - AgentDecisionDataPolicyReceiptLog.Retention - TimeSpan.FromDays(1))).ToImmutableArray());
        await File.WriteAllTextAsync(fixture.Layout.CatalogPath, JsonSerializer.Serialize(catalog with { AgentDecisionDataPolicyReceipts = expired }, options), fixture.Token);
        Assert.IsType<AgentDecisionDataPolicyLookup.NotObserved>(await fixture.Owner.LookupAsync(expired.Receipts[0].Command, fixture.Token));
        var ordinary = await fixture.Store.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => agent.Id == fixture.AgentId ? agent with { Summary = "After expiration" } : agent).ToArray()
        }, fixture.Token);
        Assert.Equal(expired.Receipts.ToArray(), ordinary.AgentDecisionDataPolicyReceipts!.Receipts.ToArray());
        var current = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var next = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), current.Expectation, Policy());
        Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(next, fixture.Token));
        Assert.Equal(next, Assert.Single((await fixture.CatalogAsync()).AgentDecisionDataPolicyReceipts!.Receipts).Command);
    });

    [Fact]
    public Task Corrupt_receipt_log_is_unavailable_never_missing_or_repaired_by_lookup() => RunAsync(async fixture => {
        var original = await fixture.Owner.ReadAsync(fixture.AgentId, fixture.Token);
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), original.Expectation, Policy());
        Assert.IsType<AgentDecisionDataPolicySaveOutcome.Accepted>(await fixture.Owner.ReplaceAsync(command, fixture.Token));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(fixture.Layout.CatalogPath, fixture.Token))!.AsObject();
        foreach (var corrupt in new[] { "null", "[null]", "[{\"schemaVersion\":99}]" }) {
            json["agentDecisionDataPolicyReceipts"] = JsonNode.Parse(corrupt);
            await File.WriteAllTextAsync(fixture.Layout.CatalogPath, json.ToJsonString(), fixture.Token);
            var before = await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token);
            var result = Assert.IsType<AgentDecisionDataPolicyLookup.Unavailable>(await fixture.Owner.LookupAsync(command, fixture.Token));
            Assert.NotNull(result.Failure);
            Assert.Equal(before, await File.ReadAllBytesAsync(fixture.Layout.CatalogPath, fixture.Token));
        }
    });
}
