using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Teams;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ConcurrentTeamAuthoringTests {
    [Fact]
    public async Task Metadata_conflict_keeps_raw_draft_and_applies_current_original_without_saving() {
        using var context = Context();
        var id = Guid.NewGuid();
        var original = Model(id, "Original team", "Original description");
        var current = Model(id, "Peer team", "Original description", original.ExpectedMetadata!.CreatedAtUtc);
        var reads = 0;
        var writes = new List<TeamMetadataSubmission>();
        var operations = new TeamMetadataOperations((_, _) => Task.FromResult(++reads == 1 ? original : current),
            (request, _) => {
                writes.Add(request);
                return Task.FromResult<TeamMetadataOutcome>(new TeamMetadataOutcome.Conflict());
            }, (_, _) => Task.FromResult<string?>(null));
        var cut = context.Render<TeamMetadataEditor>(parameters => parameters.Add(component => component.TeamId, id).Add(component => component.Operations, operations));
        cut.Find("[data-testid='agents-team-description']").Input("My raw description 東京");
        await cut.Find("form").SubmitAsync();
        Assert.Single(writes);
        Assert.True(cut.Find("[data-testid='agents-team-save']").HasAttribute("disabled"));
        await cut.Find("[data-testid='agents-team-compare']").ClickAsync();
        await cut.Find("[data-testid='agents-team-apply-comparison']").ClickAsync();
        Assert.Single(writes);
        Assert.Equal("Peer team", cut.Find("[data-testid='agents-team-name']").GetAttribute("value"));
        Assert.Equal("My raw description 東京", cut.Find("[data-testid='agents-team-description']").GetAttribute("value"));
        await cut.Find("form").SubmitAsync();
        Assert.Equal(current.ExpectedMetadata, writes[1].ExpectedMetadata);
        Assert.Equal("My raw description 東京", writes[1].Description);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_input_or_hint_blocks_an_obsolete_metadata_comparison(bool hint) {
        using var context = Context();
        var id = Guid.NewGuid();
        var original = Model(id, "Original", "Original description");
        var pending = new TaskCompletionSource<AgentTeamEditorModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var operations = new TeamMetadataOperations((_, _) => ++reads == 1 ? Task.FromResult(original) : pending.Task,
            (_, _) => Task.FromResult<TeamMetadataOutcome>(new TeamMetadataOutcome.Conflict()), (_, _) => Task.FromResult<string?>(null));
        var cut = context.Render<TeamMetadataEditor>(parameters => parameters.Add(component => component.TeamId, id).Add(component => component.Operations, operations));
        await cut.Find("form").SubmitAsync();
        var compare = cut.Find("[data-testid='agents-team-compare']").ClickAsync();
        if (hint) {
            cut.Render(parameters => parameters.Add(component => component.ChangeSequence, 1));
        } else {
            cut.Find("[data-testid='agents-team-name']").Input("Typed during comparison");
        }
        pending.SetResult(Model(id, "Peer", "Original description", original.ExpectedMetadata!.CreatedAtUtc));
        await compare;
        Assert.True(cut.Find("[data-testid='agents-team-apply-comparison']").HasAttribute("disabled"));
        Assert.Equal(hint ? "Original" : "Typed during comparison", cut.Find("[data-testid='agents-team-name']").GetAttribute("value"));
    }

    [Fact]
    public async Task Membership_comparison_keeps_peer_addition_and_local_removal_then_requires_another_save() {
        using var context = Context();
        var fixture = new TeamAuthoringScenario(TeamAuthoringState.Existing, (_, _) => Task.FromResult<string?>(null));
        var agents = fixture.Agents.Take(3).ToArray();
        Assert.Equal(3, agents.Length);
        var original = fixture.Team! with { AgentIds = [agents[0].Id] };
        var peer = original with { AgentIds = [agents[0].Id, agents[2].Id], UpdatedAtUtc = original.UpdatedAtUtc.AddSeconds(1) };
        var writes = new List<TeamMembershipCommand>();
        var reads = 0;
        var completed = new List<TeamMembershipSelection>();
        var operations = new TeamMembershipOperations((request, _) => {
            writes.Add(request);
            return Task.FromResult<TeamMembershipOutcome>(writes.Count == 1 ? new TeamMembershipOutcome.Conflict() : new TeamMembershipOutcome.Accepted());
        }, (_, _) => Task.FromResult(++reads == 1 ? original : peer));
        var cut = context.Render<TeamMembersSelector>(parameters => parameters.Add(component => component.Team, original)
            .Add(component => component.Agents, agents).Add(component => component.Operations, operations).Add(component => component.Completed, completed.Add));
        await SelectAsync(cut, agents[0].Id);
        await SelectAsync(cut, agents[1].Id);
        await cut.Find("[data-testid='agents-team-members-confirm']").ClickAsync();
        Assert.Empty(completed);
        Assert.Equal(new[] { agents[1].Id }, writes[0].AgentIds.ToArray());
        await cut.Find("[data-testid='agents-team-members-compare']").ClickAsync();
        await cut.Find("[data-testid='agents-team-members-apply']").ClickAsync();
        Assert.Single(writes);
        Assert.Empty(completed);
        await cut.Find("[data-testid='agents-team-members-confirm']").ClickAsync();
        Assert.Equal(peer.UpdatedAtUtc, writes[1].ExpectedUpdatedAtUtc);
        Assert.Equal(new[] { agents[1].Id, agents[2].Id }.Order(), writes[1].AgentIds.Order());
        Assert.Single(completed);
    }

    [Fact]
    public async Task Unknown_membership_acknowledgement_keeps_selection_and_prevents_repeated_write() {
        using var context = Context();
        var fixture = new TeamAuthoringScenario(TeamAuthoringState.Existing, (_, _) => Task.FromResult<string?>(null));
        var writes = 0;
        var reads = 0;
        var operations = new TeamMembershipOperations((_, _) => {
            writes++;
            return Task.FromResult<TeamMembershipOutcome>(new TeamMembershipOutcome.Unknown());
        }, (_, _) => {
            reads++;
            return Task.FromResult(fixture.Team!);
        });
        var cut = context.Render<TeamMembersSelector>(parameters => parameters.Add(component => component.Team, fixture.Team!)
            .Add(component => component.Agents, fixture.Agents).Add(component => component.Operations, operations));
        await cut.Find("[data-testid='agents-team-members-confirm']").ClickAsync();
        await cut.Find("[data-testid='agents-team-members-confirm']").ClickAsync();
        Assert.Equal(1, writes);
        Assert.Equal(1, reads);
        Assert.True(cut.Find("[data-testid='agents-team-members-confirm']").HasAttribute("disabled"));
        Assert.Contains("will not be repeated", cut.Find("[data-testid='agents-team-members-status']").TextContent);
    }

    private static Task SelectAsync(IRenderedComponent<TeamMembersSelector> cut, Guid id)
        => cut.FindComponents<CanDoItAll.AgentFramework.Components.AgentSelectionCard>().Single(component => component.Instance.Agent.Id == id)
            .Find("[data-testid='agents-team-member-card']").ClickAsync();

    private static AgentTeamEditorModel Model(Guid id, string name, string description, DateTimeOffset? created = null) => new() {
        Id = id, Name = name, Description = description, Icon = "groups",
        ExpectedMetadata = new(created ?? DateTimeOffset.UtcNow, name, description, "groups")
    };

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
