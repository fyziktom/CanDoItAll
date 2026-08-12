using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Manager;

public enum ManagerHostKind
{
    Windows,
    Linux,
    MacOs
}

public enum ManagerProcessPurpose
{
    DotnetWatch,
    TailwindBuild,
    TailwindDependencyInstall,
    Tuning
}

public enum ManagerProcessLifecycleState
{
    Running,
    Exited,
    Terminated,
    OwnershipUnverified,
    TerminationFailed
}

public enum ManagerProcessDiscoveryStatus
{
    Available,
    Exited,
    PermissionDenied,
    Incomplete,
    Unsupported,
    Failed
}

public sealed record ManagerProcessEvidence(
    int ProcessId,
    string StartIdentity,
    string ExecutablePath,
    string ObservedCommandFingerprint,
    string OwnerIdentity,
    int ParentProcessId);

public sealed record ManagerProcessDiscoveryResult(
    ManagerProcessDiscoveryStatus Status,
    ManagerProcessEvidence? Evidence,
    string DiagnosticCode)
{
    public static ManagerProcessDiscoveryResult Available(ManagerProcessEvidence evidence)
        => new(ManagerProcessDiscoveryStatus.Available, evidence, "available");

    public static ManagerProcessDiscoveryResult Unavailable(
        ManagerProcessDiscoveryStatus status,
        string diagnosticCode)
        => new(status, null, diagnosticCode);
}

public interface IManagerProcessDiscovery
{
    Task<ManagerProcessDiscoveryResult> ProbeAsync(int processId, CancellationToken cancellationToken = default);
}

public sealed record ManagerOwnedProcessRecord(
    Guid LeaseId,
    ManagerProcessPurpose Purpose,
    WorkspaceOwnedProcessIdentity HostIdentity,
    string RecoveryStartIdentity,
    string ExecutablePath,
    string PlannedArgumentsFingerprint,
    string ObservedCommandFingerprint,
    string WorkspaceRoot,
    string OwnerIdentity,
    int ParentProcessId,
    string LeaseOwner,
    ManagerProcessLifecycleState State,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? DiagnosticCode = null);

public interface IManagerOwnedProcessRegistry
{
    Task<IReadOnlyList<ManagerOwnedProcessRecord>> ReadAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(ManagerOwnedProcessRecord record, CancellationToken cancellationToken = default);
}

public sealed record ManagerProcessLaunchRequest(
    ManagerProcessPurpose Purpose,
    string ToolName,
    string RecipeId,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    string WorkspaceRoot,
    string LeaseOwner,
    int StdoutLimitCharacters = 262_144,
    int StderrLimitCharacters = 262_144);

public interface IManagerProcessLease : IAsyncDisposable
{
    ManagerOwnedProcessRecord Record { get; }

    bool HasExited { get; }

    WorkspaceProcessOutputSnapshot CaptureOutput();

    Task<WorkspaceProcessExecutionResult> WaitForExitAsync(CancellationToken cancellationToken = default);

    Task<WorkspaceProcessTerminationResult> TerminateAsync(
        string diagnosticCode,
        CancellationToken cancellationToken = default);
}

public interface IManagerProcessCoordinator
{
    Task<IManagerProcessLease> StartAsync(
        ManagerProcessLaunchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceProcessTerminationResult>> ReclaimRegisteredAsync(
        ManagerProcessPurpose purpose,
        string diagnosticCode,
        CancellationToken cancellationToken = default);
}

public static class ManagerProcessFingerprint
{
    public static string ComputeArguments(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, executablePath);
        foreach (var argument in arguments)
        {
            Append(hash, argument);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static string ComputeObservedCommand(ReadOnlySpan<byte> commandBytes)
        => Convert.ToHexString(SHA256.HashData(commandBytes)).ToLowerInvariant();

    public static string ComputeObservedCommand(string commandLine)
        => ComputeObservedCommand(Encoding.UTF8.GetBytes(commandLine));

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}

public sealed class FileManagerOwnedProcessRegistry : IManagerOwnedProcessRegistry
{
    private const int SchemaVersion = 1;
    private const int MaximumRegistryBytes = 4 * 1024 * 1024;
    private const int MaximumRecordCount = 10_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly DurableFileWriter durableFileWriter;
    private readonly string registryRoot;
    private readonly string registryPath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<Guid, ManagerOwnedProcessRecord>? records;

