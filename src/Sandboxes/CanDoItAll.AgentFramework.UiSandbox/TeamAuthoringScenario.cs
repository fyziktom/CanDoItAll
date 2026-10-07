using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Teams;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum TeamAuthoringState { New, Existing, Empty, MissingReference, Large, LoadFailure, Rejected, Unknown, HeldLoad, HeldSave, Deleted }

public sealed class TeamAuthoringScenario {
    private readonly Dictionary<Guid, AgentTeamDefinition> teams = [];
    private TaskCompletionSource? held;
    public TeamAuthoringScenario(TeamAuthoringState state, Func<string, CancellationToken, Task<string?>> chooseIcon) {
        State = state;
        var agents = CatalogFixture.Load().Agents;
        Agents = state == TeamAuthoringState.Empty ? [] : state == TeamAuthoringState.Large
            ? Enumerable.Range(0, 90).Select(index => agents[index % agents.Count] with { Id = Guid.NewGuid(), Name = $"Fixture agent {index + 1}" }).ToImmutableArray()
            : agents.Take(8).ToImmutableArray();
        if (state != TeamAuthoringState.New) {
            TeamId = Guid.NewGuid();
            teams[TeamId.Value] = new(TeamId.Value, "Fixture team", "Metadata remains separate from membership.",
                state == TeamAuthoringState.MissingReference ? [Guid.NewGuid()] : Agents.Take(1).Select(agent => agent.Id).ToImmutableArray(),
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "engineering");
        }
        Operations = new(LoadAsync, SaveAsync, chooseIcon);
    }
    public TeamAuthoringState State { get; }
    public Guid? TeamId { get; private set; }
    public IReadOnlyList<AgentDefinition> Agents { get; }
    public TeamMetadataOperations Operations { get; }
    public int MetadataWrites { get; private set; }
    public int MemberWrites { get; private set; }
    public int TeamCount => teams.Count;
    public AgentTeamDefinition? Team => TeamId is { } id && teams.TryGetValue(id, out var team) ? team : null;
    public void Release() => held?.TrySetResult();
    public void Delete() {
        if (TeamId is { } id) {
            teams.Remove(id);
        }
    }
    private async Task WaitAsync(CancellationToken token) {
        held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await held.Task.WaitAsync(token);
    }
    private async Task<AgentTeamEditorModel> LoadAsync(Guid? id, CancellationToken token) {
        if (State == TeamAuthoringState.HeldLoad) {
            await WaitAsync(token);
        }
        if (State == TeamAuthoringState.LoadFailure) {
            throw new IOException("Controlled fixture load failure.");
        }
        return id is { } target ? AgentTeamEditorModel.FromDefinition(teams[target]) : new();
    }
    private async Task<TeamMetadataOutcome> SaveAsync(TeamMetadataSubmission submission, CancellationToken token) {
        MetadataWrites++;
        if (State == TeamAuthoringState.HeldSave) {
            await WaitAsync(token);
        }
        if (State == TeamAuthoringState.Deleted) {
            Delete();
        }
        if (State == TeamAuthoringState.Unknown) {
            return new TeamMetadataOutcome.Unknown();
        }
        if (State == TeamAuthoringState.Rejected || submission.Id is { } requested && !teams.ContainsKey(requested)
            || teams.Values.Any(team => team.Id != submission.Id && team.Name.Equals(submission.Name.Trim(), StringComparison.OrdinalIgnoreCase))) {
            return new TeamMetadataOutcome.Rejected();
        }
        var id = submission.Id ?? Guid.NewGuid();
        var previous = teams.GetValueOrDefault(id);
        teams[id] = new(id, submission.Name.Trim(), submission.Description.Trim(), previous?.AgentIds ?? [],
            previous?.CreatedAtUtc ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, AgentTeamIconCatalog.Normalize(submission.Icon));
        TeamId = id;
        return new TeamMetadataOutcome.Accepted(id);
    }
    public void SaveMembers(TeamMembershipSelection selection) {
        if (!teams.TryGetValue(selection.TeamId, out var team) || selection.AgentIds.Any(id => Agents.All(agent => agent.Id != id))) {
            throw new InvalidOperationException("The team or a selected agent is unavailable.");
        }
        MemberWrites++;
        teams[team.Id] = team with { AgentIds = selection.AgentIds };
    }
}
