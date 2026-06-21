using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafAgentRuntimeProviderHealthTests
{
    [Fact]
    public void BuildProviderTestInputMessages_treats_omitted_messages_as_empty()
    {
        var request = new ProviderTestChatRequest(
            Model: "gpt-5.4-mini",
            SystemPrompt: string.Empty,
            Messages: null!,
            Prompt: "Reply with OK only.");

        var messages = MafAgentRuntime.BuildProviderTestInputMessages(request);

        Assert.Empty(messages);
    }

    [Fact]
    public void BuildProviderTestInputMessages_orders_trims_and_filters_messages()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new ProviderTestChatRequest(
            Model: "gpt-5.4-mini",
            SystemPrompt: string.Empty,
            Messages:
            [
                new ProviderTestChatMessage(ChatMessageRole.User, " second ", now.AddMinutes(2)),
                new ProviderTestChatMessage(ChatMessageRole.Assistant, "  ", now.AddMinutes(1)),
                new ProviderTestChatMessage(ChatMessageRole.System, " first ", now)
            ],
            Prompt: string.Empty);

        var messages = MafAgentRuntime.BuildProviderTestInputMessages(request);

        Assert.Collection(
            messages,
            message =>
            {
                Assert.Equal(ChatRole.System, message.Role);
                Assert.Equal("first", message.Text);
            },
            message =>
            {
                Assert.Equal(ChatRole.User, message.Role);
                Assert.Equal("second", message.Text);
            });
    }
}
