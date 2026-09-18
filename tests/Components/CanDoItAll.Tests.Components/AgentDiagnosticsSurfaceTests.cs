using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.AgentFramework.UI.Diagnostics;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDiagnosticsSurfaceTests {
    [Theory]
    [InlineData("loading")]
    [InlineData("ready")]
    [InlineData("dashboard-stale")]
    [InlineData("agents-unavailable")]
    [InlineData("runs-stale")]
    [InlineData("partial")]
    [InlineData("empty")]
    [InlineData("adversarial")]
    public void Service_free_surface_renders_safe_independent_states(string scenario) {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var presentation = DiagnosticsSandboxFixture.Create(scenario);
        var cut = context.Render<AgentDiagnosticsSurface>(parameters => parameters.Add(value => value.Presentation, presentation));
        Assert.DoesNotContain(DiagnosticsSandboxFixture.Forbidden, cut.Markup);
        Assert.Empty(cut.FindAll("script"));
        if (presentation.RunsState.AcceptedRevision.HasValue) {
            Assert.Contains("latest twelve runs", cut.Markup);
        }
        if (scenario == "adversarial") {
            Assert.Contains("<script", cut.Find("[data-testid='agents-diagnostics-panel']").TextContent);
            Assert.Contains("UTC", cut.Markup);
        }
        foreach (var lane in Enum.GetValues<DiagnosticsLane>()) {
            if (presentation.ReadState(lane).Error is not null) {
                Assert.Contains("Retry " + lane.ToString().ToLowerInvariant(), cut.Markup);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Host_publishes_async_lane_completion_only_while_owned(bool dispose) {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var reads = new DeferredReads();
        context.Services.AddSingleton<IAgentDiagnosticsReads>(reads);
        var cut = context.Render<AgentDiagnosticsPanel>();
        Assert.True(cut.Find("[data-testid='diagnostics-refresh']").HasAttribute("disabled"));
        Assert.Contains("Execution boundary", cut.Markup);
        if (dispose) {
            await context.DisposeRenderedComponentsAsync();
        }
        await context.Renderer.Dispatcher.InvokeAsync(() => reads.Pending.SetResult([DiagnosticsSandboxFixture.Run()]));
        if (!dispose) {
            cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='diagnostics-refresh']").HasAttribute("disabled")));
            Assert.Contains("Recent execution", cut.Markup);
        } else {
            Assert.True(reads.Token.IsCancellationRequested);
        }
    }

    private sealed class DeferredReads : IAgentDiagnosticsReads {
        public bool FailDashboard { get; set; }
        public TaskCompletionSource<IReadOnlyList<ExecutionRunRecord>> Pending { get; } = new();
        public CancellationToken Token { get; private set; }
        public Task<SandboxDashboardSnapshot> ReadDashboardAsync(CancellationToken token) => FailDashboard
            ? Task.FromException<SandboxDashboardSnapshot>(new InvalidOperationException("private failure"))
            : Task.FromResult(DiagnosticsSandboxFixture.Dashboard());
        public Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<AgentDefinition>>([]);
        public Task<IReadOnlyList<ExecutionRunRecord>> ReadRecentRunsAsync(CancellationToken token) {
            Token = token;
            return Pending.Task;
        }
    }

    [Fact]
    public async Task Dashboard_retry_does_not_claim_global_success_while_refresh_runs_are_pending() {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var reads = new DeferredReads { FailDashboard = true };
        context.Services.AddSingleton<IAgentDiagnosticsReads>(reads);
        var cut = context.Render<AgentDiagnosticsPanel>();
        var notices = context.Services.GetRequiredService<NotificationService>();
        Assert.Empty(notices.Messages);
        reads.FailDashboard = false;
        await cut.FindAll("button").Single(button => button.TextContent.Contains("Retry dashboard", StringComparison.Ordinal)).ClickAsync();
        Assert.True(cut.Find("[data-testid='diagnostics-refresh']").HasAttribute("disabled"));
        Assert.Equal("Dashboard diagnostics refreshed from the integrated runtime.", Assert.Single(notices.Messages).Detail);
        await cut.InvokeAsync(() => reads.Pending.SetResult([]));
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='diagnostics-refresh']").HasAttribute("disabled")));
        Assert.Single(notices.Messages);
    }

    [Fact]
    public async Task Refresh_emits_once_without_mutating_presentation() {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var presentation = DiagnosticsSandboxFixture.Create("ready");
        var intents = new List<DiagnosticsIntent>();
        var cut = context.Render<AgentDiagnosticsSurface>(parameters => parameters
            .Add(value => value.Presentation, presentation).Add(value => value.OnIntent, intents.Add));
        await cut.Find("[data-testid='diagnostics-refresh']").ClickAsync();
        Assert.IsType<DiagnosticsIntent.Refresh>(Assert.Single(intents));
        Assert.Same(presentation, cut.Instance.Presentation);
    }
}
