using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowProviderDisclosureInMemoryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_supported_memory_stores_detach_ordered_private_evidence_and_keep_public_events_unchanged(bool efProvider) {
        var store = Store(efProvider);
        var (run, started, completed) = Records();
        var mutable = completed.ProviderReadEvidence.ToList();
        completed = completed with { ProviderReadEvidence = mutable };
        var expected = JsonSerializer.Serialize(mutable[0]);
        await store.CreateRunWithStartedEventAsync(run, started);
        await store.SaveEventAsync(completed);
        mutable[0] = mutable[0] with { PayloadJson = "{\"changed\":true}" };
        var history = await store.ReadProviderDisclosureAsync(run.RunId);
        Assert.Equal(expected, JsonSerializer.Serialize(Assert.Single(Assert.Single(history.Completions).Evidence)));
        Assert.Equal("future.owner", history.Completions[0].Evidence[0].Owner.Value);
        Assert.Equal(99, history.Completions[0].Evidence[0].SchemaVersion);
        var visible = await store.ListEventsAsync(run.RunId);
        Assert.Equal(2, visible.Count);
        Assert.Equal(new[] { started.PayloadJson, completed.PayloadJson }, visible.Select(value => value.PayloadJson));
        Assert.All(visible, value => {
            Assert.Null(value.DisclosureDeclaration);
            Assert.Null(value.CompletionProof);
            Assert.Empty(value.ProviderReadEvidence);
        });
        var page = await store.ListEventPageAsync(new(run.RunId, PageSize: 1));
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        await Assert.ThrowsAsync<WorkflowRunAlreadyExistsException>(() => store.CreateRunWithStartedEventAsync(run, started));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Alternate_transition_writer_cannot_strip_or_bypass_atomic_disclosure_writer(bool efProvider) {
        var store = Store(efProvider);
        var (run, started, completed) = Records();
        await store.CreateRunWithStartedEventAsync(run, started);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.TryTransitionRunAsync(run.RunId,
            [WorkflowRunState.Running], run with { State = WorkflowRunState.Completed }, completed));
        Assert.Equal(WorkflowRunState.Running, (await store.GetRunAsync(run.RunId))!.State);
        Assert.Single(await store.ListEventsAsync(run.RunId));
        Assert.Empty((await store.ReadProviderDisclosureAsync(run.RunId)).Completions);
        await store.SaveEventAsync(completed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveEventAsync(WorkflowProviderDisclosureJournal.PublicEvent(completed)));
        Assert.Single((await store.ReadProviderDisclosureAsync(run.RunId)).Completions);
    }

    private static IWorkflowRunStore Store(bool efProvider) => efProvider
        ? new PersistentWorkflowRunStore(new OwnerFactory(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"workflow-disclosure-{Guid.NewGuid():N}").Options))
        : new InMemoryWorkflowRunStore();

    private static (WorkflowRunSnapshot Run, WorkflowEventRecord Started, WorkflowEventRecord Completed) Records() {
        var now = new DateTimeOffset(2026, 9, 11, 1, 2, 3, TimeSpan.Zero);
        var run = new WorkflowRunSnapshot(WorkflowRunId.New(), WorkflowId.New(), WorkflowVersionId.New(), WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess, "memory", "Running", now, now);
        var hash = WorkflowExecutionContentHash.Compute("exact");
        var source = WorkflowProviderDisclosureContent.Source(run.Origin);
        var declaration = new WorkflowRunDisclosureDeclaration(run.RunId, run.WorkflowId, run.VersionId, hash, source, WorkflowProviderDisclosureProtocol.Current);
        var started = new WorkflowEventRecord(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "Started", "{}", now) {
            DisclosureDeclaration = declaration
        };
        var node = new WorkflowNodeId("read");
        var occurrence = WorkflowExecutionOccurrence.Start(run.RunId).Advance(run.VersionId, node);
        var proof = new WorkflowNodeCompletionProof(Guid.NewGuid(), occurrence, run.WorkflowId, run.VersionId, node, null,
            hash, hash, hash, hash, source, WorkflowProviderDisclosureProtocol.Current);
        var completed = new WorkflowEventRecord(Guid.NewGuid(), run.RunId, WorkflowEventKind.ExecutorCompleted, node, "Done", "{}", now.AddSeconds(1)) {
            CompletionProof = proof,
            ProviderReadEvidence = [new(new("future.owner"), 99, occurrence, run.VersionId, node, "{\"retained\":true}")]
        };
        return (run, started, completed);
    }

    private sealed class OwnerFactory(DbContextOptions<WorkflowDbContext> options) : IDbContextFactory<WorkflowDbContext> {
        public WorkflowDbContext CreateDbContext() => new(options);
        public Task<WorkflowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
