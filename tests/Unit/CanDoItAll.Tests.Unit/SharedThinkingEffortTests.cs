using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;

namespace CanDoItAll.Tests.Unit;

using RuntimeProvider = CanDoItAll.AgentFramework.Models.ProviderProfile;
using RuntimeKind = CanDoItAll.AgentFramework.Models.ProviderKind;

public sealed class SharedThinkingEffortTests {
    [Theory]
    [InlineData("{\"error\":{\"code\":\"unsupported_value\",\"param\":\"temperature\",\"message\":\"private detail\"}}", "unsupported_value", "temperature")]
    [InlineData("{\"error\":{\"code\":\"private code\",\"param\":\"private field\",\"message\":\"private detail\"}}", "unclassified", "unclassified")]
    [InlineData("not json", "unclassified", "unclassified")]
    [InlineData("[]", "unclassified", "unclassified")]
    public void Upstream_diagnostics_only_expose_allowlisted_protocol_tokens(string body, string code, string parameter) {
        Assert.Equal((code, parameter), SharedProviderRelayFailureMapper.DescribeUpstreamFailure(Encoding.UTF8.GetBytes(body)));
    }

    [Theory]
    [InlineData(RuntimeKind.OpenAi, "gpt-5.6-sol", true)]
    [InlineData(RuntimeKind.OpenAi, "gpt-5.4-mini", true)]
    [InlineData(RuntimeKind.OpenAi, "gpt-4.1", false)]
    [InlineData(RuntimeKind.Ollama, "gptoss20b64k:latest", false)]
    public void Shared_temperature_follows_source_model_policy(RuntimeKind kind, string model, bool omit) {
        var source = CreateProvider(kind, model);
        var capability = SharedProviderThinkingCapabilityMapper.ToCatalog(source, model);
        var result = SharedProviderRelayThinkingPolicy.Apply(SharedProviderRelayOperation.ChatCompletions,
            Encoding.UTF8.GetBytes("""{"model":"real-model","temperature":0.7}"""), capability);
        Assert.Null(result.Failure);
        using var json = JsonDocument.Parse(result.Payload);
        Assert.Equal(!omit, json.RootElement.TryGetProperty("temperature", out _));
        var client = AsShared(source, capability);
        Assert.Equal(omit, AgentProviderModelParameterPolicy.ShouldOmitTemperature(client, client.DefaultModel));
    }

    [Theory]
    [InlineData("gpt-5.6-sol", "low")]
    [InlineData("gpt-5.6-sol", "max")]
    [InlineData("gpt-5.4-mini", "high")]
    [InlineData("gpt-5.4-mini", "none")]
    public void Source_capability_survives_wire_and_client_override(string model, string token) {
        var source = CreateProvider(RuntimeKind.OpenAi, model);
        var wire = SharedProviderThinkingCapabilityMapper.ToCatalog(source, model);
        var roundTrip = JsonSerializer.Deserialize<SharedProviderThinkingCapability>(
            JsonSerializer.Serialize(wire, SharedProviderProtocolJson.Options), SharedProviderProtocolJson.Options)!;
        var client = AsShared(source, roundTrip);
        var effort = AgentThinkingEffortConfiguration.Read(
            $$$"""{"modelParameters":{"reasoningEffort":"{{{token}}}"}}""", "agent");

        Assert.Equal(AgentThinkingEffortPolicy.ResolveCapability(source, model).AllowedEfforts,
            AgentThinkingEffortPolicy.ResolveCapability(client, client.DefaultModel).AllowedEfforts);
        Assert.Equal(effort, AgentThinkingEffortPolicy.ResolveEffectiveEffort(client, client.DefaultModel,
            AgentThinkingEffortPolicy.WriteAgentOverride("{}", effort)));
    }

    [Fact]
    public void Discovered_ollama_alias_does_not_use_client_openai_family_guessing() {
        var source = CreateProvider(RuntimeKind.Ollama, "my-local-alias") with {
            ModelThinkingEffortCapabilities = [
                AgentThinkingEffortPolicy.CreateDiscoveredCapability("my-local-alias", "gptoss",
                    AgentThinkingEffortSupportStatus.Supported)]
        };
        var capability = SharedProviderThinkingCapabilityMapper.ToCatalog(source, source.DefaultModel);
        Assert.Equal([SharedProviderReasoningEffort.Low, SharedProviderReasoningEffort.Medium, SharedProviderReasoningEffort.High],
            capability.AllowedEfforts);
        Assert.Equal(AgentThinkingEffortControlMode.EffortLevels,
            AgentThinkingEffortPolicy.ResolveCapability(AsShared(source, capability), "opaque-model").ControlMode);
    }

