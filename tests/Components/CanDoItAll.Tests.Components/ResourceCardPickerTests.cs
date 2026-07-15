using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ResourceCardPickerTests : TestContext
{
    private static readonly Guid AlphaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BetaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public ResourceCardPickerTests()
    {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Search_filters_across_card_content()
    {
        var cut = RenderPicker(CreateOptions());

        cut.Find("[data-testid='resource-picker-search']").Input("workflow");

        Assert.Empty(cut.FindAll("[data-testid='resource-option-alpha']"));
        Assert.Single(cut.FindAll("[data-testid='resource-option-beta']"));
    }

    [Fact]
    public void Enabled_card_raises_strongly_typed_selection()
    {
        Guid? selectedId = null;
        var cut = RenderPicker(
            CreateOptions(),
            selected => selectedId = selected);

        cut.Find("[data-testid='resource-option-alpha']").Click();

        Assert.Equal(AlphaId, selectedId);
    }

    [Fact]
    public void Disabled_card_exposes_reason_and_does_not_select()
    {
        Guid? selectedId = null;
        var options = CreateOptions()
            .Select(option => option.Item == BetaId
                ? option with
                {
                    IsDisabled = true,
                    DisabledReason = "Publish this workflow before selecting it."
                }
                : option)
            .ToList();
        var cut = RenderPicker(options, selected => selectedId = selected);

        var disabledCard = cut.Find("[data-testid='resource-option-beta']");
        disabledCard.Click();

        Assert.True(disabledCard.HasAttribute("disabled"));
        Assert.Null(selectedId);
        Assert.Contains("Publish this workflow before selecting it.", cut.Markup);
    }

    [Fact]
    public void Favorite_affordance_is_controlled_and_favorites_render_first()
    {
        Guid? favoriteId = null;
        var options = CreateOptions()
            .Select(option => option.Item == BetaId
                ? option with
                {
                    ShowFavorite = true,
                    IsFavorite = true
                }
                : option)
            .ToList();
        var cut = RenderPicker(
            options,
            favoriteToggled: selected => favoriteId = selected);

        var cards = cut.FindAll(".resource-card-picker__card");
        Assert.Equal("resource-option-beta-shell", cards.First().GetAttribute("data-testid"));

        var favoriteButton = cut.Find("[data-testid='resource-option-beta-favorite']");
        Assert.Equal("true", favoriteButton.GetAttribute("aria-pressed"));

        favoriteButton.Click();

        Assert.Equal(BetaId, favoriteId);
        Assert.Empty(cut.FindAll("[data-testid='resource-option-alpha-favorite']"));
    }

    [Fact]
    public void Results_use_bounded_internal_viewport_by_default()
    {
        var cut = RenderPicker(CreateOptions());

        var results = cut.Find("[data-testid='resource-picker-results']");

        Assert.Contains("resource-card-picker__results--bounded", results.ClassList);
        Assert.DoesNotContain("resource-card-picker__results--unbounded", results.ClassList);
        Assert.Null(results.QuerySelector("[data-testid='resource-picker-search']"));
        Assert.DoesNotContain("2 of 2 options", results.TextContent);
        Assert.Contains("2 of 2 options", cut.Markup);
    }

    [Fact]
    public void Consumer_can_opt_out_of_bounded_results_viewport()
    {
        var cut = RenderPicker(
            CreateOptions(),
            useBoundedResultsViewport: false);

        var results = cut.Find("[data-testid='resource-picker-results']");

        Assert.Contains("resource-card-picker__results--unbounded", results.ClassList);
        Assert.DoesNotContain("resource-card-picker__results--bounded", results.ClassList);
    }

    private IRenderedComponent<ResourceCardPicker<Guid>> RenderPicker(
        IReadOnlyList<ResourceCardPickerOption<Guid>> options,
        Action<Guid>? selectionChanged = null,
        Action<Guid>? favoriteToggled = null,
        bool useBoundedResultsViewport = true)
    {
        return RenderComponent<ResourceCardPicker<Guid>>(parameters =>
        {
            parameters
                .Add(component => component.Options, options)
                .Add(component => component.DataTestId, "resource-picker")
                .Add(component => component.UseBoundedResultsViewport, useBoundedResultsViewport);

            if (selectionChanged is not null)
            {
                parameters.Add(component => component.SelectionChanged, selectionChanged);
            }

            if (favoriteToggled is not null)
            {
                parameters.Add(component => component.FavoriteToggled, favoriteToggled);
            }
        });
    }

    private static IReadOnlyList<ResourceCardPickerOption<Guid>> CreateOptions()
    {
        return
        [
            new ResourceCardPickerOption<Guid>(AlphaId, "Alpha Person", "Person")
            {
                Subtitle = "alpha@example.test",
                TestId = "resource-option-alpha",
                VisualKind = ResourceCardPickerVisualKind.Avatar
            },
            new ResourceCardPickerOption<Guid>(BetaId, "Beta Automation", "Workflow")
            {
                Description = "Reusable workflow definition",
                TestId = "resource-option-beta",
                Icon = "account_tree"
            }
        ];
    }
}
