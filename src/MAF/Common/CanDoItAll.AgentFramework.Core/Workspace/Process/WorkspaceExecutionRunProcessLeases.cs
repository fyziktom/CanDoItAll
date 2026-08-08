using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceExecutionRunProcessLeaseCleaner
{
    Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId);
}

public interface IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory
{
    IWorkspaceExecutionRunProcessLeaseCleanupScope Create(WorkspaceExecutionScope scope);
}

public interface IWorkspaceExecutionRunProcessLeaseCleanupScope : IAsyncDisposable
{
    WorkspaceExecutionScope Scope { get; }

    Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId);
}

internal interface IWorkspaceExecutionRunProcessLeaseCleanupExecutor
{
    Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId);
}

public sealed record WorkspaceExecutionRunProcessCleanupResult(
    Guid ExecutionRunId,
    IReadOnlyList<string> CleanedStartupReceiptPaths,
    IReadOnlyList<WorkspaceExecutionRunProcessCleanupFailure> Failures)
{
    public static WorkspaceExecutionRunProcessCleanupResult Empty(Guid executionRunId)
        => new(executionRunId, [], []);
}

public sealed record WorkspaceExecutionRunProcessCleanupFailure(
    string StartupReceiptPath,
    string Message);

public sealed class WorkspaceExecutionRunProcessLeaseCleaner
    : IWorkspaceExecutionRunProcessLeaseCleaner
{
    private readonly ISandboxWorkspaceExecutionRunStore executionRunStore;
    private readonly WorkspaceExecutionScope configuredScope;
    private readonly IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory cleanupScopeFactory;

    public WorkspaceExecutionRunProcessLeaseCleaner(
        ISandboxWorkspaceExecutionRunStore executionRunStore,
        WorkspaceExecutionScope configuredScope,
        IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory cleanupScopeFactory)
    {
        this.executionRunStore = executionRunStore
            ?? throw new ArgumentNullException(nameof(executionRunStore));
        this.configuredScope = configuredScope
            ?? throw new ArgumentNullException(nameof(configuredScope));
        this.cleanupScopeFactory = cleanupScopeFactory
            ?? throw new ArgumentNullException(nameof(cleanupScopeFactory));
    }

    public async Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(
        Guid executionRunId)
    {
        if (executionRunId == Guid.Empty)
        {
            return Failure(
                executionRunId,
                "ExecutionRun workspace process cleanup requires a non-empty execution run identifier.");
        }

        ExecutionRunRecord? persistedRun;
        try
        {
            persistedRun = await executionRunStore
                .GetExecutionRunAsync(executionRunId, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return Failure(
                executionRunId,
                "Workspace process cleanup could not verify the persisted execution run. No lease cleanup was attempted.");
        }

        if (persistedRun is null)
        {
            return Failure(
                executionRunId,
                $"Execution run '{executionRunId:N}' does not exist; workspace process cleanup was not authorized.");
        }

        if (persistedRun.State is not ExecutionState.Completed
            and not ExecutionState.Failed)
        {
            return Failure(
                executionRunId,
                $"Execution run '{executionRunId:N}' is '{persistedRun.State}', not a persisted terminal state; workspace process cleanup was not authorized.");
        }

        var scopeResolution = ResolveEffectiveScope(persistedRun);
        if (!scopeResolution.Succeeded)
        {
            return Failure(executionRunId, scopeResolution.FailureMessage);
        }

        try
        {
            await using var cleanupScope = cleanupScopeFactory.Create(
                scopeResolution.Scope
                ?? throw new InvalidOperationException(
                    "Successful workspace process cleanup scope resolution produced no scope."));
            if (!cleanupScope.Scope.SharesIdentityWith(scopeResolution.Scope))
            {
                return Failure(
                    executionRunId,
                    "The workspace process cleanup factory returned services for a different workspace scope. Durable leases were retained for a later cleanup attempt.");
            }

            return await cleanupScope
                .CleanupAsync(executionRunId)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return Failure(
                executionRunId,
                "Authorized workspace process cleanup failed unexpectedly. Durable leases were retained for a later cleanup attempt.");
        }
    }

    private CleanupScopeResolution ResolveEffectiveScope(ExecutionRunRecord run)
    {
        var recordedScope = ExecutionInvocationMetadata
            .ResolveRecordedContextWorkspaceScopeForReporting(run);
        if (recordedScope.IsPresent && !recordedScope.IsValid)
        {
            return CleanupScopeResolution.Failed(
                $"Execution run '{run.Id:N}' carries malformed workspace scope metadata. Durable leases were retained for a later cleanup attempt.");
        }

        var governanceRead = AgentTurnContextMetadata
            .ReadExecutionGovernanceSnapshot(run.MetadataJson);
        if (governanceRead.State == AgentExecutionGovernanceReadState.Malformed)
        {
            return CleanupScopeResolution.Failed(
                $"Execution run '{run.Id:N}' carries a malformed authority projection. Durable leases were retained for a later cleanup attempt.");
        }

        var governance = governanceRead.Snapshot;
        if (governance is not null)
        {
            if (governance.AgentId != run.AgentId)
            {
                return CleanupScopeResolution.Failed(
                    $"Execution run '{run.Id:N}' carries an authority projection for a different agent. Durable leases were retained for a later cleanup attempt.");
            }

            if (configuredScope.DatabaseProfileId is { } databaseProfileId &&
                governance.DatabaseProfileId != databaseProfileId)
            {
                return CleanupScopeResolution.Failed(
                    $"Execution run '{run.Id:N}' carries an authority projection for a different database profile. Durable leases were retained for a later cleanup attempt.");
            }

            if (configuredScope.DatabaseProfileGeneration is { } databaseProfileGeneration &&
                governance.DatabaseProfileGeneration != databaseProfileGeneration)
            {
                return CleanupScopeResolution.Failed(
                    $"Execution run '{run.Id:N}' carries an authority projection from a different database profile generation. Durable leases were retained for a later cleanup attempt.");
            }

            if (recordedScope.Scope is { } metadataScope &&
                metadataScope != governance.WorkspaceScope)
            {
                return CleanupScopeResolution.Failed(
                    $"Execution run '{run.Id:N}' carries conflicting workspace scope metadata and governance. Durable leases were retained for a later cleanup attempt.");
            }

            return CleanupScopeResolution.Resolved(
                WorkspaceExecutionScope.ForRun(
                    configuredScope.WorkspaceRoot,
                    governance.WorkspaceScope,
                    governance,
                    run.Id));
        }

        var trustedMetadataScope = ExecutionInvocationMetadata
            .ResolveContextWorkspaceScope(run);
        return CleanupScopeResolution.Resolved(
            new WorkspaceExecutionScope(
                configuredScope.WorkspaceRoot,
                trustedMetadataScope ?? configuredScope.Scope,
                configuredScope.DatabaseProfileId,
                configuredScope.DatabaseProfileGeneration,
                executionRunId: run.Id));
    }

    private static WorkspaceExecutionRunProcessCleanupResult Failure(
        Guid executionRunId,
        string message)
        => new(
            executionRunId,
            [],
            [new WorkspaceExecutionRunProcessCleanupFailure(string.Empty, message)]);

    private sealed record CleanupScopeResolution(
        bool Succeeded,
        WorkspaceExecutionScope? Scope,
        string FailureMessage)
    {
        public static CleanupScopeResolution Resolved(WorkspaceExecutionScope scope)
            => new(true, scope, string.Empty);

        public static CleanupScopeResolution Failed(string message)
            => new(false, null, message);
    }
}

public sealed class WorkspaceExecutionRunProcessLeaseCleanupScope(
    WorkspaceExecutionScope scope,
    IWorkspaceCommandExecutionService commandExecutionService,
    IWorkspaceProcessHost processHost)
    : IWorkspaceExecutionRunProcessLeaseCleanupScope
{
    private readonly IWorkspaceExecutionRunProcessLeaseCleanupExecutor cleanupExecutor =
        commandExecutionService as IWorkspaceExecutionRunProcessLeaseCleanupExecutor
        ?? throw new WorkspaceRuntimeCompositionException(
            "The scope-bound command service does not provide process lease cleanup.");
    private bool disposed;

    public WorkspaceExecutionScope Scope { get; } =
        scope ?? throw new ArgumentNullException(nameof(scope));

    public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(
        Guid executionRunId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return cleanupExecutor.CleanupAsync(executionRunId);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        List<Exception>? failures = null;
        foreach (var ownedService in new object[]
                 {
                     commandExecutionService,
                     processHost
                 }.Distinct(ReferenceEqualityComparer.Instance))
        {
            try
            {
                switch (ownedService)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new AggregateException(
                "One or more scope-bound process lease cleanup services failed to dispose.",
                failures);
        }
    }
}

internal enum WorkspaceExecutionRunProcessLeasePhase
{
    Active = 0,
    Pending = 1
}

internal sealed record WorkspaceExecutionRunProcessLease(
    Guid ExecutionRunId,
    string StartupReceiptPath,
    WorkspaceExecutionRunProcessLeasePhase Phase,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset StartupReceiptDeadlineUtc,
    DateTimeOffset? ActivatedAtUtc);

internal sealed record WorkspaceExecutionRunProcessLeaseLoadResult(
    IReadOnlyList<WorkspaceExecutionRunProcessLease> Leases,
    IReadOnlyList<WorkspaceExecutionRunProcessCleanupFailure> Failures);

internal sealed class WorkspaceExecutionRunProcessLeaseStore
{
    internal const string StartupReceiptFileName = "startup.json";
    private const string LeaseDirectoryName = "process-leases";
    private const string CleanupClaimSuffix = ".cleanup.claim";
    private static readonly TimeSpan CleanupClaimWaitTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CleanupClaimRetryDelay = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan PendingStartupReceiptMaximumWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PendingStartupReceiptRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;

    public WorkspaceExecutionRunProcessLeaseStore(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.workspaceScope = workspaceScope;
    }

    public void Register(Guid executionRunId, string startupReceiptPath)
        => Register(
            executionRunId,
            startupReceiptPath,
            WorkspaceExecutionRunProcessLeasePhase.Active,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    public void RegisterPending(
        Guid executionRunId,
        string startupReceiptPath,
        DateTimeOffset registeredAtUtc,
        DateTimeOffset startupReceiptDeadlineUtc)
    {
        if (startupReceiptDeadlineUtc < registeredAtUtc)
        {
            throw new InvalidOperationException(
                "A pending ExecutionRun process lease startup receipt deadline cannot precede its registration time.");
        }

        Register(
            executionRunId,
            startupReceiptPath,
            WorkspaceExecutionRunProcessLeasePhase.Pending,
            registeredAtUtc,
            startupReceiptDeadlineUtc,
            activatedAtUtc: null);
    }

    public void Activate(
        Guid executionRunId,
        string startupReceiptPath,
        DateTimeOffset activatedAtUtc)
    {
        var normalizedPath = NormalizeStartupReceiptPath(startupReceiptPath);
        var leaseFile = GetLeaseFilePath(executionRunId, normalizedPath);
        var lease = ReadOwnedLease(
            leaseFile,
            executionRunId,
            normalizedPath);
        if (lease.Phase == WorkspaceExecutionRunProcessLeasePhase.Active)
        {
            return;
        }

        ReplaceLeaseAtomically(
            leaseFile,
            lease with
            {
                Phase = WorkspaceExecutionRunProcessLeasePhase.Active,
                ActivatedAtUtc = activatedAtUtc
            });
    }

    private void Register(
        Guid executionRunId,
        string startupReceiptPath,
        WorkspaceExecutionRunProcessLeasePhase phase,
        DateTimeOffset registeredAtUtc,
        DateTimeOffset startupReceiptDeadlineUtc,
        DateTimeOffset? activatedAtUtc)
    {
        if (executionRunId == Guid.Empty)
        {
            throw new InvalidOperationException("An ExecutionRun workspace process lease requires a non-empty execution run identifier.");
        }

        var normalizedPath = NormalizeStartupReceiptPath(startupReceiptPath);
        var lease = new WorkspaceExecutionRunProcessLease(
            executionRunId,
            normalizedPath,
            phase,
            registeredAtUtc,
            startupReceiptDeadlineUtc,
            activatedAtUtc);
        var leaseDirectory = GetLeaseDirectory(executionRunId);
        Directory.CreateDirectory(leaseDirectory);
        WriteLeaseAtomically(
            Path.Combine(leaseDirectory, BuildLeaseFileName(normalizedPath)),
            lease);
    }

    public WorkspaceExecutionRunProcessLeaseLoadResult Load(Guid executionRunId)
    {
        if (executionRunId == Guid.Empty)
        {
            return new WorkspaceExecutionRunProcessLeaseLoadResult(
                [],
                [new WorkspaceExecutionRunProcessCleanupFailure(
                    string.Empty,
                    "ExecutionRun workspace process cleanup requires a non-empty execution run identifier.")]);
        }

        var leaseDirectory = GetLeaseDirectory(executionRunId);
        try
        {
            var attributes = File.GetAttributes(leaseDirectory);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                return new WorkspaceExecutionRunProcessLeaseLoadResult(
                    [],
                    [new WorkspaceExecutionRunProcessCleanupFailure(
                        string.Empty,
                        "The durable workspace process lease location is invalid. No lease cleanup was attempted.")]);
            }
        }
        catch (DirectoryNotFoundException)
        {
            return new WorkspaceExecutionRunProcessLeaseLoadResult([], []);
        }
        catch (FileNotFoundException)
        {
            return new WorkspaceExecutionRunProcessLeaseLoadResult([], []);
        }
        catch (Exception)
        {
            return new WorkspaceExecutionRunProcessLeaseLoadResult(
                [],
                [new WorkspaceExecutionRunProcessCleanupFailure(
                    string.Empty,
                    "Durable workspace process leases could not be accessed. They were retained for a later cleanup attempt.")]);
        }

        var leases = new List<WorkspaceExecutionRunProcessLease>();
        var failures = new List<WorkspaceExecutionRunProcessCleanupFailure>();
        string[] leaseFiles;
        try
        {
            leaseFiles = Directory.GetFiles(leaseDirectory, "*.json")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception)
        {
            return new WorkspaceExecutionRunProcessLeaseLoadResult(
                [],
                [new WorkspaceExecutionRunProcessCleanupFailure(
                    string.Empty,
                    "Durable workspace process leases could not be enumerated. They were retained for a later cleanup attempt.")]);
        }

        foreach (var leaseFile in leaseFiles)
        {
            try
            {
                var lease = JsonSerializer.Deserialize<WorkspaceExecutionRunProcessLease>(
                    File.ReadAllText(leaseFile),
                    SerializerOptions)
                    ?? throw new InvalidOperationException("The durable lease record was empty.");
                if (lease.ExecutionRunId != executionRunId)
                {
                    throw new InvalidOperationException(
                        $"The durable lease belongs to execution run '{lease.ExecutionRunId:N}', not '{executionRunId:N}'.");
                }

                leases.Add(lease with
                {
                    StartupReceiptPath = NormalizeStartupReceiptPath(lease.StartupReceiptPath)
                });
                var normalizedLease = leases[^1];
                var expectedFileName = BuildLeaseFileName(normalizedLease.StartupReceiptPath);
                if (!string.Equals(
                    Path.GetFileName(leaseFile),
                    expectedFileName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    leases.RemoveAt(leases.Count - 1);
                    failures.Add(new WorkspaceExecutionRunProcessCleanupFailure(
                        normalizedLease.StartupReceiptPath,
                        "Durable workspace process lease filename does not match its normalized startup receipt identity."));
                }
            }
            catch (Exception)
            {
                failures.Add(new WorkspaceExecutionRunProcessCleanupFailure(
                    Path.GetFileName(leaseFile),
                    "The durable workspace process lease could not be read. It was retained for operator inspection and a later cleanup attempt."));
            }
        }

        return new WorkspaceExecutionRunProcessLeaseLoadResult(
            leases
                .GroupBy(lease => lease.StartupReceiptPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray(),
            failures);
    }

    public void Remove(Guid executionRunId, string startupReceiptPath)
    {
        if (executionRunId == Guid.Empty)
        {
            throw new InvalidOperationException("Removing an ExecutionRun workspace process lease requires a non-empty execution run identifier.");
        }

        var normalizedPath = NormalizeStartupReceiptPath(startupReceiptPath);
        var leaseFile = GetLeaseFilePath(executionRunId, normalizedPath);
        if (!LeaseFileExists(leaseFile))
        {
            return;
        }

        _ = ReadOwnedLease(
            leaseFile,
            executionRunId,
            normalizedPath);

        File.Delete(leaseFile);
    }

    public static void ValidateAuditIdentity(
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState auditScope)
    {
        ArgumentNullException.ThrowIfNull(auditScope);
        if (auditScope.ExecutionRunId == Guid.Empty)
        {
            throw new InvalidOperationException("Workspace process lease ownership requires a non-empty execution run identifier.");
        }
    }

    public string GetLeaseFilePath(
        Guid executionRunId,
        string startupReceiptPath)
        => Path.Combine(
            GetLeaseDirectory(executionRunId),
            BuildLeaseFileName(NormalizeStartupReceiptPath(startupReceiptPath)));

    public async Task<WorkspaceExecutionRunProcessLeaseCleanupClaim?> AcquireCleanupClaimAsync(
        Guid executionRunId,
        string startupReceiptPath)
    {
        var leaseFile = GetLeaseFilePath(executionRunId, startupReceiptPath);
        var claimFile = $"{leaseFile}{CleanupClaimSuffix}";
        var waitStarted = Stopwatch.StartNew();
        while (true)
        {
            if (!LeaseFileExists(leaseFile))
            {
                return null;
            }

            try
            {
                var stream = new FileStream(
                    claimFile,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 256,
                    FileOptions.WriteThrough);
                if (!LeaseFileExists(leaseFile))
                {
                    stream.Dispose();
                    return null;
                }

                stream.SetLength(0);
                using (var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 256,
                    leaveOpen: true))
                {
                    writer.Write(
                        $"processId={Environment.ProcessId};acquiredAtUtc={DateTimeOffset.UtcNow:O};executionRunId={executionRunId:N}");
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                return new WorkspaceExecutionRunProcessLeaseCleanupClaim(stream);
            }
            catch (IOException) when (waitStarted.Elapsed < CleanupClaimWaitTimeout)
            {
                await Task.Delay(CleanupClaimRetryDelay)
                    .ConfigureAwait(false);
            }
        }
    }

    public bool HasLease(
        Guid executionRunId,
        string startupReceiptPath)
        => LeaseFileExists(GetLeaseFilePath(executionRunId, startupReceiptPath));

    public async Task<bool> WaitForPendingStartupReceiptAsync(
        WorkspaceExecutionRunProcessLease lease)
    {
        if (lease.Phase != WorkspaceExecutionRunProcessLeasePhase.Pending)
        {
            return true;
        }

        var fullPath = ResolveStartupReceiptFullPath(lease.StartupReceiptPath);
        var waitDeadlineUtc = Min(
            lease.StartupReceiptDeadlineUtc,
            DateTimeOffset.UtcNow.Add(PendingStartupReceiptMaximumWait));
        while (true)
        {
            if (PathFileExists(fullPath))
            {
                return true;
            }

            var remaining = waitDeadlineUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            await Task.Delay(
                    remaining < PendingStartupReceiptRetryDelay
                        ? remaining
                        : PendingStartupReceiptRetryDelay)
                .ConfigureAwait(false);
        }
    }

    public string NormalizeStartupReceiptPath(string startupReceiptPath)
    {
        if (string.IsNullOrWhiteSpace(startupReceiptPath))
        {
            throw new InvalidOperationException("Workspace process identity requires a non-empty startup.json receipt path.");
        }

        var candidate = startupReceiptPath.Trim().Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, candidate));
        var relativePath = Path.GetRelativePath(workspaceRoot, fullPath);
        if (Path.IsPathRooted(relativePath) ||
            string.Equals(relativePath, "..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Workspace process startup receipt '{startupReceiptPath}' is outside the workspace root.");
        }

        if (!string.Equals(
            Path.GetFileName(fullPath),
            StartupReceiptFileName,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workspace process identity requires a {StartupReceiptFileName} receipt. Received '{startupReceiptPath}'.");
        }

        return WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
    }

    public string ResolveSingleStartupReceiptPath(
        IReadOnlyList<string> targetPaths,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(targetPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        var startupReceiptPaths = targetPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Where(path => string.Equals(
                Path.GetFileName(path.Replace('/', Path.DirectorySeparatorChar)),
                StartupReceiptFileName,
                StringComparison.OrdinalIgnoreCase))
            .Select(NormalizeStartupReceiptPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return startupReceiptPaths.Length switch
        {
            1 => startupReceiptPaths[0],
            0 => throw new InvalidOperationException(
                $"{operation} did not prove a {StartupReceiptFileName} path in its receipt target paths."),
            _ => throw new InvalidOperationException(
                $"{operation} proved multiple {StartupReceiptFileName} paths in its receipt target paths: {string.Join(", ", startupReceiptPaths)}.")
        };
    }

    private string GetLeaseDirectory(Guid executionRunId)
        => Path.Combine(
            WorkspaceExecutionAuditTrailWriter.GetRunAuditRoot(
                workspaceRoot,
                workspaceScope,
                executionRunId),
            LeaseDirectoryName);

    private static string BuildLeaseFileName(string normalizedStartupReceiptPath)
    {
        var identity = normalizedStartupReceiptPath.ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return $"{hash}.json";
    }

    private WorkspaceExecutionRunProcessLease ReadOwnedLease(
        string leaseFile,
        Guid executionRunId,
        string normalizedStartupReceiptPath)
    {
        if (!LeaseFileExists(leaseFile))
        {
            throw new InvalidOperationException(
                $"Durable workspace process lease '{leaseFile}' does not exist.");
        }

        var lease = JsonSerializer.Deserialize<WorkspaceExecutionRunProcessLease>(
            File.ReadAllText(leaseFile),
            SerializerOptions)
            ?? throw new InvalidOperationException(
                $"Durable workspace process lease '{leaseFile}' was empty.");
        if (lease.ExecutionRunId != executionRunId)
        {
            throw new InvalidOperationException(
                $"Durable workspace process lease belongs to execution run '{lease.ExecutionRunId:N}', not '{executionRunId:N}'.");
        }

        var registeredPath = NormalizeStartupReceiptPath(lease.StartupReceiptPath);
        if (!string.Equals(
            registeredPath,
            normalizedStartupReceiptPath,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Durable workspace process lease identity mismatch for '{normalizedStartupReceiptPath}'.");
        }

        return lease with
        {
            StartupReceiptPath = registeredPath
        };
    }

    private string ResolveStartupReceiptFullPath(string startupReceiptPath)
    {
        var normalizedPath = NormalizeStartupReceiptPath(startupReceiptPath);
        return Path.GetFullPath(Path.Combine(
            workspaceRoot,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static DateTimeOffset Min(
        DateTimeOffset left,
        DateTimeOffset right)
        => left <= right ? left : right;

    private static bool LeaseFileExists(string leaseFile)
    {
        try
        {
            if ((File.GetAttributes(leaseFile) & FileAttributes.Directory) != 0)
            {
                throw new InvalidOperationException(
                    $"Durable workspace process lease identity '{leaseFile}' is a directory, not a lease record.");
            }

            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static bool PathFileExists(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
            {
                throw new InvalidOperationException(
                    $"Workspace process startup receipt '{path}' is a directory, not a file.");
            }

            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static void WriteLeaseAtomically(
        string leaseFile,
        WorkspaceExecutionRunProcessLease lease)
    {
        if (File.Exists(leaseFile))
        {
            var existing = JsonSerializer.Deserialize<WorkspaceExecutionRunProcessLease>(
                File.ReadAllText(leaseFile),
                SerializerOptions)
                ?? throw new InvalidOperationException($"Durable workspace process lease '{leaseFile}' was empty.");
            if (existing.ExecutionRunId != lease.ExecutionRunId ||
                !string.Equals(
                    existing.StartupReceiptPath,
                    lease.StartupReceiptPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Durable workspace process lease '{leaseFile}' contains a conflicting identity.");
            }

            return;
        }

        var tempFile = $"{leaseFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            var payload = JsonSerializer.Serialize(lease, SerializerOptions);
            using (var stream = new FileStream(
                tempFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(payload);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(tempFile, leaseFile);
            }
            catch (IOException) when (File.Exists(leaseFile))
            {
                var existing = JsonSerializer.Deserialize<WorkspaceExecutionRunProcessLease>(
                    File.ReadAllText(leaseFile),
                    SerializerOptions)
                    ?? throw new InvalidOperationException($"Durable workspace process lease '{leaseFile}' was empty.");
                if (existing.ExecutionRunId != lease.ExecutionRunId ||
                    !string.Equals(
                        existing.StartupReceiptPath,
                        lease.StartupReceiptPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Durable workspace process lease '{leaseFile}' contains a conflicting identity.");
                }
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void ReplaceLeaseAtomically(
        string leaseFile,
        WorkspaceExecutionRunProcessLease lease)
    {
        var tempFile = $"{leaseFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            WriteLeasePayloadDurably(tempFile, lease);
            File.Move(tempFile, leaseFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void WriteLeasePayloadDurably(
        string path,
        WorkspaceExecutionRunProcessLease lease)
    {
        var payload = JsonSerializer.Serialize(lease, SerializerOptions);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(payload);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}

internal sealed class WorkspaceExecutionRunProcessLeaseCleanupClaim(
    FileStream stream) : IDisposable
{
    public void Dispose()
    {
        stream.Dispose();
    }
}

internal sealed record WorkspaceExecutionRunProcessLeaseCleanupAttempt(
    bool Succeeded,
    string StartupReceiptPath,
    string Message);

internal static class WorkspaceExecutionRunProcessLeaseCleanupCoordinator
{
    private static readonly ConcurrentDictionary<string, SharedCleanupOperation> Operations =
        new(StringComparer.OrdinalIgnoreCase);

    public static CleanupOperationLease Acquire(
        string leaseIdentityPath,
        Func<Task<WorkspaceExecutionRunProcessLeaseCleanupAttempt>> cleanup)
    {
        while (true)
        {
            var operation = Operations.GetOrAdd(
                leaseIdentityPath,
                _ => new SharedCleanupOperation(cleanup));
            if (operation.TryAcquire())
            {
                return new CleanupOperationLease(
                    leaseIdentityPath,
                    operation,
                    Release);
            }

            Operations.TryRemove(
                new KeyValuePair<string, SharedCleanupOperation>(
                    leaseIdentityPath,
                    operation));
        }
    }

    private static void Release(
        string leaseIdentityPath,
        SharedCleanupOperation operation)
    {
        if (!operation.Release())
        {
            return;
        }

        Operations.TryRemove(
            new KeyValuePair<string, SharedCleanupOperation>(
                leaseIdentityPath,
                operation));
    }

    internal sealed class CleanupOperationLease : IDisposable
    {
        private readonly string leaseIdentityPath;
        private readonly SharedCleanupOperation operation;
        private readonly Action<string, SharedCleanupOperation> release;
        private bool disposed;

        public CleanupOperationLease(
            string leaseIdentityPath,
            SharedCleanupOperation operation,
            Action<string, SharedCleanupOperation> release)
        {
            this.leaseIdentityPath = leaseIdentityPath;
            this.operation = operation;
            this.release = release;
        }

        public Task<WorkspaceExecutionRunProcessLeaseCleanupAttempt> Task => operation.Task;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            release(leaseIdentityPath, operation);
        }
    }

    internal sealed class SharedCleanupOperation
    {
        private readonly object synchronization = new();
        private readonly Lazy<Task<WorkspaceExecutionRunProcessLeaseCleanupAttempt>> task;
        private int referenceCount;
        private bool closed;

        public SharedCleanupOperation(
            Func<Task<WorkspaceExecutionRunProcessLeaseCleanupAttempt>> cleanup)
        {
            task = new Lazy<Task<WorkspaceExecutionRunProcessLeaseCleanupAttempt>>(
                cleanup,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Task<WorkspaceExecutionRunProcessLeaseCleanupAttempt> Task => task.Value;

        public bool TryAcquire()
        {
            lock (synchronization)
            {
                if (closed)
                {
                    return false;
                }

                referenceCount++;
                return true;
            }
        }

        public bool Release()
        {
            lock (synchronization)
            {
                referenceCount--;
                if (referenceCount > 0)
                {
                    return false;
                }

                closed = true;
                return true;
            }
        }
    }
}
