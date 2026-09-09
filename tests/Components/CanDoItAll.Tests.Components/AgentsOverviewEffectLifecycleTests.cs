using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public enum OverviewDialogCase { Consumer, Provider, Model }

public sealed class AgentsOverviewEffectLifecycleTests {

    public enum DelayedReadOutcome { Success, Failure, Canceled }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer, DelayedReadOutcome.Success)]
    [InlineData(OverviewDialogCase.Consumer, DelayedReadOutcome.Failure)]
    [InlineData(OverviewDialogCase.Consumer, DelayedReadOutcome.Canceled)]
    [InlineData(OverviewDialogCase.Provider, DelayedReadOutcome.Success)]
    [InlineData(OverviewDialogCase.Provider, DelayedReadOutcome.Failure)]
    [InlineData(OverviewDialogCase.Provider, DelayedReadOutcome.Canceled)]
    [InlineData(OverviewDialogCase.Model, DelayedReadOutcome.Success)]
    [InlineData(OverviewDialogCase.Model, DelayedReadOutcome.Failure)]
    [InlineData(OverviewDialogCase.Model, DelayedReadOutcome.Canceled)]
    public async Task Usage_dialog_cancellation_allows_delayed_registration_and_fences_finally(OverviewDialogCase kind, DelayedReadOutcome outcome) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var release = new TaskCompletionSource();
        var newer = new TaskCompletionSource<ProviderUsageSourceResult>();
        var registered = new TaskCompletionSource();
        var callbacks = 0;
        fixture.Agents.Read = async token => {
            await release.Task;
            try {
                using var registration = token.Register(() => callbacks++);
                Assert.True(token.WaitHandle.WaitOne(0));
                registered.SetResult();
            } catch (Exception error) {
                registered.SetException(error);
                throw;
            }
            return outcome switch {
                DelayedReadOutcome.Failure => throw new IOException(OverviewPageFixture.PrivateFailure),
                DelayedReadOutcome.Canceled => throw new OperationCanceledException(token),
                _ => fixture.Agents.Result("Old result")
            };
        };
        fixture.Chats.Read = _ => newer.Task;
        var cut = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.Agents.Reads));
        var oldToken = fixture.Agents.LastToken;
        cut.Render(p => p.Add(c => c.Type, ComponentType(kind)).Add(c => c.Parameters, DynamicParameters(ProviderUsageWorkloadSelection.SimpleChats)));
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.Chats.Reads));
        Assert.True(oldToken.IsCancellationRequested);
        await cut.InvokeAsync(() => release.SetResult());
        await registered.Task;
        Assert.Equal(1, callbacks);
        Assert.Empty(cut.FindComponents<CdaChart>());
        Assert.Empty(cut.FindAll("[data-testid='" + ContentId(kind) + "']"));
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
        await cut.InvokeAsync(() => newer.SetResult(fixture.Chats.Result("Current result")));
        cut.WaitForElement("[data-testid='" + ContentId(kind) + "']");
        Assert.Throws<ObjectDisposedException>(() => oldToken.WaitHandle);
        Assert.Empty(fixture.Harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Fact]
    public async Task Closing_team_editor_cancels_its_icon_picker_and_preserves_unrelated_dialog() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var host = fixture.Harness.Context.Render<DialogHost>();
        var dialogs = fixture.Harness.Context.Services.GetRequiredService<DialogService>();
        using var lease = dialogs.PreserveDialogsOnSamePageNavigation();
        var unrelatedTask = dialogs.OpenAsync("Independent", _ => builder => builder.AddContent(0, "Unrelated work"));
        var unrelated = Assert.Single(dialogs.Dialogs);
        var editorTask = dialogs.OpenAsync<AgentTeamDetailsDialog>("Team");
        host.WaitForElement("[data-testid='agents-team-choose-icon']");
        var editor = dialogs.Dialogs.Single(dialog => dialog.ComponentType == typeof(AgentTeamDetailsDialog));
        var pickerTask = host.InvokeAsync(() => host.Find("[data-testid='agents-team-choose-icon']").ClickAsync());
        host.WaitForAssertion(() => Assert.Equal(3, dialogs.Dialogs.Count));
        try {
            await host.InvokeAsync(() => editor.CloseAsync());
            await editorTask;
            host.WaitForAssertion(() => Assert.Same(unrelated, Assert.Single(dialogs.Dialogs)));
            await pickerTask;
            Assert.Contains("Unrelated work", host.Markup);
        } finally {
            foreach (var reference in dialogs.Dialogs.ToArray()) {
                await host.InvokeAsync(() => reference.CloseAsync());
            }
            await pickerTask;
            await unrelatedTask;
        }
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer, false)]
    [InlineData(OverviewDialogCase.Consumer, true)]
    [InlineData(OverviewDialogCase.Provider, false)]
    [InlineData(OverviewDialogCase.Provider, true)]
    [InlineData(OverviewDialogCase.Model, false)]
    [InlineData(OverviewDialogCase.Model, true)]
    public async Task Closing_pending_usage_dialog_cancels_read_and_ignores_late_completion(OverviewDialogCase kind, bool failLate) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var host = fixture.Harness.Context.Render<DialogHost>();
        var dialogs = fixture.Harness.Context.Services.GetRequiredService<DialogService>();
        var pending = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => pending.Task;
        var opening = dialogs.OpenAsync("Owned usage", ComponentType(kind), Parameters(ProviderUsageWorkloadSelection.Agents));
        host.WaitForAssertion(() => Assert.Equal(1, fixture.Agents.Reads));
        var owned = Assert.Single(dialogs.Dialogs);
        try {
            await host.InvokeAsync(() => owned.CloseAsync());
            host.WaitForAssertion(() => Assert.Empty(dialogs.Dialogs));
            Assert.True(fixture.Agents.LastToken.IsCancellationRequested);
        } finally {
            await host.InvokeAsync(() => Complete(pending, fixture, failLate));
            await opening;
        }
        Assert.Empty(host.FindAll("[data-testid='" + ContentId(kind) + "']"));
        Assert.Empty(fixture.Harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer, false)]
    [InlineData(OverviewDialogCase.Consumer, true)]
    [InlineData(OverviewDialogCase.Provider, false)]
    [InlineData(OverviewDialogCase.Provider, true)]
    [InlineData(OverviewDialogCase.Model, false)]
    [InlineData(OverviewDialogCase.Model, true)]
    public async Task Usage_dialog_request_replacement_rejects_old_success_and_failure(OverviewDialogCase kind, bool failLate) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var pending = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => pending.Task;
        var cut = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.Agents.Reads));
        try {
            cut.Render(p => p.Add(c => c.Type, ComponentType(kind)).Add(c => c.Parameters, DynamicParameters(ProviderUsageWorkloadSelection.SimpleChats)));
            cut.WaitForAssertion(() => Assert.Equal(1, fixture.Chats.Reads));
            cut.WaitForElement("[data-testid='" + ContentId(kind) + "']");
            Assert.True(fixture.Agents.LastToken.IsCancellationRequested);
        } finally {
            await cut.InvokeAsync(() => Complete(pending, fixture, failLate));
        }
        cut.WaitForAssertion(() => Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup));
        Assert.NotEmpty(cut.FindComponents<CdaChart>());
        Assert.Empty(fixture.Harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer)]
    [InlineData(OverviewDialogCase.Provider)]
    [InlineData(OverviewDialogCase.Model)]
    public async Task Usage_dialog_infrastructure_error_is_bounded(OverviewDialogCase kind) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Agents.Read = _ => Task.FromException<ProviderUsageSourceResult>(new IOException(OverviewPageFixture.PrivateFailure));
        var cut = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        cut.WaitForAssertion(() => Assert.Contains("could not be loaded", cut.Markup));
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer)]
    [InlineData(OverviewDialogCase.Provider)]
    [InlineData(OverviewDialogCase.Model)]
    public async Task Usage_dialog_matching_partial_evidence_is_usable_with_warning(OverviewDialogCase kind) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Agents.Read = _ => Task.FromResult(fixture.Agents.Result("Partial consumer") with {
            State = ProviderUsageSourceState.Partial,
            Error = new("partial", OverviewPageFixture.PrivateFailure)
        });
        var cut = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        cut.WaitForElement("[data-testid='" + ContentId(kind) + "']");
        Assert.Contains("Displayed totals are partial", cut.Markup);
        Assert.DoesNotContain(OverviewPageFixture.PrivateFailure, cut.Markup);
        Assert.NotEmpty(cut.FindComponents<CdaChart>().SelectMany(chart => chart.Instance.Series));
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer)]
    [InlineData(OverviewDialogCase.Provider)]
    [InlineData(OverviewDialogCase.Model)]
    public async Task Usage_dialog_chart_options_and_series_are_instance_owned(OverviewDialogCase kind) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var a = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        var b = Render(fixture, kind, ProviderUsageWorkloadSelection.SimpleChats);
        a.WaitForElement("[data-testid='" + ContentId(kind) + "']");
        b.WaitForElement("[data-testid='" + ContentId(kind) + "']");
        var first = a.FindComponents<CdaChart>().Select(x => x.Instance).ToArray();
        var second = b.FindComponents<CdaChart>().Select(x => x.Instance).ToArray();
        Assert.NotEmpty(first);
        Assert.NotEmpty(second);
        foreach (var left in first) {
            foreach (var right in second) {
                Assert.NotSame(left.Options, right.Options);
                Assert.NotSame(left.Series, right.Series);
                Assert.DoesNotContain(left.Series, x => right.Series.Any(y => ReferenceEquals(x.Points, y.Points)));
            }
        }
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer)]
    [InlineData(OverviewDialogCase.Provider)]
    [InlineData(OverviewDialogCase.Model)]
    public async Task Usage_dialog_rejects_conflicting_source_metadata(OverviewDialogCase kind) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Agents.Read = _ => Task.FromResult(fixture.Agents.Result("Wrong source") with { WorkloadKind = ProviderUsageWorkloadKind.SimpleChat });
        var cut = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        cut.WaitForAssertion(() => Assert.Contains("could not be loaded", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='" + ContentId(kind) + "']"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Leaving_overview_or_replacing_scope_closes_only_owned_usage_dialogs(bool leaveOverview) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var page = fixture.Render();
        page.WaitForDashboardLoaded();
        var host = fixture.Harness.Context.Render<DialogHost>();
        var dialogs = fixture.Harness.Context.Services.GetRequiredService<DialogService>();
        var unrelatedTask = dialogs.OpenAsync("Independent overlay", _ => builder => builder.AddContent(0, "Unrelated work"));
        var unrelated = Assert.Single(dialogs.Dialogs);
        var opening = page.InvokeAsync(() => page.Find("[data-testid='agents-overview-open-provider-usage']").ClickAsync());
        host.WaitForElement("[data-testid='provider-usage-dialog']");
        await page.InvokeAsync(() => {
            var navigation = fixture.Harness.Context.Services.GetRequiredService<NavigationManager>();
            navigation.NavigateTo(navigation.GetUriWithQueryParameter(leaveOverview ? "tab" : "usageScope", leaveOverview ? "providers" : "simple-chats"));
        });
        try {
            host.WaitForAssertion(() => Assert.Same(unrelated, Assert.Single(dialogs.Dialogs)));
            await opening;
            Assert.Contains("Unrelated work", host.Markup);
        } finally {
            await host.InvokeAsync(() => unrelated.CloseAsync());
            await unrelatedTask;
        }
    }

    [Fact]
    public async Task Removing_page_cancels_owned_overlay_and_preserves_unrelated_dialog() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        fixture.Harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/agents");
        var parent = fixture.Harness.Context.Render<DynamicComponent>(p => p.Add(c => c.Type, typeof(AgentsHomePage)));
        var page = parent.FindComponent<AgentsHomePage>();
        page.WaitForDashboardLoaded();
        var host = fixture.Harness.Context.Render<DialogHost>();
        var dialogs = fixture.Harness.Context.Services.GetRequiredService<DialogService>();
        var unrelatedTask = dialogs.OpenAsync("Independent overlay", _ => builder => builder.AddContent(0, "Unrelated work"));
        var unrelated = Assert.Single(dialogs.Dialogs);
        var pending = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => pending.Task;
        var opening = page.InvokeAsync(() => page.Find("[data-testid='agents-overview-open-agent-usage']").ClickAsync());
        host.WaitForAssertion(() => Assert.Equal(2, fixture.Agents.Reads));
        try {
            parent.Render(p => p.Add(c => c.Type, typeof(EmptyState)));
            host.WaitForAssertion(() => Assert.Same(unrelated, Assert.Single(dialogs.Dialogs)));
            Assert.True(fixture.Agents.LastToken.IsCancellationRequested);
        } finally {
            await host.InvokeAsync(() => pending.TrySetResult(fixture.Agents.Result("After page removal")));
            await host.InvokeAsync(() => {
                foreach (var dialog in dialogs.Dialogs.ToArray()) {
                    dialogs.Close(dialog);
                }
            });
            await opening;
            await unrelatedTask;
        }
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer)]
    [InlineData(OverviewDialogCase.Provider)]
    [InlineData(OverviewDialogCase.Model)]
    public async Task Usage_dialog_rejects_snapshot_for_a_different_selection(OverviewDialogCase kind) {
        await using var fixture = await OverviewPageFixture.CreateAsync(services => {
            services.RemoveAll<IAgentsWorkspaceQuery>();
            services.AddSingleton<IAgentsWorkspaceQuery>(new WrongScopeQuery());
        });
        var cut = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        cut.WaitForAssertion(() => Assert.Contains("could not be loaded", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='" + ContentId(kind) + "']"));
        Assert.Empty(cut.FindComponents<CdaChart>());
    }

    [Theory]
    [InlineData(OverviewDialogCase.Consumer)]
    [InlineData(OverviewDialogCase.Provider)]
    [InlineData(OverviewDialogCase.Model)]
    public async Task Old_dialog_finally_cannot_clear_newer_request_loading(OverviewDialogCase kind) {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var a = new TaskCompletionSource<ProviderUsageSourceResult>();
        var b = new TaskCompletionSource<ProviderUsageSourceResult>();
        fixture.Agents.Read = _ => a.Task;
        fixture.Chats.Read = _ => b.Task;
        var cut = Render(fixture, kind, ProviderUsageWorkloadSelection.Agents);
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.Agents.Reads));
        cut.Render(p => p.Add(c => c.Type, ComponentType(kind)).Add(c => c.Parameters, DynamicParameters(ProviderUsageWorkloadSelection.SimpleChats)));
        cut.WaitForAssertion(() => Assert.Equal(1, fixture.Chats.Reads));
        try {
            await cut.InvokeAsync(() => a.TrySetResult(fixture.Agents.Result("Old completion")));
            Assert.Empty(cut.FindAll("[data-testid='" + ContentId(kind) + "']"));
            Assert.Empty(cut.FindComponents<CdaChart>());
        } finally {
            await cut.InvokeAsync(() => b.TrySetResult(fixture.Chats.Result("Current completion")));
        }
        cut.WaitForElement("[data-testid='" + ContentId(kind) + "']");
    }

    [Fact]
    public async Task One_real_team_click_performs_one_navigation_to_its_identity() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var workspace = fixture.Harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await workspace.SaveAgentTeamAsync(new AgentTeamEditorModel { Name = "Overview navigation team" });
        fixture.Overview = _ => Task.FromResult(OverviewPageFixture.Snapshot() with {
            TeamShortcuts = [new(id, "Overview navigation team", "", "groups", 0)]
        });
        var page = fixture.Render();
        page.WaitForDashboardLoaded();
        var navigation = fixture.Harness.Context.Services.GetRequiredService<NavigationManager>();
        var locations = new List<string>();
        navigation.LocationChanged += (_, args) => locations.Add(args.Location);
        await page.InvokeAsync(() => page.Find("[data-testid='agents-overview-team-shortcut']").ClickAsync());
        page.WaitForAssertion(() => Assert.Contains("teamId=" + id.ToString("D"), navigation.Uri));
        Assert.Contains("tab=agents", Assert.Single(locations));
        Assert.NotNull(page.Find("[data-testid='agents-hr-agent-open-header']"));
    }

    [Fact]
    public async Task Pending_detail_disables_its_action_and_repeated_intent_does_not_open_twice() {
        await using var fixture = await OverviewPageFixture.CreateAsync();
        var page = fixture.Render();
        page.WaitForDashboardLoaded();
        var host = fixture.Harness.Context.Render<DialogHost>();
        var dialogs = fixture.Harness.Context.Services.GetRequiredService<DialogService>();
        var opening = page.InvokeAsync(() => page.Find("[data-testid='agents-overview-open-provider-usage']").ClickAsync());
        host.WaitForElement("[data-testid='provider-usage-dialog']");
        try {
            Assert.True(page.Find("[data-testid='agents-overview-open-provider-usage']").HasAttribute("disabled"));
            await page.InvokeAsync(() => page.FindComponent<AgentsOverviewSurface>().Instance.Intent.InvokeAsync(
                new AgentsOverviewIntent.OpenDetail(AgentsOverviewDetail.Providers, ProviderUsageWorkloadSelection.Both)));
            Assert.Single(dialogs.Dialogs);
        } finally {
            await host.InvokeAsync(() => Assert.Single(dialogs.Dialogs).CloseAsync());
            await opening;
        }
        page.WaitForAssertion(() => Assert.False(page.Find("[data-testid='agents-overview-open-provider-usage']").HasAttribute("disabled")));
    }

    private sealed class WrongScopeQuery : IAgentsWorkspaceQuery {
        public Task<AgentsHeaderSnapshot> ReadHeaderAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<AgentOverviewSnapshot> ReadOverviewAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public ValueTask<ProviderUsageSnapshot> ReadUsageAsync(ProviderUsageWorkloadSelection selection, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.SimpleChats));
    }

    private static IRenderedComponent<DynamicComponent> Render(OverviewPageFixture fixture, OverviewDialogCase kind, ProviderUsageWorkloadSelection selection)
        => fixture.Harness.Context.Render<DynamicComponent>(p => p.Add(c => c.Type, ComponentType(kind)).Add(c => c.Parameters, DynamicParameters(selection)));

    private static Dictionary<string, object> DynamicParameters(ProviderUsageWorkloadSelection selection)
        => new() { [nameof(AgentUsageDialog.Selection)] = selection };

    private static Dictionary<string, object?> Parameters(ProviderUsageWorkloadSelection selection)
        => new() { [nameof(AgentUsageDialog.Selection)] = selection };

    private static Type ComponentType(OverviewDialogCase kind) => kind switch {
        OverviewDialogCase.Consumer => typeof(AgentUsageDialog),
        OverviewDialogCase.Provider => typeof(ProviderUsageDialog),
        OverviewDialogCase.Model => typeof(ModelUsageDialog),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string ContentId(OverviewDialogCase kind) => kind switch {
        OverviewDialogCase.Consumer => "agents-usage-dialog",
        OverviewDialogCase.Provider => "provider-usage-dialog",
        OverviewDialogCase.Model => "model-usage-dialog",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void Complete(TaskCompletionSource<ProviderUsageSourceResult> pending, OverviewPageFixture fixture, bool fail) {
        if (fail) {
            pending.TrySetException(new IOException(OverviewPageFixture.PrivateFailure));
        } else {
            pending.TrySetResult(fixture.Agents.Result("Late old request"));
        }
    }
}
