using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal static class ProviderProfilesUiJourney {
    internal sealed record SavedProvider(Guid Id, string Name, string Model, string AlternateModel);

    internal static async Task<SavedProvider> CreateAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, string endpoint) {
        await page.SetViewportSizeAsync(1920, 1080);
        var setup = await host.SeedAsync(async services => {
            var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                Name = "PP1 synthetic credential", Kind = SecretKind.ApiKey, SecretValue = "local-fixture-credential", Scope = "workspace"
            });
            Assert.True(secret.IsSuccess);
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            return (Secret: secret.Value, Others: (await workspace.ListProvidersAsync()).ToDictionary(provider => provider.Id, provider => JsonSerializer.Serialize(provider)));
        });
        var name = "PP1 UI provider " + Guid.NewGuid().ToString("N")[..8];
        var model = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        var alternate = ManagedSeedProviderFallbacks.OpenAiSuggestedModels.First(candidate =>
            candidate != model && OpenAiModelSuggestions.IsMainModel(candidate));
        await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=providers");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
        await Assertions.Expect(page.GetByTestId("providers-save")).ToBeEnabledAsync();
        var surface = page.GetByTestId("agents-provider-profiles-panel");
        var previousActivation = await surface.GetAttributeAsync("data-editor-activation");
        await page.GetByTestId("providers-new").ClickAsync();
        await Assertions.Expect(surface).Not.ToHaveAttributeAsync("data-editor-activation", previousActivation!);
        await page.GetByTestId("providers-name-input").FillAsync(" " + name + " ");
        await page.GetByTestId("providers-base-url-input").FillAsync(endpoint);
        await page.GetByTestId("providers-model-input").FillAsync(model);
        await page.GetByTestId("providers-api-key-input").SelectOptionAsync($"secret:{setup.Secret:D}");
        await page.GetByTestId("providers-tags-input").FillAsync("pp1");
        await page.GetByTestId("providers-tags-input").PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("providers-tags").GetByRole(AriaRole.Button, new() { Name = "pp1", Exact = true })).ToBeVisibleAsync();
        Assert.False(await ExistsAsync(), "Adding a tag must not submit the provider form.");
        await Tab("runtime");
        await page.GetByTestId("providers-suggested-models").FillAsync($"{model}\n{alternate}\n");
        await page.GetByTestId("providers-config-json").FillAsync("{\"timeoutSeconds\":90,\"pp1Extension\":{\"retained\":true}}");
        await page.GetByTestId("providers-notes").FillAsync("Draft notes before blur");
        await Tab("prices");
        await page.GetByTestId("provider-pricing-add-button").ClickAsync();
        await page.GetByTestId("provider-pricing-model-0").FillAsync(model);
        (string Field, string Value)[] prices = [
            ("input", "1.25"), ("cached", "0.125"), ("cache-write", "1.5"), ("output", "3.5"),
            ("image-input", "2"), ("cached-image-input", "0.25"), ("long-threshold", "128000"),
            ("long-input", "2.5"), ("long-cached", "0.5"), ("long-cache-write", "3"), ("long-output", "7")
        ];
        foreach (var (field, value) in prices) {
            await page.GetByTestId($"provider-pricing-{field}-0").FillAsync(value);
        }
        await page.GetByTestId("provider-pricing-input-0").FillAsync("1e-");
        await Tab("runtime");
        await page.GetByTestId("providers-config-json").FillAsync("{invalid");
        await Tab("prices");
        await Assertions.Expect(page.GetByTestId("provider-pricing-input-0")).ToHaveValueAsync("1e-");
        await page.GetByTestId("providers-save").ClickAsync();
        Assert.False(await ExistsAsync());
        await page.GetByTestId("provider-pricing-input-0").FillAsync("1.25");
        await Tab("runtime");
        await Assertions.Expect(page.GetByTestId("providers-config-json")).ToHaveValueAsync("{invalid");
        await page.GetByTestId("providers-config-json").FillAsync("{\"timeoutSeconds\":90,\"pp1Extension\":{\"retained\":true}}");
        await Assertions.Expect(page.GetByTestId("providers-notes")).ToHaveValueAsync("Draft notes before blur");
        await Tab("thinking");
        await page.GetByRole(AriaRole.Button, new() { Name = $"Edit thinking for {model}", Exact = true }).ClickAsync();
        await page.GetByTestId("thinking-automatic").UncheckAsync();
        await page.GetByTestId("thinking-model-default").SelectOptionAsync(new SelectOptionValue { Label = "Low" });
        await Assertions.Expect(page.GetByTestId("thinking-apply")).ToBeInViewportAsync(new() { Ratio = 1 });
        await page.ScreenshotAsync(new() { Path = host.Artifact("provider-pp1-thinking-dialog.png") });
        await page.GetByTestId("thinking-apply").ClickAsync();
        Assert.False(await ExistsAsync());
        await Tab("connection");
        await Assertions.Expect(page.GetByTestId("providers-name-input")).ToHaveValueAsync(" " + name + " ");
        await page.GetByTestId("providers-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("providers-name-input")).ToHaveValueAsync(" " + name + " ");
        await Assertions.Expect(page.GetByTestId("providers-save")).ToBeInViewportAsync(new() { Ratio = 1 });
        await page.ScreenshotAsync(new() { Path = host.Artifact("provider-pp1-connection.png") });
        await page.GetByTestId("providers-save").ClickAsync();
        await page.GetByText("Provider saved", new() { Exact = true }).First.WaitForAsync();
        var saved = await host.SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var all = await workspace.ListProvidersAsync();
            foreach (var (id, json) in setup.Others) {
                Assert.Equal(json, JsonSerializer.Serialize(all.Single(provider => provider.Id == id)));
            }
            var provider = Assert.Single(all, item => item.Name == name);
            var editor = await workspace.GetProviderEditorAsync(provider.Id);
            Assert.NotNull(editor.ExpectedConcurrencyToken);
            Assert.Equal(endpoint, editor.BaseUrl);
            Assert.Equal($"secret:{setup.Secret:D}", editor.ApiKeyEnvironmentVariable);
            Assert.Equal(ProviderTransportKind.Responses, editor.Transport);
            Assert.Equal([model, alternate], editor.SuggestedModels);
            Assert.Contains("pp1", editor.Tags);
            using var configuration = JsonDocument.Parse(editor.ConfigurationJson);
            Assert.True(configuration.RootElement.GetProperty("pp1Extension").GetProperty("retained").GetBoolean());
            var thinking = Assert.Single(ProviderModelThinkingConfiguration.Read(editor.ConfigurationJson));
            Assert.Equal(model, thinking.Model);
            Assert.Equal(AgentReasoningEffortLevel.Low, thinking.DefaultEffort);
            var rate = Assert.Single(editor.ModelPrices);
            Assert.Equal(model, rate.Model);
            Assert.Equal((1.25m, 0.125m, 1.5m, 3.5m, 2m, 0.25m), (rate.InputPerMillionTokensUsd, rate.CachedInputPerMillionTokensUsd,
                rate.CacheWritePerMillionTokensUsd, rate.OutputPerMillionTokensUsd, rate.ImageInputPerMillionTokensUsd, rate.CachedImageInputPerMillionTokensUsd));
            Assert.Equal((128000, 2.5m, 0.5m, 3m, 7m), (rate.LongContextThresholdTokens, rate.LongContextInputPerMillionTokensUsd,
                rate.LongContextCachedInputPerMillionTokensUsd, rate.LongContextCacheWritePerMillionTokensUsd, rate.LongContextOutputPerMillionTokensUsd));
            return new SavedProvider(provider.Id, provider.Name, model, alternate);
        });
        await SharedProviderMetadataUiChecks.OpenProviderAsync(page, host.BaseUrl, name, oracle.NavigateAsync);
        await Assertions.Expect(page.GetByTestId("providers-name-input")).ToHaveValueAsync(name);
        await page.GetByTestId("providers-name-input").FillAsync("Unsaved selection proof");
        await page.GetByTestId("providers-tree-provider").Filter(new() { HasTextString = name }).First.ClickAsync();
        await page.GetByTestId("providers-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("providers-name-input")).ToHaveValueAsync("Unsaved selection proof");
        await Tab("prices");
        await Assertions.Expect(page.GetByTestId("provider-pricing-long-output-0")).ToHaveValueAsync("7");
        await page.ScreenshotAsync(new() { Path = host.Artifact("provider-pp1-prices.png") });
        await Tab("runtime");
        await Assertions.Expect(page.GetByTestId("providers-config-json")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("pp1Extension"));
        await page.ScreenshotAsync(new() { Path = host.Artifact("provider-pp1-runtime.png") });
        await Tab("sharing");
        await Assertions.Expect(page.GetByText("Published capability preview", new() { Exact = true })).ToBeVisibleAsync();
        await Tab("history");
        await Assertions.Expect(page.GetByTestId("provider-request-history")).ToBeVisibleAsync();
        await page.GetByTestId("providers-connections").ClickAsync();
        await Assertions.Expect(page.GetByTestId("shared-provider-connections-dialog")).ToBeVisibleAsync();
        await page.GetByTestId("shared-provider-connections-close").ClickAsync();
        await File.WriteAllTextAsync(host.Artifact("provider-pp1-created.json"), JsonSerializer.Serialize(saved));
        return saved;

        Task Tab(string section) => page.GetByTestId($"provider-editor-tab-{section}").ClickAsync();
        Task<bool> ExistsAsync() => host.SeedAsync(async services => (await services.GetRequiredService<IAgentFrameworkWorkspaceService>()
            .ListProvidersAsync()).Any(provider => provider.Name == name));
    }
}
