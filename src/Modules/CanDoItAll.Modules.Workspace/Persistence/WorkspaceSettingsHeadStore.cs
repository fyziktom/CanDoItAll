using System.Globalization;
using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

internal static class WorkspaceSettingsHeadStore {
    internal static Task<WorkspaceSettings?> ReadAsync(WorkspaceSettingsDbContext context, bool tracking,
        CancellationToken cancellationToken) {
        var query = context.Set<WorkspaceSettings>().Where(settings => settings.IsCurrent);
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    internal static async Task ExcludeWritersAsync(WorkspaceSettingsDbContext context, CancellationToken cancellationToken) {
        if (context.Database.IsNpgsql() && context.Database.CurrentTransaction is not null) {
            await context.Database.ExecuteSqlRawAsync("LOCK TABLE \"Workspace_Settings\" IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        } else if (!context.Database.IsInMemory()) {
            throw new InvalidOperationException("Workspace head exclusion requires an existing PostgreSQL transaction or an explicit InMemory fixture.");
        }
    }

    internal static WorkspaceSettings Create(WorkspaceSettingsDbContext context) {
        var settings = new WorkspaceSettings { IsCurrent = true };
        context.Add(settings);
        return settings;
    }

    internal static WorkspaceSettingsSnapshot Snapshot(DatasetNamespaceId dataset, WorkspaceSettings? settings)
        => new(Expectation(dataset, settings), settings is null ? WorkspaceSettingsValues.Capture(new()) : Values(settings));

    internal static WorkspaceSettingsExpectation Expectation(DatasetNamespaceId dataset, WorkspaceSettings? settings)
        => settings is null
            ? new(dataset, WorkspaceSettingsHeadState.Empty, null)
            : new(dataset, WorkspaceSettingsHeadState.Existing, new(settings.Id, settings.ConcurrencyToken));

    internal static WorkspaceSettingsSnapshotV2 SnapshotV2(DatasetNamespaceId dataset, WorkspaceSettings? settings) {
        DecisionDataPolicy? policy = null;
        if (settings?.DecisionDataPolicyJson is { } json) {
            try {
                policy = DecisionDataPolicyCodec.Deserialize(json);
            } catch (JsonException) {
                throw new WorkspaceSettingsPolicyUnavailableException();
            }
        }
        return new(Expectation(dataset, settings), settings is null ? WorkspaceSettingsValues.Capture(new()) : Values(settings), policy);
    }

    internal static WorkspaceSettingsValues Values(WorkspaceSettings settings)
        => new(settings.WorkspaceName, settings.DefaultProviderProfileId, settings.DefaultPromptOutputFormat,
            settings.CurrencyCode, settings.CurrencyCultureName, settings.Notes);

    internal static void Replace(WorkspaceSettingsDbContext context, WorkspaceSettings settings,
        WorkspaceSettingsValues values, DateTimeOffset now) {
        settings.WorkspaceName = values.WorkspaceName;
        settings.DefaultProviderProfileId = values.DefaultProviderProfileId;
        settings.DefaultPromptOutputFormat = values.DefaultPromptOutputFormat;
        settings.CurrencyCode = values.CurrencyCode;
        settings.CurrencyCultureName = values.CurrencyCultureName;
        settings.Notes = values.Notes;
        Advance(context, settings, now);
    }

    internal static void Advance(WorkspaceSettingsDbContext context, WorkspaceSettings settings, DateTimeOffset now) {
        settings.UpdatedAtUtc = now;
        if (context.Entry(settings).State != EntityState.Added) {
            context.Entry(settings).Property(item => item.UpdatedAtUtc).IsModified = true;
        }
    }

    internal static bool TryNormalize(WorkspaceSettingsValues submitted, out WorkspaceSettingsValues normalized) {
        normalized = submitted;
        if (string.IsNullOrWhiteSpace(submitted.WorkspaceName) || submitted.WorkspaceName.Length > 200 || submitted.WorkspaceName.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(submitted.DefaultPromptOutputFormat) || submitted.DefaultPromptOutputFormat.Length > 40 ||
                submitted.CurrencyCode.Length != 3 || !submitted.CurrencyCode.All(char.IsAsciiLetter) ||
                string.IsNullOrWhiteSpace(submitted.CurrencyCultureName) || submitted.CurrencyCultureName.Length > 40 || submitted.Notes.Length > 8192) {
            return false;
        }
        try {
            var culture = CultureInfo.GetCultureInfo(submitted.CurrencyCultureName);
            normalized = new(submitted.WorkspaceName.Trim(), submitted.DefaultProviderProfileId,
                submitted.DefaultPromptOutputFormat.Trim(), submitted.CurrencyCode.ToUpperInvariant(), culture.Name, submitted.Notes.Trim());
            return true;
        } catch (CultureNotFoundException) {
            return false;
        }
    }
}
