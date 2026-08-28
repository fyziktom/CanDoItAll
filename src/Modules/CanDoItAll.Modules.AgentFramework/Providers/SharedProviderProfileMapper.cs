using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

internal sealed class SharedProviderProfileMapper
{
    public AgentFrameworkProviderProfile Map(
        SharedProviderRuntimeProfileMaterializationResult materialization)
    {
        ArgumentNullException.ThrowIfNull(materialization);
        var provider = materialization.Profile
            ?? throw new InvalidOperationException(
                "A shared-provider runtime profile cannot be projected from an invalid source graph.");
        var configurationJson = JsonSerializer.Serialize(new
        {
            timeoutSeconds = Math.Max(5, provider.TimeoutSeconds)
        });
        var allowedModels = provider.Models
            .Select(model => model.Id.Value)
            .ToArray();
        return new AgentFrameworkProviderProfile(
            provider.ProviderProfileId,
            provider.Name,
            provider.Kind,
            provider.BaseUri.AbsoluteUri,
            ProviderMetadata.CreateSecretReference(
                provider.SourceTokenSecretReferenceId),
            provider.DefaultModelId.Value,
            provider.Transport,
            materialization.IsAvailable,
            provider.SupportsStreaming,
            provider.SupportsTools,
            provider.PreferFrameworkManagedChatHistory,
            provider.SupportsBackgroundResponses,
            configurationJson,
            "Source-managed CanDoItAll shared provider.",
            materialization.Availability.ToString(),
            null,
            provider.Models.Where(model => model.IsSuggested).Select(model => model.Id.Value).ToArray(),
            provider.Purpose)
        {
            ConnectorPluginKey = provider.ConnectorPluginKey,
            CredentialBinding = new ProviderCredentialBinding(
                provider.SourceTokenSecretReferenceId,
                ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source,
                provider.SourceId),
            NetworkAccessPolicy = provider.NetworkPolicy switch
            {
                SharedProviderSourceNetworkPolicy.PublicOnly =>
                    ProviderNetworkAccessPolicy.PublicOnly,
                SharedProviderSourceNetworkPolicy.AllowPrivateNetwork =>
                    ProviderNetworkAccessPolicy.AllowPrivateNetwork,
                _ => throw new InvalidOperationException(
                    "The shared-provider source network policy is invalid.")
            },
            FeatureConstraints = new ProviderFeatureConstraints(
                provider.SupportsStructuredOutput,
                provider.SupportsVision,
                AllowsNativeTools: false,
                AllowsHostedMcp: false,
                AllowsServiceManagedHistory: false,
                AllowsCompaction: false,
                AllowsParallelFunctionTools: provider.SupportsParallelTools),
            ModelSelectionConstraint =
                new ProviderModelSelectionConstraint(allowedModels),
            IsPrivateProvider = provider.IsPrivateProvider,
            ModelThinkingEffortCapabilities = Array.AsReadOnly(provider.Models
                .Where(model => model.Thinking is not null)
                .Select(model => SharedProviderThinkingCapabilityMapper.ToRuntime(model.Id.Value, model.Thinking!)).ToArray()),
            ModelCatalog = Array.AsReadOnly(provider.Models
                .Select(model => new ProviderModelDisplayMetadata(model.Id.Value, model.DisplayName)).ToArray()),
            ModelPrices = Array.AsReadOnly(provider.Models
                .Where(model => model.Price is not null)
                .Select(model => SharedProviderPriceMapper.ToRuntime(model.Id.Value, model.Price!)).ToArray()),
            Tags = provider.Tags
        };
    }
}
