using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderModelThinkingConfigurationTests {
    private const string CustomModel = "my-work-model:latest";
    private const string ManualConfiguration = """
        {"modelThinkingEffortOverrides":[{"model":"my-work-model:latest","status":"supported","controlMode":"effortLevels","allowedEfforts":["low","high"]}],"timeoutSeconds":91}
        """;

    [Fact]
    public void Per_model_default_is_used_locally_and_published_without_freezing_client_inheritance() {
        var configuration = ProviderModelThinkingConfiguration.Write(ManualConfiguration, CustomModel,
            new ProviderModelThinkingOverride(CustomModel, AgentThinkingEffortSupportStatus.Supported,
                AgentThinkingEffortControlMode.EffortLevels, [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High], AgentReasoningEffortLevel.Low));
        var source = CreateProvider(configuration);
        Assert.Equal(AgentReasoningEffortLevel.Low, AgentThinkingEffortPolicy.ResolveEffectiveEffort(source, CustomModel, "{}"));
        var catalog = SharedProviderThinkingCapabilityMapper.ToCatalog(source, CustomModel);
        Assert.Equal(SharedProviderReasoningEffort.Low, catalog.DefaultEffort);
        var client = source with {
            CredentialBinding = new ProviderCredentialBinding(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ConfigurationJson = "{}",
            ModelThinkingEffortCapabilities = [SharedProviderThinkingCapabilityMapper.ToRuntime(CustomModel, catalog)]
        };
        Assert.Equal(AgentReasoningEffortLevel.Low, AgentThinkingEffortPolicy.ResolveProviderDefault(client, CustomModel));
        Assert.Null(AgentThinkingEffortPolicy.ResolveEffectiveEffort(client, CustomModel, "{}"));
        Assert.Equal(AgentReasoningEffortLevel.High, AgentThinkingEffortPolicy.ResolveEffectiveEffort(client, CustomModel,
            AgentThinkingEffortPolicy.WriteAgentOverride("{}", AgentReasoningEffortLevel.High)));
    }

    [Fact]
    public void Reset_removes_only_the_selected_override_and_recovers_discovery() {
        var configuration = ProviderModelThinkingConfiguration.Write(ManualConfiguration, CustomModel, null);
        Assert.Empty(ProviderModelThinkingConfiguration.Read(configuration));
        Assert.Contains("timeoutSeconds", configuration);
        var provider = CreateProvider(configuration) with {
            ModelThinkingEffortCapabilities = [AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                CustomModel, "gptoss", AgentThinkingEffortSupportStatus.Supported)]
        };
        var capability = AgentThinkingEffortPolicy.ResolveCapability(provider, CustomModel);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, capability.Source);
        Assert.Equal([AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.Medium, AgentReasoningEffortLevel.High], capability.AllowedEfforts);
    }

    [Fact]
    public void Shared_import_never_uses_local_override_to_guess_missing_source_metadata() {
        var client = CreateProvider(ManualConfiguration) with {
            CredentialBinding = new ProviderCredentialBinding(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid())
        };
        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown, AgentThinkingEffortPolicy.ResolveCapability(client, CustomModel).Status);
    }

    [Fact]
    public void Default_outside_allowed_efforts_and_openai_boolean_controls_are_rejected() {
        var value = new ProviderModelThinkingOverride(CustomModel, AgentThinkingEffortSupportStatus.Supported,
            AgentThinkingEffortControlMode.EffortLevels, [AgentReasoningEffortLevel.Low], AgentReasoningEffortLevel.High);
        Assert.Throws<InvalidOperationException>(() => ProviderModelThinkingConfiguration.Write("{}", CustomModel, value));
        var boolean = ProviderModelThinkingConfiguration.Write("{}", CustomModel, value with {
            ControlMode = AgentThinkingEffortControlMode.BooleanToggle,
            AllowedEfforts = [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Medium], DefaultEffort = null
        });
        Assert.Throws<InvalidOperationException>(() => ProviderModelThinkingConfiguration.ValidateForProvider(boolean,
            CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi, ProviderTransportKind.Responses, ProviderProfilePurpose.Chat));
    }

    [Fact]
    public void Manual_configuration_overrides_discovery_and_survives_health_refresh() {
        var service = new CanDoItAll.AgentFramework.Core.ProviderProfileService();
        var provider = CreateProvider(ManualConfiguration) with {
            ModelThinkingEffortCapabilities = [AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                CustomModel, "unknown", AgentThinkingEffortSupportStatus.Unsupported)]
        };
        var refreshed = service.ApplyHealthResult(provider, new ProviderHealthResult(true, "Healthy", [CustomModel]) {
            ModelThinkingEffortCapabilities = [AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                CustomModel, "unknown", AgentThinkingEffortSupportStatus.Unsupported)]
        }, DateTimeOffset.UtcNow);
        var capability = AgentThinkingEffortPolicy.ResolveCapability(refreshed, CustomModel);
        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, capability.Status);
        Assert.Equal([AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High], capability.AllowedEfforts);
        Assert.Equal(ManualConfiguration, refreshed.ConfigurationJson);
        Assert.Equal(AgentReasoningEffortLevel.High, AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            refreshed, CustomModel, AgentThinkingEffortPolicy.WriteAgentOverride("{}", AgentReasoningEffortLevel.High)));
    }

    [Fact]
    public void Manual_configuration_is_published_and_enforced_for_shared_model() {
        var wire = SharedProviderThinkingCapabilityMapper.ToCatalog(CreateProvider(ManualConfiguration), CustomModel);
        Assert.Equal(SharedProviderThinkingSupport.Supported, wire.Support);
        Assert.Equal([SharedProviderReasoningEffort.Low, SharedProviderReasoningEffort.High], wire.AllowedEfforts);
        var client = CreateProvider("{}") with {
            CredentialBinding = new ProviderCredentialBinding(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ModelThinkingEffortCapabilities = [SharedProviderThinkingCapabilityMapper.ToRuntime(CustomModel, wire)]
        };
        Assert.True(AgentThinkingEffortPolicy.IsOverrideSupported(client, CustomModel, AgentReasoningEffortLevel.High));
        Assert.False(AgentThinkingEffortPolicy.IsOverrideSupported(client, CustomModel, AgentReasoningEffortLevel.Medium));
    }

    [Fact]
    public void Explicit_unsupported_overrides_a_known_model_default() {
        var provider = CreateProvider("""
            {"modelThinkingEffortOverrides":[{"model":"gpt-5.6-sol","status":"unsupported","controlMode":"unspecified","allowedEfforts":[]}]}
            """) with { Kind = CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi, DefaultModel = "gpt-5.6-sol" };
        Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported,
            AgentThinkingEffortPolicy.ResolveCapability(provider, provider.DefaultModel).Status);
    }

    [Theory]
    [InlineData("""[{"model":"x","status":"supported","controlMode":"effortLevels","allowedEfforts":[]}]""")]
    [InlineData("""[{"model":"x","status":"unsupported","controlMode":"unspecified","allowedEfforts":["low"]}]""")]
    [InlineData("""[{"model":"x","status":"supported","controlMode":"booleanToggle","allowedEfforts":["high"]}]""")]
    [InlineData("""[{"model":"x","status":"supported","controlMode":"effortLevels","allowedEfforts":["low","low"]}]""")]
    [InlineData("""[{"model":"x","status":"unsupported","controlMode":"unspecified","allowedEfforts":[]},{"model":"X","status":"unsupported","controlMode":"unspecified","allowedEfforts":[]}]""")]
    [InlineData("""[{"model":"x","status":0,"controlMode":"effortLevels","allowedEfforts":["low"]}]""")]
    public void Save_rejects_invalid_manual_capabilities(string settings) {
        var service = new CanDoItAll.AgentFramework.Core.ProviderProfileService();
        var editor = service.CreateEditor(CreateProvider("{\"modelThinkingEffortOverrides\":" + settings + "}"));
        Assert.Throws<ProviderProfileValidationException>(() => service.CreateProfile(editor));
    }

    private static CanDoItAll.AgentFramework.Models.ProviderProfile CreateProvider(string configuration) => new(
        Guid.NewGuid(), "Custom Ollama", CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
        "http://localhost:11434", "", CustomModel, ProviderTransportKind.ChatCompletions,
        true, true, true, true, false, configuration, "", "", null, [CustomModel]);
}
