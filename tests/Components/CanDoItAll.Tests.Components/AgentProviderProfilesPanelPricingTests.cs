using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentProviderProfilesPanelPricingTests
{
    [Fact]
    public async Task Provider_editor_surfaces_model_prices_on_dedicated_prices_tab()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var secretRecordId = Guid.NewGuid();

        await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "AAA Priced Provider",
            Kind = ProviderKind.OpenAi,
            BaseUrl = "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable = $"secret:{secretRecordId:D}",
            DefaultModel = "priced-model",
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            SuggestedModels = ["priced-model"],
            ModelPrices =
            [
                new ProviderModelTokenPriceEditorModel
                {
                    Model = "priced-model",
                    InputPerMillionTokensUsd = 1.25m,
                    CachedInputPerMillionTokensUsd = 0.125m,
                    OutputPerMillionTokensUsd = 5.00m
                }
            ]
        });
        var savedProvider = Assert.Single(
            await workspaceService.ListProvidersAsync(),
            provider => provider.Name == "AAA Priced Provider");
        Assert.Contains(savedProvider.ModelPrices, price => price.Model == "priced-model");

        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='provider-editor-tabs']");
        cut.WaitForElement("[data-testid='providers-tree-provider']");
        var providerNode = cut.FindAll("[data-testid='providers-tree-provider']")
            .First(node => node.TextContent.Contains("AAA Priced Provider", StringComparison.OrdinalIgnoreCase));
        providerNode.Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                cut.FindAll("h2"),
                heading => heading.TextContent.Contains("AAA Priced Provider", StringComparison.OrdinalIgnoreCase));
        });

        await cut.InvokeAsync(() =>
            cut.FindAll("button[role='tab']")
                .Single(button => button.TextContent.Contains("Prices", StringComparison.OrdinalIgnoreCase))
                .Click());

        cut.WaitForElement("[data-testid='provider-pricing-row-0']");
        var modelInput = (IHtmlInputElement)cut.Find("[data-testid='provider-pricing-model-0']");
        Assert.Equal("priced-model", modelInput.Value);
        Assert.NotNull(cut.Find("[data-testid='provider-pricing-refresh-button']"));
    }
}
