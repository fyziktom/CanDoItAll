using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentsOverviewReadLifecycleTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Latest_usage_scope_wins_even_when_the_old_read_finishes_last(bool routeReplacement) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        var pending = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => pending.Task;
        var first = ChangeScopeAsync(cut, "Agents");
        cut.WaitForAssertion(() => Assert.Equal(2, fixture.Agents.Reads));
        try {
            if (routeReplacement) {
                await NavigateQueryAsync(fixture, cut, "usageScope", "simple-chats");
            } else {
                await ChangeScopeAsync(cut, "Chats");
            }
            cut.WaitForAssertion(() => Assert.Contains("Chats accepted", Rankings(cut)));
        } finally {
            await cut.InvokeAsync(() => pending.TrySetResult(fixture.Agents.Result("Late Agents")));
            await first;
        }
        cut.WaitForAssertion(() => {
            Assert.Contains("Chats accepted", Rankings(cut));
            Assert.DoesNotContain("Late Agents", Rankings(cut));
        });
    }

    [Fact]
    public async Task Late_usage_failure_does_not_replace_newer_success() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        var pending = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => pending.Task;
        var first = ChangeScopeAsync(cut, "Agents");
        cut.WaitForAssertion(() => Assert.Equal(2, fixture.Agents.Reads));
        await ChangeScopeAsync(cut, "Chats");
        await cut.InvokeAsync(() => pending.SetException(new IOException(OverviewPageFixture.PrivateFailure)));
        await first;
        Assert.Contains("Chats accepted", Rankings(cut));
        Assert.Empty(cut.FindAll("[data-testid='agents-overview-usage-partial']"));
        Assert.Empty(cut.FindAll("[data-testid='agents-overview-usage-error']"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_cancels_each_owned_aggregate_read(bool usageLane) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var overview = new TaskCompletionSource<AgentOverviewSnapshot>();
        var usage = new TaskCompletionSource<ProviderUsageSourceResult>();
        if (usageLane) {
            fixture.Agents.Read = _ => usage.Task;
        } else {
            fixture.Overview = _ => overview.Task;
        }
        var cut = fixture.Render();
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.OverviewReads));
        try {
            await fixture.Harness.Context.DisposeRenderedComponentsAsync();
            Assert.True(usageLane ? fixture.Agents.LastToken.IsCancellationRequested : fixture.OverviewToken.IsCancellationRequested);
        } finally {
            overview.TrySetResult(OverviewPageFixture.Snapshot());
            usage.TrySetResult(fixture.Agents.Result("After disposal"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Entering_history_cancels_pending_aggregates(bool usageLane) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var overview = new TaskCompletionSource<AgentOverviewSnapshot>();
        var usage = new TaskCompletionSource<ProviderUsageSourceResult>();
        if (usageLane) {
            fixture.Agents.Read = _ => usage.Task;
        } else {
            fixture.Overview = _ => overview.Task;
        }
        var cut = fixture.Render();
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.OverviewReads));
        try {
            await NavigateQueryAsync(fixture, cut, "tab", AgentWorkspaceTabs.Providers);
            Assert.True(usageLane ? fixture.Agents.LastToken.IsCancellationRequested : fixture.OverviewToken.IsCancellationRequested);
            Assert.Equal(1, fixture.OverviewReads);
        } finally {
            overview.TrySetResult(OverviewPageFixture.Snapshot());
            usage.TrySetResult(fixture.Agents.Result("After history"));
        }
    }

    [Theory]
    [InlineData(AgentWorkspaceTabs.Agents)]
    [InlineData(AgentWorkspaceTabs.Chat)]
    [InlineData(AgentWorkspaceTabs.Capabilities)]
    [InlineData(AgentWorkspaceTabs.Governance)]
    public async Task Aggregate_failure_cannot_override_ready_selection_access(string tab) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Overview = _ => Task.FromException<AgentOverviewSnapshot>(new IOException(OverviewPageFixture.PrivateFailure));
        var cut = fixture.Render(tab);
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.OverviewReads));
        await cut.InvokeAsync(async () => {
            var ready = AgentChatContextAccessState.Ready;
            switch (tab) {
                case AgentWorkspaceTabs.Agents:
                    await cut.FindComponent<AgentCatalogHost>().Instance.ContextAccessStateChanged.InvokeAsync(ready);
                    break;
                case AgentWorkspaceTabs.Chat:
                    await cut.FindComponent<AgentChatPanel>().Instance.ContextAccessStateChanged.InvokeAsync(ready);
                    break;
                case AgentWorkspaceTabs.Capabilities:
                    await cut.FindComponent<AgentCapabilitiesPanel>().Instance.ContextAccessStateChanged.InvokeAsync(ready);
                    break;
                case AgentWorkspaceTabs.Governance:
                    await cut.FindComponent<AgentGovernancePanel>().Instance.ContextAccessStateChanged.InvokeAsync(ready);
                    break;
            }
        });
        cut.WaitForAssertion(() => Assert.Equal(AgentChatContextAccessState.Ready, Context(cut).ContextAccessState));
    }

    [Fact]
    public async Task Overview_failure_preserves_independently_loaded_hr_action() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Overview = _ => Task.FromException<AgentOverviewSnapshot>(new IOException(OverviewPageFixture.PrivateFailure));
        var cut = fixture.Render();
        cut.WaitForElement("[data-testid='agents-overview-load-error']");
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='agents-hr-agent-open-header']").HasAttribute("disabled")));
    }

    [Fact]
    public async Task Bound_failure_preserves_hr_and_accepted_overview() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.BoundFailure = true;
        var cut = fixture.Render();
        cut.WaitForAssertion(() => Assert.Equal("42", Metric(cut, "agents")));
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='agents-hr-agent-open-header']").HasAttribute("disabled")));
        Assert.Empty(cut.FindAll("[data-testid='agents-overview-load-error']"));
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
    }

    [Fact]
    public async Task Usage_failure_is_not_an_overview_failure() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Agents.Read = _ => Task.FromException<ProviderUsageSourceResult>(new IOException(OverviewPageFixture.PrivateFailure));
        var cut = fixture.Render();
        cut.WaitForAssertion(() => Assert.Equal("42", Metric(cut, "agents")));
        cut.WaitForElement("[data-testid='agents-overview-usage-error']");
        Assert.Empty(cut.FindAll("[data-testid='agents-overview-load-error']"));
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='agents-hr-agent-open-header']").HasAttribute("disabled")));
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
    }

    [Fact]
    public async Task Header_failure_does_not_poison_accepted_overview() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.HeaderFailure = true;
        var cut = fixture.Render();
        cut.WaitForAssertion(() => Assert.Equal("42", Metric(cut, "agents")));
        Assert.Empty(cut.FindAll("[data-testid='agents-overview-load-error']"));
        Assert.Equal(AgentChatContextAccessState.Ready, Context(cut).ContextAccessState);
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
    }

    [Fact]
    public async Task Initial_failure_has_no_invented_facts_and_keeps_workspace_context_ready() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Overview = _ => Task.FromException<AgentOverviewSnapshot>(new IOException(OverviewPageFixture.PrivateFailure));
        var cut = fixture.Render();
        cut.WaitForElement("[data-testid='agents-overview-load-error']");
        Assert.Empty(Context(cut).Surface.Position.Facts);
        Assert.Equal("\u2014", Metric(cut, "agents"));
        Assert.Equal("\u2014", HeaderCount(cut));
        Assert.Equal(AgentChatContextAccessState.Ready, Context(cut).ContextAccessState);
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
    }

    [Fact]
    public async Task Explicit_overview_retry_only_reads_its_own_lane() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Overview = _ => Task.FromException<AgentOverviewSnapshot>(new IOException(OverviewPageFixture.PrivateFailure));
        var cut = fixture.Render();
        cut.WaitForElement("[data-testid='agents-overview-load-error']");
        var headers = fixture.HeaderReads;
        var usage = fixture.Agents.Reads;
        fixture.Overview = _ => Task.FromResult(OverviewPageFixture.Snapshot());
        await cut.Find("[data-testid='agents-overview-retry']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal("42", Metric(cut, "agents")));
        Assert.Equal(2, fixture.OverviewReads);
        Assert.Equal(headers, fixture.HeaderReads);
        Assert.Equal(usage, fixture.Agents.Reads);
        Assert.Empty(cut.FindAll("[data-testid='agents-feed-defaults-confirmation']"));
    }

    [Fact]
    public async Task Overview_refresh_failure_retains_accepted_data_with_stale_state() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        fixture.Overview = _ => Task.FromException<AgentOverviewSnapshot>(new IOException(OverviewPageFixture.PrivateFailure));
        await cut.Find("[data-testid='agents-overview-retry']").ClickAsync();
        cut.WaitForElement("[data-testid='agents-overview-stale']");
        Assert.Equal("42", Metric(cut, "agents"));
        Assert.Equal("42", HeaderCount(cut));
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
    }

    [Fact]
    public async Task Usage_refresh_failure_retains_same_scope_data_with_stale_state() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        fixture.Agents.Read = _ => Task.FromException<ProviderUsageSourceResult>(new IOException(OverviewPageFixture.PrivateFailure));
        await cut.Find("[data-testid='agents-overview-usage-retry']").ClickAsync();
        cut.WaitForElement("[data-testid='agents-overview-usage-stale']");
        Assert.Contains("Agents accepted", Rankings(cut));
        Assert.Contains("Chats accepted", Rankings(cut));
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
    }

    [Fact]
    public async Task Failed_requested_scope_never_exposes_previous_scope_as_current() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        fixture.Chats.Read = _ => Task.FromException<ProviderUsageSourceResult>(new IOException(OverviewPageFixture.PrivateFailure));
        await ChangeScopeAsync(cut, "Chats");
        Assert.DoesNotContain("Agents accepted", Rankings(cut));
        Assert.True(cut.Find("[data-testid='agents-overview-open-provider-usage']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Partial_matching_scope_remains_usable_with_visible_warning() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        fixture.Chats.Read = _ => Task.FromResult(fixture.Chats.Result("Partial chats") with {
            State = ProviderUsageSourceState.Partial,
            Error = new("fixture-partial", "Safe partial evidence")
        });
        await ChangeScopeAsync(cut, "Chats");
        cut.WaitForElement("[data-testid='agents-overview-usage-partial']");
        Assert.Contains("Partial chats", Rankings(cut));
        Assert.False(cut.Find("[data-testid='agents-overview-open-provider-usage']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Old_finally_cannot_clear_newer_usage_loading_owner() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        var a = new TaskCompletionSource<ProviderUsageSourceResult>();
        var b = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => a.Task;
        fixture.Chats.Read = _ => b.Task;
        var first = ChangeScopeAsync(cut, "Agents");
        cut.WaitForAssertion(() => Assert.Equal(2, fixture.Agents.Reads));
        var second = ChangeScopeAsync(cut, "Chats");
        cut.WaitForAssertion(() => Assert.Equal(2, fixture.Chats.Reads));
        try {
            await cut.InvokeAsync(() => a.TrySetResult(fixture.Agents.Result("Old completion")));
            await first;
            Assert.Equal("...", Metric(cut, "usage"));
            Assert.True(cut.Find("[data-testid='agents-overview-open-provider-usage']").HasAttribute("disabled"));
        } finally {
            await cut.InvokeAsync(() => b.TrySetResult(fixture.Chats.Result("Current completion")));
            await second;
        }
    }

    [Fact]
    public async Task Same_pending_route_echo_reuses_usage_read() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        var pending = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Chats.Read = _ => pending.Task;
        var change = ChangeScopeAsync(cut, "Chats");
        cut.WaitForAssertion(() => Assert.Equal(2, fixture.Chats.Reads));
        try {
            await NavigateQueryAsync(fixture, cut, "usageScope", "simple-chats");
            Assert.Equal(2, fixture.Chats.Reads);
        } finally {
            await cut.InvokeAsync(() => pending.TrySetResult(fixture.Chats.Result("Chats accepted")));
            await change;
        }
    }

    [Fact]
    public async Task One_accepted_overview_drives_header_and_metrics() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var cut = fixture.Render();
        cut.WaitForDashboardLoaded();
        Assert.Equal("42", HeaderCount(cut));
        Assert.Equal(HeaderCount(cut), Metric(cut, "agents"));
        await ChangeScopeAsync(cut, "Chats");
        Assert.Equal(1, fixture.OverviewReads);
        Assert.Equal("42", HeaderCount(cut));
    }

    private static AgentChatContextSurfaceProvider Context(IRenderedComponent<AgentsHomePage> cut)
        => cut.FindComponent<AgentChatContextSurfaceProvider>().Instance;

    private static string Rankings(IRenderedComponent<AgentsHomePage> cut)
        => cut.Find("[data-testid='agents-overview-top-consumers']").TextContent;

    private static string Metric(IRenderedComponent<AgentsHomePage> cut, string name)
        => cut.Find($"[data-testid='agents-overview-metric-{name}'] strong").TextContent.Trim();

    private static string HeaderCount(IRenderedComponent<AgentsHomePage> cut)
        => Assert.IsType<string>(cut.FindComponent<PageHeader>().FindComponents<CompactStat>().Single(x => x.Instance.Label == "technical agents").Instance.Value);

    private static Task ChangeScopeAsync(IRenderedComponent<AgentsHomePage> cut, string label)
        => cut.InvokeAsync(() => cut.FindAll("[data-testid='agents-overview-usage-scope'] button").Single(x => x.TextContent.Trim() == label).ClickAsync());

    private static Task NavigateQueryAsync(OverviewPageFixture fixture, IRenderedComponent<AgentsHomePage> cut, string key, string value) {
        var navigation = fixture.Harness.Context.Services.GetRequiredService<NavigationManager>();
        return cut.InvokeAsync(() => navigation.NavigateTo(navigation.GetUriWithQueryParameter(key, value)));
    }
}

