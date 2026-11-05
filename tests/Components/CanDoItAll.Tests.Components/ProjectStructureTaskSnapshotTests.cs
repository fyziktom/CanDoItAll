using System.Data;
using System.Data.Common;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTaskSnapshotTests {
    [Fact]
    public async Task A_real_peer_Task_and_assignment_commit_cannot_produce_a_mixed_current_snapshot() {
        var barrier = new NodeReadBarrier();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(barrier), ServiceLifetime.Singleton));
        var services = harness.Context.Services;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Coherent native Task snapshot" })).Value;
        var admission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission!;
        var owner = new ProjectStructureAgentContext("component-tests", "Coherent Task snapshot", Environment.MachineName,
            AppContext.BaseDirectory, "", "coherent-task-snapshot") { ExpectedProjectAdmission = admission };
        var person = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new PartyEditorModel {
            PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active, DisplayName = "Snapshot peer assignee", LastChangedBy = "component-tests"
        });
        Assert.True(person.IsSuccess);
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var created = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(project,
            new("Original snapshot Task", start, start.AddHours(8), Estimate: new(8, ProjectWorkItemEffortUnit.Hours, null, "")) {
                ExpectedProjectAdmission = admission
            }, owner);
        var original = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == created.TaskNodeId);
        var reader = services.GetRequiredService<ProjectStructureTaskSnapshotReader>();
        barrier.NodeId = original.Id;
        barrier.Armed = true;
        var reading = reader.ReadAsync(admission, original.Id, original.RecordId!.Value, null, CancellationToken.None);
        try {
            await barrier.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await using var peerScope = services.CreateAsyncScope();
            var peer = peerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            Assert.NotNull(await peer.UpdateObjectAsync(project, original.Id,
                new("Peer snapshot Task", original.Subtitle, original.Notes, original.StartUtc, original.EndUtc,
                    original.MetadataJson, original.DurationSeconds, original.NodeReferences) { ExpectedProjectAdmission = admission, ExpectedNode = original })
                .WaitAsync(TimeSpan.FromSeconds(10)));
            await peerScope.ServiceProvider.GetRequiredService<ProjectStructureWorkItemAssigneeService>().ReplaceAsync(project, original.Id,
                new(ProjectStructureTaskResourceKind.Person, person.Value), "snapshot-peer", mutationOwner: owner).WaitAsync(TimeSpan.FromSeconds(10));
        } finally {
            barrier.Release.TrySetResult();
        }
        var snapshot = await reading.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(IsolationLevel.RepeatableRead, barrier.Isolation);
        Assert.Equal(original.Title, snapshot.Task.Title);
        Assert.Equal(0, snapshot.State.DirectAssignmentRevision);
        Assert.Empty(snapshot.Assignees.DirectAssignments);
        var current = await reader.ReadAsync(admission, original.Id, original.RecordId.Value, null, CancellationToken.None);
        Assert.Equal("Peer snapshot Task", current.Task.Title);
        Assert.Equal(1, current.State.DirectAssignmentRevision);
        Assert.Equal(person.Value, Assert.Single(current.Assignees.DirectAssignments).PartyId);
    }

    private sealed class NodeReadBarrier : DbCommandInterceptor {
        public string NodeId { get; set; } = string.Empty;
        public bool Armed { get; set; }
        public IsolationLevel? Isolation { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("\"Workbench_ProjectObjects\"", StringComparison.Ordinal) &&
                command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string id && id == NodeId)) {
                Armed = false;
                Isolation = command.Transaction?.IsolationLevel;
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
