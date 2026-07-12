using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.Tests.Support.CognitiveMemory;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemorySourceIngestionTests
{
    [Fact]
    public void SourceSnapshotItemIds_AreDeterministicAndParseable()
    {
        var projectId = Guid.NewGuid();
        var item = FakeProjectStructureSourceSnapshotProvider.CreateNode(
            projectId,
            "node-a",
            "Node A",
            "Docker local simulation context");

        Assert.True(MemorySourceItemId.TryParse(item.Id, out var key));
        Assert.Equal(MemorySourceKind.WorkbenchProjectStructure, key.SourceKind);
        Assert.Equal(projectId, key.ScopeId);
        Assert.Equal(MemorySourceEntityKind.ProjectNode, key.EntityKind);
        Assert.Equal("node-a", key.SourceEntityId);
    }

    [Fact]
    public void SourceSnapshotHashes_ChangeOnlyWhenAuthoritativeContentChanges()
    {
        var projectId = Guid.NewGuid();
        var first = FakeProjectStructureSourceSnapshotProvider.CreateNode(
            projectId,
            "node-a",
            "Node A",
            "Docker local simulation context");
        var same = FakeProjectStructureSourceSnapshotProvider.CreateNode(
            projectId,
            "node-a",
            "Node A",
            "Docker local simulation context");
        var changed = FakeProjectStructureSourceSnapshotProvider.CreateNode(
            projectId,
            "node-a",
            "Node A",
            "Docker production deployment context");

        Assert.Equal(first.ContentHash, same.ContentHash);
        Assert.NotEqual(first.ContentHash, changed.ContentHash);
    }
}
