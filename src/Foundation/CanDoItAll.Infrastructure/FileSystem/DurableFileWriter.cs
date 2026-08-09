using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure;

public enum DurableFileCommitMode
{
    ReplaceExisting,
    CreateNew
}

public sealed record DurableFileWriteOptions
{
    public static DurableFileWriteOptions Default { get; } = new();

    public static DurableFileWriteOptions CreateNew { get; } = new()
    {
        CommitMode = DurableFileCommitMode.CreateNew
    };

    public static DurableFileWriteOptions Private { get; } = new()
    {
        RequirePrivateUnixMode = true
    };

    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public bool CreateBackup { get; init; }

    public DurableFileCommitMode CommitMode { get; init; }

    public bool RequirePrivateUnixMode { get; init; }
}

internal enum DurableFileWriteStage
{
    TemporaryFileFlushed,
    BeforeCommit,
    Committed
}

public sealed class DurableFileWriter
{
    internal const string CleanupFailureDataKey = "CanDoItAll.DurableFileWriter.CleanupFailure";

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite;

    private readonly IPhysicalFileSystemPathPolicyFactory pathPolicyFactory;
    private readonly Action<DurableFileWriteStage>? stageObserver;

    public DurableFileWriter(IPhysicalFileSystemPathPolicyFactory pathPolicyFactory)
        : this(pathPolicyFactory, stageObserver: null)
    {
    }

    internal DurableFileWriter(
        IPhysicalFileSystemPathPolicyFactory pathPolicyFactory,
        Action<DurableFileWriteStage>? stageObserver)
    {
        this.pathPolicyFactory = pathPolicyFactory ?? throw new ArgumentNullException(nameof(pathPolicyFactory));
        this.stageObserver = stageObserver;
    }

    public void EnsureDirectory(
        string managedRoot,
        string directoryPath,
        bool requirePrivateUnixMode)
    {
        IPhysicalFileSystemPathPolicy policy = pathPolicyFactory.Create(managedRoot);
        string fullDirectory = policy.ResolveContainedPath(directoryPath);
        EnsureDirectoryTree(policy.RootPath, fullDirectory, requirePrivateUnixMode);
        pathPolicyFactory.Create(policy.RootPath).EnsureSafePath(fullDirectory);
    }

    public void HardenPrivateFile(string managedRoot, string filePath)
    {
        IPhysicalFileSystemPathPolicy policy = pathPolicyFactory.Create(managedRoot);
        string fullPath = policy.ResolveContainedPath(filePath);
        string parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidPath,
                "The private file does not have a directory parent.");
        EnsureDirectoryTree(policy.RootPath, parentPath, requirePrivateUnixMode: true);
        policy = pathPolicyFactory.Create(policy.RootPath);
        policy.EnsureSafePath(fullPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The private file to harden does not exist.", fullPath);
        }

