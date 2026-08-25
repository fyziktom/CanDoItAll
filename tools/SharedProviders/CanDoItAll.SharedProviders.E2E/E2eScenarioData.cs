using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.SharedProviders.E2E;

internal sealed class E2eScenarioData
{
    private const long MaximumLogScanBytes = 32L * 1024 * 1024;
    private const long MaximumManifestBytes = 64L * 1024;

    private static readonly IReadOnlyList<string> ExpectedDockerServices =
    [
        "artifact-permissions",
        "central",
        "client-a",
        "client-b",
        "db",
        "deterministic-personal-upstream",
        "deterministic-upstream",
        "e2e-central",
        "e2e-client-a",
        "e2e-client-b",
        "e2e-runner"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };

    private readonly E2eScenarioOptions options;
    private readonly DurableFileWriter writer = new(new PhysicalFileSystemPathPolicyFactory());

    public E2eScenarioData(E2eScenarioOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
    }

    public Task<E2eStateSnapshot> ReadSnapshotAsync(
        E2eRole role,
        CancellationToken cancellationToken)
        => ReadJsonAsync<E2eStateSnapshot>(
            Path.Combine(
                options.ArtifactRootPath,
                "handoff",
                $"{E2eCommandLine.ToToken(role)}-state.json"),
            "A required role snapshot is missing or invalid.",
            cancellationToken);

    public Task<E2eStateSnapshot> ReadCheckpointSnapshotAsync(
        E2eRole role,
        string checkpoint,
        CancellationToken cancellationToken)
        => ReadJsonAsync<E2eStateSnapshot>(
            Path.Combine(
                options.ArtifactRootPath,
                "handoff",
                $"{E2eCommandLine.ToToken(role)}-{checkpoint}-state.json"),
            "A required role checkpoint snapshot is missing or invalid.",
            cancellationToken);

    public Task<E2eSyncOutcome> ReadSyncOutcomeAsync(
        E2eRole role,
        CancellationToken cancellationToken)
        => ReadJsonAsync<E2eSyncOutcome>(
            Path.Combine(
                options.ArtifactRootPath,
                "handoff",
                $"{E2eCommandLine.ToToken(role)}-sync-outcome.json"),
            "A required source-sync outcome artifact is missing or invalid.",
            cancellationToken);

    public async Task<E2eScenarioBaseline> CaptureBaselineAsync(
        E2eStateSnapshot central,
        E2eStateSnapshot clientA,
        E2eStateSnapshot clientB,
        CancellationToken cancellationToken)
    {
        var baseline = new E2eScenarioBaseline(
            SchemaVersion: 1,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            central,
            clientA,
            clientB);
        await writer.WriteTextAsync(
            options.ArtifactRootPath,
            Path.Combine(options.ArtifactRootPath, "scenario-results", "baseline-state.json"),
            JsonSerializer.Serialize(baseline, JsonOptions),
            DurableFileWriteOptions.Default,
            cancellationToken);
        return baseline;
    }

    public Task<E2eScenarioBaseline> ReadBaselineAsync(CancellationToken cancellationToken)
        => ReadJsonAsync<E2eScenarioBaseline>(
            Path.Combine(options.ArtifactRootPath, "scenario-results", "baseline-state.json"),
            "The normal-phase scenario baseline is missing or invalid.",
            cancellationToken);

