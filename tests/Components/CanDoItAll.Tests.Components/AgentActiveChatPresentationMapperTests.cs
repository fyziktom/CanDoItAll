using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentActiveChatPresentationMapperTests
{
    [Theory]
    [InlineData(ActiveAgentChatVisibility.Visible, ActiveAgentChatRunState.Idle, "Open", PresentationTone.Success, "Ready", PresentationTone.Success)]
    [InlineData(ActiveAgentChatVisibility.Hidden, ActiveAgentChatRunState.Running, "Kept active", PresentationTone.Default, "Running", PresentationTone.Info)]
    [InlineData(ActiveAgentChatVisibility.Hidden, ActiveAgentChatRunState.AwaitingApproval, "Kept active", PresentationTone.Default, "Awaiting approval", PresentationTone.Warning)]
    public void Maps_agent_chat_state_and_round_trips_handle_identity(
        ActiveAgentChatVisibility visibility,
        ActiveAgentChatRunState runState,
        string visibilityText,
        PresentationTone visibilityTone,
        string runStateText,
        PresentationTone runStateTone)
    {
        var handleId = AgentChatHandleId.Create();
        var now = DateTimeOffset.UtcNow;
        var chat = new ActiveAgentChat(
            handleId,
            new AgentChatIdentity(Guid.NewGuid(), "Agent Alpha", "Reviewer", string.Empty),
            Guid.NewGuid(),
            visibility,
            runState,
            now,
            now,
            visibility == ActiveAgentChatVisibility.Hidden ? now : null);

        var result = AgentActiveChatPresentationMapper.Map(chat);

        Assert.Equal("Agent Alpha", result.DisplayName);
        Assert.Equal(new PresentationBadge(visibilityText, visibilityTone), result.Badges[0]);
        Assert.Equal(new PresentationBadge(runStateText, runStateTone), result.Badges[1]);
        Assert.Equal("Open", result.Actions[0].Label);
        Assert.Equal(chat.IsVisible, result.Actions[0].IsDisabled);
        Assert.Equal("Stop", result.Actions[1].Label);
        Assert.Equal(!chat.CanStop, result.Actions[1].IsDisabled);
        Assert.Equal(ConversationActionStyle.Danger, result.Actions[1].Style);
        Assert.Equal(
            AgentActiveChatPresentationAction.Open,
            AgentActiveChatPresentationMapper.ResolveAction(result.Actions[0].Key));
        Assert.Equal(
            AgentActiveChatPresentationAction.Stop,
            AgentActiveChatPresentationMapper.ResolveAction(result.Actions[1].Key));
        Assert.Equal(handleId, AgentActiveChatPresentationMapper.ResolveHandleId(result.Key));
    }

    [Fact]
    public void Rejects_non_agent_presentation_keys()
    {
        var key = new ConversationPresentationKey("external/conversation");

        var exception = Assert.Throws<ArgumentException>(
            () => AgentActiveChatPresentationMapper.ResolveHandleId(key));

        Assert.Contains("not an Agent chat handle", exception.Message);
    }

    [Fact]
    public void Global_shell_focus_controls_open_action_without_changing_agent_handle_visibility()
    {
        var now = DateTimeOffset.UtcNow;
        var chat = new ActiveAgentChat(
            AgentChatHandleId.Create(),
            new AgentChatIdentity(Guid.NewGuid(), "Agent Alpha", "Reviewer", string.Empty),
            Guid.NewGuid(),
            ActiveAgentChatVisibility.Visible,
            ActiveAgentChatRunState.Idle,
            now,
            now,
            null);

        var result = AgentActiveChatPresentationMapper.Map(chat, isFocused: false);

        Assert.Equal(new PresentationBadge("Kept active", PresentationTone.Default), result.Badges[0]);
        Assert.False(result.Actions[0].IsDisabled);
    }

    [Fact]
    public void Rejects_unknown_active_chat_action_keys()
    {
        var key = new ConversationPresentationKey("archive");

        var exception = Assert.Throws<ArgumentException>(
            () => AgentActiveChatPresentationMapper.ResolveAction(key));

        Assert.Contains("not an Agent active-chat action", exception.Message);
    }
}
