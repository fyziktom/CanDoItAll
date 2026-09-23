using Bunit;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChatSurfaceTests {
    [Theory]
    [InlineData(AgentChatScenario.Loading)]
    [InlineData(AgentChatScenario.NoAgents)]
    [InlineData(AgentChatScenario.MissingAgent)]
    [InlineData(AgentChatScenario.Threads)]
    [InlineData(AgentChatScenario.Transcript)]
    [InlineData(AgentChatScenario.Executing)]
    [InlineData(AgentChatScenario.Approvals)]
    [InlineData(AgentChatScenario.Attachments)]
    [InlineData(AgentChatScenario.Voice)]
    [InlineData(AgentChatScenario.Focused)]
    [InlineData(AgentChatScenario.Failed)]
    [InlineData(AgentChatScenario.Adversarial)]
    public void Every_scenario_renders_without_feature_services_and_keeps_product_content(AgentChatScenario scenario) {
        using var context = Context();
        var fixture = new AgentChatSandboxFixture();
        fixture.SetScenario(scenario);
        var cut = context.Render<AgentChatSurface>(p => p.Add(x => x.Presentation, fixture.Presentation).Add(x => x.Navigation, fixture.Navigation));
        Assert.Single(cut.FindAll("[data-testid='agents-chat-panel']"));
        if (scenario == AgentChatScenario.MissingAgent) {
            Assert.Contains(fixture.Navigation.ErrorMessage, cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindComponents<ChatWorkspaceSurface>());
            return;
        }
        Assert.Single(cut.FindComponents<ChatWorkspaceSurface>());
        if (scenario == AgentChatScenario.Focused) {
            Assert.Empty(cut.FindAll(".agents-chat-left-rail"));
        }
        if (scenario == AgentChatScenario.Adversarial) {
            Assert.Empty(cut.FindAll("script"));
            Assert.Contains(fixture.Presentation.Messages[^1].Content, cut.Find("[data-testid='agents-chat-panel']").TextContent, StringComparison.Ordinal);
        }
        if (scenario == AgentChatScenario.Attachments) {
            Assert.All(fixture.Presentation.Attachments, path => Assert.Contains(path, cut.Markup, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Approval_choices_are_typed_complete_and_reset_on_target_replacement() {
        using var context = Context();
        var fixture = new AgentChatSandboxFixture();
        fixture.SetScenario(AgentChatScenario.Approvals);
        var intents = new List<ChatWorkspaceIntent>();
        var cut = context.Render<ChatWorkspaceSurface>(p => p.Add(x => x.Presentation, fixture.Presentation).Add(x => x.Intent, value => intents.Add(value)));
        cut.Find("[data-testid='chat-approval-approve-fixture-read']").Click();
        Assert.Empty(intents);
        cut.Find("[data-testid='chat-approval-reject-fixture-write']").Click();
        cut.Find("[data-testid='chat-submit-approval-decisions-button']").Click();
        var intent = Assert.Single(intents);
        Assert.Equal(ChatWorkspaceAction.Approvals, intent.Action);
        Assert.Collection(intent.Decisions,
            item => Assert.Equal(new("fixture-read", true), item),
            item => Assert.Equal(new("fixture-write", false), item));
        cut.Render(p => p.Add(x => x.Presentation, fixture.Presentation with { Revision = fixture.Presentation.Revision + 1 }));
        Assert.True(cut.Find("[data-testid='chat-submit-approval-decisions-button']").HasAttribute("disabled"));
    }

    [Fact]
    public void Voice_attachment_and_execution_buttons_emit_one_typed_intent_each() {
        using var context = Context();
        var fixture = new AgentChatSandboxFixture();
        var intents = new List<ChatWorkspaceIntent>();
        var cut = context.Render<ChatWorkspaceSurface>(p => p.Add(x => x.Presentation, fixture.Presentation).Add(x => x.Intent, value => intents.Add(value)));
        foreach (var (testId, action) in new[] {
            ("chat-attachment-button", ChatWorkspaceAction.AttachArtifacts),
            ("chat-voice-mode-button", ChatWorkspaceAction.VoiceModeChanged),
            ("chat-voice-record-button", ChatWorkspaceAction.ToggleRecording),
            ("chat-voice-speak-button", ChatWorkspaceAction.SpeakLatest)
        }) {
            intents.Clear();
            cut.Find($"[data-testid='{testId}']").Click();
            Assert.Equal(action, Assert.Single(intents).Action);
        }
        fixture.SetScenario(AgentChatScenario.Executing);
        cut.Render(p => p.Add(x => x.Presentation, fixture.Presentation));
        intents.Clear();
        cut.Find("[data-testid='chat-execution-entry']").Click();
        Assert.Equal(fixture.Presentation.ExecutionSteps[0].Id, Assert.Single(intents).EntryId);
        intents.Clear();
        cut.Find("[data-testid='chat-execution-history']").Click();
        Assert.Equal(ChatWorkspaceAction.ExecutionHistory, Assert.Single(intents).Action);
    }

    [Fact]
    public void Full_and_focused_navigation_share_the_same_workspace_renderer() {
        using var context = Context();
        var fixture = new AgentChatSandboxFixture();
        var intents = new List<AgentChatNavigationIntent>();
        var cut = context.Render<AgentChatSurface>(p => p.Add(x => x.Presentation, fixture.Presentation)
            .Add(x => x.Navigation, fixture.Navigation).Add(x => x.NavigationIntent, value => intents.Add(value)));
        cut.Find("[data-testid='agent-switch-button']").Click();
        Assert.Equal(AgentChatNavigationAction.SwitchAgent, Assert.Single(intents).Action);
        Assert.Single(cut.FindComponents<ChatWorkspaceSurface>());
        cut.Render(p => p.Add(x => x.Navigation, fixture.Navigation with { Focused = true }));
        Assert.Single(cut.FindComponents<ChatWorkspaceSurface>());
        Assert.Empty(cut.FindAll(".agents-chat-left-rail"));
    }

    internal static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
