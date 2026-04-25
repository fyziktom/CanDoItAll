using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string WorkspaceName { get; set; } = "CanDoItAll";

    public Guid? DefaultProviderProfileId { get; set; }

    public string DefaultPromptOutputFormat { get; set; } = "Markdown";

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProviderProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public ProviderKind? ProviderKind { get; set; }

    public string ConnectorPluginKey { get; set; } = string.Empty;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public Guid? ApiKeySecretId { get; set; }

    public string DefaultModel { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 45;

    public bool IsEnabled { get; set; } = true;

    public bool SupportsStreaming { get; set; }

    public bool SupportsToolCalling { get; set; }

    public bool SupportsStructuredOutput { get; set; }

    public bool SupportsVision { get; set; }

    public DateTimeOffset? LastHealthCheckAtUtc { get; set; }

    public string? LastHealthStatus { get; set; }

    public string ExtraSettingsJson { get; set; } = "{}";
}

internal sealed class WorkspaceSettingsConfiguration : IEntityTypeConfiguration<WorkspaceSettings>
{
    public void Configure(EntityTypeBuilder<WorkspaceSettings> builder)
    {
        builder.ToTable("Workspace_Settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.WorkspaceName).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.DefaultPromptOutputFormat).HasMaxLength(40).IsRequired();
        builder.Property(settings => settings.Notes).HasColumnType("TEXT");
    }
}

internal sealed class ProviderProfileConfiguration : IEntityTypeConfiguration<ProviderProfile>
{
    public void Configure(EntityTypeBuilder<ProviderProfile> builder)
    {
        builder.ToTable("Workspace_ProviderProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Name).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(profile => profile.ConnectorPluginKey).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.ConfigSchemaVersion).HasMaxLength(40).IsRequired();
        builder.Property(profile => profile.DefaultModel).HasMaxLength(120);
        builder.Property(profile => profile.LastHealthStatus).HasMaxLength(120);
        builder.Property(profile => profile.ExtraSettingsJson).HasColumnType("TEXT");
    }
}

public sealed class WorkspaceSettingsModel
{
    public Guid? DefaultProviderProfileId { get; set; }

    public string WorkspaceName { get; set; } = "CanDoItAll";

    public string DefaultPromptOutputFormat { get; set; } = "Markdown";

    public string Notes { get; set; } = string.Empty;
}

public sealed record ProviderProfileSummary(
    Guid Id,
    string Name,
    ProviderKind? LegacyProviderKind,
    string ConnectorPluginKey,
    string ConnectorDisplayName,
    string BaseUrl,
    string DefaultModel,
    bool IsEnabled,
    string? LastHealthStatus,
    DateTimeOffset? LastHealthCheckAtUtc);

public sealed class ProviderProfileEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ConnectorPluginKey { get; set; } = string.Empty;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

    public Guid? ApiKeySecretId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool SupportsStreaming { get; set; }

    public bool SupportsToolCalling { get; set; }

    public bool SupportsStructuredOutput { get; set; }

    public bool SupportsVision { get; set; }

