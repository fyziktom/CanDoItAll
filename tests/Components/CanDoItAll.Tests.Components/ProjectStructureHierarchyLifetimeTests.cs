using CanDoItAll.Workbench.Structure.UI;
using Bunit;
using System.Data.Common;
using System.Security.Claims;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureHierarchyLifetimeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dataset_or_generation_change_during_surface_read_prevents_publication(bool changeNamespace) {
        var read = new OwnerReadGate();
        MutableRuntime? runtime = null;
        await using var harness = await CreateHarnessAsync(read: read, configure: services => {
            services.AddSingleton<CanonicalRuntimeDatabase>();
            services.AddSingleton<ICanonicalRuntimeDatabase>(provider => runtime = new(provider.GetRequiredService<CanonicalRuntimeDatabase>()));
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var project = await CreateProjectAsync(projects, "Original surface read");
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        read.ArmProjects();
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        try {
            await read.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (changeNamespace) {
                var activation = harness.Context.Services.GetRequiredService<DatasetActivationService>();
                using var file = activation.Acquire(runtime!.Profile.Profile.Id);
                var original = (await file.ReadAsync())!.Plan.Binding;
                var changed = new DatasetActivationBinding(new(Guid.NewGuid()), Guid.NewGuid(), original.SchemaVersion, original.GuardProtocol);
                var plan = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate, changed, original);
                await file.WritePreparedAsync(plan);
                await file.MarkActiveAsync(plan);
            } else {
                runtime!.Generation++;
            }
        } finally {
            read.Release.TrySetResult();
        }
        page.WaitForAssertion(() => Assert.Contains("The project structure could not be loaded", page.Markup, StringComparison.Ordinal));
        Assert.Empty(page.FindAll("[data-testid='project-structure-canvas-loaded']"));
        Assert.Empty(page.FindComponents<StructureStructuralDialogs>());
        Assert.Empty(page.Instance.AuthoringOutcomes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Displayed_canvas_cannot_adopt_a_new_runtime_generation(bool changeAfterOpening) {
        MutableRuntime? runtime = null;
        await using var harness = await CreateHarnessAsync(configure: services => {
            services.AddSingleton<CanonicalRuntimeDatabase>();
            services.AddSingleton<ICanonicalRuntimeDatabase>(provider => runtime = new(provider.GetRequiredService<CanonicalRuntimeDatabase>()));
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Original runtime canvas");
        var child = await CreateProjectAsync(projects, "Original runtime child");
        var page = Render(harness, parent);
        EventCallback originalSubmit = default;
        if (changeAfterOpening) {
            await OpenHierarchyAsync(page, parent, child);
            originalSubmit = page.FindComponent<StructureStructuralDialogs>().Instance.ExecuteProjectHierarchyCommand;
        }
        runtime!.Generation++;
        if (changeAfterOpening) {
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, parent)));
            await page.InvokeAsync(() => originalSubmit.InvokeAsync());
        } else {
            await ContextActionAsync(page, $"project:{parent:D}", "project:add-subproject");
            Assert.Contains("Reload the project", page.Markup, StringComparison.Ordinal);
        }
        Assert.All(page.FindComponents<StructureStructuralDialogs>(), component => Assert.Null(component.Instance.ProjectHierarchyDialog));
        Assert.Empty(await projects.ListHierarchyLinksAsync());
        Assert.Empty(page.Instance.AuthoringOutcomes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hierarchy_commit_after_close_or_same_target_reopening_keeps_original_receipt(bool reopen) {
        var gate = new OwnerWriteGate();
        await using var harness = await CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Original parent");
        var child = await CreateProjectAsync(projects, "Original child");
        var neighbor = await CreateProjectAsync(projects, "Replacement choice");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        gate.ArmHierarchy(parent);
        var pending = SubmitHierarchyAsync(page);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.CloseProjectHierarchy.InvokeAsync());
            if (reopen) {
                await OpenHierarchyAsync(page, parent, neighbor);
            }
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));

        var links = await projects.ListHierarchyLinksAsync();
        Assert.Contains(links, link => link.ParentProjectId == parent && link.ChildProjectId == child);
        Assert.DoesNotContain(links, link => link.ParentProjectId == parent && link.ChildProjectId == neighbor);
        await page.InvokeAsync(() => {
            var dialog = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog;
            if (reopen) {
                Assert.NotNull(dialog);
                Assert.Equal(neighbor, dialog.SelectedProjectId);
                Assert.Empty(dialog.Error);
            } else {
                Assert.Null(dialog);
            }
            Assert.Contains("Original child", page.Markup, StringComparison.Ordinal);
            var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
            Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
            Assert.Equal(child, outcome.TargetProjectId);
            Assert.Equal(parent, outcome.Project.ProjectId);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Conversion_commit_cannot_close_or_replace_another_opening(bool loseReply, bool reopen) {
        var gate = new OwnerWriteGate { LoseCommitReply = loseReply };
        await using var harness = await CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = await CreateProjectAsync(projects, "Conversion owner");
        var first = await CreateNoteAsync(workbench, project, "Original note");
        var second = await CreateNoteAsync(workbench, project, "Untouched note");
        var page = Render(harness, project);
        await ContextActionAsync(page, first.Id, "note:convert-to-block");
        gate.ArmNode(project, first.Id);
        var submit = page.FindComponent<StructureStructuralDialogs>().Instance.ExecuteBlockMutation;
        var close = page.FindComponent<StructureStructuralDialogs>().Instance.CloseBlockMutation;
        var pending = page.InvokeAsync(() => submit.InvokeAsync());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => submit.InvokeAsync()).WaitAsync(TimeSpan.FromSeconds(5));
            await page.InvokeAsync(() => close.InvokeAsync());
            if (reopen) {
                await ContextActionAsync(page, second.Id, "note:convert-to-block");
                await page.InvokeAsync(() => submit.InvokeAsync());
                await page.InvokeAsync(() => close.InvokeAsync());
            }
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var stored = await workbench.GetStructureAsync(project);
        Assert.NotEqual(ProjectObjectType.Note, Assert.Single(stored.Nodes, node => node.Id == first.Id).ObjectType);
        Assert.Equal(ProjectObjectType.Note, Assert.Single(stored.Nodes, node => node.Id == second.Id).ObjectType);
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(first.Id, outcome.SourceNodeId);
        Assert.Equal(loseReply ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        await page.InvokeAsync(() => Assert.Equal(reopen ? second.Id : null,
            page.FindComponent<StructureStructuralDialogs>().Instance.BlockMutationDialog?.NodeId));
    }

    [Fact]
    public async Task Conversion_rejects_an_original_node_whose_type_changed_before_submit() {
        await using var harness = await CreateHarnessAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = await CreateProjectAsync(projects, "Changed conversion target");
        var first = await CreateNoteAsync(workbench, project, "Original note");
        var neighbor = await CreateNoteAsync(workbench, project, "Neighbor");
        var page = Render(harness, project);
        await ContextActionAsync(page, first.Id, "note:convert-to-block");
        var competingOrigin = await workbench.GetStructureAsync(project);
        var changed = await workbench.ReclassifyObjectAsync(project, first.Id,
            new(ProjectObjectType.ProjectBlock, "decision", "External change", "", "Externally retained notes") {
                ExpectedState = ProjectStructureNodeStateGuard.Capture(
                    Assert.Single(competingOrigin.Nodes, node => node.Id == first.Id), competingOrigin.ExpectedProjectAdmission!)
            });
        Assert.NotNull(changed);
        await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.ExecuteBlockMutation.InvokeAsync());
        var stored = await workbench.GetStructureAsync(project);
        var actual = Assert.Single(stored.Nodes, node => node.Id == first.Id);
        Assert.Equal((changed.Title, changed.Notes, changed.ObjectType, changed.ObjectSubtype),
            (actual.Title, actual.Notes, actual.ObjectType, actual.ObjectSubtype));
        Assert.Equal(neighbor.Title, Assert.Single(stored.Nodes, node => node.Id == neighbor.Id).Title);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, Assert.Single(page.Instance.AuthoringOutcomes).Kind);
    }

    [Fact]
    public async Task Duplicate_submission_and_retired_callbacks_cannot_mutate_A_B_A_reopening() {
        var gate = new OwnerWriteGate();
        await using var harness = await CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "ABA parent");
        var child = await CreateProjectAsync(projects, "ABA child");
        var neighbor = await CreateProjectAsync(projects, "ABA neighbor");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        var oldSubmit = page.FindComponent<StructureStructuralDialogs>().Instance.ExecuteProjectHierarchyCommand;
        var oldClose = page.FindComponent<StructureStructuralDialogs>().Instance.CloseProjectHierarchy;
        var oldId = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!.OpeningId;
        gate.ArmHierarchy(parent);
        var pending = page.InvokeAsync(() => oldSubmit.InvokeAsync());
        Guid reopenedId;
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => oldSubmit.InvokeAsync()).WaitAsync(TimeSpan.FromSeconds(5));
            await page.InvokeAsync(() => oldClose.InvokeAsync());
            await OpenHierarchyAsync(page, parent, neighbor);
            await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.CloseProjectHierarchy.InvokeAsync());
            await OpenHierarchyAsync(page, parent, child);
            reopenedId = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!.OpeningId;
            Assert.NotEqual(oldId, reopenedId);
            await page.InvokeAsync(() => oldSubmit.InvokeAsync());
            await page.InvokeAsync(() => oldClose.InvokeAsync());
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        await page.InvokeAsync(() => Assert.Equal(reopenedId,
            page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!.OpeningId));
        Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Single(await projects.ListHierarchyLinksAsync(), link => link.ParentProjectId == parent && link.ChildProjectId == child);
        Assert.DoesNotContain(await projects.ListHierarchyLinksAsync(), link => link.ChildProjectId == neighbor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recreated_subject_or_selected_project_is_refused_by_native_hierarchy_writer(bool recreateChild) {
        await using var harness = await CreateHarnessAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Lifetime parent");
        var child = await CreateProjectAsync(projects, "Lifetime child");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        var retired = recreateChild ? child : parent;
        var admission = (await projects.GetAsync(retired)).ExpectedProjectAdmission!;
        await SubmitSetupAsync(harness, new ProjectLifecycleCommand.Delete((await projects.GetAsync(retired)).ExpectedDefinition!));
        Assert.True((await projects.CreateAsync(retired, new() { Name = "Unrelated replacement" })).IsSuccess);
        await SubmitHierarchyAsync(page);
        Assert.DoesNotContain(await projects.ListHierarchyLinksAsync(), link => link.ParentProjectId == parent && link.ChildProjectId == child);
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Unconfirmed, outcome.Kind);
        var retained = Assert.Single(PendingStore(harness).Records);
        var command = Assert.IsType<ProjectLifecycleCommand.EnsureLink>(retained.Frozen.Submission.Command);
        Assert.Equal(admission, recreateChild ? command.Child : command.Parent);
        Assert.Equal(ProjectPendingStage.Unconfirmed, retained.Stage);
    }

    [Fact]
    public async Task Known_hierarchy_rejection_does_not_publish_into_successor_draft() {
        var read = new OwnerReadGate();
        await using var harness = await CreateHarnessAsync(read: read);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Cycle parent");
        var child = await CreateProjectAsync(projects, "Cycle child");
        var neighbor = await CreateProjectAsync(projects, "Next draft");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        await SubmitSetupAsync(harness, new ProjectLifecycleCommand.EnsureLink(
            (await projects.GetAsync(child)).ExpectedProjectAdmission!, (await projects.GetAsync(parent)).ExpectedProjectAdmission!));
        read.ArmHierarchy();
        var pending = SubmitHierarchyAsync(page);
        Guid nextId;
        try {
            await read.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.CloseProjectHierarchy.InvokeAsync());
            await OpenHierarchyAsync(page, parent, neighbor);
            nextId = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!.OpeningId;
        } finally {
            read.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var dialog = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!;
        Assert.Equal(nextId, dialog.OpeningId);
        Assert.Equal(neighbor, dialog.SelectedProjectId);
        Assert.Empty(dialog.Error);
        Assert.False(dialog.IsBusy);
        Assert.Equal(ProjectStructureAuthoringResultKind.Unconfirmed, Assert.Single(page.Instance.AuthoringOutcomes).Kind);
        Assert.Equal(ProjectPendingStage.Unconfirmed, Assert.Single(PendingStore(harness).Records).Stage);
        Assert.DoesNotContain(await projects.ListHierarchyLinksAsync(), link => link.ParentProjectId == parent && link.ChildProjectId == child);
    }

    [Fact]
    public async Task Lost_native_hierarchy_acknowledgement_recovers_original_receipt_and_leaves_successor_open() {
        var gate = new OwnerWriteGate { LoseCommitReply = true };
        await using var harness = await CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Unknown parent");
        var child = await CreateProjectAsync(projects, "Unknown child");
        var neighbor = await CreateProjectAsync(projects, "Untouched successor");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        gate.ArmHierarchy(parent);
        var pending = SubmitHierarchyAsync(page);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Contains(await projects.ListHierarchyLinksAsync(), link => link.ParentProjectId == parent && link.ChildProjectId == child);
            await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.CloseProjectHierarchy.InvokeAsync());
            await OpenHierarchyAsync(page, parent, neighbor);
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        Assert.Equal(child, outcome.TargetProjectId);
        var retained = Assert.Single(PendingStore(harness).Records);
        Assert.NotNull(retained.Accepted);
        Assert.Equal(new[] { parent, child }.Order(), ProjectLifecyclePendingTargets.Capture(retained.Frozen.Submission.Command).Order());
        var found = Assert.IsType<ProjectLifecycleLookup.Found>(await harness.Context.Services
            .GetRequiredService<ProjectNativeLifecycleOperations>().LookupAsync(retained.Operation));
        Assert.Equal(found.Accepted.Original, retained.Frozen.Submission);
        var successor = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!;
        Assert.Equal(neighbor, successor.SelectedProjectId);
        Assert.Empty(successor.Error);
        Assert.False(successor.RequiresObservation);
    }

    public enum ReadRetirement { Close, Replace, Route, RecreatedLifetime, Actor, RuntimeGeneration }

    [Theory]
    [InlineData(ReadRetirement.Close)]
    [InlineData(ReadRetirement.Replace)]
    [InlineData(ReadRetirement.Route)]
    [InlineData(ReadRetirement.RecreatedLifetime)]
    [InlineData(ReadRetirement.Actor)]
    [InlineData(ReadRetirement.RuntimeGeneration)]
    public async Task Delayed_choices_cannot_reopen_a_retired_opening(ReadRetirement retirement) {
        var read = new OwnerReadGate();
        MutableRuntime? runtime = null;
        await using var harness = await CreateHarnessAsync(read: read, configure: services => {
            services.AddSingleton<CanonicalRuntimeDatabase>();
            services.AddSingleton<ICanonicalRuntimeDatabase>(provider => runtime = new(provider.GetRequiredService<CanonicalRuntimeDatabase>()));
        });
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Read owner");
        var other = await CreateProjectAsync(projects, "Other owner");
        var actor = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "First")], "Fixture"))));
        var host = harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(component => component.Value, actor)
            .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, parent)));
        var page = host.FindComponent<ProjectStructurePage>();
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        read.ArmProjects();
        var pending = ContextActionAsync(page, $"project:{parent:D}", "project:add-subproject");
        Guid? replacementOpening = null;
        try {
            await read.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (retirement is ReadRetirement.Close or ReadRetirement.Replace) {
                await page.InvokeAsync(() => page.FindComponent<StructureStructuralDialogs>().Instance.CloseProjectHierarchy.InvokeAsync());
            }
            if (retirement == ReadRetirement.Replace) {
                await OpenHierarchyAsync(page, parent, other);
                replacementOpening = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog!.OpeningId;
            } else if (retirement == ReadRetirement.RecreatedLifetime) {
                await SubmitSetupAsync(harness, new ProjectLifecycleCommand.Delete((await projects.GetAsync(parent)).ExpectedDefinition!));
                Assert.True((await projects.CreateAsync(parent, new() { Name = "Recreated read owner" })).IsSuccess);
                host.Render(parameters => parameters.Add(component => component.Value, actor)
                    .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, parent)));
            } else if (retirement is ReadRetirement.Route or ReadRetirement.Actor or ReadRetirement.RuntimeGeneration) {
                if (retirement == ReadRetirement.RuntimeGeneration) {
                    runtime!.Generation++;
                }
                var nextActor = retirement == ReadRetirement.Actor
                    ? Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Second")], "Fixture")))) : actor;
                host.Render(parameters => parameters.Add(component => component.Value, nextActor)
                    .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId,
                        retirement == ReadRetirement.Route ? other : parent)));
            }
        } finally {
            read.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
            node => node.Id == $"project:{(retirement == ReadRetirement.Route ? other : parent):D}"));
        await page.InvokeAsync(() => {
            var dialog = page.FindComponent<StructureStructuralDialogs>().Instance.ProjectHierarchyDialog;
            if (replacementOpening.HasValue) {
                Assert.Equal(replacementOpening, dialog?.OpeningId);
                Assert.Equal(other, dialog?.SelectedProjectId);
            } else {
                Assert.Null(dialog);
            }
        });
        Assert.Empty(page.Instance.AuthoringOutcomes);
        Assert.Empty(await projects.ListHierarchyLinksAsync());
    }

    [Fact]
    public async Task Two_native_instances_keep_independent_hierarchy_completions() {
        var gate = new OwnerWriteGate();
        await using var harness = await CreateHarnessAsync(gate);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var first = await CreateProjectAsync(projects, "First instance");
        var firstChild = await CreateProjectAsync(projects, "First child");
        var second = await CreateProjectAsync(projects, "Second instance");
        var secondChild = await CreateProjectAsync(projects, "Second child");
        var left = Render(harness, first);
        var right = Render(harness, second);
        await OpenHierarchyAsync(left, first, firstChild);
        await OpenHierarchyAsync(right, second, secondChild);
        gate.ArmHierarchy(first);
        var firstWrite = SubmitHierarchyAsync(left);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var secondWrite = SubmitHierarchyAsync(right);
        gate.Release.TrySetResult();
        await Task.WhenAll(firstWrite, secondWrite).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(firstChild, Assert.Single(left.Instance.AuthoringOutcomes).TargetProjectId);
        Assert.Equal(secondChild, Assert.Single(right.Instance.AuthoringOutcomes).TargetProjectId);
        var links = await projects.ListHierarchyLinksAsync();
        Assert.Equal(2, links.Count);
        Assert.Contains(links, link => link.ParentProjectId == first && link.ChildProjectId == firstChild);
        Assert.Contains(links, link => link.ParentProjectId == second && link.ChildProjectId == secondChild);
    }

    [Fact]
    public async Task Unretained_hierarchy_acceptance_blocks_a_second_opening_before_dispatch() {
        await using var harness = await CreateHarnessAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Retained parent");
        var child = await CreateProjectAsync(projects, "First child");
        var other = await CreateProjectAsync(projects, "Second choice");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        PendingStore(harness).AcceptFailure = new IOException("Synthetic evidence retention failure");
        await SubmitHierarchyAsync(page);
        var store = PendingStore(harness);
        var original = Assert.Single(store.Records);
        Assert.Null(original.Accepted);
        await OpenHierarchyAsync(page, parent, other);
        await SubmitHierarchyAsync(page);
        Assert.Equal(original, Assert.Single(store.Records));
        Assert.Equal(1, store.DispatchClaims);
        Assert.DoesNotContain(await projects.ListHierarchyLinksAsync(), link => link.ChildProjectId == other);
        Assert.Contains(original.Operation.Id.ToString("D"), page.Instance.AuthoringOutcomes.Last().Message);
    }

    [Fact]
    public async Task Observed_hierarchy_add_allows_a_subsequent_reconnect_without_recovery() {
        await using var harness = await CreateHarnessAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Original parent");
        var child = await CreateProjectAsync(projects, "Child");
        var destination = await CreateProjectAsync(projects, "Destination parent");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        await SubmitHierarchyAsync(page);
        Assert.Empty(PendingStore(harness).Records);
        Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project-child:{child:D}");

        await ContextActionAsync(page, $"project-child:{child:D}", "project:reconnect-subproject");
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-project-select']").Change(destination.ToString("D")));
        await SubmitHierarchyAsync(page);

        var link = Assert.Single(await projects.ListHierarchyLinksAsync());
        Assert.Equal((destination, child), (link.ParentProjectId, link.ChildProjectId));
        Assert.Equal(2, PendingStore(harness).DispatchClaims);
        Assert.Empty(PendingStore(harness).Records);
        Assert.All(page.Instance.AuthoringOutcomes, outcome => Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind));
    }

    [Fact]
    public async Task Retired_refresh_keeps_the_accepted_hierarchy_record_for_recovery() {
        var read = new OwnerReadGate();
        await using var harness = await CreateHarnessAsync(read: read);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Refresh owner");
        var child = await CreateProjectAsync(projects, "Accepted child");
        var replacement = await CreateProjectAsync(projects, "Replacement surface");
        var page = Render(harness, parent);
        await OpenHierarchyAsync(page, parent, child);
        PendingStore(harness).BeforeAccept = () => {
            read.ArmHierarchy();
            return Task.CompletedTask;
        };
        var pending = SubmitHierarchyAsync(page);
        try {
            await read.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, replacement)));
            page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
                node => node.Id == $"project:{replacement:D}"));
        } finally {
            read.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.NotNull(Assert.Single(PendingStore(harness).Records).Accepted);
        Assert.Equal(child, Assert.Single(await projects.ListHierarchyLinksAsync()).ChildProjectId);
        Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == $"project:{replacement:D}");
    }

    [Fact]
    public async Task Failed_acknowledgement_cleanup_keeps_acceptance_and_blocks_another_dispatch() {
        await using var harness = await CreateHarnessAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Cleanup parent");
        var child = await CreateProjectAsync(projects, "Accepted child");
        var other = await CreateProjectAsync(projects, "Unchanged child");
        var page = Render(harness, parent);
        var store = PendingStore(harness);
        store.ClearFailure = new IOException("Synthetic cleanup failure");
        await OpenHierarchyAsync(page, parent, child);
        await SubmitHierarchyAsync(page);
        var retained = Assert.Single(store.Records);
        Assert.NotNull(retained.Accepted);
        var outcome = page.Instance.AuthoringOutcomes.Last();
        Assert.Equal(ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        Assert.Same(store.ClearFailure, outcome.Failure);
        Assert.Contains("recovery record could not be cleared", outcome.Message, StringComparison.Ordinal);
        await OpenHierarchyAsync(page, parent, other);
        await SubmitHierarchyAsync(page);
        Assert.Equal(retained, Assert.Single(store.Records));
        Assert.Equal(1, store.DispatchClaims);
        Assert.Equal(child, Assert.Single(await projects.ListHierarchyLinksAsync()).ChildProjectId);
    }

    [Fact]
    public async Task Reconnect_keeps_the_opening_edge_identity_when_matching_endpoints_are_recreated() {
        await using var harness = await CreateHarnessAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await CreateProjectAsync(projects, "Original edge parent");
        var child = await CreateProjectAsync(projects, "Original edge child");
        var destination = await CreateProjectAsync(projects, "New parent");
        var parentAdmission = (await projects.GetAsync(parent)).ExpectedProjectAdmission!;
        var childAdmission = (await projects.GetAsync(child)).ExpectedProjectAdmission!;
        var first = await SubmitSetupAsync(harness, new ProjectLifecycleCommand.EnsureLink(parentAdmission, childAdmission));
        var firstEdge = Assert.IsType<ProjectLifecycleAcknowledgement.LinkEnsured>(first.Acknowledgement).EdgeId;
        var page = Render(harness, parent);
        await ContextActionAsync(page, $"project-child:{child}", "project:reconnect-subproject");
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-project-select']").Change(destination.ToString("D")));
        await SubmitSetupAsync(harness, new ProjectLifecycleCommand.RemoveLink(new(firstEdge, parentAdmission, childAdmission)));
        var replacement = await SubmitSetupAsync(harness, new ProjectLifecycleCommand.EnsureLink(parentAdmission, childAdmission));
        var replacementEdge = Assert.IsType<ProjectLifecycleAcknowledgement.LinkEnsured>(replacement.Acknowledgement).EdgeId;
        Assert.NotEqual(firstEdge, replacementEdge);
        await SubmitHierarchyAsync(page);
        var retained = Assert.Single(PendingStore(harness).Records);
        var command = Assert.IsType<ProjectLifecycleCommand.ReconnectLink>(retained.Frozen.Submission.Command);
        Assert.Equal(firstEdge, command.Expected.EdgeId);
        Assert.Equal(ProjectPendingStage.Unconfirmed, retained.Stage);
        var links = await projects.ListHierarchyLinksAsync();
        Assert.Contains(links, link => link.ParentProjectId == parent && link.ChildProjectId == child);
        Assert.DoesNotContain(links, link => link.ParentProjectId == destination && link.ChildProjectId == child);
        Assert.Equal(ProjectStructureAuthoringResultKind.Unconfirmed, Assert.Single(page.Instance.AuthoringOutcomes).Kind);
    }

    internal static Task<ComponentTestHarness> CreateHarnessAsync(OwnerWriteGate? gate = null, OwnerReadGate? read = null,
        Action<IServiceCollection>? configure = null)
        => ComponentTestHarness.CreateAsync(services => {
            var interceptors = new IInterceptor?[] { gate, read }.OfType<IInterceptor>().ToArray();
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new PooledDbContextFactory<ProjectsDbContext>(
                new DbContextOptionsBuilder<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
                    .AddInterceptors(interceptors).Options));
            services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
                new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>())
                    .AddInterceptors(interceptors).Options));
            configure?.Invoke(services);
        });

    private sealed class MutableRuntime(ICanonicalRuntimeDatabase inner) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile => inner.Profile;
        public long Generation { get; set; } = inner.Generation;
    }

    private static ProjectLifecyclePendingTestStore PendingStore(ComponentTestHarness harness)
        => Assert.IsType<ProjectLifecyclePendingTestStore>(harness.Context.Services.GetRequiredService<IProjectLifecyclePendingStore>());

    private static async Task<ProjectLifecycleAccepted> SubmitSetupAsync(ComponentTestHarness harness, ProjectLifecycleCommand command) {
        var native = harness.Context.Services.GetRequiredService<ProjectNativeLifecycleOperations>();
        var context = await native.GetContextAsync();
        var original = native.Freeze(command, ProjectNativeLifecycleOperations.Scope(context), context, Guid.NewGuid());
        return Assert.IsType<ProjectLifecycleOutcome.Accepted>(await native.DispatchAsync(original.Submission)).Result;
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name) {
        var result = await projects.SaveAsync(new() { Name = name });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateNoteAsync(ProjectWorkbenchService workbench, Guid project, string title)
        => workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, title, "", title, $"project:{project:D}"));

    private static IRenderedComponent<ProjectStructurePage> Render(ComponentTestHarness harness, Guid project) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForAssertion(() => Assert.Contains(page.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
            node => node.Id == $"project:{project:D}"));
        return page;
    }

    private static Task ContextActionAsync(IRenderedComponent<ProjectStructurePage> page, string node, string action)
        => page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(node, action, 0, 0));

    private static async Task OpenHierarchyAsync(IRenderedComponent<ProjectStructurePage> page, Guid parent, Guid child) {
        await ContextActionAsync(page, $"project:{parent:D}", "project:add-subproject");
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-project-select']").Change(child.ToString("D")));
    }

    private static Task SubmitHierarchyAsync(IRenderedComponent<ProjectStructurePage> page)
        => page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-submit']").ClickAsync(new MouseEventArgs()));

    internal sealed class OwnerWriteGate : SaveChangesInterceptor, IDbTransactionInterceptor {
        private Guid? project;
        private string? node;
        private DbContext? committedContext;
        public bool LoseCommitReply { get; init; }
        public Exception LostReply { get; } = new InvalidOperationException("Synthetic lost native hierarchy acknowledgement.");
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ArmHierarchy(Guid parent) => project = parent;

        public void ArmNode(Guid owner, string key) {
            project = owner;
            node = key;
        }

        public void ArmCreatedNodes(Guid owner) => ArmNode(owner, string.Empty);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            var matches = project is { } owner && (node is null
                ? eventData.Context?.ChangeTracker.Entries<ProjectHierarchyLink>().Any(entry =>
                    entry.State == EntityState.Added && entry.Entity.ParentProjectId == owner) is true
                : eventData.Context?.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry =>
                    entry.Entity.ProjectId == owner && (node == string.Empty ? entry.State == EntityState.Added :
                        entry.State == EntityState.Modified && entry.Entity.NodeKey == node)) is true);
            if (matches) {
                project = null;
                if (LoseCommitReply) {
                    committedContext = eventData.Context;
                } else {
                    Entered.TrySetResult();
                    await Release.Task.WaitAsync(cancellationToken);
                }
            }
            return result;
        }

        public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (committedContext is not null && ReferenceEquals(committedContext, eventData.Context)) {
                committedContext = null;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
                throw LostReply;
            }
        }
    }

    internal sealed class OwnerReadGate : DbCommandInterceptor {
        private string? table;
        private Func<bool>? shouldBlock;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void ArmProjects(Func<bool>? when = null) {
            table = "Projects_Projects";
            shouldBlock = when;
        }
        public void ArmHierarchy() => table = "Projects_ProjectHierarchyLinks";
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (table is { } target && eventData.Context is ProjectsDbContext &&
                command.CommandText.Contains(target, StringComparison.Ordinal) && (shouldBlock?.Invoke() ?? true)) {
                table = null;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
