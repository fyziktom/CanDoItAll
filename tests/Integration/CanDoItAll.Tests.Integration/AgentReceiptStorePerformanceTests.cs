using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Models;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "LongRunning")]
public sealed class AgentReceiptStorePerformanceTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.OneProposal)]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.FourProposals)]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.MaximumProposals)]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.NearMaximumBytes)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.OneProposal)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.FourProposals)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.MaximumProposals)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.NearMaximumBytes)]
    [InlineData(ReceiptPerformanceWorkload.NativeSessionRefresh, ReceiptPerformanceShape.OneProposal)]
    [InlineData(ReceiptPerformanceWorkload.NativeSessionRefresh, ReceiptPerformanceShape.FourProposals)]
    [InlineData(ReceiptPerformanceWorkload.NativeSessionRefresh, ReceiptPerformanceShape.MaximumProposals)]
    [InlineData(ReceiptPerformanceWorkload.NativeSessionRefresh, ReceiptPerformanceShape.NearMaximumBytes)]
    public Task Serial_owner_slice_detail_and_native_refresh(ReceiptPerformanceWorkload workload, ReceiptPerformanceShape shape)
        => RunAsync(workload, shape, ReceiptPerformanceTopology.Serial, 1);

    [Theory]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.MaximumProposals, 2)]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.MaximumProposals, 4)]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.NearMaximumBytes, 2)]
    [InlineData(ReceiptPerformanceWorkload.CoreSlice, ReceiptPerformanceShape.NearMaximumBytes, 4)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.MaximumProposals, 2)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.MaximumProposals, 4)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.NearMaximumBytes, 2)]
    [InlineData(ReceiptPerformanceWorkload.FreshDetail, ReceiptPerformanceShape.NearMaximumBytes, 4)]
    public Task Independent_maximum_workspaces(ReceiptPerformanceWorkload workload, ReceiptPerformanceShape shape, int concurrency)
        => RunAsync(workload, shape, ReceiptPerformanceTopology.IndependentWorkspaces, concurrency);

    [Theory]
    [InlineData(ReceiptPerformanceShape.MaximumProposals)]
    [InlineData(ReceiptPerformanceShape.NearMaximumBytes)]
    public Task Four_distinct_runs_share_actual_workspace_coordination(ReceiptPerformanceShape shape)
        => RunAsync(ReceiptPerformanceWorkload.CoreSlice, shape, ReceiptPerformanceTopology.SharedWorkspace, 4);

    private async Task RunAsync(ReceiptPerformanceWorkload workload, ReceiptPerformanceShape shape,
        ReceiptPerformanceTopology topology, int concurrency) {
        var evidence = AgentReceiptStorePerformanceEvidence.Create(workload, shape, topology, concurrency);
        AgentReceiptStorePerformanceFixture? fixture = null;
        ReceiptPerformanceMemorySampler? wholeRow = null;
        var samples = new List<ReceiptPerformanceSample>();
        Exception? workloadFailure = null;
        var cleanupFailures = new List<Exception>();
        AgentReceiptStoreDiagnosticSession? diagnostic = null;
        Exception? diagnosticFailure = null;
        var workloadStarted = false;
        var setup = Stopwatch.StartNew();
        try {
            diagnostic = evidence.CreateDiagnosticSession();
            if (diagnostic is not null) {
                await diagnostic.BeginAsync();
                setup.Restart();
            }
            workloadStarted = true;
            wholeRow = new();
            var idle = wholeRow.Snapshot().First;
            evidence.Write("idle", idle);
            RequireReferenceHeadroom(idle);
            await RecordIdleWindowAsync(evidence);
            fixture = await AgentReceiptStorePerformanceFixture.CreateAsync(evidence.FixtureRoot, shape, topology, concurrency);
            setup.Stop();
            evidence.Write("prepared", new { elapsedMilliseconds = setup.Elapsed.TotalMilliseconds, shapes = fixture.Shapes,
                memory = wholeRow.Snapshot(), sourceGraphsRetained = false, ownedRoots = fixture.OwnedRoots,
                repetitionState = "same saved runs; each confirmed slice advances original lease/cursor/revisions, never reinitializes effect identity" });
            for (var ordinal = 0; ordinal < 10; ordinal++) {
                var kind = ordinal == 0 ? ReceiptPerformanceSampleKind.ColdOwner
                    : ordinal < 3 ? ReceiptPerformanceSampleKind.Warmup : ReceiptPerformanceSampleKind.Measured;
                var measured = await MeasureAsync(fixture, workload, concurrency, ordinal, kind);
                samples.Add(measured.Sample);
                evidence.Write("sample", measured.Sample);
                if (measured.Failure is not null) {
                    ExceptionDispatchInfo.Capture(measured.Failure).Throw();
                }
                RequireCompletedSample(measured.Sample, workload, shape, concurrency);
                RequireReferenceHeadroom(measured.Sample.Memory.First);
                if (concurrency == 4 && topology == ReceiptPerformanceTopology.IndependentWorkspaces &&
                    measured.Sample.Memory.MaximumObservedPrivateBytes - measured.Sample.Memory.First.PrivateBytes >
                        AgentReceiptStorePerformanceEvidence.IncrementalReferenceBytes) {
                    throw new InvalidOperationException("Observed operation-window incremental private bytes exceed the declared four-run reference envelope.");
                }
            }
            var whole = wholeRow.Snapshot();
            if (concurrency == 4 && topology == ReceiptPerformanceTopology.IndependentWorkspaces &&
                whole.MaximumObservedPrivateBytes - whole.First.PrivateBytes > AgentReceiptStorePerformanceEvidence.IncrementalReferenceBytes) {
                throw new InvalidOperationException("The whole-row reference envelope was exceeded, including setup; operation-window fit alone cannot qualify this profile. Attribute setup/transient retention before drawing an application-capacity conclusion.");
            }
        } catch (Exception exception) {
            if (workloadStarted) {
                workloadFailure = exception;
            } else {
                diagnosticFailure = exception;
            }
        } finally {
            var cleanupStarted = Stopwatch.GetTimestamp();
            if (fixture is not null) {
                try {
                    evidence.ValidateFixtureTree();
                    await fixture.DisposeAsync();
                } catch (Exception exception) {
                    cleanupFailures.Add(exception);
                }
            }
            try {
                evidence.VerifyAndRemoveEmptyFixtureParents();
            } catch (Exception exception) {
                cleanupFailures.Add(exception);
            }
            if (wholeRow is not null) {
                try {
                    await wholeRow.DisposeAsync();
                } catch (Exception exception) {
                    workloadFailure = workloadFailure is null ? exception : new AggregateException(workloadFailure, exception);
                }
            }
            var measured = samples.Where(sample => sample.Kind == ReceiptPerformanceSampleKind.Measured).ToArray();
            var elapsed = measured.Select(sample => sample.ElapsedMilliseconds).Order().ToArray();
            var allocated = measured.Select(sample => sample.ProcessAllocatedBytes).Order().ToArray();
            try {
                evidence.Write("complete", new {
                    workloadStarted, workloadPassed = workloadStarted && workloadFailure is null,
                    workloadFailureType = workloadFailure?.GetType().FullName,
                    cleanupPassed = cleanupFailures.Count == 0, cleanupFailureTypes = cleanupFailures.Select(item => item.GetType().FullName).ToArray(),
                    cleanupMilliseconds = Stopwatch.GetElapsedTime(cleanupStarted).TotalMilliseconds,
                    completedSamples = samples.Count, completedMeasuredSamples = measured.Length,
                    medianMeasuredMilliseconds = elapsed.Length == 7 ? elapsed[3] : (double?)null,
                    maximumMeasuredMilliseconds = elapsed.Length == 0 ? null : (double?)elapsed[^1],
                    medianMeasuredAllocatedBytes = allocated.Length == 7 ? allocated[3] : (long?)null,
                    maximumMeasuredAllocatedBytes = allocated.Length == 0 ? null : (long?)allocated[^1],
                    maximumOperationEntryRelativePrivateIncrementBytes = samples.Count == 0 ? null :
                        (long?)samples.Max(sample => sample.Memory.MaximumObservedPrivateBytes - sample.Memory.First.PrivateBytes),
                    maximumOperationObservedAbsolutePrivateBytes = samples.Count == 0 ? null :
                        (long?)samples.Max(sample => sample.Memory.MaximumObservedPrivateBytes),
                    wholeRowPreSetupRelativePrivateIncrementBytes = wholeRow is null ? null :
                        (long?)(wholeRow.Snapshot().MaximumObservedPrivateBytes - wholeRow.Snapshot().First.PrivateBytes),
                    wholeRowMemory = wholeRow?.Snapshot(), setupMilliseconds = setup.Elapsed.TotalMilliseconds,
                    physicalIoCollected = false, diagnosticPeakCorroborationRequired = true,
                    processStillOwnsParentRunnerDeadline = true
                });
            } catch (Exception exception) {
                cleanupFailures.Add(exception);
            }
            if (diagnostic is not null) {
                try {
                    await diagnostic.CompleteAsync();
                } catch (Exception exception) {
                    diagnosticFailure = diagnosticFailure is null ? exception : new AggregateException(diagnosticFailure, exception);
                }
            }
            if (diagnostic is not null || diagnosticFailure is not null) {
                try {
                    evidence.Write("diagnostic", new {
                        collectionAcknowledged = diagnosticFailure is null,
                        failureType = diagnosticFailure?.GetType().FullName,
                        independentOfWorkloadOutcome = true,
                        parserAcceptanceRequired = true
                    });
                } catch (Exception exception) {
                    diagnosticFailure = diagnosticFailure is null ? exception : new AggregateException(diagnosticFailure, exception);
                }
            }
        }
        output.WriteLine($"Receipt-store row recorded {samples.Count} planned samples; cleanup failures: {cleanupFailures.Count}.");
        if (diagnosticFailure is not null) {
            var failures = new List<Exception>();
            if (workloadFailure is not null) {
                failures.Add(workloadFailure);
            }
            failures.AddRange(cleanupFailures);
            failures.Add(diagnosticFailure);
            throw new AggregateException("Diagnostic collection failed; original workload and cleanup outcomes are retained separately.", failures);
        }
        if (cleanupFailures.Count != 0) {
            throw new AggregateException("The performance row requires review of its preserved workload/cleanup evidence.",
                workloadFailure is null ? cleanupFailures : new[] { workloadFailure }.Concat(cleanupFailures));
        }
        if (workloadFailure is not null) {
            ExceptionDispatchInfo.Capture(workloadFailure).Throw();
        }
    }

    private static async Task<(ReceiptPerformanceSample Sample, Exception? Failure)> MeasureAsync(
        AgentReceiptStorePerformanceFixture fixture, ReceiptPerformanceWorkload workload, int concurrency,
        int ordinal, ReceiptPerformanceSampleKind kind) {
        using var probe = new AgentReceiptStorePerformanceProbe(concurrency);
        var memory = new ReceiptPerformanceMemorySampler();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        var workers = Enumerable.Range(0, concurrency).Select(worker => Task.Run(async () => {
            if (Interlocked.Increment(ref arrivals) == concurrency) {
                ready.SetResult();
            }
            await start.Task;
            return await probe.MeasureWorkerAsync(worker, () => fixture.ExecuteAsync(worker, workload, probe));
        })).ToArray();
        await ready.Task;
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        start.SetResult();
        var failures = new List<Exception>();
        try {
            await Task.WhenAll(workers);
        } catch {
            foreach (var worker in workers) {
                if (worker.Exception is { } failure) {
                    failures.AddRange(failure.Flatten().InnerExceptions);
                } else if (worker.IsCanceled) {
                    failures.Add(new TaskCanceledException(worker));
                }
            }
        }
        var milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var delta = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        try {
            await memory.DisposeAsync();
        } catch (Exception exception) {
            failures.Add(exception);
        }
        var cost = probe.Snapshot();
        var sample = new ReceiptPerformanceSample(ordinal, kind, milliseconds, delta,
            probe.ObservedOperations,
            cost, memory.Snapshot(), failures.Select(exception => exception.GetType().FullName ?? exception.GetType().Name).ToArray());
        return (sample, failures.Count == 0 ? null : new AggregateException("The measured sample failed; it was not retried.", failures));
    }

    private static void RequireCompletedSample(ReceiptPerformanceSample sample, ReceiptPerformanceWorkload workload,
        ReceiptPerformanceShape shape, int concurrency) {
        Assert.Empty(sample.FailureTypes);
        Assert.False(sample.Store.EvidenceOverflow, "The bounded evidence inventory overflowed; this sample is incomplete.");
        Assert.Equal(concurrency, sample.Store.MaximumOverlappingWorkers);
        Assert.Equal(concurrency, sample.Results.Length);
        Assert.True(sample.Store.CurrentReadChecks > 0);
        Assert.All(sample.Results, result => {
            Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
            Assert.True(result.ResponseBytes > 0);
        });
        if (workload == ReceiptPerformanceWorkload.CoreSlice) {
            var expected = shape == ReceiptPerformanceShape.OneProposal ? 1 : 4;
            Assert.All(sample.Results, result => {
                Assert.Equal(expected, result.Examined);
                Assert.Contains(result.Stop, new[] { AgentEffectReconciliationStop.ExhaustedSnapshot, AgentEffectReconciliationStop.LookupLimit });
                Assert.InRange(result.ResponseBytes, 1, AgentEffectContinuation.MaximumUtf8Bytes);
            });
            Assert.Equal(expected * concurrency, sample.Store.OwnerLookups);
            Assert.Equal(2 * concurrency, sample.Store.DurableIntents);
        } else {
            Assert.All(sample.Results, result => Assert.Equal(AgentEffectReconciliationStop.SnapshotRead, result.Stop));
            Assert.Equal(0, sample.Store.OwnerLookups);
            Assert.Equal(0, sample.Store.DurableIntents);
        }
    }

    private static void RequireReferenceHeadroom(ReceiptPerformanceMemory observation) {
        if (observation.PrivateBytes > AgentReceiptStorePerformanceEvidence.IdleReserveBytes ||
            observation.PhysicalAvailableBytes < 2 * AgentReceiptStorePerformanceEvidence.IncrementalReferenceBytes ||
            observation.LastGcTotalAvailableMemoryBytes < AgentReceiptStorePerformanceEvidence.ApplicationEnvelopeBytes ||
            observation.WindowsProcessAvailableCommitBytes is { } commit &&
                commit < 2 * AgentReceiptStorePerformanceEvidence.IncrementalReferenceBytes) {
            throw new InvalidOperationException("The actual pre-operation idle/headroom observation does not meet the declared reference profile; this is not an application-capacity result.");
        }
    }

    private static async Task RecordIdleWindowAsync(AgentReceiptStorePerformanceEvidence evidence) {
        using var probe = new AgentReceiptStorePerformanceProbe(1);
        var memory = new ReceiptPerformanceMemorySampler();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        try {
            await Task.Delay(TimeSpan.FromSeconds(1));
        } finally {
            await memory.DisposeAsync();
            evidence.Write("idle-window", new { elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                processAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocated, memory = memory.Snapshot(),
                subtractedFromSamples = false, scope = "one whole-row and one cohort sampler plus empty activity listener" });
        }
    }
}
