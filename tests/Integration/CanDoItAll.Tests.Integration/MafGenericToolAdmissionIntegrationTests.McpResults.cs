using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    [Fact]
    public async Task Native_MCP_result_survives_journal_restart_without_repeating_the_effect() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var effects = new List<string>();
        using var client = new ScriptClient(complete: true);
        var runtime = CreateRuntime(client, effects, unsupportedResult: true);
        runtime.Capabilities.RuntimeToolMetadata.Add(new("fixture-provider", "generic_fixture_write",
            AgentRuntimeToolOperationKind.Mutation, false) {
            AuthorizeResultDisclosureAsync = (_, _) => ValueTask.FromResult<IAsyncDisposable?>(null)
        });
        var native = new TextContentBlock { Text = "Native MCP result Ω" };
        var contents = new[] { native.ToAIContent()! };
        var call = new FunctionCallContent("mcp-effect", "generic_fixture_write", new Dictionary<string, object?>());
        var request = AgentToolProtocolEnvelope.ComputeDigest("mcp-checkpoint-request");
        var journal = fixture.NewJournal();
        AgentToolProtocolEnvelope checkpoint;
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await opened.Context.AdmitResponseAsync(request, AgentToolAdmissionJournalFixture.Envelope(), [call], default);
            using var effectScope = AgentToolInvocationEffectScope.Begin();
            var returned = await opened.Context.InvokeAsync(call, _ => {
                effects.Add("original");
                return ValueTask.FromResult<object?>(contents);
            }, effectScope, default);
            AssertNativeResult(returned);
            var proposal = Assert.Single((await journal.ReadAsync(lease, default)).Batches[0].Proposals);
            Assert.Equal(AgentToolProposalState.Completed, proposal.State);
            checkpoint = Assert.IsType<AgentToolProtocolEnvelope>(proposal.Result);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var replayLease = await restarted.AcquireRunAsync(fixture.Session, default);
        using var replayBound = replayLease.Bind();
        var replay = await OpenAsync(fixture, restarted, replayLease, runtime);
        using var replayActive = replay.Context.Bind();
        Assert.NotNull(await replay.Context.ReplayResponseAsync(request, default));
        using var replayEffect = AgentToolInvocationEffectScope.Begin();
        var restored = await replay.Context.InvokeAsync(call, _ => {
            effects.Add("duplicate");
            return ValueTask.FromResult<object?>(null);
        }, replayEffect, default);
        AssertNativeResult(restored);
        Assert.Equal(checkpoint, Assert.Single((await restarted.ReadAsync(replayLease, default)).Batches[0].Proposals).Result);
        Assert.Equal(["original"], effects);
        Assert.Equal(0, client.Requests);

        static void AssertNativeResult(object? value) {
            var content = Assert.IsType<TextContent>(Assert.Single(Assert.IsAssignableFrom<IEnumerable<AIContent>>(value)));
            Assert.Equal("Native MCP result Ω", content.Text);
            Assert.Equal(content.Text, Assert.IsType<TextContentBlock>(content.RawRepresentation).Text);
        }
    }
}