    public ConnectorConfigState Configuration { get; set; } = new();
}

public sealed record ProviderHealthResult(bool Success, string Message);

public sealed partial class WorkspaceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    SecretService secretService,
    ProviderRegistry providerRegistry,
    IProviderRuntimeGateway providerRuntimeGateway,
    IStorageCatalogService storageCatalogService,
    IStorageDriverRegistry storageDriverRegistry,
    IActivityStream activityStream)
{
    public async Task<WorkspaceSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = (await dbContext.Set<WorkspaceSettings>().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        if (settings is null)
        {
            return new WorkspaceSettingsModel();
        }

        return new WorkspaceSettingsModel
        {
            DefaultProviderProfileId = settings.DefaultProviderProfileId,
            WorkspaceName = settings.WorkspaceName,
            DefaultPromptOutputFormat = settings.DefaultPromptOutputFormat,
            Notes = settings.Notes
        };
    }

    public async Task SaveSettingsAsync(WorkspaceSettingsModel model, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = (await dbContext.Set<WorkspaceSettings>().ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
        if (settings is null)
        {
            settings = new WorkspaceSettings();
            await dbContext.Set<WorkspaceSettings>().AddAsync(settings, cancellationToken);
        }

        settings.WorkspaceName = string.IsNullOrWhiteSpace(model.WorkspaceName) ? "CanDoItAll" : model.WorkspaceName.Trim();
        settings.DefaultProviderProfileId = model.DefaultProviderProfileId;
        settings.DefaultPromptOutputFormat = string.IsNullOrWhiteSpace(model.DefaultPromptOutputFormat) ? "Markdown" : model.DefaultPromptOutputFormat.Trim();
        settings.Notes = model.Notes?.Trim() ?? string.Empty;
        settings.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "workspace",
            "save-defaults",
            "Updated workspace defaults",
            $"Workspace name: {settings.WorkspaceName}.",
            Route: "/settings"), cancellationToken);
    }

    public IReadOnlyList<ConnectorPluginManifest> ListProviderManifests()
    {
        return providerRegistry.ListManifests();
    }

    public async Task<IReadOnlyList<ProviderProfileSummary>> ListProviderProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profiles = await dbContext.Set<ProviderProfile>()
            .OrderBy(profile => profile.Name)
            .ToListAsync(cancellationToken);
        return profiles
            .Select(profile =>
            {
                var providerPlugin = providerRegistry.Resolve(profile);
                return new ProviderProfileSummary(
                profile.Id,
                profile.Name,
                profile.ProviderKind,
                providerPlugin?.Manifest.PluginKey ?? profile.ConnectorPluginKey,
                providerPlugin?.Manifest.DisplayName ?? profile.ConnectorPluginKey,
                profile.BaseUrl,
                profile.DefaultModel,
                profile.IsEnabled,
                profile.LastHealthStatus,
                profile.LastHealthCheckAtUtc);
            })
            .ToList();
    }

    public async Task<ProviderProfileEditorModel> GetProviderAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return NewProvider();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<ProviderProfile>().FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (provider is null)
        {
            return NewProvider();
        }

        return new ProviderProfileEditorModel
        {
            Id = provider.Id,
            Name = provider.Name,
            ConnectorPluginKey = providerRegistry.Resolve(provider)?.Manifest.PluginKey ?? provider.ConnectorPluginKey,
            ConfigSchemaVersion = provider.ConfigSchemaVersion,
            ApiKeySecretId = provider.ApiKeySecretId,
            IsEnabled = provider.IsEnabled,
            SupportsStreaming = provider.SupportsStreaming,
            SupportsToolCalling = provider.SupportsToolCalling,
            SupportsStructuredOutput = provider.SupportsStructuredOutput,
            SupportsVision = provider.SupportsVision,
            Configuration = BuildProviderConfiguration(provider)
        };
    }

    public async Task<Result<Guid>> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Provider profile name is required."));
        }

        var configuredBaseUrl = model.Configuration.GetText(ProviderConnectorFieldKeys.BaseUrl);
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return Result<Guid>.Failure(Error.Validation("Provider base URL is required."));
        }

        var providerResolutionError = TryResolveProviderPlugin(
            model,
            out var providerPlugin,
            out var providerManifest,
            out var configSchemaVersion);
        if (providerResolutionError is not null)
        {
            return Result<Guid>.Failure(providerResolutionError);
        }

        var requiresSecret = providerManifest.SecretRequirements.Any(requirement => requirement.IsRequired);
        if (requiresSecret && !model.ApiKeySecretId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation(
                $"{providerManifest.DisplayName} requires a secret reference."));
        }

        var configuredTimeoutSeconds = model.Configuration.GetNumber(ProviderConnectorFieldKeys.TimeoutSeconds) ?? 45;
        if (configuredTimeoutSeconds < 5)
        {
            return Result<Guid>.Failure(Error.Validation("Provider timeout must be at least five seconds."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<ProviderProfile>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new ProviderProfile();
            await dbContext.Set<ProviderProfile>().AddAsync(entity, cancellationToken);
        }

        entity.Name = model.Name.Trim();
        entity.ConnectorPluginKey = providerPlugin.Manifest.PluginKey;
        entity.ProviderKind = providerPlugin.LegacyProviderKind;
        entity.ConfigSchemaVersion = configSchemaVersion;
        entity.BaseUrl = configuredBaseUrl.Trim().TrimEnd('/');
        entity.ApiKeySecretId = model.ApiKeySecretId;
        entity.DefaultModel = ResolveDefaultModel(model, providerPlugin.Manifest.PluginKey);
        entity.TimeoutSeconds = Math.Max(5, configuredTimeoutSeconds);
        entity.IsEnabled = model.IsEnabled;
        var isResponsesManagedPlugin =
            string.Equals(providerPlugin.Manifest.PluginKey, OpenAiProviderAdapter.PluginKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(providerPlugin.Manifest.PluginKey, ScenarioHarnessProviderAdapter.PluginKey, StringComparison.OrdinalIgnoreCase);
        entity.SupportsStreaming = isResponsesManagedPlugin || model.SupportsStreaming;
        entity.SupportsToolCalling = isResponsesManagedPlugin || model.SupportsToolCalling;
        entity.SupportsStructuredOutput = isResponsesManagedPlugin || model.SupportsStructuredOutput;
        entity.SupportsVision = string.Equals(providerPlugin.Manifest.PluginKey, OpenAiProviderAdapter.PluginKey, StringComparison.OrdinalIgnoreCase) &&
                                model.SupportsVision;
        entity.ExtraSettingsJson = model.Configuration.ToJson();

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "providers",
            model.Id.HasValue ? "update" : "create",
            $"{(model.Id.HasValue ? "Updated" : "Created")} provider profile",
            $"{entity.Name} ({providerPlugin.Manifest.DisplayName})",
            ArtifactKind: "provider-profile",
            ArtifactId: entity.Id,
            Route: "/agents?tab=providers"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<ProviderProfile>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "providers",
            "delete",
            "Deleted provider profile",
            entity.Name,
            ArtifactKind: "provider-profile",
            ArtifactId: entity.Id,
            Route: "/agents?tab=providers"), cancellationToken);
    }

    public Task<ProviderHealthResult> TestProviderAsync(Guid id, CancellationToken cancellationToken = default)
        => providerRuntimeGateway.CheckHealthAsync(id, cancellationToken);

    public Task<IReadOnlyList<SecretListItem>> ListSecretsAsync(CancellationToken cancellationToken = default)
        => secretService.ListForPickerAsync(cancellationToken);

    private static ProviderProfileEditorModel NewProvider() => new()
    {
        ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
        ConfigSchemaVersion = "1.0",
        Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProviderConnectorFieldKeys.BaseUrl] = "https://api.openai.com/v1/models",
            [ProviderConnectorFieldKeys.DefaultModel] = "gpt-4.1",
            [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
        }),
        IsEnabled = true,
        SupportsStreaming = true,
        SupportsToolCalling = true,
        SupportsStructuredOutput = true
    };

    private static string ResolveDefaultModel(ProviderProfileEditorModel model, string pluginKey)
    {
        var configuredModel = model.Configuration.GetText(ProviderConnectorFieldKeys.DefaultModel);
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel.Trim();
        }

        return pluginKey.Trim() switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => ScenarioHarnessProviderAdapter.DefaultModel,
            ProcessMockProviderAdapter.PluginKey => ProcessMockProviderAdapter.DefaultModel,
            OpenAiProviderAdapter.PluginKey => "gpt-4.1",
            OllamaProviderAdapter.PluginKey or OllamaRemoteProviderAdapter.PluginKey => "llama3.1",
            _ => "unknown"
        };
    }

    private Error? TryResolveProviderPlugin(
        ProviderProfileEditorModel model,
        out IProviderAdapter providerPlugin,
        out ConnectorPluginManifest manifest,
        out string configSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(model);

        providerPlugin = default!;
        manifest = default!;
        configSchemaVersion = string.Empty;

        var requestedPluginKey = model.ConnectorPluginKey?.Trim();
        if (!providerRegistry.TryResolve(requestedPluginKey, out providerPlugin))
        {
            return Error.Validation(
                string.IsNullOrWhiteSpace(requestedPluginKey)
                    ? "Select a connector plugin for the provider profile."
                    : $"No provider adapter is registered for plugin '{requestedPluginKey}'.");
        }

        manifest = providerPlugin.Manifest;
        configSchemaVersion = string.IsNullOrWhiteSpace(model.ConfigSchemaVersion)
            ? manifest.ConfigurationSchema.Version
            : model.ConfigSchemaVersion.Trim();
        if (!string.Equals(configSchemaVersion, manifest.ConfigurationSchema.Version, StringComparison.Ordinal))
        {
            return Error.Validation(
                $"Provider plugin '{manifest.PluginKey}' requires config schema version '{manifest.ConfigurationSchema.Version}', but '{configSchemaVersion}' was supplied.");
        }

        return null;
    }

    private static ConnectorConfigState BuildProviderConfiguration(ProviderProfile provider)
    {
        var configuration = ConnectorConfigState.FromJson(provider.ExtraSettingsJson);

        configuration.SetText(ProviderConnectorFieldKeys.BaseUrl, provider.BaseUrl);
        configuration.SetText(ProviderConnectorFieldKeys.DefaultModel, provider.DefaultModel);
        configuration.SetNumber(ProviderConnectorFieldKeys.TimeoutSeconds, provider.TimeoutSeconds);

        return configuration;
    }
}


