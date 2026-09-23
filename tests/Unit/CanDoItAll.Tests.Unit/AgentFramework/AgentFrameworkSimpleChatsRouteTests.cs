using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework.Pages;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentFrameworkSimpleChatsRouteTests
{
    [Theory]
    [InlineData(AgentWorkspaceTabs.Providers)]
    [InlineData(AgentWorkspaceTabs.RequestHistory)]
    public void History_hosts_round_trip_without_inheriting_chat_or_team_routes(string tab) {
        var state = AgentWorkspaceRouteState.Parse(tab, null, Guid.NewGuid(), "conversations", null, Guid.NewGuid().ToString("D"), "agents");
        Assert.Equal(tab, state.Tab);
        Assert.Null(state.TeamId);
        Assert.Equal(ProviderUsageWorkloadSelection.Agents, state.UsageSelection);
        Assert.Equal($"/agents?tab={tab}", AgentWorkspaceRouteState.Build(state));
    }

    [Fact]
    public void DefinitionsAreTheDefaultSimpleChatWorkspaceView()
    {
        var state = AgentWorkspaceRouteState.Parse(
            AgentWorkspaceTabs.SimpleChats,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Equal(SimpleChatWorkspaceView.Definitions, state.SimpleChat.View);
        Assert.Equal("/agents?tab=simple-chats", AgentWorkspaceRouteState.Build(state));
    }

    [Fact]
    public void ConversationDeepLinksRemainExplicitAfterDefinitionsBecomeTheDefault()
    {
        var conversationId = Guid.NewGuid();
        var state = AgentWorkspaceRouteState.Parse(
            AgentWorkspaceTabs.SimpleChats,
            null,
            null,
            null,
            null,
            conversationId.ToString("D"),
            null);

        Assert.Equal(SimpleChatWorkspaceView.Conversations, state.SimpleChat.View);
        Assert.Equal(conversationId, state.SimpleChat.ConversationId);
        Assert.Equal(
            $"/agents?tab=simple-chats&simpleChatView=conversations&conversationId={conversationId:D}",
            AgentWorkspaceRouteState.Build(state));
    }

    [Fact]
    public void ChatsRouteRedirectsAndPreservesRecognizedState()
    {
        var definitionId = Guid.NewGuid();
        var route = AgentWorkspaceRouteState.BuildCompatibilityRedirect(new Uri(
            $"https://localhost/chats?simpleChatView=definitions&definitionId={definitionId:D}&ignored=secret"));

        Assert.Equal(
            $"/agents?tab=simple-chats&simpleChatView=definitions&definitionId={definitionId:D}",
            route);
        Assert.DoesNotContain("ignored", route, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidOrIncompatibleSimpleChatStateIsDiscardedDeterministically()
    {
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var state = AgentWorkspaceRouteState.Parse(
            AgentWorkspaceTabs.SimpleChats,
            null,
            null,
            "conversations",
            definitionId.ToString("D"),
            conversationId.ToString("D"),
            null);

        Assert.Equal(SimpleChatWorkspaceView.Conversations, state.SimpleChat.View);
        Assert.Null(state.SimpleChat.DefinitionId);
        Assert.Equal(conversationId, state.SimpleChat.ConversationId);
        Assert.Equal(
            $"/agents?tab=simple-chats&simpleChatView=conversations&conversationId={conversationId:D}",
            AgentWorkspaceRouteState.Build(state));
    }

    [Theory]
    [InlineData("agents", ProviderUsageWorkloadSelection.Agents)]
    [InlineData("simple-chats", ProviderUsageWorkloadSelection.SimpleChats)]
    [InlineData("both", ProviderUsageWorkloadSelection.Both)]
    [InlineData("invalid", ProviderUsageWorkloadSelection.Both)]
    public void UsageScopeHasTypedDeterministicParsing(
        string value,
        ProviderUsageWorkloadSelection expected)
    {
        Assert.Equal(expected, AgentWorkspaceRouteState.ParseUsageSelection(value));
    }
}
