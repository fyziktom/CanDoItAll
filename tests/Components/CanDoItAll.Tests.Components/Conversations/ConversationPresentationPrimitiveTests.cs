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
    public void Meta_item_builds_labeled_and_unlabeled_display_text()
    {
        var labeled = new PresentationMetaItem("gpt-test", "Model", "Runtime model");
        var unlabeled = new PresentationMetaItem("Updated just now");

        Assert.Equal("Model: gpt-test", labeled.DisplayText);
        Assert.Equal("Runtime model", labeled.Tooltip);
        Assert.Equal("Updated just now", unlabeled.DisplayText);
    }
}
