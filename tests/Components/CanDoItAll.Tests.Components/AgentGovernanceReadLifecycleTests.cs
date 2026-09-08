using System.Globalization;
using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentGovernanceReadLifecycleTests {
    [Fact]
    public Task Requested_agent_A_list_late_success_cannot_replace_B() => AgentRaceAsync(false, false);

    [Fact]
    public Task Requested_agent_A_late_failure_cannot_fault_B() => AgentRaceAsync(true, false);

    [Fact]
    public Task Stale_callbacks_cannot_publish_A_or_failed_access_after_B() => AgentRaceAsync(false, true);

    private static async Task AgentRaceAsync(bool fail, bool callbacks) {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.Run1]);
        f.Reads.List = (q, t) => q.AgentId == GovernancePanelFixture.A ? pending.Read(t) : Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.RunB]);
        var cut = f.Render(GovernancePanelFixture.A);
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.B));
        cut.WaitForAssertion(() => f.AssertDetail(cut, "Run B"));
        var before = f.Publications.Count;
        await f.CompleteAsync(() => {
            if (fail) {
                pending.Fail();
            } else {
                pending.Complete();
            }
        });
        f.AssertDetail(cut, "Run B");
        Assert.False(f.Context.Renderer.UnhandledException.IsCompleted, "An obsolete read escaped into the renderer.");
        Assert.Equal(AgentChatContextAccessState.Ready, f.Access.Last());
        if (callbacks) {
            Assert.Empty(f.Publications.Skip(before));
        }
    }

    [Fact]
    public async Task Catalog_completion_cannot_overwrite_a_newer_agent_request() {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending<IReadOnlyList<AgentDefinition>>(GovernancePanelFixture.Agents);
        f.Reads.Catalog = pending.Read;
        var cut = f.Render(GovernancePanelFixture.A);
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.B));
        await f.CompleteAsync(pending.Complete);
        cut.WaitForAssertion(() => f.AssertDetail(cut, "Run B"));
        Assert.DoesNotContain(f.Selected, id => id == GovernancePanelFixture.A || id is null);
        Assert.DoesNotContain(AgentChatContextAccessState.Failed, f.Access);
    }

    [Fact]
    public Task Run_R1_detail_late_success_cannot_replace_R2() => DetailRaceAsync(false);

    [Fact]
    public Task Run_R1_detail_late_failure_cannot_fault_R2() => DetailRaceAsync(true);

    private static async Task DetailRaceAsync(bool fail) {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        var pending = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1));
        f.Reads.Detail = (id, t) => id == GovernancePanelFixture.R1 ? pending.Read(t) : Task.FromResult(GovernancePanelFixture.Detail(GovernancePanelFixture.Run2));
        var first = f.SelectAsync(cut, "Run 1");
        cut.WaitForAssertion(() => Assert.True(pending.Started));
        await f.SelectAsync(cut, "Run 2");
        await f.CompleteAsync(() => {
            if (fail) {
                pending.Fail();
            } else {
                pending.Complete();
            }
        });
        Assert.Null(await Record.ExceptionAsync(() => first));
        Assert.False(f.Context.Renderer.UnhandledException.IsCompleted, "An obsolete detail failure escaped into the renderer.");
        f.AssertDetail(cut, "Run 2");
    }

    [Fact]
    public async Task Replacing_agent_cancels_pending_list_read() {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.Run1]);
        f.Reads.List = (q, t) => q.AgentId == GovernancePanelFixture.A ? pending.Read(t) : Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.RunB]);
        var cut = f.Render(GovernancePanelFixture.A);
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.B));
        Assert.True(pending.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Replacing_run_cancels_pending_detail_read() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        var pending = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1));
        f.Reads.Detail = (id, t) => id == GovernancePanelFixture.R1 ? pending.Read(t) : Task.FromResult(GovernancePanelFixture.Detail(GovernancePanelFixture.Run2));
        var first = f.SelectAsync(cut, "Run 1");
        cut.WaitForAssertion(() => Assert.True(pending.Started));
        await f.SelectAsync(cut, "Run 2");
        Assert.True(pending.Token.IsCancellationRequested);
        await f.CompleteAsync(pending.Complete);
        await first;
    }

    [Fact]
    public async Task Disposing_panel_cancels_pending_catalog_read() {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending<IReadOnlyList<AgentDefinition>>(GovernancePanelFixture.Agents);
        f.Reads.Catalog = pending.Read;
        f.Render(GovernancePanelFixture.A);
        await f.Context.DisposeRenderedComponentsAsync();
        Assert.True(pending.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Disposing_panel_cancels_pending_list_and_detail_reads() {
        foreach (var list in new[] { true, false }) {
            await using var f = new GovernancePanelFixture();
            var pendingList = f.Pending<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.Run1]);
            var pendingDetail = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1));
            if (list) {
                f.Reads.List = (_, t) => pendingList.Read(t);
            } else {
                f.Reads.Detail = (_, t) => pendingDetail.Read(t);
            }
            f.Render(GovernancePanelFixture.A);
            await f.Context.DisposeRenderedComponentsAsync();
            Assert.True((list ? pendingList.Token : pendingDetail.Token).IsCancellationRequested);
        }
    }

    [Fact]
    public async Task Noncooperative_completion_after_disposal_publishes_nothing() {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.Run1]);
        f.Reads.List = (_, t) => pending.Read(t);
        f.Render(GovernancePanelFixture.A);
        await f.Context.DisposeRenderedComponentsAsync();
        var count = f.Publications.Count;
        await f.CompleteAsync(pending.Complete);
        Assert.Equal(count, f.Publications.Count);
    }

    [Fact]
    public async Task Old_detail_finally_cannot_clear_newer_busy_state() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        var first = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1));
        var second = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run2));
        f.Reads.Detail = (id, t) => id == GovernancePanelFixture.R1 ? first.Read(t) : second.Read(t);
        var a = f.SelectAsync(cut, "Run 1");
        cut.WaitForAssertion(() => Assert.True(first.Started));
        var b = f.SelectAsync(cut, "Run 2");
        cut.WaitForAssertion(() => Assert.True(second.Started));
        await f.CompleteAsync(first.Complete);
        await a;
        Assert.True(f.Button(cut, "Refresh").HasAttribute("disabled"));
        await f.CompleteAsync(second.Complete);
        await b;
    }

    [Fact]
    public async Task Refresh_detail_cannot_override_newer_manual_run_selection() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        var pending = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1));
        f.Reads.Detail = (id, t) => id == GovernancePanelFixture.R1 ? pending.Read(t) : Task.FromResult(GovernancePanelFixture.Detail(GovernancePanelFixture.Run2));
        var refresh = f.Button(cut, "Refresh").ClickAsync();
        cut.WaitForAssertion(() => Assert.True(pending.Started));
        await f.SelectAsync(cut, "Run 2");
        await f.CompleteAsync(pending.Complete);
        await refresh;
        f.AssertDetail(cut, "Run 2");
    }

    [Fact]
    public async Task Accepted_list_remains_visible_when_automatic_detail_fails() {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1));
        f.Reads.Detail = (_, t) => pending.Read(t);
        var cut = f.Render(GovernancePanelFixture.A);
        Assert.Equal(2, f.Rows(cut).Count);
        await f.CompleteAsync(pending.Fail);
        Assert.Equal(2, f.Rows(cut).Count);
        cut.WaitForElement("[data-testid='agents-governance-detail-error']");
    }

    [Fact]
    public async Task Missing_requested_agent_does_not_fall_back_to_all_agents() {
        await using var f = new GovernancePanelFixture();
        f.Render(Guid.NewGuid());
        Assert.Empty(f.Reads.Queries);
        Assert.Equal(AgentChatContextAccessState.Failed, f.Access.Last());
        Assert.Empty(f.Selected);
    }

    [Fact]
    public async Task Missing_agent_retry_keeps_target_through_real_page_echo() {
        var requested = Guid.NewGuid();
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var navigation = fixture.Harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents?tab=governance&agentId=" + requested);
        var page = fixture.Harness.Context.Render<AgentsHomePage>();
        var panel = page.WaitForComponent<AgentGovernancePanel>();
        page.WaitForAssertion(() => Assert.Equal(requested, panel.Instance.PreferredAgentId));
        var refresh = panel.FindAll("button").First(x => x.TextContent.Contains("Retry", StringComparison.Ordinal) || x.TextContent.Trim() == "Refresh");
        await refresh.ClickAsync();
        page.WaitForAssertion(() => Assert.Equal(requested, page.FindComponent<AgentGovernancePanel>().Instance.PreferredAgentId));
    }

    [Fact]
    public async Task Same_preferred_agent_echo_preserves_manual_run_selection() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        await f.SelectAsync(cut, "Run 2");
        var reads = f.Reads.DetailIds.Count;
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.A));
        f.AssertDetail(cut, "Run 2");
        Assert.Equal(reads, f.Reads.DetailIds.Count);
    }

    [Fact]
    public async Task Explicit_all_agents_then_same_requested_agent_reloads_target() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        await cut.Find("select").ChangeAsync(new ChangeEventArgs { Value = "" });
        Assert.Null(f.Reads.Queries.Last().AgentId);
        cut.Render(p => p.Add(c => c.PreferredAgentId, (Guid?)null));
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.A));
        Assert.Equal(GovernancePanelFixture.A, f.Reads.Queries.Last().AgentId);
    }

    [Fact]
    public async Task Detail_failure_keeps_validated_agent_context_ready() {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending(GovernancePanelFixture.Detail(GovernancePanelFixture.Run1));
        f.Reads.Detail = (_, t) => pending.Read(t);
        f.Render(GovernancePanelFixture.A);
        await f.CompleteAsync(pending.Fail);
        Assert.Equal(AgentChatContextAccessState.Ready, f.Access.Last());
        Assert.Equal(GovernancePanelFixture.A, f.Selected.Last());
    }

    [Fact]
    public Task Wrong_run_identity_in_detail_is_rejected() => WrongIdentityAsync(true);

    [Fact]
    public Task Wrong_agent_identity_in_detail_is_rejected() => WrongIdentityAsync(false);

    private static async Task WrongIdentityAsync(bool run) {
        await using var f = new GovernancePanelFixture();
        var wrong = GovernancePanelFixture.Run1 with { Title = "Wrong identity detail", Id = run ? Guid.NewGuid() : GovernancePanelFixture.R1, AgentId = run ? GovernancePanelFixture.A : GovernancePanelFixture.B };
        f.Reads.Detail = (_, _) => Task.FromResult(GovernancePanelFixture.Detail(wrong));
        var cut = f.Render(GovernancePanelFixture.A);
        Assert.DoesNotContain("Wrong identity detail", cut.Markup);
        cut.WaitForElement("[data-testid='agents-governance-detail-error']");
    }

    [Fact]
    public async Task Same_target_failed_refresh_retains_explicit_stale_list() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        f.Reads.List = (_, _) => Task.FromException<IReadOnlyList<ExecutionRunRecord>>(new IOException(GovernancePanelFixture.PrivateFailure));
        Assert.Null(await Record.ExceptionAsync(() => f.Button(cut, "Refresh").ClickAsync()));
        Assert.Equal(2, f.Rows(cut).Count);
        cut.WaitForElement("[data-testid='agents-governance-list-stale']");
    }

    [Fact]
    public async Task Different_agent_loading_hides_previous_agent_rows() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        var pending = f.Pending<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.RunB]);
        f.Reads.List = (_, t) => pending.Read(t);
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.B));
        Assert.Empty(f.Rows(cut));
    }

    [Fact]
    public async Task Removed_selected_run_remains_explicitly_unavailable() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        await f.SelectAsync(cut, "Run 2");
        f.Reads.List = (_, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.Run1]);
        var reads = f.Reads.DetailIds.Count;
        await f.Button(cut, "Refresh").ClickAsync();
        cut.WaitForElement("[data-testid='agents-governance-detail-unavailable']");
        Assert.Equal(reads, f.Reads.DetailIds.Count);
        Assert.DoesNotContain("Run 1", cut.FindAll("h2").Select(x => x.TextContent.Trim()));
    }

    [Fact]
    public async Task Retry_detail_does_not_reload_catalog_or_run_list() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        f.Reads.Detail = (_, _) => Task.FromException<ExecutionRunDetail>(new IOException(GovernancePanelFixture.PrivateFailure));
        Assert.Null(await Record.ExceptionAsync(() => f.SelectAsync(cut, "Run 2")));
        var catalog = f.Reads.CatalogReads;
        var list = f.Reads.Queries.Count;
        f.Reads.Detail = (_, _) => Task.FromResult(GovernancePanelFixture.Detail(GovernancePanelFixture.Run2));
        await cut.Find("[data-testid='agents-governance-detail-retry']").ClickAsync();
        f.AssertDetail(cut, "Run 2");
        Assert.Equal(catalog, f.Reads.CatalogReads);
        Assert.Equal(list, f.Reads.Queries.Count);
    }

    [Fact]
    public async Task Initial_read_failure_shows_bounded_error_and_retry() {
        await using var f = new GovernancePanelFixture();
        f.Reads.Catalog = _ => Task.FromException<IReadOnlyList<AgentDefinition>>(new IOException(GovernancePanelFixture.PrivateFailure));
        IRenderedComponent<AgentGovernancePanel>? cut = null;
        Assert.Null(Record.Exception(() => cut = f.Render(GovernancePanelFixture.A)));
        Assert.False(cut!.Markup.Contains(GovernancePanelFixture.PrivateFailure, StringComparison.Ordinal), "Infrastructure details must not be rendered.");
        cut.Find("[data-testid='agents-governance-catalog-retry']");
    }

    [Fact]
    public async Task Synchronous_reads_and_repeated_echo_do_not_duplicate_loads() {
        await using var f = new GovernancePanelFixture();
        var cut = f.Render(GovernancePanelFixture.A);
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.A));
        cut.Render(p => p.Add(c => c.PreferredAgentId, GovernancePanelFixture.A));
        Assert.Equal(1, f.Reads.CatalogReads);
        Assert.Single(f.Reads.Queries);
        Assert.Single(f.Reads.DetailIds);
    }

    [Fact]
    public async Task Empty_guid_is_invalid_and_never_publishes_all_agents() {
        await using var f = new GovernancePanelFixture();
        f.Render(Guid.Empty);
        Assert.Empty(f.Reads.Queries);
        Assert.Empty(f.Selected);
        Assert.Equal(AgentChatContextAccessState.Failed, f.Access.Last());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Canceled_owner_allows_delayed_token_registration_without_late_publication(bool fail) {
        await using var f = new GovernancePanelFixture();
        var pending = f.Pending<IReadOnlyList<ExecutionRunRecord>>([GovernancePanelFixture.Run1]);
        f.Reads.List = (_, t) => pending.Read(t);
        f.Render(GovernancePanelFixture.A);
        await f.Context.DisposeRenderedComponentsAsync();
        Assert.True(pending.Token.IsCancellationRequested);
        var notified = false;
        using var registration = pending.Token.Register(() => notified = true);
        Assert.True(notified);
        var publications = f.Publications.Count;
        await f.CompleteAsync(() => {
            if (fail) {
                pending.Fail();
            } else {
                pending.Complete();
            }
        });
        Assert.Equal(publications, f.Publications.Count);
        Assert.False(f.Context.Renderer.UnhandledException.IsCompleted);
    }

    [Theory]
    [InlineData("en-US", 120)]
    [InlineData("cs-CZ", -240)]
    public async Task Governance_timestamps_are_explicit_UTC_independent_of_culture_and_offset(string culture, int offsetMinutes) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            await using var f = new GovernancePanelFixture();
            var timestamp = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
            var run = GovernancePanelFixture.Run1 with { UpdatedAtUtc = timestamp };
            f.Reads.List = (_, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([run]);
            f.Reads.Detail = (_, _) => Task.FromResult(GovernancePanelFixture.Detail(run));
            var cut = f.Render(GovernancePanelFixture.A);
            Assert.Contains("2026-03-29 01:30:00 UTC", cut.Markup);
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Forbidden_execution_payloads_and_raw_runtime_prose_never_render() {
        await using var f = new GovernancePanelFixture();
        var denied = "opaque-fixture-" + Guid.NewGuid().ToString("N");
        var run = GovernancePanelFixture.Run1 with { MetadataJson = denied, SerializedSessionStateJson = denied, InputSummary = denied, ResultSummary = denied, StructuredOutputRawOutput = denied, StructuredOutputValidationErrorsJson = denied, PendingApprovals = [new("approval", "call", "Tool", "tool", denied, denied)] };
        var detail = new ExecutionRunDetail(run, null, [new(Guid.NewGuid(), run.AgentId, null, DateTimeOffset.UnixEpoch, ExecutionState.Running, "Execution", denied)], []) {
            Approvals = [new("approval", run.Id, "call", "Tool", "tool", denied, denied, ExecutionApprovalStatus.Pending, DateTimeOffset.UnixEpoch, null, "", "", "")],
            Checkpoints = [new(Guid.NewGuid(), run.Id, denied, denied, "Saved", ExecutionState.Running, [denied], DateTimeOffset.UnixEpoch, null, denied, denied, denied, denied, denied, denied, denied, denied, denied)]
        };
        f.Reads.List = (_, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([run]);
        f.Reads.Detail = (_, _) => Task.FromResult(detail);
        var cut = f.Render(GovernancePanelFixture.A);
        Assert.False(cut.Markup.Contains(denied, StringComparison.Ordinal), "A denied execution field crossed the presentation boundary.");
    }

    [Fact]
    public async Task Runtime_details_shared_children_use_UTC_and_omit_raw_timeline_message() {
        await using var f = new GovernancePanelFixture();
        var denied = "opaque-fixture-" + Guid.NewGuid().ToString("N");
        var cut = f.Context.Render<AgentRuntimeDetailsDialog>(p => p
            .Add(c => c.Run, GovernancePanelFixture.Run1)
            .Add(c => c.ExecutionLog, [new ExecutionLogEntry(Guid.NewGuid(), GovernancePanelFixture.A, null, DateTimeOffset.UnixEpoch, ExecutionState.Running, "Execution", denied)])
            .Add(c => c.Metrics, [new AgentRunMetric(Guid.NewGuid(), GovernancePanelFixture.A, null, DateTimeOffset.UnixEpoch, RunOutcome.Succeeded, "Fixture", "model", 100, 3, 4, 1)]));
        Assert.False(cut.Markup.Contains(denied, StringComparison.Ordinal), "Raw timeline content crossed the shared child boundary.");
        Assert.Contains("1970-01-01 00:00:00 UTC", cut.Markup);
        Assert.Contains("Input 3, output 4, tools 1", cut.Markup);
    }
}

internal sealed class GovernancePanelFixture : IAsyncDisposable {
    public static readonly Guid A = Guid.Parse("f1543271-b70d-4856-99c3-87fbd3ad2b01");
    public static readonly Guid B = Guid.Parse("f1543271-b70d-4856-99c3-87fbd3ad2b02");
    public static readonly Guid R1 = Guid.Parse("f1543271-b70d-4856-99c3-87fbd3ad2b11");
    public static readonly Guid R2 = Guid.Parse("f1543271-b70d-4856-99c3-87fbd3ad2b12");
    public static readonly ExecutionRunRecord Run1 = Run(R1, A, "Run 1");
    public static readonly ExecutionRunRecord Run2 = Run(R2, A, "Run 2");
    public static readonly ExecutionRunRecord RunB = Run(Guid.Parse("f1543271-b70d-4856-99c3-87fbd3ad2b21"), B, "Run B");
    public static readonly IReadOnlyList<AgentDefinition> Agents = [Agent(A, "Agent A"), Agent(B, "Agent B")];
    public const string PrivateFailure = "Private infrastructure fixture failure";
    public BunitContext Context { get; } = new();
    public GovernanceWorkspaceProxy Reads { get; }
    public List<Guid?> Selected { get; } = [];
    public List<AgentChatContextAccessState> Access { get; } = [];
    public List<string> Publications { get; } = [];
    private readonly List<Action> releases = [];

    public GovernancePanelFixture() {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.Services.AddLogging();
        Context.Services.AddCanDoItAllBaseLib();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, GovernanceWorkspaceProxy>();
        Reads = (GovernanceWorkspaceProxy)(object)workspace;
        Context.Services.AddSingleton(workspace);
        Context.Services.AddScoped<IAgentGovernanceReads, AgentGovernanceReads>();
    }

    public IRenderedComponent<AgentGovernancePanel> Render(Guid? id) => Context.Render<AgentGovernancePanel>(p => p
        .Add(c => c.PreferredAgentId, id)
        .Add(c => c.SelectedAgentChanged, (AgentDefinition? agent) => {
            Selected.Add(agent?.Id);
            Publications.Add("selection");
        })
        .Add(c => c.ContextAccessStateChanged, (AgentChatContextAccessState access) => {
            Access.Add(access);
            Publications.Add("access");
        }));

    public DeferredGovernanceRead<T> Pending<T>(T result) {
        var pending = new DeferredGovernanceRead<T>(result);
        releases.Add(pending.Complete);
        return pending;
    }

    public Task CompleteAsync(Action complete) => Context.Renderer.Dispatcher.InvokeAsync(async () => {
        complete();
        await Task.Yield();
    });

    public AngleSharp.Dom.IElement Button(IRenderedComponent<AgentGovernancePanel> cut, string text) => cut.FindAll("button").Single(x => x.TextContent.Trim() == text);
    public IReadOnlyList<AngleSharp.Dom.IElement> Rows(IRenderedComponent<AgentGovernancePanel> cut) => cut.FindAll("[data-testid='agents-governance-run-item']");
    public Task SelectAsync(IRenderedComponent<AgentGovernancePanel> cut, string title) => Rows(cut).Single(x => x.TextContent.Contains(title, StringComparison.Ordinal)).ClickAsync();
    public void AssertDetail(IRenderedComponent<AgentGovernancePanel> cut, string title) => Assert.Contains(title, cut.FindAll("h2").Select(x => x.TextContent.Trim()));

    public async ValueTask DisposeAsync() {
        await Context.DisposeRenderedComponentsAsync();
        await CompleteAsync(() => {
            foreach (var release in releases) {
                release();
            }
        });
        await Context.DisposeAsync();
    }

    public static ExecutionRunDetail Detail(ExecutionRunRecord run) => new(run, null, [], []);
    public static ExecutionRunRecord Run(Guid id, Guid agent, string title) => new(id, agent, null, title, "manual", "", "", "", "", "", "{}", "Input", "Result", "Fixture provider", "fixture-model", ExecutionState.Completed, RunOutcome.Succeeded, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null, "", null, []);
    private static AgentDefinition Agent(Guid id, string name) => new(id, name, "Fixture", "Summary", "", AgentLifecycleStatus.Active, null, "fixture-model", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged, 0.2, true, false, "{}", false, "", AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}

internal sealed class DeferredGovernanceRead<T>(T result) {
    private readonly TaskCompletionSource<T> completion = new();
    public CancellationToken Token { get; private set; }
    public bool Started { get; private set; }
    public Task<T> Read(CancellationToken token) {
        Token = token;
        Started = true;
        return completion.Task;
    }
    public void Complete() => completion.TrySetResult(result);
    public void Fail() => completion.TrySetException(new IOException(GovernancePanelFixture.PrivateFailure));
}

public class GovernanceWorkspaceProxy : DispatchProxy {
    public Func<CancellationToken, Task<IReadOnlyList<AgentDefinition>>> Catalog { get; set; } = _ => Task.FromResult(GovernancePanelFixture.Agents);
    public Func<ExecutionRunQuery, CancellationToken, Task<IReadOnlyList<ExecutionRunRecord>>> List { get; set; } = (q, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(q.AgentId == GovernancePanelFixture.B ? [GovernancePanelFixture.RunB] : [GovernancePanelFixture.Run1, GovernancePanelFixture.Run2]);
    public Func<Guid, CancellationToken, Task<ExecutionRunDetail>> Detail { get; set; } = (id, _) => Task.FromResult(GovernancePanelFixture.Detail(new[] { GovernancePanelFixture.Run1, GovernancePanelFixture.Run2, GovernancePanelFixture.RunB }.Single(x => x.Id == id)));
    public int CatalogReads { get; private set; }
    public List<ExecutionRunQuery> Queries { get; } = [];
    public List<Guid> DetailIds { get; } = [];

    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        var token = args!.OfType<CancellationToken>().SingleOrDefault();
        switch (method!.Name) {
            case nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync):
                CatalogReads++;
                return Catalog(token);
            case nameof(IAgentFrameworkWorkspaceService.ListExecutionRunsAsync):
                var query = (ExecutionRunQuery)args[0]!;
                Queries.Add(query);
                return List(query, token);
            case nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync):
                var id = (Guid)args[0]!;
                DetailIds.Add(id);
                return Detail(id, token);
            default:
                throw new NotSupportedException("Unexpected workspace operation in Governance fixture.");
        }
    }
}
