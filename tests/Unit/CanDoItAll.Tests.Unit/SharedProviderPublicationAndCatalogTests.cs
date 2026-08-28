using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using PersistedProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using ProviderAdministrationEditorModel = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfileEditorModel;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class SharedProviderPublicationAndCatalogTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 24, 23, 45, 0, TimeSpan.Zero);

    private static readonly SharedProviderSourceInstanceId SourceInstanceId =
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    [Fact]
    public void SharedThinkingEffort_Publication_copies_support_default_and_main_suggestions() {
        var profile = CreateProfile(defaultModel: "gpt-5.6-sol", suggestedModels: ["gpt-4.1", "gpt-3.5-turbo"]);
        profile.ExtraSettingsJson = AgentThinkingEffortPolicy.WriteProviderDefault(
            profile.ExtraSettingsJson, AgentReasoningEffortLevel.Low);
        var source = CreateProjectionSource(profile, CreatePolicy(), isPublished: true);
        var before = SharedProviderCatalogProjector.Project(SourceInstanceId, [source]);
        var models = before.Catalog.Providers[0].Models;
        var thinking = models.Single(model => model.DisplayName == "gpt-5.6-sol").Thinking!;
        Assert.Equal(SharedProviderReasoningEffort.Low, thinking.DefaultEffort);
        Assert.Contains(SharedProviderReasoningEffort.Max, thinking.AllowedEfforts);
        Assert.Equal(SharedProviderThinkingSupport.Unsupported,
            models.Single(model => model.DisplayName == "gpt-4.1").Thinking!.Support);
        Assert.False(models.Single(model => model.DisplayName == "gpt-3.5-turbo").IsSuggested);
        Assert.True(models.Single(model => model.DisplayName == "gpt-5.6-sol").IsSuggested);

        profile.ExtraSettingsJson = AgentThinkingEffortPolicy.WriteProviderDefault(
            profile.ExtraSettingsJson, AgentReasoningEffortLevel.High);
        var after = SharedProviderCatalogProjector.Project(SourceInstanceId, [source]);
        Assert.NotEqual(before.EntityTag, after.EntityTag);
        Assert.Equal(before.Catalog.Providers[0].DefaultModelId, after.Catalog.Providers[0].DefaultModelId);
        var publication = after.Catalog.Providers[0];
        var changedSuggestions = publication with { Models = publication.Models.Select(model => model with { IsSuggested = true }).ToArray() };
        Assert.NotEqual(publication.Revision, SharedProviderCanonicalRevision.ComputePublication(changedSuggestions));
    }

    [Fact]
    public void PublicationDoesNotInventOpenAiModelsWithoutPersistedSuggestions() {
        var profile = CreateProfile(defaultModel: "custom-default");
        var decision = CreatePolicy().Evaluate(profile, CreateManifest(profile.ConnectorPluginKey), true);

        Assert.True(decision.IsEligible);
        Assert.Equal("custom-default", decision.Models[0].UpstreamModelId);
        Assert.Equal("custom-default", Assert.Single(decision.Models).UpstreamModelId);
    }

    [Theory]
    [InlineData(ProviderConnectorKeys.OpenAi, AgentFrameworkProviderKind.OpenAi, ProviderProfilePurpose.Chat)]
    [InlineData(ProviderConnectorKeys.OpenAi, AgentFrameworkProviderKind.OpenAi, ProviderProfilePurpose.ImageGeneration)]
    [InlineData(OllamaProviderAdministrationConnector.PluginKey, AgentFrameworkProviderKind.Ollama, ProviderProfilePurpose.Chat)]
    public void PublicationModelSetMatchesSourceRuntimeMapper(string connector, AgentFrameworkProviderKind kind,
        ProviderProfilePurpose purpose) {
        var profile = CreateProfile(connectorPluginKey: connector, providerKind: kind, purpose: purpose,
            defaultModel: "custom-default", suggestedModels: ["custom-secondary", "CUSTOM-SECONDARY"]);
        var mapper = new ProviderProfileMapper(new ProviderAdministrationConnectorCatalog([]), new ProviderProfileService());
        var runtime = mapper.Map(profile);

        Assert.True(SharedProviderProfilePublicationMetadataReader.TryRead(profile, out var metadata, out var failure), failure);
        Assert.Equal(runtime.SuggestedModels.Order(), metadata.Models.Order());
        Assert.Equal("custom-default", metadata.Models[0]);
        Assert.Equal(1, metadata.Models.Count(model => model.Equals("custom-secondary", StringComparison.OrdinalIgnoreCase)));
        if (kind == AgentFrameworkProviderKind.Ollama || purpose == ProviderProfilePurpose.ImageGeneration) {
            Assert.Equal(["custom-default", "custom-secondary"], metadata.Models);
        }
    }

    [Fact]
    public void PublicationRejectsConfiguredCatalogBeyondProtocolModelLimit() {
        var profile = CreateProfile(defaultModel: "custom-default");
        var configuration = System.Text.Json.Nodes.JsonNode.Parse(profile.ExtraSettingsJson)!.AsObject();
        configuration[ProviderProfileMetadataPropertyNames.SuggestedModels] =
            JsonSerializer.SerializeToNode(Enumerable.Range(1, 128).Select(index => $"custom-{index}"));
        profile.ExtraSettingsJson = configuration.ToJsonString();
        var decision = CreatePolicy().Evaluate(profile, CreateManifest(profile.ConnectorPluginKey), true);

        Assert.Equal(SharedProviderPublicationEligibilityCode.MetadataInvalid, decision.Code);
        Assert.Contains("at most", decision.SanitizedReason);
    }

    [Fact]
    public void PublicationPricesMatchEffectiveSourcePricesWithoutPersistedPricing() {
        var profile = CreateProfile();
        var mapper = new ProviderProfileMapper(new ProviderAdministrationConnectorCatalog([]), new ProviderProfileService());
        var runtime = mapper.Map(profile);
        var projection = SharedProviderCatalogProjector.Project(SourceInstanceId,
            [CreateProjectionSource(profile, CreatePolicy(), isPublished: true)]);

        foreach (var model in Assert.Single(projection.Catalog.Providers).Models) {
            var sourcePrice = runtime.ModelPrices.SingleOrDefault(price => price.Model == model.DisplayName);
            Assert.Equal(sourcePrice?.InputPerMillionTokensUsd, model.Price?.InputPerMillionTokensUsd);
            Assert.Equal(sourcePrice?.OutputPerMillionTokensUsd, model.Price?.OutputPerMillionTokensUsd);
        }
    }

    [Fact]
    public void SourceMetadataNamesPreserveModelNamesAndDistinctRoutingIds() {
        var profile = CreateProfile(defaultModel: "model-alpha", suggestedModels: ["model-beta"]);
        var projection = SharedProviderCatalogProjector.Project(
            SourceInstanceId,
            [CreateProjectionSource(profile, CreatePolicy(), isPublished: true)]);
        var publication = Assert.Single(projection.Catalog.Providers);

        Assert.Equal(new[] { "model-alpha", "model-beta" }.Order(),
            publication.Models.Select(model => model.DisplayName).Order());
        Assert.All(publication.Models, model => Assert.StartsWith("sp1.", model.Id.Value));
    }

    [Fact]
    public void SourceMetadataPublishesPrivateFlagAndExactPricesWithoutDriverDefaults() {
        var profile = CreateProfile(defaultModel: "model-alpha");
        profile.ExtraSettingsJson = ProviderPricingMetadata.Write(profile.ExtraSettingsJson, true,
            [new ProviderModelTokenPrice("model-alpha", 1.23m, 0m, 4.56m)]);
        var projection = SharedProviderCatalogProjector.Project(
            SourceInstanceId,
            [CreateProjectionSource(profile, CreatePolicy(), isPublished: true)]);
        using var document = JsonDocument.Parse(SharedProviderProtocolJson.SerializeCatalog(projection.Catalog));
        var publication = document.RootElement.GetProperty("providers")[0];

        Assert.True(publication.GetProperty("isPrivateProvider").GetBoolean());
        var price = publication.GetProperty("models").EnumerateArray()
            .Single(model => model.GetProperty("displayName").GetString() == "model-alpha").GetProperty("price");
        Assert.Equal(1.23m, price.GetProperty("inputPerMillionTokensUsd").GetDecimal());
        Assert.Equal(0m, price.GetProperty("cachedInputPerMillionTokensUsd").GetDecimal());
        Assert.Equal(4.56m, price.GetProperty("outputPerMillionTokensUsd").GetDecimal());
        Assert.Single(publication.GetProperty("models").EnumerateArray(),
            model => model.TryGetProperty("price", out var item) && item.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public void SourceMetadataPriceChangeInvalidatesRevisionWithoutChangingRoute() {
        var profile = CreateProfile(defaultModel: "model-alpha");
        var source = CreateProjectionSource(profile, CreatePolicy(), isPublished: true);
        profile.ExtraSettingsJson = ProviderPricingMetadata.Write(profile.ExtraSettingsJson, false,
            [new ProviderModelTokenPrice("model-alpha", 1m, 0m, 2m)]);
        var before = SharedProviderCatalogProjector.Project(SourceInstanceId, [source]);
        profile.ExtraSettingsJson = ProviderPricingMetadata.Write(profile.ExtraSettingsJson, true,
            [new ProviderModelTokenPrice("model-alpha", 9m, 0m, 2m)]);
        var after = SharedProviderCatalogProjector.Project(SourceInstanceId, [source]);

        Assert.NotEqual(before.EntityTag, after.EntityTag);
        Assert.Equal(before.Catalog.Providers[0].DefaultModelId, after.Catalog.Providers[0].DefaultModelId);
    }

    [Fact]
    public void EligibleChatProfileIntersectsTypedMetadataProfileAndRelayCapabilities()
    {
        var profile = CreateProfile(
            transport: ProviderTransportKind.Responses,
            supportsStreaming: true,
            supportsTools: true,
            supportsStructuredOutput: true,
            supportsVision: true);
        var policy = CreatePolicy(CreateChatSupport(
            supportsStreaming: true,
            supportsTools: true,
            supportsParallelTools: true,
            supportsStructuredOutput: true,
            supportsVision: true));

        using var metadataDocument = JsonDocument.Parse(
            profile.ExtraSettingsJson);
        Assert.Equal(
            AgentFrameworkProviderKind.OpenAi.ToString(),
            metadataDocument.RootElement
                .GetProperty(ProviderProfileMetadataPropertyNames.ProviderKind)
                .GetString());
        Assert.Equal(
            ProviderTransportKind.Responses.ToString(),
            metadataDocument.RootElement
                .GetProperty(ProviderProfileMetadataPropertyNames.ProviderTransport)
                .GetString());
        Assert.Equal(
            ProviderProfilePurpose.Chat.ToString(),
            metadataDocument.RootElement
                .GetProperty(ProviderProfileMetadataPropertyNames.ProviderPurpose)
                .GetString());
        Assert.Empty(metadataDocument.RootElement
            .GetProperty(ProviderProfileMetadataPropertyNames.SuggestedModels)
            .EnumerateArray());

        var decision = policy.Evaluate(profile, CreateManifest(profile.ConnectorPluginKey), requiredSecretExists: true);

        Assert.True(decision.IsEligible);
        Assert.Equal(SharedProviderPublicationEligibilityCode.Eligible, decision.Code);
        Assert.Equal(SharedProviderPurpose.Chat, decision.Purpose);
        Assert.Equal(SharedProviderTransport.OpenAiCompatible, decision.Transport);
        Assert.Equal(
            [
                SharedProviderCapability.Responses,
                SharedProviderCapability.Streaming,
                SharedProviderCapability.FunctionTools,
                SharedProviderCapability.ParallelFunctionTools,
                SharedProviderCapability.StructuredOutput,
                SharedProviderCapability.VisionInput
            ],
            decision.Models.Single(model => model.UpstreamModelId == profile.DefaultModel).Capabilities);
    }

    [Fact]
    public void DisabledProfileIsIneligible()
    {
        var profile = CreateProfile(isEnabled: false);

        var decision = CreatePolicy().Evaluate(
            profile,
            CreateManifest(profile.ConnectorPluginKey),
            requiredSecretExists: true);

        Assert.False(decision.IsEligible);
        Assert.Equal(SharedProviderPublicationEligibilityCode.ProfileDisabled, decision.Code);
        Assert.Contains("enabled", decision.SanitizedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SyntheticImportedAndRuntimeFallbackProfilesAreIneligible()
    {
        var policy = CreatePolicy();
        var synthetic = CreateProfile(connectorPluginKey: ScenarioHarnessProviderAdministrationConnector.PluginKey);
        var imported = CreateProfile(
            connectorPluginKey: SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey);
        var fallback = CreateProfile(id: ProviderProfileWellKnownIds.RuntimeFallbackOllama);

        var syntheticDecision = policy.Evaluate(
            synthetic,
            CreateManifest(synthetic.ConnectorPluginKey),
            requiredSecretExists: true);
        var importedDecision = policy.Evaluate(
            imported,
            CreateManifest(imported.ConnectorPluginKey),
            requiredSecretExists: true);
        var fallbackDecision = policy.Evaluate(
            fallback,
            CreateManifest(fallback.ConnectorPluginKey),
            requiredSecretExists: true);

        Assert.Equal(SharedProviderPublicationEligibilityCode.NonProductionProfile, syntheticDecision.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.NonProductionProfile, importedDecision.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.NonProductionProfile, fallbackDecision.Code);
    }

    [Fact]
    public void UnknownOrNonExecutionConnectorIsIneligible()
    {
        var profile = CreateProfile();
        var policy = CreatePolicy();
        var invalidSchema = CreateProfile();
        invalidSchema.ConfigSchemaVersion = "0.9";
        var invalidEndpoint = CreateProfile();
        invalidEndpoint.BaseUrl = "/relative";

        var missingManifest = policy.Evaluate(profile, connectorManifest: null, requiredSecretExists: true);
        var nonExecutionManifest = policy.Evaluate(
            profile,
            CreateManifest(profile.ConnectorPluginKey, ConnectorManifestCapability.AgentExposure),
            requiredSecretExists: true);
        var invalidSchemaDecision = policy.Evaluate(
            invalidSchema,
            CreateManifest(invalidSchema.ConnectorPluginKey),
            requiredSecretExists: true);
        var invalidEndpointDecision = policy.Evaluate(
            invalidEndpoint,
            CreateManifest(invalidEndpoint.ConnectorPluginKey),
            requiredSecretExists: true);

        Assert.Equal(SharedProviderPublicationEligibilityCode.ConnectorUnavailable, missingManifest.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.ConnectorUnavailable, nonExecutionManifest.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.ProfileInvalid, invalidSchemaDecision.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.ProfileInvalid, invalidEndpointDecision.Code);
    }

    [Fact]
    public void MissingRelayDescriptorIsIneligibleWithActionableReason()
    {
        var profile = CreateProfile();
        var policy = CreatePolicy(includeDescriptor: false);
        var testDescriptorPolicy = CreatePolicy(
            classification: SharedProviderRelayAdapterClassification.Test);

        var decision = policy.Evaluate(profile, CreateManifest(profile.ConnectorPluginKey), requiredSecretExists: true);
        var testDescriptorDecision = testDescriptorPolicy.Evaluate(
            profile,
            CreateManifest(profile.ConnectorPluginKey),
            requiredSecretExists: true);

        Assert.False(decision.IsEligible);
        Assert.Equal(SharedProviderPublicationEligibilityCode.RelayUnsupported, decision.Code);
        Assert.Equal(
            SharedProviderPublicationEligibilityCode.RelayUnsupported,
            testDescriptorDecision.Code);
        Assert.Contains(profile.ConnectorPluginKey, decision.SanitizedReason, StringComparison.Ordinal);
        Assert.Contains("chat", decision.SanitizedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedMetadataMissingSecretOrInvalidModelsAreIneligible()
    {
        var policy = CreatePolicy();
        var malformed = CreateProfile(extraSettingsJson: "{not-json");
        var missingSecret = CreateProfile();
        missingSecret.ApiKeySecretId = null;
        var emptySecret = CreateProfile(apiKeySecretId: Guid.Empty);
        var numericClassification = CreateProfile(extraSettingsJson: JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                [ProviderProfileMetadataPropertyNames.ProviderKind] = "0",
                [ProviderProfileMetadataPropertyNames.ProviderTransport] = ProviderTransportKind.Responses.ToString(),
                [ProviderProfileMetadataPropertyNames.ProviderPurpose] = ProviderProfilePurpose.Chat.ToString(),
                [ProviderProfileMetadataPropertyNames.SuggestedModels] = new[] { "gpt-5-mini" }
            }));
        var invalidModels = CreateProfile(extraSettingsJson: CreateMetadata(
            AgentFrameworkProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            ["valid-model", " invalid-model "]));

        var malformedDecision = policy.Evaluate(
            malformed,
            CreateManifest(malformed.ConnectorPluginKey),
            requiredSecretExists: true);
        var secretDecision = policy.Evaluate(
            missingSecret,
            CreateManifest(missingSecret.ConnectorPluginKey),
            requiredSecretExists: false);
        var emptySecretDecision = policy.Evaluate(
            emptySecret,
            CreateManifest(emptySecret.ConnectorPluginKey),
            requiredSecretExists: false);
        var modelDecision = policy.Evaluate(
            invalidModels,
            CreateManifest(invalidModels.ConnectorPluginKey),
            requiredSecretExists: true);
        var numericDecision = policy.Evaluate(
            numericClassification,
            CreateManifest(numericClassification.ConnectorPluginKey),
            requiredSecretExists: true);

        Assert.Equal(SharedProviderPublicationEligibilityCode.MetadataInvalid, malformedDecision.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.SecretReferenceMissing, secretDecision.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.SecretReferenceMissing, emptySecretDecision.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.MetadataInvalid, modelDecision.Code);
        Assert.Equal(SharedProviderPublicationEligibilityCode.MetadataInvalid, numericDecision.Code);
    }

    [Fact]
    public void ImageProfileRequiresImageRelayAndAdvertisesOnlyImageCapabilities()
    {
        var profile = CreateProfile(
            connectorPluginKey: ComfyUiProviderAdministrationConnector.PluginKey,
            providerKind: AgentFrameworkProviderKind.ComfyUi,
            transport: ProviderTransportKind.ChatCompletions,
            purpose: ProviderProfilePurpose.ImageGeneration,
            defaultModel: "gpt-image-1",
            suggestedModels: ["gpt-image-1", "brand-art-v2"],
            supportsStreaming: true,
            supportsTools: true,
            supportsStructuredOutput: true,
            supportsVision: true);
        var support = new SharedProviderRelaySupportDescriptor(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ImageGenerations
            },
            SharedProviderStreamingMode.None,
            supportsFunctionTools: false,
            supportsParallelFunctionTools: false,
            supportsStructuredOutput: false,
            supportsVisionInput: false,
            supportsBase64Images: true,
            maximumRequestBytes: 1_000_000,
            maximumOutputTokens: 1,
            maximumImageCount: 4);
        var openAiProfile = CreateProfile(
            connectorPluginKey: ProviderConnectorKeys.OpenAi,
            providerKind: AgentFrameworkProviderKind.OpenAi,
            purpose: ProviderProfilePurpose.ImageGeneration,
            defaultModel: "gpt-image-1",
            suggestedModels: ["gpt-image-1"]);
        var policy = new SharedProviderPublicationEligibilityPolicy(
            new TestRelaySupportCatalog(
            [
                new SharedProviderRelayAdapterDescriptor(
                    profile.ConnectorPluginKey,
                    SharedProviderPurpose.ImageGeneration,
                    SharedProviderRelayAdapterClassification.Production,
                    support),
                new SharedProviderRelayAdapterDescriptor(
                    openAiProfile.ConnectorPluginKey,
                    SharedProviderPurpose.ImageGeneration,
                    SharedProviderRelayAdapterClassification.Production,
                    support)
            ]));

        var decision = policy.Evaluate(profile, CreateManifest(profile.ConnectorPluginKey), requiredSecretExists: true);
        var openAiDecision = policy.Evaluate(
            openAiProfile,
            CreateManifest(openAiProfile.ConnectorPluginKey),
            requiredSecretExists: true);

        Assert.True(decision.IsEligible);
        Assert.True(openAiDecision.IsEligible);
        Assert.Equal(SharedProviderPurpose.ImageGeneration, decision.Purpose);
        Assert.All(decision.Models, model => Assert.Equal(
            [
                SharedProviderCapability.ImageGenerations,
                SharedProviderCapability.Base64Json
            ],
            model.Capabilities));
        Assert.Equal(2, decision.Models.Count);
        Assert.Equal(
            [
                SharedProviderCapability.ImageGenerations,
                SharedProviderCapability.Base64Json
            ],
            Assert.Single(openAiDecision.Models).Capabilities);
    }

    [Fact]
    public void UnsupportedOptionalProfileCapabilitiesAreOmittedFromIntersection()
    {
        var profile = CreateProfile(
            supportsStreaming: true,
            supportsTools: true,
            supportsStructuredOutput: true,
            supportsVision: true);
        var support = CreateChatSupport(
            supportsStreaming: false,
            supportsTools: false,
            supportsParallelTools: false,
            supportsStructuredOutput: false,
            supportsVision: false);

        var decision = CreatePolicy(support).Evaluate(
            profile,
            CreateManifest(profile.ConnectorPluginKey),
            requiredSecretExists: true);
        var transportMismatch = CreatePolicy(new SharedProviderRelaySupportDescriptor(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ChatCompletions
            },
            SharedProviderStreamingMode.ServerSentEvents,
            supportsFunctionTools: true,
            supportsParallelFunctionTools: true,
            supportsStructuredOutput: true,
            supportsVisionInput: true,
            supportsBase64Images: false,
            maximumRequestBytes: 1_000_000,
            maximumOutputTokens: 8_192,
            maximumImageCount: 1)).Evaluate(
            profile,
            CreateManifest(profile.ConnectorPluginKey),
            requiredSecretExists: true);

        Assert.True(decision.IsEligible);
        Assert.Equal(
            [SharedProviderCapability.Responses],
            decision.Models.Single(model => model.UpstreamModelId == profile.DefaultModel).Capabilities);
        Assert.Equal(
            SharedProviderPublicationEligibilityCode.RelayUnsupported,
            transportMismatch.Code);
    }

    [Fact]
    public void ProjectorExcludesUnpublishedAndIneligibleProfiles()
    {
        var eligibleProfile = CreateProfile(name: "Eligible");
        var unpublishedProfile = CreateProfile(name: "Unpublished");
        var disabledProfile = CreateProfile(name: "Disabled", isEnabled: false);
        var policy = CreatePolicy();
        var sources = new[]
        {
            CreateProjectionSource(eligibleProfile, policy, isPublished: true),
            CreateProjectionSource(unpublishedProfile, policy, isPublished: false),
            CreateProjectionSource(disabledProfile, policy, isPublished: true)
        };

        var projection = SharedProviderCatalogProjector.Project(SourceInstanceId, sources);

        var publication = Assert.Single(projection.Catalog.Providers);
        Assert.Equal("Eligible", publication.DisplayName);
    }

    [Fact]
    public void ProjectorCreatesStablePublicIdentitiesAndDistinctDuplicateModelRoutes()
    {
        var firstProfile = CreateProfile(name: "First", defaultModel: "duplicate-model");
        var secondProfile = CreateProfile(name: "Second", defaultModel: "duplicate-model");
        var policy = CreatePolicy();
        var first = CreateProjectionSource(firstProfile, policy, isPublished: true);
        var second = CreateProjectionSource(secondProfile, policy, isPublished: true);

        var firstProjection = SharedProviderCatalogProjector.Project(SourceInstanceId, [first, second]);
        var secondProjection = SharedProviderCatalogProjector.Project(SourceInstanceId, [first, second]);

        Assert.Equal(firstProjection.CatalogRevision, secondProjection.CatalogRevision);
        Assert.Equal(firstProjection.EntityTag, secondProjection.EntityTag);
        var routingIds = firstProjection.Catalog.Providers
            .SelectMany(publication => publication.Models)
            .Select(model => model.Id)
            .ToArray();
        Assert.Equal(2, routingIds.Length);
        Assert.Equal(routingIds.Length, routingIds.Distinct().Count());
    }

    [Fact]
    public void ProjectorProducesCanonicalOrderingAndDeterministicRevisions()
    {
        var policy = CreatePolicy();
        var alpha = CreateProjectionSource(CreateProfile(name: "Alpha"), policy, isPublished: true);
        var omega = CreateProjectionSource(CreateProfile(name: "Omega"), policy, isPublished: true);

        var forward = SharedProviderCatalogProjector.Project(SourceInstanceId, [alpha, omega]);
        var reverse = SharedProviderCatalogProjector.Project(SourceInstanceId, [omega, alpha]);

        Assert.Equal(forward.CatalogRevision, reverse.CatalogRevision);
        Assert.Equal(forward.EntityTag, reverse.EntityTag);
        Assert.Equal(
            forward.Catalog.Providers.Select(publication => publication.PublicationId),
            reverse.Catalog.Providers.Select(publication => publication.PublicationId));
        SharedProviderProtocolJson.ValidateCatalog(forward.Catalog);
    }

    [Fact]
    public void PrivateProfileChangesDoNotChangePublicRevisionOrEntityTag()
    {
        var policy = CreatePolicy();
        var original = CreateProfile(
            baseUrl: "https://private-a.example.test/v1",
            apiKeySecretId: Guid.Parse("20000000-0000-0000-0000-000000000001"),
            privateNote: "internal-a");
        var changed = CloneProfile(
            original,
            baseUrl: "https://private-b.example.test/v1",
            apiKeySecretId: Guid.Parse("20000000-0000-0000-0000-000000000002"),
            privateNote: "internal-b");
        var publication = CreatePublication(original.Id, isPublished: true);

        var first = SharedProviderCatalogProjector.Project(
            SourceInstanceId,
            [new(publication, original, policy.Evaluate(original, CreateManifest(original.ConnectorPluginKey), requiredSecretExists: true))]);
        var second = SharedProviderCatalogProjector.Project(
            SourceInstanceId,
            [new(publication, changed, policy.Evaluate(changed, CreateManifest(changed.ConnectorPluginKey), requiredSecretExists: true))]);

        Assert.Equal(first.CatalogRevision, second.CatalogRevision);
        Assert.Equal(first.EntityTag, second.EntityTag);
        Assert.Equal(
            Assert.Single(first.Catalog.Providers).Revision,
            Assert.Single(second.Catalog.Providers).Revision);
    }

    [Fact]
    public void PublicProfileChangesChangePublicationAndCatalogRevisions()
    {
        var policy = CreatePolicy();
        var original = CreateProfile(name: "Public provider");
        var changed = CloneProfile(
            original,
            lastHealthCheckAtUtc: Timestamp,
            lastHealthStatus: SharedProviderPublicHealthMapper.HealthyStatus);
        var publication = CreatePublication(original.Id, isPublished: true);

        var first = SharedProviderCatalogProjector.Project(
            SourceInstanceId,
            [new(publication, original, policy.Evaluate(original, CreateManifest(original.ConnectorPluginKey), requiredSecretExists: true))]);
        var second = SharedProviderCatalogProjector.Project(
            SourceInstanceId,
            [new(publication, changed, policy.Evaluate(changed, CreateManifest(changed.ConnectorPluginKey), requiredSecretExists: true))]);

        Assert.NotEqual(first.CatalogRevision, second.CatalogRevision);
        Assert.NotEqual(first.EntityTag, second.EntityTag);
        Assert.NotEqual(
            Assert.Single(first.Catalog.Providers).Revision,
            Assert.Single(second.Catalog.Providers).Revision);
        Assert.Equal(
            SharedProviderHealthState.Degraded,
            Assert.Single(first.Catalog.Providers).Health.State);
        Assert.Equal(
            SharedProviderHealthState.Available,
            Assert.Single(second.Catalog.Providers).Health.State);
    }

    [Fact]
    public void SerializedCatalogContainsNoPrivateProviderFieldsOrValues()
    {
        var profile = CreateProfile(
            baseUrl: "https://private-upstream.example.test/v1",
            apiKeySecretId: Guid.Parse("30000000-0000-0000-0000-000000000001"),
            privateNote: "private-operator-note",
            lastHealthCheckAtUtc: Timestamp,
            lastHealthStatus: "raw private upstream health detail");
        var source = CreateProjectionSource(profile, CreatePolicy(), isPublished: true);

        var projection = SharedProviderCatalogProjector.Project(SourceInstanceId, [source]);
        var json = SharedProviderProtocolJson.SerializeCatalog(projection.Catalog);

        Assert.Equal(
            SharedProviderHealthState.Unavailable,
            Assert.Single(projection.Catalog.Providers).Health.State);
        Assert.DoesNotContain(profile.Id.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(profile.BaseUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain(profile.ApiKeySecretId!.Value.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-operator-note", json, StringComparison.Ordinal);
        Assert.DoesNotContain("baseUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("extraSettings", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastHealthStatus", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "raw private upstream health detail",
            json,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RoutingIndexResolvesExactPublishedModelAndRejectsUnknownModel()
    {
        var profile = CreateProfile(
            defaultModel: "default-model",
            suggestedModels: ["default-model", "secondary-model"]);
        var projection = SharedProviderCatalogProjector.Project(
            SourceInstanceId,
            [CreateProjectionSource(profile, CreatePolicy(), isPublished: true)]);
        var publication = Assert.Single(projection.Catalog.Providers);
        var secondaryModel = publication.Models.Single(model => model.DisplayName == "secondary-model");
        var unknown = SharedProviderRoutingModelIdCodec.Create(
            publication.PublicationId,
            "unknown-model");

        var found = projection.RoutingIndex.TryGetValue(secondaryModel.Id, out var route);
        var unknownFound = projection.RoutingIndex.TryGetValue(unknown, out _);

        Assert.True(found);
        Assert.NotNull(route);
        Assert.Equal(profile.Id, route.ProviderProfileId);
        Assert.Equal("secondary-model", route.UpstreamModelId);
        Assert.False(unknownFound);
    }

    [Fact]
    public async Task PublicationApplicationPublishesAndUnpublishesWithPostCommitActivityAndInvalidation()
    {
        var fixture = await CreatePersistenceFixtureAsync(isPublished: false);
        var activity = new RecordingActivityStream();
        var observer = new RecordingPublicationCommitObserver();
        var service = fixture.CreatePublicationService(activity, observer);
        await using (var mutation = await fixture.DbContextFactory.CreateDbContextAsync())
        {
            var profile = await mutation.Set<PersistedProviderProfile>()
                .SingleAsync(item => item.Id == fixture.Profile.Id);
            profile.ExtraSettingsJson = "{\"privateOperatorNote\":\"provider-save-path\"}";
            await mutation.SaveChangesAsync();
        }

        var saveActivity = new RecordingActivityStream();
        var saveObserver = new RecordingProviderProfileCommitObserver();
        var providerAdministration = fixture.CreateProviderAdministrationService(saveActivity, saveObserver);
        var saved = await providerAdministration.SaveProviderAsync(CreateEditor(fixture));
        Assert.True(saved.IsSuccess);
        Assert.Equal(fixture.Profile.Id, saved.Value);

        await using (var verification = await fixture.DbContextFactory.CreateDbContextAsync())
        {
            var persistedProfile = await verification.Set<PersistedProviderProfile>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == fixture.Profile.Id);
            using var metadataDocument = JsonDocument.Parse(persistedProfile.ExtraSettingsJson);
            Assert.Equal(
                AgentFrameworkProviderKind.OpenAi.ToString(),
                metadataDocument.RootElement
                    .GetProperty(ProviderProfileMetadataPropertyNames.ProviderKind)
                    .GetString());
            Assert.Equal(
                ProviderTransportKind.Responses.ToString(),
                metadataDocument.RootElement
                    .GetProperty(ProviderProfileMetadataPropertyNames.ProviderTransport)
                    .GetString());
            Assert.Equal(
                ProviderProfilePurpose.Chat.ToString(),
                metadataDocument.RootElement
                    .GetProperty(ProviderProfileMetadataPropertyNames.ProviderPurpose)
                    .GetString());
            Assert.Empty(metadataDocument.RootElement
                .GetProperty(ProviderProfileMetadataPropertyNames.SuggestedModels)
                .EnumerateArray());
            Assert.True(fixture.EligibilityPolicy.Evaluate(
                persistedProfile,
                CreateManifest(persistedProfile.ConnectorPluginKey),
                requiredSecretExists: true).IsEligible);
        }

        var published = await service.ChangeAsync(new SharedProviderPublicationChangeRequest(
            fixture.Profile.Id,
            SharedProviderPublicationAction.Publish,
            fixture.Publication.ConcurrencyToken));
        var unpublished = await service.ChangeAsync(new SharedProviderPublicationChangeRequest(
            fixture.Profile.Id,
            SharedProviderPublicationAction.Unpublish,
            published.ConcurrencyToken));

        Assert.True(published.IsPublished);
        Assert.False(unpublished.IsPublished);
        Assert.Equal(fixture.Publication.PublicId, published.PublicId);
        Assert.Equal(fixture.Publication.PublicId, unpublished.PublicId);
        Assert.Equal(["publish", "unpublish"], activity.Requests.Select(request => request.Action));
        Assert.Equal([fixture.Profile.Id, fixture.Profile.Id], observer.ProviderProfileIds);
        Assert.Single(saveActivity.Requests);
        Assert.Equal([fixture.Profile.Id], saveObserver.ProviderProfileIds);
        Assert.All(activity.CancellationTokens, token => Assert.False(token.CanBeCanceled));
        Assert.All(observer.CancellationTokens, token => Assert.False(token.CanBeCanceled));
    }

    [Fact]
    public async Task PublicationApplicationRejectsIneligibleAndStaleRequestsWithoutPostCommitSideEffects()
    {
        var referencedFixture = await CreatePersistenceFixtureAsync(
            isPublished: false);
        var deletionActivity = new RecordingActivityStream();
        var secretService = new SecretService(
            referencedFixture.DbContextFactory,
            new NoOpSecretVault(),
            new NoOpSecretProtector(),
            referencedFixture.Clock,
            deletionActivity,
            [new ProviderSecretDeletionReferencePolicy()]);
        var deletionException = await Assert.ThrowsAsync<
            SecretDeletionBlockedException>(() => secretService.DeleteAsync(
            referencedFixture.SecretRecordId));
        Assert.Contains(
            "provider profile",
            deletionException.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(deletionActivity.Requests);
        await using (var referenceVerification =
                     await referencedFixture.DbContextFactory
                         .CreateDbContextAsync())
        {
            Assert.True(await referenceVerification.Set<SecretRecord>()
                .AnyAsync(secret =>
                    secret.Id == referencedFixture.SecretRecordId));
        }

        var replacementSecretRecordId = Guid.NewGuid();
        await using (var replacementSeed =
                     await referencedFixture.DbContextFactory.CreateDbContextAsync())
        {
            replacementSeed.Add(new SecretRecord
            {
                Id = replacementSecretRecordId,
                Name = "Replacement provider secret",
                Kind = SecretKind.ApiKey,
                EncryptedPayload = "replacement-ciphertext",
                CreatedAtUtc = Timestamp,
                UpdatedAtUtc = Timestamp
            });
            await replacementSeed.SaveChangesAsync();
        }

        var raceActivity = new RecordingActivityStream();
        var raceObserver = new RecordingProviderProfileCommitObserver();
        var raceProviderAdministration = referencedFixture.CreateProviderAdministrationService(
            raceActivity,
            raceObserver);
        var gatedDeletionPolicy = new GatedSecretDeletionReferencePolicy(
            new ProviderSecretDeletionReferencePolicy());
        var raceSecretService = new SecretService(
            referencedFixture.DbContextFactory,
            new NoOpSecretVault(),
            new NoOpSecretProtector(),
            referencedFixture.Clock,
            raceActivity,
            [gatedDeletionPolicy]);
        var deleteObservation = Record.ExceptionAsync(() => raceSecretService.DeleteAsync(
            referencedFixture.SecretRecordId));
        await gatedDeletionPolicy.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        var raceSave = raceProviderAdministration.SaveProviderAsync(
            CreateEditor(referencedFixture, replacementSecretRecordId));
        try
        {
            await Task.Yield();
            Assert.False(raceSave.IsCompleted);
        }
        finally
        {
            gatedDeletionPolicy.Release();
        }

        await Task.WhenAll(deleteObservation, raceSave);
        var observedDeleteException = await deleteObservation;
        Assert.IsType<SecretDeletionBlockedException>(observedDeleteException);
        Assert.True((await raceSave).IsSuccess);
        await using (var raceVerification =
                     await referencedFixture.DbContextFactory.CreateDbContextAsync())
        {
            var persistedProfile = await raceVerification.Set<PersistedProviderProfile>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == referencedFixture.Profile.Id);
            Assert.Equal(replacementSecretRecordId, persistedProfile.ApiKeySecretId);
            Assert.True(await raceVerification.Set<SecretRecord>()
                .AnyAsync(secret => secret.Id == replacementSecretRecordId));
            var originalSecretExists = await raceVerification.Set<SecretRecord>()
                .AnyAsync(secret => secret.Id == referencedFixture.SecretRecordId);
            Assert.True(originalSecretExists);
        }

        var ineligibleFixture = await CreatePersistenceFixtureAsync(isPublished: false);
        await using (var mutation = await ineligibleFixture.DbContextFactory.CreateDbContextAsync())
        {
            var secret = await mutation.Set<SecretRecord>()
                .SingleAsync(item => item.Id == ineligibleFixture.SecretRecordId);
            mutation.Remove(secret);
            await mutation.SaveChangesAsync();
        }

        var ineligibleActivity = new RecordingActivityStream();
        var ineligibleObserver = new RecordingPublicationCommitObserver();
        var ineligibleService = ineligibleFixture.CreatePublicationService(
            ineligibleActivity,
            ineligibleObserver);
        await Assert.ThrowsAsync<SharedProviderPublicationEligibilityException>(() =>
            ineligibleService.ChangeAsync(new SharedProviderPublicationChangeRequest(
                ineligibleFixture.Profile.Id,
                SharedProviderPublicationAction.Publish,
                ineligibleFixture.Publication.ConcurrencyToken)));

        var fixture = await CreatePersistenceFixtureAsync(isPublished: false);
        var activity = new RecordingActivityStream();
        var observer = new RecordingPublicationCommitObserver();
        var service = fixture.CreatePublicationService(activity, observer);
        var missingSecretActivity = new RecordingActivityStream();
        var missingSecretObserver = new RecordingProviderProfileCommitObserver();
        var missingSecretSave = await fixture.CreateProviderAdministrationService(
                missingSecretActivity,
                missingSecretObserver)
            .SaveProviderAsync(CreateEditor(fixture, Guid.NewGuid()));

        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => service.ChangeAsync(
            new SharedProviderPublicationChangeRequest(
                fixture.Profile.Id,
                SharedProviderPublicationAction.Publish,
                Guid.NewGuid())));

        await using var verification = await fixture.DbContextFactory.CreateDbContextAsync();
        var persisted = await verification.Set<ProviderSharePublication>()
            .AsNoTracking()
            .SingleAsync(publication => publication.Id == fixture.Publication.Id);
        Assert.False(persisted.IsPublished);
        Assert.Empty(ineligibleActivity.Requests);
        Assert.Empty(ineligibleObserver.ProviderProfileIds);
        Assert.Empty(activity.Requests);
        Assert.Empty(observer.ProviderProfileIds);
        Assert.False(missingSecretSave.IsSuccess);
        Assert.Contains(
            "does not exist",
            Assert.Single(missingSecretSave.Errors).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(missingSecretActivity.Requests);
        Assert.Empty(missingSecretObserver.ProviderProfileIds);
    }

    [Fact]
    public async Task QueryCacheUsesPersistedStampAcrossInstancesAndRechecksCurrentEligibility()
    {
        var fixture = await CreatePersistenceFixtureAsync(isPublished: true);
        var firstHost = fixture.CreateCatalogService(new SharedProviderCatalogCache());
        var secondHost = fixture.CreateCatalogService(new SharedProviderCatalogCache());
        var firstBefore = await firstHost.GetSnapshotAsync();
        var secondBefore = await secondHost.GetSnapshotAsync();

        await using (var mutation = await fixture.DbContextFactory.CreateDbContextAsync())
        {
            var secret = await mutation.Set<SecretRecord>()
                .SingleAsync(item => item.Id == fixture.SecretRecordId);
            mutation.Remove(secret);
            await mutation.SaveChangesAsync();
        }

        var firstAfter = await firstHost.GetSnapshotAsync();
        var secondAfter = await secondHost.GetSnapshotAsync();

        Assert.Single(firstBefore.Catalog.Providers);
        Assert.Single(secondBefore.Catalog.Providers);
        Assert.Empty(firstAfter.Catalog.Providers);
        Assert.Empty(secondAfter.Catalog.Providers);
        Assert.NotEqual(firstBefore.EntityTag, firstAfter.EntityTag);
        Assert.NotEqual(secondBefore.EntityTag, secondAfter.EntityTag);
        Assert.Equal(firstAfter.EntityTag, secondAfter.EntityTag);
    }

    private static SharedProviderPublicationEligibilityPolicy CreatePolicy(
        SharedProviderRelaySupportDescriptor? support = null,
        string connectorPluginKey = ProviderConnectorKeys.OpenAi,
        SharedProviderPurpose purpose = SharedProviderPurpose.Chat,
        SharedProviderRelayAdapterClassification classification =
            SharedProviderRelayAdapterClassification.Production,
        bool includeDescriptor = true)
    {
        var catalog = includeDescriptor
            ? new TestRelaySupportCatalog([
                new SharedProviderRelayAdapterDescriptor(
                    connectorPluginKey,
                    purpose,
                    classification,
                    support ?? CreateChatSupport())
            ])
            : new TestRelaySupportCatalog([]);
        return new SharedProviderPublicationEligibilityPolicy(catalog);
    }

    private static SharedProviderRelaySupportDescriptor CreateChatSupport(
        bool supportsStreaming = true,
        bool supportsTools = true,
        bool supportsParallelTools = true,
        bool supportsStructuredOutput = true,
        bool supportsVision = true)
        => new(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ChatCompletions,
                SharedProviderRelayOperation.Responses
            },
            supportsStreaming
                ? SharedProviderStreamingMode.ServerSentEvents
                : SharedProviderStreamingMode.None,
            supportsTools,
            supportsParallelTools,
            supportsStructuredOutput,
            supportsVision,
            supportsBase64Images: false,
            maximumRequestBytes: 1_000_000,
            maximumOutputTokens: 8_192,
            maximumImageCount: 1);

    private static SharedProviderCatalogProjectionSource CreateProjectionSource(
        PersistedProviderProfile profile,
        SharedProviderPublicationEligibilityPolicy policy,
        bool isPublished)
        => new(
            CreatePublication(profile.Id, isPublished),
            profile,
            policy.Evaluate(
                profile,
                CreateManifest(profile.ConnectorPluginKey),
                requiredSecretExists: true));

    private static ProviderSharePublication CreatePublication(Guid profileId, bool isPublished)
    {
        var publication = SharedProviderPublicationTransitions.Create(
            profileId,
            new SharedProviderPublicationId(Guid.NewGuid()),
            Timestamp);
        if (isPublished)
        {
            SharedProviderPublicationTransitions.Publish(publication, Timestamp);
        }

        return publication;
    }

    private static PersistedProviderProfile CreateProfile(
        Guid? id = null,
        string name = "Central provider",
        string connectorPluginKey = ProviderConnectorKeys.OpenAi,
        AgentFrameworkProviderKind providerKind = AgentFrameworkProviderKind.OpenAi,
        ProviderTransportKind transport = ProviderTransportKind.Responses,
        ProviderProfilePurpose purpose = ProviderProfilePurpose.Chat,
        string defaultModel = "gpt-5-mini",
        IReadOnlyList<string>? suggestedModels = null,
        bool isEnabled = true,
        bool supportsStreaming = true,
        bool supportsTools = true,
        bool supportsStructuredOutput = true,
        bool supportsVision = false,
        string baseUrl = "https://private-upstream.example.test/v1",
        Guid? apiKeySecretId = null,
        string? extraSettingsJson = null,
        string privateNote = "operator-only",
        DateTimeOffset? lastHealthCheckAtUtc = null,
        string? lastHealthStatus = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            ConnectorPluginKey = connectorPluginKey,
            ConfigSchemaVersion = "1.0",
            BaseUrl = baseUrl,
            ApiKeySecretId = apiKeySecretId ?? Guid.Parse("40000000-0000-0000-0000-000000000001"),
            DefaultModel = defaultModel,
            IsEnabled = isEnabled,
            SupportsStreaming = supportsStreaming,
            SupportsToolCalling = supportsTools,
            SupportsStructuredOutput = supportsStructuredOutput,
            SupportsVision = supportsVision,
            LastHealthCheckAtUtc = lastHealthCheckAtUtc,
            LastHealthStatus = lastHealthStatus,
            ExtraSettingsJson = extraSettingsJson ??
                SharedProviderProfilePublicationMetadataWriter.Write(
                    JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["privateOperatorNote"] = privateNote
                    }),
                    providerKind,
                    transport,
                    purpose,
                    defaultModel,
                    suggestedModels)
        };

    private static PersistedProviderProfile CloneProfile(
        PersistedProviderProfile source,
        string? name = null,
        string? baseUrl = null,
        Guid? apiKeySecretId = null,
        string? privateNote = null,
        DateTimeOffset? lastHealthCheckAtUtc = null,
        string? lastHealthStatus = null)
        => new()
        {
            Id = source.Id,
            Name = name ?? source.Name,
            ProviderKind = source.ProviderKind,
            ConnectorPluginKey = source.ConnectorPluginKey,
            ConfigSchemaVersion = source.ConfigSchemaVersion,
            BaseUrl = baseUrl ?? source.BaseUrl,
            ApiKeySecretId = apiKeySecretId ?? source.ApiKeySecretId,
            DefaultModel = source.DefaultModel,
            TimeoutSeconds = source.TimeoutSeconds,
            IsEnabled = source.IsEnabled,
            SupportsStreaming = source.SupportsStreaming,
            SupportsToolCalling = source.SupportsToolCalling,
            SupportsStructuredOutput = source.SupportsStructuredOutput,
            SupportsVision = source.SupportsVision,
            LastHealthCheckAtUtc = lastHealthCheckAtUtc ?? source.LastHealthCheckAtUtc,
            LastHealthStatus = lastHealthStatus ?? source.LastHealthStatus,
            ExtraSettingsJson = ReplacePrivateNote(source.ExtraSettingsJson, privateNote),
            ConcurrencyToken = source.ConcurrencyToken
        };

    private static string ReplacePrivateNote(string json, string? privateNote)
    {
        if (privateNote is null)
        {
            return json;
        }

        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? throw new InvalidOperationException("Test metadata is invalid.");
        var mutable = values.ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
        mutable["privateOperatorNote"] = privateNote;
        return JsonSerializer.Serialize(mutable);
    }

    private static string CreateMetadata(
        AgentFrameworkProviderKind providerKind,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        IReadOnlyList<string> suggestedModels,
        string privateNote = "operator-only")
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [ProviderProfileMetadataPropertyNames.ProviderKind] = providerKind.ToString(),
            [ProviderProfileMetadataPropertyNames.ProviderTransport] = transport.ToString(),
            [ProviderProfileMetadataPropertyNames.ProviderPurpose] = purpose.ToString(),
            [ProviderProfileMetadataPropertyNames.SuggestedModels] = suggestedModels,
            ["privateOperatorNote"] = privateNote
        });

    private static ConnectorPluginManifest CreateManifest(
        string pluginKey,
        ConnectorManifestCapability capabilities = ConnectorManifestCapability.ProviderExecution)
        => new(
            pluginKey,
            "Test provider connector",
            "1.0.0",
            capabilities,
            new ConnectorConfigurationSchema("1.0", []),
            [new ConnectorSecretRequirement("apiKey", "API key", true, "Provider API key")],
            new ConnectorHealthCheckDescriptor("test", "Test connector health."),
            new ConnectorAgentExposure("test", true, false, "Test connector."),
            null);

    private static ProviderAdministrationEditorModel CreateEditor(
        PersistenceFixture fixture,
        Guid? secretRecordId = null)
        => new()
        {
            Id = fixture.Profile.Id,
            Name = fixture.Profile.Name,
            ConnectorPluginKey = fixture.Profile.ConnectorPluginKey,
            ConfigSchemaVersion = fixture.Profile.ConfigSchemaVersion,
            ApiKeySecretId = secretRecordId ?? fixture.SecretRecordId,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsToolCalling = true,
            SupportsStructuredOutput = true,
            SupportsVision = fixture.Profile.SupportsVision,
            Configuration = new ConnectorConfigState(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProviderConnectorFieldKeys.BaseUrl] = fixture.Profile.BaseUrl,
                    [ProviderConnectorFieldKeys.DefaultModel] = fixture.Profile.DefaultModel,
                    [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
                })
        };

    private static async Task<PersistenceFixture> CreatePersistenceFixtureAsync(bool isPublished)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(ProviderManagementModuleAssemblyMarker).Assembly,
            typeof(SecretService).Assembly
        ]);
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"shared-provider-catalog-{Guid.NewGuid():N}", databaseRoot)
            .Options;
        var dbContextFactory = new TestDbContextFactory(options);
        var profile = CreateProfile();
        var publication = CreatePublication(profile.Id, isPublished);
        var identity = SharedProviderServiceIdentity.Create(SourceInstanceId, Timestamp);
        var secret = new SecretRecord
        {
            Id = profile.ApiKeySecretId!.Value,
            Name = "Provider secret",
            Kind = SecretKind.ApiKey,
            EncryptedPayload = "ciphertext",
            CreatedAtUtc = Timestamp,
            UpdatedAtUtc = Timestamp
        };
        await using (var dbContext = dbContextFactory.CreateDbContext())
        {
            dbContext.AddRange(profile, publication, identity, secret);
            await dbContext.SaveChangesAsync();
        }

        var manifest = CreateManifest(profile.ConnectorPluginKey);
        var registry = new ProviderAdministrationConnectorCatalog([new TestProviderAdministrationConnector(manifest)]);
        var policy = CreatePolicy();
        return new PersistenceFixture(
            dbContextFactory,
            profile,
            publication,
            secret.Id,
            registry,
            policy,
            new FixedClock(Timestamp.AddMinutes(1)));
    }

    private sealed record PersistenceFixture(
        IDbContextFactory<AppDbContext> DbContextFactory,
        PersistedProviderProfile Profile,
        ProviderSharePublication Publication,
        Guid SecretRecordId,
        ProviderAdministrationConnectorCatalog ProviderAdministrationConnectorCatalog,
        SharedProviderPublicationEligibilityPolicy EligibilityPolicy,
        IClock Clock)
    {
        public SharedProviderPublicationApplicationService CreatePublicationService(
            IActivityStream activityStream,
            ISharedProviderPublicationCommitObserver observer)
            => new(
                DbContextFactory,
                ProviderAdministrationConnectorCatalog,
                EligibilityPolicy,
                activityStream,
                Clock,
                [observer]);

        public SharedProviderCatalogQueryService CreateCatalogService(
            SharedProviderCatalogCache cache)
            => new(
                DbContextFactory,
                new SharedProviderServiceIdentityStore(DbContextFactory, Clock),
                ProviderAdministrationConnectorCatalog,
                EligibilityPolicy,
                cache);

        public ProviderAdministrationService CreateProviderAdministrationService(
            IActivityStream activityStream,
            IProviderProfileCommitObserver observer)
        {
            var secretService = new SecretService(
                DbContextFactory,
                new NoOpSecretVault(),
                new NoOpSecretProtector(),
                Clock,
                activityStream,
                [new ProviderSecretDeletionReferencePolicy()]);
            return new ProviderAdministrationService(
                DbContextFactory,
                secretService,
                secretRuntimeResolver: null!,
                ProviderAdministrationConnectorCatalog,
                providerHealthCheckService: null!,
                activityStream,
                [],
                [observer]);
        }
    }

    private sealed class TestRelaySupportCatalog(
        IReadOnlyList<SharedProviderRelayAdapterDescriptor> descriptors)
        : ISharedProviderRelaySupportCatalog
    {
        public IReadOnlyList<SharedProviderRelayAdapterDescriptor> List() => descriptors;

        public bool TryGet(
            string connectorPluginKey,
            SharedProviderPurpose purpose,
            out SharedProviderRelayAdapterDescriptor descriptor)
        {
            var match = descriptors.SingleOrDefault(item =>
                string.Equals(item.ConnectorPluginKey, connectorPluginKey, StringComparison.Ordinal) &&
                item.Purpose == purpose);
            if (match is null)
            {
                descriptor = null!;
                return false;
            }

            descriptor = match;
            return true;
        }
    }

    private sealed class NoOpSecretVault : ISecretVault
    {
        public Task SetAsync(
            string key,
            string value,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> GetAsync(
            string key,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpSecretProtector : ISecretProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string protectedValue) => protectedValue;
    }

    private sealed class GatedSecretDeletionReferencePolicy(
        ISecretDeletionReferencePolicy inner) : ISecretDeletionReferencePolicy
    {
        private readonly TaskCompletionSource entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => entered.Task;

        public async Task<SecretDeletionReference?> FindReferenceAsync(
            AppDbContext dbContext,
            Guid secretRecordId,
            CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return await inner.FindReferenceAsync(
                dbContext,
                secretRecordId,
                cancellationToken);
        }

        public void Release() => release.TrySetResult();
    }

    private sealed class TestProviderAdministrationConnector(ConnectorPluginManifest manifest) : IProviderAdministrationConnector
    {
        public ConnectorPluginManifest Manifest { get; } = manifest;

        public CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderKind? LegacyProviderKind => null;

        public SharedProviderProfilePublicationMetadata DefaultPublicationMetadata { get; } = new(
            AgentFrameworkProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            []);

    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    private sealed class FixedClock(DateTimeOffset current) : IClock
    {
        public DateTimeOffset GetUtcNow() => current;
    }

    private sealed class RecordingActivityStream : IActivityStream
    {
        public List<ActivityWriteRequest> Requests { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task RecordAsync(
            ActivityWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            CancellationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPublicationCommitObserver : ISharedProviderPublicationCommitObserver
    {
        public List<Guid> ProviderProfileIds { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task PublicationChangedAsync(
            Guid providerProfileId,
            CancellationToken cancellationToken = default)
        {
            ProviderProfileIds.Add(providerProfileId);
            CancellationTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProviderProfileCommitObserver
        : IProviderProfileCommitObserver
    {
        public List<Guid> ProviderProfileIds { get; } = [];

        public Task ProviderSavedAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            ProviderProfileIds.Add(providerId);
            return Task.CompletedTask;
        }

        public Task ProviderDeletedAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
