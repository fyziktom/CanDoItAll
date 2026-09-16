using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cached_result_with_a_catalog_lease_never_reenters_its_journal_workspace_lock(bool independentCatalogStore) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var effects = new List<string>();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, effects);
        var catalog = independentCatalogStore ? fixture.NewStore() : fixture.Store;
        var readChecks = 0;
        runtime.Capabilities.RuntimeToolMetadata.Add(new("fixture-provider", "generic_fixture_read",
            AgentRuntimeToolOperationKind.Read, false) {
            AuthorizeResultDisclosureAsync = async (disclosure, token) => {
                Assert.Equal("saved-original-result", disclosure.Result.GetString());
                readChecks++;
                return await catalog.AcquireAgentReadLeaseAsync(fixture.Agent.Id, token);
            }
        });
        var call = new FunctionCallContent("read", "generic_fixture_read", new Dictionary<string, object?>());
        var requestDigest = AgentToolProtocolEnvelope.ComputeDigest("read-lease-fixture-request");
        var journal = fixture.NewJournal();
        AgentToolBusinessIntentId originalIntent;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(requestDigest, AgentToolAdmissionJournalFixture.Envelope(), [call], default);
            using var firstEffect = AgentToolInvocationEffectScope.Begin();
            var first = await opened.Context.InvokeAsync(call, _ => {
                effects.Add("read");
                return ValueTask.FromResult<object?>("saved-original-result");
            }, firstEffect, default);
            Assert.Equal("saved-original-result", first);
            originalIntent = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals).IntentId;
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var replayLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var replayBound = replayLease.Bind();
        var restored = await OpenAsync(fixture, restarted, replayLease, runtime);
        using var replayActive = restored.Context.Bind();
        Assert.NotNull(await restored.Context.ReplayResponseAsync(requestDigest, default));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var replayEffect = AgentToolInvocationEffectScope.Begin();
        var result = await restored.Context.InvokeAsync(call, _ => {
            effects.Add("duplicate");
            return ValueTask.FromResult<object?>("must-not-run");
        }, replayEffect, timeout.Token);
        Assert.Equal("saved-original-result", result);
        Assert.Equal(["read"], effects);
        Assert.True(readChecks >= 3);
        Assert.Equal(0, client.Requests);
        var saved = (await fixture.Store.GetExecutionRunAsync(fixture.Session.ExecutionRunId, timeout.Token))!.ToolAdmission!;
        Assert.Equal(originalIntent, Assert.Single(saved.Batches[0].Proposals).IntentId);
        Assert.Equal(AgentToolProposalState.Completed, saved.Batches[0].Proposals[0].State);
        await using var released = await catalog.AcquireAgentReadLeaseAsync(fixture.Agent.Id, timeout.Token);
        Assert.Equal(fixture.Agent.Id, released.Agent!.Id);
    }
}
