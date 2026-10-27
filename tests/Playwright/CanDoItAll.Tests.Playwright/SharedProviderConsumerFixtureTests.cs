using System.Net;
using System.Net.Http.Headers;

namespace CanDoItAll.Tests.Playwright;

public sealed class SharedProviderConsumerFixtureTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_public_assets_load_with_a_bearer_header_while_project_API_requires_authorization() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        await fixture.NavigateAsync("/agents?tab=overview");
        var assetPath = await fixture.Page.Locator("script[src*='_content/']").First.GetAttributeAsync("src");
        Assert.False(string.IsNullOrWhiteSpace(assetPath));
        var assetUri = new Uri(new Uri(fixture.Address), assetPath);
        Assert.Equal(new Uri(fixture.Address).Authority, assetUri.Authority);
        using var client = new HttpClient { BaseAddress = new Uri(fixture.Address), Timeout = TimeSpan.FromSeconds(30) };
        using var publicAsset = await client.GetAsync(assetUri);
        Assert.Equal(HttpStatusCode.OK, publicAsset.StatusCode);
        var expected = await publicAsset.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(expected);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-static-asset-probe");
        using var publicAssetWithInvalidToken = await client.GetAsync(assetUri);
        Assert.Equal(HttpStatusCode.OK, publicAssetWithInvalidToken.StatusCode);
        Assert.Equal(expected, await publicAssetWithInvalidToken.Content.ReadAsByteArrayAsync());
        foreach (var protectedPath in new[] { "/api/projects", "/authorized-files/content", "/storage/objects/download" }) {
            using var denied = await client.GetAsync(protectedPath);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var protectedPath in new[] { "/api/projects", "/authorized-files/content", "/storage/objects/download" }) {
            using var anonymous = await client.GetAsync(protectedPath);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }
        using var authorized = await fixture.Api.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }

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
