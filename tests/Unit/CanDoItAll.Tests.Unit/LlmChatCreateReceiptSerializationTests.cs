using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatCreateReceiptSerializationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Durable_receipt_round_trip_preserves_the_original_validated_identity(bool useWebDefaults) {
        var original = new LlmChatDefinitionCreateReceipt(
            new(new("hr-simple-chat", "original-agent", "original-chat"), new(Guid.NewGuid())),
            new(Guid.NewGuid()), new(7), 19, DateTimeOffset.UtcNow);
        var options = useWebDefaults ? JsonSerializerOptions.Web : JsonSerializerOptions.Default;

        var json = JsonSerializer.Serialize(original, options);
        var recovered = JsonSerializer.Deserialize<LlmChatDefinitionCreateReceipt>(json, options);

        Assert.Equal(original, recovered);
        Assert.NotEqual(Guid.Empty, recovered!.Key.IntentId.Value);
        Assert.NotEqual(Guid.Empty, recovered.DefinitionId.Value);
        Assert.Equal(7, recovered.DefinitionRevision.Value);
    }
}
