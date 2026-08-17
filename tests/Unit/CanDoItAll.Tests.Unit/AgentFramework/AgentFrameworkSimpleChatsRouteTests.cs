using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework.Pages;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentFrameworkSimpleChatsRouteTests
{
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
            $"/agents?tab=simple-chats&conversationId={conversationId:D}",
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
