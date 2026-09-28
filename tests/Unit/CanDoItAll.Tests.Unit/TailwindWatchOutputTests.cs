using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class TailwindWatchOutputTests
{
    [Theory]
    [InlineData("\u001b[3m\u001b[1m\u001b[34m≈\u001b[39m\u001b[22m\u001b[23m tailwindcss \u001b[34mv4.3.3\u001b[39m", TailwindWatchLineKind.Informational, "tailwindcss v4.3.3")]
    [InlineData("\u001b[2m┌\u001b[22m", TailwindWatchLineKind.Informational, "")]
    [InlineData("> tailwindcss -i ./input.css -o ../src/App/CanDoItAll.Web/wwwroot/css/output.css --watch", TailwindWatchLineKind.Informational, "> tailwindcss -i ./input.css -o ../src/App/CanDoItAll.Web/wwwroot/css/output.css --watch")]
    [InlineData("\r\u001b[2K\u001b[2mPolling for changes…\u001b[22m", TailwindWatchLineKind.Informational, "Polling for changes…")]
    [InlineData("npm warn config production Use `--omit=dev` instead.", TailwindWatchLineKind.Informational, "npm warn config production Use `--omit=dev` instead.")]
    [InlineData("Done in \u001b[34m113ms\u001b[39m", TailwindWatchLineKind.BuildCompleted, "Done in 113ms")]
    [InlineData("\r\u001b[2KDone in \u001b[31m4s\u001b[39m", TailwindWatchLineKind.BuildCompleted, "Done in 4s")]
    [InlineData("[0] Done in 5ms", TailwindWatchLineKind.BuildCompleted, "[0] Done in 5ms")]
    [InlineData("\u001b[31mError:\u001b[39m", TailwindWatchLineKind.Error, "Error:")]
    [InlineData("\u001b[2m│\u001b[22m CssSyntaxError: input.css:3:1: Missing closing } at .broken", TailwindWatchLineKind.Error, "CssSyntaxError: input.css:3:1: Missing closing } at .broken")]
    [InlineData("Ôöé CssSyntaxError: input.css:3:1: Missing closing } at .broken", TailwindWatchLineKind.Error, "CssSyntaxError: input.css:3:1: Missing closing } at .broken")]
    [InlineData("npm error Missing script: \"watch\"", TailwindWatchLineKind.Error, "npm error Missing script: \"watch\"")]
    [InlineData("npm ERR! code ELIFECYCLE", TailwindWatchLineKind.Error, "npm ERR! code ELIFECYCLE")]
    [InlineData("'tailwindcss' is not recognized as an internal or external command,", TailwindWatchLineKind.Error, "'tailwindcss' is not recognized as an internal or external command,")]
    [InlineData("sh: 1: tailwindcss: not found", TailwindWatchLineKind.Error, "sh: 1: tailwindcss: not found")]
    public void Parser_classifies_tailwind_watch_output(string rawLine, TailwindWatchLineKind expectedKind, string expectedText)
    {
        var line = TailwindWatchOutputParser.Parse(rawLine);

        Assert.Equal(expectedKind, line.Kind);
        Assert.Equal(expectedText, line.Text);
    }

    [Fact]
    public void Parser_reports_the_build_duration()
    {
        var line = TailwindWatchOutputParser.Parse("Done in \u001b[32m5ms\u001b[39m");

        Assert.Equal("5ms", line.Duration);
    }

    [Fact]
    public void Tracker_fails_a_cycle_that_reported_an_error_before_done()
    {
        var tracker = new TailwindBuildCycleTracker();

        var headerChanged = tracker.ObserveError(TailwindWatchOutputParser.Parse("Error:"));
        var detailChanged = tracker.ObserveError(TailwindWatchOutputParser.Parse("│ CssSyntaxError: input.css:3:1: Missing closing } at .broken"));
        var outcome = tracker.CompleteCycle();

        Assert.True(headerChanged);
        Assert.True(detailChanged);
        Assert.Equal(TailwindBuildCycleOutcome.Failed, outcome);
        Assert.Equal("CssSyntaxError: input.css:3:1: Missing closing } at .broken", tracker.LastError);
    }

    [Fact]
    public void Tracker_keeps_the_root_cause_of_a_multi_line_npm_error()
    {
        var tracker = new TailwindBuildCycleTracker();

        Assert.True(tracker.ObserveError(TailwindWatchOutputParser.Parse("npm error Missing script: \"watch\"")));
        Assert.False(tracker.ObserveError(TailwindWatchOutputParser.Parse("npm error")));
        Assert.False(tracker.ObserveError(TailwindWatchOutputParser.Parse("npm error Did you mean this?")));

        Assert.Equal("npm error Missing script: \"watch\"", tracker.LastError);
    }

    [Fact]
    public void Tracker_clears_the_error_after_a_clean_cycle()
    {
        var tracker = new TailwindBuildCycleTracker();
        tracker.ObserveError(TailwindWatchOutputParser.Parse("CssSyntaxError: broken"));
        tracker.CompleteCycle();

        var outcome = tracker.CompleteCycle();

        Assert.Equal(TailwindBuildCycleOutcome.Succeeded, outcome);
        Assert.Null(tracker.LastError);
    }

    [Fact]
    public void Restart_policy_backs_off_quick_failures_and_resets_after_a_stable_run()
    {
        var policy = new TailwindWatchRestartPolicy(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(1));

        var first = policy.NextDelay(null, TimeSpan.FromSeconds(1));
        var second = policy.NextDelay(first, TimeSpan.FromSeconds(1));
        var third = policy.NextDelay(second, TimeSpan.FromSeconds(1));
        var capped = policy.NextDelay(third, TimeSpan.FromSeconds(1));
        var afterStableRun = policy.NextDelay(capped, TimeSpan.FromMinutes(5));

        Assert.Equal(TimeSpan.FromSeconds(2), first);
        Assert.Equal(TimeSpan.FromSeconds(4), second);
        Assert.Equal(TimeSpan.FromSeconds(8), third);
        Assert.Equal(TimeSpan.FromSeconds(10), capped);
        Assert.Equal(TimeSpan.FromSeconds(2), afterStableRun);
    }

    [Fact]
    public void Output_cursor_publishes_a_quiet_trailing_line_without_inventing_a_blank_line()
    {
        var cursor = new ManagerProcessOutputCursor(TimeSpan.FromMilliseconds(250));

        var first = cursor.Read("≈ tailwindcss v4.3.3\n\nDone in 113ms", flush: false, nowMilliseconds: 1_000);
        var beforeIdle = cursor.Read("≈ tailwindcss v4.3.3\n\nDone in 113ms", flush: false, nowMilliseconds: 1_100);
        var afterIdle = cursor.Read("≈ tailwindcss v4.3.3\n\nDone in 113ms", flush: false, nowMilliseconds: 1_300);
        var nextOutput = cursor.Read("≈ tailwindcss v4.3.3\n\nDone in 113ms\r\nDone in 5ms", flush: false, nowMilliseconds: 5_000);
        var nextAfterIdle = cursor.Read("≈ tailwindcss v4.3.3\n\nDone in 113ms\r\nDone in 5ms", flush: false, nowMilliseconds: 5_300);

        Assert.Equal(["≈ tailwindcss v4.3.3", string.Empty], first);
        Assert.Empty(beforeIdle);
        Assert.Equal(["Done in 113ms"], afterIdle);
        Assert.Empty(nextOutput);
        Assert.Equal(["Done in 5ms"], nextAfterIdle);
    }

    [Fact]
    public void Output_cursor_flush_publishes_the_pending_line_immediately()
    {
        var cursor = new ManagerProcessOutputCursor(TimeSpan.FromMinutes(1));

        var lines = cursor.Read("first\nsecond", flush: true, nowMilliseconds: 0);

        Assert.Equal(["first", "second"], lines);
    }
}