    public Task WriteCatalogEvidenceAsync(
        string rawCatalogJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawCatalogJson);
        using var _ = JsonDocument.Parse(rawCatalogJson);
        return writer.WriteTextAsync(
            options.ArtifactRootPath,
            Path.Combine(options.ArtifactRootPath, "scenario-results", "catalog-evidence.json"),
            rawCatalogJson,
            DurableFileWriteOptions.Default,
            cancellationToken);
    }

    public async Task<E2eDatabaseIsolationObservation> ObserveDatabaseIsolationAsync(
        CancellationToken cancellationToken)
    {
        var central = await ReadConnectionStringAsync(
            options.CentralDatabaseConnectionStringFilePath,
            cancellationToken);
        var clientA = await ReadConnectionStringAsync(
            options.ClientADatabaseConnectionStringFilePath,
            cancellationToken);
        var clientB = await ReadConnectionStringAsync(
            options.ClientBDatabaseConnectionStringFilePath,
            cancellationToken);
        var configured = new[] { central, clientA, clientB };
        var builders = configured.Select(value => new NpgsqlConnectionStringBuilder(value)).ToArray();
        var validConnections = new bool[builders.Length];
        var providerCounts = new int[builders.Length];
        for (var index = 0; index < builders.Length; index++)
        {
            await using var context = CreateContext(builders[index].ConnectionString);
            validConnections[index] = await context.Database.CanConnectAsync(cancellationToken);
            if (validConnections[index])
            {
                providerCounts[index] = await context.Set<ProviderProfile>()
                    .AsNoTracking()
                    .CountAsync(cancellationToken);
            }
        }

        var crossRoleDenied = new List<bool>();
        for (var source = 0; source < builders.Length; source++)
        {
            for (var target = 0; target < builders.Length; target++)
            {
                if (source == target)
                {
                    continue;
                }

                var mismatched = new NpgsqlConnectionStringBuilder(builders[source].ConnectionString)
                {
                    Database = builders[target].Database,
                    Timeout = 3,
                    CommandTimeout = 3,
                    Pooling = false
                };
                await using var context = CreateContext(mismatched.ConnectionString);
                crossRoleDenied.Add(!await context.Database.CanConnectAsync(cancellationToken));
            }
        }

        return new E2eDatabaseIsolationObservation(
            validConnections.All(value => value),
            providerCounts.All(count => count > 0),
            builders.Select(builder => builder.Database).Distinct(StringComparer.Ordinal).Count() == 3,
            builders.Select(builder => builder.Username).Distinct(StringComparer.Ordinal).Count() == 3,
            crossRoleDenied.Count == 6 && crossRoleDenied.All(value => value));
    }

    public async Task<IReadOnlyList<string>> ReadKnownSensitiveValuesAsync(
        CancellationToken cancellationToken)
    {
        var values = new List<string>
        {
            await E2eSecretFile.ReadRequiredAsync(
                options.UpstreamControlTokenFilePath,
                "upstream control token",
                cancellationToken),
            await E2eSecretFile.ReadRequiredAsync(
                options.PersonalUpstreamControlTokenFilePath,
                "personal upstream control token",
                cancellationToken)
        };
        foreach (var credentialFile in new[]
                 {
                     E2eFixtures.CentralAccessCredentialFileName,
                     E2eFixtures.CentralCatalogOnlyCredentialFileName,
                     E2eFixtures.CentralInvokeOnlyCredentialFileName,
                     E2eFixtures.ClientAAccessCredentialFileName,
                     E2eFixtures.ClientBAccessCredentialFileName
                 })
        {
            values.Add(await E2eSecretFile.ReadRequiredAsync(
                Path.Combine(options.ArtifactRootPath, "credentials", credentialFile),
                "generated access token",
                cancellationToken));
        }

        foreach (var connectionStringFile in new[]
                 {
                     options.CentralDatabaseConnectionStringFilePath,
                     options.ClientADatabaseConnectionStringFilePath,
                     options.ClientBDatabaseConnectionStringFilePath
                 })
        {
            var connectionString = await ReadConnectionStringAsync(
                connectionStringFile,
                cancellationToken);
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            values.Add(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                values.Add(builder.Password);
            }
        }

        return values.Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<E2eAuditObservation> ObserveAuditAsync(
        string accessContext,
        string contentCanary,
        IReadOnlyCollection<string> sensitiveValues,
        string? expectedTraceId,
        CancellationToken cancellationToken)
    {
        var connectionString = await ReadConnectionStringAsync(
            options.CentralDatabaseConnectionStringFilePath,
            cancellationToken);
        await using var context = CreateContext(connectionString);
        var records = await context.Set<SharedProviderInvocationRecord>()
            .AsNoTracking()
            .OrderBy(item => item.StartedAtUtc)
            .ToArrayAsync(cancellationToken);
        var accessContextObserved = records.Any(record =>
            string.Equals(
                record.AccessContextReference?.Value,
                accessContext,
                StringComparison.Ordinal));
        var tracedRecord = expectedTraceId is null
            ? null
            : records.SingleOrDefault(record =>
                string.Equals(
                    record.AccessContextReference?.Value,
                    accessContext,
                    StringComparison.Ordinal) &&
                string.Equals(record.TraceId, expectedTraceId, StringComparison.Ordinal));
        var traceIdObserved = expectedTraceId is null || tracedRecord is not null;
        var contextsIndependent = expectedTraceId is null ||
            tracedRecord is not null &&
            tracedRecord.CorrelationId.Length is > 0 and <= 128 &&
            !string.Equals(tracedRecord.CorrelationId, expectedTraceId, StringComparison.Ordinal) &&
            !string.Equals(tracedRecord.CorrelationId, accessContext, StringComparison.Ordinal);
        var serializedMetadata = records.Select(record => string.Join(
            '\n',
            record.RequestId,
            record.AuthenticatedSubject,
            record.AccessContextReference?.Value,
            record.TraceId,
            record.CorrelationId,
            record.PublicModelId.Value,
            record.UpstreamModelId,
            record.FailureCategory?.ToString(),
            record.Outcome.ToString(),
            record.UsageCompleteness.ToString(),
            record.PricingCompleteness.ToString()));
        var contentAbsent = serializedMetadata.All(value =>
            !value.Contains(contentCanary, StringComparison.Ordinal));
        var secretsAbsent = serializedMetadata.All(value => sensitiveValues.All(secret =>
            !string.IsNullOrEmpty(secret) &&
            !value.Contains(secret, StringComparison.Ordinal)));
        var completed = records.Length > 0 && records.All(record =>
            record.Outcome != SharedProviderInvocationOutcome.InProgress &&
            record.CompletedAtUtc.HasValue &&
            record.DurationMilliseconds.HasValue);
        var usageTruthful = records.All(record => record.Operation switch
        {
            CanDoItAll.SharedProviders.Abstractions.SharedProviderRelayOperation.ChatCompletions or
            CanDoItAll.SharedProviders.Abstractions.SharedProviderRelayOperation.Responses =>
                record.UsageCompleteness switch
                {
                    SharedProviderMetadataCompleteness.Complete =>
                        record.InputTokenCount is > 0 &&
                        record.OutputTokenCount is > 0 &&
                        record.ImageCount is null,
                    SharedProviderMetadataCompleteness.Unavailable =>
                        record.InputTokenCount is null &&
                        record.OutputTokenCount is null &&
                        record.ImageCount is null,
                    _ => false
                },
            CanDoItAll.SharedProviders.Abstractions.SharedProviderRelayOperation.ImageGenerations =>
                record.UsageCompleteness switch
                {
                    SharedProviderMetadataCompleteness.Complete =>
                        record.ImageCount is > 0 &&
                        record.InputTokenCount is null &&
                        record.OutputTokenCount is null,
                    SharedProviderMetadataCompleteness.Unavailable =>
                        record.ImageCount is null &&
                        record.InputTokenCount is null &&
                        record.OutputTokenCount is null,
                    _ => false
                },
            _ => false
        });
        return new E2eAuditObservation(
            records.Length,
            accessContextObserved,
            contentAbsent,
            secretsAbsent,
            completed,
            usageTruthful,
            traceIdObserved,
            contextsIndependent);
    }

    public async Task<E2eLogObservation> ObserveLogsAsync(
        string contentCanary,
        IReadOnlyCollection<string> sensitiveValues,
        CancellationToken cancellationToken)
    {
        var logsRoot = Path.Combine(options.ArtifactRootPath, "logs");
        if (!Directory.Exists(logsRoot))
        {
            return E2eLogObservation.Missing;
        }

        var requiredSources = new[] { "docker", "central", "client-a", "client-b" };
        var sourcesComplete = true;
        var payloadFiles = new List<string>();
        foreach (var source in requiredSources)
        {
            var sourceRoot = Path.Combine(logsRoot, source);
            var manifest = await TryReadJsonAsync<E2eLogCollectionManifest>(
                Path.Combine(sourceRoot, "collection.json"),
                cancellationToken);
            var sourcePayloads = Directory.Exists(sourceRoot)
                ? Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                    .Where(path => !string.Equals(
                        Path.GetFullPath(path),
                        Path.GetFullPath(Path.Combine(sourceRoot, "collection.json")),
                        StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : [];
            var payloadBytes = sourcePayloads.Sum(path => new FileInfo(path).Length);
            var dockerCoverageValid = source != "docker" ||
                manifest?.ServiceCoverage is { } services &&
                services.SequenceEqual(ExpectedDockerServices, StringComparer.Ordinal);
            sourcesComplete &= manifest is
                {
                    SchemaVersion: 1,
                    Successful: true,
                    PayloadFileCount: > 0,
                    PayloadBytes: > 0,
                    CollectedAtUtc: { } collectedAtUtc
                } &&
                collectedAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1) &&
                string.Equals(manifest.SourceId, source, StringComparison.Ordinal) &&
                manifest.PayloadFileCount == sourcePayloads.Length &&
                manifest.PayloadBytes == payloadBytes &&
                dockerCoverageValid;
            payloadFiles.AddRange(sourcePayloads);
        }
        var hostSecretScan = await TryReadJsonAsync<E2eHostSecretScan>(
            Path.Combine(logsRoot, "host-secret-scan.json"),
            cancellationToken);
        var databaseScan = await TryReadJsonAsync<E2eHostDatabaseScan>(
            Path.Combine(logsRoot, "host-database-scan.json"),
            cancellationToken);

        long totalBytes = 0;
        var files = payloadFiles
            .Append(Path.Combine(logsRoot, "host-secret-scan.json"))
            .Append(Path.Combine(logsRoot, "host-database-scan.json"))
            .Where(File.Exists)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            return E2eLogObservation.Missing;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            totalBytes += info.Length;
            if (totalBytes > MaximumLogScanBytes)
            {
                throw new E2eSafeException("The E2E log scan exceeded its bounded input limit.");
            }

            var text = await File.ReadAllTextAsync(file, cancellationToken);
            if (text.Contains(contentCanary, StringComparison.Ordinal) ||
                sensitiveValues.Any(secret =>
                    !string.IsNullOrEmpty(secret) && text.Contains(secret, StringComparison.Ordinal)))
            {
                return new E2eLogObservation(
                    sourcesComplete,
                    KnownValuesAbsent: false,
                    HostSecretScanComplete: false,
                    HostDatabaseScanComplete: false);
            }
        }

        return new E2eLogObservation(
            sourcesComplete,
            KnownValuesAbsent: true,
            hostSecretScan is
            {
                SchemaVersion: 1,
                Clean: true,
                SecretCount: 14,
                MissingInputCount: 0,
                ScannedFileCount: > 0,
                ScannedBytes: > 0
            },
            databaseScan is
            {
                SchemaVersion: 1,
                Clean: true,
                DatabaseCount: 3,
                MissingDatabaseCount: 0,
                RuntimeSecretCount: 14,
                GeneratedCredentialCount: 5,
                ContentCanaryCount: 1,
                CandidateCount: 20,
                ScannedBytes: > 0
            } &&
            HasCompleteDatabaseCoverage(databaseScan));
    }

    private static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .EnableDetailedErrors(false)
            .EnableSensitiveDataLogging(false)
            .Options;
        return new AppDbContext(options);
    }

    private static bool HasCompleteDatabaseCoverage(E2eHostDatabaseScan scan)
    {
        var expectedRoles = new[] { "central", "client-a", "client-b" };
        return scan.DatabaseCoverage is { Count: 3 } coverage &&
            coverage
                .OrderBy(item => item.Role, StringComparer.Ordinal)
                .Select(item => item.Role)
                .SequenceEqual(expectedRoles, StringComparer.Ordinal) &&
            coverage.All(item => item.Successful && item.ScannedBytes > 0) &&
            coverage.Sum(item => item.ScannedBytes) == scan.ScannedBytes;
    }

    private static Task<string> ReadConnectionStringAsync(
        string path,
        CancellationToken cancellationToken)
        => E2eSecretFile.ReadRequiredAsync(path, "database connection string", cancellationToken);

    private static async Task<T> ReadJsonAsync<T>(
        string path,
        string safeFailure,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new E2eSafeException(safeFailure);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
                ?? throw new E2eSafeException(safeFailure);
        }
        catch (JsonException exception)
        {
            throw new E2eSafeException(safeFailure, exception);
        }
    }

    private static async Task<T?> TryReadJsonAsync<T>(
        string path,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new FileInfo(path);
        if (info.Length <= 0 ||
            info.Length > MaximumManifestBytes ||
            info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed record E2eScenarioBaseline(
    int SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    E2eStateSnapshot Central,
    E2eStateSnapshot ClientA,
    E2eStateSnapshot ClientB);

internal sealed record E2eDatabaseIsolationObservation(
    bool AllRolesConnect,
    bool AllRolesQueryable,
    bool DistinctDatabases,
    bool DistinctUsers,
    bool AllCrossRoleConnectionsDenied);

internal sealed record E2eAuditObservation(
    int InvocationCount,
    bool AccessContextObserved,
    bool ContentAbsent,
    bool SecretsAbsent,
    bool AllInvocationsCompleted,
    bool UsageTruthful,
    bool TraceIdObserved,
    bool ContextsIndependent);

internal sealed record E2eLogObservation(
    bool SourcesComplete,
    bool KnownValuesAbsent,
    bool HostSecretScanComplete,
    bool HostDatabaseScanComplete)
{
    public static E2eLogObservation Missing { get; } = new(
        SourcesComplete: false,
        KnownValuesAbsent: false,
        HostSecretScanComplete: false,
        HostDatabaseScanComplete: false);
}

internal sealed record E2eHostSecretScan(
    int SchemaVersion,
    bool Clean,
    int SecretCount,
    int MissingInputCount,
    int ScannedFileCount,
    long ScannedBytes);

internal sealed record E2eLogCollectionManifest(
    int SchemaVersion,
    string SourceId,
    bool Successful,
    int PayloadFileCount,
    long PayloadBytes,
    IReadOnlyList<string>? ServiceCoverage,
    DateTimeOffset CollectedAtUtc);

internal sealed record E2eHostDatabaseScan(
    int SchemaVersion,
    bool Clean,
    int DatabaseCount,
    int MissingDatabaseCount,
    int RuntimeSecretCount,
    int GeneratedCredentialCount,
    int ContentCanaryCount,
    int CandidateCount,
    long ScannedBytes,
    IReadOnlyList<E2eDatabaseScanCoverage> DatabaseCoverage);

internal sealed record E2eDatabaseScanCoverage(
    string Role,
    bool Successful,
    long ScannedBytes);
