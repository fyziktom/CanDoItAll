using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

internal enum ReceiptPerformancePhase { OwnerPreparation, Acquire, Reconcile, OwnerLookup, LeaseRelease, DetailRead, NativeRefresh, ResponseEncoding, OwnerDisposal }

internal sealed record ReceiptPerformancePhaseCost(int Worker, ReceiptPerformancePhase Phase, double Milliseconds,
    long? ProcessAllocatedBytes, string? FailureType, double StartedOffsetMilliseconds, double EndedOffsetMilliseconds);
internal sealed record ReceiptPerformanceWorkerInterval(int Worker, double StartedOffsetMilliseconds, double EndedOffsetMilliseconds);
internal sealed record ReceiptPerformanceActivity(string Operation, double Milliseconds, long? LogicalPayloadBytes,
    ReceiptPerformancePhase? Phase, int? Worker);
internal sealed record ReceiptPerformanceRead(string PayloadType, FileSandboxWorkspaceJsonReadKind Kind, long LogicalBytes);
internal sealed record ReceiptPerformanceStoreCost(ReceiptPerformancePhaseCost[] Phases, ReceiptPerformanceActivity[] Activities,
    ReceiptPerformanceRead[] Reads, int DurableIntents, int OwnerLookups, int CurrentReadChecks, bool EvidenceOverflow,
    ReceiptPerformanceWorkerInterval[] Workers, int MaximumOverlappingWorkers);
internal sealed record ReceiptPerformanceMemory(long ManagedEstimateBytes, long LastGcHeapBytes, long LastGcLohBytes,
    long LastGcCommittedBytes, long PrivateBytes, long WorkingSetBytes, long OsProcessPeakWorkingSetBytes,
    long PhysicalTotalBytes, long PhysicalAvailableBytes, long ProcessAllocatedBytes, double CpuMilliseconds,
    double TotalGcPauseMilliseconds, int Gen0, int Gen1, int Gen2, long LastGcIndex, long LastGcTotalAvailableMemoryBytes,
    long? WindowsEffectiveCommitLimitBytes, long? WindowsProcessAvailableCommitBytes);
internal sealed record ReceiptPerformanceMemoryWindow(ReceiptPerformanceMemory First, ReceiptPerformanceMemory Last,
    long MaximumObservedManagedEstimateBytes, long MaximumObservedLastGcHeapBytes, long MaximumObservedLastGcLohBytes,
    long MaximumObservedLastGcCommittedBytes, long MaximumObservedPrivateBytes, long MaximumObservedWorkingSetBytes,
    long MinimumObservedPhysicalAvailableBytes, int Samples, double RequestedIntervalMilliseconds,
    double MaximumObservedGapMilliseconds);

internal sealed class AgentReceiptStorePerformanceProbe : IDisposable {
    private const int MaximumEvents = 10_000;
    private const string PhaseTag = "receipt.performance.phase";
    private const string WorkerTag = "receipt.performance.worker";
    private readonly ConcurrentQueue<ReceiptPerformancePhaseCost> phases = new();
    private readonly ConcurrentQueue<ReceiptPerformanceActivity> activities = new();
    private readonly ConcurrentQueue<ReceiptPerformanceRead> reads = new();
    private readonly ConcurrentQueue<ReceiptPerformanceWorkerInterval> workers = new();
    private readonly ConcurrentDictionary<int, ReceiptPerformanceOperation> observedOperations = new();
    private readonly long origin = Stopwatch.GetTimestamp();
    private readonly Activity root = new("receipt-store-performance");
    private readonly ActivityListener listener;
    private readonly bool isolated;
    private int events;
    private int overflow;
    private int durableIntents;
    private int ownerLookups;
    private int currentReads;
    private int activeWorkers;
    private int maximumWorkers;