internal sealed class OverviewPageFixture : IAsyncDisposable {
    public const string PrivateFailure = "Private infrastructure failure detail";
    public ComponentTestHarness Harness { get; private set; } = default!;
    public OverviewUsageSource Agents { get; } = new(ProviderUsageWorkloadKind.Agent, "Agents accepted");
    public OverviewUsageSource Chats { get; } = new(ProviderUsageWorkloadKind.SimpleChat, "Chats accepted");
    public Func<CancellationToken, Task<AgentOverviewSnapshot>> Overview { get; set; } = _ => Task.FromResult(Snapshot());
    public bool HeaderFailure { get; set; }
    public bool BoundFailure { get; set; }
    public int OverviewReads { get; private set; }
    public int HeaderReads { get; private set; }
    public CancellationToken OverviewToken { get; private set; }

    public static async Task<OverviewPageFixture> CreateAsync(Action<IServiceCollection>? configure = null) {
        var fixture = new OverviewPageFixture();
        fixture.Harness = await ComponentTestHarness.CreateAsync(services => {
            var factory = services.Last(x => x.ServiceType == typeof(IAgentFrameworkWorkspaceService)).ImplementationFactory!;
            services.AddScoped<IAgentFrameworkWorkspaceService>(provider => {
                var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, OverviewWorkspaceProxy>();
                var target = (OverviewWorkspaceProxy)(object)proxy;
                target.Inner = (IAgentFrameworkWorkspaceService)factory(provider);
                target.Overview = token => {
                    fixture.OverviewReads++;
                    fixture.OverviewToken = token;
                    return fixture.Overview(token);
                };
                target.Header = () => {
                    fixture.HeaderReads++;
                    return fixture.HeaderFailure;
                };
                return proxy;
            });
            services.AddSingleton<IBoundAgentResourceQuery>(new OverviewBoundRead(fixture));
            services.RemoveAll<IProviderUsageProjectionSource>();
            services.AddSingleton<IProviderUsageProjectionSource>(fixture.Agents);
            services.AddSingleton<IProviderUsageProjectionSource>(fixture.Chats);
            configure?.Invoke(services);
        });
        return fixture;
    }

