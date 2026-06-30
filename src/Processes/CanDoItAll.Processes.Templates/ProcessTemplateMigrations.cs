using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public interface IProcessTemplateMigration
{
    string MigrationId { get; }

    string FromSchemaVersion { get; }

    string ToSchemaVersion { get; }

    JsonDocument Migrate(JsonDocument source);
}

public sealed record ProcessTemplateMigrationPlanResult(
    bool Succeeded,
    IReadOnlyList<IProcessTemplateMigration> Migrations,
    string? ErrorCode,
    string? ErrorMessage);

public sealed class ProcessTemplateMigrationRegistry
{
    private readonly IReadOnlyList<string> orderedSchemaVersions;
    private readonly Dictionary<(string From, string To), IProcessTemplateMigration> migrationsByStep;

    public ProcessTemplateMigrationRegistry(
        IReadOnlyList<string> orderedSchemaVersions,
        IReadOnlyList<IProcessTemplateMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(orderedSchemaVersions);
        ArgumentNullException.ThrowIfNull(migrations);

        if (orderedSchemaVersions.Count == 0)
        {
            throw new ArgumentException("At least one schema version is required.", nameof(orderedSchemaVersions));
        }

        this.orderedSchemaVersions = orderedSchemaVersions;
        migrationsByStep = migrations.ToDictionary(
            migration => (migration.FromSchemaVersion, migration.ToSchemaVersion),
            StringTupleComparer.Ordinal);
    }

    public ProcessTemplateMigrationPlanResult CreatePlan(string fromSchemaVersion, string toSchemaVersion)
    {
        var fromIndex = IndexOf(fromSchemaVersion);
        var toIndex = IndexOf(toSchemaVersion);
        if (fromIndex < 0 || toIndex < 0)
        {
            return Failure("TemplateMigration.UnknownSchema", "Migration plan includes an unknown schema version.");
        }

        if (fromIndex > toIndex)
        {
            return Failure("TemplateMigration.DowngradeNotSupported", "Template migrations only move forward.");
        }

        if (fromIndex == toIndex)
        {
            return new ProcessTemplateMigrationPlanResult(true, [], null, null);
        }

        var planned = new List<IProcessTemplateMigration>();
        for (var index = fromIndex; index < toIndex; index++)
        {
            var from = orderedSchemaVersions[index];
            var to = orderedSchemaVersions[index + 1];
            if (!migrationsByStep.TryGetValue((from, to), out var migration))
            {
                return Failure(
                    "TemplateMigration.MissingIntermediate",
                    $"Missing migration from '{from}' to '{to}'.");
            }

            planned.Add(migration);
        }

        return new ProcessTemplateMigrationPlanResult(true, planned, null, null);
    }

    private int IndexOf(string schemaVersion)
    {
        for (var index = 0; index < orderedSchemaVersions.Count; index++)
        {
            if (string.Equals(orderedSchemaVersions[index], schemaVersion, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static ProcessTemplateMigrationPlanResult Failure(string code, string message)
    {
        return new ProcessTemplateMigrationPlanResult(false, [], code, message);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string From, string To)>
    {
        public static StringTupleComparer Ordinal { get; } = new();

        public bool Equals((string From, string To) x, (string From, string To) y)
        {
            return string.Equals(x.From, y.From, StringComparison.Ordinal) &&
                   string.Equals(x.To, y.To, StringComparison.Ordinal);
        }

        public int GetHashCode((string From, string To) obj)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.From),
                StringComparer.Ordinal.GetHashCode(obj.To));
        }
    }
}
