using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowDefinitionReviewTests {
    [Fact]
    public void Independent_scalar_and_text_changes_combine_against_one_current_version() {
        var original = Original() with { Description = "first\nmiddle\nlast\n" };
        var local = original with { Name = "My name", Description = "local\nmiddle\nlast\n" };
        var current = original with { VersionId = WorkflowVersionId.New(), Description = "first\nmiddle\nremote\n", ExternalKey = "retained current key" };
        var review = new WorkflowDefinitionReview(original, local, current, 7, false);
        var result = review.Result();
        Assert.Equal("My name", result.Name);
        Assert.Equal("local\nmiddle\nremote\n", result.Description);
        Assert.Equal(current.VersionId, result.VersionId);
        Assert.Equal("retained current key", result.ExternalKey);
        Assert.Equal(7, review.Revision);
        Assert.True(review.KeepsLocalGraph);
    }

    [Fact]
    public void Equal_graph_values_from_independent_reads_do_not_create_a_false_topology_conflict() {
        var original = Original();
        var local = JsonSerializer.Deserialize<WorkflowDefinition>(JsonSerializer.Serialize(original))! with { Name = "Local name" };
        var current = JsonSerializer.Deserialize<WorkflowDefinition>(JsonSerializer.Serialize(original))! with { VersionId = WorkflowVersionId.New(), Description = "Current description" };
        var review = new WorkflowDefinitionReview(original, local, current, 0, false);
        Assert.True(review.IsResolved);
        Assert.True(review.KeepsLocalGraph);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Competing_graph_edits_require_a_whole_graph_choice_and_preserve_opaque_settings(bool useCurrent) {
        var original = Original();
        var local = ChangeGraph(original, "Local node", "{\"future\":{\"local\":null},\"enabled\":false}");
        var current = ChangeGraph(original, "Current node", "{\"future\":[1,null,3],\"enabled\":true}") with { VersionId = WorkflowVersionId.New() };
        var review = new WorkflowDefinitionReview(original, local, current, 3, false);
        Assert.False(review.IsResolved);
        Assert.Throws<InvalidOperationException>(() => review.Result());
        if (useCurrent) {
            review.Graph.UseCurrent();
        } else {
            review.Graph.UseLocal();
        }
        var expected = useCurrent ? current : local;
        var result = review.Result();
        Assert.Equal(JsonSerializer.Serialize(expected.Graph), JsonSerializer.Serialize(result.Graph));
        Assert.Equal(current.VersionId, result.VersionId);
        Assert.Equal(!useCurrent, review.KeepsLocalGraph);
    }

    [Fact]
    public void Raw_input_forces_a_graph_choice_when_the_parsed_local_graph_looks_unchanged() {
        var original = Original();
        var current = ChangeGraph(original, "Remote node", "{\"future\":null}") with { VersionId = WorkflowVersionId.New() };
        var review = new WorkflowDefinitionReview(original, original, current, 2, true);
        Assert.False(review.Graph.IsResolved);
        review.Graph.UseLocal();
        Assert.True(review.KeepsLocalGraph);
        Assert.Equal(JsonSerializer.Serialize(original.Graph), JsonSerializer.Serialize(review.Result().Graph));
    }

    [Fact]
    public void A_comparison_cannot_attach_another_workflow_as_the_current_base() {
        var original = Original();
        Assert.Throws<ArgumentException>(() => new WorkflowDefinitionReview(original, original,
            original with { Id = WorkflowId.New() }, 0, false));
    }

    private static WorkflowDefinition Original()
        => WorkflowCanvasDefinitionMapper.ToDefinition(WorkflowCanvasDefinitionMapper.CreateDraft([]));

    private static WorkflowDefinition ChangeGraph(WorkflowDefinition definition, string name, string settings)
        => definition with { Graph = definition.Graph with {
            Nodes = definition.Graph.Nodes.Select((node, index) => index == 0
                ? node with { Name = name, Settings = node.Settings with { ExecutorSettingsJson = settings, ExecutionPolicy = null } } : node).ToArray()
        } };
}
