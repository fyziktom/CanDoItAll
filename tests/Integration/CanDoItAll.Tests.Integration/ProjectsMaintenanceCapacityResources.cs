using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Tests.Support;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal enum CapacityResourcePhase {
    CellIntent,
    EnvironmentAllocated,
    DatabaseAllocated,
    ApplicationReady,
    WorkloadDeclared,
    CleanupStarted,
    ProviderDisposed,
    ProviderCleanupFailed,
    EnvironmentDisposed,
    EnvironmentCleanupFailed,
    CleanupComplete,
    CleanupIncomplete,
    WorkerPrepared,
    WorkerRunning,
    WorkerExited,
    WorkerDisposed,
    WorkerDisposedWithIncompleteSessionEvidence
}

internal sealed class ProjectsMaintenanceCapacityResources {
    internal const string OutputRootEnvironment = "CANDOITALL_TESTS_MAINTENANCE_CAPACITY_OUTPUT_ROOT";
    private const int ManifestVersion = 1;
    private readonly string outputRoot;
    private readonly DateTime parentStartedAtUtc;
    private int sequence;
    private bool environmentAllocationStarted;
    private bool databaseAllocationStarted;
    private bool databaseAllocationReturned;

    private ProjectsMaintenanceCapacityResources(string outputRoot) {
        this.outputRoot = outputRoot;
        Run = Guid.NewGuid();
        ProfileKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(10));
        CellParent = Path.Combine(outputRoot, $"resources-{Run:N}");
        using var parent = Process.GetCurrentProcess();
        parentStartedAtUtc = parent.StartTime.ToUniversalTime();
    }

    internal Guid Run { get; }
    internal string ProfileKey { get; }
    internal string CellParent { get; }

    internal static async Task<ProjectsMaintenanceCapacityResources> CreateAsync(int cadence, int workers, bool mixed,
        long approvedBytes, CancellationToken cancellationToken) {
        var configured = Environment.GetEnvironmentVariable(OutputRootEnvironment);
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)) {
            throw new InvalidOperationException($"{OutputRootEnvironment} must name an existing absolute runner-owned evidence directory.");
        }
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        if (!Directory.Exists(root) || root == Path.GetPathRoot(root)) {
            throw new InvalidOperationException("The capacity evidence root must exist and cannot be a filesystem root.");
        }
        RequireNoReparseAncestors(root);
        var owned = new ProjectsMaintenanceCapacityResources(root);
        if (Path.Exists(owned.CellParent)) {
            throw new InvalidOperationException("A capacity cell cannot reuse an existing resource parent.");
        }
        await owned.RecordAsync(CapacityResourcePhase.CellIntent, new {
            CadenceSeconds = cadence, Workers = workers, Mixed = mixed, ApprovedPhysicalBytes = approvedBytes,
            owned.CellParent, owned.ProfileKey, ExpectedDatabasePrefix = $"cditall_{owned.ProfileKey}_",
            DatabaseSuffix = "Exactly 32 lowercase hexadecimal characters; at most one match on the runner-recorded owned fixture",
            CellBudgetSeconds = 1800, RowBudgetSeconds = 3900,
            EvidenceRoot = root, FixtureIdentity = "Explicit parent runner ownership record; never inferred from ordinary host configuration"
        }, cancellationToken);
        return owned;
    }

    internal CanDoItAllTestEnvironment CreateEnvironment() {
        RequireNestedPath(CellParent);
        if (Path.Exists(CellParent)) {
            throw new InvalidOperationException("The pre-recorded capacity resource parent already exists.");
        }
        environmentAllocationStarted = true;
        return CanDoItAllTestEnvironment.CreateUnder(CellParent, "environment");
    }

    internal TestDatabaseProfile CreateProfile(CanDoItAllTestEnvironment environment) {
        RequireEnvironmentPath(environment);
        databaseAllocationStarted = true;
        var profile = environment.CreatePostgreSqlProfile(ProfileKey);
        databaseAllocationReturned = true;
        return profile;
    }

    internal Task RecordEnvironmentAsync(CanDoItAllTestEnvironment environment, CancellationToken token) {
        RequireEnvironmentPath(environment);
        return RecordAsync(CapacityResourcePhase.EnvironmentAllocated, new {
            Root = environment.RootPath, environment.ControlPlaneRootPath, environment.ProfilesRootPath
        }, token);
    }

    internal Task RecordDatabaseAsync(TestDatabaseProfile profile, CancellationToken token) {
        RequireNestedPath(profile.EnvironmentRootPath);
        RequireNestedPath(profile.ProfileRootPath);
        if (profile.ProfileKey != ProfileKey) {
            throw new InvalidDataException("The allocated profile does not belong to the pre-recorded capacity cell.");
        }
        var database = new NpgsqlConnectionStringBuilder(profile.ConnectionString);
        RequireDatabaseName(ProfileKey, database.Database);
        return RecordAsync(CapacityResourcePhase.DatabaseAllocated, new {
            profile.ProfileKey, DatabaseName = database.Database, FixtureHost = database.Host, FixturePort = database.Port,
            Root = profile.EnvironmentRootPath, profile.ProfileRootPath, profile.WorkspaceRootPath
        }, token);
    }

    internal static void RequireDatabaseName(string profileKey, string databaseName) {
        if (profileKey.Length != 20 || !profileKey.All(IsLowerHex)) {
            throw new InvalidDataException("The capacity profile key must be exactly 20 lowercase hexadecimal characters.");
        }
        var prefix = $"cditall_{profileKey}_";
        if (!databaseName.StartsWith(prefix, StringComparison.Ordinal) || databaseName.Length != prefix.Length + 32 ||
                !databaseName.AsSpan(prefix.Length).ToArray().All(IsLowerHex)) {
            throw new InvalidDataException("The actual database name is outside the pre-recorded exact capacity namespace.");
        }
    }

    internal async Task RecordAsync<T>(CapacityResourcePhase phase, T detail, CancellationToken token) {
        RequireNoReparseAncestors(outputRoot);
        var ordinal = Interlocked.Increment(ref sequence);
        if (ordinal > 128) {
            throw new InvalidOperationException("The capacity cell exceeded its bounded resource manifest count.");
        }
        var path = Path.Combine(outputRoot, $"capacity-{Run:N}-{ordinal:D3}-{phase}.json");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(stream, new {
            SchemaVersion = ManifestVersion, Owner = nameof(ProjectsMaintenanceCapacityTests), Run,
            Sequence = ordinal, Phase = phase.ToString(), RecordedAtUtc = DateTimeOffset.UtcNow,
            ParentProcessId = Environment.ProcessId, ParentStartedAtUtc = parentStartedAtUtc, Detail = detail
        }, cancellationToken: token);
        await stream.FlushAsync(token);
        stream.Flush(flushToDisk: true);
    }

    internal async Task<Exception?> CleanupAsync(TestApplication? application, CanDoItAllTestEnvironment? environment, Exception? primary) {
        Exception? cleanupFailure = null;
        var allocationReconciliationRequired = environmentAllocationStarted && environment is null ||
            databaseAllocationStarted && !databaseAllocationReturned;
        await RecordCleanupAsync(CapacityResourcePhase.CleanupStarted, new { PriorFailureType = primary?.GetType().Name });
        if (application is not null) {
            try {
                await application.DisposeAsync();
                await RecordCleanupAsync(CapacityResourcePhase.ProviderDisposed, new { Root = application.RootPath });
            } catch (Exception cleanup) {
                Add(cleanup);
                await RecordCleanupAsync(CapacityResourcePhase.ProviderCleanupFailed, new { FailureType = cleanup.GetType().Name });
            }
        }
        if (environment is not null) {
            try {
                RequireEnvironmentPath(environment);
                RequireNoReparseDescendants(environment.RootPath);
                await environment.DisposeAsync();
                RequireNestedPath(CellParent);
                var environmentParent = Path.Combine(CellParent, "environment");
                if (Directory.Exists(environmentParent)) {
                    Directory.Delete(environmentParent, recursive: false);
                }
                if (Directory.Exists(CellParent)) {
                    Directory.Delete(CellParent, recursive: false);
                }
                await RecordCleanupAsync(CapacityResourcePhase.EnvironmentDisposed, new { Root = environment.RootPath, CellParent });
            } catch (Exception cleanup) {
                Add(cleanup);
                await RecordCleanupAsync(CapacityResourcePhase.EnvironmentCleanupFailed, new { FailureType = cleanup.GetType().Name });
            }
        }
        var cleanupSucceeded = cleanupFailure is null && !allocationReconciliationRequired;
        await RecordCleanupAsync(cleanupSucceeded ? CapacityResourcePhase.CleanupComplete : CapacityResourcePhase.CleanupIncomplete,
            new {
                CleanupSucceeded = cleanupSucceeded, CleanupFailureType = cleanupFailure?.GetType().Name,
                WorkloadFailureType = primary?.GetType().Name, EnvironmentWasAllocated = environment is not null,
                AllocationReconciliationRequired = allocationReconciliationRequired
            });
        if (allocationReconciliationRequired) {
            Add(new InvalidOperationException("Partial capacity allocation requires parent reconciliation of the original ownership manifest."));
        }
        return primary is null ? cleanupFailure : cleanupFailure is null ? primary : new AggregateException(primary, cleanupFailure);

        void Add(Exception error) {
            cleanupFailure = cleanupFailure is null ? error : new AggregateException(cleanupFailure, error);
        }

        async Task RecordCleanupAsync<T>(CapacityResourcePhase phase, T detail) {
            try {
                await RecordAsync(phase, detail, CancellationToken.None);
            } catch (Exception recording) {
                Add(recording);
            }
        }
    }

    private void RequireEnvironmentPath(CanDoItAllTestEnvironment environment) {
        RequireNestedPath(environment.RootPath);
        var expectedParent = Path.Combine(CellParent, "environment");
        if (!string.Equals(Path.GetDirectoryName(environment.RootPath), expectedParent, PathComparison) ||
                !Guid.TryParseExact(Path.GetFileName(environment.RootPath), "N", out _)) {
            throw new InvalidDataException("The environment is outside its pre-recorded capacity cell parent.");
        }
    }

    private void RequireNestedPath(string path) {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(outputRoot + Path.DirectorySeparatorChar, PathComparison)) {
            throw new InvalidDataException("The capacity resource path escaped its explicit runner-owned root.");
        }
        RequireNoReparseAncestors(full);
    }

    private static void RequireNoReparseAncestors(string path) {
        for (DirectoryInfo? current = new(path); current is not null; current = current.Parent) {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidDataException("Capacity ownership paths cannot traverse reparse points.");
            }
        }
    }

    private static void RequireNoReparseDescendants(string root) {
        if (!Directory.Exists(root)) {
            return;
        }
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory)) {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory)) {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) {
                    throw new InvalidDataException("Capacity cleanup refuses an environment containing a reparse point.");
                }
                if ((attributes & FileAttributes.Directory) != 0) {
                    pending.Push(path);
                }
            }
        }
    }

    private static bool IsLowerHex(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f';
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
