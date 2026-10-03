using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class SharedProviderCustomMetadataTests {
    private const string ProviderName = "PP2 OpenAI chat completions";
    private const string CustomDefault = "PP2C-Custom/Exact";
    private const string UnknownModel = "PP2C-Unknown:Variant";
    private const string DatedModel = "gpt-4.1-2025-04-14";

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_custom_catalog_prices_thinking_and_metadata_only_refresh_preserve_local_draft() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        await using var context = await fixture.Page.Context.Browser!.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        await using var clientBContext = await fixture.Page.Context.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        await using var syncContext = await fixture.Page.Context.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var central = await context.NewPageAsync();
        var clientB = await clientBContext.NewPageAsync();
        var syncA = await syncContext.NewPageAsync();
        using var sourceApi = SharedProviderNativeDefaultsUiTests.Api(fixture.Settings.Central,
            await SharedProviderNativeDefaultsUiTests.IssueTokenAsync(central, fixture.Settings.Central, true));
        using var clientBApi = SharedProviderNativeDefaultsUiTests.Api(fixture.Settings.Clients[1],
            await SharedProviderNativeDefaultsUiTests.IssueTokenAsync(clientB, fixture.Settings.Clients[1], false));
        var source = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(sourceApi), item => item.Name == ProviderName);
        var before = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api), item => item.Name == ProviderName);
        await SharedProviderMetadataUiChecks.OpenProviderAsync(fixture.Page, fixture.Settings.Clients[0], ProviderName);
        await fixture.Page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        var originalAlias = await fixture.Page.GetByTestId("shared-provider-import-alias").InputValueAsync();
        var dirtyAlias = "PP2C metadata draft " + Guid.NewGuid().ToString("N")[..8];
        await fixture.Page.GetByTestId("shared-provider-import-alias").FillAsync(dirtyAlias);
        await SharedProviderMetadataUiChecks.OpenProviderAsync(central, fixture.Settings.Central, ProviderName);
        await central.GetByTestId("providers-model-input").FillAsync(CustomDefault);
        await central.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        await central.GetByTestId("providers-suggested-models").FillAsync(string.Join('\n', new[] { CustomDefault }.Concat(source.SuggestedModels)));
        await central.GetByTestId("providers-suggested-models").PressAsync("Tab");
        await central.GetByTestId("providers-save").ClickAsync();
        await central.GetByText("Provider saved", new() { Exact = true }).WaitForAsync();
        string[] models = [CustomDefault, UnknownModel, DatedModel, "gpt-4.1", OpenAiModelIds.Gpt54Mini];
        await SharedProviderMetadataUiChecks.ConfigureAsync(central, fixture.Settings.Central, ProviderName,
            CustomDefault, false, 0.00012345m, models[1..]);
        await central.GetByTestId("provider-editor-tab-thinking").ClickAsync();
        await central.GetByTestId("provider-thinking-search").FillAsync(CustomDefault);
        await central.GetByRole(AriaRole.Button, new() { Name = "Edit thinking for " + CustomDefault, Exact = true }).ClickAsync();
        await central.GetByTestId("thinking-automatic").UncheckAsync();
        await central.GetByTestId("thinking-supported").CheckAsync();
        foreach (var effort in Enum.GetValues<AgentReasoningEffortLevel>()) {
            await central.GetByTestId("thinking-allow-" + effort).SetCheckedAsync(effort is AgentReasoningEffortLevel.None or AgentReasoningEffortLevel.High);
        }
        await central.GetByTestId("thinking-model-default").SelectOptionAsync(nameof(AgentReasoningEffortLevel.None));
        await central.GetByTestId("thinking-apply").ClickAsync();
        await central.GetByTestId("providers-save").ClickAsync();
        await central.GetByText("Provider saved", new() { Exact = true }).WaitForAsync();
        source = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(sourceApi), item => item.Id == source.Id);
        Assert.Equal(CustomDefault, source.DefaultModel);
        Assert.Equal(AgentReasoningEffortLevel.None, AgentThinkingEffortPolicy.ResolveProviderDefault(source, CustomDefault));
        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown, AgentThinkingEffortPolicy.ResolveCapability(source, UnknownModel).Status);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Defined, AgentThinkingEffortPolicy.ResolveCapability(source, OpenAiModelIds.Gpt54Mini).Source);
        var catalog = SharedProviderProtocolJson.DeserializeCatalog(await sourceApi.GetStringAsync(SharedProviderRoutes.Catalog));
        var publication = Assert.Single(catalog.Providers, item => item.DisplayName == ProviderName);
        Assert.Equal(models.Order(StringComparer.Ordinal), publication.Models.Select(model => model.DisplayName).Order(StringComparer.Ordinal));
        Assert.All(publication.Models, model => Assert.Equal(SharedProviderRoutingModelIdCodec.Create(publication.PublicationId, model.DisplayName), model.Id));
        await SharedProviderNativeDefaultsUiTests.SyncAsync(syncA, fixture.Settings.Clients[0]);
        await fixture.Page.GetByTestId("providers-refresh").ClickAsync();
        await Assertions.Expect(fixture.Page.GetByTestId("shared-provider-import-alias")).ToHaveValueAsync(dirtyAlias);
        await Assertions.Expect(fixture.Page.GetByTestId("shared-provider-import-conflict")).ToHaveCountAsync(0);
        await Assertions.Expect(fixture.Page.GetByTestId("shared-provider-import-save")).ToBeEnabledAsync();
        await fixture.ScreenshotAsync("custom-metadata-preserves-local-draft");
        await fixture.Page.GetByTestId("shared-provider-import-alias").FillAsync(originalAlias);
        foreach (var (page, api, address) in new[] {
            (fixture.Page, fixture.Api, fixture.Settings.Clients[0]), (clientB, clientBApi, fixture.Settings.Clients[1])
        }) {
            await SharedProviderNativeDefaultsUiTests.SyncAsync(page, address);
            var imported = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(api), item => item.Name == ProviderName);
            Assert.Equal(models.Order(StringComparer.Ordinal), imported.ModelCatalog.Select(model => model.DisplayName).Order(StringComparer.Ordinal));
            Assert.Equal(publication.DefaultModelId.Value, imported.DefaultModel);
            Assert.NotNull(imported.ModelSelectionConstraint);
            Assert.Equal(imported.ModelCatalog.Select(model => model.Id).Order(), imported.ModelSelectionConstraint.AllowedModels.Order());
            Assert.Equal(new[] { "gpt-4.1", OpenAiModelIds.Gpt54Mini }.Order(), imported.SuggestedModels.Select(imported.GetModelDisplayName).Order());
            Assert.Equal(publication.Models.Where(model => model.IsSuggested).Select(model => model.DisplayName).Order(),
                imported.SuggestedModels.Select(imported.GetModelDisplayName).Order());
            Assert.Equal(AgentReasoningEffortLevel.None, AgentThinkingEffortPolicy.ResolveProviderDefault(imported, imported.DefaultModel));
            await SharedProviderNativeDefaultsUiTests.AssertPriceParityAsync(central, page, fixture.Settings.Central, address, source, imported);
            await SharedProviderNativeDefaultsUiTests.AssertThinkingParityAsync(central, page, fixture.Settings.Central, address, source, imported);
            await SharedProviderMetadataUiChecks.AssertSourceAgentChoicesAsync(page, address, ProviderName, CustomDefault,
                imported.SuggestedModels.Select(imported.GetModelDisplayName).Append(CustomDefault).Distinct(StringComparer.Ordinal).ToArray(),
                fixture.Settings.Evidence, "custom-" + new Uri(address).Port);
        }
        var after = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api), item => item.Id == before.Id);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.IsEnabled, after.IsEnabled);
        await fixture.EvidenceAsync("custom-catalog", new { SourceId = source.Id, ImportId = after.Id, publication,
            ModelPrices = after.ModelPrices, after.ModelThinkingEffortCapabilities, DirtyAliasPreserved = true, ExplicitNone = true });
        await AssertSavedChoiceRemovalAsync(fixture, central, syncA, after, models);
        await SharedProviderNativeDefaultsUiTests.SyncAsync(clientB, fixture.Settings.Clients[1]);
        var clients = new[] { sourceApi, fixture.Api, clientBApi };
        var beforeRestart = new List<JsonElement>();
        foreach (var api in clients) {
            beforeRestart.Add(await ReadSafeSnapshotAsync(api));
        }
        foreach (var page in new[] { context, clientBContext, syncContext }.SelectMany(owner => owner.Pages)) {
            await page.GotoAsync("about:blank");
        }
        await fixture.RestartOwnedAppsAsync();
        var afterRestart = new List<JsonElement>();
        foreach (var api in clients) {
            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            while (true) {
                try {
                    using var response = await api.GetAsync("health", ready.Token);
                    if (response.IsSuccessStatusCode) {
                        break;
                    }
                } catch (HttpRequestException) when (!ready.IsCancellationRequested) {
                }
                await Task.Delay(250, ready.Token);
            }
            afterRestart.Add(await ReadSafeSnapshotAsync(api));
        }
        for (var index = 0; index < beforeRestart.Count; index++) {
            Assert.True(JsonElement.DeepEquals(beforeRestart[index], afterRestart[index]), $"Native catalog {index} changed across its owned restart.");
        }
        await SharedProviderMetadataUiChecks.OpenProviderAsync(fixture.Page, fixture.Settings.Clients[0], ProviderName);
        await Assertions.Expect(fixture.Page.GetByTestId("providers-model-input")).ToHaveValueAsync(CustomDefault);
        await fixture.ScreenshotAsync("custom-metadata-after-restart");
        await fixture.EvidenceAsync("custom-restart-metadata", new { Before = beforeRestart, After = afterRestart, Identical = true });
    }

    private static async Task<JsonElement> ReadSafeSnapshotAsync(HttpClient api) => JsonSerializer.SerializeToElement(
        (await SharedProviderNativeDefaultsUiTests.ProfilesAsync(api)).OrderBy(profile => profile.Id).Select(profile => new {
            profile.Id, profile.Name, profile.Kind, profile.Purpose, profile.DefaultModel, profile.IsEnabled,
            profile.SuggestedModels, profile.ModelCatalog, profile.ModelPrices, profile.ModelThinkingEffortCapabilities,
            profile.ModelSelectionConstraint, profile.SupportsStreaming, profile.SupportsTools
        }), SharedProviderConsumerFixture.Json);

    private static async Task AssertSavedChoiceRemovalAsync(SharedProviderConsumerFixture fixture, IPage central, IPage sync,
        ProviderProfile provider, string[] models) {
        var dated = Assert.Single(provider.ModelCatalog, item => item.DisplayName == DatedModel).Id;
        Assert.DoesNotContain(dated, provider.SuggestedModels);
        var suffix = Guid.NewGuid().ToString("N");
        var agentId = await fixture.PostAsync<Guid>("api/agents", new AgentEditorModel {
            Name = "PP2C saved dated " + suffix, ProviderProfileId = provider.Id, Model = dated,
            Status = AgentLifecycleStatus.Active, Instructions = "No tools. Preserve the exact saved model."
        });
        var definitionName = "PP2C saved dated chat " + suffix;
        var definition = await fixture.PostAsync<JsonElement>("api/llm-chats", new {
            name = definitionName, summary = "Saved catalog choice", avatarImageUrl = "", systemPrompt = "Reply briefly.",
            providerProfileId = provider.Id, model = dated, revisionReason = "Owned fixture saved choice", tags = Array.Empty<string>()
        });
        var definitionId = definition.GetProperty("id").GetGuid();
        await OpenAgent();
        await Assertions.Expect(AgentModel().Locator("option:checked")).ToHaveTextAsync(DatedModel);
        await fixture.Page.GetByTestId("agents-catalog-save").ClickAsync();
        await fixture.Page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
        Assert.Equal(dated, (await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agentId:D}", SharedProviderConsumerFixture.Json))!.Model);
        await OpenChat();
        await Assertions.Expect(ChatModel().Locator("option:checked")).ToHaveTextAsync(DatedModel);
        await fixture.Page.GetByTestId("llm-chat-definition-editor-save").ClickAsync();
        await fixture.Page.GetByTestId("llm-chat-definition-editor-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        Assert.Equal(dated, (await fixture.GetAsync($"api/llm-chats/{definitionId:D}")).GetProperty("model").GetString());
        await SharedProviderMetadataUiChecks.ConfigureAsync(central, fixture.Settings.Central, ProviderName,
            CustomDefault, false, 0.00012345m, models.Where(model => model != DatedModel && model != CustomDefault).ToArray());
        await SharedProviderNativeDefaultsUiTests.SyncAsync(sync, fixture.Settings.Clients[0]);
        await OpenAgent();
        await Assertions.Expect(AgentModel().Locator("option:checked")).ToHaveTextAsync("Unavailable shared model — select a published model");
        await Assertions.Expect(fixture.Page.GetByTestId("agents-catalog-model-override")).ToHaveCountAsync(0);
        await fixture.ScreenshotAsync("saved-dated-agent-unavailable");
        Assert.Equal(dated, (await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agentId:D}", SharedProviderConsumerFixture.Json))!.Model);
        var marker = "PP2C_REMOVED_" + suffix;
        using var refused = await fixture.Api.PostAsJsonAsync($"api/agents/providers/{provider.Id:D}/test-chat",
            new ProviderTestChatRequest(dated, "", [], marker));
        Assert.False(refused.IsSuccessStatusCode);
        Assert.DoesNotContain((await fixture.ReadCapturesAsync()).GetProperty("requests").EnumerateArray(),
            request => request.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal));
        await AgentModel().SelectOptionAsync(new SelectOptionValue { Label = $"Provider default ({CustomDefault})" });
        await fixture.Page.GetByTestId("agents-catalog-save").ClickAsync();
        await fixture.Page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
        Assert.Empty((await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agentId:D}", SharedProviderConsumerFixture.Json))!.Model);
        await OpenChat();
        await Assertions.Expect(ChatModel().Locator("option:checked")).ToHaveTextAsync("Unavailable shared model — select a published model");
        await Assertions.Expect(fixture.Page.GetByTestId("llm-chat-definition-model-override")).ToHaveCountAsync(0);
        await fixture.ScreenshotAsync("saved-dated-chat-unavailable");
        Assert.Equal(dated, (await fixture.GetAsync($"api/llm-chats/{definitionId:D}")).GetProperty("model").GetString());
        await ChatModel().SelectOptionAsync(new SelectOptionValue { Label = $"Provider default ({CustomDefault})" });
        await fixture.Page.GetByTestId("llm-chat-definition-editor-save").ClickAsync();
        await fixture.Page.GetByTestId("llm-chat-definition-editor-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        Assert.Equal(provider.DefaultModel, (await fixture.GetAsync($"api/llm-chats/{definitionId:D}")).GetProperty("model").GetString());
        await fixture.EvidenceAsync("saved-dated-choice", new { agentId, definitionId, RemovedRoute = dated, SourceName = DatedModel,
            SavedNonSuggestedRetained = true, UnavailableExplicit = true, DispatchRefused = true, UpstreamCalled = false, OperatorCorrected = true });

        ILocator AgentModel() => fixture.Page.GetByTestId("agents-catalog-model-choice");
        ILocator ChatModel() => fixture.Page.GetByTestId("llm-chat-definition-model");
        async Task OpenAgent() {
            await fixture.NavigateAsync($"/agents?tab=agents&agentId={agentId:D}");
            await fixture.Page.GetByTestId("agents-details-dialog").GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        }
        async Task OpenChat() {
            await fixture.NavigateAsync("/agents?tab=simple-chats&simpleChatView=definitions");
            await fixture.Page.Locator("article[data-testid^='llm-chat-definition-']").Filter(new() { HasTextString = definitionName })
                .GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
            await fixture.Page.GetByTestId("llm-chat-definition-tab-runtime").ClickAsync();
        }
    }
}