    internal AgentReceiptStorePerformanceProbe(int concurrency) {
        isolated = concurrency == 1;
        root.SetIdFormat(ActivityIdFormat.W3C);
        root.Start();
        listener = new() {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.TraceId != root.TraceId || !Reserve()) {
                    return;
                }
                var payload = activity.GetTagItem("payload.bytes");
                var phase = FindPhase(activity);
                activities.Enqueue(new(activity.OperationName, activity.Duration.TotalMilliseconds,
                    payload is null ? null : Convert.ToInt64(payload, CultureInfo.InvariantCulture), phase.Phase, phase.Worker));
            }
        };
        ActivitySource.AddActivityListener(listener);
    }

    internal void Read(FileSandboxWorkspacePhysicalJsonRead read) {
        if (Reserve()) {
            reads.Enqueue(new(read.PayloadType?.Name ?? "RawText", read.Kind, read.LengthBytes));
        }
    }

    internal void Commit(ExistingRunDetailCommitStage stage) {
        if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
            Interlocked.Increment(ref durableIntents);
        }
    }

    internal void Lookup() => Interlocked.Increment(ref ownerLookups);
    internal void CurrentRead(int count = 1) => Interlocked.Add(ref currentReads, count);
    internal void ObserveOperation(ReceiptPerformanceOperation operation) => observedOperations[operation.Worker] = operation;
    internal ReceiptPerformanceOperation[] ObservedOperations => observedOperations.Values.OrderBy(item => item.Worker).ToArray();

    internal async Task<T> MeasureWorkerAsync<T>(int worker, Func<Task<T>> action) {
        var started = Stopwatch.GetTimestamp();
        var active = Interlocked.Increment(ref activeWorkers);
        int observed;
        do {
            observed = Volatile.Read(ref maximumWorkers);
        } while (active > observed && Interlocked.CompareExchange(ref maximumWorkers, active, observed) != observed);
        try {
            return await action();
        } finally {
            var ended = Stopwatch.GetTimestamp();
            Interlocked.Decrement(ref activeWorkers);
            workers.Enqueue(new(worker, Stopwatch.GetElapsedTime(origin, started).TotalMilliseconds,
                Stopwatch.GetElapsedTime(origin, ended).TotalMilliseconds));
        }
    }

    internal async Task<T> MeasureAsync<T>(int worker, ReceiptPerformancePhase phase, Func<Task<T>> action) {
        using var activity = new Activity("receipt-store-phase").SetTag(PhaseTag, phase).SetTag(WorkerTag, worker).Start();
        var allocated = isolated ? GC.GetTotalAllocatedBytes(precise: true) : 0;
        var started = Stopwatch.GetTimestamp();
        string? failure = null;
        try {
            return await action();
        } catch (Exception exception) {
            failure = exception.GetType().FullName;
            throw;
        } finally {
            var ended = Stopwatch.GetTimestamp();
            phases.Enqueue(new(worker, phase, Stopwatch.GetElapsedTime(started, ended).TotalMilliseconds,
                isolated ? GC.GetTotalAllocatedBytes(precise: true) - allocated : null, failure,
                Stopwatch.GetElapsedTime(origin, started).TotalMilliseconds, Stopwatch.GetElapsedTime(origin, ended).TotalMilliseconds));
        }
    }

    internal async Task MeasureAsync(int worker, ReceiptPerformancePhase phase, Func<Task> action) {
        await MeasureAsync(worker, phase, async () => {
            await action();
            return true;
        });
    }

    private static (ReceiptPerformancePhase? Phase, int? Worker) FindPhase(Activity activity) {
        for (Activity? current = activity; current is not null; current = current.Parent) {
            if (current.GetTagItem(PhaseTag) is ReceiptPerformancePhase phase && current.GetTagItem(WorkerTag) is int worker) {
                return (phase, worker);
            }
        }
        return (null, null);
    }

    internal ReceiptPerformanceStoreCost Snapshot() {
        return new(phases.ToArray(), activities.ToArray(), reads.ToArray(), durableIntents, ownerLookups, currentReads,
            Volatile.Read(ref overflow) != 0, workers.ToArray(), maximumWorkers);
    }

    private bool Reserve() {
        if (Interlocked.Increment(ref events) <= MaximumEvents) {
            return true;
        }
        Interlocked.Exchange(ref overflow, 1);
        return false;
    }

    public void Dispose() {
        listener.Dispose();
        root.Dispose();
    }
}

