using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Teams;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class TeamAuthoringTests {
    [Fact]
    public async Task Held_save_freezes_raw_fields_and_does_not_discard_later_edits_or_repeat_enter() {
        using var context = Context();
        var held = new TaskCompletionSource<TeamMetadataOutcome>();
        TeamMetadataSubmission? sent = null;
        var calls = 0;
        var completed = new List<Guid>();
        var operations = Operations(save: (request, _) => {
            calls++;
            sent = request;
            return held.Task;
        });
        var cut = context.Render<TeamMetadataEditor>(p => p.Add(c => c.Operations, operations).Add(c => c.Completed, completed.Add));
        cut.Find("input").Input("Submitted team");
        cut.Find("textarea").Input("Submitted description");
        var save = cut.Find("form").SubmitAsync();
        cut.Find("input").Input("Later name");
        cut.Find("textarea").Input("Later description");
        await cut.Find("form").SubmitAsync();
        Assert.Equal(1, calls);
        Assert.Equal("Submitted team", sent!.Name);
        Assert.Equal("Submitted description", sent.Description);
        Assert.Null(sent.Id);
        var id = Guid.NewGuid();
        held.SetResult(new TeamMetadataOutcome.Accepted(id));
        await save;
        Assert.Empty(completed);
        Assert.Equal("Later name", cut.Find("input").GetAttribute("value"));
        Assert.Contains("Later draft edits remain unsaved", cut.Markup, StringComparison.Ordinal);
        await cut.Find("[data-testid='agents-team-use-saved']").ClickAsync();
        Assert.Equal([id], completed);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_wrong_identity_acknowledgement_prevents_blind_retry(bool wrongIdentity) {
        using var context = Context();
        var id = Guid.NewGuid();
        var calls = 0;
        var operations = Operations(save: (_, _) => {
            calls++;
            return Task.FromResult<TeamMetadataOutcome>(wrongIdentity ? new TeamMetadataOutcome.Accepted(Guid.NewGuid()) : new TeamMetadataOutcome.Unknown());
        });
        var cut = context.Render<TeamMetadataEditor>(p => p.Add(c => c.TeamId, id).Add(c => c.Operations, operations));
        await cut.Find("form").SubmitAsync();
        await cut.Find("form").SubmitAsync();
        Assert.Equal(1, calls);
        Assert.Contains("outcome is unknown", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmed_create_parent_failure_retries_completion_without_second_write() {
        using var context = Context();
        var writes = 0;
        var completions = 0;
        var id = Guid.NewGuid();
        var cut = context.Render<TeamMetadataEditor>(p => p
            .Add(c => c.Operations, Operations(save: (_, _) => {
                writes++;
                return Task.FromResult<TeamMetadataOutcome>(new TeamMetadataOutcome.Accepted(id));
            }))
            .Add(c => c.Completed, accepted => {
                Assert.Equal(id, accepted);
                if (++completions == 1) {
                    throw new IOException("PRIVATE_CANARY");
                }
            }));
        await cut.Find("form").SubmitAsync();
        Assert.Contains("team was saved", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_CANARY", cut.Markup, StringComparison.Ordinal);
        await cut.Find("[data-testid='agents-team-use-saved']").ClickAsync();
        Assert.Equal(1, writes);
        Assert.Equal(2, completions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_icon_cannot_change_replacement_or_later_raw_edit(bool replace) {
        using var context = Context();
        var held = new TaskCompletionSource<string?>();
        var operations = Operations(icon: (_, _) => held.Task);
        var id = Guid.NewGuid();
        var cut = context.Render<TeamMetadataEditor>(p => p.Add(c => c.TeamId, id).Add(c => c.Operations, operations));
        var pending = cut.Find("[data-testid='agents-team-choose-icon']").ClickAsync();
        if (replace) {
            cut.Render(p => p.Add(c => c.TeamId, Guid.NewGuid()));
            cut.Render(p => p.Add(c => c.TeamId, id));
        } else {
            cut.Find("input").Input("Later edit");
        }
        held.SetResult("hub");
        await pending;
        Assert.DoesNotContain("hub", cut.Find("[data-testid='agents-team-selected-icon']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Late_save_after_cancel_is_silent_and_does_not_close_another_editor() {
        using var context = Context();
        var held = new TaskCompletionSource<TeamMetadataOutcome>();
        var completed = 0;
        var cancelled = 0;
        var cut = context.Render<TeamMetadataEditor>(p => p.Add(c => c.Operations, Operations(save: (_, _) => held.Task))
            .Add(c => c.Completed, _ => completed++).Add(c => c.Cancelled, () => cancelled++));
        var save = cut.Find("form").SubmitAsync();
        await cut.FindComponents<Button>().Single(b => b.Instance.Text == "Cancel").Find("button").ClickAsync();
        held.SetResult(new TeamMetadataOutcome.Accepted(Guid.NewGuid()));
        await save;
        Assert.Equal(1, cancelled);
        Assert.Equal(0, completed);
        Assert.Empty(context.Services.GetRequiredService<NotificationService>().Messages);
    }

    [Fact]
    public async Task Held_read_A_B_A_cannot_replace_newer_draft_and_token_remains_valid_until_unwind() {
        using var context = Context();
        var id = Guid.NewGuid();
        var held = new TaskCompletionSource<AgentTeamEditorModel>();
        CancellationToken token = default;
        var loads = 0;
        var operations = Operations(load: (requested, ct) => {
            if (++loads == 1) {
                token = ct;
                return held.Task;
            }
            return Task.FromResult(new AgentTeamEditorModel { Id = requested, Name = "Current" });
        });
        var cut = context.Render<TeamMetadataEditor>(p => p.Add(c => c.TeamId, id).Add(c => c.Operations, operations));
        cut.Render(p => p.Add(c => c.TeamId, Guid.NewGuid()));
        cut.Render(p => p.Add(c => c.TeamId, id));
        Assert.True(token.IsCancellationRequested);
        Assert.True(token.WaitHandle.WaitOne(0));
        cut.Find("input").Input("Retained current draft");
        held.SetResult(new() { Id = id, Name = "Stale" });
        cut.WaitForAssertion(() => Assert.Throws<ObjectDisposedException>(() => token.WaitHandle));
        Assert.Equal("Retained current draft", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Same_identity_rerender_and_second_editor_keep_separate_drafts() {
        using var context = Context();
        var operations = Operations();
        var id = Guid.NewGuid();
        var first = context.Render<TeamMetadataEditor>(p => p.Add(c => c.TeamId, id).Add(c => c.Operations, operations));
        var second = context.Render<TeamMetadataEditor>(p => p.Add(c => c.TeamId, id).Add(c => c.Operations, operations));
        first.Find("input").Input("Only first");
        first.Render(p => p.Add(c => c.TeamId, id));
        Assert.Equal("Only first", first.Find("input").GetAttribute("value"));
        Assert.Equal("Loaded team", second.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task Icon_selection_survives_search_and_same_parameter_rerender() {
        using var context = Context();
        string? result = null;
        var cut = context.Render<TeamIconPicker>(p => p.Add(c => c.SelectedIcon, "groups").Add(c => c.Completed, icon => result = icon));
        cut.Find("input").Input("engineering");
        await cut.Find("[data-testid='material-icon-picker-option']").ClickAsync();
        cut.Render(p => p.Add(c => c.SelectedIcon, "groups"));
        await cut.Find("[data-testid='material-icon-picker-confirm']").ClickAsync();
        Assert.Equal("engineering", result);
    }

    [Fact]
    public async Task Members_preserve_unavailable_reference_and_private_badge_across_filtering() {
        using var context = Context();
        var fixture = new TeamAuthoringScenario(TeamAuthoringState.MissingReference, (_, _) => Task.FromResult<string?>(null));
        var team = fixture.Team!;
        TeamMembershipSelection? result = null;
        var cut = context.Render<TeamMembersSelector>(p => p.Add(c => c.Team, team).Add(c => c.Agents, fixture.Agents)
            .Add(c => c.PrivateAgentIds, new[] { fixture.Agents[0].Id }).Add(c => c.Completed, selection => result = selection));
        Assert.NotEmpty(cut.FindAll("[data-testid='agents-team-unavailable-member']"));
        Assert.Contains("Private", cut.Markup, StringComparison.OrdinalIgnoreCase);
        cut.Find("input").Input("NO_MATCH_FOR_CA1");
        Assert.Empty(cut.FindAll("[data-testid='agents-team-member-card']"));
        cut.Find("input").Input(fixture.Agents[0].Name);
        await cut.Find("[data-testid='agents-team-member-card']").ClickAsync();
        cut.Render(p => p.Add(c => c.Team, team with { AgentIds = [] }));
        await cut.Find("[data-testid='agents-team-members-confirm']").ClickAsync();
        Assert.Equal(team.Id, result!.TeamId);
        Assert.Contains(team.AgentIds[0], result.AgentIds);
        Assert.Contains(fixture.Agents[0].Id, result.AgentIds);
    }

    [Fact]
    public async Task Cancelled_membership_token_prevents_late_confirmation() {
        using var context = Context();
        using var lifetime = new CancellationTokenSource();
        var fixture = new TeamAuthoringScenario(TeamAuthoringState.Existing, (_, _) => Task.FromResult<string?>(null));
        var calls = 0;
        var cut = context.Render<TeamMembersSelector>(p => p.Add(c => c.Team, fixture.Team!).Add(c => c.Agents, fixture.Agents)
            .Add(c => c.OwnerCancellationToken, lifetime.Token).Add(c => c.Completed, _ => calls++));
        lifetime.Cancel();
        await cut.Find("[data-testid='agents-team-members-confirm']").ClickAsync();
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(TeamAuthoringState.New)]
    [InlineData(TeamAuthoringState.Existing)]
    [InlineData(TeamAuthoringState.Empty)]
    [InlineData(TeamAuthoringState.MissingReference)]
    [InlineData(TeamAuthoringState.Large)]
    [InlineData(TeamAuthoringState.LoadFailure)]
    [InlineData(TeamAuthoringState.Rejected)]
    [InlineData(TeamAuthoringState.Unknown)]
    [InlineData(TeamAuthoringState.HeldLoad)]
    [InlineData(TeamAuthoringState.HeldSave)]
    [InlineData(TeamAuthoringState.Deleted)]
    public void Independent_scenario_host_renders_real_team_family(TeamAuthoringState state) {
        using var context = Context();
        var cut = context.Render<CanDoItAll.AgentFramework.UiSandbox.Components.TeamAuthoring>(p => p.Add(c => c.Scenario, state.ToString()));
        Assert.Single(cut.FindComponents<TeamMetadataEditor>());
        Assert.Contains("Simulated", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static TeamMetadataOperations Operations(
        Func<Guid?, CancellationToken, Task<AgentTeamEditorModel>>? load = null,
        Func<TeamMetadataSubmission, CancellationToken, Task<TeamMetadataOutcome>>? save = null,
        Func<string, CancellationToken, Task<string?>>? icon = null) => new(
            load ?? ((id, _) => Task.FromResult(new AgentTeamEditorModel { Id = id, Name = "Loaded team", Icon = "engineering" })),
            save ?? ((request, _) => Task.FromResult<TeamMetadataOutcome>(new TeamMetadataOutcome.Accepted(request.Id ?? Guid.NewGuid()))),
            icon ?? ((_, _) => Task.FromResult<string?>(null)));
}
