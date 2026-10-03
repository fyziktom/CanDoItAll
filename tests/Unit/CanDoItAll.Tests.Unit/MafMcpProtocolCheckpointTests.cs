using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafMcpProtocolCheckpointTests {
    [Theory]
    [InlineData("""{"type":"text","text":"Owned result Ω","_meta":{"owner":"fixture"},"annotations":{"audience":["user"],"priority":0.7}}""")]
    [InlineData("""{"type":"image","data":"AQID","mimeType":"image/png"}""")]
    [InlineData("""{"type":"audio","data":"AQID","mimeType":"audio/wav"}""")]
    [InlineData("""{"type":"resource","resource":{"uri":"fixture://owned/text","mimeType":"text/plain","text":"Resource Ω"}}""")]
    [InlineData("""{"type":"resource","resource":{"uri":"fixture://owned/blob","mimeType":"application/octet-stream","blob":"AQID"}}""")]
    public void Installed_MCP_content_round_trips_without_discarding_native_metadata(string json) {
        var native = JsonSerializer.Deserialize<ContentBlock>(json, McpJsonUtilities.DefaultOptions)!;
        var content = native.ToAIContent()!;
        Assert.NotNull(content.RawRepresentation);

        var envelope = MafToolProtocolCodec.Encode(content);
        var restored = MafToolProtocolCodec.Decode<AIContent>(envelope);

        Assert.Equal(content.GetType(), restored.GetType());
        Assert.Equal(content.RawRepresentation.GetType(), restored.RawRepresentation!.GetType());
        Assert.Equal(MafToolProtocolCodec.Digest(content), MafToolProtocolCodec.Digest(restored));
        Assert.Equal(JsonSerializer.Serialize(content.RawRepresentation, content.RawRepresentation.GetType(), McpJsonUtilities.DefaultOptions),
            JsonSerializer.Serialize(restored.RawRepresentation, restored.RawRepresentation.GetType(), McpJsonUtilities.DefaultOptions));
    }

    [Fact]
    public void Unregistered_opaque_content_still_requires_explicit_recovery() {
        AIContent content = new TextContent("Unknown") { RawRepresentation = new object() };

        var exception = Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Encode(content));

        Assert.Equal(AgentToolAdmissionException.UnsupportedProtocolCode, exception.Code);
    }

    [Theory]
    [InlineData("package")]
    [InlineData("model")]
    public void Foreign_MCP_package_or_shape_cannot_be_replayed(string property) {
        AIContent content = new TextContent("Owned") { RawRepresentation = new TextContentBlock { Text = "Owned" } };
        var original = MafToolProtocolCodec.Encode(content);
        var json = JsonNode.Parse(original.PayloadJson)!;
        json["Value"]!["providerRaw"]![property] = "unsupported";
        var changed = CanDoItAll.AgentFramework.Models.AgentToolProtocolEnvelope.Create(original.Format, original.Version, json.ToJsonString());

        var exception = Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Decode<AIContent>(changed));

        Assert.Equal(AgentToolAdmissionException.UnsupportedProtocolCode, exception.Code);
    }
}