    public IRenderedComponent<AgentsHomePage> Render(string tab = AgentWorkspaceTabs.Overview) {
        Harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/agents?tab=" + tab);
        return Harness.Context.Render<AgentsHomePage>();
    }

    public static AgentOverviewSnapshot Snapshot() => AgentOverviewSnapshot.Empty with {
        Totals = AgentOverviewTotals.Empty with { AgentCount = 42, ProviderCount = 3, CapabilityCount = 7 }
    };

    public ValueTask DisposeAsync() => Harness.DisposeAsync();

    private sealed class OverviewBoundRead(OverviewPageFixture fixture) : IBoundAgentResourceQuery {
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => fixture.BoundFailure
            ? Task.FromException<int>(new IOException(PrivateFailure)) : Task.FromResult(9);
    }
}

public class OverviewWorkspaceProxy : DispatchProxy {
    public IAgentFrameworkWorkspaceService Inner { get; set; } = default!;
    public Func<CancellationToken, Task<AgentOverviewSnapshot>> Overview { get; set; } = default!;
    public Func<bool> Header { get; set; } = default!;

    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        if (method!.Name == nameof(IAgentFrameworkWorkspaceService.GetAgentOverviewAsync)) {
            return Overview((CancellationToken)args![0]!);
        }
        if (method.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) && Header()) {
            return Task.FromException<IReadOnlyList<AgentDefinition>>(new IOException(OverviewPageFixture.PrivateFailure));
        }
        return method.Invoke(Inner, args);
    }
}

