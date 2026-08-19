using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class TailwindSourceMonitoringTests
{
    [Fact]
    public async Task Signal_queue_coalesces_duplicate_paths_without_losing_generation_or_kind()
    {
        using var directory = new TemporaryDirectory();
        TailwindWatchRoot root = CreateRoot(directory.Path);
        string firstPath = Path.Combine(directory.Path, "First.razor");
        string secondPath = Path.Combine(directory.Path, "Second.razor");
        var queue = new TailwindWatchSignalQueue();

        queue.Signal(root, firstPath, TailwindWatchSignalKind.FileSystemEvent);
        queue.Signal(root, firstPath, TailwindWatchSignalKind.FileSystemEvent);
        queue.Signal(root, secondPath, TailwindWatchSignalKind.WatcherError);

        TailwindWatchSignalBatch batch = await queue.ReadBatchAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Equal(3, batch.Generation);
        Assert.Equal(
            TailwindWatchSignalKind.FileSystemEvent | TailwindWatchSignalKind.WatcherError,
            batch.Kinds);
        Assert.Equal([firstPath, secondPath], batch.ChangedPaths);
    }

    [Fact]
    public async Task Signal_queue_preserves_both_rename_paths_and_poll_recovery()
    {
        using var directory = new TemporaryDirectory();
        TailwindWatchRoot root = CreateRoot(directory.Path);
        string oldPath = Path.Combine(directory.Path, "Before.razor");
        string newPath = Path.Combine(directory.Path, "After.razor");
        var queue = new TailwindWatchSignalQueue();

        queue.Signal(root, oldPath, TailwindWatchSignalKind.FileSystemEvent);
        queue.Signal(root, newPath, TailwindWatchSignalKind.FileSystemEvent);
        queue.Signal(root, root.FullPath, TailwindWatchSignalKind.Poll);

        TailwindWatchSignalBatch batch = await queue.ReadBatchAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Equal(3, batch.Generation);
        Assert.True(batch.Kinds.HasFlag(TailwindWatchSignalKind.Poll));
        Assert.Contains(oldPath, batch.ChangedPaths);
        Assert.Contains(newPath, batch.ChangedPaths);
    }

    [Fact]
    public void Fingerprint_detects_content_change_without_a_watcher_signal()
    {
        using var directory = new TemporaryDirectory();
        TailwindWatchRoot root = CreateRoot(directory.Path);
        string sourcePath = Path.Combine(directory.Path, "Component.razor");
        string outputPath = Path.Combine(directory.Path, "output.js");
        File.WriteAllText(sourcePath, "<p>before</p>");

        string before = TailwindSourceFingerprint.Compute([root], outputPath);
        File.WriteAllText(sourcePath, "<p>after</p>");
        string after = TailwindSourceFingerprint.Compute([root], outputPath);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Fingerprint_ignores_generated_output_changes()
    {
        using var directory = new TemporaryDirectory();
        TailwindWatchRoot root = CreateRoot(directory.Path);
        string sourcePath = Path.Combine(directory.Path, "Component.razor");
        string outputPath = Path.Combine(directory.Path, "output.js");
        File.WriteAllText(sourcePath, "<p>stable</p>");
        File.WriteAllText(outputPath, "before");

        string before = TailwindSourceFingerprint.Compute([root], outputPath);
        File.WriteAllText(outputPath, "after");
        string after = TailwindSourceFingerprint.Compute([root], outputPath);

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Linux_case_distinct_source_paths_remain_distinct()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        TailwindWatchRoot root = CreateRoot(directory.Path);
        string upperPath = Path.Combine(directory.Path, "Component.razor");
        string lowerPath = Path.Combine(directory.Path, "component.razor");
        var queue = new TailwindWatchSignalQueue();

        queue.Signal(root, upperPath, TailwindWatchSignalKind.FileSystemEvent);
        queue.Signal(root, lowerPath, TailwindWatchSignalKind.FileSystemEvent);
        TailwindWatchSignalBatch batch = await queue.ReadBatchAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Equal(PhysicalFileSystemCaseSensitivity.Sensitive, root.PathPolicy.CaseSensitivity);
        Assert.Equal(2, batch.ChangedPaths.Count);
    }

    [Fact]
    public void Linux_path_policy_does_not_ignore_case_distinct_segments_or_extensions()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        TailwindWatchRoot root = CreateRoot(directory.Path);
        string upperBinSource = Path.Combine(directory.Path, "BIN", "Component.razor");
        string upperExtensionSource = Path.Combine(directory.Path, "Component.RAZOR");

        Assert.True(TailwindSourcePathPolicy.IsRelevant(root, upperBinSource, Path.Combine(directory.Path, "output.css")));
        Assert.False(TailwindSourcePathPolicy.IsRelevant(root, upperExtensionSource, Path.Combine(directory.Path, "output.css")));
    }

    private static TailwindWatchRoot CreateRoot(string path)
    {
        var factory = new PhysicalFileSystemPathPolicyFactory();
        return new TailwindWatchRoot(0, path, TailwindWatchRootKind.ContentSource, factory.Create(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "candoitall-tailwind-monitoring-tests",
                Guid.NewGuid().ToString("N"));
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
