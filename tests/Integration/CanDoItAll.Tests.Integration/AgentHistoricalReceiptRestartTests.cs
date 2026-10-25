using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.HistoricalReceipts;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class AgentHistoricalReceiptRestartTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Historical_hierarchy_recovers_the_unresolved_original_reconnect_after_seed_exit_and_Web_restart(bool process)
        => VerifyCreatedTargetRestartAsync(process ? HistoricalReceiptSeedKind.ProcessHierarchy : HistoricalReceiptSeedKind.InteractiveHierarchy, process);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Historical_created_definition_recovers_the_unresolved_original_edit_after_seed_exit_and_Web_restart(bool process)
        => VerifyCreatedTargetRestartAsync(process ? HistoricalReceiptSeedKind.ProcessCreatedDefinition : HistoricalReceiptSeedKind.InteractiveCreatedDefinition, process);

    private static Task VerifyCreatedTargetRestartAsync(HistoricalReceiptSeedKind kind, bool process)
        => AgentHistoricalReceiptRestartFixture.RunAsync(kind, async fixture => {
                if (process) {
                    await fixture.ExpireOriginalClaimAsync();
                }
                await fixture.StartWebAsync();
                var first = await fixture.ReadAsync(fixture.Seed.Request);
                fixture.AssertAcceptedPage(first, firstPage: true);
                var lastRequest = fixture.Next(first);
                fixture.AssertAcceptedPage(await fixture.ReadAsync(lastRequest), firstPage: false);
                await fixture.AssertOriginalEvidenceAsync();
                await fixture.SetSourceReadAsync(false);
                fixture.AssertRefused(await fixture.ReadAsync(lastRequest), AgentHistoricalReceiptReviewStop.ReadDenied);
                await fixture.SetSourceReadAsync(true);
                fixture.AssertAcceptedPage(await fixture.ReadAsync(lastRequest), firstPage: false);
                await fixture.ReplaceTargetLifetimeAsync();
                var replaced = await fixture.ReadAsync(lastRequest);
                Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, replaced.Stop);
                Assert.Equal(AgentHistoricalReceiptKnowledge.ReadUnavailable, Assert.Single(replaced.Observations).Knowledge);
                await fixture.AssertOriginalEvidenceAsync();
            });

    [Fact]
    public Task Historical_interactive_receipts_survive_actual_seed_exit_and_Web_restart_without_resuming_the_original_run()
        => AgentHistoricalReceiptRestartFixture.RunAsync(HistoricalReceiptSeedKind.Interactive, async fixture => {
            await fixture.StartWebAsync();
            await fixture.AssertOriginalEvidenceAsync();
            var first = await fixture.ReadAsync(fixture.Seed.Request);
            fixture.AssertAcceptedPage(first, firstPage: true);
            var nextRequest = fixture.Next(first);
            fixture.AssertAcceptedPage(await fixture.ReadAsync(nextRequest), firstPage: false);
            await fixture.EditCurrentTargetAsync();
            fixture.AssertAcceptedPage(await fixture.ReadAsync(nextRequest), firstPage: false);
            await fixture.AssertOriginalEvidenceAsync();

            await fixture.SetSourceReadAsync(false);
            fixture.AssertRefused(await fixture.ReadAsync(nextRequest), AgentHistoricalReceiptReviewStop.ReadDenied);
            await fixture.SetSourceReadAsync(true);
            await fixture.ReplaceTargetLifetimeAsync();
            fixture.AssertRefused(await fixture.ReadAsync(nextRequest), AgentHistoricalReceiptReviewStop.ReadDenied);
            await fixture.AssertOriginalEvidenceAsync();
        });

    [Fact]
    public Task Historical_Process_receipts_survive_expired_claim_and_restart_but_refuse_reactivated_dataset_in_new_Web_process()
        => AgentHistoricalReceiptRestartFixture.RunAsync(HistoricalReceiptSeedKind.Process, async fixture => {
            await fixture.ExpireOriginalClaimAsync();
            await fixture.StartWebAsync();
            var first = await fixture.ReadAsync(fixture.Seed.Request);
            fixture.AssertAcceptedPage(first, firstPage: true);
            var nextRequest = fixture.Next(first);
            fixture.AssertAcceptedPage(await fixture.ReadAsync(nextRequest), firstPage: false);
            await fixture.AssertOriginalEvidenceAsync();

            await fixture.SetSourceReadAsync(false);
            fixture.AssertRefused(await fixture.ReadAsync(nextRequest), AgentHistoricalReceiptReviewStop.ReadDenied);
            await fixture.SetSourceReadAsync(true);
            await fixture.StopWebAsync();
            await fixture.ReactivateAsync();
            await fixture.StartWebAsync();
            var changed = await fixture.ReadAsync(fixture.Seed.Request);
            Assert.Equal(AgentHistoricalReceiptReviewStop.LookupLimit, changed.Stop);
            Assert.Equal(4, changed.Observations.Count);
            Assert.All(changed.Observations, observation => Assert.Equal(AgentHistoricalReceiptKnowledge.DatasetChanged, observation.Knowledge));
            Assert.NotNull(changed.NextCursor);
            var last = await fixture.ReadAsync(fixture.Next(changed));
            Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, last.Stop);
            Assert.Equal(AgentHistoricalReceiptKnowledge.DatasetChanged, Assert.Single(last.Observations).Knowledge);
            Assert.Null(last.NextCursor);
            await fixture.AssertOriginalEvidenceAsync();
        });
}
