using Bunit;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class FloatingChatHeaderTests {
    [Fact]
    public async Task Minimized_header_updates_without_recreating_the_chat_or_losing_its_draft() {
        await using var context = CreateContext();
        var cut = context.Render<FloatingChatSpecimen>();
        var chat = cut.FindComponent<ChatWorkspaceSurface>().Instance;
        await cut.Find("textarea").InputAsync(new() { Value = "Retained draft" });

        await cut.Find("[aria-label='Minimize window']").ClickAsync();
        await cut.FindComponents<Button>().Single(button => button.Instance.Text == "Completed").Find("button").ClickAsync();

        cut.WaitForAssertion(() => Assert.Equal("Completed", cut.Find("header [role='status']").TextContent));
        Assert.True(cut.Find(".cda-overlay-window__body").HasAttribute("hidden"));
        Assert.Same(chat, cut.FindComponent<ChatWorkspaceSurface>().Instance);

        await cut.Find("[aria-label='Expand window']").ClickAsync();

        Assert.Same(chat, cut.FindComponent<ChatWorkspaceSurface>().Instance);
        Assert.Equal("Retained draft", cut.Find("textarea").GetAttribute("value"));
        Assert.Single(cut.FindAll("[data-testid='floating-agent-chat-run-state']"));
    }

    [Fact]
    public async Task Activity_is_scoped_to_its_own_window() {
        await using var context = CreateContext();
        var first = context.Render<FloatingChatSpecimen>();
        var second = context.Render<FloatingChatSpecimen>();

        await first.Find("[aria-label='Minimize window']").ClickAsync();
        await first.FindComponents<Button>().Single(button => button.Instance.Text == "Completed").Find("button").ClickAsync();

        first.WaitForAssertion(() => Assert.Equal("Completed", first.Find("header [role='status']").TextContent));
        Assert.Equal("Waiting on tool", second.Find("header [role='status']").TextContent);
        Assert.False(second.Find(".cda-overlay-window__body").HasAttribute("hidden"));
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