    [Fact]
    public void Source_default_is_displayed_but_not_sent_as_a_client_override() {
        var source = CreateProvider(RuntimeKind.OpenAi, "gpt-5.6-sol") with {
            ConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault("{}", AgentReasoningEffortLevel.High)
        };
        var client = AsShared(source, SharedProviderThinkingCapabilityMapper.ToCatalog(source, source.DefaultModel));
        Assert.Equal(AgentReasoningEffortLevel.High, AgentThinkingEffortPolicy.ResolveProviderDefault(client, client.DefaultModel));
        Assert.Null(AgentThinkingEffortPolicy.ResolveEffectiveEffort(client, client.DefaultModel, "{}"));
    }

    [Theory]
    [InlineData(SharedProviderRelayOperation.ChatCompletions)]
    [InlineData(SharedProviderRelayOperation.Responses)]
    public void Per_request_override_wins_and_omission_uses_current_source_default(SharedProviderRelayOperation operation) {
        var capability = Levels(SharedProviderReasoningEffort.High);
        foreach (var token in new[] { "low", "high", "low", "high" }) {
            var result = SharedProviderRelayThinkingPolicy.Apply(operation, Payload(operation, token), capability);
            Assert.Null(result.Failure);
            Assert.True(result.IsOverride);
            Assert.Equal(token, ReadEffort(operation, result.Payload));
        }
        var inherited = SharedProviderRelayThinkingPolicy.Apply(operation, Payload(operation, null),
            capability with { DefaultEffort = SharedProviderReasoningEffort.Medium });
        Assert.Null(inherited.Failure);
        Assert.False(inherited.IsOverride);
        Assert.Equal("medium", ReadEffort(operation, inherited.Payload));
        Assert.Equal(SharedProviderReasoningEffort.High, capability.DefaultEffort);
    }

