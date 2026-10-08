using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureImportSourceIdentityTests {
    [Fact]
    public void Omitted_key_preserves_legacy_import_behavior() {
        using var node = JsonDocument.Parse("{\"title\":\"Synthetic task\"}");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Assert.Null(ProjectStructureImportSourceIdentity.ReadOptionalKey(node.RootElement, keys));
        Assert.Empty(keys);
    }

    [Fact]
    public void Required_key_cannot_be_silently_omitted() {
        using var node = JsonDocument.Parse("{\"title\":\"Synthetic task\"}");
        var exception = Assert.Throws<ProjectStructureAgentException>(() => ProjectStructureImportSourceIdentity.ReadOptionalKey(
            node.RootElement, new HashSet<string>(StringComparer.Ordinal), required: true));
        Assert.Equal("ImportSourceKeyRequired", exception.ErrorCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"\"")]
    [InlineData("\"a b\"")]
    [InlineData("\"a\\nb\"")]
    [InlineData("\"_a\"")]
    [InlineData("\"č\"")]
    public void Invalid_keys_are_rejected_without_registering_identity(string keyJson) {
        using var node = JsonDocument.Parse("{\"sourceKey\":" + keyJson + "}");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureImportSourceIdentity.ReadOptionalKey(node.RootElement, keys));
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("InvalidImportSourceKey", exception.ErrorCode);
        Assert.Empty(keys);
    }

    [Fact]
    public void Key_length_is_bounded_and_protocol_punctuation_is_preserved() {
        const string prefix = "a:demo/seed-v2.";
        var key = prefix + new string('x', 128 - prefix.Length);
        Assert.Equal(128, key.Length);
        var node = JsonSerializer.SerializeToElement(new { sourceKey = key });
        Assert.Equal(key, ProjectStructureImportSourceIdentity.ReadOptionalKey(node, new HashSet<string>(StringComparer.Ordinal)));
        var oversized = JsonSerializer.SerializeToElement(new { sourceKey = key + "x" });
        Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureImportSourceIdentity.ReadOptionalKey(oversized, new HashSet<string>(StringComparer.Ordinal)));
    }

    [Fact]
    public void Uniqueness_is_case_sensitive_across_the_shared_outline_scope() {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var first = JsonSerializer.SerializeToElement(new { sourceKey = "Task-A" });
        var second = JsonSerializer.SerializeToElement(new { sourceKey = "task-a" });
        Assert.Equal("Task-A", ProjectStructureImportSourceIdentity.ReadOptionalKey(first, keys));
        Assert.Equal("task-a", ProjectStructureImportSourceIdentity.ReadOptionalKey(second, keys));
        var exception = Assert.Throws<ProjectStructureAgentException>(() => ProjectStructureImportSourceIdentity.ReadOptionalKey(first, keys));
        Assert.Equal("DuplicateImportSourceKey", exception.ErrorCode);
    }

    [Fact]
    public void Metadata_keeps_the_import_identity_out_of_human_notes() {
        var identity = new ProjectStructureImportSourceIdentity("arrival/check-in", ProjectStructureImportSourceKind.JsonOutline, "custom:container");
        using var metadata = JsonDocument.Parse(identity.ToMetadataJson());
        var source = metadata.RootElement.GetProperty("importSource");
        Assert.Equal(identity.SourceKey, source.GetProperty("sourceKey").GetString());
        Assert.Equal(3, source.GetProperty("sourceKind").GetInt32());
        Assert.Equal(identity.ContainerNodeId, source.GetProperty("containerNodeId").GetString());
        Assert.False(metadata.RootElement.TryGetProperty("workItem", out _));
        Assert.False(metadata.RootElement.TryGetProperty("notes", out _));
        var roundTrip = ProjectObjectMetadataSerializer.Parse(metadata.RootElement.GetRawText());
        Assert.Equal(identity, roundTrip.ImportSource);
        using var rewritten = JsonDocument.Parse(ProjectObjectMetadataSerializer.Serialize(roundTrip));
        Assert.Equal(3, rewritten.RootElement.GetProperty("importSource").GetProperty("sourceKind").GetInt32());
    }

    [Theory]
    [InlineData(ProjectObjectType.ProjectBlock, "delivery")]
    [InlineData(ProjectObjectType.WorkItem, "task")]
    public void Node_kind_normalization_preserves_typed_import_provenance(ProjectObjectType objectType, string subtype) {
        var identity = new ProjectStructureImportSourceIdentity("arrival/check-in", ProjectStructureImportSourceKind.JsonOutline, "custom:container");
        var metadata = ProjectObjectMetadataSerializer.Parse(identity.ToMetadataJson());
        var normalized = ProjectNodeKindRegistry.NormalizeMetadata(objectType, subtype, metadata, "Confirm arrival.", null);
        Assert.Equal(identity, normalized.ImportSource);
        var roundTrip = ProjectObjectMetadataSerializer.Parse(ProjectObjectMetadataSerializer.Serialize(normalized));
        Assert.Equal(identity, roundTrip.ImportSource);
        if (objectType == ProjectObjectType.WorkItem) {
            Assert.Equal("Confirm arrival.", roundTrip.WorkItem!.Description);
        }
    }
}
