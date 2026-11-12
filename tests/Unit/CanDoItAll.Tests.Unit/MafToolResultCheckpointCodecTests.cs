using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafToolResultCheckpointCodecTests {
    private const string OriginalShape = "CanDoItAll.AgentFramework.Maf.MafToolRunContext+ResultCheckpoint";

    [Fact]
    public void Historical_literal_text_checkpoint_remains_readable() {
        var saved = Historical("""{"kind":1,"value":"original result"}""");

        Assert.Equal("original result", MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.None));
        Assert.Equal(MafToolResultKind.Text, MafToolResultCheckpointCodec.Decode(saved).Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("original result")]
    public void New_scalar_checkpoint_preserves_original_wire_identity(string? value) {
        var saved = MafToolResultCheckpointCodec.Capture(value);
        using var json = JsonDocument.Parse(saved.PayloadJson);

        Assert.Equal(1, saved.Version);
        Assert.Equal("maf-tool-protocol", saved.Format);
        Assert.Equal(OriginalShape, json.RootElement.GetProperty("Shape").GetString());
        Assert.Equal(Packages(), json.RootElement.GetProperty("Packages").GetString());
        var payload = json.RootElement.GetProperty("Value");
        Assert.Equal(value is null ? MafToolResultKind.Null : MafToolResultKind.Text,
            payload.GetProperty("kind").Deserialize<MafToolResultKind>(MafToolProtocolCodec.SerializationOptions));
        Assert.Equal(2, payload.EnumerateObject().Count());
        Assert.Equal(value, MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.None));
    }

    [Fact]
    public void Historical_committed_result_retains_owner_evidence_and_refuses_changed_outcome() {
        var saved = Historical("""{"kind":8,"value":"accepted","committedEffect":{"sourceKind":"task-plan","sourceId":"original-operation"},"committedResultKind":1}""");
        using var effects = AgentToolInvocationEffectScope.Begin();

        Assert.Equal("accepted", MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.Committed));
        Assert.Equal(new AgentToolCommittedEffect("task-plan", "original-operation"), effects.CommittedEffect);
        Assert.Throws<AgentToolAdmissionException>(() =>
            MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.NotCommitted));
        Assert.Throws<AgentToolAdmissionException>(() => MafToolResultCheckpointCodec.Capture("accepted",
            new("denied", "No authority"), new("task-plan", "original-operation")));
    }

    [Fact]
    public void Historical_denial_restores_its_original_failure_without_owner_commit() {
        var saved = Historical("""{"kind":5,"value":"No authority","preDispatchFailure":{"failureCode":"denied","safeMessage":"No authority","canRetryWithCorrectedInput":false}}""");
        using var effects = AgentToolInvocationEffectScope.Begin();

        Assert.Equal("No authority", MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.NotCommitted));
        Assert.Equal(new AgentToolPreDispatchFailure("denied", "No authority"), effects.PreDispatchFailure);
        Assert.Null(effects.CommittedEffect);
        Assert.Throws<AgentToolAdmissionException>(() =>
            MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.Committed));
    }

    [Fact]
    public void Typed_failure_disclosure_and_sdk_content_keep_their_original_shapes() {
        var failure = new AgentToolFailureResult(false, "declined", "Owner refused", false) {
            EffectState = AgentToolEffectState.NotCommitted
        };
        var saved = MafToolResultCheckpointCodec.Capture(failure);

        Assert.Equal(failure, Assert.IsType<AgentToolFailureResult>(
            MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.NotCommitted)));
        var disclosure = MafToolResultCheckpointCodec.Decode(saved);
        Assert.Equal(MafToolResultKind.TypedFailureJson, disclosure.Kind);
        Assert.Equal("declined", disclosure.Value.GetProperty("errorCode").GetString());
        Assert.Null(disclosure.PreDispatchFailure);

        var text = new TextContent("model evidence Ω");
        var content = MafToolResultCheckpointCodec.Capture(text);
        Assert.Equal(text.Text, Assert.IsType<TextContent>(
            MafToolResultCheckpointCodec.Restore(content, AgentToolEffectState.None)).Text);
    }

    [Fact]
    public void Browser_canonical_payload_survives_result_projection() {
        var canonical = JsonSerializer.SerializeToElement(new { isError = true, content = new[] { new { type = "text", text = "original" } } });
        var result = new BrowserMcpToolResult(AgentToolInvocationOutcome.Failed, "projected", [], "browser-failure") {
            CanonicalResult = canonical
        };
        var saved = MafToolResultCheckpointCodec.Capture(result);
        var restored = Assert.IsType<BrowserMcpToolResult>(
            MafToolResultCheckpointCodec.Restore(saved, AgentToolEffectState.Unknown));

        Assert.Equal(result.Outcome, restored.Outcome);
        Assert.Equal(result.Message, restored.Message);
        Assert.Equal(canonical.GetRawText(), restored.CanonicalResult.GetRawText());
    }

    [Fact]
    public void Refactor_does_not_admit_an_alternate_type_identity_or_unknown_result_kind() {
        var original = Historical("""{"kind":1,"value":"saved"}""");
        var payload = JsonNode.Parse(original.PayloadJson)!.AsObject();
        payload["Shape"] = typeof(MafToolResultCheckpoint).FullName;
        var renamed = AgentToolProtocolEnvelope.Create(original.Format, original.Version, payload.ToJsonString());

        Assert.Throws<AgentToolAdmissionException>(() => MafToolResultCheckpointCodec.Decode(renamed));
        Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Decode<ChatResponse>(original));
        Assert.Throws<AgentToolAdmissionException>(() =>
            MafToolResultCheckpointCodec.Restore(Historical("""{"kind":999,"value":null}"""), AgentToolEffectState.None));
    }

    private static AgentToolProtocolEnvelope Historical(string value) {
        using var payload = JsonDocument.Parse(value);
        return AgentToolProtocolEnvelope.Create("maf-tool-protocol", 1,
            JsonSerializer.Serialize(new { Packages = Packages(), Shape = OriginalShape, Value = payload.RootElement }));
    }

    private static string Packages()
        => string.Join("|", typeof(ChatResponse).Assembly.GetName().Version,
            typeof(OpenAIClient).Assembly.GetName().Version, typeof(OllamaApiClient).Assembly.GetName().Version);
}
