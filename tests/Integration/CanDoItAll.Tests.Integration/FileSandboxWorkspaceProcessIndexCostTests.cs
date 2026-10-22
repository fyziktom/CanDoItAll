using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspaceProcessIndexCostTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2000, 1)]
    [InlineData(2000, 8)]
    [InlineData(2000, 32)]
    public async Task Actual_owner_writes_and_concurrent_pages_preserve_each_accepted_revision(int historyCount, int writers) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var token = deadline.Token;
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var process = Guid.NewGuid();
        ExecutionRunRecord Run(Guid owner) => fixture.Detail.Run with {
            Id = Guid.NewGuid(), ChatSessionId = null, ToolAdmission = null, Title = "Cost sample", SourceKind = "process-step", SourceId = "cost-proof",
            ProcessRunId = owner.ToString("D"), ProcessStepId = Guid.NewGuid().ToString("D")
        };
        var selected = Enumerable.Range(0, writers).Select(_ => Run(process)).ToArray();
        var history = Enumerable.Range(0, historyCount).Select(_ => Run(Guid.NewGuid())).ToArray();
        await fixture.Store.UpdateExecutionAsync(state => state with { ExecutionRuns = [.. state.ExecutionRuns, .. selected, .. history] }, token);
        var accepted = new int[writers];
        var stores = Enumerable.Range(0, writers).Select(_ => fixture.NewStore()).ToArray();
        async Task Write(int owner) {
            await stores[owner].UpdateExecutionRunDetailAsync(selected[owner].Id, current => current with {
                Run = current.Run with { Revision = checked(current.Run.Revision + 1), UpdatedAtUtc = current.Run.UpdatedAtUtc.AddTicks(1) }
            }, token);
            accepted[owner]++;
        }
        for (var warmup = 0; warmup < 20; warmup++) {
            await Write(warmup % writers);
        }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new ConcurrentBag<double>();
        var reads = new ConcurrentBag<double>();
        var conflicts = new ConcurrentBag<double>();
        var physical = new List<FileSandboxWorkspacePhysicalJsonRead>();
        var reader = new FileSandboxWorkspaceStore(fixture.WorkspaceRoot, fixture.StorageScope, null, null,
            new FileSandboxWorkspaceJsonReadDiagnostics(physical.Add));
        var next = -1;
        var tasks = Enumerable.Range(0, writers).Select(async owner => {
            await start.Task.WaitAsync(token);
            while (Interlocked.Increment(ref next) < 100) {
                var watch = Stopwatch.StartNew();
                try {
                    await Write(owner);
                    writes.Add(watch.Elapsed.TotalMilliseconds);
                } catch (SandboxWorkspacePublicationConflictException) {
                    conflicts.Add(watch.Elapsed.TotalMilliseconds);
                }
            }
        }).Append(ReadPages()).ToArray();
        async Task ReadPages() {
            await start.Task.WaitAsync(token);
            for (var operation = 0; operation < 100; operation++) {
                var watch = Stopwatch.StartNew();
                var page = await reader.ReadProcessExecutionRunPageAsync(new(process, Take: 1), token);
                reads.Add(watch.Elapsed.TotalMilliseconds);
                Assert.Equal(ProcessExecutionRunPageStatus.Ready, page.Status);
                Assert.Equal(process, Guid.Parse(Assert.Single(page.Candidates).ProcessRunId));
            }
        }
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var elapsed = Stopwatch.StartNew();
        start.SetResult();
        await Task.WhenAll(tasks).WaitAsync(token);
        elapsed.Stop();
        allocated = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        Assert.Equal(100, writes.Count + conflicts.Count);
        Assert.Equal(100, reads.Count);
        for (var owner = 0; owner < writers; owner++) {
            var run = (await fixture.NewStore().GetExecutionRunAsync(selected[owner].Id, token))!;
            Assert.Equal(selected[owner].Revision + accepted[owner], run.Revision);
        }
        output.WriteLine(JsonSerializer.Serialize(new {
            historyCount, writers, warmupWrites = 20, attemptedWrites = 100, acceptedWrites = writes.Count,
            conflictingWrites = conflicts.Count, pageReads = reads.Count, elapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds,
            acceptedWritesPerSecond = writes.Count / elapsed.Elapsed.TotalSeconds, allocatedBytes = allocated,
            physicalReadBytes = physical.Sum(read => read.LengthBytes),
            journalReads = physical.Count(read => read.PayloadType == typeof(ExecutionRunRecord)),
            writeMilliseconds = Distribution(writes), readMilliseconds = Distribution(reads), conflictMilliseconds = Distribution(conflicts),
            scope = "Actual file-owner records and single-attempt concurrent writes/reads; one diagnostic sample; filesystem cache uncontrolled; no queue-wait or disk-flush attribution."
        }));
    }

    private static object Distribution(IEnumerable<double> values) {
        var sorted = values.Order().ToArray();
        double? At(double percentile) => sorted.Length == 0 ? null : sorted[Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length) - 1)];
        return new { p50 = At(0.5), p95 = At(0.95), p99 = At(0.99) };
    }
}
