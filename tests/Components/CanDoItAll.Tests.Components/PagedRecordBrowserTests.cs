using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PagedRecordBrowserTests : BunitContext
{
    private static readonly Guid AlphaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BetaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public PagedRecordBrowserTests()
    {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Initial_load_uses_a_zero_based_typed_request()
    {
        PagedRecordRequest<RecordScope>? receivedRequest = null;
        CancellationToken receivedCancellationToken = default;
        var cut = RenderBrowser((request, cancellationToken) =>
        {
            receivedRequest = request;
            receivedCancellationToken = cancellationToken;
            return Task.FromResult(Page(request, Option(AlphaId, "Alpha")));
        });

        cut.WaitForAssertion(() =>
        {
            var request = Assert.IsType<PagedRecordRequest<RecordScope>>(receivedRequest);
            Assert.Equal(string.Empty, request.SearchText);
            Assert.Empty(request.Tags);
            Assert.Equal(RecordScope.All, request.Filter);
            Assert.Equal(0, request.PageIndex);
            Assert.Equal(2, request.PageSize);
            Assert.True(receivedCancellationToken.CanBeCanceled);
            Assert.Contains("1 matching record(s)", cut.Markup);
        });
    }

    [Fact]
    public void Search_is_debounced_and_normalized_before_loading()
    {
        var requests = new List<PagedRecordRequest<RecordScope>>();
        var cut = RenderBrowser((request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(Page(request, Option(AlphaId, request.SearchText)));
        });

        cut.Find("[data-testid='records-search']").Input("  alpha  ");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, requests.Count);
            Assert.Equal("alpha", requests[^1].SearchText);
            Assert.Equal(0, requests[^1].PageIndex);
        });
    }

    [Fact]
    public void Tag_and_typed_scope_changes_reset_to_the_first_page()
    {
        var requests = new List<PagedRecordRequest<RecordScope>>();
        var cut = RenderBrowser((request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(new PagedRecordPage<Guid>(
                [Option(AlphaId, "Alpha")],
                request.PageIndex,
                request.PageSize,
                5));
        });

        cut.Find("[data-testid='records-next']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, requests[^1].PageIndex));

        cut.Find("[data-testid='records-tag-filter-input']").Input(" priority,");
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, requests[^1].PageIndex);
            Assert.Equal(["priority"], requests[^1].Tags);
        });

        cut.Find("[data-testid='scope-people']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, requests[^1].PageIndex);
            Assert.Equal(RecordScope.People, requests[^1].Filter);
            Assert.Equal("true", cut.Find("[data-testid='scope-people']").GetAttribute("aria-pressed"));
        });
    }

    [Fact]
    public void Paging_and_selection_remain_strongly_typed()
    {
        Guid? selectedId = null;
        var cut = RenderBrowser(
            (request, _) =>
            {
                var option = request.PageIndex == 0
                    ? Option(AlphaId, "Alpha")
                    : Option(BetaId, "Beta");
                return Task.FromResult(new PagedRecordPage<Guid>(
                    [option],
                    request.PageIndex,
                    request.PageSize,
                    3));
            },
            selectionChanged: value => selectedId = value);

        cut.Find("[data-testid='records-next']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='record-beta']")));

        cut.Find("[data-testid='record-beta']").Click();

        Assert.Equal(BetaId, selectedId);
        Assert.Contains("Page 2 of 2", cut.Markup);
        Assert.False(cut.Find("[data-testid='records-previous']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='records-next']").HasAttribute("disabled"));
    }

    [Fact]
    public void Results_scrolling_is_typed_opt_in_and_does_not_change_the_pager_region()
    {
        var defaultCut = RenderBrowser(LoaderWith(Option(AlphaId, "Alpha")));
        var boundedCut = RenderBrowser(
            LoaderWith(Option(AlphaId, "Alpha")),
            resultsScrollMode: PagedRecordResultsScrollMode.Bounded,
            fillHeight: true);

        Assert.DoesNotContain(
            "paged-record-browser__results--bounded",
            defaultCut.Find("[data-testid='records-results']").ClassList);
        Assert.Contains(
            "paged-record-browser__results--bounded",
            boundedCut.Find("[data-testid='records-results']").ClassList);
        Assert.DoesNotContain(
            "paged-record-browser__results--bounded",
            boundedCut.Find("[data-testid='records-pager']").ClassList);
        var boundedRoot = boundedCut.Find("[data-testid='records']");
        Assert.Contains("w-full", boundedRoot.ClassList);
        Assert.Contains("paged-record-browser--fill-height", boundedRoot.ClassList);
        Assert.Contains("min-h-0", boundedRoot.ClassList);
        Assert.Contains("flex-1", boundedRoot.ClassList);
        Assert.Contains("overflow-hidden", boundedRoot.ClassList);
        var boundedRootStyle = boundedRoot.GetAttribute("style")!.Replace(" ", string.Empty);
        Assert.Contains(";height:100%", boundedRootStyle);
        Assert.Contains(";min-height:0", boundedRootStyle);
        var boundedPager = boundedCut.Find("[data-testid='records-pager']");
        Assert.Contains("paged-record-browser__pager", boundedPager.ClassList);
        Assert.Contains("shrink-0", boundedPager.ClassList);
    }

    [Fact]
    public void Bounded_catalog_grid_keeps_sparse_cards_at_a_suitable_width()
    {
        var defaultCut = RenderBrowser(LoaderWith(Option(AlphaId, "Alpha")));
        var boundedCut = RenderBrowser(
            LoaderWith(Option(AlphaId, "Alpha")),
            gridMode: PagedRecordGridMode.BoundedCatalog);

        var defaultGridStyle = defaultCut
            .Find("[data-testid='records-results']")
            .FirstElementChild!
            .GetAttribute("style");
        var boundedGridStyle = boundedCut
            .Find("[data-testid='records-results']")
            .FirstElementChild!
            .GetAttribute("style");

        Assert.Contains("repeat(auto-fit,minmax(16rem,1fr))", defaultGridStyle);
        Assert.Contains("repeat(auto-fill,minmax(min(100%,17rem),24rem))", boundedGridStyle);
    }

    [Fact]
    public void Filter_toolbar_keeps_fields_flexible_and_actions_intrinsic()
    {
        var cut = RenderBrowser(LoaderWith(Option(AlphaId, "Alpha")));
        var filters = cut.Find("[data-testid='records-filters']");

        Assert.Equal("DIV", filters.TagName);
        Assert.Contains("paged-record-browser__filters", filters.ClassList);
        Assert.Collection(
            filters.Children,
            search =>
            {
                Assert.Equal("records-search", search.GetAttribute("data-testid"));
                Assert.Contains("paged-record-browser__filter-field", search.ClassList);
            },
            tags =>
            {
                Assert.Equal("records-tag-filter", tags.GetAttribute("data-testid"));
                Assert.Contains("paged-record-browser__filter-field", tags.ClassList);
            },
            scope =>
            {
                Assert.Equal("records-scope-filter", scope.GetAttribute("data-testid"));
                Assert.Contains("paged-record-browser__scope", scope.ClassList);
            },
            reset =>
            {
                Assert.Equal("records-reset", reset.GetAttribute("data-testid"));
                Assert.Contains("paged-record-browser__reset", reset.ClassList);
            });
    }

    [Fact]
    public void Typed_item_template_keeps_selection_owned_by_the_browser()
    {
        Guid? selectedId = null;
        RenderFragment<PagedRecordItemTemplateContext<Guid>> template = context => builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddAttribute(2, "data-testid", "templated-record");
            builder.AddAttribute(3, "onclick", context.SelectAsync);
            builder.AddContent(4, $"{context.Option.Title}:{context.IsSelected}");
            builder.CloseElement();
        };
        var cut = Render<PagedRecordBrowser<Guid, RecordScope>>(parameters => parameters
            .Add(component => component.Loader, LoaderWith(Option(AlphaId, "Alpha")))
            .Add(component => component.InitialFilter, RecordScope.All)
            .Add(component => component.ShowTagFilter, false)
            .Add(component => component.PageSize, 2)
            .Add(component => component.ColumnTemplate, "repeat(auto-fit,minmax(17rem,1fr))")
            .Add(component => component.DataTestId, "records")
            .Add(component => component.SelectionChanged, value => selectedId = value)
            .Add(component => component.ItemTemplate, template));

        cut.WaitForAssertion(() => Assert.Contains("Alpha:False", cut.Markup));
        cut.Find("[data-testid='templated-record']").Click();

        Assert.Equal(AlphaId, selectedId);
        Assert.Empty(cut.FindAll("[data-testid='record-alpha-shell']"));
    }

    [Fact]
    public void Default_card_uses_typed_tones_stable_anatomy_and_service_tooltips()
    {
        const string longTag = "a-very-long-governance-classification";
        var option = new PagedRecordOption<Guid>(
            AlphaId,
            "A deliberately long record title that needs a stable two-line slot",
            "AI agent")
        {
            Subtitle = "Long business identity that remains aligned",
            SubtitleTooltip = "Primary organization. Other affiliations: Contoso / Advisor.",
            Description = "A long operational summary that is clamped visually while its complete value remains discoverable.",
            Meta = "Updated 27.07.2026 09:45",
            Icon = "smart_toy",
            Tags = [longTag, "review", "governed"],
            KindTone = PagedRecordBadgeTone.Accent,
            TagTone = PagedRecordBadgeTone.Teal,
            CornerStatus = new PagedRecordCornerStatus("Inactive", PagedRecordStatusTone.Danger),
            TestId = "record-alpha"
        };
        var cut = RenderBrowser(LoaderWith(option));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='record-alpha']")));

        var shell = cut.Find("[data-testid='record-alpha-shell']");
        Assert.Contains("paged-record-browser__card--tone-accent", shell.ClassList);
        Assert.Contains(
            "paged-record-browser__status--danger",
            shell.QuerySelector(".paged-record-browser__status")!.ClassList);
        Assert.Equal("Inactive", shell.QuerySelector(".paged-record-browser__status span")!.TextContent.Trim());
        Assert.NotNull(shell.QuerySelector(".paged-record-browser__media"));
        Assert.NotNull(shell.QuerySelector(".paged-record-browser__title-slot"));
        Assert.NotNull(shell.QuerySelector(".paged-record-browser__description-slot"));
        Assert.NotNull(shell.QuerySelector(".paged-record-browser__footer"));

        shell.QuerySelector(".paged-record-browser__subtitle-slot .paged-record-browser__tooltip-target")!
            .TriggerEvent(
                "onmouseenter",
                new MouseEventArgs
                {
                    ClientX = 120,
                    ClientY = 80
                });
        var subtitleTooltip = Services.GetRequiredService<TooltipService>().Current;
        Assert.Equal(
            "Primary organization. Other affiliations: Contoso / Advisor.",
            subtitleTooltip?.Text);

        var tagTargets = shell.QuerySelectorAll(".paged-record-browser__tag-target");
        Assert.Equal(3, tagTargets.Length);
        Assert.Contains("+1", shell.QuerySelector(".paged-record-browser__card-tags")!.TextContent);

        tagTargets[0].TriggerEvent(
            "onmouseenter",
            new MouseEventArgs
            {
                ClientX = 120,
                ClientY = 80
            });

        var tooltip = Services.GetRequiredService<TooltipService>().Current;
        Assert.Equal(longTag, tooltip?.Text);
        Assert.Equal(TooltipPosition.Bottom, tooltip?.Options.Position);
    }

    [Fact]
    public async Task Late_cancelled_search_cannot_replace_a_newer_result()
    {
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderBrowser(async (request, _) =>
        {
            if (request.SearchText == "slow")
            {
                slowStarted.TrySetResult();
                await slowCompletion.Task;
                return Page(request, Option(AlphaId, "Stale result"));
            }

            return request.SearchText == "fast"
                ? Page(request, Option(BetaId, "Current result"))
                : Page(request, Option(AlphaId, "Initial result"));
        });

        var staleEvent = cut.InvokeAsync(
            () => cut.Find("[data-testid='records-search']").Input("slow"));
        await slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var currentEvent = cut.InvokeAsync(
            () => cut.Find("[data-testid='records-search']").Input("fast"));
        await currentEvent;
        cut.WaitForAssertion(() => Assert.Contains("Current result", cut.Markup));

        slowCompletion.TrySetResult();
        await staleEvent;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Current result", cut.Markup);
            Assert.DoesNotContain("Stale result", cut.Markup);
        });
    }

    [Fact]
    public void Failure_is_explicit_and_retry_reissues_the_same_page()
    {
        var attempts = 0;
        Exception? reportedException = null;
        var cut = RenderBrowser(
            (request, _) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("Data source unavailable.");
                }

                return Task.FromResult(Page(request, Option(AlphaId, "Recovered")));
            },
            loadFailed: exception => reportedException = exception);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='records-error']"));
            Assert.IsType<InvalidOperationException>(reportedException);
            Assert.DoesNotContain("Data source unavailable.", cut.Markup);
        });

        cut.Find("[data-testid='records-retry']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, attempts);
            Assert.Contains("Recovered", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='records-error']"));
        });
    }

    [Fact]
    public async Task Loading_and_empty_states_are_explicit()
    {
        var completion = new TaskCompletionSource<PagedRecordPage<Guid>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderBrowser((_, _) => completion.Task);

        Assert.NotNull(cut.Find("[data-testid='records-loading']"));
        Assert.True(cut.Find("[data-testid='records-next']").HasAttribute("disabled"));

        completion.TrySetResult(new PagedRecordPage<Guid>([], 0, 2, 0));
        await cut.InvokeAsync(() => Task.CompletedTask);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='records-empty']"));
            Assert.Contains("No pages", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='records-loading']"));
        });
    }

    [Fact]
    public void Controlled_dialog_confirms_typed_selection_from_a_stable_footer()
    {
        Guid? confirmedId = null;
        var closeCount = 0;
        var cut = Render<PagedRecordPickerDialog<Guid, RecordScope>>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Loader, LoaderWith(Option(AlphaId, "Alpha")))
            .Add(component => component.InitialFilter, RecordScope.All)
            .Add(component => component.FilterOptions, ScopeOptions())
            .Add(component => component.ShowTagFilter, false)
            .Add(component => component.PageSize, 2)
            .Add(component => component.DataTestId, "record-dialog")
            .Add(component => component.SelectionConfirmed, value => confirmedId = value)
            .Add(component => component.OnClose, () => closeCount++));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='record-alpha']")));
        Assert.True(cut.Find("[data-testid='record-dialog-confirm']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='record-dialog-browser-tag-filter']"));

        cut.Find("[data-testid='record-alpha']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find("[data-testid='record-alpha']").GetAttribute("aria-pressed"));
            Assert.Contains("1 record selected", cut.Markup);
            Assert.False(cut.Find("[data-testid='record-dialog-confirm']").HasAttribute("disabled"));
        });
        Assert.Null(confirmedId);

        cut.Find("[data-testid='record-dialog-confirm']").Click();
        Assert.Equal(AlphaId, confirmedId);

        cut.Find("[data-testid='record-dialog-cancel']").Click();
        Assert.Equal(1, closeCount);
    }

    private IRenderedComponent<PagedRecordBrowser<Guid, RecordScope>> RenderBrowser(
        PagedRecordLoader<Guid, RecordScope> loader,
        Action<Guid>? selectionChanged = null,
        Action<Exception>? loadFailed = null,
        PagedRecordResultsScrollMode resultsScrollMode = PagedRecordResultsScrollMode.Page,
        PagedRecordGridMode gridMode = PagedRecordGridMode.Fluid,
        bool fillHeight = false)
    {
        return Render<PagedRecordBrowser<Guid, RecordScope>>(parameters =>
        {
            parameters
                .Add(component => component.Loader, loader)
                .Add(component => component.InitialFilter, RecordScope.All)
                .Add(component => component.FilterOptions, ScopeOptions())
                .Add(component => component.TagSuggestions, ["priority", "partner"])
                .Add(component => component.PageSize, 2)
                .Add(component => component.ResultsScrollMode, resultsScrollMode)
                .Add(component => component.GridMode, gridMode)
                .Add(component => component.FillHeight, fillHeight)
                .Add(component => component.DataTestId, "records");

            if (selectionChanged is not null)
            {
                parameters.Add(component => component.SelectionChanged, selectionChanged);
            }

            if (loadFailed is not null)
            {
                parameters.Add(component => component.LoadFailed, loadFailed);
            }
        });
    }

    private static PagedRecordLoader<Guid, RecordScope> LoaderWith(
        params PagedRecordOption<Guid>[] options)
    {
        return (request, _) => Task.FromResult(new PagedRecordPage<Guid>(
            options,
            request.PageIndex,
            request.PageSize,
            options.Length));
    }

    private static PagedRecordPage<Guid> Page(
        PagedRecordRequest<RecordScope> request,
        params PagedRecordOption<Guid>[] options)
    {
        return new PagedRecordPage<Guid>(
            options,
            request.PageIndex,
            request.PageSize,
            options.Length);
    }

    private static PagedRecordOption<Guid> Option(Guid id, string title)
    {
        return new PagedRecordOption<Guid>(id, title, "Person")
        {
            TestId = id == AlphaId ? "record-alpha" : "record-beta",
            Tags = ["priority"]
        };
    }

    private static IReadOnlyList<PagedRecordFilterOption<RecordScope>> ScopeOptions()
    {
        return
        [
            new(RecordScope.All, "All", "scope-all"),
            new(RecordScope.People, "People", "scope-people"),
            new(RecordScope.Organizations, "Organizations", "scope-organizations")
        ];
    }

    private enum RecordScope
    {
        All,
        People,
        Organizations
    }
}
