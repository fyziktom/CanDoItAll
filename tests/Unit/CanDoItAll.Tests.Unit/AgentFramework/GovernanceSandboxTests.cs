using System.Text.Json;
using CanDoItAll.AgentFramework.UI.Governance;
using CanDoItAll.AgentFramework.UiSandbox;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class GovernanceSandboxTests {
    public static TheoryData<GovernanceSandboxScenario> Scenarios => new(Enum.GetValues<GovernanceSandboxScenario>());

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Representative_sample_uses_only_allowlisted_bounded_values(GovernanceSandboxScenario scenario) {
        var sample = new GovernanceSandboxFixture();
        sample.SetScenario(scenario);
        Assert.DoesNotContain("governance-denied-payload", JsonSerializer.Serialize(sample.Presentation), StringComparison.Ordinal);
        Assert.All(sample.Presentation.Runs, run => {
            Assert.True(run.Title.Length <= 160);
            Assert.EndsWith(" UTC", run.Updated);
        });
        if (sample.State.AcceptedRunId is { } accepted) {
            Assert.Equal(accepted, sample.Presentation.Detail?.Run.Id);
        }
    }

    [Fact]
    public void Controlled_selection_changes_sample_and_records_typed_intent() {
        var sample = new GovernanceSandboxFixture();
        sample.Apply(new GovernanceIntent.SelectRun(GovernanceSandboxFixture.Run2));
        Assert.Equal(GovernanceSandboxFixture.Run2, sample.State.AcceptedRunId);
        Assert.Contains("SelectRun", sample.IntentLog);
        sample.Apply(new GovernanceIntent.SelectAgent(null));
        Assert.Null(sample.State.AcceptedAgentId);
        Assert.Contains(sample.Presentation.Runs, run => run.AgentId == GovernanceSandboxFixture.AgentB);
        Assert.Contains("SelectAgent", sample.IntentLog);
    }

    [Fact]
    public void Detail_retry_preserves_sample_list_and_selected_target() {
        var sample = new GovernanceSandboxFixture();
        sample.SetScenario(GovernanceSandboxScenario.DetailFailure);
        var rows = sample.Presentation.Runs;
        sample.Apply(new GovernanceIntent.RetryDetail());
        Assert.Equal(rows, sample.Presentation.Runs);
        Assert.Equal(GovernanceReadPhase.Ready, sample.State.Detail.Phase);
        Assert.Equal(GovernanceSandboxFixture.Run1, sample.State.AcceptedRunId);
    }

    [Fact]
    public void Missing_sample_target_does_not_become_all_agents_on_retry() {
        var sample = new GovernanceSandboxFixture();
        sample.SetScenario(GovernanceSandboxScenario.MissingAgent);
        var target = sample.State.DesiredAgentId;
        sample.Apply(new GovernanceIntent.RetryCatalog());
        Assert.False(sample.State.AgentResolved);
        Assert.Equal(target, sample.State.DesiredAgentId);
        Assert.Empty(sample.Presentation.Runs);
    }
}