    public FileManagerOwnedProcessRegistry(
        IConfiguration configuration,
        DurableFileWriter durableFileWriter)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.durableFileWriter = durableFileWriter ?? throw new ArgumentNullException(nameof(durableFileWriter));
        var options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new ManagerOptions();
        var workspaceRoot = ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, options);
        registryRoot = Path.GetFullPath(options.ArtifactsRoot, workspaceRoot);
        registryPath = Path.Combine(registryRoot, "manager-process-registry.json");
    }

    internal FileManagerOwnedProcessRegistry(
        string registryRoot,
        DurableFileWriter durableFileWriter)
    {
        this.durableFileWriter = durableFileWriter ?? throw new ArgumentNullException(nameof(durableFileWriter));
        this.registryRoot = Path.GetFullPath(registryRoot);
        registryPath = Path.Combine(this.registryRoot, "manager-process-registry.json");
    }

    public async Task<IReadOnlyList<ManagerOwnedProcessRecord>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return records!.Values
                .OrderBy(record => record.RegisteredAtUtc)
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UpsertAsync(
        ManagerOwnedProcessRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        Validate(record);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            records![record.LeaseId] = record;
            var document = new ManagerProcessRegistryDocument(SchemaVersion, records.Values.ToArray());
            var json = JsonSerializer.Serialize(document, JsonOptions);
            durableFileWriter.EnsureDirectory(registryRoot, registryRoot, requirePrivateUnixMode: true);
            await durableFileWriter.WriteTextAsync(
                registryRoot,
                registryPath,
                json,
                DurableFileWriteOptions.Private,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (records is not null)
        {
            return;
        }

        if (!File.Exists(registryPath))
        {
            records = [];
            return;
        }

        if (new FileInfo(registryPath).Length > MaximumRegistryBytes)
        {
            throw new InvalidOperationException("The Manager process registry exceeds its bounded size limit.");
        }

        ManagerProcessRegistryDocument document;
        try
        {
            await using var stream = new FileStream(
                registryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            document = await JsonSerializer.DeserializeAsync<ManagerProcessRegistryDocument>(
                           stream,
                           JsonOptions,
                           cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidDataException("The Manager process registry is empty.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw new InvalidOperationException(
                "The Manager process registry is invalid and cannot be used for process authorization.",
                exception);
        }

        if (document.SchemaVersion != SchemaVersion)
        {
            throw new InvalidOperationException(
                $"The Manager process registry schema '{document.SchemaVersion}' is not supported.");
        }

        if (document.Records.Count > MaximumRecordCount)
        {
            throw new InvalidOperationException("The Manager process registry contains too many records.");
        }

        foreach (var record in document.Records)
        {
            Validate(record);
        }

        if (document.Records.Select(record => record.LeaseId).Distinct().Count() != document.Records.Count)
        {
            throw new InvalidOperationException("The Manager process registry contains duplicate lease identities.");
        }

        records = document.Records.ToDictionary(record => record.LeaseId);
    }

    private static void Validate(ManagerOwnedProcessRecord record)
    {
        if (record.LeaseId == Guid.Empty ||
            record.HostIdentity.ProcessId <= 0 ||
            string.IsNullOrWhiteSpace(record.RecoveryStartIdentity) ||
            string.IsNullOrWhiteSpace(record.ExecutablePath) ||
            string.IsNullOrWhiteSpace(record.WorkspaceRoot) ||
            string.IsNullOrWhiteSpace(record.OwnerIdentity) ||
            string.IsNullOrWhiteSpace(record.LeaseOwner) ||
            !IsSha256(record.HostIdentity.ExecutablePathFingerprint) ||
            !IsSha256(record.PlannedArgumentsFingerprint) ||
            !IsSha256(record.ObservedCommandFingerprint) ||
            !Enum.IsDefined(record.Purpose) ||
            !Enum.IsDefined(record.State))
        {
            throw new InvalidOperationException("The Manager process registry contains an incomplete ownership record.");
        }


        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(record.ExecutablePath, "Manager process executable path");
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(record.WorkspaceRoot, "Manager process workspace root");
            if (!Path.IsPathRooted(record.ExecutablePath) || !Path.IsPathRooted(record.WorkspaceRoot))
            {
                throw new InvalidOperationException("The Manager process registry contains a non-rooted physical path.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new InvalidOperationException("The Manager process registry contains an invalid physical path.", exception);
        }
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private sealed record ManagerProcessRegistryDocument(
        int SchemaVersion,
        IReadOnlyList<ManagerOwnedProcessRecord> Records);
}

public sealed class ManagerProcessCoordinator : IManagerProcessCoordinator
{
    private readonly IWorkspaceLongRunningProcessHost processHost;
    private readonly IManagerProcessDiscovery discovery;
    private readonly IManagerOwnedProcessRegistry registry;
    private readonly IPhysicalFileSystemPathPolicyFactory pathPolicyFactory;
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy;
    private readonly ManagerProcessOwnershipVerifier verifier;
    private readonly SemaphoreSlim launchGate = new(1, 1);

    public ManagerProcessCoordinator(
        IWorkspaceLongRunningProcessHost processHost,
        IManagerProcessDiscovery discovery,
        IManagerOwnedProcessRegistry registry,
        IPhysicalFileSystemPathPolicyFactory pathPolicyFactory)
    {
        this.processHost = processHost ?? throw new ArgumentNullException(nameof(processHost));
        this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.pathPolicyFactory = pathPolicyFactory ?? throw new ArgumentNullException(nameof(pathPolicyFactory));
        environmentPolicy = new WorkspaceCommandEnvironmentPolicy();
        verifier = new ManagerProcessOwnershipVerifier(
            ManagerHostKindExtensions.Current(),
            pathPolicyFactory);
    }

    internal ManagerProcessCoordinator(
        IWorkspaceLongRunningProcessHost processHost,
        IManagerProcessDiscovery discovery,
        IManagerOwnedProcessRegistry registry,
        IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
        ManagerHostKind hostKind)
        : this(processHost, discovery, registry, pathPolicyFactory)
    {
        verifier = new ManagerProcessOwnershipVerifier(hostKind, pathPolicyFactory);
    }

    public async Task<IManagerProcessLease> StartAsync(
        ManagerProcessLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateLaunch(request);
        await launchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureNoConflictingLeaseAsync(request, cancellationToken).ConfigureAwait(false);
            return await StartCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            launchGate.Release();
        }
    }

    private async Task<IManagerProcessLease> StartCoreAsync(
        ManagerProcessLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var plannedFingerprint = ManagerProcessFingerprint.ComputeArguments(
            request.ExecutablePath,
            request.Arguments);
        var environment = environmentPolicy.MergeEnvironmentVariables(
            request.EnvironmentVariables,
            request.ToolName);
        var session = await processHost.StartSessionAsync(
            new WorkspaceProcessSessionRequest(
                request.ToolName,
                request.RecipeId,
                request.ExecutablePath,
                request.Arguments,
                request.WorkingDirectory,
                environment,
                request.StdoutLimitCharacters,
                request.StderrLimitCharacters,
                StandardInput: null,
                WorkspaceProcessTerminationMode.GracefulThenForceTree),
            cancellationToken).ConfigureAwait(false);

        try
        {
            var discoveryResult = await ProbeStartedProcessAsync(
                session.Identity.ProcessId,
                cancellationToken).ConfigureAwait(false);
            if (discoveryResult is not { Status: ManagerProcessDiscoveryStatus.Available, Evidence: not null })
            {
                await session.TerminateAsync(
                    WorkspaceProcessTerminationReason.StartFailed,
                    "Manager could not establish complete ownership evidence for the launched process.",
                    CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Manager process launch was rejected because ownership evidence is '{discoveryResult.DiagnosticCode}'.");
            }

            var evidence = discoveryResult.Evidence;
            if (evidence.ParentProcessId != Environment.ProcessId)
            {
                await session.TerminateAsync(
                    WorkspaceProcessTerminationReason.StartFailed,
                    "Manager rejected a launched process whose parent identity did not match the Manager host.",
                    CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    "Manager process launch was rejected because its parent identity did not match the Manager host.");
            }

            var now = DateTimeOffset.UtcNow;
            var record = new ManagerOwnedProcessRecord(
                Guid.NewGuid(),
                request.Purpose,
                session.Identity,
                evidence.StartIdentity,
                evidence.ExecutablePath,
                plannedFingerprint,
                evidence.ObservedCommandFingerprint,
                Path.GetFullPath(request.WorkspaceRoot),
                evidence.OwnerIdentity,
                evidence.ParentProcessId,
                request.LeaseOwner,
                ManagerProcessLifecycleState.Running,
                now,
                now);
            await registry.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
            return new ManagerProcessLease(session, record, registry);
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task EnsureNoConflictingLeaseAsync(
        ManagerProcessLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var records = await registry.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var record in records.Where(record =>
                     (record.State is ManagerProcessLifecycleState.Running or
                         ManagerProcessLifecycleState.OwnershipUnverified or
                         ManagerProcessLifecycleState.TerminationFailed) &&
                     record.Purpose == request.Purpose &&
                     string.Equals(record.LeaseOwner, request.LeaseOwner, StringComparison.Ordinal)))
        {
            if (record.State != ManagerProcessLifecycleState.Running)
            {
                throw new InvalidOperationException(
                    "Manager refused a duplicate process launch because the previous lease requires explicit cleanup.");
            }

            var discovered = await discovery.ProbeAsync(
                record.HostIdentity.ProcessId,
                cancellationToken).ConfigureAwait(false);
            var ownership = verifier.Verify(record, discovered);
            if (ownership.Status == ManagerProcessOwnershipStatus.AlreadyExited)
            {
                await registry.UpsertAsync(
                    record with
                    {
                        State = ManagerProcessLifecycleState.Exited,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                        DiagnosticCode = "duplicate-check-process-exited"
                    },
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new InvalidOperationException(
                ownership.Status == ManagerProcessOwnershipStatus.Verified
                    ? "Manager refused a duplicate process launch because the existing owned lease is still running."
                    : "Manager refused a duplicate process launch because the previous lease could not be re-verified.");
        }
    }

    public async Task<IReadOnlyList<WorkspaceProcessTerminationResult>> ReclaimRegisteredAsync(
        ManagerProcessPurpose purpose,
        string diagnosticCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticCode);
        var records = await registry.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<WorkspaceProcessTerminationResult>();
        foreach (var record in records.Where(record =>
                     record.Purpose == purpose &&
                     record.State == ManagerProcessLifecycleState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var discovered = await discovery.ProbeAsync(
                record.HostIdentity.ProcessId,
                cancellationToken).ConfigureAwait(false);
            var ownership = verifier.Verify(record, discovered);
            if (ownership.Status == ManagerProcessOwnershipStatus.AlreadyExited)
            {
                await registry.UpsertAsync(
                    record with
                    {
                        State = ManagerProcessLifecycleState.Exited,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                        DiagnosticCode = diagnosticCode
                    },
                    cancellationToken).ConfigureAwait(false);
                results.Add(new WorkspaceProcessTerminationResult(
                    WorkspaceProcessTerminationStatus.AlreadyExited,
                    ResidualProcessPossible: false,
                    "The registered Manager process has already exited."));
                continue;
            }

            if (ownership.Status != ManagerProcessOwnershipStatus.Verified)
            {
                await registry.UpsertAsync(
                    record with
                    {
                        State = ManagerProcessLifecycleState.OwnershipUnverified,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                        DiagnosticCode = ownership.DiagnosticCode
                    },
                    cancellationToken).ConfigureAwait(false);
                results.Add(new WorkspaceProcessTerminationResult(
                    WorkspaceProcessTerminationStatus.IdentityMismatch,
                    ResidualProcessPossible: true,
                    "The registered Manager process could not be re-verified and was not terminated."));
                continue;
            }

            var termination = await processHost.TerminateOwnedProcessAsync(
                record.HostIdentity,
                cancellationToken).ConfigureAwait(false);
            await registry.UpsertAsync(
                record with
                {
                    State = termination.Status is WorkspaceProcessTerminationStatus.Terminated or
                        WorkspaceProcessTerminationStatus.AlreadyExited
                        ? ManagerProcessLifecycleState.Terminated
                        : ManagerProcessLifecycleState.TerminationFailed,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    DiagnosticCode = diagnosticCode
                },
                cancellationToken).ConfigureAwait(false);
            results.Add(termination);
        }

        return results;
    }

    private async Task<ManagerProcessDiscoveryResult> ProbeStartedProcessAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        ManagerProcessDiscoveryResult result = ManagerProcessDiscoveryResult.Unavailable(
            ManagerProcessDiscoveryStatus.Incomplete,
            "launch-evidence-incomplete");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = await discovery.ProbeAsync(processId, cancellationToken).ConfigureAwait(false);
            if (result.Status != ManagerProcessDiscoveryStatus.Incomplete)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void ValidateLaunch(ManagerProcessLaunchRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ToolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RecipeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseOwner);
        var policy = pathPolicyFactory.Create(request.WorkspaceRoot);
        if (!policy.IsWithinRoot(request.WorkingDirectory))
        {
            throw new InvalidOperationException("Manager process working directory must be inside its authorized workspace root.");
        }
    }
}

internal enum ManagerProcessOwnershipStatus
{
    Verified,
    AlreadyExited,
    Unverified
}

internal sealed record ManagerProcessOwnershipResult(
    ManagerProcessOwnershipStatus Status,
    string DiagnosticCode);

internal sealed class ManagerProcessOwnershipVerifier(
    ManagerHostKind hostKind,
    IPhysicalFileSystemPathPolicyFactory pathPolicyFactory)
{
    private readonly StringComparer ownerComparer = hostKind == ManagerHostKind.Windows
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public ManagerProcessOwnershipResult Verify(
        ManagerOwnedProcessRecord record,
        ManagerProcessDiscoveryResult discovery)
    {
        if (discovery.Status == ManagerProcessDiscoveryStatus.Exited)
        {
            return new ManagerProcessOwnershipResult(
                ManagerProcessOwnershipStatus.AlreadyExited,
                "process-exited");
        }

        if (discovery is not { Status: ManagerProcessDiscoveryStatus.Available, Evidence: not null })
        {
            return new ManagerProcessOwnershipResult(
                ManagerProcessOwnershipStatus.Unverified,
                discovery.DiagnosticCode);
        }

        var evidence = discovery.Evidence;
        if (evidence.ProcessId != record.HostIdentity.ProcessId)
        {
            return Unverified("pid-mismatch");
        }

        if (!string.Equals(evidence.StartIdentity, record.RecoveryStartIdentity, StringComparison.Ordinal))
        {
            return Unverified("start-identity-mismatch");
        }

        string recordedExecutablePath;
        string observedExecutablePath;
        StringComparer executableComparer;
        try
        {
            recordedExecutablePath = Path.GetFullPath(record.ExecutablePath);
            observedExecutablePath = Path.GetFullPath(evidence.ExecutablePath);
            var executableDirectory = Path.GetDirectoryName(recordedExecutablePath);
            if (string.IsNullOrWhiteSpace(executableDirectory))
            {
                return Unverified("executable-path-policy-unavailable");
            }

            executableComparer = pathPolicyFactory.Create(executableDirectory).PathComparer;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidOperationException or NotSupportedException)
        {
            return Unverified("executable-path-policy-unavailable");
        }

        if (!executableComparer.Equals(observedExecutablePath, recordedExecutablePath))
        {
            return Unverified("executable-mismatch");
        }

        if (!string.Equals(
                evidence.ObservedCommandFingerprint,
                record.ObservedCommandFingerprint,
                StringComparison.Ordinal))
        {
            return Unverified("command-mismatch");
        }

        if (!ownerComparer.Equals(evidence.OwnerIdentity, record.OwnerIdentity))
        {
            return Unverified("owner-mismatch");
        }

        return new ManagerProcessOwnershipResult(
            ManagerProcessOwnershipStatus.Verified,
            "verified");
    }

    private static ManagerProcessOwnershipResult Unverified(string diagnosticCode)
        => new(ManagerProcessOwnershipStatus.Unverified, diagnosticCode);
}

internal sealed class ManagerProcessLease(
    IWorkspaceProcessSession session,
    ManagerOwnedProcessRecord record,
    IManagerOwnedProcessRegistry registry) : IManagerProcessLease
{
    private readonly SemaphoreSlim completionGate = new(1, 1);
    private int completed;

    public ManagerOwnedProcessRecord Record { get; private set; } = record;

    public bool HasExited => session.HasExited;

    public WorkspaceProcessOutputSnapshot CaptureOutput() => session.CaptureOutput();

    public async Task<WorkspaceProcessExecutionResult> WaitForExitAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await session.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await CompleteAsync(
            ManagerProcessLifecycleState.Exited,
            "process-exited",
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<WorkspaceProcessTerminationResult> TerminateAsync(
        string diagnosticCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticCode);
        var execution = await session.TerminateAsync(
            WorkspaceProcessTerminationReason.CallerCanceled,
            "Manager terminated its owned process.",
            cancellationToken).ConfigureAwait(false);
        var status = execution.ResidualProcessPossible
            ? ManagerProcessLifecycleState.TerminationFailed
            : ManagerProcessLifecycleState.Terminated;
        await CompleteAsync(status, diagnosticCode, cancellationToken).ConfigureAwait(false);
        return new WorkspaceProcessTerminationResult(
            execution.ResidualProcessPossible
                ? WorkspaceProcessTerminationStatus.Failed
                : WorkspaceProcessTerminationStatus.Terminated,
            execution.ResidualProcessPossible,
            execution.ResidualProcessPossible
                ? "Manager could not confirm termination of its owned process."
                : "Manager terminated its owned process.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Interlocked.CompareExchange(ref completed, 0, 0) == 0)
            {
                await TerminateAsync("lease-disposed", CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task CompleteAsync(
        ManagerProcessLifecycleState state,
        string diagnosticCode,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref completed) != 0)
        {
            return;
        }

        await completionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref completed) != 0)
            {
                return;
            }

            var completedRecord = Record with
            {
                State = state,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                DiagnosticCode = diagnosticCode
            };
            await registry.UpsertAsync(completedRecord, cancellationToken).ConfigureAwait(false);
            Record = completedRecord;
            Volatile.Write(ref completed, 1);
        }
        finally
        {
            completionGate.Release();
        }
    }
}

internal static class ManagerHostKindExtensions
{
    public static ManagerHostKind Current()
        => OperatingSystem.IsWindows()
            ? ManagerHostKind.Windows
            : OperatingSystem.IsLinux()
                ? ManagerHostKind.Linux
                : OperatingSystem.IsMacOS()
                    ? ManagerHostKind.MacOs
                    : throw new PlatformNotSupportedException("Manager process ownership is supported only on Windows, Linux, and macOS.");
}