    [Theory]
    [InlineData(SharedProviderThinkingSupport.Unknown)]
    [InlineData(SharedProviderThinkingSupport.Unsupported)]
    public void Unsupported_and_unknown_reject_explicit_override(SharedProviderThinkingSupport support) {
        var capability = new SharedProviderThinkingCapability(support, SharedProviderThinkingControl.Unspecified, [], null);
        var result = SharedProviderRelayThinkingPolicy.Apply(
            SharedProviderRelayOperation.ChatCompletions, Payload(SharedProviderRelayOperation.ChatCompletions, "high"), capability);
        Assert.Equal(SharedProviderFailureCategory.Validation, result.Failure!.Category);
        Assert.True(result.Payload.IsEmpty);
        var client = AsShared(CreateProvider(RuntimeKind.OpenAi, "gpt-5.6-sol"), capability);
        Assert.Throws<InvalidOperationException>(() => AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            client, client.DefaultModel, AgentThinkingEffortPolicy.WriteAgentOverride("{}", AgentReasoningEffortLevel.High)));
    }

    [Fact]
    public void Missing_metadata_never_infers_reasoning_from_a_known_model_name() {
        var client = AsShared(CreateProvider(RuntimeKind.OpenAi, "gpt-5.6-sol"), Levels(null)) with {
            DefaultModel = "gpt-5.6-sol", ModelThinkingEffortCapabilities = []
        };
        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown,
            AgentThinkingEffortPolicy.ResolveCapability(client, client.DefaultModel).Status);
    }

    [Fact]
    public void Capability_changes_invalidate_prepared_agent_cache() {
        var provider = AsShared(CreateProvider(RuntimeKind.OpenAi, "gpt-5.6-sol"), Levels(null));
        var before = CanDoItAll.AgentFramework.Core.ProviderConfigurationFingerprintFactory.Create(provider);
        var changed = provider with {
            ModelThinkingEffortCapabilities = [provider.ModelThinkingEffortCapabilities[0] with {
                AllowedEfforts = [AgentReasoningEffortLevel.Low]
            }]
        };
        Assert.NotEqual(before, CanDoItAll.AgentFramework.Core.ProviderConfigurationFingerprintFactory.Create(changed));
    }

    [Fact]
    public void Boolean_controls_and_invalid_defaults_are_not_silently_downgraded() {
        var boolean = new SharedProviderThinkingCapability(SharedProviderThinkingSupport.Supported,
            SharedProviderThinkingControl.BooleanToggle, [SharedProviderReasoningEffort.None, SharedProviderReasoningEffort.Medium], null);
        foreach (var token in new[] { "none", "medium" }) {
            Assert.Null(SharedProviderRelayThinkingPolicy.Apply(SharedProviderRelayOperation.ChatCompletions,
                Payload(SharedProviderRelayOperation.ChatCompletions, token), boolean).Failure);
        }
        Assert.NotNull(SharedProviderRelayThinkingPolicy.Apply(SharedProviderRelayOperation.ChatCompletions,
            Payload(SharedProviderRelayOperation.ChatCompletions, "high"), boolean).Failure);
        Assert.Equal(SharedProviderFailureCategory.Unavailable, SharedProviderRelayThinkingPolicy.Apply(
            SharedProviderRelayOperation.ChatCompletions, Payload(SharedProviderRelayOperation.ChatCompletions, null),
            boolean with { DefaultEffort = SharedProviderReasoningEffort.High }).Failure!.Category);
    }

    [Fact]
    public void Invalid_wire_capability_rejects_duplicates_and_numeric_enum_values() {
        Assert.Throws<JsonException>(() => (Levels(null) with {
            AllowedEfforts = [SharedProviderReasoningEffort.Low, SharedProviderReasoningEffort.Low]
        }).Validate());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SharedProviderThinkingCapability>(
            """{"support":2,"control":"effortLevels","allowedEfforts":["low"],"defaultEffort":null}""",
            SharedProviderProtocolJson.Options));
    }

    [Fact]
    public void Main_model_suggestions_exclude_snapshots_and_obsolete_models() {
        Assert.True(OpenAiModelSuggestions.IsMainModel("gpt-5.6-sol"));
        Assert.True(OpenAiModelSuggestions.IsMainModel("gpt-5.4-mini"));
        Assert.False(OpenAiModelSuggestions.IsMainModel("gpt-3.5-turbo"));
        Assert.False(OpenAiModelSuggestions.IsMainModel("gpt-5.4-2026-03-05"));
        Assert.False(OpenAiModelSuggestions.IsMainModel("e2e-secondary-model"));
    }

    private static RuntimeProvider CreateProvider(RuntimeKind kind, string model) => new(
        Guid.NewGuid(), "Source", kind, "https://provider.invalid/v1", string.Empty, model,
        ProviderTransportKind.ChatCompletions, true, true, true, true, false, "{}", "", "", null, [model]);

    private static RuntimeProvider AsShared(RuntimeProvider source, SharedProviderThinkingCapability capability) => source with {
        Kind = RuntimeKind.OpenAi,
        DefaultModel = "opaque-model",
        ConfigurationJson = "{}",
        CredentialBinding = new ProviderCredentialBinding(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
            ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
        ModelThinkingEffortCapabilities = [SharedProviderThinkingCapabilityMapper.ToRuntime("opaque-model", capability)]
    };

    private static SharedProviderThinkingCapability Levels(SharedProviderReasoningEffort? sourceDefault) => new(
        SharedProviderThinkingSupport.Supported, SharedProviderThinkingControl.EffortLevels,
        [SharedProviderReasoningEffort.Low, SharedProviderReasoningEffort.Medium, SharedProviderReasoningEffort.High], sourceDefault);

    private static byte[] Payload(SharedProviderRelayOperation operation, string? token) => Encoding.UTF8.GetBytes(
        token is null ? """{"model":"real-model"}""" :
        operation == SharedProviderRelayOperation.Responses
            ? $$$"""{"model":"real-model","reasoning":{"effort":"{{{token}}}"}}"""
            : $$$"""{"model":"real-model","reasoning_effort":"{{{token}}}"}""");

    private static string? ReadEffort(SharedProviderRelayOperation operation, ReadOnlyMemory<byte> payload) {
        using var json = JsonDocument.Parse(payload);
        return operation == SharedProviderRelayOperation.Responses
            ? json.RootElement.GetProperty("reasoning").GetProperty("effort").GetString()
            : json.RootElement.GetProperty("reasoning_effort").GetString();
    }
}
