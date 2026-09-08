using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Diagnostics;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentDiagnosticsTests {
    [Theory]
    [InlineData(DiagnosticsLane.Dashboard, false)]
    [InlineData(DiagnosticsLane.Agents, false)]
    [InlineData(DiagnosticsLane.Runs, false)]
    [InlineData(DiagnosticsLane.Dashboard, true)]
    [InlineData(DiagnosticsLane.Agents, true)]
    [InlineData(DiagnosticsLane.Runs, true)]
    public async Task Superseded_success_failure_and_finally_cannot_publish(DiagnosticsLane lane, bool fails) {
        var read = new Reads();
        using var session = Create(read);
        var pending = new TaskCompletionSource();
        read.Before = (kind, _) => kind == lane ? pending.Task : Task.CompletedTask;
        var old = session.RefreshAsync();
        read.Before = (_, _) => Task.CompletedTask;
        await session.RefreshAsync();
        var accepted = session.Presentation;
        if (fails) {
            pending.SetException(new InvalidOperationException(DiagnosticsSandboxFixture.Forbidden));
        } else {
            pending.SetResult();
        }
        await old;
        Assert.Same(accepted, session.Presentation);
        Assert.False(accepted.IsBusy);
    }

    [Theory]
    [InlineData(DiagnosticsLane.Dashboard)]
    [InlineData(DiagnosticsLane.Agents)]
    [InlineData(DiagnosticsLane.Runs)]
    public async Task Partial_failure_preserves_other_lanes_and_retry_is_local(DiagnosticsLane lane) {
        var read = new Reads();
        using var session = Create(read);
        read.Before = (kind, _) => kind == lane ? Task.FromException(new Exception(DiagnosticsSandboxFixture.Forbidden)) : Task.CompletedTask;
        await session.RefreshAsync();
        Assert.Equal(DiagnosticsReadPhase.Failed, session.Presentation.ReadState(lane).Phase);
        Assert.All(Enum.GetValues<DiagnosticsLane>().Where(value => value != lane), value => Assert.Equal(DiagnosticsReadPhase.Ready, session.Presentation.ReadState(value).Phase));
        read.Before = (_, _) => Task.CompletedTask;
        var before = read.Calls;
        await session.RetryAsync(lane);
        Assert.Equal(before + 1, read.Calls);
        read.Before = (_, _) => Task.FromException(new Exception(DiagnosticsSandboxFixture.Forbidden));
        await session.RetryAsync(lane);
        Assert.Equal(DiagnosticsReadPhase.Stale, session.Presentation.ReadState(lane).Phase);
        Assert.NotNull(session.Presentation.Dashboard);
        Assert.Single(session.Presentation.Runs);
        Assert.DoesNotContain(DiagnosticsSandboxFixture.Forbidden, JsonSerializer.Serialize(session.Presentation));
    }

    [Theory]
    [InlineData(DiagnosticsLane.Dashboard)]
    [InlineData(DiagnosticsLane.Agents)]
    [InlineData(DiagnosticsLane.Runs)]
    public async Task Disposal_retains_token_resources_until_noncooperative_read_finishes(DiagnosticsLane lane) {
        var pending = new TaskCompletionSource();
        CancellationToken token = default;
        var read = new Reads { Before = (kind, value) => {
            if (kind != lane) {
                return Task.CompletedTask;
            }
            token = value;
            return pending.Task;
        }};
        var session = Create(read);
        var operation = session.RefreshAsync();
        var publications = 0;
        session.Changed += () => publications++;
        session.Dispose();
        session.Dispose();
        Assert.True(token.IsCancellationRequested);
        using (token.Register(() => { })) {
            Assert.True(token.WaitHandle.WaitOne(0));
        }
        pending.SetResult();
        await operation;
        Assert.Equal(0, publications);
        Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);
    }

    [Fact]
    public async Task Accepted_rows_are_independent_and_failure_population_is_only_recent_window() {
        var read = new Reads();
        using var session = Create(read);
        await session.RefreshAsync();
        read.Runs.Clear();
        Assert.Single(session.Presentation.Runs);
        Assert.Single(session.Presentation.Failures);
        Assert.Equal(23, session.Presentation.Dashboard!.FailedRuns);
        Assert.Equal("Unknown agent", session.Presentation.Runs[0].Agent);
    }

    [Theory]
    [InlineData("cs-CZ", 5)]
    [InlineData("ar-SA", -7)]
    public void Allowlist_is_bounded_encoded_input_and_invariant_utc(string culture, int offset) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var at = new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.FromHours(offset));
            var row = DiagnosticsPresentationMapping.Run(DiagnosticsSandboxFixture.Run(true) with { UpdatedAtUtc = at });
            Assert.Equal(at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), row.Updated);
            Assert.True(row.Title.Length <= 160);
            Assert.Contains("<script", row.Title);
            Assert.DoesNotContain(DiagnosticsSandboxFixture.Forbidden, JsonSerializer.Serialize(DiagnosticsSandboxFixture.Create("adversarial")));
            Assert.Equal("Unrecognized workspace process host", DiagnosticsPresentationMapping.Boundary(DiagnosticsSandboxFixture.Dashboard().ToolExecutionBoundary with { Mode = DiagnosticsSandboxFixture.Forbidden }).Host);
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static AgentDiagnosticsSession Create(Reads reads) => new(reads, NullLogger<AgentDiagnosticsSession>.Instance);
    private sealed class Reads : IAgentDiagnosticsReads {
        public Func<DiagnosticsLane, CancellationToken, Task> Before { get; set; } = (_, _) => Task.CompletedTask;
        public List<ExecutionRunRecord> Runs { get; } = [DiagnosticsSandboxFixture.Run()];
        public int Calls { get; private set; }
        public async Task<SandboxDashboardSnapshot> ReadDashboardAsync(CancellationToken token) {
            Calls++;
            await Before(DiagnosticsLane.Dashboard, token);
            return DiagnosticsSandboxFixture.Dashboard();
        }
        public async Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken token) {
            Calls++;
            await Before(DiagnosticsLane.Agents, token);
            return [];
        }
        public async Task<IReadOnlyList<ExecutionRunRecord>> ReadRecentRunsAsync(CancellationToken token) {
            Calls++;
            await Before(DiagnosticsLane.Runs, token);
            return Runs;
        }
    }
}
