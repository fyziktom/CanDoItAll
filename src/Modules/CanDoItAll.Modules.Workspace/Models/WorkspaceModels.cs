using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string WorkspaceName { get; set; } = "CanDoItAll";

    public Guid? DefaultProviderProfileId { get; set; }

    public string DefaultPromptOutputFormat { get; set; } = "Markdown";

    public string CurrencyCode { get; set; } = CurrencyDisplaySettings.Default.CurrencyCode;

    public string CurrencyCultureName { get; set; } = CurrencyDisplaySettings.Default.CultureName;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class WorkspaceSettingsConfiguration : IEntityTypeConfiguration<WorkspaceSettings>
{
    public void Configure(EntityTypeBuilder<WorkspaceSettings> builder)
    {
        builder.ToTable("Workspace_Settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.WorkspaceName).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.DefaultPromptOutputFormat).HasMaxLength(40).IsRequired();
        builder.Property(settings => settings.CurrencyCode).HasMaxLength(3).HasDefaultValue("USD").IsRequired();
        builder.Property(settings => settings.CurrencyCultureName).HasMaxLength(40).HasDefaultValue("en-US").IsRequired();
        builder.Property(settings => settings.Notes).HasColumnType("TEXT");
    }
}

public sealed class WorkspaceSettingsModel
{
    public Guid? DefaultProviderProfileId { get; set; }

    public string WorkspaceName { get; set; } = "CanDoItAll";

    public string DefaultPromptOutputFormat { get; set; } = "Markdown";

    public string CurrencyCode { get; set; } = CurrencyDisplaySettings.Default.CurrencyCode;

    public string CurrencyCultureName { get; set; } = CurrencyDisplaySettings.Default.CultureName;

    public string Notes { get; set; } = string.Empty;
}

public sealed partial class WorkspaceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    SecretService secretService,
    ISecretRuntimeResolver secretRuntimeResolver,
    IStorageCatalogService storageCatalogService,
    IStorageDriverRegistry storageDriverRegistry,
    IActivityStream activityStream,
    CurrencyDisplayState currencyDisplayState)
{
    public async Task<WorkspaceSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.Set<WorkspaceSettings>()
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            var defaultModel = new WorkspaceSettingsModel();
            currencyDisplayState.Update(defaultModel.CurrencyCode, defaultModel.CurrencyCultureName);
            return defaultModel;
        }

        var currencySettings = CurrencyDisplaySettings.Normalize(settings.CurrencyCode, settings.CurrencyCultureName);
        var model = new WorkspaceSettingsModel
        {
            DefaultProviderProfileId = settings.DefaultProviderProfileId,
            WorkspaceName = settings.WorkspaceName,
            DefaultPromptOutputFormat = settings.DefaultPromptOutputFormat,
            CurrencyCode = currencySettings.CurrencyCode,
            CurrencyCultureName = currencySettings.CultureName,
            Notes = settings.Notes
        };
        currencyDisplayState.Update(currencySettings);
        return model;
    }

    public async Task SaveSettingsAsync(WorkspaceSettingsModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.Set<WorkspaceSettings>()
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new WorkspaceSettings();
            await dbContext.Set<WorkspaceSettings>().AddAsync(settings, cancellationToken);
        }

        settings.WorkspaceName = string.IsNullOrWhiteSpace(model.WorkspaceName) ? "CanDoItAll" : model.WorkspaceName.Trim();
        settings.DefaultProviderProfileId = model.DefaultProviderProfileId;
        settings.DefaultPromptOutputFormat = string.IsNullOrWhiteSpace(model.DefaultPromptOutputFormat) ? "Markdown" : model.DefaultPromptOutputFormat.Trim();
        var currencySettings = CurrencyDisplaySettings.Normalize(model.CurrencyCode, model.CurrencyCultureName);
        settings.CurrencyCode = currencySettings.CurrencyCode;
        settings.CurrencyCultureName = currencySettings.CultureName;
        settings.Notes = model.Notes?.Trim() ?? string.Empty;
        settings.UpdatedAtUtc = clock.GetUtcNow();
        currencyDisplayState.Update(currencySettings);

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "workspace",
            "save-defaults",
            "Updated workspace defaults",
            $"Workspace name: {settings.WorkspaceName}.",
            Route: "/settings"), cancellationToken);
    }

    public Task<IReadOnlyList<SecretListItem>> ListSecretsAsync(CancellationToken cancellationToken = default)
        => secretService.ListForPickerAsync(cancellationToken);
}
