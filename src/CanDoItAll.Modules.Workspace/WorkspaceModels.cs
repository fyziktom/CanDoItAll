using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

public enum ProviderKind
{
    OpenAi,
    OllamaLocal,
    OllamaRemote
}

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

    public ProviderKind ProviderKind { get; set; }

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
    ProviderKind ProviderKind,
    string BaseUrl,
    string DefaultModel,
    bool IsEnabled,
    string? LastHealthStatus,
    DateTimeOffset? LastHealthCheckAtUtc);

public sealed class ProviderProfileEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ProviderKind ProviderKind { get; set; } = ProviderKind.OpenAi;

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

    public string ExtraSettingsJson { get; set; } = "{}";
}

public sealed record ProviderHealthResult(bool Success, string Message);

public sealed partial class WorkspaceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    SecretService secretService,
    ProviderRegistry providerRegistry,
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

    public async Task<IReadOnlyList<ProviderProfileSummary>> ListProviderProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProviderProfile>()
            .OrderBy(profile => profile.Name)
            .Select(profile => new ProviderProfileSummary(
                profile.Id,
                profile.Name,
                profile.ProviderKind,
                profile.BaseUrl,
                profile.DefaultModel,
                profile.IsEnabled,
                profile.LastHealthStatus,
                profile.LastHealthCheckAtUtc))
            .ToListAsync(cancellationToken);
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
            ProviderKind = provider.ProviderKind,
            ConnectorPluginKey = provider.ConnectorPluginKey,
            ConfigSchemaVersion = provider.ConfigSchemaVersion,
            BaseUrl = provider.BaseUrl,
            ApiKeySecretId = provider.ApiKeySecretId,
            DefaultModel = provider.DefaultModel,
            TimeoutSeconds = provider.TimeoutSeconds,
            IsEnabled = provider.IsEnabled,
            SupportsStreaming = provider.SupportsStreaming,
            SupportsToolCalling = provider.SupportsToolCalling,
            SupportsStructuredOutput = provider.SupportsStructuredOutput,
            SupportsVision = provider.SupportsVision,
            ExtraSettingsJson = provider.ExtraSettingsJson
        };
    }

    public async Task<Result<Guid>> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Provider profile name is required."));
        }

        if (string.IsNullOrWhiteSpace(model.BaseUrl))
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

        if (model.TimeoutSeconds < 5)
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
        entity.ProviderKind = model.ProviderKind;
        entity.ConnectorPluginKey = providerPlugin.Manifest.PluginKey;
        entity.ConfigSchemaVersion = configSchemaVersion;
        entity.BaseUrl = model.BaseUrl.Trim().TrimEnd('/');
        entity.ApiKeySecretId = model.ApiKeySecretId;
        entity.DefaultModel = ResolveDefaultModel(model);
        entity.TimeoutSeconds = Math.Max(5, model.TimeoutSeconds);
        entity.IsEnabled = model.IsEnabled;
        entity.SupportsStreaming = model.ProviderKind == ProviderKind.OpenAi || model.SupportsStreaming;
        entity.SupportsToolCalling = model.ProviderKind == ProviderKind.OpenAi || model.SupportsToolCalling;
        entity.SupportsStructuredOutput = model.ProviderKind == ProviderKind.OpenAi || model.SupportsStructuredOutput;
        entity.SupportsVision = model.ProviderKind == ProviderKind.OpenAi && model.SupportsVision;
        entity.ExtraSettingsJson = string.IsNullOrWhiteSpace(model.ExtraSettingsJson) ? "{}" : model.ExtraSettingsJson;

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "providers",
            model.Id.HasValue ? "update" : "create",
            $"{(model.Id.HasValue ? "Updated" : "Created")} provider profile",
            $"{entity.Name} ({providerPlugin.Manifest.DisplayName})",
            ArtifactKind: "provider-profile",
            ArtifactId: entity.Id,
            Route: "/settings"), cancellationToken);
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
            Route: "/settings"), cancellationToken);
    }

    public async Task<ProviderHealthResult> TestProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<ProviderProfile>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (provider is null)
        {
            return new ProviderHealthResult(false, "Provider profile not found.");
        }

        var adapter = providerRegistry.Resolve(provider);
        if (adapter is null)
        {
            return new ProviderHealthResult(false, $"No adapter is registered for provider profile '{provider.Name}'.");
        }

        var secretValue = provider.ApiKeySecretId.HasValue
            ? (await secretService.GetAsync(provider.ApiKeySecretId.Value, cancellationToken))?.SecretValue
            : null;

        try
        {
            var result = await adapter.CheckHealthAsync(provider, secretValue, cancellationToken);
            provider.LastHealthCheckAtUtc = clock.GetUtcNow();
            provider.LastHealthStatus = result.Message;

            await dbContext.SaveChangesAsync(cancellationToken);
            await activityStream.RecordAsync(new ActivityWriteRequest(
                "providers",
                "health-check",
                $"Checked provider health for {provider.Name}",
                provider.LastHealthStatus,
                ArtifactKind: "provider-profile",
                ArtifactId: provider.Id,
                Route: "/settings"), cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            provider.LastHealthCheckAtUtc = clock.GetUtcNow();
            provider.LastHealthStatus = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ProviderHealthResult(false, ex.Message);
        }
    }

    public Task<IReadOnlyList<SecretListItem>> ListSecretsAsync(CancellationToken cancellationToken = default)
        => secretService.ListForPickerAsync(cancellationToken);

    private static ProviderProfileEditorModel NewProvider() => new()
    {
        ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
        ConfigSchemaVersion = "1.0",
        BaseUrl = "https://api.openai.com/v1/models",
        DefaultModel = "gpt-4.1",
        IsEnabled = true,
        SupportsStreaming = true,
        SupportsToolCalling = true,
        SupportsStructuredOutput = true
    };

    private static string ResolveDefaultModel(ProviderProfileEditorModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.DefaultModel))
        {
            return model.DefaultModel.Trim();
        }

        return model.ProviderKind switch
        {
            ProviderKind.OpenAi => "gpt-4.1",
            ProviderKind.OllamaLocal or ProviderKind.OllamaRemote => "llama3.1",
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

        var requestedPluginKey = string.IsNullOrWhiteSpace(model.ConnectorPluginKey)
            ? providerRegistry.ResolvePluginKey(model.ProviderKind)
            : model.ConnectorPluginKey.Trim();
        if (!providerRegistry.TryResolve(model.ProviderKind, requestedPluginKey, out providerPlugin))
        {
            return Error.Validation($"No provider adapter is registered for plugin '{requestedPluginKey}'.");
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
}


