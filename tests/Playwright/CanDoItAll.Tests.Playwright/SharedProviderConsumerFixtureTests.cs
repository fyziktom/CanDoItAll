namespace CanDoItAll.Tests.Playwright;

public sealed class SharedProviderConsumerFixtureTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_fixture_disposal_preserves_an_unconfirmed_response_plan() {
        await using var observer = await SharedProviderConsumerFixture.StartAsync();
        var pending = await SharedProviderConsumerFixture.StartAsync();
        await pending.ScriptAsync("AC1 never dispatched", "AC1_RETAIN_" + Guid.NewGuid().ToString("N"),
            new { kind = "text", text = "Retain until the owned attempt is inspected." });
        await pending.DisposeAsync();
        var progress = await observer.ReadScriptProgressAsync();
        await observer.EvidenceAsync("unconfirmed-script-after-disposal", progress);
        Assert.Equal(1, progress.GetProperty("total").GetInt32());
        Assert.Equal(0, progress.GetProperty("consumed").GetInt32());
        await observer.ClearScriptAsync();
    }
}
