using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using RuntimeProvider = CanDoItAll.AgentFramework.Models.ProviderProfile;
using RuntimeKind = CanDoItAll.AgentFramework.Models.ProviderKind;

public static class SharedProviderThinkingCapabilityMapper {
    public static RuntimeProvider CreateSourceProvider(ProviderProfile profile) {
        if (!SharedProviderProfilePublicationMetadataReader.TryRead(profile, out var metadata, out var failure)) {
            throw new InvalidOperationException(failure);
        }
        return new RuntimeProvider(
            profile.Id, profile.Name, metadata.ProviderKind, profile.BaseUrl, string.Empty,
            profile.DefaultModel, metadata.Transport, profile.IsEnabled, profile.SupportsStreaming,
            profile.SupportsToolCalling, false, false, profile.ExtraSettingsJson, string.Empty,
            profile.LastHealthStatus ?? string.Empty, profile.LastHealthCheckAtUtc, metadata.Models, metadata.Purpose) {
            ModelThinkingEffortCapabilities = ProviderMetadata.ReadThinkingEffortCapabilities(profile.ExtraSettingsJson)
        };
    }

    public static SharedProviderThinkingCapability ToCatalog(RuntimeProvider provider, string model) {
        var capability = AgentThinkingEffortPolicy.ResolveCapability(provider, model);
        var supported = capability.Status == AgentThinkingEffortSupportStatus.Supported;
        var providerDefault = supported
            ? ProviderModelThinkingConfiguration.ReadDefault(provider.ConfigurationJson, model, provider.Kind == RuntimeKind.Ollama)
            : null;
        return new SharedProviderThinkingCapability(
            capability.Status switch {
                AgentThinkingEffortSupportStatus.Supported => SharedProviderThinkingSupport.Supported,
                AgentThinkingEffortSupportStatus.Unsupported => SharedProviderThinkingSupport.Unsupported,
                AgentThinkingEffortSupportStatus.Unknown => SharedProviderThinkingSupport.Unknown,
                _ => throw new ArgumentOutOfRangeException(nameof(capability))
            },
            capability.ControlMode switch {
                AgentThinkingEffortControlMode.BooleanToggle => SharedProviderThinkingControl.BooleanToggle,
                AgentThinkingEffortControlMode.EffortLevels => SharedProviderThinkingControl.EffortLevels,
                AgentThinkingEffortControlMode.Unspecified => SharedProviderThinkingControl.Unspecified,
                _ => throw new ArgumentOutOfRangeException(nameof(capability))
            },
            capability.AllowedEfforts.Select(ToCatalogEffort).ToArray(),
            providerDefault.HasValue ? ToCatalogEffort(providerDefault.Value) : null) {
            OmitTemperature = AgentProviderModelParameterPolicy.ShouldOmitTemperature(provider.Kind, model)
        }.Snapshot();
    }

    public static ProviderModelThinkingEffortCapability ToRuntime(string model, SharedProviderThinkingCapability thinking) {
        thinking.Validate();
        return new ProviderModelThinkingEffortCapability(
            model,
            thinking.Support switch {
                SharedProviderThinkingSupport.Supported => AgentThinkingEffortSupportStatus.Supported,
                SharedProviderThinkingSupport.Unsupported => AgentThinkingEffortSupportStatus.Unsupported,
                SharedProviderThinkingSupport.Unknown => AgentThinkingEffortSupportStatus.Unknown,
                _ => throw new ArgumentOutOfRangeException(nameof(thinking))
            },
            AgentThinkingEffortCapabilitySource.Defined,
            Array.AsReadOnly(thinking.AllowedEfforts.Select(ToRuntimeEffort).ToArray()),
            Summary: thinking.Support switch {
                SharedProviderThinkingSupport.Supported => "The source model supports the published thinking controls.",
                SharedProviderThinkingSupport.Unsupported => "The source model does not support configurable thinking.",
                _ => "The source has not verified this model's thinking capabilities."
            },
            ControlMode: thinking.Control switch {
                SharedProviderThinkingControl.BooleanToggle => AgentThinkingEffortControlMode.BooleanToggle,
                SharedProviderThinkingControl.EffortLevels => AgentThinkingEffortControlMode.EffortLevels,
                _ => AgentThinkingEffortControlMode.Unspecified
            }) {
            SourceDefaultEffort = thinking.DefaultEffort.HasValue ? ToRuntimeEffort(thinking.DefaultEffort.Value) : null,
            OmitTemperature = thinking.OmitTemperature
        };
    }

    private static SharedProviderReasoningEffort ToCatalogEffort(AgentReasoningEffortLevel effort) => effort switch {
        AgentReasoningEffortLevel.None => SharedProviderReasoningEffort.None,
        AgentReasoningEffortLevel.Minimal => SharedProviderReasoningEffort.Minimal,
        AgentReasoningEffortLevel.Low => SharedProviderReasoningEffort.Low,
        AgentReasoningEffortLevel.Medium => SharedProviderReasoningEffort.Medium,
        AgentReasoningEffortLevel.High => SharedProviderReasoningEffort.High,
        AgentReasoningEffortLevel.ExtraHigh => SharedProviderReasoningEffort.ExtraHigh,
        AgentReasoningEffortLevel.Max => SharedProviderReasoningEffort.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(effort))
    };

    private static AgentReasoningEffortLevel ToRuntimeEffort(SharedProviderReasoningEffort effort) => effort switch {
        SharedProviderReasoningEffort.None => AgentReasoningEffortLevel.None,
        SharedProviderReasoningEffort.Minimal => AgentReasoningEffortLevel.Minimal,
        SharedProviderReasoningEffort.Low => AgentReasoningEffortLevel.Low,
        SharedProviderReasoningEffort.Medium => AgentReasoningEffortLevel.Medium,
        SharedProviderReasoningEffort.High => AgentReasoningEffortLevel.High,
        SharedProviderReasoningEffort.ExtraHigh => AgentReasoningEffortLevel.ExtraHigh,
        SharedProviderReasoningEffort.Max => AgentReasoningEffortLevel.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(effort))
    };
}
