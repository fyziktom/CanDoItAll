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
}
