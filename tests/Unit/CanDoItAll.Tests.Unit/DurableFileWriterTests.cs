using System.Text;
using System.Diagnostics;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class DurableFileWriterTests
{
    [Fact]
    public async Task Interrupted_write_preserves_complete_previous_content_and_cleans_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "catalog.json");
        var factory = new PhysicalFileSystemPathPolicyFactory();
        var writer = new DurableFileWriter(factory);
        await writer.WriteTextAsync(directory.Path, targetPath, "old");
        var failingWriter = new DurableFileWriter(
            factory,
            stage =>
            {
                if (stage == DurableFileWriteStage.BeforeCommit)
                {
                    throw new InjectedWriteFailureException();
                }
            });

        await Assert.ThrowsAsync<InjectedWriteFailureException>(() =>
            failingWriter.WriteTextAsync(directory.Path, targetPath, "new"));

        Assert.Equal("old", await File.ReadAllTextAsync(targetPath));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".*.tmp"));
    }

    [Fact]
    public async Task Cancellation_after_flush_preserves_previous_content()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "preferences.json");
        var factory = new PhysicalFileSystemPathPolicyFactory();
        var writer = new DurableFileWriter(factory);
        await writer.WriteTextAsync(directory.Path, targetPath, "old");
        using var cancellation = new CancellationTokenSource();
        var cancellingWriter = new DurableFileWriter(
            factory,
            stage =>
            {
                if (stage == DurableFileWriteStage.TemporaryFileFlushed)
                {
                    cancellation.Cancel();
                }
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancellingWriter.WriteTextAsync(
                directory.Path,
                targetPath,
                "new",
                cancellationToken: cancellation.Token));

        Assert.Equal("old", await File.ReadAllTextAsync(targetPath));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".*.tmp"));
    }

    [Fact]
    public async Task Exclusive_coordination_times_out_and_recovers_after_owner_releases_handle()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "profiles.json");
        string lockPath = targetPath + ".candoitall.lock";
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        await using IAsyncDisposable firstLease = await writer.AcquireCoordinationAsync(
            directory.Path,
            lockPath,
            TimeSpan.FromSeconds(1),
            requirePrivateUnixMode: false);
        var options = DurableFileWriteOptions.Default with
        {
            LockTimeout = TimeSpan.FromMilliseconds(100)
        };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            writer.WriteTextAsync(directory.Path, targetPath, "blocked", options));
        await firstLease.DisposeAsync();

        await writer.WriteTextAsync(directory.Path, targetPath, "recovered", options);
        Assert.Equal("recovered", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task Concurrent_writers_commit_one_complete_payload_without_temporary_residue()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "storage.bin");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        byte[][] payloads = Enumerable.Range(0, 12)
            .Select(index => Encoding.UTF8.GetBytes(new string((char)('a' + index), 8_192)))
            .ToArray();

        await Task.WhenAll(payloads.Select(payload =>
            writer.WriteBytesAsync(directory.Path, targetPath, payload)));

        byte[] committed = await File.ReadAllBytesAsync(targetPath);
        Assert.Contains(payloads, payload => payload.AsSpan().SequenceEqual(committed));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".*.tmp"));
    }

    [Fact]
    public async Task Crashed_process_preserves_previous_content_and_next_writer_recovers_stale_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "catalog.json");
        string readyPath = Path.Combine(directory.Path, "child-ready");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        await writer.WriteTextAsync(directory.Path, targetPath, "old");
        string hostPath = Path.Combine(
            AppContext.BaseDirectory,
            "CanDoItAll.DurableFileWriter.TestHost.dll");
        Assert.True(File.Exists(hostPath), $"The durable-writer test host was not built at '{hostPath}'.");
        using Process process = StartTestHost(hostPath, targetPath, readyPath);

        try
        {
            await WaitForFileAsync(readyPath, process, TimeSpan.FromSeconds(10));
            var boundedOptions = DurableFileWriteOptions.Default with
            {
                LockTimeout = TimeSpan.FromMilliseconds(200)
            };
            await Assert.ThrowsAsync<TimeoutException>(() =>
                writer.WriteTextAsync(directory.Path, targetPath, "blocked", boundedOptions));

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Equal("old", await File.ReadAllTextAsync(targetPath));
            Assert.Single(Directory.EnumerateFiles(directory.Path, ".*.tmp"));

            await writer.WriteTextAsync(directory.Path, targetPath, "recovered", boundedOptions);

            Assert.Equal("recovered", await File.ReadAllTextAsync(targetPath));
            Assert.Empty(Directory.EnumerateFiles(directory.Path, ".*.tmp"));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [Fact]
    public async Task Backup_contains_previous_complete_version()
    {
        using var directory = new TemporaryDirectory();
        string targetPath = Path.Combine(directory.Path, "catalog.json");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        await writer.WriteTextAsync(directory.Path, targetPath, "old");

        await writer.WriteTextAsync(
            directory.Path,
            targetPath,
            "new",
            DurableFileWriteOptions.Default with { CreateBackup = true });

        Assert.Equal("new", await File.ReadAllTextAsync(targetPath));
        Assert.Equal("old", await File.ReadAllTextAsync(targetPath + ".bak"));
    }

    [Fact]
    public async Task Write_creates_missing_managed_root_and_revalidates_it()
    {
        using var parent = new TemporaryDirectory();
        string managedRoot = Path.Combine(parent.Path, "new-root");
        string targetPath = Path.Combine(managedRoot, "nested", "catalog.json");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());

        await writer.WriteTextAsync(managedRoot, targetPath, "created");

        Assert.Equal("created", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task Private_write_applies_and_verifies_owner_only_unix_modes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        string privateDirectory = Path.Combine(directory.Path, "keys");
        string targetPath = Path.Combine(privateDirectory, "key.json");
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());

        await writer.WriteTextAsync(
            directory.Path,
            targetPath,
            "secret",
            DurableFileWriteOptions.Private);

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(privateDirectory));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(targetPath));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(targetPath + ".candoitall.lock"));
    }

    [Fact]
    public async Task Parent_link_swap_before_commit_fails_closed_without_touching_outside_target()
    {
        using var managed = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        string parentPath = Path.Combine(managed.Path, "catalog");
        Directory.CreateDirectory(parentPath);
        string movedParentPath = Path.Combine(managed.Path, "catalog-moved");
        string targetPath = Path.Combine(parentPath, "profiles.json");
        string outsideTarget = Path.Combine(outside.Path, "profiles.json");
        await File.WriteAllTextAsync(outsideTarget, "outside");
        var writer = new DurableFileWriter(
            new PhysicalFileSystemPathPolicyFactory(),
            stage =>
            {
                if (stage != DurableFileWriteStage.TemporaryFileFlushed)
                {
                    return;
                }

                Directory.Move(parentPath, movedParentPath);
                Directory.CreateSymbolicLink(parentPath, outside.Path);
            });

        Exception? exception = await Record.ExceptionAsync(() =>
            writer.WriteTextAsync(managed.Path, targetPath, "managed"));
        if (exception is IOException && exception is not PhysicalPathValidationException && OperatingSystem.IsWindows())
        {
            return;
        }

        PhysicalPathValidationException validationException =
            Assert.IsType<PhysicalPathValidationException>(exception);
        Assert.IsType<PhysicalPathValidationException>(
            validationException.Data[DurableFileWriter.CleanupFailureDataKey]);
        Assert.Equal("outside", await File.ReadAllTextAsync(outsideTarget));
        Assert.Single(Directory.EnumerateFiles(outside.Path));
    }

    private sealed class InjectedWriteFailureException : IOException;

    private static Process StartTestHost(string hostPath, string targetPath, string readyPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add(hostPath);
        startInfo.ArgumentList.Add("write-and-wait-before-commit");
        startInfo.ArgumentList.Add(targetPath);
        startInfo.ArgumentList.Add(readyPath);
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The durable-writer test process could not be started.");
    }

    private static async Task WaitForFileAsync(string path, Process process, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!File.Exists(path))
        {
            if (process.HasExited)
            {
                string standardError = await process.StandardError.ReadToEndAsync(cancellation.Token);
                throw new InvalidOperationException(
                    $"The durable-writer test process exited with code {process.ExitCode}: {standardError}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellation.Token);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"candoitall-durable-writer-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
