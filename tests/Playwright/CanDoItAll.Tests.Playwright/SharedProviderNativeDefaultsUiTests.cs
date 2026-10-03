using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class SharedProviderNativeDefaultsUiTests {
    private const string SourceName = "PP2 native source";
    private const string SourceSecretName = "PP2 native source access";
    private const string UpstreamSecretName = "E2E central upstream credential";
    private static readonly string[] Presets = ["OpenAI default", "OpenAI chat completions", "OpenAI image generation", "Remote Ollama"];
    private static readonly string[] PriceFields = ["input", "cached", "cache-write", "output", "image-input", "cached-image-input",
        "long-threshold", "long-input", "long-cached", "long-cache-write", "long-output"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Owned_three_instance_native_defaults_publish_import_and_refresh_through_production_controls() {
        var settings = Settings.Load();
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        await using var centralContext = await DesktopAsync(browser);
        await using var clientAContext = await DesktopAsync(browser);
        await using var clientBContext = await DesktopAsync(browser);
        await using var secondAContext = await DesktopAsync(browser);
        var central = await centralContext.NewPageAsync();
        var clients = new[] { await clientAContext.NewPageAsync(), await clientBContext.NewPageAsync() };
        var heldA = await secondAContext.NewPageAsync();
        var sourceToken = await IssueTokenAsync(central, settings.Central, shared: true);
        using var centralApi = Api(settings.Central, sourceToken);
        var sourceProfiles = await ProfilesAsync(centralApi);
        var native = Presets.Select(name => Assert.Single(sourceProfiles, profile => profile.Name == name)).ToArray();
        await EvidenceAsync(settings, "source-defaults-before", native.Select(SafeProfile).ToArray());
        var savedNative = new List<ProviderProfile>();
        foreach (var source in native) {
            Assert.False(source.IsSourceManaged);
            Assert.NotEmpty(source.SuggestedModels);
            await OpenAsync(central, settings.Central, source.Name);
            await AssertUiAsync(central, source, imported: false);
            if (source.Kind == ProviderKind.Ollama && source.Name == "Remote Ollama") {
                await central.GetByTestId("provider-editor-tab-sharing").ClickAsync();
                await central.GetByText("This provider is not persisted", new() { Exact = true }).WaitForAsync();
                Assert.True(await central.GetByTestId("shared-provider-publish").CountAsync() == 0);
            }
            var savedName = "PP2 " + source.Name;
            var existing = sourceProfiles.SingleOrDefault(profile => profile.Name == savedName);
            Assert.True(existing is null || settings.Resume, "An owned profile already exists; explicitly resume the fixture.");
            if (existing is null) {
                await central.GetByTestId("providers-new").ClickAsync();
                await Assertions.Expect(central.GetByTestId("providers-name-input")).ToHaveValueAsync("New OpenAI provider");
            } else {
                await OpenAsync(central, settings.Central, savedName);
            }
            await central.GetByTestId("providers-kind-select").SelectOptionAsync(source.Kind.ToString());
            await Assertions.Expect(central.GetByTestId("providers-kind-select")).ToHaveValueAsync(source.Kind.ToString());
            await central.GetByTestId("providers-purpose-select").SelectOptionAsync(source.Purpose.ToString());
            await central.GetByTestId("providers-model-input").FillAsync(source.DefaultModel);
            await central.GetByTestId("providers-model-input").PressAsync("Tab");
            await central.GetByTestId("provider-editor-tab-runtime").ClickAsync();
            await central.GetByTestId("providers-transport-select").SelectOptionAsync(source.Transport.ToString());
            await central.GetByTestId("providers-suggested-models").FillAsync(string.Join('\n', source.SuggestedModels));
            await central.GetByTestId("providers-config-json").FillAsync(source.ConfigurationJson);
            await central.GetByLabel("Streaming", new() { Exact = true }).SetCheckedAsync(source.SupportsStreaming);
            await central.GetByLabel("Tool calling", new() { Exact = true }).SetCheckedAsync(source.SupportsTools);
            await central.GetByLabel("Framework-managed history", new() { Exact = true }).SetCheckedAsync(source.PreferFrameworkManagedChatHistory);
            await central.GetByLabel("Background responses", new() { Exact = true }).SetCheckedAsync(source.SupportsBackgroundResponses);
            await central.GetByTestId("provider-editor-tab-prices").ClickAsync();
            await CopyPricesAsync(central, source.ModelPrices);
            await central.GetByTestId("provider-editor-tab-connection").ClickAsync();
            await central.GetByTestId("providers-name-input").FillAsync(savedName);
            await central.GetByTestId("providers-base-url-input").FillAsync(source.Kind == ProviderKind.Ollama
                ? "http://deterministic-upstream:8080" : "http://deterministic-upstream:8080/v1");
            await central.GetByTestId("providers-api-key-input").SelectOptionAsync(new SelectOptionValue { Label = UpstreamSecretName });
            await central.GetByTestId("providers-save").ClickAsync();
            await central.GetByText("Provider saved", new() { Exact = true }).WaitForAsync();
            var saved = Assert.Single(await ProfilesAsync(centralApi), profile => profile.Name == savedName);
            Assert.NotEqual(source.Id, saved.Id);
            Assert.Equal(source.DefaultModel, saved.DefaultModel);
            Assert.Equal(source.SuggestedModels, saved.SuggestedModels);
            Assert.Equal(source.Transport, saved.Transport);
            if (source.Kind == ProviderKind.OpenAi) {
                Assert.Equal(source.ModelPrices, saved.ModelPrices);
            }
            savedNative.Add(saved);
            await central.GetByTestId("provider-editor-tab-sharing").ClickAsync();
            await central.GetByTestId("shared-provider-publication-status").WaitForAsync();
            if (!settings.Resume || (await central.GetByTestId("shared-provider-publication-status").InnerTextAsync()).Trim() != "Published") {
                await central.GetByTestId("shared-provider-publish").ClickAsync();
            }
            await Assertions.Expect(central.GetByTestId("shared-provider-publication-status")).ToContainTextAsync("Published");
        }

        var retained = await ProfilesAsync(centralApi);
        Assert.Equal(JsonSerializer.Serialize(native.Select(SafeProfile), Json), JsonSerializer.Serialize(
            native.Select(original => SafeProfile(Assert.Single(retained, profile => profile.Id == original.Id))), Json));
        await EvidenceAsync(settings, "native-seed-identities-retained", native.Select(profile => new { profile.Id, profile.Name }));

        var catalog = SharedProviderProtocolJson.DeserializeCatalog(await centralApi.GetStringAsync(SharedProviderRoutes.Catalog));
        foreach (var source in savedNative) {
            var publication = Assert.Single(catalog.Providers, item => item.DisplayName == source.Name);
            Assert.Equal(source.SuggestedModels.Append(source.DefaultModel).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
                publication.Models.Select(model => model.DisplayName).Order(StringComparer.Ordinal));
            Assert.Equal(source.DefaultModel, Assert.Single(publication.Models, model => model.Id == publication.DefaultModelId).DisplayName);
            foreach (var model in publication.Models) {
                Assert.Equal(SharedProviderRoutingModelIdCodec.Create(publication.PublicationId, model.DisplayName), model.Id);
            }
        }
        await EvidenceAsync(settings, "native-public-catalog", catalog);
        var clientTokens = new List<string>();
        var clientImports = new List<ProviderProfile[]>();
        var observed = new List<object>();
        var invocations = new List<object>();
        for (var index = 0; index < clients.Length; index++) {
            var page = clients[index];
            var address = settings.Clients[index];
            clientTokens.Add(await IssueTokenAsync(page, address, shared: false));
            await SharedProviderTwoInstanceUiAcceptanceTests.CreateSecretAsync(page, address, SourceSecretName, sourceToken);
            await ImportAsync(page, address, savedNative.Select(profile => profile.Name).ToArray(), settings.Resume);
            using var clientApi = Api(address, clientTokens[index]);
            var imported = (await ProfilesAsync(clientApi)).Where(profile => profile.IsSourceManaged && savedNative.Any(source => source.Name == profile.Name)).ToArray();
            Assert.Equal(savedNative.Count, imported.Length);
            clientImports.Add(imported);
            foreach (var source in savedNative) {
                var target = Assert.Single(imported, profile => profile.Name == source.Name);
                var publication = Assert.Single(catalog.Providers, item => item.DisplayName == source.Name);
                Assert.Equal(publication.DefaultModelId.Value, target.DefaultModel);
                Assert.Equal(publication.Models.Select(model => (Id: model.Id.Value, model.DisplayName)).OrderBy(model => model.Id),
                    target.ModelCatalog.Select(model => (model.Id, model.DisplayName)).OrderBy(model => model.Id));
                Assert.Equal(publication.Models.Where(model => model.IsSuggested).Select(model => model.Id.Value).Order(StringComparer.Ordinal),
                    target.SuggestedModels.Order(StringComparer.Ordinal));
                Assert.Equal(publication.Models.Select(model => model.Id.Value).Order(StringComparer.Ordinal),
                    target.ModelSelectionConstraint!.AllowedModels.Order(StringComparer.Ordinal));
                Assert.NotEqual(source.Id, target.Id);
                await OpenAsync(page, address, target.Name);
                await AssertUiAsync(page, target, imported: true);
                await AssertPriceParityAsync(central, page, settings.Central, address, source, target);
                await AssertThinkingParityAsync(central, page, settings.Central, address, source, target);
                observed.Add(new { Client = index, Source = SafeProfile(source), Import = SafeProfile(target), publication.PublicationId, publication.Revision });
                await EvidenceAsync(settings, "native-client-parity", observed);
                await ScreenshotAsync(page, settings, $"client-{index}-{source.Kind}-{source.Purpose}-{source.Transport}");
                if (source.Purpose == ProviderProfilePurpose.Chat) {
                    foreach (var model in new[] { publication.DefaultModelId, publication.Models.First(model => model.Id != publication.DefaultModelId).Id }) {
                        using var response = await clientApi.PostAsJsonAsync($"api/agents/providers/{target.Id:D}/test-chat",
                            new ProviderTestChatRequest(model.Value, string.Empty, [], $"PP2 native model {model.Value}"));
                        invocations.Add(new { Client = index, ProviderId = target.Id, ClientRoute = model.Value,
                            ExpectedUpstreamModel = publication.Models.Single(item => item.Id == model).DisplayName, Status = (int)response.StatusCode });
                        await EvidenceAsync(settings, "native-client-invocations", invocations);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        var result = await response.Content.ReadFromJsonAsync<ProviderTestChatResult>(Json);
                        Assert.NotNull(result);
                        Assert.Equal(model.Value, result.Model);
                        Assert.Contains("deterministic fixture response", result.ResponseText, StringComparison.Ordinal);
                        Assert.True(result.InputTokens > 0 && result.OutputTokens > 0);
                    }
                }
            }
        }
        Assert.Empty(clientImports[0].Select(profile => profile.Id).Intersect(clientImports[1].Select(profile => profile.Id)));
        await EvidenceAsync(settings, "native-client-parity", observed);
        await RefreshAcrossCircuitsAsync(central, clients, heldA, settings, centralApi, clientTokens, savedNative, clientImports);
    }

    private static async Task RefreshAcrossCircuitsAsync(IPage central, IPage[] clients, IPage heldA, Settings settings,
        HttpClient centralApi, IReadOnlyList<string> tokens, IReadOnlyList<ProviderProfile> sources, IReadOnlyList<ProviderProfile[]> imports) {
        var changing = sources.Single(profile => profile.Name == "PP2 OpenAI chat completions");
        await OpenAsync(heldA, settings.Clients[0], changing.Name);
        var originalDefault = await heldA.GetByTestId("providers-model-input").InputValueAsync();
        var nextDefault = changing.SuggestedModels.First(model => model != changing.DefaultModel);
        var remaining = changing.SuggestedModels.Where(model => model != changing.DefaultModel).Append("pp2-added-model").ToArray();
        await OpenAsync(central, settings.Central, changing.Name);
        await central.GetByTestId("providers-model-input").FillAsync(nextDefault);
        await central.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        await central.GetByTestId("providers-suggested-models").FillAsync(string.Join('\n', remaining));
        await central.GetByTestId("providers-save").ClickAsync();
        await central.GetByText("Provider saved", new() { Exact = true }).WaitForAsync();
        Assert.Equal(nextDefault, Assert.Single(await ProfilesAsync(centralApi), profile => profile.Id == changing.Id).DefaultModel);
        await SyncAsync(clients[0], settings.Clients[0]);
        Assert.Equal(originalDefault, await heldA.GetByTestId("providers-model-input").InputValueAsync());
        await heldA.GetByTestId("providers-refresh").ClickAsync();
        await Assertions.Expect(heldA.GetByTestId("providers-model-input")).ToHaveValueAsync(nextDefault);
        var acquired = await heldA.GetByTestId("providers-model-input").ElementHandleAsync();
        await heldA.GetByTestId("providers-refresh").ClickAsync();
        Assert.True(await acquired!.EvaluateAsync<bool>("element => element.isConnected"));
        using var clientAApi = Api(settings.Clients[0], tokens[0]);
        var updated = Assert.Single(await ProfilesAsync(clientAApi), profile => profile.Id == imports[0].Single(profile => profile.Name == changing.Name).Id);
        Assert.DoesNotContain(updated.ModelCatalog, model => model.DisplayName == changing.DefaultModel);
        Assert.Contains(updated.ModelCatalog, model => model.DisplayName == "pp2-added-model");
        await AssertUiAsync(heldA, updated, imported: true);
        using var clientBApi = Api(settings.Clients[1], tokens[1]);
        var beforeB = Assert.Single(await ProfilesAsync(clientBApi), profile => profile.Id == imports[1].Single(profile => profile.Name == changing.Name).Id);
        Assert.Equal(originalDefault, beforeB.GetModelDisplayName(beforeB.DefaultModel));
        await SyncAsync(clients[1], settings.Clients[1]);
        var afterB = Assert.Single(await ProfilesAsync(clientBApi), profile => profile.Id == beforeB.Id);
        Assert.Equal(updated.DefaultModel, afterB.DefaultModel);
        await EvidenceAsync(settings, "two-circuit-refresh", new { Original = originalDefault, Current = nextDefault,
            ClientA = SafeProfile(updated), ClientBBefore = SafeProfile(beforeB), ClientBAfter = SafeProfile(afterB) });
        await ScreenshotAsync(heldA, settings, "two-circuit-refresh");
    }

    private static async Task AssertUiAsync(IPage page, ProviderProfile profile, bool imported) {
        await page.GetByTestId("provider-editor-tab-connection").ClickAsync();
        var defaultLabel = profile.GetModelDisplayName(profile.DefaultModel);
        await Assertions.Expect(page.GetByTestId("providers-model-input")).ToHaveValueAsync(defaultLabel);
        var node = page.GetByTestId("providers-tree-provider").Filter(new() { HasTextString = profile.Name }).First;
        Assert.Contains(defaultLabel, await node.GetAttributeAsync("aria-description"), StringComparison.Ordinal);
        if (imported) {
            Assert.DoesNotContain("sp1.", await node.GetAttributeAsync("aria-description"), StringComparison.Ordinal);
            Assert.True(await page.GetByTestId("providers-model-input").IsDisabledAsync());
        }
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        var models = (await page.GetByTestId("providers-suggested-models").InputValueAsync()).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(profile.SuggestedModels.Select(profile.GetModelDisplayName).Order(StringComparer.Ordinal), models.Order(StringComparer.Ordinal));
    }

    internal static async Task AssertPriceParityAsync(IPage central, IPage client, string sourceUrl, string clientUrl,
        ProviderProfile source, ProviderProfile target) {
        await OpenAsync(central, sourceUrl, source.Name);
        await central.GetByTestId("provider-editor-tab-prices").ClickAsync();
        var expected = await PricesAsync(central);
        await OpenAsync(client, clientUrl, target.Name);
        await client.GetByTestId("provider-editor-tab-prices").ClickAsync();
        var actual = await PricesAsync(client);
        foreach (var model in target.ModelCatalog) {
            if (expected.TryGetValue(model.DisplayName, out var price)) {
                Assert.Equal(price, actual[model.DisplayName]);
            } else {
                Assert.False(actual.ContainsKey(model.DisplayName));
                Assert.Contains(model.DisplayName, await client.GetByTestId("provider-pricing-table").InnerTextAsync(), StringComparison.Ordinal);
            }
        }
        Assert.All(actual.Keys, name => Assert.Contains(target.ModelCatalog, model => model.DisplayName == name));
    }

    private static async Task CopyPricesAsync(IPage page, IReadOnlyList<ProviderModelTokenPrice> prices) {
        var rows = page.Locator("[data-testid^='provider-pricing-row-']");
        for (var remaining = await rows.CountAsync(); remaining > 0; remaining--) {
            await rows.First.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }).ClickAsync();
            await Assertions.Expect(rows).ToHaveCountAsync(remaining - 1);
        }
        for (var row = 0; row < prices.Count; row++) {
            var price = prices[row];
            await page.GetByTestId("provider-pricing-add-button").ClickAsync();
            await Assertions.Expect(rows).ToHaveCountAsync(row + 1);
            await page.GetByTestId($"provider-pricing-model-{row}").FillAsync(price.Model);
            decimal?[] values = [price.InputPerMillionTokensUsd, price.CachedInputPerMillionTokensUsd,
                price.CacheWritePerMillionTokensUsd, price.OutputPerMillionTokensUsd, price.ImageInputPerMillionTokensUsd,
                price.CachedImageInputPerMillionTokensUsd, price.LongContextThresholdTokens,
                price.LongContextInputPerMillionTokensUsd, price.LongContextCachedInputPerMillionTokensUsd,
                price.LongContextCacheWritePerMillionTokensUsd, price.LongContextOutputPerMillionTokensUsd];
            for (var field = 0; field < PriceFields.Length; field++) {
                await page.GetByTestId($"provider-pricing-{PriceFields[field]}-{row}")
                    .FillAsync(values[field]?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            }
            await page.GetByTestId($"provider-pricing-{PriceFields[^1]}-{row}").PressAsync("Tab");
        }
    }

    private static async Task<Dictionary<string, string[]>> PricesAsync(IPage page) {
        var prices = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var count = await page.Locator("[data-testid^='provider-pricing-row-']").CountAsync();
        for (var row = 0; row < count; row++) {
            var name = await page.GetByTestId($"provider-pricing-model-{row}").InputValueAsync();
            var values = new List<string>();
            foreach (var field in PriceFields) {
                values.Add(await page.GetByTestId($"provider-pricing-{field}-{row}").InputValueAsync());
            }
            prices.Add(name, values.ToArray());
        }
        return prices;
    }

    internal static async Task AssertThinkingParityAsync(IPage central, IPage client, string sourceUrl, string clientUrl,
        ProviderProfile source, ProviderProfile target) {
        await OpenAsync(central, sourceUrl, source.Name);
        await central.GetByTestId("provider-editor-tab-thinking").ClickAsync();
        await OpenAsync(client, clientUrl, target.Name);
        await client.GetByTestId("provider-editor-tab-thinking").ClickAsync();
        foreach (var model in target.ModelCatalog) {
            await central.GetByTestId("provider-thinking-search").FillAsync(model.DisplayName);
            await client.GetByTestId("provider-thinking-search").FillAsync(model.DisplayName);
            var sourceRow = central.GetByTestId("provider-thinking-table").Locator("tbody tr").Filter(new() {
                Has = central.GetByRole(AriaRole.Cell, new() { Name = model.DisplayName, Exact = true }) });
            var clientRow = client.GetByTestId("provider-thinking-table").Locator("tbody tr").Filter(new() {
                Has = client.GetByRole(AriaRole.Cell, new() { Name = model.DisplayName, Exact = true }) });
            await Assertions.Expect(sourceRow).ToHaveCountAsync(1);
            await Assertions.Expect(clientRow).ToHaveCountAsync(1);
            Assert.Equal((await sourceRow.Locator("td").AllTextContentsAsync()).Skip(2).Take(3),
                (await clientRow.Locator("td").AllTextContentsAsync()).Skip(2).Take(3));
        }
        await client.GetByTestId("provider-thinking-search").FillAsync(string.Empty);
        Assert.Empty(await client.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).AllAsync());
    }

    private static async Task ImportAsync(IPage page, string address, IReadOnlyList<string> names, bool resume) {
        await NavigateAsync(page, address + "/agents?tab=providers");
        await page.GetByTestId("providers-connections").ClickAsync();
        var source = page.GetByTestId("shared-provider-source-card").Filter(new() { HasTextString = SourceName });
        await page.GetByTestId("shared-provider-source-add").WaitForAsync();
        if (!resume || await source.CountAsync() == 0) {
            await page.GetByTestId("shared-provider-source-add").ClickAsync();
            await page.GetByTestId("shared-provider-source-name").FillAsync(SourceName);
            await page.GetByTestId("shared-provider-source-uri").FillAsync("http://central:8080");
            await page.GetByTestId("shared-provider-source-secret").SelectOptionAsync(new SelectOptionValue { Label = SourceSecretName });
            await page.GetByTestId("shared-provider-source-dialog").GetByText("Allow HTTP on a private network", new() { Exact = true }).ClickAsync();
            await page.GetByTestId("shared-provider-source-save").ClickAsync();
            await page.GetByTestId("shared-provider-source-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }
        await Assertions.Expect(source).ToHaveCountAsync(1);
        await source.GetByTestId("shared-provider-source-test").ClickAsync();
        await page.GetByText("Source connection passed", new() { Exact = true }).WaitForAsync();
        await source.GetByTestId("shared-provider-source-discover").ClickAsync();
        var dialog = page.GetByTestId("shared-provider-catalog-dialog");
        foreach (var name in names) {
            await dialog.Locator("label").Filter(new() { Has = page.GetByText(name, new() { Exact = true }) })
                .GetByTestId("shared-provider-catalog-selection").CheckAsync();
        }
        await page.GetByTestId("shared-provider-catalog-apply").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.GetByTestId("shared-provider-connections-close").ClickAsync();
    }

    internal static async Task SyncAsync(IPage page, string address) {
        await NavigateAsync(page, address + "/agents?tab=providers");
        await page.GetByTestId("providers-connections").ClickAsync();
        await page.GetByTestId("shared-provider-source-card").Filter(new() { HasTextString = SourceName })
            .GetByTestId("shared-provider-source-sync").ClickAsync();
        await page.GetByText("Source synchronized", new() { Exact = true }).WaitForAsync();
        await page.GetByTestId("shared-provider-connections-close").ClickAsync();
    }

    internal static async Task<string> IssueTokenAsync(IPage page, string address, bool shared, IEnumerable<string>? additionalScopes = null) {
        await NavigateAsync(page, address + "/settings?tab=api-access");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Token subject", Exact = true }).FillAsync("pp2-native-ui");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Token display name", Exact = true }).FillAsync("PP2 native UI verification");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Token lifetime minutes", Exact = true }).FillAsync("240");
        var scopes = new List<string> { ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.ExecuteAgents,
            ApiAccessScopeNames.ReadLlmChats, ApiAccessScopeNames.ManageLlmChats, ApiAccessScopeNames.ExecuteLlmChats,
            ApiAccessScopeNames.ReadProviderHistory, ApiAccessScopeNames.ReadProviderHistoryContent };
        if (shared) {
            scopes.AddRange([ApiAccessScopeNames.ReadSharedProviderCatalog, ApiAccessScopeNames.InvokeSharedProviders]);
        }
        scopes.AddRange(additionalScopes ?? []);
        await page.GetByTestId("api-token-scopes").FillAsync(string.Join(' ', scopes));
        await page.GetByTestId("api-token-scopes").PressAsync("Tab");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create token", Exact = true }).ClickAsync();
        var field = page.Locator("textarea[readonly]");
        await field.WaitForAsync();
        var token = await field.InputValueAsync();
        Assert.True(token.StartsWith("eyJ", StringComparison.Ordinal), "Token issuance did not return a JWT.");
        await NavigateAsync(page, address + "/agents?tab=providers");
        return token;
    }

    internal static HttpClient Api(string address, string token) {
        var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) {
            BaseAddress = new(address + "/"), Timeout = TimeSpan.FromSeconds(60)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    internal static async Task<ProviderProfile[]> ProfilesAsync(HttpClient api) =>
        await api.GetFromJsonAsync<ProviderProfile[]>("api/agents/providers", Json) ?? throw new InvalidOperationException("Provider read returned no document.");
    private static Task<IBrowserContext> DesktopAsync(IBrowser browser) => browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
    private static Task OpenAsync(IPage page, string address, string name) => SharedProviderMetadataUiChecks.OpenProviderAsync(page, address, name);
    private static Task NavigateAsync(IPage page, string address) => SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, address);
    private static object SafeProfile(ProviderProfile profile) => new { profile.Id, profile.Name, profile.Kind, profile.Purpose, profile.Transport,
        profile.DefaultModel, profile.SuggestedModels, profile.ModelCatalog, profile.ModelPrices, profile.SupportsStreaming, profile.SupportsTools };
    private static Task EvidenceAsync(Settings settings, string name, object value) =>
        File.WriteAllTextAsync(Path.Combine(settings.Evidence, name + ".json"), JsonSerializer.Serialize(value, Json));
    private static Task ScreenshotAsync(IPage page, Settings settings, string name) =>
        page.ScreenshotAsync(new() { Path = Path.Combine(settings.Evidence, name + ".png"), FullPage = false });

    internal sealed record Settings(string Central, string[] Clients, string Evidence, bool Resume) {
        public static Settings Load() {
            if (Environment.GetEnvironmentVariable("CANDOITALL_ALLOW_SHARED_PROVIDER_FIXTURE_WRITES") != "1") {
                throw new InvalidOperationException("Owned fixture writes require explicit opt-in.");
            }
            var root = Path.GetFullPath(Environment.GetEnvironmentVariable("CANDOITALL_SHARED_PP2_FIXTURE_ROOT")
                ?? throw new InvalidOperationException("The owned PP2 fixture root is required."));
            if (!Path.GetFileName(root).StartsWith("shared-providers-e2e-pp2-", StringComparison.Ordinal) ||
                File.ReadAllText(Path.Combine(root, ".shared-providers-e2e-root")).Trim() != "CanDoItAll.SharedProviders.E2E/v1") {
                throw new InvalidOperationException("This is not a marked owned PP2 fixture.");
            }
            using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "host-run-metadata.json")));
            var port = metadata.RootElement.GetProperty("portBase").GetInt32();
            Assert.Equal(root, metadata.RootElement.GetProperty("artifactRoot").GetString());
            Assert.Equal("candoitall-" + Path.GetFileName(root), metadata.RootElement.GetProperty("composeProjectName").GetString());
            var evidence = Path.GetFullPath(Environment.GetEnvironmentVariable("CANDOITALL_SHARED_UI_EVIDENCE_DIRECTORY")
                ?? throw new InvalidOperationException("A task-owned evidence directory is required."));
            Directory.CreateDirectory(evidence);
            File.Copy(Path.Combine(root, "host-run-metadata.json"), Path.Combine(evidence, "image-source-provenance.json"), overwrite: true);
            return new($"http://127.0.0.1:{port}", [$"http://127.0.0.1:{port + 1}", $"http://127.0.0.1:{port + 2}"], evidence,
                Environment.GetEnvironmentVariable("CANDOITALL_SHARED_PP2_RESUME") == "1");
        }
    }
}
