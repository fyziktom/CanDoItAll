using System.Text.Json;
using CanDoItAll.Modules.Resources;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceContractCompatibilityTests {
    [Fact]
    public void Resource_summary_keeps_the_wire_shape_and_does_not_serialize_lifetime_provenance() {
        const string payload = """
            {"Id":"00000001-0000-0000-0000-000000000000","ProjectId":"00000002-0000-0000-0000-000000000000","ProjectName":"Project","LegacyResourceKind":2,"ConnectorPluginKey":"resource.web-link","ConnectorDisplayName":"Web link","Name":"Documentation","LocationOrIdentifier":"https://example.test/docs","ValidationStatus":1,"Sensitivity":2}
            """;
        var summary = JsonSerializer.Deserialize<ResourceSummary>(payload)! with { ProjectLifetimeId = new Guid(3, 0, 0, new byte[8]) };
        Assert.Equal(ResourceKind.WebLink, summary.LegacyResourceKind);
        Assert.Equal(ResourceValidationStatus.Valid, summary.ValidationStatus);
        Assert.Equal(ResourceSensitivity.Restricted, summary.Sensitivity);
        Assert.Equal(payload, JsonSerializer.Serialize(summary));
    }

    [Fact]
    public void Existing_connector_json_and_editor_defaults_remain_readable_after_the_assembly_move() {
        const string payload = """{"Url":"https://example.test/docs","TitleHint":"Existing title"}""";
        var config = JsonSerializer.Deserialize<WebLinkResourceConfig>(payload)!;
        Assert.Equal("Existing title", config.TitleHint);
        Assert.Equal(payload, JsonSerializer.Serialize(config));
        var editor = new ResourceEditorModel();
        Assert.Null(editor.Id);
        Assert.Null(editor.ExpectedProjectAdmission);
        Assert.Equal(ResourceConnectorPluginKeys.Repository, editor.ConnectorPluginKey);
        Assert.Equal(ResourceValidationStatus.Unknown, editor.ValidationStatus);
        Assert.Equal(ResourceSensitivity.Normal, editor.Sensitivity);
    }
}
