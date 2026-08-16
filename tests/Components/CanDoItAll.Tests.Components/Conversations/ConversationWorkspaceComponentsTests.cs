using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationWorkspaceComponentsTests
{
    [Fact]
    public void Markdown_renderer_disables_raw_html_and_rerenders_changed_content()
    {
        using var context = CreateContext();
        var cut = context.Render<ConversationMarkdownRenderer>(parameters => parameters
            .Add(component => component.Markdown, "<script>alert('unsafe')</script>\n\n**First**"));

        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("First", cut.Markup);

        cut.Render(parameters => parameters
            .Add(component => component.Markdown, "## Updated"));

        Assert.DoesNotContain("First", cut.Markup);
        Assert.Contains("Updated", cut.Markup);
    }

    [Fact]
    public void Markdown_renderer_preserves_allowlisted_link_and_image_uris()
    {
        const string markdown = """
            [relative](docs/help) [root](/docs) [fragment](#details)
            [http](http://example.test) [https](https://example.test) [mail](mailto:user@example.test)
            ![relative image](images/preview.png) ![https image](https://example.test/preview.png)
            """;

        var html = ConversationMarkdownRenderer.RenderHtml(markdown);

        Assert.Contains("href=\"docs/help\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/docs\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#details\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"http://example.test\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.test\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"mailto:user@example.test\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"images/preview.png\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"https://example.test/preview.png\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("java&#x73;cript:alert(1)")]
    [InlineData("%6A%61vascript:alert(1)")]
    [InlineData("java%0D%0Ascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    public void Markdown_renderer_rewrites_hostile_link_and_image_uris_to_inert_targets(string uri)
    {
        var html = ConversationMarkdownRenderer.RenderHtml($"[link]({uri}) ![image]({uri})");

        Assert.Contains("href=\"about:blank\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"about:blank\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_bubble_preserves_opaque_key_context_copy_and_metadata()
    {
        using var context = CreateContext();
        var message = new ConversationMessagePresentation(
            new("external/message:42"),
            ConversationMessageRole.User,
            "You",
            PresentationTone.Accent,
            "Visible request",
            "10:15",
            hiddenContext: "Private execution context",
            copyValue: "Visible request",
            copyAriaLabel: "Copy request",
            tokenEstimate: 3);

        var cut = context.Render<ConversationMessageBubble>(parameters => parameters
            .Add(component => component.Message, message));

        Assert.Contains("Visible request", cut.Markup);
        Assert.Contains("Private execution context", cut.Find("[data-testid='chat-message-hidden-context']").TextContent);
        Assert.Contains("~3 tokens", cut.Markup);
        Assert.Equal("Copy request", cut.Find("button[aria-label='Copy request']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Transcript_renders_empty_state_or_messages_and_extension_slots()
    {
        using var context = CreateContext();
        var empty = context.Render<ConversationTranscript>(parameters => parameters
            .Add(component => component.EmptyState, new ConversationEmptyStatePresentation(
                "Start here",
                "No messages",
                "Send the first request.")));

        Assert.Contains("No messages", empty.Markup);
        Assert.DoesNotContain("after-messages", empty.Markup);

        var message = new ConversationMessagePresentation(
            new("opaque:assistant"),
            ConversationMessageRole.Assistant,
            "Assistant",
            PresentationTone.Success,
            "Response",
            "10:20");
        var populated = context.Render<ConversationTranscript>(parameters => parameters
            .Add(component => component.Messages, [message])
            .Add(component => component.AfterMessages, builder => builder.AddMarkupContent(0, "<div id='after-messages'>Execution</div>"))
            .Add(component => component.AfterPendingMessage, builder => builder.AddMarkupContent(0, "<div id='after-pending'>Approval</div>")));

        Assert.Contains("Response", populated.Markup);
        Assert.Contains("Execution", populated.Markup);
        Assert.Contains("Approval", populated.Markup);
    }

    [Fact]
    public void Transcript_renders_bounded_transient_states_using_message_roles()
    {
        using var context = CreateContext();
        var assistantAvatar = new ConversationAvatarPresentation(
            "Assistant avatar",
            null,
            "AI",
            "Assistant");
        var transientMessages = new[]
        {
            CreateTransientMessage("pending-user", ConversationMessageRole.User, ConversationMessageState.Pending),
            CreateTransientMessage("streaming-assistant", ConversationMessageRole.Assistant, ConversationMessageState.Streaming, assistantAvatar),
            CreateTransientMessage("failed-assistant", ConversationMessageRole.Assistant, ConversationMessageState.Failed, assistantAvatar),
            CreateTransientMessage("cancelled-user", ConversationMessageRole.User, ConversationMessageState.Cancelled)
        };

        var cut = context.Render<ConversationTranscript>(parameters => parameters
            .Add(component => component.TransientMessages, transientMessages));

        var renderedMessages = cut.FindAll("[data-testid='conversation-message']");
        Assert.Equal(ConversationTranscript.MaximumTransientMessageCount, renderedMessages.Count);
        Assert.Contains("justify-end", renderedMessages[0].ClassList);
        Assert.Equal("true", renderedMessages[0].GetAttribute("aria-busy"));
        Assert.Contains("Sending...", renderedMessages[0].TextContent);
        Assert.Contains("justify-start", renderedMessages[1].ClassList);
        Assert.Equal("true", renderedMessages[1].GetAttribute("aria-busy"));
        Assert.Contains("Responding...", renderedMessages[1].TextContent);
        Assert.Contains("Assistant avatar", renderedMessages[1].InnerHtml);
        Assert.Equal("alert", renderedMessages[2].GetAttribute("role"));
        Assert.Contains("Failed", renderedMessages[2].TextContent);
        Assert.Contains("Cancelled", renderedMessages[3].TextContent);
        Assert.Equal(ConversationTranscript.MaximumTransientMessageCount, cut.FindAll(".chat-message-footer").Count);
        Assert.All(transientMessages, message => Assert.Contains(message.CopyAriaLabel!, cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void Transcript_rejects_transient_messages_above_the_public_bound()
    {
        using var context = CreateContext();
        var messages = Enumerable.Range(0, ConversationTranscript.MaximumTransientMessageCount + 1)
            .Select(index => CreateTransientMessage(
                $"transient-{index}",
                ConversationMessageRole.Assistant,
                ConversationMessageState.Streaming))
            .ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => context.Render<ConversationTranscript>(parameters => parameters
            .Add(component => component.TransientMessages, messages)));
    }

    [Fact]
    public void Transcript_rejects_multiple_uncoalesced_streaming_assistant_messages()
    {
        using var context = CreateContext();
        var messages = new[]
        {
            CreateTransientMessage(
                "streaming-assistant-1",
                ConversationMessageRole.Assistant,
                ConversationMessageState.Streaming),
            CreateTransientMessage(
                "streaming-assistant-2",
                ConversationMessageRole.Assistant,
                ConversationMessageState.Streaming)
        };

        Assert.Throws<ArgumentException>(() => context.Render<ConversationTranscript>(parameters => parameters
            .Add(component => component.TransientMessages, messages)));
    }

    [Fact]
    public void Transcript_preserves_the_single_pending_message_compatibility_facade()
    {
        using var context = CreateContext();
        var pendingMessage = CreateTransientMessage(
            "legacy-pending",
            ConversationMessageRole.User,
            ConversationMessageState.Pending);

        var cut = context.Render<ConversationTranscript>(parameters => parameters
            .Add(component => component.PendingMessage, pendingMessage));

        Assert.Single(cut.FindAll("[data-testid='conversation-message']"));
        Assert.Contains("Sending...", cut.Markup);
    }

    [Fact]
    public void Composer_routes_draft_and_send_callbacks_and_honors_disabled_state()
    {
        using var context = CreateContext();
        var draft = string.Empty;
        var sendCount = 0;
        var cut = context.Render<ConversationComposer>(parameters => parameters
            .Add(component => component.DraftPrompt, string.Empty)
            .Add(component => component.DraftPromptChanged, value => draft = value)
            .Add(component => component.SendRequested, () => sendCount++));

        cut.Find("[data-testid='chat-prompt-input']").Input("Inspect this change");
        cut.Find("[data-testid='chat-send-button']").Click();

        Assert.Equal("Inspect this change", draft);
        Assert.Equal(1, sendCount);

        cut.Render(parameters => parameters
            .Add(component => component.DraftPrompt, draft)
            .Add(component => component.DraftPromptChanged, value => draft = value)
            .Add(component => component.SendRequested, () => sendCount++)
            .Add(component => component.IsSendDisabled, true));

        Assert.NotNull(cut.Find("[data-testid='chat-send-button']").GetAttribute("disabled"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ConversationMessagePresentation CreateTransientMessage(
        string key,
        ConversationMessageRole role,
        ConversationMessageState state,
        ConversationAvatarPresentation? avatar = null)
        => new(
            new(key),
            role,
            role == ConversationMessageRole.User ? "User" : "Assistant",
            role == ConversationMessageRole.User ? PresentationTone.Accent : PresentationTone.Success,
            $"Content for {key}",
            "10:30",
            copyValue: $"Content for {key}",
            copyAriaLabel: $"Copy {key}",
            avatar: avatar,
            state: state);
}
