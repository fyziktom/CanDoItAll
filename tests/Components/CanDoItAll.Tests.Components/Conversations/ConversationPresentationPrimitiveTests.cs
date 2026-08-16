using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationPresentationPrimitiveTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Presentation_key_rejects_blank_values(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ConversationPresentationKey(value!));
    }

    [Fact]
    public void Presentation_key_preserves_opaque_value_and_uses_value_equality()
    {
        const string value = "source:any/opaque key";

        var first = new ConversationPresentationKey(value);
        var second = new ConversationPresentationKey(value);

        Assert.Equal(value, first.Value);
        Assert.Equal(value, first.ToString());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Presentation_key_trims_outer_whitespace_and_rejects_oversized_values()
    {
        var key = new ConversationPresentationKey("  source:any/opaque key  ");

        Assert.Equal("source:any/opaque key", key.Value);
        Assert.Throws<ArgumentException>(() => new ConversationPresentationKey(new string('x', 257)));
    }

    [Fact]
    public void Presentation_records_copy_caller_owned_collections()
    {
        var key = new ConversationPresentationKey("item");
        var badges = new List<PresentationBadge> { new("Ready") };
        var tags = new List<string> { "review" };
        var metadata = new List<PresentationMetaItem> { new("model-x") };
        var models = new List<string> { "model-x" };
        var actions = new List<ParticipantActionPresentation>
        {
            new(new("open"), "Open", "open_in_new")
        };
        var activeActions = new List<ConversationActionPresentation>
        {
            new(new("stop"), "Stop", "stop_circle")
        };
        var avatar = new ConversationAvatarPresentation("Participant", null, "P", "participant");

        var participant = new ConversationParticipantPresentation(
            key,
            "Participant",
            badges: badges,
            tags: tags,
            metadata: metadata);
        var thread = new ConversationThreadPresentation(
            key,
            "Thread",
            DateTimeOffset.UtcNow,
            "Now",
            "Metadata",
            "Preview",
            badges: badges);
        var provider = new ConversationProviderOption(key, "Provider", true, "model-x", models);
        var header = new ConversationHeaderPresentation(avatar, badges);
        var compactItem = new ConversationParticipantCompactItemPresentation(participant, actions);
        var activeItem = new ConversationActiveItemPresentation(key, "Participant", badges, activeActions);
        var remappedProvider = provider with { SuggestedModels = models };

        badges.Clear();
        tags.Clear();
        metadata.Clear();
        models.Clear();
        actions.Clear();
        activeActions.Clear();

        Assert.Single(participant.Badges);
        Assert.Single(participant.Tags);
        Assert.Single(participant.Metadata);
        Assert.Single(thread.Badges);
        Assert.Single(provider.SuggestedModels);
        Assert.Single(header.Badges);
        Assert.Single(compactItem.Actions);
        Assert.Single(activeItem.Actions);
        Assert.Single(remappedProvider.SuggestedModels);
    }

    [Fact]
    public void Presentation_records_reject_null_collection_items()
    {
        var key = new ConversationPresentationKey("item");
        var avatar = new ConversationAvatarPresentation("Participant", null, "P", "participant");

        Assert.Throws<ArgumentException>(() => new ConversationHeaderPresentation(
            avatar,
            new PresentationBadge[] { null! }));
        Assert.Throws<ArgumentException>(() => new ConversationProviderOption(
            key,
            "Provider",
            true,
            "model-x",
            new string[] { null! }));
        Assert.Throws<ArgumentException>(() => new ConversationParticipantPresentation(
            key,
            "Participant",
            tags: new string[] { null! }));
        Assert.Throws<ArgumentException>(() => new ConversationThreadPresentation(
            key,
            "Thread",
            DateTimeOffset.UtcNow,
            "Now",
            "Metadata",
            "Preview",
            badges: new PresentationBadge[] { null! }));
        Assert.Throws<ArgumentException>(() => new ConversationActiveItemPresentation(
            key,
            "Participant",
            [],
            new ConversationActionPresentation[] { null! }));
    }

    [Fact]
    public void Meta_item_builds_labeled_and_unlabeled_display_text()
    {
        var labeled = new PresentationMetaItem("gpt-test", "Model", "Runtime model");
        var unlabeled = new PresentationMetaItem("Updated just now");

        Assert.Equal("Model: gpt-test", labeled.DisplayText);
        Assert.Equal("Runtime model", labeled.Tooltip);
        Assert.Equal("Updated just now", unlabeled.DisplayText);
    }
}
