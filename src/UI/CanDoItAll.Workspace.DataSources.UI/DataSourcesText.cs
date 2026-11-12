using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.DataSources.Contracts;

namespace CanDoItAll.Workspace.DataSources.UI;

public static class DataSourcesText {
    public static string DescribeSchemaStatus(SchemaHealth schemaHealth) {
        return schemaHealth.Status switch {
            SchemaStatus.Current => "Schema current",
            SchemaStatus.NeedsMigration => "Needs schema",
            SchemaStatus.Unavailable => "Schema check failed",
            _ => "Schema unknown"
        };
    }

    public static string DescribeSchemaTone(SchemaHealth schemaHealth) {
        return schemaHealth.Status switch {
            SchemaStatus.Current => "success",
            SchemaStatus.NeedsMigration => "warning",
            SchemaStatus.Unavailable => "danger",
            _ => "neutral"
        };
    }

    public static AlertStyle ResolveSchemaAlertStyle(SchemaHealth schemaHealth) {
        return schemaHealth.Status switch {
            SchemaStatus.Unavailable => AlertStyle.Danger,
            SchemaStatus.NeedsMigration => AlertStyle.Warning,
            _ => AlertStyle.Info
        };
    }

    public static AlertStyle ResolveMessageAlertStyle(string tone) {
        return tone switch {
            "success" => AlertStyle.Success,
            "warning" => AlertStyle.Warning,
            "danger" => AlertStyle.Danger,
            _ => AlertStyle.Info
        };
    }

    public static bool HasSchemaDetails(SchemaHealth schemaHealth) {
        return schemaHealth.PendingMigrations.Count > 0 || schemaHealth.SchemaIssues.Count > 0;
    }

    public static string DescribeSchemaDetails(SchemaHealth schemaHealth) {
        if (schemaHealth.PendingMigrations.Count > 0) {
            var visibleMigrations = schemaHealth.PendingMigrations.Take(3).ToList();
            var suffix = schemaHealth.PendingMigrations.Count > visibleMigrations.Count
                ? $" and {schemaHealth.PendingMigrations.Count - visibleMigrations.Count} more"
                : string.Empty;
            return $"Pending: {string.Join(", ", visibleMigrations)}{suffix}.";
        }

        var visibleIssues = schemaHealth.SchemaIssues.Take(4).ToList();
        var issueSuffix = schemaHealth.SchemaIssues.Count > visibleIssues.Count
            ? $" and {schemaHealth.SchemaIssues.Count - visibleIssues.Count} more"
            : string.Empty;
        return $"Missing: {string.Join(", ", visibleIssues)}{issueSuffix}.";
    }

    public static string BuildProfileSummary(ProfileSummary profile) {
        var lastUsed = profile.LastUsedUtc.HasValue
            ? $"Last used {FormatTimestamp(profile.LastUsedUtc.Value)}"
            : $"Created {FormatTimestamp(profile.CreatedUtc)}";
        return $"{profile.Descriptor} · {lastUsed}";
    }

    public static string DescribeProvider(DataSourceProvider providerKind) {
        return providerKind switch {
            DataSourceProvider.PostgreSql => "PostgreSQL",
            DataSourceProvider.InMemory => "In-memory",
            _ => providerKind.ToString()
        };
    }

    public static string DescribeSource(DataSourceProvider provider) => provider == DataSourceProvider.PostgreSql ? "PostgreSQL connection" : "In-memory";
    public static string DescribeResolutionSource(DataSourceResolution source) => source switch {
        DataSourceResolution.ExplicitOverride => "Explicit startup override",
        DataSourceResolution.PersistedActiveProfile => "Persisted active profile",
        DataSourceResolution.PersistedCatalogFallback => "Persisted catalog fallback",
        DataSourceResolution.AutoProvisionedPostgreSql => "Auto-provisioned PostgreSQL",
        _ => "Unknown runtime selection"
    };

    public static string DescribeEditorLead(ProfileDraft model) {
        return model.ProviderKind switch {
            DataSourceProvider.PostgreSql
                => "PostgreSQL profiles carry the connection metadata needed to test, create, and activate a runtime database safely.",
            DataSourceProvider.InMemory
                => "In-memory profiles are primarily for test and override scenarios.",
            _ => "Edit the saved data source metadata and runtime path details."
        };
    }

    public static string FormatTimestamp(DateTimeOffset value) {
        return value.LocalDateTime.ToString("g");
    }

}
