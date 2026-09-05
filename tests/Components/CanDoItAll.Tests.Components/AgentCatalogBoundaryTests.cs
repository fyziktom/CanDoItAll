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
    public async Task Real_operations_respect_initial_data_and_repair_then_reload() {
        var repair = new RecordingRepair();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IAgentFrameworkOrganizationCatalogRepairService>(repair));
        var operations = Assert.IsType<AgentCatalogOperations>(harness.Context.Services.GetRequiredService<IAgentCatalogOperations>());
        var empty = await operations.LoadAsync(new(Repair: true, Agents: [], Providers: [], Teams: []));
        Assert.Equal(1, repair.Reads);
        Assert.Empty(empty.Agents);
        Assert.Empty(empty.Teams);
        Assert.Empty(empty.PrivateProviderById);
        var loaded = await operations.LoadAsync(new(Repair: false));
        Assert.Equal(1, repair.Reads);
        Assert.NotEmpty(loaded.Agents);
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        Assert.Equal((await workspace.ListAgentsAsync(false)).Select(agent => agent.Id), loaded.Agents.Select(agent => agent.Id));
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

    [Fact]
    public async Task Real_operations_update_and_delete_only_the_selected_team() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(false)).First();
        var target = await workspace.SaveAgentTeamAsync(new AgentTeamEditorModel { Name = "Catalog seam target" });
        var other = await workspace.SaveAgentTeamAsync(new AgentTeamEditorModel { Name = "Catalog seam survivor" });
        var operations = harness.Context.Services.GetRequiredService<IAgentCatalogOperations>();
        await operations.UpdateMembersAsync(target, [agent.Id]);
        Assert.Equal([agent.Id], (await workspace.ListAgentTeamsAsync()).Single(team => team.Id == target).AgentIds);
        await operations.DeleteTeamAsync(target);
        var teams = await workspace.ListAgentTeamsAsync();
        Assert.DoesNotContain(teams, team => team.Id == target);
        Assert.Contains(teams, team => team.Id == other);
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

    private sealed class RecordingRepair : IAgentFrameworkOrganizationCatalogRepairService {
        public int Reads { get; private set; }
        public Task EnsureCurrentOrganizationCatalogAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCatalogOperations : IAgentCatalogOperations {
        public AgentCatalogSnapshot Snapshot { get; set; } = AgentCatalogSnapshot.Empty;
        public Task<AgentCatalogSnapshot>? NextLoad { get; set; }
        public List<Guid> DeletedTeams { get; } = [];
        public int Loads { get; private set; }
        public int MemberWrites { get; private set; }
        public Task<AgentCatalogSnapshot> LoadAsync(AgentCatalogLoadRequest request, CancellationToken cancellationToken = default) {
            Loads++;
            return NextLoad ?? Task.FromResult(Snapshot);
        }
        public Task DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default) {
            DeletedTeams.Add(teamId);
            Snapshot = Snapshot with { Teams = Snapshot.Teams.Where(team => team.Id != teamId).ToArray() };
            return Task.CompletedTask;
        }
        public Task UpdateMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default) {
            MemberWrites++;
            Snapshot = Snapshot with { Teams = Snapshot.Teams.Select(team => team.Id == teamId ? team with { AgentIds = agentIds } : team).ToArray() };
            return Task.CompletedTask;
        }
    }
}
