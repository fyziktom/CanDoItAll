using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderModelPricingEditorTests : BunitContext
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Source_managed_empty_prices_remain_empty_and_private_flag_is_not_inferred(bool isPrivate) {
        var model = new ProviderProfileEditorModel { IsPrivateProvider = isPrivate, ModelPrices = [] };
        var cut = Render<ProviderModelPricingEditor>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.SourceManaged, true)
            .Add(component => component.PricingKind, ProviderKind.OpenAi)
            .Add(component => component.DefaultModel, "source-model"));

        Assert.Empty(model.ModelPrices);
        Assert.Equal(isPrivate, model.IsPrivateProvider);
        Assert.Contains("unpriced", cut.Find("[data-testid='provider-pricing-unavailable']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='provider-pricing-reset-button']"));
        Assert.Empty(cut.FindAll("[data-testid='provider-pricing-add-button']"));
        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
    }

    [Fact]
    public void Renders_existing_model_prices_as_editable_rows()
    {
        var model = new ProviderProfileEditorModel
        {
            IsPrivateProvider = true,
            ModelPrices =
            [
                new ProviderModelTokenPriceEditorModel
                {
                    Model = "api-model",
                    InputPerMillionTokensUsd = 1.25m,
                    CachedInputPerMillionTokensUsd = 0.125m,
                    OutputPerMillionTokensUsd = 5.00m
                }
            ]
        };

        var cut = Render<ProviderModelPricingEditor>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.PricingKind, ProviderKind.OpenAi)
            .Add(component => component.DefaultModel, "api-model"));

        Assert.Equal("api-model", ReadInputValue(cut, "provider-pricing-model-0"));
        Assert.Equal("1.25", ReadInputValue(cut, "provider-pricing-input-0"));
        Assert.Equal("0.125", ReadInputValue(cut, "provider-pricing-cached-0"));
        Assert.Equal("5.00", ReadInputValue(cut, "provider-pricing-output-0"));
        Assert.NotNull(cut.Find("[data-testid='provider-pricing-row-0']"));
    }

    [Fact]
    public void Add_model_price_creates_named_manual_row()
    {
        var model = new ProviderProfileEditorModel
        {
            ModelPrices =
            [
                new ProviderModelTokenPriceEditorModel
                {
                    Model = "existing-model",
                    InputPerMillionTokensUsd = 1.00m,
                    CachedInputPerMillionTokensUsd = 0.10m,
                    OutputPerMillionTokensUsd = 4.00m
                }
            ]
        };

        var cut = Render<ProviderModelPricingEditor>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.PricingKind, ProviderKind.OpenAi)
            .Add(component => component.DefaultModel, "existing-model"));

        cut.Find("[data-testid='provider-pricing-add-button']").Click();

        Assert.Contains(model.ModelPrices, price => price.Model == "custom-model");
        Assert.Equal("custom-model", ReadInputValue(cut, "provider-pricing-model-1"));
    }

    private static string? ReadInputValue(
        IRenderedComponent<ProviderModelPricingEditor> cut,
        string testId)
    {
        return ((IHtmlInputElement)cut.Find($"[data-testid='{testId}']")).Value;
    }
}