internal sealed class OverviewUsageSource(ProviderUsageWorkloadKind kind, string name) : IProviderUsageProjectionSource {
    public string SourceName => kind.ToString();
    public ProviderUsageWorkloadKind WorkloadKind => kind;
    public Func<CancellationToken, Task<ProviderUsageSourceResult>>? Read { get; set; }
    public int Reads { get; private set; }
    public CancellationToken LastToken { get; private set; }

    public ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default) {
        Reads++;
        LastToken = cancellationToken;
        return new(Read?.Invoke(cancellationToken) ?? Task.FromResult(Result(name)));
    }

    public ProviderUsageSourceResult Result(string consumer) => new(SourceName, kind, ProviderUsageSourceState.Complete,
        [new(SourceName, kind, kind == ProviderUsageWorkloadKind.Agent ? ProviderUsageConsumerKind.Agent : ProviderUsageConsumerKind.SimpleChatDefinition,
            SourceName, consumer, null, "Fixture provider", ProviderKind.OpenAi, "fixture-model", SourceName,
            ProviderUsageExecutionOutcome.Succeeded, ProviderUsageCompleteness.Observed, ProviderUsagePricingCompleteness.Unpriced,
            ProviderUsageTokenCounts.Empty, null, DateTimeOffset.UnixEpoch)], DateTimeOffset.UnixEpoch);
}
