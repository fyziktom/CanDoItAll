using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentToolBackgroundSourceSerializationTests {
    [Fact]
    public void Interactive_reference_retains_its_three_original_serialized_fields_and_nonempty_identifiers() {
        var original = new AgentToolSessionReference(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create());
        var json = JsonSerializer.Serialize(original);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["ExecutionRunId", "ChatSessionId", "AuthorityId"], document.RootElement.EnumerateObject().Select(item => item.Name).ToArray());
        var restored = JsonSerializer.Deserialize<AgentToolSessionReference>(json);
        Assert.Equal(original, restored);
        Assert.NotEqual(Guid.Empty, restored!.ExecutionRunId);
        Assert.NotEqual(Guid.Empty, restored.ChatSessionId);
        Assert.NotEqual(Guid.Empty, restored.AuthorityId.Value);
        Assert.Null(restored.BackgroundSource);
    }

    [Fact]
    public void Background_reference_round_trips_exact_source_with_no_interactive_identity() {
        var binding = new AgentToolBackgroundSourceBinding("fixture-process", "step-instance",
            new(new string('a', 64)), new(new string('b', 64)));
        var original = new AgentToolSessionReference(Guid.NewGuid(), Guid.Empty, default, binding);
        var json = JsonSerializer.Serialize(original);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("ChatSessionId", out _));
        Assert.False(document.RootElement.TryGetProperty("AuthorityId", out _));
        var restored = JsonSerializer.Deserialize<AgentToolSessionReference>(json);
        Assert.Equal(original, restored);
        Assert.NotEqual(Guid.Empty, restored!.ExecutionRunId);
        Assert.Equal(binding, restored.BackgroundSource);
        Assert.Equal(binding.OwnerFingerprint, restored.BackgroundSource!.OwnerFingerprint);
        Assert.Equal(binding.ExecutionFingerprint, restored.BackgroundSource.ExecutionFingerprint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Background_reference_rejects_mixed_interactive_authority_or_chat(bool chat) {
        var binding = new AgentToolBackgroundSourceBinding("fixture-process", "step-instance",
            new(new string('a', 64)), new(new string('b', 64)));
        Assert.Throws<ArgumentException>(() => new AgentToolSessionReference(Guid.NewGuid(),
            chat ? Guid.NewGuid() : Guid.Empty, chat ? default : AgentExecutionAuthorityId.Create(), binding));
    }

    [Fact]
    public void Background_input_cannot_replace_an_interactive_message_or_be_used_as_a_message_id() {
        var reference = new AgentToolSessionReference(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create());
        var record = new AgentToolJournalRecord(1, 1, new(reference, Guid.NewGuid(), AgentRuntimeContextPurpose.InteractiveChat,
            new(Guid.NewGuid(), "fixture-profile", new(3))), [], [], BackgroundInput: new("A detached background prompt."));
        Assert.Throws<InvalidDataException>(record.Validate);
    }
}
