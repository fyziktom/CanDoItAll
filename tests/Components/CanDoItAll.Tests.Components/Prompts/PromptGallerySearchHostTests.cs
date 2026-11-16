using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Gallery;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptGallerySearchHostTests
{
    [Fact]
    public void Actual_chat_model_filter_uses_the_pinned_provider_model_and_can_show_all_prompts()
    {
        const string provider = "OpenAi";
        const string model = "gpt-5.4-mini";
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);

        var cut = context.Render<PromptGallerySearchHost>(parameters => parameters
            .Add(component => component.Consumer, PromptGalleryConsumer.Chat)
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, model)
            .Add(component => component.ShowActualChatModelFilter, true)
            .Add(component => component.Compact, true));

        cut.WaitForAssertion(() =>
        {
            var query = Assert.Single(gallery.Queries);
            Assert.Equal(provider, query.Provider);
            Assert.Equal(model, query.Model);
            Assert.Contains("Show actual chat model only", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='prompt-gallery-clear-filters']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, gallery.Queries.Count);
            Assert.Null(gallery.Queries[^1].Provider);
            Assert.Null(gallery.Queries[^1].Model);
        });

        cut.Find("[data-testid='prompt-gallery-actual-model-filter']").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, gallery.Queries.Count);
            Assert.Equal(provider, gallery.Queries[^1].Provider);
            Assert.Equal(model, gallery.Queries[^1].Model);
        });

        cut.Find("[data-testid='prompt-gallery-actual-model-filter']").Change(false);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(4, gallery.Queries.Count);
            Assert.Null(gallery.Queries[^1].Provider);
            Assert.Null(gallery.Queries[^1].Model);
        });
    }

    [Fact]
    public void Favorite_toggle_writes_and_reloads_the_current_page()
    {
        var gallery = new ScriptedPromptGalleryService();
        var itemId = Guid.NewGuid();
        var favorite = false;
        gallery.Search = (query, _) => Task.FromResult(ScriptedPromptGalleryService.Page(query, [ScriptedPromptGalleryService.Item(itemId, favorite: favorite)]));
        gallery.SetFavorite = (_, value) =>
        {
            favorite = value;
            return Task.FromResult(SharedKernel.Result.Success());
        };
        using var context = CreateContext(gallery);

        var cut = context.Render<PromptGallerySearchHost>(parameters => parameters
            .Add(component => component.Compact, false)
            .Add(component => component.ShowSelectAction, false));

        cut.WaitForAssertion(() =>
            Assert.Equal("false", cut.Find("[data-testid='prompt-gallery-favorite']").GetAttribute("aria-pressed")));

        cut.Find("[data-testid='prompt-gallery-favorite']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal((itemId, true), Assert.Single(gallery.FavoriteWrites));
            Assert.Equal(2, gallery.Queries.Count);
            Assert.Equal("true", cut.Find("[data-testid='prompt-gallery-favorite']").GetAttribute("aria-pressed"));
        });
    }

    [Fact]
    public async Task Old_search_failure_after_a_newer_request_does_not_show_an_error()
    {
        var gallery = new ScriptedPromptGalleryService();
        var stale = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentStarted = new TaskCompletionSource<PromptGalleryQuery>(TaskCreationOptions.RunContinuationsAsynchronously);
        gallery.Search = (query, _) => {
            if (query.Kind is not PromptGalleryItemKind.Part) {
                return stale.Task;
            }
            currentStarted.SetResult(query);
            return current.Task;
        };
        using var context = CreateContext(gallery);
        var notifications = context.Services.GetRequiredService<NotificationService>();
        var cut = context.Render<PromptGallerySearchHost>(parameters => parameters.Add(component => component.Compact, false));
        cut.WaitForAssertion(() => Assert.Single(gallery.Queries));

        cut.Find("[data-testid='prompt-gallery-kind-filter']").Change(nameof(PromptGalleryItemKind.Part));
        Assert.Single(gallery.Queries);
        Assert.True(gallery.SearchTokens[0].IsCancellationRequested);
        await cut.InvokeAsync(() => stale.SetException(new IOException("Late failure.")));
        var currentQuery = await currentStarted.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        Assert.Equal(2, gallery.Queries.Count);

        Assert.Empty(notifications.Messages);
        Assert.Contains("Searching reusable prompts", cut.Markup, StringComparison.Ordinal);

        await cut.InvokeAsync(() => current.SetResult(ScriptedPromptGalleryService.Page(currentQuery, [ScriptedPromptGalleryService.Item()])));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='prompt-gallery-grid']")));
        Assert.Empty(notifications.Messages);
        Assert.Equal(PromptGalleryItemKind.Part, gallery.Queries[1].Kind);
    }

    [Fact]
    public async Task Context_change_while_loading_issues_a_new_request_and_ignores_the_stale_page()
    {
        var gallery = new ScriptedPromptGalleryService();
        var stale = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = new TaskCompletionSource<PromptGalleryPage<PromptGallerySearchItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentStarted = new TaskCompletionSource<PromptGalleryQuery>(TaskCreationOptions.RunContinuationsAsynchronously);
        gallery.Search = (query, _) => {
            if (query.Model is not "gpt-5.4") {
                return stale.Task;
            }
            currentStarted.SetResult(query);
            return current.Task;
        };
        using var context = CreateContext(gallery);
        var cut = context.Render<PromptGallerySearchHost>(parameters => parameters
            .Add(component => component.Consumer, PromptGalleryConsumer.Chat)
            .Add(component => component.Provider, "OpenAI")
            .Add(component => component.Model, "gpt-5.4-mini")
            .Add(component => component.Compact, true));
        cut.WaitForAssertion(() => Assert.Single(gallery.Queries));

        cut.Render(parameters => parameters
            .Add(component => component.Consumer, PromptGalleryConsumer.Chat)
            .Add(component => component.Provider, "OpenAI")
            .Add(component => component.Model, "gpt-5.4")
            .Add(component => component.Compact, true));

        Assert.Single(gallery.Queries);
        Assert.True(gallery.SearchTokens[0].IsCancellationRequested);
        await cut.InvokeAsync(() => stale.SetResult(ScriptedPromptGalleryService.Page(gallery.Queries[0], [ScriptedPromptGalleryService.Item(title: "Stale")])));
        var currentQuery = await currentStarted.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        Assert.Equal(2, gallery.Queries.Count);
        Assert.Equal("gpt-5.4", currentQuery.Model);
        Assert.DoesNotContain("Stale", cut.Markup, StringComparison.Ordinal);

        await cut.InvokeAsync(() => current.SetResult(ScriptedPromptGalleryService.Page(currentQuery, [ScriptedPromptGalleryService.Item(title: "Current")])));
        cut.WaitForAssertion(() => Assert.Contains("Current", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(2, gallery.Queries.Count);
    }

    [Fact]
    public async Task Refresh_reloads_in_place_and_preserves_filters()
    {
        var gallery = new ScriptedPromptGalleryService();
        using var context = CreateContext(gallery);
        var cut = context.Render<PromptGallerySearchHost>(parameters => parameters.Add(component => component.Compact, false));
        cut.WaitForAssertion(() => Assert.Single(gallery.Queries));

        cut.Find("[data-testid='prompt-gallery-status-filter']").Change("Final");
        cut.WaitForAssertion(() => Assert.Equal(2, gallery.Queries.Count));

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        Assert.Equal(3, gallery.Queries.Count);
        Assert.Equal(PromptArtifactStatus.Final, gallery.Queries[^1].Status);
        Assert.Equal(PromptArtifactStatus.Final, cut.Instance.Presentation.Filters.Status);
    }

    private static BunitContext CreateContext(ScriptedPromptGalleryService gallery)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IPromptGalleryService>(gallery);
        CanDoItAll.Tests.Support.PromptGalleryUiTestAccess.Register(context.Services);
        return context;
    }
}
