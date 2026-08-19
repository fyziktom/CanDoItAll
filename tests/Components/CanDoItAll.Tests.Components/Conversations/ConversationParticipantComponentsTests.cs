using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationParticipantComponentsTests
{
    [Fact]
    public void Card_renders_source_neutral_presentation_and_routes_opaque_key_callbacks()
    {
        using var context = CreateContext();
        var key = new ConversationPresentationKey("external/source:participant-alpha");
        ConversationPresentationKey? selected = null;
        ConversationPresentationKey? favorited = null;
        var participant = new ConversationParticipantPresentation(
            key,
            "Participant Alpha",
            subtitle: "Reviewer",
            summary: "Reviews changes.",
            avatarFallbackText: "PA",
            detailsText: "Participant details",
            badges: [new("Ready", PresentationTone.Success)],
            tags: ["dotnet", "review", "architecture"],
            metadata: [new("model-x")],
            favorite: new(false, false, "Mark preferred", "Remove preferred", "participant-favorite"),
            isSelected: true,
            isBusy: true);

        var cut = context.Render<ConversationParticipantCard>(parameters => parameters
            .Add(component => component.Participant, participant)
            .Add(component => component.MaxVisibleTags, 2)
            .Add(component => component.SelectTestId, "participant-select")
            .Add(component => component.Selected, value => selected = value)
            .Add(component => component.FavoriteToggled, value => favorited = value));

        Assert.Contains("agent-selection-card--selected", cut.Find("article").ClassList);
        Assert.Equal("true", cut.Find("article").GetAttribute("aria-busy"));
        Assert.Contains("Ready", cut.Markup);
        Assert.Contains("+1", cut.Markup);
        Assert.Contains("model-x", cut.Markup);

        cut.Find("[data-testid='participant-select']").Click();
        cut.Find("[data-testid='participant-favorite']").Click();

        Assert.Equal(key, selected);
        Assert.Equal(key, favorited);
    }

    [Fact]
    public void Compact_list_routes_selection_and_actions_without_guid_identity()
    {
        using var context = CreateContext();
        var participantKey = new ConversationPresentationKey("participant/not-a-guid");
        var actionKey = new ConversationPresentationKey("open-history");
        ConversationPresentationKey? selected = null;
        ParticipantActionRequest? requested = null;
        var participant = new ConversationParticipantPresentation(
            participantKey,
            "Opaque participant",
            subtitle: "Operator",
            avatarFallbackText: "OP",
            badges: [new("Available", PresentationTone.Info)],
            metadata: [new("local-model")]);
        var item = new ConversationParticipantCompactItemPresentation(
            participant,
            [new(actionKey, "Open history", "history", "opaque-history")],
            "opaque-item",
            "opaque-select");

        var cut = context.Render<ConversationParticipantCompactList>(parameters => parameters
            .Add(component => component.Items, [item])
            .Add(component => component.Selected, value => selected = value)
            .Add(component => component.ActionRequested, value => requested = value));

        cut.Find("[data-testid='opaque-select']").Click();
        cut.Find("[data-testid='opaque-history']").Click();

        Assert.Equal(participantKey, selected);
        Assert.Equal(new ParticipantActionRequest(participantKey, actionKey), requested);
    }

    [Fact]
    public void Picker_filters_by_neutral_search_and_tags_and_orders_favorites_first()
    {
        using var context = CreateContext();
        var first = CreateParticipant("one", "Alpha Builder", "implementation", false);
        var favorite = CreateParticipant("two", "Zulu Reviewer", "quality", true);
        RenderFragment<ConversationParticipantPresentation> template = participant => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "picker-result");
            builder.AddContent(2, participant.DisplayName);
            builder.CloseElement();
        };
        var text = new ConversationParticipantPickerText(
            "Participants",
            "No participants",
            "Add participants.",
            "Search participants",
            "Search participants",
            "Filter tags",
            "participants",
            "No matches",
            "No participant matches",
            "Clear filters.",
            "Favorites first");

        var cut = context.Render<ConversationParticipantPicker>(parameters => parameters
            .Add(component => component.Participants, [first, favorite])
            .Add(component => component.Text, text)
            .Add(component => component.SearchTestId, "picker-search")
            .Add(component => component.TagFilterInputTestId, "picker-tag-input")
            .Add(component => component.ParticipantTemplate, template));

        var results = cut.FindAll("[data-testid='picker-result']");
        Assert.Equal("Zulu Reviewer", results[0].TextContent);

        cut.Find("[data-testid='picker-search']").Input("alpha");
        results = cut.FindAll("[data-testid='picker-result']");
        Assert.Single(results);
        Assert.Equal("Alpha Builder", results[0].TextContent);

        cut.Find("[data-testid='picker-search']").Input(string.Empty);
        cut.Find("[data-testid='picker-tag-input']").Input("quality,");
        results = cut.FindAll("[data-testid='picker-result']");
        Assert.Single(results);
        Assert.Equal("Zulu Reviewer", results[0].TextContent);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ConversationParticipantPresentation CreateParticipant(
        string key,
        string name,
        string tag,
        bool isFavorite)
        => new(
            new(key),
            name,
            avatarFallbackText: name[..1],
            searchText: $"{name} {tag}",
            tags: [tag],
            isFavorite: isFavorite);
}
