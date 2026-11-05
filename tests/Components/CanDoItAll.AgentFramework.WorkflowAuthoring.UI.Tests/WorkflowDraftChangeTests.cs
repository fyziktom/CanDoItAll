using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowDraftChangeTests {
    [Fact]
    public void Scalar_edits_record_ABA_without_counting_equal_assignments() {
        var document = new WorkflowCanvasDocument();
        var original = document.Name;
        var before = document.EditRevision;
        document.Name = original;
        Assert.Equal(before, document.EditRevision);
        document.Name = "Other name";
        document.Name = original;
        Assert.Equal(before + 2, document.EditRevision);
    }

    [Fact]
    public void Removed_nodes_cannot_change_the_document_revision() {
        var document = new WorkflowCanvasDocument();
        var node = Node();
        document.Nodes.Add(node);
        var before = document.EditRevision;
        node.Name = "Current member";
        Assert.Equal(before + 1, document.EditRevision);
        document.Nodes.Remove(node);
        before = document.EditRevision;
        node.Name = "Retired member";
        Assert.Equal(before, document.EditRevision);
    }

    [Fact]
    public void Removed_edges_cannot_change_the_document_revision() {
        var document = new WorkflowCanvasDocument();
        var edge = Edge();
        document.Edges.Add(edge);
        var before = document.EditRevision;
        edge.ConditionExpression = "Current route";
        Assert.Equal(before + 1, document.EditRevision);
        document.Edges.Remove(edge);
        before = document.EditRevision;
        edge.ConditionExpression = "Retired route";
        Assert.Equal(before, document.EditRevision);
    }

    [Fact]
    public void Clear_and_readd_release_and_restore_only_current_members() {
        var document = new WorkflowCanvasDocument();
        var node = Node();
        var edge = Edge();
        document.Nodes.Add(node);
        document.Edges.Add(edge);
        document.Nodes.Clear();
        document.Edges.Clear();
        var before = document.EditRevision;
        node.Name = "Retired node";
        edge.ConditionExpression = "Retired route";
        Assert.Equal(before, document.EditRevision);
        document.Nodes.Add(node);
        document.Edges.Add(edge);
        before = document.EditRevision;
        node.Name = "Current node";
        edge.ConditionExpression = "Current route";
        Assert.Equal(before + 2, document.EditRevision);
    }

    [Fact]
    public void Duplicate_references_notify_once_until_the_last_membership_is_removed() {
        var document = new WorkflowCanvasDocument();
        var node = Node();
        document.Nodes.Add(node);
        document.Nodes.Add(node);
        var before = document.EditRevision;
        node.Name = "One notification";
        Assert.Equal(before + 1, document.EditRevision);
        document.Nodes.Remove(node);
        before = document.EditRevision;
        node.Name = "Still a member";
        Assert.Equal(before + 1, document.EditRevision);
        document.Nodes.Clear();
        before = document.EditRevision;
        node.Name = "No membership";
        Assert.Equal(before, document.EditRevision);
    }

    private static WorkflowCanvasNodeDraft Node() => new(new("start"), WorkflowNodeKind.Start);

    private static WorkflowCanvasEdgeDraft Edge() => new(new("route"), new("start"), new("end"));
}
