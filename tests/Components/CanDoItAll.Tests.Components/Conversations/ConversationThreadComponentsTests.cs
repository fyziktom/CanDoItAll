using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationThreadComponentsTests
{
    [Fact]
    public void Rail_orders_filters_and_routes_opaque_thread_keys()
    {
        using var context = CreateContext();
        var older = CreateThread("external/thread:older", "Architecture notes", 1);
        var newer = CreateThread("external/thread:newer", "Runtime review", 2);
        ConversationPresentationKey? selected = null;

        var cut = context.Render<ConversationThreadRail>(parameters => parameters
            .Add(component => component.Threads, [older, newer])
            .Add(component => component.Selected, key => selected = key));

        var rows = cut.FindAll("[data-testid='agent-thread-card']");
        Assert.Contains("Runtime review", rows[0].TextContent);

        cut.Find("[data-testid='agent-thread-search']").Input("architecture");
        rows = cut.FindAll("[data-testid='agent-thread-card']");
        Assert.Single(rows);
        rows[0].Click();

        Assert.Equal(older.Key, selected);
    }

    [Fact]
    public void History_list_caps_items_and_preserves_selected_state()
    {
        using var context = CreateContext();
        var threads = Enumerable.Range(0, 30)
            .Select(index => CreateThread(
                $"opaque:{index}",
                $"Thread {index:D2}",
                index,
                isSelected: index == 29))
            .ToArray();

        var cut = context.Render<ConversationThreadHistoryList>(parameters => parameters
            .Add(component => component.Threads, threads)
            .Add(component => component.MaxItems, 25));

        var rows = cut.FindAll("[data-testid='agent-thread-history-row']");
        Assert.Equal(25, rows.Count);
        Assert.Contains("Thread 29", rows[0].TextContent);
        Assert.Equal("true", rows[0].GetAttribute("aria-pressed"));
        Assert.DoesNotContain("Thread 04", cut.Markup);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ConversationThreadPresentation CreateThread(
        string key,
        string title,
        int minute,
        bool isSelected = false)
        => new(
            new(key),
            title,
            new DateTimeOffset(2026, 8, 16, 10, minute, 0, TimeSpan.Zero),
            $"10:{minute:D2}",
            "1 message",
            $"Preview for {title}",
            searchText: $"{title} searchable",
            badges: [new("1 message")],
            isSelected: isSelected);
}
