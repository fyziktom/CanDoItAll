using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.SourceGateway;
namespace CanDoItAll.Tests.Unit.Memory;

public sealed class MemorySourceSnapshotTests
{
    [Fact]
    public void SourceSnapshotItemIds_AreDeterministicAndParseable()
    {
        var projectId = Guid.NewGuid();
        var item = CreateNode(
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
        var first = CreateNode(
            projectId,
            "node-a",
            "Node A",
            "Docker local simulation context");
        var same = CreateNode(
            projectId,
            "node-a",
            "Node A",
            "Docker local simulation context");
        var changed = CreateNode(
            projectId,
            "node-a",
            "Node A",
            "Docker production deployment context");

        Assert.Equal(first.ContentHash, same.ContentHash);
        Assert.NotEqual(first.ContentHash, changed.ContentHash);
    }

    private static MemorySourceItem CreateNode(
        Guid projectId,
        string sourceEntityId,
        string title,
        string content)
    {
        var itemId = MemorySourceItemId.Create(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            MemorySourceEntityKind.ProjectNode,
            sourceEntityId);

        return new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceEntityKind.ProjectNode,
            title,
            content,
            MemorySourceSnapshotHasher.Compute(sourceEntityId, title, content),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new MemorySourceProvenance(
                MemorySourceKind.WorkbenchProjectStructure,
                projectId,
                MemorySourceEntityKind.ProjectNode,
                sourceEntityId,
                $"/projects/{projectId:D}/structure/{sourceEntityId}"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                RedactionPolicy: "test-no-redaction",
                AllowedFutureUsageSummary: "Deterministic provider-neutral source snapshot tests."),
            Layout: null,
            Links: [],
            References: [],
            StorageReference: null,
            Metadata: new Dictionary<string, string>
            {
                ["fixture"] = "memory-source"
            })
        {
            HashPolicy = MemorySourceHashPolicy.PublicRedactedContent
        };
    }
}
