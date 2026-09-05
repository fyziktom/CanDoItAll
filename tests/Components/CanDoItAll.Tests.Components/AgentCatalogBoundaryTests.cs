using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCatalogBoundaryTests {
    [Fact]
    public void Controlled_panel_search_and_selection_emit_intents_without_services() {
        var first = AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Catalog first", "");
        var agents = new[] { first, AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Catalog second", "") };
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var intents = new List<AgentCatalogIntent>();
        var cut = context.Render<AgentCatalogPanel>(parameters => parameters
            .Add(component => component.Snapshot, new AgentCatalogSnapshot(agents, [], new Dictionary<Guid, bool>()))
            .Add(component => component.Selection, new AgentCatalogSelection(null, null))
            .Add(component => component.Intent, EventCallback.Factory.Create<AgentCatalogIntent>(this, intents.Add)));

        cut.Find("[data-testid='agents-catalog-search']").Input(first.Name);
        Assert.All(cut.FindAll("[data-testid='agents-catalog-card-shell']"), card => Assert.Contains(first.Name, card.TextContent));
        cut.Find("[data-testid='agents-catalog-card']").Click();
        Assert.Equal(new AgentCatalogIntent.SelectAgent(first.Id), Assert.Single(intents));
        Assert.Null(cut.Instance.Selection.AgentId);
        cut.Find("[data-testid='agents-catalog-reset']").Click();
        Assert.Equal(agents.Length, cut.FindAll("[data-testid='agents-catalog-card']").Count);
        cut.Find("[data-testid='agents-catalog-new']").Click();
        Assert.Equal(new AgentCatalogIntent.OpenAgent(null), intents.Last());
        Assert.Empty(context.Services.GetRequiredService<DialogService>().Dialogs);
    }

    [Fact]
    public async Task Requested_agent_opens_once_and_changed_request_opens_again() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agents = (await workspace.ListAgentsAsync(false)).Take(2).ToArray();
        var dialogs = harness.Context.Services.GetRequiredService<DialogService>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.InitialAgents, agents)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.InitialTeams, Array.Empty<AgentTeamDefinition>())
            .Add(component => component.SkipCatalogRepair, true)
            .Add(component => component.RequestedAgentId, agents[0].Id));
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        Assert.Equal(agents[0].Id, dialogs.Dialogs[0].Parameters[nameof(AgentDetailsDialog.AgentId)]);
        await cut.InvokeAsync(() => dialogs.CloseAsync());
        cut.Render(parameters => parameters.Add(component => component.RequestedAgentId, agents[0].Id));
        Assert.Empty(dialogs.Dialogs);
        cut.Render(parameters => parameters.Add(component => component.RequestedAgentId, Guid.NewGuid()));
        Assert.Empty(dialogs.Dialogs);
        cut.Render(parameters => parameters.Add(component => component.RequestedAgentId, agents[1].Id));
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        Assert.Equal(agents[1].Id, dialogs.Dialogs[0].Parameters[nameof(AgentDetailsDialog.AgentId)]);
        await cut.InvokeAsync(() => dialogs.CloseAsync());
    }


    [Fact]
    public async Task Team_delete_preserves_no_confirmation_and_clears_selection() {
        var operations = new RecordingCatalogOperations();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var agent = (await harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false)).First();
        var team = new AgentTeamDefinition(Guid.NewGuid(), "Seam team", "", [agent.Id], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        operations.Snapshot = new([agent], [team], new Dictionary<Guid, bool>());
        var selected = new List<AgentTeamDefinition?>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.SkipCatalogRepair, true)
            .Add(component => component.RequestedTeamId, team.Id)
            .Add(component => component.SelectedTeamChanged, EventCallback.Factory.Create<AgentTeamDefinition?>(this, selected.Add)));
        cut.WaitForElement("[data-testid='agents-team-delete']").Click();
        cut.WaitForAssertion(() => Assert.Equal(team.Id, Assert.Single(operations.DeletedTeams)));
        cut.WaitForAssertion(() => Assert.Null(selected.Last()));
        Assert.Empty(harness.Context.Services.GetRequiredService<DialogService>().Dialogs);
        Assert.Equal(2, operations.Loads);
        Assert.Null(cut.FindComponent<AgentCatalogPanel>().Instance.Selection.TeamId);
        Assert.DoesNotContain(team.Name, cut.Find("[data-testid='agents-team-panel']").TextContent);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Team_dialog_results_refresh_once_and_cancel_has_no_write(bool members) {
        var operations = new RecordingCatalogOperations();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var agent = AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Team member", "");
        var team = new AgentTeamDefinition(Guid.NewGuid(), "Team result", "", [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        operations.Snapshot = new([agent], [team], new Dictionary<Guid, bool>());
        var selected = new List<AgentTeamDefinition?>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.SkipCatalogRepair, true)
            .Add(component => component.RequestedTeamId, team.Id)
            .Add(component => component.SelectedTeamChanged, EventCallback.Factory.Create<AgentTeamDefinition?>(this, selected.Add)));
        var dialogs = harness.Context.Services.GetRequiredService<DialogService>();
        var selector = members ? "[data-testid='agents-team-members']" : "[data-testid='agents-team-edit']";
        cut.Find(selector).Click();
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        Assert.Equal(members ? typeof(AgentTeamMembersDialog) : typeof(AgentTeamDetailsDialog), dialogs.Dialogs[0].ComponentType);
        await cut.InvokeAsync(() => dialogs.CloseAsync());
        Assert.Equal(1, operations.Loads);
        Assert.Equal(0, operations.MemberWrites);
        cut.Find(selector).Click();
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        object result = members ? new AgentTeamMembersDialogResult(team.Id, [agent.Id]) : new AgentTeamDetailsDialogResult(team.Id);
        await cut.InvokeAsync(() => dialogs.CloseAsync(result));
        cut.WaitForAssertion(() => Assert.Equal(2, operations.Loads));
        Assert.Equal(members ? 1 : 0, operations.MemberWrites);
        Assert.Equal(team.Id, selected.Last()?.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_catalog_refresh_publishes_selection_only_for_the_current_editor_target(bool clearDuringRefresh) {
        var operations = new RecordingCatalogOperations();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var original = (await workspace.ListAgentsAsync(false)).First();
        operations.Snapshot = new([original], [], new Dictionary<Guid, bool>());
        var dialogHost = harness.Context.Render<DialogHost>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.SkipCatalogRepair, true));
        await cut.WaitForElement("[data-testid='agents-catalog-card']").ClickAsync();
        await cut.Find("[data-testid='agents-catalog-new']").ClickAsync();
        var editor = dialogHost.WaitForComponent<AgentDetailsDialog>();
        editor.WaitForElement("[data-testid='agents-catalog-name']").Change("Delayed catalog publication");
        var pending = new TaskCompletionSource<AgentCatalogSnapshot>();
        operations.NextLoad = pending.Task;
        var submitted = editor.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => Assert.Equal(2, operations.Loads), TimeSpan.FromSeconds(10));
        var savedId = editor.Instance.CurrentTarget.AgentId;
        Assert.NotNull(savedId);
        if (clearDuringRefresh) {
            await editor.FindComponent<StickyActionFooter>().FindAll("button")
                .Single(button => button.TextContent.Trim() == "Clear").ClickAsync();
            Assert.True(editor.Instance.CurrentTarget.IsNew);
        }
        var refreshed = new AgentCatalogSnapshot(await workspace.ListAgentsAsync(false), [], new Dictionary<Guid, bool>());
        await cut.InvokeAsync(() => pending.SetResult(refreshed));
        await submitted;
        Assert.Equal(clearDuringRefresh ? original.Id : savedId, cut.FindComponent<AgentCatalogPanel>().Instance.Selection.AgentId);
        Assert.Single(refreshed.Agents, agent => agent.Id == savedId);
        await harness.Context.Services.GetRequiredService<DialogService>().CloseAsync();
    }

    [Fact]
    public async Task First_save_and_update_keep_one_editor_through_real_page_selection_echo() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents?tab=agents");
        var dialogHost = harness.Context.Render<DialogHost>();
        var page = harness.Context.Render<CanDoItAll.Modules.AgentFramework.Pages.AgentsHomePage>();
        page.WaitForDashboardLoaded();
        await page.WaitForElement("[data-testid='agents-catalog-new']").ClickAsync();
        var editor = dialogHost.WaitForComponent<AgentDetailsDialog>();
        editor.WaitForElement("[data-testid='agents-catalog-name']").Change("Page save echo proof");
        await editor.Find("form").SubmitAsync();
        var savedId = editor.Instance.CurrentTarget.AgentId;
        Assert.NotNull(savedId);
        page.WaitForAssertion(() => Assert.Equal(savedId,
            page.FindComponent<AgentCatalogPanel>().Instance.Selection.AgentId));
        Assert.Single(harness.Context.Services.GetRequiredService<DialogService>().Dialogs);
        Assert.Same(editor.Instance, dialogHost.FindComponent<AgentDetailsDialog>().Instance);
        editor.Find("[data-testid='agents-catalog-name']").Change("Page save echo updated");
        await editor.Find("form").SubmitAsync();
        Assert.Equal(savedId, editor.Instance.CurrentTarget.AgentId);
        Assert.Single(harness.Context.Services.GetRequiredService<DialogService>().Dialogs);
        Assert.Equal("Page save echo updated", (await workspace.GetAgentEditorAsync(savedId)).Name);
        await harness.Context.Services.GetRequiredService<DialogService>().CloseAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Requested_agent_can_be_cleared_and_same_deep_link_reopens(bool invalidRequest) {
        var agent = AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Reopened agent", "");
        var operations = new RecordingCatalogOperations {
            Snapshot = new([agent], [], new Dictionary<Guid, bool>())
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var selected = new List<AgentDefinition?>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.RequestedAgentId, agent.Id)
            .Add(component => component.SelectedAgentChanged,
                EventCallback.Factory.Create<AgentDefinition?>(this, selected.Add)));
        var dialogs = harness.Context.Services.GetRequiredService<DialogService>();
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        await cut.InvokeAsync(() => dialogs.CloseAsync());
        cut.Render(parameters => parameters.Add(component => component.RequestedAgentId,
            invalidRequest ? Guid.NewGuid() : (Guid?)null));
        Assert.Null(cut.FindComponent<AgentCatalogPanel>().Instance.Selection.AgentId);
        Assert.Null(selected.Last());
        Assert.Empty(dialogs.Dialogs);
        cut.Render(parameters => parameters.Add(component => component.RequestedAgentId, agent.Id));
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        Assert.Equal(agent.Id, dialogs.Dialogs[0].Parameters[nameof(AgentDetailsDialog.AgentId)]);
        await cut.InvokeAsync(() => dialogs.CloseAsync());
        cut.Render(parameters => parameters.Add(component => component.RequestedAgentId, agent.Id));
        Assert.Empty(dialogs.Dialogs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposed_catalog_cancels_load_and_ignores_late_success_or_failure(bool fail) {
        var pending = new TaskCompletionSource<AgentCatalogSnapshot>();
        var operations = new RecordingCatalogOperations { NextLoad = pending.Task };
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var states = new List<AgentChatContextAccessState>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.ContextAccessStateChanged,
                EventCallback.Factory.Create<AgentChatContextAccessState>(this, states.Add)));
        cut.WaitForAssertion(() => Assert.Equal(1, operations.Loads));
        var count = states.Count;
        await cut.InvokeAsync(() => {
            cut.Instance.Dispose();
            if (fail) {
                pending.SetException(new IOException("Late catalog error."));
            } else {
                pending.SetResult(new([AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Late agent", "")],
                    [], new Dictionary<Guid, bool>()));
            }
        });
        await cut.InvokeAsync(() => Task.CompletedTask);
        Assert.True(operations.LastToken.IsCancellationRequested);
        Assert.Equal(count, states.Count);
        Assert.Empty(cut.FindComponent<AgentCatalogPanel>().Instance.Snapshot.Agents);
        Assert.Empty(harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposed_catalog_ignores_team_dialog_results(bool members) {
        var team = new AgentTeamDefinition(Guid.NewGuid(), "Disposed team", "", [],
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var operations = new RecordingCatalogOperations {
            Snapshot = new([AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Available agent", "")], [team], new Dictionary<Guid, bool>())
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var selected = new List<AgentTeamDefinition?>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.RequestedTeamId, team.Id)
            .Add(component => component.SelectedTeamChanged,
                EventCallback.Factory.Create<AgentTeamDefinition?>(this, selected.Add)));
        cut.Find(members ? "[data-testid='agents-team-members']" : "[data-testid='agents-team-edit']").Click();
        var dialogs = harness.Context.Services.GetRequiredService<DialogService>();
        cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
        var pendingDialog = dialogs.Dialogs[0];
        await cut.InvokeAsync(() => cut.Instance.Dispose());
        Assert.Empty(dialogs.Dialogs);
        Assert.True(pendingDialog.Result.IsCanceled);
        object result = members ? new AgentTeamMembersDialogResult(team.Id, []) : new AgentTeamDetailsDialogResult(team.Id);
        await cut.InvokeAsync(() => dialogs.CloseAsync(result));
        Assert.Equal(1, operations.Loads);
        Assert.Equal(0, operations.MemberWrites);
        Assert.Empty(selected);
        Assert.Empty(harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_cancels_inflight_team_mutations_without_refresh_or_notification(bool members) {
        var team = new AgentTeamDefinition(Guid.NewGuid(), "Pending mutation", "", [],
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var pending = new TaskCompletionSource();
        var operations = new RecordingCatalogOperations {
            Snapshot = new([AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Available agent", "")], [team], new Dictionary<Guid, bool>()),
            NextMutation = pending.Task
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var selected = new List<AgentTeamDefinition?>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.RequestedTeamId, team.Id)
            .Add(component => component.SelectedTeamChanged,
                EventCallback.Factory.Create<AgentTeamDefinition?>(this, selected.Add)));
        Task action;
        if (members) {
            await cut.Find("[data-testid='agents-team-members']").ClickAsync();
            var dialogs = harness.Context.Services.GetRequiredService<DialogService>();
            cut.WaitForAssertion(() => Assert.Single(dialogs.Dialogs));
            action = cut.InvokeAsync(() => dialogs.CloseAsync(new AgentTeamMembersDialogResult(team.Id, [])));
        } else {
            action = cut.Find("[data-testid='agents-team-delete']").ClickAsync();
        }
        await operations.MutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, operations.MemberWrites + operations.DeletedTeams.Count);
        await cut.InvokeAsync(() => {
            cut.Instance.Dispose();
            pending.SetResult();
        });
        await action;
        await cut.InvokeAsync(async () => await Task.Yield());
        Assert.True(operations.LastToken.IsCancellationRequested);
        Assert.Equal(1, operations.Loads);
        Assert.Empty(selected);
        Assert.Empty(harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_cancels_chat_launch_and_ignores_late_result(bool fail) {
        var agent = AgentCatalogPanelTests.CreateAgent(HrAgentIdentity.AgentId, "HR Agent", HrAgentIdentity.TemplateKey);
        var operations = new RecordingCatalogOperations {
            Snapshot = new([agent], [], new Dictionary<Guid, bool>())
        };
        var launcher = new PendingChatLauncher();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IAgentCatalogOperations>(operations);
            services.AddSingleton<IAgentChatLauncher>(launcher);
        });
        var cut = harness.Context.Render<AgentCatalogHost>();
        var started = cut.WaitForElement("[data-testid='agents-hr-agent-open']").ClickAsync();
        cut.WaitForAssertion(() => Assert.True(launcher.Token.CanBeCanceled));
        await cut.InvokeAsync(() => {
            cut.Instance.Dispose();
            if (fail) {
                launcher.Pending.SetException(new IOException("Late chat failure."));
            } else {
                launcher.Pending.SetResult(new(AgentChatHandleId.Create(), new AgentChatIdentity(agent.Id, agent.Name, agent.RoleTitle, agent.AvatarImageUrl), null,
                    ActiveAgentChatVisibility.Visible, ActiveAgentChatRunState.Idle, DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch, null));
            }
        });
        await started;
        Assert.True(launcher.Token.IsCancellationRequested);
        Assert.Empty(harness.Context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Fact]
    public async Task Team_dialog_shell_and_content_have_unique_test_ids() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var dialogHost = harness.Context.Render<DialogHost>();
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.SkipCatalogRepair, true));
        await cut.WaitForElement("[data-testid='agents-team-new']").ClickAsync();
        dialogHost.WaitForElement("[data-testid='agents-team-details-dialog-content']");
        Assert.Single(dialogHost.FindAll("[data-testid='agents-team-details-dialog-shell']"));
        Assert.Single(dialogHost.FindAll("[data-testid='agents-team-details-dialog-content']"));
        Assert.Empty(dialogHost.FindAll("[data-testid='agents-team-details-dialog']"));
        await harness.Context.Services.GetRequiredService<DialogService>().CloseAsync();
    }

    [Fact]
    public async Task Disposing_catalog_closes_only_its_editor_and_allows_same_target_in_replacement_host() {
        var agent = AgentCatalogPanelTests.CreateAgent(Guid.NewGuid(), "Lifetime target", "");
        var operations = new RecordingCatalogOperations {
            Snapshot = new([agent], [], new Dictionary<Guid, bool>())
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentCatalogOperations>(operations));
        var dialogs = harness.Context.Services.GetRequiredService<DialogService>();
        var unrelated = dialogs.OpenAsync<AgentTeamDetailsDialog>("Unrelated editor");
        var cut = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.RequestedAgentId, agent.Id));
        cut.WaitForAssertion(() => Assert.Equal(2, dialogs.Dialogs.Count));
        var owned = dialogs.Dialogs.Single(dialog => dialog.ComponentType == typeof(AgentDetailsDialog));

        await cut.InvokeAsync(() => cut.Instance.Dispose());

        Assert.True(owned.Result.IsCanceled);
        Assert.False(unrelated.IsCompleted);
        Assert.Equal(typeof(AgentTeamDetailsDialog), Assert.Single(dialogs.Dialogs).ComponentType);
        var replacement = harness.Context.Render<AgentCatalogHost>(parameters => parameters
            .Add(component => component.RequestedAgentId, agent.Id));
        replacement.WaitForAssertion(() => Assert.Single(dialogs.Dialogs.Where(dialog =>
            dialog.ComponentType == typeof(AgentDetailsDialog))));
        await replacement.InvokeAsync(() => replacement.Instance.Dispose());
        Assert.Equal(typeof(AgentTeamDetailsDialog), Assert.Single(dialogs.Dialogs).ComponentType);
        await dialogs.CloseAsync();
    }

    private sealed class PendingChatLauncher : IAgentChatLauncher {
        public TaskCompletionSource<ActiveAgentChat> Pending { get; } = new();
        public CancellationToken Token { get; private set; }
        public Task<ActiveAgentChat> StartNewChatAsync(Guid agentId, CancellationToken cancellationToken = default) {
            Token = cancellationToken;
            return Pending.Task;
        }
        public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents) => throw new NotSupportedException();
        public Task<ActiveAgentChat> OpenChatAsync(Guid agentId, Guid chatSessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingCatalogOperations : IAgentCatalogOperations {
        public AgentCatalogSnapshot Snapshot { get; set; } = AgentCatalogSnapshot.Empty;
        public Task<AgentCatalogSnapshot>? NextLoad { get; set; }
        public Task? NextMutation { get; set; }
        public TaskCompletionSource MutationStarted { get; } = new();
        public List<Guid> DeletedTeams { get; } = [];
        public int Loads { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public int MemberWrites { get; private set; }
        public Task<AgentCatalogSnapshot> LoadAsync(AgentCatalogLoadRequest request, CancellationToken cancellationToken = default) {
            LastToken = cancellationToken;
            Loads++;
            return NextLoad ?? Task.FromResult(Snapshot);
        }
        public Task DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default) {
            LastToken = cancellationToken;
            DeletedTeams.Add(teamId);
            MutationStarted.TrySetResult();
            Snapshot = Snapshot with { Teams = Snapshot.Teams.Where(team => team.Id != teamId).ToArray() };
            return NextMutation ?? Task.CompletedTask;
        }
        public Task UpdateMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default) {
            LastToken = cancellationToken;
            MemberWrites++;
            MutationStarted.TrySetResult();
            Snapshot = Snapshot with { Teams = Snapshot.Teams.Select(team => team.Id == teamId ? team with { AgentIds = agentIds } : team).ToArray() };
            return NextMutation ?? Task.CompletedTask;
        }
    }
}
