using System.Net;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderCatalogRefreshTests {
    [Fact]
    public async Task Kind_change_clears_old_state_and_draft_discovery_saves_the_full_unpriced_catalog() {
        var http = new CatalogHttpClientFactory(HttpStatusCode.OK,
            """{"models":[{"name":"gpt-oss:20b"},{"name":"gemma3:4b"}]}""");
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IHttpClientFactory>(http));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-tree-provider']");
        cut.WaitForElement("[data-testid='providers-new']").Click();
        cut.Find("[data-testid='providers-name-input']").Change("Real inventory test");
        cut.Find("[data-testid='providers-model-input']").Change("gpt-5.4-mini");
        cut.Find("[data-testid='providers-kind-select']").Change(ProviderKind.Ollama.ToString());

        Assert.Equal(string.Empty, Input(cut, "providers-model-input"));
        Assert.Equal(string.Empty, Input(cut, "providers-base-url-input"));
        cut.Find("[data-testid='providers-base-url-input']").Change("http://127.0.0.1:11434");
        await OpenTabAsync(cut, "Prices");
        Assert.Empty(cut.FindComponent<ProviderModelPricingEditor>().Instance.Model.ModelPrices);
        await cut.WaitForElement("[data-testid='provider-pricing-refresh-button']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal(2,
            cut.FindAll("[data-testid='provider-pricing-unpriced-model']").Count));
        Assert.Equal("http://127.0.0.1:11434/api/tags", http.RequestUri?.ToString());
        Assert.Equal(new[] { "gpt-oss:20b", "gemma3:4b" }.Order(),
            cut.FindComponent<ProviderModelPricingEditor>().Instance.ModelNames!.Order());
        Assert.Empty(cut.FindComponent<ProviderModelPricingEditor>().Instance.Model.ModelPrices);

        await OpenTabAsync(cut, "Connection");
        cut.Find("[data-testid='providers-model-input']").Change("gpt-oss:20b");
        await cut.Find("form").SubmitAsync();
        var runtime = harness.Context.Services.GetRequiredService<IProviderRuntimeAdministrationService>();
        var saved = Assert.Single(await runtime.ListProvidersAsync(), provider => provider.Name == "Real inventory test");
        Assert.Equal(new[] { "gpt-oss:20b", "gemma3:4b" }.Order(), saved.SuggestedModels.Order());
        Assert.Empty(saved.ModelPrices);
        Assert.True(saved.IsPrivateProvider);
        Assert.Empty(saved.ApiKeyEnvironmentVariable);
        Assert.DoesNotContain("openai", saved.Tags);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "{}")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"models\":[]}")]
    public async Task Failed_or_empty_refresh_preserves_the_existing_editor_catalog(HttpStatusCode status, string payload) {
        var http = new CatalogHttpClientFactory(status, payload);
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IHttpClientFactory>(http));
        var runtime = harness.Context.Services.GetRequiredService<IProviderRuntimeAdministrationService>();
        await runtime.SaveProviderAsync(new ProviderProfileEditorModel {
            Name = "AAA existing inventory",
            Kind = ProviderKind.Ollama,
            Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434",
            DefaultModel = "gemma3:4b",
            SuggestedModels = ["gemma3:4b"],
            ModelPrices = [new() { Model = "gemma3:4b", InputPerMillionTokensUsd = 0.1m }]
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-tree-provider']");
        cut.FindAll("[data-testid='providers-tree-provider']")
            .First(node => node.TextContent.Contains("AAA existing inventory")).Click();
        cut.WaitForAssertion(() => Assert.Equal("AAA existing inventory", Input(cut, "providers-name-input")));
        await OpenTabAsync(cut, "Prices");
        await cut.WaitForElement("[data-testid='provider-pricing-refresh-button']").ClickAsync(new());

        var editor = cut.FindComponent<ProviderModelPricingEditor>().Instance;
        Assert.Equal("gemma3:4b", Assert.Single(editor.ModelNames!));
        Assert.Equal(0.1m, Assert.Single(editor.Model.ModelPrices).InputPerMillionTokensUsd);
        Assert.NotNull(http.RequestUri);
    }

    private static Task OpenTabAsync(IRenderedComponent<AgentProviderProfilesPanel> cut, string name) {
        return cut.InvokeAsync(() => cut.FindAll("button[role='tab']")
            .Single(button => button.TextContent.Contains(name, StringComparison.Ordinal)).Click());
    }

    private static string Input(IRenderedComponent<AgentProviderProfilesPanel> cut, string testId) =>
        ((IHtmlInputElement)cut.Find($"[data-testid='{testId}']")).Value;

    private sealed class CatalogHttpClientFactory(HttpStatusCode status, string payload) : IHttpClientFactory {
        public Uri? RequestUri { get; private set; }

        public HttpClient CreateClient(string name) => new(new Handler(request => {
            RequestUri = request.RequestUri;
            return new HttpResponseMessage(status) { Content = new StringContent(payload) };
        }));

        private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(respond(request));
        }
    }
}
