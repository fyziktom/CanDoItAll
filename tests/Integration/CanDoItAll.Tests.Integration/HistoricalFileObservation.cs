using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class HistoricalFileObservation : IDisposable {
    private const int MaximumEvents = 4096;
    private const int MaximumDiagnosticEvents = 16;
    private const int MaximumSetupEntries = 65536;
    private static readonly TimeSpan SetupBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QuietInterval = TimeSpan.FromMilliseconds(100);
    private readonly FileSystemWatcher watcher;
    private readonly ConcurrentQueue<(WatcherChangeTypes Kind, string Path)> events = new();
    private readonly string root;
    private readonly string sentinel;
    private readonly string renamedSentinel;
    private readonly object sync = new();
    private TaskCompletionSource? deleted;
    private Exception? failure;
    private int eventCount;
    private int nonsentinelEventCount;

    private HistoricalFileObservation(string root) {
        this.root = root;
        sentinel = Path.Combine(root, $"historical-observer-{Guid.NewGuid():N}.tmp");
        renamedSentinel = sentinel + ".renamed";
        watcher = new(root) {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Security,
            InternalBufferSize = 64 * 1024
        };
        watcher.Created += Observe;
        watcher.Changed += Observe;
        watcher.Deleted += Observe;
        watcher.Renamed += Observe;
        watcher.Error += (_, args) => {
            lock (sync) {
                failure ??= args.GetException();
            }
        };
        watcher.EnableRaisingEvents = true;
    }

    internal static async Task<HistoricalFileObservation> StartAsync(string root) {
        var observation = new HistoricalFileObservation(root);
        try {
            var setup = Stopwatch.StartNew();
            await observation.BarrierAsync(RemainingSetupBudget(setup));
            var calibration = observation.events.ToArray();
            Assert.Contains(calibration, item => item.Kind == WatcherChangeTypes.Created);
            Assert.Contains(calibration, item => item.Kind == WatcherChangeTypes.Changed);
            Assert.Contains(calibration, item => item.Kind == WatcherChangeTypes.Renamed);
            Assert.Contains(calibration, item => item.Kind == WatcherChangeTypes.Deleted);
            observation.ObserveExistingDirectoryMetadata(setup);
            await observation.DrainSetupAsync(setup);
            lock (observation.sync) {
                Assert.Null(observation.failure);
                observation.events.Clear();
                observation.eventCount = 0;
            }
            return observation;
        } catch {
            observation.Dispose();
            throw;
        }
    }

    internal async Task AssertNoChangesAsync() {
        await BarrierAsync();
        Assert.Null(failure);
        var unexpected = events.Where(item => item.Path != sentinel && item.Path != renamedSentinel).ToArray();
        Assert.True(unexpected.Length == 0,
            $"Historical no-write observation found {unexpected.Length} unexpected events; showing at most {MaximumDiagnosticEvents}." +
            Environment.NewLine + string.Join(Environment.NewLine, unexpected.Take(MaximumDiagnosticEvents)
                .Select(item => $"Kind={item.Kind}; Path={JsonSerializer.Serialize(item.Path)}")));
    }

    private void ObserveExistingDirectoryMetadata(Stopwatch setup) {
        var pending = new Stack<string>();
        pending.Push(root);
        var visited = 0;
        while (pending.TryPop(out var directory)) {
            _ = RemainingSetupBudget(setup);
            _ = Directory.GetCreationTimeUtc(directory);
            _ = Directory.GetLastWriteTimeUtc(directory);
            _ = Directory.GetLastAccessTimeUtc(directory);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", new EnumerationOptions {
                RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0
            })) {
                _ = RemainingSetupBudget(setup);
                if (++visited > MaximumSetupEntries) {
                    throw new InvalidOperationException("Historical observer setup exceeded its bounded entry capacity.");
                }
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) {
                    throw new InvalidOperationException("Historical observer setup encountered a filesystem link.");
                }
                if ((attributes & FileAttributes.Directory) != 0) {
                    pending.Push(path);
                }
            }
        }
    }

    private async Task DrainSetupAsync(Stopwatch setup) {
        for (var pass = 0; pass < 20; pass++) {
            await BarrierAsync(RemainingSetupBudget(setup));
            int before;
            lock (sync) {
                before = nonsentinelEventCount;
            }
            if (RemainingSetupBudget(setup) < QuietInterval) {
                throw new TimeoutException("Historical observer setup could not establish a complete quiet interval.");
            }
            await Task.Delay(QuietInterval);
            await BarrierAsync(RemainingSetupBudget(setup));
            _ = RemainingSetupBudget(setup);
            lock (sync) {
                Assert.Null(failure);
                if (before == nonsentinelEventCount) {
                    return;
                }
            }
        }
        throw new InvalidOperationException("Historical observer setup did not drain within its bounded barrier passes.");
    }

    private static TimeSpan RemainingSetupBudget(Stopwatch setup) {
        var remaining = SetupBudget - setup.Elapsed;
        return remaining > TimeSpan.Zero
            ? remaining
            : throw new TimeoutException("Historical observer setup exceeded its bounded time budget.");
    }

    private async Task BarrierAsync(TimeSpan? timeout = null) {
        lock (sync) {
            deleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        await File.WriteAllBytesAsync(sentinel, [1]);
        await File.AppendAllTextAsync(sentinel, "barrier");
        File.Move(sentinel, renamedSentinel);
        File.Delete(renamedSentinel);
        await deleted.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(5));
        Assert.Null(failure);
    }

    private void Observe(object sender, FileSystemEventArgs args) {
        lock (sync) {
            if (++eventCount > MaximumEvents) {
                failure ??= new InvalidOperationException("Historical filesystem observation exceeded its bounded event capacity.");
                return;
            }
            events.Enqueue((args.ChangeType, args.FullPath));
            if (args.FullPath != sentinel && args.FullPath != renamedSentinel) {
                nonsentinelEventCount++;
            }
            if (args.ChangeType == WatcherChangeTypes.Deleted && args.FullPath == renamedSentinel) {
                deleted?.TrySetResult();
            }
        }
    }

    public void Dispose() {
        watcher.Dispose();
        foreach (var path in new[] { sentinel, renamedSentinel }) {
            if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Historical observer cleanup escaped its recorded fixture root.");
            }
            File.Delete(path);
        }
    }
}
