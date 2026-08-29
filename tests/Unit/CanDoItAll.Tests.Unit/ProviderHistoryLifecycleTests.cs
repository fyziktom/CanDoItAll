using System.Text;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderHistoryLifecycleTests {
    [Theory]
    [InlineData(1, "")]
    [InlineData(3, "")]
    [InlineData(4, "😀")]
    [InlineData(5, "😀a")]
    public void Detail_bound_preserves_unicode(int limit, string expected) {
        var captured = HistoryTextCapture.Capture("😀abc", limit, []);
        Assert.Equal(expected, captured.Text);
        Assert.Equal(Encoding.UTF8.GetByteCount(captured.Text), captured.CapturedBytes);
        Assert.Equal(7, captured.OriginalBytes);
        Assert.True(captured.Flags.HasFlag(HistoryDetailFlags.Truncated));
    }

    [Fact]
    public void Secret_crossing_capture_boundary_is_redacted_before_truncation() {
        var captured = HistoryTextCapture.Capture("abcfixture-secret-token trailing", 8, ["fixture-secret-token"]);
        Assert.Equal("abc[reda", captured.Text);
        Assert.True(captured.Flags.HasFlag(HistoryDetailFlags.Redacted));
        Assert.DoesNotContain("fixture", captured.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Bearer sample.token.value")]
    [InlineData("api_key=sample-secret")]
    [InlineData("password: sample-secret")]
    public void Credential_patterns_are_redacted(string text) {
        Assert.Equal("[redacted]", HistoryTextCapture.Capture(text, 1024, []).Text);
    }

    [Fact]
    public void Late_cancellation_is_ignored_but_conflicting_completion_is_rejected() {
        var terminal = Completion(HistoryOutcome.Succeeded, HistoryUsageState.Complete);
        Assert.False(HistoryCompletionTransitions.ShouldApply(terminal, Completion(HistoryOutcome.Cancelled, HistoryUsageState.Unavailable)));
        Assert.Throws<ProviderHistoryException>(() => HistoryCompletionTransitions.ShouldApply(terminal,
            terminal with { Usage = new(HistoryUsageState.Complete, 999, 5) }));
    }

    [Fact]
    public void Observed_terminal_evidence_can_resolve_an_interrupted_attempt() {
        var terminal = Completion(HistoryOutcome.Succeeded, HistoryUsageState.Complete);
        Assert.True(HistoryCompletionTransitions.ShouldApply(Completion(HistoryOutcome.Interrupted, HistoryUsageState.Unavailable), terminal));
        Assert.False(HistoryCompletionTransitions.ShouldApply(terminal, terminal));
    }

    [Fact]
    public void Repeated_redactions_cannot_pull_a_cut_secret_fragment_into_captured_text() {
        var secret = "known-" + new string('s', 4090);
        var captured = HistoryTextCapture.Capture(secret + secret + "end", 2048, [secret]);
        Assert.DoesNotContain("known-", captured.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('s', 16), captured.Text, StringComparison.Ordinal);
        Assert.True(captured.Flags.HasFlag(HistoryDetailFlags.Truncated));
    }


    [Fact]
    public void Reconciliation_cannot_erase_known_usage_categories_or_price() {
        var cancelled = Completion(HistoryOutcome.Cancelled, HistoryUsageState.Partial) with {
            Usage = new(HistoryUsageState.Partial, 10, null, 4),
            Price = new(HistoryPriceState.ProviderReported, 1m, "USD")
        };
        var terminal = Completion(HistoryOutcome.Succeeded, HistoryUsageState.Complete);
        Assert.Throws<ProviderHistoryException>(() => HistoryCompletionTransitions.ShouldApply(cancelled, terminal));
        Assert.Throws<ProviderHistoryException>(() => HistoryCompletionTransitions.ShouldApply(cancelled,
            terminal with { Price = cancelled.Price }));
        Assert.True(HistoryCompletionTransitions.ShouldApply(cancelled,
            terminal with { Usage = terminal.Usage with { CachedInputTokens = 4 }, Price = cancelled.Price }));
    }

    [Theory]
    [InlineData("😀abc")]
    [InlineData("a😀b😀")]
    public async Task Stream_byte_count_handles_split_surrogate_pairs(string value) {
        var recorder = new RecordingProviderHistory();
        var start = await recorder.BeginAsync(new(new(new(Guid.NewGuid()), "fixture", "OpenAi", new("model"), new("model")),
            HistoryOperation.CompleteChat, HistoryInvocationContext.Create()), default);
        var buffer = new HistoryResponseBuffer(start);
        foreach (var character in value) {
            buffer.Append(character.ToString());
        }
        Assert.Equal(value, buffer.GetText());
        Assert.Equal(Encoding.UTF8.GetByteCount(value), buffer.OriginalBytes);
    }

    private static HistoryAttemptCompletion Completion(HistoryOutcome outcome, HistoryUsageState usage)
        => new(outcome, DateTimeOffset.UnixEpoch, new(usage, 10, 5), new(HistoryPriceState.Unpriced));
}