        ApplyPrivateFileMode(fullPath, required: true);
        VerifyPrivateFileMode(fullPath, required: true);
    }

    public Task WriteTextAsync(
        string managedRoot,
        string targetPath,
        string content,
        DurableFileWriteOptions? options = null,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? beforeCommit = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return WriteBytesAsync(
            managedRoot,
            targetPath,
            Encoding.UTF8.GetBytes(content),
            options,
            cancellationToken,
            beforeCommit);
    }

    public void WriteText(
        string managedRoot,
        string targetPath,
        string content,
        DurableFileWriteOptions? options = null,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? beforeCommit = null)
        => WriteTextAsync(
                managedRoot,
                targetPath,
                content,
                options,
                cancellationToken,
                beforeCommit)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public void WriteBytes(
        string managedRoot,
        string targetPath,
        ReadOnlyMemory<byte> content,
        DurableFileWriteOptions? options = null,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? beforeCommit = null)
        => WriteBytesAsync(
                managedRoot,
                targetPath,
                content,
                options,
                cancellationToken,
                beforeCommit)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public Task WriteBytesAsync(
        string managedRoot,
        string targetPath,
        ReadOnlyMemory<byte> content,
        DurableFileWriteOptions? options = null,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? beforeCommit = null)
        => WriteStreamAsync(
            managedRoot,
            targetPath,
            (stream, token) => stream.WriteAsync(content, token).AsTask(),
            options,
            cancellationToken,
            beforeCommit);

    public async Task WriteStreamAsync(
        string managedRoot,
        string targetPath,
        Func<Stream, CancellationToken, Task> writeContent,
        DurableFileWriteOptions? options = null,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? beforeCommit = null)
    {
        ArgumentNullException.ThrowIfNull(writeContent);
        options ??= DurableFileWriteOptions.Default;
        ValidateOptions(options);
        IPhysicalFileSystemPathPolicy policy = pathPolicyFactory.Create(managedRoot);
        string fullPath = policy.ResolveContainedPath(targetPath);
        string parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidPath,
                "The durable file target does not have a directory parent.");

        EnsureDirectoryTree(policy.RootPath, parentPath, options.RequirePrivateUnixMode);
        policy = pathPolicyFactory.Create(policy.RootPath);
        policy.RevalidateMutationTarget(fullPath);

        string lockPath = fullPath + ".candoitall.lock";
        await using IAsyncDisposable coordination = await AcquireCoordinationAsync(
            policy.RootPath,
            lockPath,
            options.LockTimeout,
            options.RequirePrivateUnixMode,
            cancellationToken).ConfigureAwait(false);

        policy = pathPolicyFactory.Create(policy.RootPath);
        policy.RevalidateMutationTarget(fullPath);
        CleanupStaleTemporaryFiles(policy, parentPath, Path.GetFileName(fullPath));
        string temporaryPath = Path.Combine(
            parentPath,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        policy.RevalidateMutationTarget(temporaryPath);

        try
        {
            await using (FileStream stream = CreateNewFileStream(
                             temporaryPath,
                             options.RequirePrivateUnixMode,
                             asynchronous: true))
            {
                await writeContent(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            VerifyPrivateFileMode(temporaryPath, options.RequirePrivateUnixMode);
            stageObserver?.Invoke(DurableFileWriteStage.TemporaryFileFlushed);
            if (beforeCommit is not null)
            {
                await beforeCommit(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            policy = pathPolicyFactory.Create(policy.RootPath);
            policy.RevalidateMutationTarget(fullPath);
            policy.EnsureSafePath(temporaryPath);
            stageObserver?.Invoke(DurableFileWriteStage.BeforeCommit);

            Commit(temporaryPath, fullPath, options.CommitMode, options.CreateBackup);
            ApplyPrivateFileMode(fullPath, options.RequirePrivateUnixMode);
            VerifyPrivateFileMode(fullPath, options.RequirePrivateUnixMode);
            if (options.CreateBackup)
            {
                VerifyPrivateFileMode(GetBackupPath(fullPath), options.RequirePrivateUnixMode);
            }

            stageObserver?.Invoke(DurableFileWriteStage.Committed);
        }
        catch (Exception writeFailure)
        {
            try
            {
                CleanupGeneratedFile(policy, temporaryPath);
            }
            catch (Exception cleanupFailure)
            {
                if (writeFailure is PhysicalPathValidationException &&
                    cleanupFailure is PhysicalPathValidationException)
                {
                    writeFailure.Data[CleanupFailureDataKey] = cleanupFailure;
                    ExceptionDispatchInfo.Capture(writeFailure).Throw();
                }

                throw new AggregateException(
                    "The durable write failed and its exact temporary file could not be cleaned safely.",
                    writeFailure,
                    cleanupFailure);
            }

            throw;
        }

        CleanupGeneratedFile(policy, temporaryPath);
    }

    public async Task DeleteAsync(
        string managedRoot,
        string targetPath,
        DurableFileWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= DurableFileWriteOptions.Default;
        ValidateOptions(options);
        IPhysicalFileSystemPathPolicy policy = pathPolicyFactory.Create(managedRoot);
        string fullPath = policy.ResolveContainedPath(targetPath);
        string lockPath = fullPath + ".candoitall.lock";
        await using IAsyncDisposable coordination = await AcquireCoordinationAsync(
            policy.RootPath,
            lockPath,
            options.LockTimeout,
            options.RequirePrivateUnixMode,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        policy = pathPolicyFactory.Create(policy.RootPath);
        policy.RevalidateMutationTarget(fullPath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public IDisposable AcquireCoordination(
        string managedRoot,
        string lockPath,
        TimeSpan timeout,
        bool requirePrivateUnixMode,
        CancellationToken cancellationToken = default)
        => (IDisposable)AcquireCoordinationAsync(
                managedRoot,
                lockPath,
                timeout,
                requirePrivateUnixMode,
                cancellationToken)
            .AsTask()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public async ValueTask<IAsyncDisposable> AcquireCoordinationAsync(
        string managedRoot,
        string lockPath,
        TimeSpan timeout,
        bool requirePrivateUnixMode,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The lock timeout must be positive.");
        }

        IPhysicalFileSystemPathPolicy policy = pathPolicyFactory.Create(managedRoot);
        string fullLockPath = policy.ResolveContainedPath(lockPath);
        string parentPath = Path.GetDirectoryName(fullLockPath)
            ?? throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidPath,
                "The coordination file does not have a directory parent.");
        EnsureDirectoryTree(policy.RootPath, parentPath, requirePrivateUnixMode);
        policy = pathPolicyFactory.Create(policy.RootPath);
        policy.RevalidateMutationTarget(fullLockPath);

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            policy.RevalidateMutationTarget(fullLockPath);
            try
            {
                FileStream stream = OpenLockFile(fullLockPath, requirePrivateUnixMode);
                ApplyPrivateFileMode(fullLockPath, requirePrivateUnixMode);
                VerifyPrivateFileMode(fullLockPath, requirePrivateUnixMode);
                return new FileLockLease(stream);
            }
            catch (IOException) when (stopwatch.Elapsed < timeout)
            {
                TimeSpan remaining = timeout - stopwatch.Elapsed;
                TimeSpan delay = remaining < TimeSpan.FromMilliseconds(25)
                    ? remaining
                    : TimeSpan.FromMilliseconds(25);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (IOException exception)
            {
                throw new TimeoutException(
                    "Timed out waiting for exclusive durable-file coordination.",
                    exception);
            }
        }
    }

    private void EnsureDirectoryTree(string managedRoot, string targetDirectory, bool requirePrivateUnixMode)
    {
        IPhysicalFileSystemPathPolicy policy = pathPolicyFactory.Create(managedRoot);
        string fullDirectory = policy.ResolveContainedPath(targetDirectory);
        string relativePath = Path.GetRelativePath(policy.RootPath, fullDirectory);
        string currentPath = policy.RootPath;

        if (!Directory.Exists(currentPath))
        {
            CreateDirectory(currentPath, requirePrivateUnixMode);
            policy = pathPolicyFactory.Create(currentPath);
            policy.EnsureSafePath(currentPath);
        }

        ApplyPrivateDirectoryMode(currentPath, requirePrivateUnixMode);
        VerifyPrivateDirectoryMode(currentPath, requirePrivateUnixMode);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return;
        }

        foreach (string segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            policy.EnsureSafePath(currentPath, allowMissingLeaf: true);
            if (!Directory.Exists(currentPath))
            {
                CreateDirectory(currentPath, requirePrivateUnixMode);
            }

            policy = pathPolicyFactory.Create(managedRoot);
            policy.EnsureSafePath(currentPath);
            ApplyPrivateDirectoryMode(currentPath, requirePrivateUnixMode);
            VerifyPrivateDirectoryMode(currentPath, requirePrivateUnixMode);
        }
    }

    private static void CreateDirectory(string path, bool requirePrivateUnixMode)
    {
        if (requirePrivateUnixMode && !OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path, PrivateDirectoryMode);
            return;
        }

        Directory.CreateDirectory(path);
    }

    private static FileStream CreateNewFileStream(string path, bool requirePrivateUnixMode, bool asynchronous)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 64 * 1024,
            Options = FileOptions.WriteThrough |
                      (asynchronous ? FileOptions.Asynchronous : FileOptions.None)
        };
        if (requirePrivateUnixMode && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFileMode;
        }

        return new FileStream(path, options);
    }

    private static FileStream OpenLockFile(string path, bool requirePrivateUnixMode)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 1,
            Options = FileOptions.WriteThrough
        };
        if (requirePrivateUnixMode && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFileMode;
        }

        return new FileStream(path, options);
    }

    private static void Commit(
        string temporaryPath,
        string fullPath,
        DurableFileCommitMode commitMode,
        bool createBackup)
    {
        if (commitMode == DurableFileCommitMode.CreateNew)
        {
            File.Move(temporaryPath, fullPath);
            return;
        }

        if (!File.Exists(fullPath))
        {
            File.Move(temporaryPath, fullPath);
            return;
        }

        if (createBackup)
        {
            File.Replace(temporaryPath, fullPath, GetBackupPath(fullPath), ignoreMetadataErrors: true);
            return;
        }

        File.Move(temporaryPath, fullPath, overwrite: true);
    }

    private static string GetBackupPath(string fullPath)
        => fullPath + ".bak";

    private static void CleanupGeneratedFile(IPhysicalFileSystemPathPolicy policy, string path)
    {
        policy.EnsureSafePath(path, allowMissingLeaf: true);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void CleanupStaleTemporaryFiles(
        IPhysicalFileSystemPathPolicy policy,
        string parentPath,
        string targetFileName)
    {
        FileInfo[] staleFiles = new DirectoryInfo(parentPath)
            .EnumerateFiles()
            .Where(file => IsGeneratedTemporaryFileName(
                file.Name,
                targetFileName,
                policy.PathComparison))
            .OrderBy(file => file.Name, policy.PathComparer)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (FileInfo staleFile in staleFiles)
        {
            policy.EnsureSafePath(staleFile.FullName);
            staleFile.Delete();
        }
    }

    private static bool IsGeneratedTemporaryFileName(
        string candidate,
        string targetFileName,
        StringComparison comparison)
    {
        string prefix = $".{targetFileName}.";
        const string suffix = ".tmp";
        const int tokenLength = 32;
        if (candidate.Length != prefix.Length + tokenLength + suffix.Length ||
            !candidate.StartsWith(prefix, comparison) ||
            !candidate.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParseExact(
            candidate.AsSpan(prefix.Length, tokenLength),
            "N",
            out _);
    }

    private static void ApplyPrivateDirectoryMode(string path, bool required)
    {
        if (required && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, PrivateDirectoryMode);
        }
    }

    private static void VerifyPrivateDirectoryMode(string path, bool required)
    {
        if (required && !OperatingSystem.IsWindows() && File.GetUnixFileMode(path) != PrivateDirectoryMode)
        {
            throw new UnauthorizedAccessException(
                "A secret-bearing directory could not be restricted to owner-only access.");
        }
    }

    private static void ApplyPrivateFileMode(string path, bool required)
    {
        if (required && !OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetUnixFileMode(path, PrivateFileMode);
        }
    }

    private static void VerifyPrivateFileMode(string path, bool required)
    {
        if (required && !OperatingSystem.IsWindows() && File.Exists(path) && File.GetUnixFileMode(path) != PrivateFileMode)
        {
            throw new UnauthorizedAccessException(
                "A secret-bearing file could not be restricted to owner-only access.");
        }
    }

    private static void ValidateOptions(DurableFileWriteOptions options)
    {
        if (!Enum.IsDefined(options.CommitMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The durable-file commit mode is not recognized.");
        }

        if (options.CommitMode == DurableFileCommitMode.CreateNew && options.CreateBackup)
        {
            throw new ArgumentException(
                "A create-new durable write cannot create a replacement backup.",
                nameof(options));
        }

        if (options.LockTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The durable-file lock timeout must be positive.");
        }
    }

    private sealed class FileLockLease(FileStream stream) : IDisposable, IAsyncDisposable
    {
        public void Dispose()
            => stream.Dispose();

        public ValueTask DisposeAsync()
            => stream.DisposeAsync();
    }
}
