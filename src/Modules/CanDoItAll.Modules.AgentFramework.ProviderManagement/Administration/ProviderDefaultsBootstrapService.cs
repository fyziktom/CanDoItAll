using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class ProviderDefaultsBootstrapService {
    private static readonly Guid RuntimeBootstrapOpenAiProviderId = Guid.Parse("C1C103DB-707E-3F52-8809-8D804FC171D1");
    private static readonly Guid RuntimeBootstrapOpenAiChatCompletionsProviderId = Guid.Parse("036B360A-E3F4-8350-97CA-F88DE60BA2BB");
    private static readonly Guid RuntimeBootstrapOpenAiImageProviderId = Guid.Parse("8958FA61-4BD6-1451-8123-4E4E4FEA2E26");
    private static readonly Guid RuntimeBootstrapComfyUiProviderId = Guid.Parse("509EAF62-4A4E-1C50-856F-8836328A519E");
    private static readonly Guid RuntimeBootstrapLocalOllamaProviderId = Guid.Parse("BD2BFFBB-23D5-D152-82F6-E1D37908B169");
    private const string RuntimeBootstrapOpenAiProviderName = ManagedSeedProviderFallbacks.OpenAiDefaultProviderName;
    private const string RuntimeBootstrapOpenAiChatCompletionsProviderName = ManagedSeedProviderFallbacks.OpenAiChatCompletionsProviderName;
    private const string RuntimeBootstrapOpenAiImageProviderName = "OpenAI image generation";
    private const string RuntimeBootstrapOpenAiBaseUrl = "https://api.openai.com/v1";
    private const string RuntimeBootstrapOpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private const string RuntimeBootstrapOpenAiModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
    private static readonly IReadOnlyList<string> RuntimeBootstrapOpenAiSuggestedModels =
    [
        OpenAiModelIds.Gpt56,
        .. ManagedSeedProviderFallbacks.OpenAiSuggestedModels
    ];
    private const string RuntimeBootstrapLegacyOpenAiImageModel = OpenAiModelIds.GptImage1Mini;
    private const string RuntimeBootstrapOpenAiImageModel = OpenAiModelIds.GptImage2;
    private const string RuntimeBootstrapLocalOllamaProviderName = "Local Ollama";
    private const string RuntimeBootstrapLocalOllamaBaseUrl = "http://127.0.0.1:11434";
    private const string RuntimeBootstrapLocalOllamaModel = "llama3.1";
    private const string RuntimeBootstrapProviderSchemaVersion = "1.0";
    private const int RuntimeBootstrapOpenAiTimeoutSeconds = 600;
    private const int RuntimeBootstrapLocalOllamaTimeoutSeconds = 45;

    public async Task<ProviderDefaultsBootstrapStage> PrepareAsync(
        ResolvedDatabaseProfile profile,
        Guid? openAiSecretId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(profile);
        var options = new DbContextOptionsBuilder<ProvidersDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        var dbContext = new ProvidersDbContext(options.Options);
        IDbContextTransaction? transaction = null;
        try {
            if (dbContext.Database.IsRelational()) {
                transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            }
            var (providerId, changed) = await PrepareProvidersAsync(dbContext, openAiSecretId, cancellationToken);
            return new ProviderDefaultsBootstrapStage(dbContext, transaction,
                CoordinatedDatabaseTransaction.ForProfile(profile),
                RuntimeBootstrapOpenAiProviderId, providerId, changed);
        } catch {
            if (transaction is not null) {
                await transaction.DisposeAsync();
            }
            await dbContext.DisposeAsync();
            throw;
        }
    }

    private async Task<(Guid ProviderId, bool Changed)> PrepareProvidersAsync(
        ProvidersDbContext dbContext, Guid? openAiSecretId, CancellationToken cancellationToken) {
        var changed = false;
        var openAiProvider = await dbContext.Set<ProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == RuntimeBootstrapOpenAiProviderId, cancellationToken)
            ?? await dbContext.Set<ProviderProfile>()
                .Where(item => item.Name == RuntimeBootstrapOpenAiProviderName)
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        if (openAiProvider is null) {
            openAiProvider = new ProviderProfile {
                Id = RuntimeBootstrapOpenAiProviderId,
                Name = RuntimeBootstrapOpenAiProviderName,
                ProviderKind = ProviderKind.OpenAi,
                ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
                ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
                BaseUrl = RuntimeBootstrapOpenAiBaseUrl,
                ApiKeySecretId = openAiSecretId,
                DefaultModel = RuntimeBootstrapOpenAiModel,
                TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true,
                SupportsVision = false,
                LastHealthStatus = "OpenAI active",
                LastHealthCheckAtUtc = null,
                ExtraSettingsJson =
                    BuildRuntimeBootstrapOpenAiProviderConfigurationJson(
                        openAiSecretId)
            };
            dbContext.Set<ProviderProfile>().Add(openAiProvider);
            changed = true;
        } else {
            changed |= UpdateRuntimeBootstrapOpenAiProvider(openAiProvider);
        }

        if (openAiSecretId.HasValue && openAiProvider.ApiKeySecretId != openAiSecretId.Value) {
            openAiProvider.ApiKeySecretId = openAiSecretId.Value;
            changed = true;
        }

        changed |= UpdateRuntimeBootstrapOpenAiProviderConfigurationJson(openAiProvider);

        var openAiChatCompletionsProvider = await dbContext.Set<ProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == RuntimeBootstrapOpenAiChatCompletionsProviderId, cancellationToken)
            ?? await dbContext.Set<ProviderProfile>()
                .Where(item => item.Name == RuntimeBootstrapOpenAiChatCompletionsProviderName)
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        if (openAiChatCompletionsProvider is null) {
            openAiChatCompletionsProvider = new ProviderProfile {
                Id = RuntimeBootstrapOpenAiChatCompletionsProviderId,
                Name = RuntimeBootstrapOpenAiChatCompletionsProviderName,
                ProviderKind = ProviderKind.OpenAi,
                ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
                ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
                BaseUrl = RuntimeBootstrapOpenAiBaseUrl,
                ApiKeySecretId = openAiSecretId,
                DefaultModel = RuntimeBootstrapOpenAiModel,
                TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true,
                SupportsVision = false,
                LastHealthStatus = "OpenAI active",
                LastHealthCheckAtUtc = null,
                ExtraSettingsJson = BuildRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(openAiSecretId)
            };
            dbContext.Set<ProviderProfile>().Add(openAiChatCompletionsProvider);
            changed = true;
        } else {
            changed |= UpdateRuntimeBootstrapOpenAiChatCompletionsProvider(openAiChatCompletionsProvider);
        }

        if (openAiSecretId.HasValue && openAiChatCompletionsProvider.ApiKeySecretId != openAiSecretId.Value) {
            openAiChatCompletionsProvider.ApiKeySecretId = openAiSecretId.Value;
            changed = true;
        }

        changed |= UpdateRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(openAiChatCompletionsProvider);
        changed |= await EnsureManagedCatalogProviderSeedsAsync(
            dbContext,
            openAiSecretId,
            cancellationToken);

        return (openAiProvider.Id, changed);
    }

    private async Task<bool> EnsureManagedCatalogProviderSeedsAsync(
        ProvidersDbContext dbContext,
        Guid? openAiSecretId,
        CancellationToken cancellationToken) {
        var seeds = CreateManagedCatalogProviderSeeds(openAiSecretId);
        var seedIds = seeds.Select(seed => seed.Id).ToArray();
        var seedNames = seeds.Select(seed => seed.Name).ToArray();
        var existingProviders = await dbContext
            .Set<ProviderProfile>()
            .Where(provider =>
                seedIds.Contains(provider.Id) ||
                seedNames.Contains(provider.Name))
            .ToListAsync(cancellationToken);
        var changed = false;

        foreach (var seed in seeds) {
            var byId = existingProviders
                .FirstOrDefault(provider => provider.Id == seed.Id);
            var byName = existingProviders
                .FirstOrDefault(provider =>
                    string.Equals(
                        provider.Name,
                        seed.Name,
                        StringComparison.Ordinal));
            if (byId is not null &&
                !string.Equals(byId.Name, seed.Name, StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"Managed provider seed '{seed.Name}' cannot use Id '{seed.Id:D}' because that Id belongs to '{byId.Name}'.");
            }

            if (byName is not null && byName.Id != seed.Id) {
                throw new InvalidOperationException(
                    $"Managed provider seed '{seed.Name}' must use Id '{seed.Id:D}', but the canonical provider uses '{byName.Id:D}'.");
            }

            if (byId is not null) {
                changed |= UpgradeManagedOpenAiImageProviderModel(byId);
                changed |= UpgradeManagedLocalOllamaCapabilities(byId);
                if (MatchesManagedCatalogProviderSeed(byId, seed) &&
                    !string.Equals(
                        byId.ExtraSettingsJson,
                        seed.ExtraSettingsJson,
                        StringComparison.Ordinal)) {
                    byId.ExtraSettingsJson = seed.ExtraSettingsJson;
                    changed = true;
                }

                continue;
            }

            dbContext
                .Set<ProviderProfile>()
                .Add(seed.CreateEntity());
            changed = true;
        }

        return changed;
    }

    private static bool MatchesManagedCatalogProviderSeed(
        ProviderProfile provider,
        ManagedCatalogProviderSeed seed) {
        return provider.ProviderKind == seed.ProviderKind &&
               string.Equals(provider.ConnectorPluginKey, seed.ConnectorPluginKey, StringComparison.Ordinal) &&
               string.Equals(provider.ConfigSchemaVersion, RuntimeBootstrapProviderSchemaVersion, StringComparison.Ordinal) &&
               string.Equals(provider.BaseUrl, seed.BaseUrl, StringComparison.Ordinal) &&
               provider.ApiKeySecretId == seed.ApiKeySecretId &&
               string.Equals(provider.DefaultModel, seed.DefaultModel, StringComparison.Ordinal) &&
               provider.TimeoutSeconds == seed.TimeoutSeconds &&
               provider.SupportsStreaming == seed.SupportsStreaming &&
               provider.SupportsToolCalling == seed.SupportsToolCalling &&
               provider.SupportsStructuredOutput == seed.SupportsStructuredOutput &&
               !provider.SupportsVision;
    }

    private static bool UpgradeManagedOpenAiImageProviderModel(ProviderProfile provider) {
        if (provider.Id != RuntimeBootstrapOpenAiImageProviderId ||
            !string.Equals(
                provider.DefaultModel,
                RuntimeBootstrapLegacyOpenAiImageModel,
                StringComparison.Ordinal)) {
            return false;
        }

        provider.DefaultModel = RuntimeBootstrapOpenAiImageModel;
        return true;
    }

    private static bool UpgradeManagedLocalOllamaCapabilities(
        ProviderProfile provider) {
        if (provider.Id != RuntimeBootstrapLocalOllamaProviderId ||
            provider.SupportsStructuredOutput) {
            return false;
        }

        provider.SupportsStructuredOutput = true;
        return true;
    }

    private static IReadOnlyList<ManagedCatalogProviderSeed>
        CreateManagedCatalogProviderSeeds(Guid? openAiSecretId) {
        return
        [
            new ManagedCatalogProviderSeed(
                RuntimeBootstrapOpenAiImageProviderId,
                RuntimeBootstrapOpenAiImageProviderName,
                ProviderKind.OpenAi,
                ProviderConnectorKeys.OpenAi,
                RuntimeBootstrapOpenAiBaseUrl,
                openAiSecretId,
                RuntimeBootstrapOpenAiImageModel,
                RuntimeBootstrapOpenAiTimeoutSeconds,
                SupportsStreaming: false,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                BuildManagedProviderConfigurationJson(
                    "{}",
                    ProviderConnectorKeys.OpenAi,
                    openAiSecretId,
                    RuntimeBootstrapOpenAiTimeoutSeconds,
                    CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
                    ProviderTransportKind.Responses,
                    ProviderProfilePurpose.ImageGeneration,
                    RuntimeBootstrapOpenAiImageModel,
                    ["cloud", "image", "image-generation", "openai"],
                    isPrivateProvider: false,
                    ProviderPricingDefaults.CreateDefaultPrices(
                        CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
                        RuntimeBootstrapOpenAiImageModel))),
            new ManagedCatalogProviderSeed(
                RuntimeBootstrapComfyUiProviderId,
                ComfyUiFluxProviderDefaults.ProviderName,
                ProviderKind: null,
                ProviderConnectorKeys.ComfyUi,
                ComfyUiFluxProviderDefaults.DefaultBaseUrl,
                ApiKeySecretId: null,
                ComfyUiFluxProviderDefaults.DefaultModel,
                ComfyUiFluxProviderDefaults.TimeoutSeconds,
                SupportsStreaming: false,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                BuildManagedProviderConfigurationJson(
                    ComfyUiFluxProviderDefaults.CreateConfigurationJson(),
                    ProviderConnectorKeys.ComfyUi,
                    secretRecordId: null,
                    ComfyUiFluxProviderDefaults.TimeoutSeconds,
                    CanDoItAll.AgentFramework.Models.ProviderKind.ComfyUi,
                    ProviderTransportKind.ChatCompletions,
                    ProviderProfilePurpose.ImageGeneration,
                    ComfyUiFluxProviderDefaults.DefaultModel,
                    ["comfyui", "flux", "image", "image-generation", "local"],
                    isPrivateProvider: true,
                    ProviderPricingDefaults.CreateDefaultPrices(
                        CanDoItAll.AgentFramework.Models.ProviderKind.ComfyUi,
                        ComfyUiFluxProviderDefaults.DefaultModel))),
            new ManagedCatalogProviderSeed(
                RuntimeBootstrapLocalOllamaProviderId,
                RuntimeBootstrapLocalOllamaProviderName,
                ProviderKind.OllamaLocal,
                ProviderConnectorKeys.Ollama,
                RuntimeBootstrapLocalOllamaBaseUrl,
                ApiKeySecretId: null,
                RuntimeBootstrapLocalOllamaModel,
                RuntimeBootstrapLocalOllamaTimeoutSeconds,
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: true,
                BuildManagedProviderConfigurationJson(
                    JsonSerializer.Serialize(new {
                        history = "framework-managed",
                        local = true,
                        modelParameters = new {
                            numPredict =
                                AgentProviderModelParameterPolicy
                                    .DefaultOllamaMaxOutputTokens
                        }
                    }),
                    ProviderConnectorKeys.Ollama,
                    secretRecordId: null,
                    RuntimeBootstrapLocalOllamaTimeoutSeconds,
                    CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
                    ProviderTransportKind.ChatCompletions,
                    ProviderProfilePurpose.Chat,
                    RuntimeBootstrapLocalOllamaModel,
                    ["chat", "local", "ollama"],
                    isPrivateProvider: true,
                    ProviderPricingDefaults.CreateDefaultPrices(
                        CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
                        RuntimeBootstrapLocalOllamaModel)))
        ];
    }

    private static string BuildManagedProviderConfigurationJson(
        string configurationJson,
        string connectorPluginKey,
        Guid? secretRecordId,
        int timeoutSeconds,
        CanDoItAll.AgentFramework.Models.ProviderKind providerKind,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        string defaultModel,
        IEnumerable<string> tags,
        bool isPrivateProvider,
        IReadOnlyList<ProviderModelTokenPrice> modelPrices) {
        var configuration = JsonNode.Parse(configurationJson)?.AsObject()
            ?? new JsonObject();
        configuration["connectorPluginKey"] = connectorPluginKey;
        configuration["configSchemaVersion"] =
            RuntimeBootstrapProviderSchemaVersion;
        configuration["timeoutSeconds"] = timeoutSeconds;
        if (secretRecordId.HasValue) {
            configuration["secretRecordId"] =
                secretRecordId.Value.ToString("D");
            configuration["apiKeyEnvironmentVariable"] =
                RuntimeBootstrapOpenAiApiKeyEnvironmentVariable;
        }

        var tagArray = new JsonArray();
        foreach (var tag in tags
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Select(tag => tag.Trim().TrimStart('#').ToLowerInvariant())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)) {
            tagArray.Add(tag);
        }

        configuration["tags"] = tagArray;
        return SharedProviderProfilePublicationMetadataWriter.Write(
            ProviderPricingMetadata.Write(
                configuration.ToJsonString(),
                isPrivateProvider,
                modelPrices),
            providerKind,
            transport,
            purpose,
            defaultModel,
            []);
    }

    private sealed record ManagedCatalogProviderSeed(
        Guid Id,
        string Name,
        ProviderKind? ProviderKind,
        string ConnectorPluginKey,
        string BaseUrl,
        Guid? ApiKeySecretId,
        string DefaultModel,
        int TimeoutSeconds,
        bool SupportsStreaming,
        bool SupportsToolCalling,
        bool SupportsStructuredOutput,
        string ExtraSettingsJson) {
        public ProviderProfile CreateEntity() {
            return new ProviderProfile {
                Id = Id,
                Name = Name,
                ProviderKind = ProviderKind,
                ConnectorPluginKey = ConnectorPluginKey,
                ConfigSchemaVersion =
                    RuntimeBootstrapProviderSchemaVersion,
                BaseUrl = BaseUrl,
                ApiKeySecretId = ApiKeySecretId,
                DefaultModel = DefaultModel,
                TimeoutSeconds = TimeoutSeconds,
                IsEnabled = true,
                SupportsStreaming = SupportsStreaming,
                SupportsToolCalling = SupportsToolCalling,
                SupportsStructuredOutput = SupportsStructuredOutput,
                SupportsVision = false,
                LastHealthStatus = "Not checked",
                ExtraSettingsJson = ExtraSettingsJson
            };
        }
    }

    private static bool UpdateRuntimeBootstrapOpenAiProvider(ProviderProfile provider) {
        var changed = false;
        if (!string.Equals(provider.Name, RuntimeBootstrapOpenAiProviderName, StringComparison.Ordinal)) {
            provider.Name = RuntimeBootstrapOpenAiProviderName;
            changed = true;
        }

        if (provider.ProviderKind != ProviderKind.OpenAi) {
            provider.ProviderKind = ProviderKind.OpenAi;
            changed = true;
        }

        if (!string.Equals(provider.ConnectorPluginKey, ProviderConnectorKeys.OpenAi, StringComparison.Ordinal)) {
            provider.ConnectorPluginKey = ProviderConnectorKeys.OpenAi;
            changed = true;
        }

        if (!string.Equals(provider.ConfigSchemaVersion, RuntimeBootstrapProviderSchemaVersion, StringComparison.Ordinal)) {
            provider.ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion;
            changed = true;
        }

        if (!string.Equals(provider.BaseUrl, RuntimeBootstrapOpenAiBaseUrl, StringComparison.Ordinal)) {
            provider.BaseUrl = RuntimeBootstrapOpenAiBaseUrl;
            changed = true;
        }

        if (!string.Equals(provider.DefaultModel, RuntimeBootstrapOpenAiModel, StringComparison.Ordinal)) {
            provider.DefaultModel = RuntimeBootstrapOpenAiModel;
            changed = true;
        }

        if (provider.TimeoutSeconds != RuntimeBootstrapOpenAiTimeoutSeconds) {
            provider.TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds;
            changed = true;
        }

        if (!provider.IsEnabled) {
            provider.IsEnabled = true;
            changed = true;
        }

        if (!provider.SupportsStreaming) {
            provider.SupportsStreaming = true;
            changed = true;
        }

        if (!provider.SupportsToolCalling) {
            provider.SupportsToolCalling = true;
            changed = true;
        }

        if (!provider.SupportsStructuredOutput) {
            provider.SupportsStructuredOutput = true;
            changed = true;
        }

        if (provider.SupportsVision) {
            provider.SupportsVision = false;
            changed = true;
        }

        if (!string.Equals(provider.LastHealthStatus, "OpenAI active", StringComparison.Ordinal)) {
            provider.LastHealthStatus = "OpenAI active";
            changed = true;
        }

        return changed;
    }

    private static bool UpdateRuntimeBootstrapOpenAiProviderConfigurationJson(ProviderProfile provider) {
        var expectedExtraSettingsJson =
            BuildRuntimeBootstrapOpenAiProviderConfigurationJson(
                provider.ApiKeySecretId);
        if (string.Equals(provider.ExtraSettingsJson, expectedExtraSettingsJson, StringComparison.Ordinal)) {
            return false;
        }

        provider.ExtraSettingsJson = expectedExtraSettingsJson;
        return true;
    }

    private static string BuildRuntimeBootstrapOpenAiProviderConfigurationJson(
        Guid? secretRecordId)
        => SharedProviderProfilePublicationMetadataWriter.Write(
            ProviderPricingMetadata.Write(
                JsonSerializer.Serialize(new {
                    history = "service-managed",
                    modelParameters = new {
                        reasoningEffort =
                            ManagedSeedProviderFallbacks.DefaultReasoningEffort
                    },
                    apiKeyEnvironmentVariable =
                        RuntimeBootstrapOpenAiApiKeyEnvironmentVariable,
                    connectorPluginKey = ProviderConnectorKeys.OpenAi,
                    configSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
                    secretRecordId = secretRecordId?.ToString("D"),
                    timeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds
                }),
                isPrivateProvider: false,
                ProviderPricingDefaults.CreateDefaultPrices(
                    CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
                    RuntimeBootstrapOpenAiModel)),
            CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            RuntimeBootstrapOpenAiModel,
            RuntimeBootstrapOpenAiSuggestedModels);
    private static bool UpdateRuntimeBootstrapOpenAiChatCompletionsProvider(ProviderProfile provider) {
        var changed = false;
        if (!string.Equals(provider.Name, RuntimeBootstrapOpenAiChatCompletionsProviderName, StringComparison.Ordinal)) {
            provider.Name = RuntimeBootstrapOpenAiChatCompletionsProviderName;
            changed = true;
        }

        if (provider.ProviderKind != ProviderKind.OpenAi) {
            provider.ProviderKind = ProviderKind.OpenAi;
            changed = true;
        }

        if (!string.Equals(provider.ConnectorPluginKey, ProviderConnectorKeys.OpenAi, StringComparison.Ordinal)) {
            provider.ConnectorPluginKey = ProviderConnectorKeys.OpenAi;
            changed = true;
        }

        if (!string.Equals(provider.ConfigSchemaVersion, RuntimeBootstrapProviderSchemaVersion, StringComparison.Ordinal)) {
            provider.ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion;
            changed = true;
        }

        if (!string.Equals(provider.BaseUrl, RuntimeBootstrapOpenAiBaseUrl, StringComparison.Ordinal)) {
            provider.BaseUrl = RuntimeBootstrapOpenAiBaseUrl;
            changed = true;
        }

        if (!string.Equals(provider.DefaultModel, RuntimeBootstrapOpenAiModel, StringComparison.Ordinal)) {
            provider.DefaultModel = RuntimeBootstrapOpenAiModel;
            changed = true;
        }

        if (provider.TimeoutSeconds != RuntimeBootstrapOpenAiTimeoutSeconds) {
            provider.TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds;
            changed = true;
        }

        if (!provider.IsEnabled) {
            provider.IsEnabled = true;
            changed = true;
        }

        if (!provider.SupportsStreaming) {
            provider.SupportsStreaming = true;
            changed = true;
        }

        if (!provider.SupportsToolCalling) {
            provider.SupportsToolCalling = true;
            changed = true;
        }

        if (!provider.SupportsStructuredOutput) {
            provider.SupportsStructuredOutput = true;
            changed = true;
        }

        if (provider.SupportsVision) {
            provider.SupportsVision = false;
            changed = true;
        }

        if (!string.Equals(provider.LastHealthStatus, "OpenAI active", StringComparison.Ordinal)) {
            provider.LastHealthStatus = "OpenAI active";
            changed = true;
        }

        return changed;
    }

    private static bool UpdateRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(ProviderProfile provider) {
        var expectedExtraSettingsJson = BuildRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(provider.ApiKeySecretId);
        if (string.Equals(provider.ExtraSettingsJson, expectedExtraSettingsJson, StringComparison.Ordinal)) {
            return false;
        }

        provider.ExtraSettingsJson = expectedExtraSettingsJson;
        return true;
    }

    private static string BuildRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(Guid? secretRecordId)
        => SharedProviderProfilePublicationMetadataWriter.Write(
            ProviderPricingMetadata.Write(
                JsonSerializer.Serialize(new {
                    history = "framework-managed",
                    apiKeyEnvironmentVariable =
                        RuntimeBootstrapOpenAiApiKeyEnvironmentVariable,
                    connectorPluginKey = ProviderConnectorKeys.OpenAi,
                    configSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
                    secretRecordId = secretRecordId?.ToString("D"),
                    timeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds
                }),
                isPrivateProvider: false,
                ProviderPricingDefaults.CreateDefaultPrices(
                    CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
                    RuntimeBootstrapOpenAiModel)),
            CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            ProviderProfilePurpose.Chat,
            RuntimeBootstrapOpenAiModel,
            RuntimeBootstrapOpenAiSuggestedModels);
}
