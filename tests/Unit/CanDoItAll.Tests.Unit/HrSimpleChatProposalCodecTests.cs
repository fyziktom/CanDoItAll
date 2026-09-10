using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Agents.SimpleChats;
using Xunit;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class HrSimpleChatProposalCodecTests {
    [Fact]
    public void Create_preparation_uses_the_exact_owner_digest_and_detaches_mutable_tags() {
        var codec = new HrSimpleChatProposalCodec();
        var tags = new[] { "beta", "alpha" };
        var command = HrSimpleChatTestFixture.CreateCommand() with { Tags = tags };
        var owner = LlmChatDefinitionCreatePreparation.Prepare(command);
        var prepared = codec.PrepareCreate(command);
        tags[0] = "changed after admission";
        Assert.Equal(owner.Fingerprint.Value, prepared.Digest.Value);
        Assert.Equal(owner.SemanticVersion, prepared.SemanticVersion);
        Assert.Equal(["alpha", "beta"], owner.Definition.Tags);
        Assert.Equal(["alpha", "beta"], codec.Read<CreateLlmChatDefinitionCommand>(prepared).Tags);
        Assert.DoesNotContain(command.SystemPrompt, prepared.ToString());
        Assert.DoesNotContain(command.SystemPrompt, owner.ToString());
    }

    [Fact]
    public void Equivalent_json_property_order_timeout_and_canonical_tags_preserve_create_semantics() {
        var codec = new HrSimpleChatProposalCodec();
        var first = HrSimpleChatTestFixture.CreateCommand() with {
            Settings = new(0.2, "{\"z\":1,\"a\":{\"n\":2}}"),
            ResponseFormat = new(true, "{\"type\":\"object\",\"additionalProperties\":false}", "response", "description")
        };
        var second = first with {
            Name = $"  {first.Name}  ",
            Settings = new(0.2, "{\"a\": {\"n\": 2}, \"z\": 1}"),
            ResponseFormat = new(true, "{\"additionalProperties\":false,\"type\":\"object\"}", "response", "description"),
            Timeout = TimeSpan.FromTicks(first.Timeout!.Value.Ticks),
            Tags = [" BETA ", "ALPHA"]
        };
        Assert.Equal(codec.PrepareCreate(first).Digest, codec.PrepareCreate(second).Digest);
    }

    [Fact]
    public void Every_create_field_and_model_setting_affects_the_approved_semantics() {
        var codec = new HrSimpleChatProposalCodec();
        var original = HrSimpleChatTestFixture.CreateCommand();
        var digest = codec.PrepareCreate(original).Digest;
        CreateLlmChatDefinitionCommand[] changes = [
            original with { Name = "Different" },
            original with { Summary = "Different" },
            original with { AvatarImageUrl = "https://example.test/avatar.png" },
            original with { SystemPrompt = "Different" },
            original with { ProviderProfileId = Guid.NewGuid() },
            original with { Model = "different-model" },
            original with { Settings = original.Settings with { Temperature = 0.3 } },
            original with { Settings = original.Settings with { ThinkingEffort = AgentReasoningEffortLevel.High } },
            original with { Settings = original.Settings with { ModelParameterConfigurationJson = "{\"different\":true}" } },
            original with { Timeout = original.Timeout!.Value + TimeSpan.FromTicks(1) },
            original with { ResponseFormat = null },
            original with { ResponseFormat = original.ResponseFormat! with { RequireJson = false } },
            original with { ResponseFormat = original.ResponseFormat! with { SchemaJson = "{\"type\":\"string\"}" } },
            original with { ResponseFormat = original.ResponseFormat! with { SchemaName = "different" } },
            original with { ResponseFormat = original.ResponseFormat! with { SchemaDescription = "different" } },
            original with { RevisionReason = "Different" },
            original with { Tags = ["different"] }
        ];
        Assert.All(changes, changed => Assert.NotEqual(digest, codec.PrepareCreate(changed).Digest));
    }

    [Fact]
    public void Update_and_disclosure_bind_exact_identity_revision_and_concurrency_token() {
        var codec = new HrSimpleChatProposalCodec();
        var target = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        var updatedRevision = new HrSimpleChatDefinitionVersion(target.DefinitionId, 2, 0);
        var updatedToken = new HrSimpleChatDefinitionVersion(target.DefinitionId, 1, 1);
        var changedIdentity = new HrSimpleChatDefinitionVersion(Guid.NewGuid(), 1, 0);
        var value = HrSimpleChatTestFixture.CreateCommand();
        var updateDigest = codec.PrepareUpdate(new(target, value)).Digest;
        var disclosureDigest = codec.PrepareSettings(target).Digest;
        foreach (var changed in new[] { updatedRevision, updatedToken, changedIdentity }) {
            Assert.NotEqual(updateDigest, codec.PrepareUpdate(new(changed, value)).Digest);
            Assert.NotEqual(disclosureDigest, codec.PrepareSettings(changed).Digest);
        }

        Assert.NotEqual(codec.PrepareStatus(new(target, LlmChatDefinitionStatus.Active)).Digest,
            codec.PrepareStatus(new(target, LlmChatDefinitionStatus.Archived)).Digest);
    }

    [Fact]
    public void Protocol_preparation_rejects_unknown_fields_and_duplicate_properties() {
        var codec = new HrSimpleChatProposalCodec();
        var prepared = codec.PrepareCreate(HrSimpleChatTestFixture.CreateCommand());
        var extra = JsonNode.Parse(prepared.ArgumentsJson)!.AsObject();
        extra["authority"] = "untrusted";
        Assert.Throws<JsonException>(() => codec.Prepare(prepared.ToolName, JsonSerializer.SerializeToElement(extra)));
        var unknownField = JsonNode.Parse(prepared.ArgumentsJson)!.AsObject();
        unknownField["request"]!.AsObject()["tools"] = new JsonArray("unapproved tool");
        Assert.Throws<JsonException>(() => codec.Prepare(prepared.ToolName, JsonSerializer.SerializeToElement(unknownField)));
        using var duplicate = JsonDocument.Parse("{\"request\":{},\"request\":{}}");
        Assert.Throws<JsonException>(() => codec.Prepare(prepared.ToolName, duplicate.RootElement));
    }

    [Fact]
    public void Stored_payload_tampering_and_unknown_semantic_versions_fail_explicitly() {
        var codec = new HrSimpleChatProposalCodec();
        var original = codec.PrepareCreate(HrSimpleChatTestFixture.CreateCommand());
        var changed = codec.PrepareCreate(HrSimpleChatTestFixture.CreateCommand() with { SystemPrompt = "tampered" });
        var tampered = new AgentToolPreparedPayload(original.ToolName, original.SemanticVersion, original.Digest,
            changed.ArgumentsJson, original.Effect, original.Recovery);
        Assert.Throws<InvalidOperationException>(() => codec.Read<CreateLlmChatDefinitionCommand>(tampered));
        var unknownVersion = new AgentToolPreparedPayload(original.ToolName, original.SemanticVersion + 1, original.Digest,
            original.ArgumentsJson, original.Effect, original.Recovery);
        Assert.Throws<InvalidOperationException>(() => codec.Read<CreateLlmChatDefinitionCommand>(unknownVersion));
    }

    [Fact]
    public void Paging_cursor_round_trip_preserves_the_exact_owner_definition_id() {
        var codec = new HrSimpleChatProposalCodec();
        var cursor = new HrSimpleChatDefinitionCursor(Guid.NewGuid(), HrSimpleChatTestFixture.Now);
        var request = codec.Read<HrSimpleChatSearchRequest>(codec.PrepareSearch(new(Cursor: cursor)));
        Assert.Equal(cursor, request.Cursor);
        Assert.Equal(cursor.DefinitionId, request.ToOwnerQuery().Cursor!.Value.DefinitionId.Value);
    }

    [Fact]
    public void Sensitive_prompt_settings_schema_and_revision_reason_are_redacted_from_policy_arguments() {
        var codec = new HrSimpleChatProposalCodec();
        var command = HrSimpleChatTestFixture.CreateCommand();
        var prepared = codec.PrepareCreate(command);
        using var document = JsonDocument.Parse(prepared.ArgumentsJson);
        var arguments = document.RootElement.EnumerateObject().Select(property =>
            new KeyValuePair<string, object?>(property.Name, property.Value.Clone())).ToArray();
        var redacted = JsonSerializer.Serialize(AgentToolInvocationPolicyMetadata.SanitizeArguments(prepared.ToolName, arguments));
        Assert.DoesNotContain(command.SystemPrompt, redacted);
        Assert.DoesNotContain(command.Settings.ModelParameterConfigurationJson, redacted);
        Assert.DoesNotContain(command.ResponseFormat!.SchemaDescription, redacted);
        Assert.DoesNotContain(command.RevisionReason, redacted);
        Assert.Contains("redacted", redacted);
    }

    [Fact]
    public void Neutral_durable_intent_and_digest_ids_round_trip_without_allocating_new_identity() {
        var intent = new AgentToolBusinessIntentId(Guid.NewGuid());
        var digest = new AgentToolSemanticDigest(new string('a', 64));
        Assert.Equal(intent, JsonSerializer.Deserialize<AgentToolBusinessIntentId>(JsonSerializer.Serialize(intent)));
        Assert.Equal(digest, JsonSerializer.Deserialize<AgentToolSemanticDigest>(JsonSerializer.Serialize(digest)));
    }
}
