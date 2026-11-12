using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ChatSessionTitleChangeTests {
    [Fact]
    public void Rename_changes_only_title_and_monotonic_timestamp() {
        var current = CreateSession();
        var renamed = Change(current, "  Reviewed\t title\n ") with { ChangedAtUtc = current.UpdatedAtUtc.AddDays(-1) };

        var result = renamed.Apply(current);

        Assert.Equal(current with { Title = "Reviewed title" }, result);
        Assert.Same(current.Messages, result.Messages);
        Assert.Same(current.Compatibility, result.Compatibility);
    }

    [Fact]
    public void Unchanged_normalized_title_is_a_noop() {
        var current = CreateSession();
        Assert.Same(current, Change(current, "  Original  ").Apply(current));
    }

    [Fact]
    public void Stale_title_is_rejected_without_disclosing_titles() {
        var current = CreateSession();
        var failure = Assert.Throws<ChatSessionTitleConflictException>(() =>
            (Change(current, "Private proposed title") with { ExpectedTitle = "Private previous title" }).Apply(current));
        Assert.Equal(current.Id, failure.SessionId);
        Assert.DoesNotContain("Private", failure.Message);
        Assert.DoesNotContain(current.Title, failure.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Different_owner_is_rejected(bool differentAgent) {
        var current = CreateSession();
        var change = Change(current, "New");
        change = differentAgent ? change with { AgentId = Guid.NewGuid() } : change with { SessionId = Guid.NewGuid() };
        Assert.Throws<InvalidOperationException>(() => change.Apply(current));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    public void Blank_title_is_rejected(string title) {
        var current = CreateSession();
        Assert.Throws<ArgumentException>(() => Change(current, title).Apply(current));
    }

    [Fact]
    public void Normalized_title_is_bounded() {
        Assert.Equal(new string('x', 96), ChatSessionTitleChange.Normalize(new string('x', 120)));
    }

    [Fact]
    public void Late_run_publication_preserves_title_and_keeps_new_transcript() {
        var original = CreateSession();
        var renamed = Change(original, "Reviewed title").Apply(original);
        var completed = original with {
            Messages = [.. original.Messages, new(Guid.NewGuid(), ChatMessageRole.Assistant, "Completed", original.UpdatedAtUtc, 1)]
        };

        var result = ChatSessionTitleChange.PreserveCurrentTitle(renamed, completed);

        Assert.Equal(completed with { Title = renamed.Title, UpdatedAtUtc = renamed.UpdatedAtUtc }, result);
        Assert.Same(completed.Messages, result!.Messages);
    }

    [Fact]
    public void Run_publication_cannot_replace_session_identity() {
        var current = CreateSession();
        Assert.Throws<InvalidOperationException>(() => ChatSessionTitleChange.PreserveCurrentTitle(current,
            current with { Id = Guid.NewGuid() }));
    }

    private static ChatSessionTitleChange Change(ChatSessionRecord current, string title)
        => new(current.AgentId, current.Id, current.Title, title, current.UpdatedAtUtc.AddMinutes(1));

    private static ChatSessionRecord CreateSession() {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), Guid.NewGuid(), "Original", now, now,
            [new(Guid.NewGuid(), ChatMessageRole.User, "Retain this message", now, 3)], Guid.NewGuid(),
            new("runtime-session", "{}", [], true));
    }
}
