using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.ProjectStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanCurrentnessTests {
    [Fact]
    public async Task Empty_original_set_detects_native_task_insert_before_owner_exclusion() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        await RefuseAfterAsync(fixture, command, async () => { await CreateTaskAsync(fixture); });
        await fixture.VerifyAsync(1, 0);
    }

    [Fact]
    public async Task Actual_held_native_writer_commit_is_visible_after_project_advisory_wait() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var command = await fixture.CommandAsync();
        var flushed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        fixture.Faults.Save.AfterFlush = async token => {
            flushed.TrySetResult(fixture.Probe.ProjectWaiterPid);
            await release.Task.WaitAsync(token);
        };
        var edit = EditAsync(fixture, node, title: "Concurrent accepted edit", cancellationToken: deadline.Token);
        Task<ProjectTaskPlanOutcome>? publishing = null;
        try {
            var holderPid = await flushed.Task.WaitAsync(deadline.Token);
            Assert.True(holderPid > 0);
            fixture.Probe.Reset();
            publishing = fixture.Publisher().PublishAsync(command, fixture.Invocation, deadline.Token);
            await fixture.WaitForProjectWaiterAsync(holderPid, deadline.Token);
        } finally {
            release.TrySetResult();
            await edit;
        }
        Assert.Equal(ProjectTaskPlanRefusal.BasisChanged, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await publishing!).Reason);
        await fixture.VerifyAsync(1, 0);
        Assert.Equal("Concurrent accepted edit", (await NodeAsync(fixture, node.Id)).Title);
    }

    [Fact]
    public async Task Native_status_progress_and_priority_each_invalidate_the_original_material_set() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var owner = Owner(fixture);
        foreach (var operation in new[] { 0, 1, 2 }) {
            var command = await fixture.CommandAsync();
            var state = await ProjectStructureTestOrigin.ReadStateAsync(owner, fixture.Project.ProjectId, node.Id);
            await RefuseAfterAsync(fixture, command, async () => {
                switch (operation) {
                    case 0:
                        await owner.UpdateObjectStatusesDetailedAsync(fixture.Project.ProjectId, [node.Id], "Active",
                            expectedProjectAdmission: fixture.Project, expectedStates: [state]);
                        break;
                    case 1:
                        await owner.UpdateObjectProgressDetailedAsync(fixture.Project.ProjectId, [node.Id], "progress", 67,
                            expectedProjectAdmission: fixture.Project, expectedStates: [state]);
                        break;
                    case 2:
                        await owner.UpdateObjectPriorityDetailedAsync(fixture.Project.ProjectId, [node.Id], 6,
                            expectedProjectAdmission: fixture.Project, expectedStates: [state]);
                        break;
                }
            });
        }
        await fixture.VerifyAsync(1, 0);
    }

    [Fact]
    public async Task Lifecycle_task_exit_and_entry_change_the_complete_set_even_with_same_node_occurrence() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        foreach (var subtype in new[] { "issue", ProjectObjectSubtypePolicy.Task }) {
            var current = await NodeAsync(fixture, node.Id);
            var command = await fixture.CommandAsync();
            await RefuseAfterAsync(fixture, command, async () => {
                var changed = await Owner(fixture).ReclassifyObjectAsync(fixture.Project.ProjectId, node.Id,
                    new(ProjectObjectType.WorkItem, subtype, current.Title, current.Subtitle, current.Notes, current.MetadataJson) {
                        ExpectedNode = current, ExpectedProjectAdmission = fixture.Project
                    });
                Assert.NotNull(changed);
                Assert.Equal(subtype, changed.ObjectSubtype);
            });
        }
        await fixture.VerifyAsync(1, 0);
    }

    [Fact]
    public async Task Timing_and_compensated_execution_fields_are_material_even_when_pricing_is_excluded() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var command = await fixture.CommandAsync();
        await RefuseAfterAsync(fixture, command, async () => {
            await EditAsync(fixture, node, start: command.Basis.CapturedAtUtc.AddDays(1));
        });
        node = await NodeAsync(fixture, node.Id);
        command = await fixture.CommandAsync();
        var current = ProjectStructureTaskEditStatePolicy.Read(node);
        var restored = current with { Execution = new(ProjectTaskExecutionState.Started, command.Basis.CapturedAtUtc, null) };
        var origin = await ProjectStructureTestOrigin.ReadOwnerAsync(Owner(fixture), fixture.Project.ProjectId);
        await RefuseAfterAsync(fixture, command, async () => {
            await fixture.Services.GetRequiredService<ProjectStructureTaskEditCompensationService>().RestorePricingAsync(
                fixture.Project.ProjectId, node.Id, current, restored, mutationOwner: origin);
        });
        await fixture.VerifyAsync(1, 0);
    }

    [Fact]
    public async Task Native_deletion_removes_an_occurrence_and_refuses_stale_append() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var command = await fixture.CommandAsync();
        var origin = await ProjectStructureTestOrigin.ReadOwnerAsync(Owner(fixture), fixture.Project.ProjectId);
        await RefuseAfterAsync(fixture, command, async () => {
            await Owner(fixture).DeleteObjectDetailedAsync(fixture.Project.ProjectId, node.Id,
                ProjectStructureManagedStorageDisposition.RetainManagedFiles, mutationOwner: origin);
        });
        await fixture.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Reparented_context_is_material_even_though_publisher_only_appends_flat_tasks() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var parent = await Owner(fixture).CreateObjectAsync(fixture.Project.ProjectId,
            new(ProjectObjectType.ProjectBlock, "New parent", "", "", $"project:{fixture.Project.ProjectId:D}") {
                ExpectedProjectAdmission = fixture.Project
            });
        var command = await fixture.CommandAsync();
        var origin = await ProjectStructureTestOrigin.ReadOwnerAsync(Owner(fixture), fixture.Project.ProjectId);
        await RefuseAfterAsync(fixture, command, async () => {
            Assert.NotNull(await Owner(fixture).ReparentObjectAsync(fixture.Project.ProjectId, node.Id, parent.Id, mutationOwner: origin));
        });
        Assert.Equal(parent.Id, (await NodeAsync(fixture, node.Id)).ParentId);
        await using var read = await fixture.ContextAsync();
        Assert.Empty(await read.Set<ProjectTaskPlanReceiptRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task Actual_clipboard_copy_adds_new_occurrences_to_the_complete_task_set() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var command = await fixture.CommandAsync();
        var origin = await ProjectStructureTestOrigin.ReadOwnerAsync(Owner(fixture), fixture.Project.ProjectId);
        await RefuseAfterAsync(fixture, command, async () => {
            await Owner(fixture).CopySubtreesAsync(fixture.Project.ProjectId, [node.Id], $"project:{fixture.Project.ProjectId:D}", mutationOwner: origin);
        });
        await fixture.VerifyAsync(2, 0);
    }

    [Fact]
    public async Task Native_cross_project_move_changes_both_original_source_and_destination_bases() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var sourceCommand = await fixture.CommandAsync();
        await using var projects = await fixture.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var target = new Project { Name = "Move target", Slug = Guid.NewGuid().ToString("N") };
        projects.Add(target);
        await projects.SaveChangesAsync();
        var targetAdmission = (await fixture.Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(target.Id))!;
        var targetBasis = Assert.IsType<ProjectTaskPlanBasisReadResult.Captured>(await fixture.Capture.CaptureAsync(targetAdmission,
            (_, _) => Task.CompletedTask)).Basis;
        var targetCommand = new ProjectTaskPlanCommand(new(sourceCommand.Operation.Namespace, sourceCommand.Operation.Epoch,
            Guid.NewGuid(), sourceCommand.Operation.CallerBinding), targetBasis, sourceCommand.Proposal, sourceCommand.Policy, sourceCommand.Source);
        var origin = await ProjectStructureTestOrigin.ReadTransferOwnerAsync(Owner(fixture), fixture.Project.ProjectId, target.Id);
        await RefuseAfterAsync(fixture, sourceCommand, async () => {
            Assert.NotNull(await Owner(fixture).MoveNodesToProjectAsync(fixture.Project.ProjectId, [node.Id], target.Id, mutationOwner: origin));
        });
        fixture.Authority.BeforeAcquire = null;
        Assert.Equal(ProjectTaskPlanRefusal.BasisChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(targetCommand, fixture.Invocation)).Reason);
        await using var read = await fixture.ContextAsync();
        Assert.Empty(await read.Set<ProjectTaskPlanReceiptRecord>().ToArrayAsync());
        Assert.Equal(target.Id, (await read.Set<ProjectObjectRecord>().SingleAsync(row => row.NodeKey == node.Id)).ProjectId);
    }

    [Fact]
    public async Task Project_scalar_definition_revision_is_part_of_the_original_basis() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        var projects = fixture.Services.GetRequiredService<ProjectsService>();
        var editor = await projects.GetAsync(fixture.Project.ProjectId);
        editor.Name = "Accepted current definition";
        await RefuseAfterAsync(fixture, command, async () => {
            Assert.True((await projects.SaveAsync(editor)).IsSuccess);
        });
        fixture.Authority.BeforeAcquire = null;
        var beforeDeletion = await fixture.CommandAsync();
        await projects.DeleteAsync(fixture.Project.ProjectId, expectedProjectAdmission: fixture.Project);
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(beforeDeletion, fixture.Invocation)).Reason);
        await fixture.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Same_occurrence_exact_value_restoration_is_current_and_does_not_claim_change_history_equality() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var command = await fixture.CommandAsync(1);
        await EditAsync(fixture, node, title: "Intermediate title");
        await EditAsync(fixture, await NodeAsync(fixture, node.Id), title: node.Title);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        await fixture.VerifyAsync(2, 1, accepted.Acknowledgement);
        Assert.Equal(node.RecordId, (await NodeAsync(fixture, node.Id)).RecordId);
    }

    [Fact]
    public async Task Excluded_layout_and_estimate_edits_do_not_create_false_material_conflicts() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var node = await CreateTaskAsync(fixture);
        var command = await fixture.CommandAsync(1);
        await Owner(fixture).MoveObjectsAsync(fixture.Project.ProjectId, [new(node.Id, 2200, 1800)],
            expectedProjectAdmission: fixture.Project, expectedNodes: [node]);
        node = await NodeAsync(fixture, node.Id);
        var current = ProjectStructureTaskEditStatePolicy.Read(node);
        var origin = await ProjectStructureTestOrigin.ReadOwnerAsync(Owner(fixture), fixture.Project.ProjectId);
        await fixture.Services.GetRequiredService<ProjectStructureTaskEditCompensationService>().RestorePricingAsync(
            fixture.Project.ProjectId, node.Id, current, current with {
                Estimate = new(7, ProjectWorkItemEffortUnit.Hours, null, "")
            }, mutationOwner: origin);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        await fixture.VerifyAsync(2, 1, accepted.Acknowledgement);
    }

    private static ProjectWorkbenchService Owner(ProjectTaskPlanOperationFixture fixture)
        => fixture.Services.GetRequiredService<ProjectWorkbenchService>();

    private static Task<ProjectStructureNode> CreateTaskAsync(ProjectTaskPlanOperationFixture fixture)
        => Owner(fixture).CreateObjectAsync(fixture.Project.ProjectId,
            new(ProjectObjectType.WorkItem, "Existing task", "Task", "", $"project:{fixture.Project.ProjectId:D}",
                ObjectSubtype: ProjectObjectSubtypePolicy.Task,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                    WorkItem = new() { WorkItemKind = ProjectWorkItemKind.Task, Description = "Original", ExecutionState = ProjectTaskExecutionState.NotStarted }
                })) { ExpectedProjectAdmission = fixture.Project });

    private static async Task<ProjectStructureNode> NodeAsync(ProjectTaskPlanOperationFixture fixture, string id)
        => Assert.Single((await Owner(fixture).GetStructureAsync(fixture.Project.ProjectId)).Nodes, node => node.Id == id);

    private static Task<ProjectStructureNode?> EditAsync(ProjectTaskPlanOperationFixture fixture, ProjectStructureNode node,
        string? title = null, DateTimeOffset? start = null, CancellationToken cancellationToken = default)
        => Owner(fixture).UpdateObjectAsync(fixture.Project.ProjectId, node.Id,
            new(title ?? node.Title, node.Subtitle, node.Notes, start ?? node.StartUtc, node.EndUtc, node.MetadataJson) {
                ExpectedNode = node, ExpectedProjectAdmission = fixture.Project
            }, cancellationToken);

    private static async Task RefuseAfterAsync(ProjectTaskPlanOperationFixture fixture, ProjectTaskPlanCommand command, Func<Task> mutation) {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        fixture.Authority.BeforeAcquire = async token => {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        var publishing = fixture.Publisher().PublishAsync(command, fixture.Invocation, deadline.Token);
        try {
            await entered.Task.WaitAsync(deadline.Token);
            await mutation();
        } finally {
            release.TrySetResult();
        }
        var rejected = Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await publishing);
        Assert.Equal(ProjectTaskPlanRefusal.BasisChanged, rejected.Reason);
        Assert.True(rejected.RequiresReview);
        await using var read = await fixture.ContextAsync();
        Assert.Empty(await read.Set<ProjectTaskPlanReceiptRecord>().ToArrayAsync());
    }
}
