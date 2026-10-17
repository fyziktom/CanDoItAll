using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceSettings : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool IsCurrent { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public string WorkspaceName { get; set; } = "CanDoItAll";

    public Guid? DefaultProviderProfileId { get; set; }

    public string DefaultPromptOutputFormat { get; set; } = "Markdown";

    public string CurrencyCode { get; set; } = CurrencyDisplaySettings.Default.CurrencyCode;

    public string CurrencyCultureName { get; set; } = CurrencyDisplaySettings.Default.CultureName;

    public string Notes { get; set; } = string.Empty;

    public string? DecisionDataPolicyJson { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class WorkspaceSettingsConfiguration : IEntityTypeConfiguration<WorkspaceSettings>
{
    public void Configure(EntityTypeBuilder<WorkspaceSettings> builder)
    {
        builder.ToTable("Workspace_Settings", table => table.HasCheckConstraint("CK_Workspace_Settings_DecisionDataPolicyJson",
            $"\"DecisionDataPolicyJson\" IS NULL OR octet_length(\"DecisionDataPolicyJson\") BETWEEN 1 AND {DecisionDataPolicyCodec.MaximumUtf8Bytes}"));
        builder.HasKey(settings => settings.Id);
        builder.HasIndex(settings => settings.IsCurrent).IsUnique().HasFilter("\"IsCurrent\"");
        builder.Property(settings => settings.ConcurrencyToken).IsConcurrencyToken().HasDefaultValueSql("gen_random_uuid()");
        builder.Property(settings => settings.WorkspaceName).HasMaxLength(200).IsRequired();
        builder.Property(settings => settings.DefaultPromptOutputFormat).HasMaxLength(40).IsRequired();
        builder.Property(settings => settings.CurrencyCode).HasMaxLength(3).HasDefaultValue("USD").IsRequired();
        builder.Property(settings => settings.CurrencyCultureName).HasMaxLength(40).HasDefaultValue("en-US").IsRequired();
        builder.Property(settings => settings.Notes).HasColumnType("TEXT");
        builder.Property(settings => settings.DecisionDataPolicyJson).HasColumnType("TEXT");
    }
}

public sealed partial class WorkspaceService(
    IDbContextFactory<WorkspaceSettingsDbContext> dbContextFactory,
    IClock clock,
    SecretService secretService,
    ISecretRuntimeResolver secretRuntimeResolver,
    IStorageCatalogService storageCatalogService,
    IStorageDriverRegistry storageDriverRegistry,
    IActivityStream activityStream,
    CurrencyDisplayState currencyDisplayState,
    WorkspaceSettingsWriter settingsWriter)
{
    public async Task<WorkspaceSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await WorkspaceSettingsHeadStore.ReadAsync(dbContext, tracking: false, cancellationToken);
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

    public Task<WorkspaceSettingsSnapshot> ReadSettingsSnapshotAsync(CancellationToken cancellationToken = default)
        => settingsWriter.ReadAsync(cancellationToken);

    public Task<WorkspaceSettingsSaveOutcome> SaveSettingsAsync(ReplaceWorkspaceSettingsCommand command, CancellationToken cancellationToken = default)
        => settingsWriter.ReplaceAsync(command, cancellationToken);

    public Task<WorkspaceSettingsSnapshotV2> ReadSettingsSnapshotV2Async(CancellationToken cancellationToken = default)
        => settingsWriter.ReadV2Async(cancellationToken);

    public Task<WorkspaceSettingsSaveOutcomeV2> SaveSettingsV2Async(ReplaceWorkspaceSettingsV2Command command,
        CancellationToken cancellationToken = default)
        => settingsWriter.ReplaceV2Async(command, cancellationToken);

    public Task<IReadOnlyList<SecretListItem>> ListSecretsAsync(CancellationToken cancellationToken = default)
        => secretService.ListForPickerAsync(cancellationToken);
}
