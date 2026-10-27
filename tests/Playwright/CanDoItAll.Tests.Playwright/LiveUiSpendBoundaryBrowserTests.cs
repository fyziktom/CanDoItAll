using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;
using static CanDoItAll.Tests.Playwright.Smoke.ScriptedAgentUiFixture;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class LiveUiSpendBoundaryBrowserTests {
    [Fact]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public async Task Last_permitted_batch_can_finish_and_the_next_batch_stops_the_owned_host() {
        const int batchLimit = 10;
        var evidence = new UiEvidence("live-ui-inclusive-batch-boundary") { Execution = "deterministic-external-model" };
        await using var wire = await AgentResponseFixture.StartAsync(ProjectStructureToolPolicy.ProjectStructureAssetGet);
        await using var host = await LiveUiHost.StartAsync();
        using var cancelTurn = new CancellationTokenSource();
        var releaseExcess = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExecutionRunDetail>? turn = null;
        try {
            var fixture = await CreateFileJourneyAsync(host);
            await ConfigureScriptedAgentAsync(host, fixture.Agent.Id, wire.BaseUrl);
            host.Watch(fixture.Agent.Id);
            wire.HoldReply = false;
            var read = new AgentResponseFixture.ScriptedCall(ProjectStructureToolPolicy.ProjectStructureAssetGet,
                JsonSerializer.Serialize(new { projectId = fixture.ProjectId, nodeId = fixture.SeedAssetId }));
            wire.Steps = [
                .. Enumerable.Repeat<Func<JsonElement, AgentResponseFixture.ScriptedTurn>>(
                    _ => new([read]), batchLimit - 1),
                _ => new([], "The last permitted response completed."),
                _ => new([read]),
                _ => new([], "The excess response remains held.")
            ];
            wire.BeforeResponse = async ordinal => {
                if (ordinal == batchLimit + 2) {
                    await releaseExcess.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
            };
            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            await GrantFileJourneyAsync(host, oracle, page, fixture);
            var chat = await OpenFileChatAsync(host, oracle, page, fixture);
            turn = FileTurnAsync(host, page, chat, fixture.Agent.Id, "Read the selected asset metadata.", cancellationToken: cancelTurn.Token);
            var completed = await turn.WaitAsync(TimeSpan.FromSeconds(90));
            await ObserveAsync(() => host.ToolAdmissionBatches >= batchLimit || host.StopReason != LiveUiStopReason.None);
            Assert.Equal(batchLimit, host.ToolAdmissionBatches);
            Assert.Equal(LiveUiStopReason.None, host.StopReason);
            Assert.Equal(ExecutionState.Completed, completed.Run.State);
            Assert.Equal(batchLimit, wire.Requests);
            Assert.False(host.SpendBoundExceeded);
            evidence.RecordRun("last-permitted-completion", completed);

            turn = FileTurnAsync(host, page, chat, fixture.Agent.Id, "Read the selected asset metadata once more.", cancellationToken: cancelTurn.Token);
            await ObserveAsync(() => host.StopReason != LiveUiStopReason.None);
            Assert.Equal(LiveUiStopReason.ToolBatchLimit, host.StopReason);
            Assert.Equal(batchLimit + 1, host.ToolAdmissionBatches);
            Assert.True(host.SpendBoundExceeded);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => turn.WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Contains(nameof(LiveUiStopReason.ToolBatchLimit), failure.Message, StringComparison.Ordinal);
            Assert.InRange(wire.Requests, batchLimit + 1, batchLimit + 2);
            Assert.Equal(0, await host.CountModelRequestsAsync());
            evidence.Observations["boundary"] = new { permittedBatches = batchLimit, wire.Requests, host.StopReason, realModelRequests = 0 };
            evidence.Passed = true;
        } finally {
            releaseExcess.TrySetResult();
            await cancelTurn.CancelAsync();
            if (turn is not null && !turn.IsCompleted) {
                _ = await Record.ExceptionAsync(async () => await turn);
            }
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }
    }

    private static async Task ObserveAsync(Func<bool> observed) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!observed()) {
            await Task.Delay(50, deadline.Token);
        }
    }
}
