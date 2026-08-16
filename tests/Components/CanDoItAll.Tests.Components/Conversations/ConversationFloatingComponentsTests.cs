using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.OverlayLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationFloatingComponentsTests
{
    [Fact]
    public void Floating_window_preserves_controlled_state_and_default_geometry()
    {
        using var context = CreateContext();
        var state = new OverlayWindowState
        {
            IsVisible = true,
            Width = 640,
            Height = 600
        };

        var cut = context.Render<ConversationFloatingWindow>(parameters => parameters
            .Add(component => component.WindowId, "conversation-window")
            .Add(component => component.TestId, "conversation-window-test")
            .Add(component => component.AriaLabel, "Conversation window")
            .Add(component => component.Title, "Conversations")
            .Add(component => component.State, state)
            .AddChildContent("Window content"));

        var overlay = cut.FindComponent<OverlayWindow>();
        Assert.Same(state, overlay.Instance.State);
        Assert.Equal(560, overlay.Instance.DefaultWidth);
        Assert.Equal(720, overlay.Instance.DefaultHeight);
        Assert.Equal(360, overlay.Instance.MinWidth);
        Assert.Equal(420, overlay.Instance.MinHeight);
        Assert.Equal(900, overlay.Instance.MaxWidth);
        Assert.Equal(900, overlay.Instance.MaxHeight);
    }

    [Fact]
    public void Catalog_owns_two_panel_composition_without_participant_source_types()
    {
        using var context = CreateContext();

        var cut = context.Render<ConversationFloatingCatalog>(parameters => parameters
            .Add(component => component.PrimaryText, "People")
            .Add(component => component.PrimaryIcon, "people")
            .Add(component => component.PrimaryContent, builder => builder.AddContent(0, "Primary content"))
            .Add(component => component.ActiveText, "Active chats")
            .Add(component => component.ActiveIcon, "forum")
            .Add(component => component.ActiveContent, builder => builder.AddContent(0, "Active content")));

        var tabs = cut.FindComponent<Tabs>();
        var items = cut.FindComponents<TabsItem>();
        Assert.Equal(TabsPanelOverflowMode.Hidden, tabs.Instance.PanelOverflowMode);
        Assert.True(tabs.Instance.FillHeight);
        Assert.Equal(["People", "Active chats"], items.Select(item => item.Instance.Text));
    }

    [Fact]
    public void Active_list_routes_declared_opaque_actions()
    {
        using var context = CreateContext();
        var openKey = new ConversationPresentationKey("source/chat-open");
        var hiddenKey = new ConversationPresentationKey("source/chat-hidden");
        var openActionKey = new ConversationPresentationKey("open");
        var stopActionKey = new ConversationPresentationKey("stop");
        var requests = new List<ConversationActionRequest>();
        IReadOnlyList<ConversationActiveItemPresentation> items =
        [
            new(
                openKey,
                "Open conversation",
                [new("Open", PresentationTone.Success)],
                [
                    new(openActionKey, "Open", "open_in_new", isDisabled: true),
                    new(stopActionKey, "Stop", "stop_circle", isDisabled: true, style: ConversationActionStyle.Danger)
                ]),
            new(
                hiddenKey,
                "Hidden conversation",
                [new("Kept active")],
                [
                    new(openActionKey, "Open", "open_in_new"),
                    new(stopActionKey, "Stop", "stop_circle", style: ConversationActionStyle.Danger)
                ])
        ];

        var cut = context.Render<ConversationActiveList>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.TestId, "active-list")
            .Add(component => component.ActionRequested, requests.Add));

        Assert.True(cut.Find("[data-testid='active-list-source/chat-open-open']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='active-list-source/chat-open-stop']").HasAttribute("disabled"));

        cut.Find("[data-testid='active-list-source/chat-hidden-open']").Click();
        cut.Find("[data-testid='active-list-source/chat-hidden-stop']").Click();

        Assert.Equal(
            [
                new ConversationActionRequest(hiddenKey, openActionKey),
                new ConversationActionRequest(hiddenKey, stopActionKey)
            ],
            requests);
    }

    [Fact]
    public void Lifecycle_fields_bind_values_and_apply_neutral_limits()
    {
        using var context = CreateContext();
        var retention = 10;
        var maximumActive = 12;

        var cut = context.Render<ConversationActiveChatLifecycleFields>(parameters => parameters
            .Add(component => component.HiddenChatRetentionMinutes, retention)
            .Add(component => component.HiddenChatRetentionMinutesChanged, value => retention = value)
            .Add(component => component.MaximumRetentionMinutes, 1440)
            .Add(component => component.MaximumActiveChats, maximumActive)
            .Add(component => component.MaximumActiveChatsChanged, value => maximumActive = value)
            .Add(component => component.MaximumActiveChatLimit, 50)
            .Add(component => component.RetentionInputTestId, "retention")
            .Add(component => component.MaximumActiveInputTestId, "maximum-active"));

        var retentionInput = cut.Find("[data-testid='retention']");
        var maximumInput = cut.Find("[data-testid='maximum-active']");
        Assert.Equal("1", retentionInput.GetAttribute("min"));
        Assert.Equal("1440", retentionInput.GetAttribute("max"));
        Assert.Equal("50", maximumInput.GetAttribute("max"));

        retentionInput.Change("25");
        maximumInput.Change("9");

        Assert.Equal(25, retention);
        Assert.Equal(9, maximumActive);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
