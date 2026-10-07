using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class SharedProviderFinalUiTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_native_history_preserves_lazy_reads_and_exact_caller_key_identity() {
        var settings = SharedProviderNativeDefaultsUiTests.Settings.Load();
        await new ProviderHistoryUiAcceptanceTests(settings, "PP2 native source access", "PP2 native source", "PP2 Remote Ollama")
            .Provider_and_global_history_are_lazy_and_filter_the_same_attempts();
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_two_circuit_local_settings_preserve_concurrency_and_merge_only_edited_fields() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var profile = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api),
            item => item.IsSourceManaged && item.Name == "PP2 OpenAI default");
        await using var otherContext = await fixture.Page.Context.Browser!.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var other = await otherContext.NewPageAsync();
        var page = fixture.Page;
        await OpenSharingAsync(page, fixture.Settings.Clients[0], profile.Name);
        var originalAlias = await page.GetByTestId("shared-provider-import-alias").InputValueAsync();
        var originalEnabled = await page.GetByTestId("shared-provider-import-enabled").IsCheckedAsync();
        await OpenSharingAsync(other, fixture.Settings.Clients[0], profile.Name);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var firstAlias = "PP2C other first " + suffix;
        await SaveLocalAsync(other, firstAlias, false);
        await AssertNativeAsync(firstAlias, false);
        await page.GetByTestId("providers-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-import-alias")).ToHaveValueAsync(firstAlias);
        await Assertions.Expect(page.GetByTestId("shared-provider-import-enabled")).Not.ToBeCheckedAsync();
        var mine = "PP2C my edited alias " + suffix;
        await page.GetByTestId("shared-provider-import-alias").FillAsync(mine);
        var secondAlias = "PP2C other second " + suffix;
        await SaveLocalAsync(other, secondAlias, true);
        await AssertNativeAsync(secondAlias, true);
        await page.GetByTestId("providers-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-import-conflict")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-import-alias")).ToHaveValueAsync(mine);
        await Assertions.Expect(page.GetByTestId("shared-provider-import-enabled")).Not.ToBeCheckedAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-import-save")).ToBeDisabledAsync();
        await page.GetByTestId("shared-provider-import-alias").PressAsync("Enter");
        await AssertNativeAsync(secondAlias, true);
        await fixture.ScreenshotAsync("final-two-circuit-conflict");
        await page.GetByTestId("shared-provider-import-keep-edits").ClickAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-import-enabled")).ToBeCheckedAsync();
        await page.GetByTestId("shared-provider-import-save").ClickAsync();
        await AssertNativeAsync(mine, true);
        await SaveLocalAsync(page, originalAlias, originalEnabled);
        await AssertNativeAsync(profile.Name, profile.IsEnabled);
        await fixture.EvidenceAsync("final-two-circuit-concurrency", new {
            profile.Id, CleanAdoptsOtherValues = true, DirtySaveRefused = true,
            ExplicitMergePreservesOtherEnabledChange = true, Restored = true
        });

        async Task AssertNativeAsync(string name, bool enabled) {
            for (var attempt = 0; attempt < 50; attempt++) {
                var actual = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api), item => item.Id == profile.Id);
                if (actual.Name == name && actual.IsEnabled == enabled) {
                    return;
                }
                await Task.Delay(100);
            }
            Assert.Fail("Native imported local settings did not match the exact expected writer.");
        }
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_source_disable_retire_and_reimport_keep_identity_and_refuse_dispatch() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var profile = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api),
            item => item.IsSourceManaged && item.Name == "PP2 Remote Ollama");
        var page = fixture.Page;
        var source = await OpenSourceAsync(fixture);
        var sourceId = Guid.Parse((await source.GetAttributeAsync("data-source-id"))!);
        await source.GetByTestId("shared-provider-source-edit").ClickAsync();
        var sourceEditor = page.GetByTestId("shared-provider-source-dialog");
        await sourceEditor.GetByTestId("shared-provider-source-private").UncheckAsync();
        var originalUri = await sourceEditor.GetByTestId("shared-provider-source-uri").InputValueAsync();
        await sourceEditor.GetByTestId("shared-provider-source-save").ClickAsync();
        await Assertions.Expect(sourceEditor.GetByTestId("shared-provider-source-editor-error")).ToBeVisibleAsync();
        await Assertions.Expect(sourceEditor.GetByTestId("shared-provider-source-uri")).ToHaveValueAsync(originalUri);
        await fixture.ScreenshotAsync("final-source-http-policy-refusal");
        await sourceEditor.GetByTestId("shared-provider-source-cancel").ClickAsync();
        await source.GetByTestId("shared-provider-source-edit").ClickAsync();
        await Assertions.Expect(sourceEditor.GetByTestId("shared-provider-source-private")).ToBeCheckedAsync();
        await Assertions.Expect(sourceEditor.GetByTestId("shared-provider-source-uri")).ToHaveValueAsync(originalUri);
        await sourceEditor.GetByTestId("shared-provider-source-cancel").ClickAsync();
        await source.GetByTestId("shared-provider-source-toggle").ClickAsync();
        await Assertions.Expect(source.GetByTestId("shared-provider-source-toggle")).ToHaveTextAsync("Enable");
        await Assertions.Expect(source.GetByTestId("shared-provider-source-toggle")).ToBeEnabledAsync();
        await page.GetByTestId("shared-provider-connections-close").ClickAsync();
        await OpenSharingAsync(page, fixture.Settings.Clients[0], profile.Name);
        var disabled = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api), item => item.Id == profile.Id);
        Assert.False(disabled.IsEnabled);
        Assert.Equal("SourceDisabled", disabled.HealthStatus);
        var marker = "PP2C_DISABLED_" + Guid.NewGuid().ToString("N");
        using var refused = await fixture.Api.PostAsJsonAsync($"api/agents/providers/{profile.Id:D}/test-chat",
            new ProviderTestChatRequest(profile.DefaultModel, "", [], marker));
        Assert.False(refused.IsSuccessStatusCode);
        var captures = await fixture.ReadCapturesAsync();
        Assert.DoesNotContain(captures.GetProperty("requests").EnumerateArray(), item => item.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal));
        await fixture.ScreenshotAsync("final-source-disabled");
        source = await OpenSourceAsync(fixture);
        Assert.Equal(sourceId.ToString("D"), await source.GetAttributeAsync("data-source-id"));
        await source.GetByTestId("shared-provider-source-toggle").ClickAsync();
        await Assertions.Expect(source.GetByTestId("shared-provider-source-toggle")).ToHaveTextAsync("Disable");
        await Assertions.Expect(source.GetByTestId("shared-provider-source-toggle")).ToBeEnabledAsync();
        await source.GetByTestId("shared-provider-source-sync").ClickAsync();
        await page.GetByText("Source synchronized", new() { Exact = true }).WaitForAsync();
        await page.GetByTestId("shared-provider-connections-close").ClickAsync();
        await OpenSharingAsync(page, fixture.Settings.Clients[0], profile.Name);
        await page.GetByTestId("shared-provider-import-retire").ClickAsync();
        await page.GetByTestId("shared-provider-confirmation-dialog").WaitForAsync();
        await page.GetByTestId("shared-provider-confirmation-apply").ClickAsync();
        await page.GetByTestId("shared-provider-confirmation-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        var retired = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api), item => item.Id == profile.Id);
        Assert.False(retired.IsEnabled);
        source = await OpenSourceAsync(fixture);
        await source.GetByTestId("shared-provider-source-discover").ClickAsync();
        var catalog = page.GetByTestId("shared-provider-catalog-dialog");
        var selection = catalog.Locator("label").Filter(new() { Has = page.GetByText(profile.Name, new() { Exact = true }) })
            .GetByTestId("shared-provider-catalog-selection");
        await Assertions.Expect(selection).Not.ToBeCheckedAsync();
        await selection.CheckAsync();
        await page.GetByTestId("shared-provider-catalog-apply").ClickAsync();
        await catalog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.GetByTestId("shared-provider-connections-close").ClickAsync();
        var restored = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api), item => item.Id == profile.Id);
        Assert.True(restored.IsEnabled);
        Assert.Equal(profile.DefaultModel, restored.DefaultModel);
        Assert.Equal(profile.ModelCatalog, restored.ModelCatalog);
        await fixture.EvidenceAsync("final-source-lifecycle", new {
            sourceId, ProviderId = profile.Id, DisabledDispatchStatus = (int)refused.StatusCode,
            UpstreamCalledWhileDisabled = false, RetiredThenReimportedSameIdentity = true,
            HttpWithoutPrivateNetworkPolicyRefused = true, RejectedSourceEditPreservedOriginal = true
        });
    }

    private static async Task<ILocator> OpenSourceAsync(SharedProviderConsumerFixture fixture) {
        await fixture.NavigateAsync("/agents?tab=providers");
        await fixture.Page.GetByTestId("providers-connections").ClickAsync();
        var source = fixture.Page.GetByTestId("shared-provider-source-card").Filter(new() { HasTextString = "PP2 native source" });
        await source.WaitForAsync();
        return source;
    }

    private static async Task OpenSharingAsync(IPage page, string address, string name) {
        await SharedProviderMetadataUiChecks.OpenProviderAsync(page, address, name);
        await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        await page.GetByTestId("shared-provider-import-alias").WaitForAsync();
    }

    private static async Task SaveLocalAsync(IPage page, string alias, bool enabled) {
        await page.GetByTestId("shared-provider-import-alias").FillAsync(alias);
        await page.GetByTestId("shared-provider-import-enabled").SetCheckedAsync(enabled);
        await page.GetByTestId("shared-provider-import-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-import-save")).ToBeEnabledAsync();
    }
}
