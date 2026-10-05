using System.Reflection;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTransferLifetimeTests {
    public enum Completion { Success, LostCreationReply, Compensated, PartialCommit }

    [Theory]
    [InlineData(Completion.Success)]
    [InlineData(Completion.LostCreationReply)]
    [InlineData(Completion.Compensated)]
    [InlineData(Completion.PartialCommit)]
    public async Task Transfer_retains_exact_native_effect_and_receipt_after_another_dialog_opens(Completion completion) {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ProjectCreationReceipt? creation = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            if (completion == Completion.PartialCommit) {
                services.Replace(ServiceDescriptor.Scoped<IProjectPartyIntegrationBridge>(provider => {
                    var bridge = DispatchProxy.Create<IProjectPartyIntegrationBridge, ReconciliationFailure>();
                    ((ReconciliationFailure)(object)bridge).Inner = provider.GetRequiredService<ProjectPartyIntegrationService>();
                    return bridge;
                }));
            }
            services.AddScoped(provider => {
                var projects = provider.GetRequiredService<ProjectsService>();
                var workbench = provider.GetRequiredService<ProjectWorkbenchService>();
                return new ProjectStructureSubprojectTransferCoordinator(new ProjectStructureSubprojectTransferOperations(
                    async (parent, target, reservation, editor, token, authority) => {
                        Assert.NotNull(reservation);
                        var accepted = await projects.CreateWithReceiptAsync(reservation, editor, token, authority);
                        Assert.True(accepted.IsSuccess);
                        creation = accepted.Value;
                        entered.TrySetResult();
                        await release.Task.WaitAsync(token);
                        if (completion == Completion.LostCreationReply) {
                            throw new InvalidOperationException("Synthetic loss of the original creation reply.");
                        }
                        return accepted;
                    }, workbench.MoveDescendantsToProjectAsync, workbench.MoveNodesToProjectAsync, projects.TryCompensateCreationAsync));
            });
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var saved = await projects.SaveAsync(new() { Name = "Transfer source" });
        Assert.True(saved.IsSuccess);
        var source = saved.Value;
        var first = await CreateNoteAsync(workbench, source, "Original anchor", $"project:{source:D}");
        var child = await CreateNoteAsync(workbench, source, "Original descendant", first.Id);
        var next = await CreateNoteAsync(workbench, source, "Successor anchor", $"project:{source:D}");
        var neighbor = await CreateNoteAsync(workbench, source, "Untouched neighbor", next.Id);
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, source));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await OpenAsync(page, first.Id);
        var originalOpening = page.FindComponent<ProjectStructureCanvasDialogs>().Instance.SubprojectTransferDialog!.OpeningId;
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-subproject-transfer-name']").Input("Original extraction"));
        var pending = page.InvokeAsync(() => page.FindComponent<ProjectStructureCanvasDialogs>().Instance.ExecuteSubprojectTransfer.InvokeAsync());
        Guid successorOpening;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.NotNull(creation);
            if (completion == Completion.Compensated) {
                Assert.Equal(1, await workbench.DeleteObjectAsync(source, child.Id));
            }
            await page.InvokeAsync(() => page.FindComponent<ProjectStructureCanvasDialogs>().Instance.CloseSubprojectTransfer.InvokeAsync());
            await OpenAsync(page, next.Id);
            await page.InvokeAsync(() => page.Find("[data-testid='project-structure-subproject-transfer-name']").Input("Unsubmitted successor"));
            successorOpening = page.FindComponent<ProjectStructureCanvasDialogs>().Instance.SubprojectTransferDialog!.OpeningId;
        } finally {
            release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));

        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(originalOpening, outcome.OpeningId);
        Assert.Equal(creation!.Project.ProjectId, outcome.TargetProjectId);
        var successor = page.FindComponent<ProjectStructureCanvasDialogs>().Instance.SubprojectTransferDialog!;
        Assert.Equal(successorOpening, successor.OpeningId);
        Assert.Equal("Unsubmitted successor", successor.ProjectName);
        Assert.Empty(successor.Error);
        Assert.False(successor.IsBusy);
        var sourceAfter = await workbench.GetStructureAsync(source);
        var unchanged = Assert.Single(sourceAfter.Nodes, node => node.Id == neighbor.Id);
        Assert.Equal((neighbor.ParentId, neighbor.Title, neighbor.Notes, neighbor.MetadataJson, neighbor.X, neighbor.Y),
            (unchanged.ParentId, unchanged.Title, unchanged.Notes, unchanged.MetadataJson, unchanged.X, unchanged.Y));
        Assert.Contains(sourceAfter.Nodes, node => node.Id == first.Id);
        if (completion == Completion.Compensated) {
            Assert.Equal(ProjectStructureAuthoringResultKind.Compensated, outcome.Kind);
            Assert.Equal(creation, outcome.Creation);
            Assert.Equal(creation.Project.ProjectId, Assert.IsType<ProjectStructureCompensatedSubprojectTransferException>(outcome.Failure).RemovedProjectId);
            Assert.DoesNotContain(await projects.ListAsync(), project => project.Id == creation.Project.ProjectId);
        } else if (completion == Completion.LostCreationReply) {
            Assert.Equal(ProjectStructureAuthoringResultKind.Unconfirmed, outcome.Kind);
            Assert.False(Assert.IsType<ProjectCreationPartialCompletion>(Assert.IsType<ProjectStructureAgentException>(outcome.Failure).Details).CreationObserved);
            Assert.Contains(sourceAfter.Nodes, node => node.Id == child.Id);
            Assert.Contains(await projects.ListAsync(), project => project.Id == creation.Project.ProjectId);
        } else {
            Assert.Equal(creation, outcome.Creation);
            var targetAfter = await workbench.GetStructureAsync(creation.Project.ProjectId);
            Assert.Equal($"project:{creation.Project.ProjectId:D}", Assert.Single(targetAfter.Nodes, node => node.Id == child.Id).ParentId);
            Assert.DoesNotContain(sourceAfter.Nodes, node => node.Id == child.Id);
            Assert.Contains(await projects.ListHierarchyLinksAsync(), link => link.ParentProjectId == source && link.ChildProjectId == creation.Project.ProjectId);
            if (completion == Completion.PartialCommit) {
                Assert.Equal(ProjectStructureAuthoringResultKind.PartialCommit, outcome.Kind);
                var recovery = Assert.IsType<ProjectStructureTransferPartialCommitException>(outcome.Failure).Recovery;
                Assert.Equal(creation.Project.ProjectId, recovery.TargetProjectId);
                Assert.Equal(ProjectStructureTransferCommitState.WorkbenchCommitted, recovery.CommitState);
                await using var database = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
                var durable = await database.Set<ProjectCrossModuleMutationRecord>().AsNoTracking().SingleAsync(row => row.Id == recovery.DurableMutationId);
                Assert.Equal(ProjectCrossModuleMutationStatus.Failed, durable.Status);
                var payload = JsonSerializer.Deserialize<MoveDescendantsMutationPayload>(durable.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Assert.Equal(creation.Project.ProjectId, payload!.TargetProjectId);
            } else {
                Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
                Assert.Equal(child.Id, Assert.Single(outcome.Transfer!.Transfer.MovedNodeIds));
            }
        }
    }

    private static Task OpenAsync(IRenderedComponent<ProjectStructurePage> page, string node)
        => page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(node, "move-descendants-to-subproject", 0, 0));

    private static Task<ProjectStructureNode> CreateNoteAsync(ProjectWorkbenchService workbench, Guid project, string title, string parent)
        => workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, title, "", title, parent));

    public class ReconciliationFailure : DispatchProxy {
        public IProjectPartyIntegrationBridge Inner { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name == nameof(IProjectPartyIntegrationBridge.MoveAssignmentsToProjectAsync)
                ? Task.FromException(new InvalidOperationException("Synthetic failure at the assignment reconciliation boundary."))
                : targetMethod.Invoke(Inner, args);
        }
    }
}
