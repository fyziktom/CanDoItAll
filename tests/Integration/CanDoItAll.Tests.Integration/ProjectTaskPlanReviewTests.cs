using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using EntryFixture = CanDoItAll.Tests.Integration.ProjectTaskPlanExecutionEntryTests.EntryFixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanReviewTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_read_revocation_blocks_waiting_and_completed_review_without_mutating_history(bool completed) {
        await using var fixture = await EntryFixture.CreateAsync();
        var original = await ProjectTaskPlanExecutionActionTests.StartAsync(fixture);
        if (completed) {
            await fixture.Workspace.ApplyPlanningActionAsync(
                ProjectTaskPlanExecutionActionTests.Action(original, AgentPlanningAction.Approve), AgentExecutionOperationId.New());
        }
        var before = JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id));
        await fixture.Files.Store.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => agent.Id == fixture.Files.Agent.Id ? agent with {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new())
            } : agent).ToArray()
        });
        var catalog = await fixture.Files.Store.LoadCatalogAsync();
        Assert.False(AgentProjectStructureAccessMetadata.Read(catalog.Agents.Single(agent => agent.Id == fixture.Files.Agent.Id).ConfigurationJson).CanRead);
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Workspace.GetExecutionRunDetailAsync(original.Run.Id));
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id)));
        Assert.Single(fixture.Wire.Requests);
        await fixture.Files.Sql.VerifyAsync(completed ? 1 : 0, completed ? 1 : 0);
    }
}
