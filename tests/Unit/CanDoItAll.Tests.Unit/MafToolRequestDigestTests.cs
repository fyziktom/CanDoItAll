using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafToolRequestDigestTests {
    [Fact]
    public void A_new_internal_history_correlation_preserves_the_transport_digest_and_does_not_mutate_options() {
        var firstContext = HistoryInvocationContext.Create(HistoryWorkload.Agent);
        var secondContext = HistoryInvocationContext.Create(HistoryWorkload.Agent);
        Assert.NotEqual(firstContext.RequestId, secondContext.RequestId);
        var options = Options();
        var first = ProviderHistoryChatContext.WithContext(options, firstContext);
        var second = ProviderHistoryChatContext.WithContext(options, secondContext);
        ChatMessage[] input = [new(ChatRole.User, "Read the admitted file.")];

        Assert.Equal(MafToolAdmissionChatClient.RequestDigest(input, first, []),
            MafToolAdmissionChatClient.RequestDigest(input, second, []));
        Assert.Same(firstContext, ProviderHistoryChatContext.Read(first));
        Assert.Same(secondContext, ProviderHistoryChatContext.Read(second));
        Assert.Single(first.Tools!);
        Assert.Equal("original", first.AdditionalProperties!["remote_option"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Changed_transmitted_options_messages_or_tools_still_require_explicit_recovery(int change) {
        var original = ProviderHistoryChatContext.WithContext(Options(), HistoryInvocationContext.Create(HistoryWorkload.Agent));
        var changed = ProviderHistoryChatContext.WithContext(Options(), HistoryInvocationContext.Create(HistoryWorkload.Agent));
        ChatMessage[] input = [new(ChatRole.User, "Read the admitted file.")];
        ChatMessage[] changedInput = input;
        switch (change) {
            case 0:
                changed.AdditionalProperties!["remote_option"] = "changed";
                break;
            case 1:
                changedInput = [new(ChatRole.User, "Read a different file.")];
                break;
            case 2:
                changed.Tools = [AIFunctionFactory.Create((string value) => value, name: "different_read")];
                break;
        }

        Assert.NotEqual(MafToolAdmissionChatClient.RequestDigest(input, original, []),
            MafToolAdmissionChatClient.RequestDigest(changedInput, changed, []));
    }

    private static ChatOptions Options() => new() {
        ModelId = "fixture-model",
        AdditionalProperties = new() { ["remote_option"] = "original" },
        Tools = [AIFunctionFactory.Create((string value) => value, name: "fixture_read")]
    };
}
