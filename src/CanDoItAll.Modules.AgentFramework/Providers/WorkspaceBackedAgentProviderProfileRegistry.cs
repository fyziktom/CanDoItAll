using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using AgentFrameworkProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using WorkspaceProviderProfile = CanDoItAll.Modules.Workspace.ProviderProfile;

internal sealed class WorkspaceBackedAgentProviderProfileRegistry(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ISandboxWorkspaceStore store,
    ProviderRegistry providerRegistry,
    IProviderProfileService providerProfileService) : IProviderProfileRegistry
{
    private const string OpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private const string OpenAiChatCompletionsProviderName = "OpenAI chat completions";
    private static readonly Guid RuntimeFallbackOllamaProviderId = Guid.Parse("12E4C814-E822-0B58-9B9F-52577D7B374E");

    public async Task<IReadOnlyList<AgentFrameworkProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var providers = await dbContext.Set<WorkspaceProviderProfile>()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var mappedProviders = providers
            .Select(MapToAgentFrameworkProvider)
            .ToList();
        await UpsertCatalogProvidersAsync(mappedProviders, cancellationToken);

        return await MergeWithCatalogProvidersAsync(mappedProviders, cancellationToken);
    }

    public async Task<AgentFrameworkProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        if (providerId == RuntimeFallbackOllamaProviderId)
        {
            return CreateRuntimeFallbackOllamaProvider();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<WorkspaceProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == providerId, cancellationToken);

        if (provider is null)
        {
            return await LoadCatalogProviderAsync(providerId, cancellationToken);
        }

        var mappedProvider = MapToAgentFrameworkProvider(provider);
        await UpsertCatalogProvidersAsync([mappedProvider], cancellationToken);

        return mappedProvider;
    }

    public async Task<AgentFrameworkProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!providerId.HasValue)
        {
            return providerProfileService.CreateEditor();
        }

        var provider = await GetProviderAsync(providerId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");
        return providerProfileService.CreateEditor(provider);
    }

    public async Task<Guid> SaveProviderAsync(
        AgentFrameworkProviderProfileEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new InvalidOperationException("Provider profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.BaseUrl))
        {
            throw new InvalidOperationException("Provider base URL is required.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var current = model.Id.HasValue
            ? await dbContext.Set<WorkspaceProviderProfile>()
                .SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        var connectorPluginKey = AgentFrameworkProviderMetadata.ResolveConnectorPluginKey(model, current);
        if (!providerRegistry.TryResolve(connectorPluginKey, out var providerAdapter))
        {
            throw new InvalidOperationException($"No workspace provider adapter is registered for plugin '{connectorPluginKey}'.");
        }

        var configSchemaVersion = AgentFrameworkProviderMetadata.ResolveConfigSchemaVersion(
            model,
            current,
            providerAdapter.Manifest.ConfigurationSchema.Version);
        if (!string.Equals(configSchemaVersion, providerAdapter.Manifest.ConfigurationSchema.Version, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider plugin '{providerAdapter.Manifest.PluginKey}' requires schema '{providerAdapter.Manifest.ConfigurationSchema.Version}', but '{configSchemaVersion}' was supplied.");
        }

        var secretRecordId = AgentFrameworkProviderMetadata.ResolveSecretRecordId(model, current?.ApiKeySecretId);
        if (providerAdapter.Manifest.SecretRequirements.Any(item => item.IsRequired) &&
            !secretRecordId.HasValue &&
            !IsEnvironmentVariableSecretReference(model.ApiKeyEnvironmentVariable))
        {
            throw new InvalidOperationException($"{providerAdapter.Manifest.DisplayName} requires a secret reference.");
        }

        var entity = current ?? new WorkspaceProviderProfile
        {
            Id = model.Id ?? Guid.NewGuid()
        };
        if (current is null)
        {
            dbContext.Set<WorkspaceProviderProfile>().Add(entity);
        }

        var timeoutSeconds = AgentFrameworkProviderMetadata.ResolveTimeoutSeconds(model, current?.TimeoutSeconds ?? 45);
        var selectedTransport = model.Transport;
        var capabilityProfile = providerProfileService.NormalizeImportedProfile(providerProfileService.CreateProfile(model) with
        {
            Transport = selectedTransport
        });
        var featureMatrix = providerProfileService.ResolveFeatureMatrix(capabilityProfile);
        entity.Name = model.Name.Trim();
        entity.ProviderKind = providerAdapter.LegacyProviderKind;
        entity.ConnectorPluginKey = providerAdapter.Manifest.PluginKey;
        entity.ConfigSchemaVersion = configSchemaVersion;
        entity.BaseUrl = model.BaseUrl.Trim().TrimEnd('/');
        entity.ApiKeySecretId = secretRecordId;
        entity.DefaultModel = string.IsNullOrWhiteSpace(model.DefaultModel)
            ? ResolveDefaultModel(providerAdapter.Manifest.PluginKey)
            : model.DefaultModel.Trim();
        entity.TimeoutSeconds = timeoutSeconds;
        entity.IsEnabled = model.IsEnabled;
        entity.SupportsStreaming = model.SupportsStreaming;
        entity.SupportsToolCalling = model.SupportsTools;
        entity.SupportsStructuredOutput = featureMatrix.SupportsStructuredOutput;
        entity.SupportsVision = !string.IsNullOrWhiteSpace(model.ConfigurationJson) &&
                                model.ConfigurationJson.Contains("vision", StringComparison.OrdinalIgnoreCase);
        var modelPrices = ProviderPricingDefaults.NormalizeModelPrices(
            capabilityProfile.Kind,
            entity.DefaultModel,
            ProviderPricingDefaults.FromEditorModels(model.ModelPrices));
        if (!ProviderPricingDefaults.TryValidateModelPrices(modelPrices, out var pricingValidationMessage))
        {
            throw new InvalidOperationException(pricingValidationMessage);
        }

        entity.ExtraSettingsJson = ProviderPricingMetadata.Write(
            AgentFrameworkProviderMetadata.BuildExtraSettingsJson(
                model.ConfigurationJson,
                providerAdapter.Manifest.PluginKey,
                configSchemaVersion,
                secretRecordId,
                timeoutSeconds,
                selectedTransport),
            ProviderPricingDefaults.ResolveIsPrivateProvider(capabilityProfile.Kind, model.IsPrivateProvider),
            modelPrices);

        await dbContext.SaveChangesAsync(cancellationToken);
        await UpsertCatalogProvidersAsync([MapToAgentFrameworkProvider(entity)], cancellationToken);

        return entity.Id;
    }

    public async Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<WorkspaceProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == providerId, cancellationToken);
        if (provider is not null)
        {
            dbContext.Remove(provider);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await store.UpdateCatalogAsync(catalog => catalog with
        {
            Providers = catalog.Providers
                .Where(item => item.Id != providerId)
                .ToList()
        }, cancellationToken);
    }

    public async Task<AgentFrameworkProviderProfile> UpdateProviderAsync(
        Guid providerId,
        Func<AgentFrameworkProviderProfile, AgentFrameworkProviderProfile> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var current = await GetProviderAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");
        var updated = update(current);
        await SaveProviderAsync(providerProfileService.CreateEditor(updated), cancellationToken);
        return (await GetProviderAsync(providerId, cancellationToken))
            ?? throw new InvalidOperationException("Provider profile was not found after update.");
    }

    private async Task UpsertCatalogProvidersAsync(
        IReadOnlyList<AgentFrameworkProviderProfile> providers,
        CancellationToken cancellationToken)
    {
        if (providers.Count == 0)
        {
            return;
        }

        await store.UpdateCatalogAsync(catalog =>
        {
            var providerIds = providers
                .Select(item => item.Id)
                .ToHashSet();

            return catalog with
            {
                Providers = catalog.Providers
                    .Where(item => !providerIds.Contains(item.Id))
                    .Concat(providers)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<AgentFrameworkProviderProfile>> MergeWithCatalogProvidersAsync(
        IReadOnlyList<AgentFrameworkProviderProfile> providers,
        CancellationToken cancellationToken)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var workspaceProviderIds = providers
            .Select(item => item.Id)
            .ToHashSet();

        var mergedProviders = catalog.Providers
            .Concat(providers)
            .GroupBy(item => item.Id)
            .Select(group => group.Last())
            .GroupBy(CreateProviderListIdentity)
            .Select(group => group
                .OrderByDescending(item => workspaceProviderIds.Contains(item.Id))
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!mergedProviders.Any(item =>
                item.Kind == AgentFrameworkProviderKind.Ollama &&
                string.Equals(item.Name, ManagedSeedProviderFallbacks.FallbackProviderName, StringComparison.OrdinalIgnoreCase)))
        {
            mergedProviders.Add(CreateRuntimeFallbackOllamaProvider());
            mergedProviders = mergedProviders
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return mergedProviders;
    }

    private async Task<AgentFrameworkProviderProfile?> LoadCatalogProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var provider = catalog.Providers.FirstOrDefault(item => item.Id == providerId);
        return provider;
    }

    private AgentFrameworkProviderProfile MapToAgentFrameworkProvider(
        WorkspaceProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var mappedKind = provider.ConnectorPluginKey switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => AgentFrameworkProviderKind.OpenAi,
            ProcessMockProviderAdapter.PluginKey => AgentFrameworkProviderKind.OpenAi,
            OpenAiProviderAdapter.PluginKey => AgentFrameworkProviderKind.OpenAi,
            _ => AgentFrameworkProviderKind.Ollama
        };
        var legacyMappedTransport = provider.ConnectorPluginKey switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => ProviderTransportKind.Responses,
            ProcessMockProviderAdapter.PluginKey => ProviderTransportKind.Responses,
            OpenAiProviderAdapter.PluginKey when IsOpenAiChatCompletionsProvider(provider) => ProviderTransportKind.ChatCompletions,
            OpenAiProviderAdapter.PluginKey => ProviderTransportKind.Responses,
            _ => ProviderTransportKind.ChatCompletions
        };
        var mappedTransport = AgentFrameworkProviderMetadata.ResolveTransport(provider, legacyMappedTransport);
        var preferFrameworkManagedChatHistory = mappedKind == AgentFrameworkProviderKind.Ollama ||
                                                mappedTransport == ProviderTransportKind.ChatCompletions;
        var supportsBackgroundResponses = mappedKind == AgentFrameworkProviderKind.OpenAi &&
                                          mappedTransport == ProviderTransportKind.Responses;
        var mappedProvider = new AgentFrameworkProviderProfile(
            provider.Id,
            provider.Name,
            mappedKind,
            provider.BaseUrl,
            ResolveApiKeyEnvironmentVariable(provider, mappedKind),
            provider.DefaultModel,
            mappedTransport,
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsToolCalling,
            preferFrameworkManagedChatHistory,
            supportsBackgroundResponses,
            AgentFrameworkProviderMetadata.BuildConfigurationJson(provider),
            providerRegistry.Resolve(provider)?.Manifest.DisplayName ?? provider.ConnectorPluginKey,
            provider.LastHealthStatus ?? "Not checked",
            provider.LastHealthCheckAtUtc,
            string.IsNullOrWhiteSpace(provider.DefaultModel) ? [] : [provider.DefaultModel]);

        return providerProfileService.NormalizeImportedProfile(mappedProvider);
    }

    private static string ResolveApiKeyEnvironmentVariable(
        WorkspaceProviderProfile provider,
        AgentFrameworkProviderKind mappedKind)
    {
        if (provider.ApiKeySecretId.HasValue)
        {
            return $"secret:{provider.ApiKeySecretId.Value:D}";
        }

        return mappedKind == AgentFrameworkProviderKind.OpenAi
            ? OpenAiApiKeyEnvironmentVariable
            : string.Empty;
    }

    private static bool IsEnvironmentVariableSecretReference(string apiKeyEnvironmentVariable)
    {
        if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable))
        {
            return false;
        }

        return !apiKeyEnvironmentVariable.Trim().StartsWith("secret:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenAiChatCompletionsProvider(
        WorkspaceProviderProfile provider)
    {
        return string.Equals(provider.Name, OpenAiChatCompletionsProviderName, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderListIdentity CreateProviderListIdentity(AgentFrameworkProviderProfile provider)
    {
        return new ProviderListIdentity(
            provider.Kind,
            provider.Name.Trim().ToUpperInvariant());
    }

    private static AgentFrameworkProviderProfile CreateRuntimeFallbackOllamaProvider()
    {
        return new AgentFrameworkProviderProfile(
            RuntimeFallbackOllamaProviderId,
            ManagedSeedProviderFallbacks.FallbackProviderName,
            AgentFrameworkProviderKind.Ollama,
            ManagedSeedProviderFallbacks.FallbackBaseUrl,
            string.Empty,
            ManagedSeedProviderFallbacks.FallbackModel,
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            true,
            false,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                history = "framework-managed",
                fallback = "runtime-remote-ollama",
                timeoutSeconds = ManagedSeedProviderFallbacks.FallbackTimeoutSeconds
            }),
            "Remote Ollama fallback provider kept available for seeded agents.",
            "Not checked",
            null,
            [
                ManagedSeedProviderFallbacks.FallbackModel,
                "qwen3.5:9b",
                "gemma3-12b-128k:latest",
                "deepseek-r1:8b-32k",
                "phi4-16k"
            ])
        {
            IsPrivateProvider = true,
            ModelPrices = ProviderPricingDefaults.CreateDefaultPrices(
                AgentFrameworkProviderKind.Ollama,
                ManagedSeedProviderFallbacks.FallbackModel)
        };
    }

    private static string ResolveDefaultModel(
        string connectorPluginKey)
    {
        return connectorPluginKey switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => ScenarioHarnessProviderAdapter.DefaultModel,
            ProcessMockProviderAdapter.PluginKey => ProcessMockProviderAdapter.DefaultModel,
            OpenAiProviderAdapter.PluginKey => OpenAiProviderAdapter.DefaultModel,
            _ => "llama3.1"
        };
    }

    private readonly record struct ProviderListIdentity(
        AgentFrameworkProviderKind Kind,
        string NormalizedName);
}
