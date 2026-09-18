using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Prompts.UI.Gallery;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGallerySearchSurfaceTests
{
    [Fact]
    public void Desktop_filters_share_one_rail_and_item_title_and_favorite_are_explicit()
    {
        var item = ScriptedPromptGalleryService.Item(title: "Reusable architecture review");
        var intents = new List<PromptGallerySearchIntent>();
        using var context = CreateContext();

        var cut = context.Render<PromptGallerySearchSurface>(parameters => parameters
            .Add(component => component.Presentation, Presentation([item]))
            .Add(component => component.Compact, false)
            .Add(component => component.ShowSelectAction, false)
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGallerySearchIntent>(this, intents.Add)));

        var rail = cut.Find("[data-testid='prompt-gallery-filter-rail']");
        Assert.Contains("--cda-grid-columns:", rail.GetAttribute("style"), StringComparison.Ordinal);
        Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-search']"));
        Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-kind-filter']"));
        Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-status-filter']"));
        Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-tag-filter']"));
        Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-favorites-filter']"));
        Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-provider-filter']"));
        Assert.NotNull(rail.QuerySelector("[data-testid='prompt-gallery-archived-filter']"));
        Assert.Contains("cda-text-block--subtitle2", cut.Find("[data-testid='prompt-gallery-grid']").InnerHtml, StringComparison.Ordinal);
        Assert.Contains("Model guidance", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("false", cut.Find("[data-testid='prompt-gallery-favorite']").GetAttribute("aria-pressed"));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-select']"));

        cut.Find("[data-testid='prompt-gallery-favorite']").Click();

        var favorite = Assert.IsType<PromptGallerySearchIntent.ToggleFavorite>(Assert.Single(intents));
        Assert.Same(item, favorite.Item);
        // The surface never flips the favorite itself; the owner re-renders it after the write.
        Assert.Equal("false", cut.Find("[data-testid='prompt-gallery-favorite']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Compact_mode_hides_desktop_only_filters_and_model_guidance()
    {
        using var context = CreateContext();

        var cut = context.Render<PromptGallerySearchSurface>(parameters => parameters
            .Add(component => component.Presentation, Presentation([ScriptedPromptGalleryService.Item()]))
            .Add(component => component.Compact, true)
            .Add(component => component.SelectText, "Insert"));

        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-provider-filter']"));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-model-filter']"));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-archived-filter']"));
        Assert.DoesNotContain("Model guidance", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Insert", cut.Find("[data-testid='prompt-gallery-select']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Discrete_filter_changes_are_immediate_and_typed_text_is_debounced()
    {
        var intents = new List<PromptGallerySearchIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGallerySearchSurface>(parameters => parameters
            .Add(component => component.Presentation, Presentation([ScriptedPromptGalleryService.Item()]))
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGallerySearchIntent>(this, intents.Add)));

        cut.Find("[data-testid='prompt-gallery-kind-filter']").Change("Part");
        cut.Find("[data-testid='prompt-gallery-favorites-filter']").Change(true);
        cut.Find("[data-testid='prompt-gallery-search']").Input("architecture");
        cut.Find("[data-testid='prompt-gallery-clear-filters']").Click();

        Assert.Equal(4, intents.Count);
        var kind = Assert.IsType<PromptGallerySearchIntent.ChangeFilters>(intents[0]);
        Assert.Equal(PromptGalleryItemKind.Part, kind.Filters.Kind);
        Assert.False(kind.Debounce);
        var favorites = Assert.IsType<PromptGallerySearchIntent.ChangeFilters>(intents[1]);
        Assert.True(favorites.Filters.FavoritesOnly);
        Assert.False(favorites.Debounce);
        var text = Assert.IsType<PromptGallerySearchIntent.ChangeFilters>(intents[2]);
        Assert.Equal("architecture", text.Filters.Text);
        Assert.True(text.Debounce);
        Assert.IsType<PromptGallerySearchIntent.ClearFilters>(intents[3]);
    }

    [Fact]
    public void Loading_error_and_empty_states_render_and_retry_emits_an_intent()
    {
        var intents = new List<PromptGallerySearchIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGallerySearchSurface>(parameters => parameters
            .Add(component => component.Presentation, Presentation([]) with { IsLoading = true })
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGallerySearchIntent>(this, intents.Add)));

        Assert.Contains("Searching reusable prompts", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-grid']"));

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, Presentation([]) with { LoadError = "Driver offline." }));
        Assert.Contains("Driver offline.", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='prompt-gallery-retry']").Click();
        Assert.IsType<PromptGallerySearchIntent.Retry>(Assert.Single(intents));

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, Presentation([])));
        Assert.Contains("No prompt items match these filters", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Paging_select_and_edit_emit_intents_with_the_rendered_item()
    {
        var item = ScriptedPromptGalleryService.Item();
        var intents = new List<PromptGallerySearchIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGallerySearchSurface>(parameters => parameters
            .Add(component => component.Presentation, Presentation([item]) with { PageIndex = 1, TotalPages = 3, TotalCount = 45 })
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGallerySearchIntent>(this, intents.Add)));

        Assert.Contains("Page 2 of 3", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='prompt-gallery-previous-page']").Click();
        cut.Find("[data-testid='prompt-gallery-next-page']").Click();
        cut.Find("[data-testid='prompt-gallery-select']").Click();
        cut.Find("[data-testid='prompt-gallery-edit']").Click();

        Assert.Equal(0, Assert.IsType<PromptGallerySearchIntent.LoadPage>(intents[0]).PageIndex);
        Assert.Equal(2, Assert.IsType<PromptGallerySearchIntent.LoadPage>(intents[1]).PageIndex);
        Assert.Same(item, Assert.IsType<PromptGallerySearchIntent.Select>(intents[2]).Item);
        Assert.Equal(item.Id, Assert.IsType<PromptGallerySearchIntent.Edit>(intents[3]).ItemId);
    }

    [Fact]
    public void Pinned_model_filter_renders_only_with_a_context_model_and_toggles_the_filter()
    {
        var intents = new List<PromptGallerySearchIntent>();
        using var context = CreateContext();
        var cut = context.Render<PromptGallerySearchSurface>(parameters => parameters
            .Add(component => component.Presentation, Presentation([]) with
            {
                ContextProvider = "OpenAI",
                ContextModel = "gpt-5.4-mini",
                ShowActualChatModelFilter = true
            })
            .Add(component => component.Intent, EventCallback.Factory.Create<PromptGallerySearchIntent>(this, intents.Add)));

        Assert.Contains("Show actual chat model only", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='prompt-gallery-actual-model-filter']").Change(false);
        var change = Assert.IsType<PromptGallerySearchIntent.ChangeFilters>(Assert.Single(intents));
        Assert.False(change.Filters.ActualChatModelOnly);
        Assert.False(change.Debounce);

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, Presentation([]) with { ContextProvider = "OpenAI", ShowActualChatModelFilter = true }));
        Assert.Empty(cut.FindAll("[data-testid='prompt-gallery-actual-model-filter']"));
        Assert.Contains("Compatibility filter", cut.Markup, StringComparison.Ordinal);

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, Presentation([])));
        Assert.DoesNotContain("Compatibility filter", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static PromptGallerySearchPresentation Presentation(IReadOnlyList<PromptGallerySearchItem> items)
        => new(
            PromptGalleryFilterValues.Initial,
            items,
            PageIndex: 0,
            TotalPages: items.Count == 0 ? 0 : 1,
            TotalCount: items.Count,
            IsLoading: false,
            LoadError: null,
            AvailableTags: ["architecture"],
            ContextProvider: null,
            ContextModel: null,
            ShowActualChatModelFilter: false,
            FavoriteBusyItemId: null);
}