internal sealed class ReceiptPerformanceMemorySampler : IAsyncDisposable {
    internal static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);
    private readonly CancellationTokenSource stopping = new();
    private readonly Process process = Process.GetCurrentProcess();
    private readonly object sync = new();
    private readonly Task sampling;
    private ReceiptPerformanceMemoryWindow window;
    private long previousTimestamp;

    internal ReceiptPerformanceMemorySampler() {
        ReceiptPerformanceMemory first;
        try {
            first = Read(process);
        } catch {
            process.Dispose();
            stopping.Dispose();
            throw;
        }
        window = new(first, first, first.ManagedEstimateBytes, first.LastGcHeapBytes, first.LastGcLohBytes,
            first.LastGcCommittedBytes, first.PrivateBytes, first.WorkingSetBytes, first.PhysicalAvailableBytes, 1,
            Interval.TotalMilliseconds, 0);
        previousTimestamp = Stopwatch.GetTimestamp();
        sampling = SampleAsync();
    }

    internal ReceiptPerformanceMemoryWindow Snapshot() {
        lock (sync) {
            return window;
        }
    }

    private async Task SampleAsync() {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stopping.Token)) {
            Capture();
        }
    }

    private void Capture() {
        var current = Read(process);
        var timestamp = Stopwatch.GetTimestamp();
        lock (sync) {
            window = window with {
                Last = current,
                MaximumObservedManagedEstimateBytes = Math.Max(window.MaximumObservedManagedEstimateBytes, current.ManagedEstimateBytes),
                MaximumObservedLastGcHeapBytes = Math.Max(window.MaximumObservedLastGcHeapBytes, current.LastGcHeapBytes),
                MaximumObservedLastGcLohBytes = Math.Max(window.MaximumObservedLastGcLohBytes, current.LastGcLohBytes),
                MaximumObservedLastGcCommittedBytes = Math.Max(window.MaximumObservedLastGcCommittedBytes, current.LastGcCommittedBytes),
                MaximumObservedPrivateBytes = Math.Max(window.MaximumObservedPrivateBytes, current.PrivateBytes),
                MaximumObservedWorkingSetBytes = Math.Max(window.MaximumObservedWorkingSetBytes, current.WorkingSetBytes),
                MinimumObservedPhysicalAvailableBytes = Math.Min(window.MinimumObservedPhysicalAvailableBytes, current.PhysicalAvailableBytes),
                Samples = checked(window.Samples + 1),
                MaximumObservedGapMilliseconds = Math.Max(window.MaximumObservedGapMilliseconds,
                    Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalMilliseconds)
            };
            previousTimestamp = timestamp;
        }
    }

    private static ReceiptPerformanceMemory Read(Process process) {
        process.Refresh();
        var gc = GC.GetGCMemoryInfo();
        var physical = ReadPhysicalMemory();
        return new(GC.GetTotalMemory(forceFullCollection: false), gc.HeapSizeBytes,
            gc.GenerationInfo.Length > 3 ? gc.GenerationInfo[3].SizeAfterBytes : 0, gc.TotalCommittedBytes,
            process.PrivateMemorySize64, process.WorkingSet64, process.PeakWorkingSet64,
            physical.Total, physical.Available, GC.GetTotalAllocatedBytes(precise: true),
            process.TotalProcessorTime.TotalMilliseconds, GC.GetTotalPauseDuration().TotalMilliseconds,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), gc.Index,
            gc.TotalAvailableMemoryBytes, physical.CommitLimit, physical.CommitAvailable);
    }

    private static (long Total, long Available, long? CommitLimit, long? CommitAvailable) ReadPhysicalMemory() {
        if (OperatingSystem.IsWindows()) {
            var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            if (!GlobalMemoryStatusEx(ref status)) {
                throw new InvalidOperationException("The owned benchmark could not observe current physical-memory headroom.");
            }
            return (checked((long)status.TotalPhysical), checked((long)status.AvailablePhysical),
                checked((long)status.TotalPageFile), checked((long)status.AvailablePageFile));
        }
        if (OperatingSystem.IsLinux()) {
            using var stream = new FileStream("/proc/meminfo", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            long total = 0;
            long available = 0;
            for (var index = 0; index < 128 && reader.ReadLine() is { } line; index++) {
                if (line.Length > 256) {
                    throw new InvalidDataException("The physical-memory observation exceeded its bounded format.");
                }
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3 && parts[2] == "kB" && long.TryParse(parts[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var value)) {
                    if (parts[0] == "MemTotal:") {
                        total = checked(value * 1024);
                    } else if (parts[0] == "MemAvailable:") {
                        available = checked(value * 1024);
                    }
                }
            }
            if (total > 0 && available > 0 && available <= total) {
                return (total, available, null, null);
            }
            throw new InvalidDataException("Current physical-memory headroom was not available in the bounded observation.");
        }
        throw new PlatformNotSupportedException("This opt-in harness requires a reviewed physical-memory observation on this platform.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    public async ValueTask DisposeAsync() {
        await stopping.CancelAsync();
        try {
            await sampling;
        } catch (OperationCanceledException) when (stopping.IsCancellationRequested) {
        } finally {
            try {
                Capture();
            } finally {
                process.Dispose();
                stopping.Dispose();
            }
        }
    }
}
