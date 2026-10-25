namespace CanDoItAll.Tests.Integration;

[Trait("Category", "LongRunning")]
public sealed class AgentPlanningPublicationCostTests {
    [Fact]
    public Task Small_coarse_publication_guard_measures_unrelated_writers_during_actual_SQL_wait()
        => AgentPlanningPublicationCostFixture.RunAsync();

    [Fact]
    public Task Small_selected_authority_keeps_unrelated_writers_responsive_during_actual_SQL_wait()
        => AgentPlanningPublicationCostFixture.RunAsync(AgentPlanningPublicationGuard.SelectedAgent);
}
