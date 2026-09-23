using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructurePageProcessLaunchScopeTests {
    private static readonly Guid DefinitionId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
        new ProcessDefinitionCatalogItemKey("customer-onboarding")).Value;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Late_preview_cannot_replace_a_new_project_dialog(bool pauseMetadata, bool failPreview) {
        var probe = new ProcessLaunchProbe { PausePreview = !pauseMetadata, FailPreview = failPreview };
        await using var harness = await CreateHarnessAsync(probe);
        var first = await CreateTargetAsync(harness, "Customer onboarding");
        var second = await CreateTargetAsync(harness, "Customer onboarding copy");
        var cut = RenderProject(harness, first);
        await OpenStartDialogAsync(cut, first);
        var admittedDialogId = GetStartDialog(cut).DialogId;
        cut.Render(parameters => parameters.Add(page => page.ProjectId, first.ProjectId));
        cut.WaitForAssertion(() => Assert.Equal(admittedDialogId, GetStartDialog(cut).DialogId));
        probe.PauseMetadata = pauseMetadata;
        var preview = cut.Find("[data-testid='project-structure-process-start-continue']")
            .ClickAsync(new MouseEventArgs());
        Guid currentDialogId;
        try {
            await probe.PreviewEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            NavigateToProject(harness, cut, second);
            await OpenStartDialogAsync(cut, second);
            currentDialogId = GetStartDialog(cut).DialogId;
        } finally {
            probe.ReleasePreview.TrySetResult();
        }

        await preview;
        AssertOrigin(Assert.Single(probe.ResolutionRequests), first);
        cut.WaitForAssertion(() => {
            var dialog = GetStartDialog(cut);
            Assert.Equal(currentDialogId, dialog.DialogId);
            Assert.Equal(second.ProjectId, dialog.ProjectId);
            Assert.Equal(ProjectStructureProcessStartStage.Confirm, dialog.Stage);
            Assert.False(dialog.IsBusy);
            Assert.Empty(dialog.Error);
            Assert.DoesNotContain(ProcessLaunchProbe.PreviewFailureMessage, cut.Markup, StringComparison.Ordinal);
            AssertCurrentNavigation(harness, second);
        });
    }

    [Fact]
    public async Task Started_run_links_its_original_target_without_replacing_the_new_project_dialog() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var first = await CreateTargetAsync(harness, "Customer onboarding");
        var second = await CreateTargetAsync(harness, "Customer onboarding copy");
        var cut = RenderProject(harness, first);
        await OpenStartDialogAsync(cut, first);
        await cut.Find("[data-testid='project-structure-process-start-continue']")
            .ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => {
            var dialog = GetStartDialog(cut);
            Assert.Equal(ProjectStructureProcessStartStage.Staffing, dialog.Stage);
            Assert.Equal(0, dialog.RequiredGapCount);
            Assert.False(dialog.AssignmentsReviewed);
        });
        var previewRequest = Assert.Single(probe.ResolutionRequests);
        var roleToChange = GetStartDialog(cut).Roles.First(role =>
            role.DirectoryCandidates.Any(candidate => candidate.CandidateId == ProcessLaunchProbe.AlternateExecutorId));
        await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureProcessAssignmentDialog>().Instance
            .SelectProcessStartCandidate.InvokeAsync(new ProjectStructureProcessStartCandidateSelection(
                roleToChange.LaunchPlanRoleId,
                ProcessLaunchProbe.AlternateExecutorId)));
        var changedRole = Assert.Single(GetStartDialog(cut).Roles, role => role.LaunchPlanRoleId == roleToChange.LaunchPlanRoleId);
        Assert.Equal(ProcessLaunchProbe.AlternateExecutorId, Assert.Single(changedRole.Candidates, candidate => candidate.IsSelected).TechnicalAgentId);
        Assert.False(GetStartDialog(cut).AssignmentsReviewed);
        await PrepareReviewedAsync(cut);
        probe.PauseLaunch = true;
        var launch = cut.Find("[data-testid='project-structure-process-assignment-review-start']")
            .ClickAsync(new MouseEventArgs());
        Guid currentDialogId;
        try {
            await probe.LaunchEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            NavigateToProject(harness, cut, second);
            await OpenStartDialogAsync(cut, second);
            currentDialogId = GetStartDialog(cut).DialogId;
        } finally {
            probe.ReleaseLaunch.TrySetResult();
        }

        await launch;
        Assert.Equal(2, probe.ResolutionRequests.Count);
        var launchRequest = probe.ResolutionRequests[1];
        AssertOrigin(launchRequest, first);
        Assert.Equal(
            previewRequest.Variables.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            launchRequest.Variables.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(2, probe.PreparedContexts.Count);
        Assert.Same(probe.PreparedContexts[0].Source, probe.PreparedContexts[1].Source);
        Assert.Equal(first.TargetNodeId, launchRequest.Variables[ProcessLaunchProbe.SelectedSourceVariable]);
        Assert.NotEmpty(launchRequest.ExecutorOverrides);
        Assert.All(launchRequest.ExecutorOverrides, assignment => {
            Assert.Equal(ProcessLaunchExecutorKinds.Agent, assignment.ExecutorKind);
            Assert.Equal(
                assignment.StepKey == roleToChange.StepKey
                    ? ProcessLaunchProbe.AlternateExecutorId.ToString("D")
                    : ProcessLaunchProbe.ExecutorId.ToString("D"),
                assignment.ExecutorId);
        });

        var runId = Assert.IsType<ProcessRunId>(probe.QueuedRunId);
        var runNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value);
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var originalSurface = await workbench.GetStructureAsync(first.ProjectId);
        var currentSurface = await workbench.GetStructureAsync(second.ProjectId);
        Assert.Single(originalSurface.Links, link =>
            link.IsUserAuthored &&
            link.Kind == ProjectObjectLinkKind.Uses &&
            link.SourceId == first.TargetNodeId &&
            link.TargetId == runNodeId);
        Assert.DoesNotContain(currentSurface.Links, link => link.TargetId == runNodeId);
        cut.WaitForAssertion(() => {
            var dialog = GetStartDialog(cut);
            Assert.Equal(currentDialogId, dialog.DialogId);
            Assert.Equal(second.ProjectId, dialog.ProjectId);
            Assert.Equal(ProjectStructureProcessStartStage.Confirm, dialog.Stage);
            Assert.Empty(dialog.Error);
            Assert.DoesNotContain("started for Delivery target", cut.Markup, StringComparison.Ordinal);
            AssertCurrentNavigation(harness, second);
        });
    }

    [Fact]
    public async Task Current_preview_failure_remains_visible_and_can_be_retried() {
        var probe = new ProcessLaunchProbe { PausePreview = true, FailPreview = true };
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Customer onboarding");
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        var preview = cut.Find("[data-testid='project-structure-process-start-continue']")
            .ClickAsync(new MouseEventArgs());
        try {
            await probe.PreviewEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        } finally {
            probe.ReleasePreview.TrySetResult();
        }
        await preview;

        var failedDialog = GetStartDialog(cut);
        Assert.False(failedDialog.IsBusy);
        Assert.Contains(ProcessLaunchProbe.PreviewFailureMessage, failedDialog.Error, StringComparison.Ordinal);
        probe.FailPreview = false;
        probe.PausePreview = false;
        await cut.Find("[data-testid='project-structure-process-start-continue']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => {
            var dialog = GetStartDialog(cut);
            Assert.Equal(failedDialog.DialogId, dialog.DialogId);
            Assert.Equal(ProjectStructureProcessStartStage.Staffing, dialog.Stage);
            Assert.False(dialog.IsBusy);
            Assert.Empty(dialog.Error);
        });
        Assert.Equal(2, probe.ResolutionRequests.Count);
        Assert.All(probe.ResolutionRequests, request => AssertOrigin(request, target));
    }

    [Fact]
    public async Task Pending_process_link_keeps_its_original_project_and_does_not_open_a_stale_start_dialog() {
        var probe = new ProcessLaunchProbe();
        var saveGate = new ProcessLinkSaveGate();
        await using var harness = await CreateHarnessAsync(probe, saveGate);
        var first = await CreateTargetAsync(harness, "Original link project", linkDefinition: false);
        var second = await CreateTargetAsync(harness, "Current link project");
        var cut = RenderProject(harness, first);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
            node => node.Id == first.TargetNodeId));
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(
            first.TargetNodeId, "add-process", 0, 0));
        await cut.WaitForElement($"[data-testid='project-structure-process-link-option-{DefinitionId:N}']")
            .ClickAsync(new MouseEventArgs());
        saveGate.ProjectId = first.ProjectId;
        var link = cut.Find("[data-testid='project-structure-process-link-submit']").ClickAsync(new MouseEventArgs());
        Guid currentDialogId;
        try {
            await saveGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            NavigateToProject(harness, cut, second);
            await OpenStartDialogAsync(cut, second);
            currentDialogId = GetStartDialog(cut).DialogId;
        } finally {
            saveGate.Release.TrySetResult();
        }
        await link;
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var original = await workbench.GetStructureAsync(first.ProjectId);
        var current = await workbench.GetStructureAsync(second.ProjectId);
        Assert.Single(original.Links, item => item.IsUserAuthored && item.SourceId == first.TargetNodeId &&
            item.TargetId == first.ProcessNodeId && item.Kind == ProjectObjectLinkKind.Uses);
        Assert.DoesNotContain(current.Links, item => item.SourceId == first.TargetNodeId);
        cut.WaitForAssertion(() => {
            Assert.Equal(currentDialogId, GetStartDialog(cut).DialogId);
            Assert.Equal(second.ProjectId, GetStartDialog(cut).ProjectId);
            AssertCurrentNavigation(harness, second);
        });
    }

    [Fact]
    public async Task Starting_the_saved_review_does_not_resolve_a_new_plan() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Saved review");
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        await cut.Find("[data-testid='project-structure-process-start-continue']").ClickAsync(new MouseEventArgs());
        await PrepareReviewedAsync(cut);
        var request = GetStartDialog(cut).PreparedRequest!;
        var planId = GetStartDialog(cut).LaunchPlanId;
        Assert.Equal(2, probe.ResolutionRequests.Count);
        probe.FailPreview = true;
        await cut.Find("[data-testid='project-structure-process-assignment-review-start']").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, probe.ResolutionRequests.Count);
        var saved = Assert.IsType<ProcessPreparedLaunchSnapshot>(await harness.Context.Services
            .GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(request.PreparedAdmissionId!.Value));
        Assert.NotNull(saved.AcceptedAtUtc);
        Assert.Equal(planId, saved.Preparation.Review.PlanId.Value);
        Assert.Equal(request.CallerIntentId, saved.Preparation.CallerIntentId);
        Assert.Equal(saved.Preparation.InitialCommit.Mutation.State.RunId, probe.QueuedRunId);
    }

    [Fact]
    public async Task Queue_acknowledgement_failure_keeps_the_accepted_run_visible_and_links_only_that_run() {
        var failure = new ArgumentException("Injected queued process acknowledgement loss.");
        var probe = new ProcessLaunchProbe { LaunchAckFailure = failure };
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Accepted launch receipt");
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        await cut.Find("[data-testid='project-structure-process-start-continue']").ClickAsync(new MouseEventArgs());
        await PrepareReviewedAsync(cut);
        var admission = GetStartDialog(cut).PreparedRequest!.PreparedAdmissionId!.Value;
        await cut.Find("[data-testid='project-structure-process-assignment-review-start']").ClickAsync(new MouseEventArgs());
        var runId = Assert.IsType<ProcessRunId>(probe.QueuedRunId);
        var saved = Assert.IsType<ProcessPreparedLaunchSnapshot>(await harness.Context.Services.GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(admission));
        Assert.NotNull(saved.AcceptedAtUtc);
        Assert.Equal(runId, saved.Preparation.InitialCommit.Mutation.State.RunId);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Delivered, saved.LinkDeliveryState);
        Assert.Contains(runId.Value.ToString("D"), harness.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        var graph = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(target.ProjectId);
        Assert.Single(graph.Links, link => link.SourceId == target.TargetNodeId &&
            link.TargetId == ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value) && link.Kind == ProjectObjectLinkKind.Uses);
        Assert.Equal(2, probe.ResolutionRequests.Count);
    }

    [Fact]
    public async Task Reloaded_dialog_restores_the_exact_saved_review_and_original_source_without_resolving_again() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Reloaded review");
        var original = RenderProject(harness, target);
        await OpenStartDialogAsync(original, target);
        await original.Find("[data-testid='project-structure-process-start-continue']").ClickAsync(new MouseEventArgs());
        await PrepareReviewedAsync(original);
        var before = GetStartDialog(original);
        var request = before.PreparedRequest!;
        original.Dispose();
        harness.Context.JSInterop.Setup<string?>("sessionStorage.getItem", _ => true).SetResult(before.LaunchIntentId.Value.ToString("D"));
        probe.FailPreview = true;
        var restored = RenderProject(harness, target);
        restored.WaitForAssertion(() => Assert.Contains(restored.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
            node => node.Id == target.TargetNodeId));
        await restored.InvokeAsync(() => restored.FindComponent<CanvasWorkbench>().Instance.OnContextAction(target.ProcessNodeId, "start-process", 0, 0));
        restored.WaitForAssertion(() => {
            var current = GetStartDialog(restored);
            Assert.Equal(request.PreparedAdmissionId, current.PreparedRequest?.PreparedAdmissionId);
            Assert.Equal(before.LaunchIntentId, current.LaunchIntentId);
            Assert.Equal(before.LaunchAuthority!.ProjectAdmission, current.LaunchAuthority!.ProjectAdmission);
            Assert.Equal(request.Variables.OrderBy(pair => pair.Key), current.PreparedRequest!.Variables.OrderBy(pair => pair.Key));
            Assert.False(current.AssignmentsReviewed);
        });
        Assert.Equal(2, probe.ResolutionRequests.Count);
        Assert.Null(probe.QueuedRunId);
    }

    [Fact]
    public async Task Editing_a_saved_assignment_allocates_a_new_intent_and_retains_the_previous_review() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Changed review");
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        await cut.Find("[data-testid='project-structure-process-start-continue']").ClickAsync(new MouseEventArgs());
        await PrepareReviewedAsync(cut);
        var before = GetStartDialog(cut);
        var role = before.Roles.First(item => item.DirectoryCandidates.Any(candidate => candidate.CandidateId == ProcessLaunchProbe.AlternateExecutorId));
        await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureProcessAssignmentDialog>().Instance.SelectProcessStartCandidate
            .InvokeAsync(new ProjectStructureProcessStartCandidateSelection(role.LaunchPlanRoleId, ProcessLaunchProbe.AlternateExecutorId)));
        Assert.Null(GetStartDialog(cut).PreparedRequest);
        Assert.NotEqual(before.LaunchIntentId, GetStartDialog(cut).LaunchIntentId);
        await PrepareReviewedAsync(cut);
        var after = GetStartDialog(cut);
        Assert.NotEqual(before.PreparedRequest!.PreparedAdmissionId, after.PreparedRequest!.PreparedAdmissionId);
        Assert.Equal(before.LaunchAuthority!.ProjectAdmission, after.LaunchAuthority!.ProjectAdmission);
        var store = harness.Context.Services.GetRequiredService<IProcessPreparedLaunchStore>();
        var retained = Assert.IsType<ProcessPreparedLaunchSnapshot>(await store.GetAsync(before.PreparedRequest.PreparedAdmissionId!.Value));
        Assert.Equal(before.LaunchIntentId, retained.Preparation.CallerIntentId);
        Assert.Null(retained.AcceptedAtUtc);
        Assert.Null(probe.QueuedRunId);
    }

    [Fact]
    public async Task Missing_browser_preparation_is_explicit_and_new_launch_requires_the_separate_control() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Pending browser intent");
        var unknownIntent = Guid.NewGuid();
        var storage = harness.Context.JSInterop.Setup<string?>("sessionStorage.getItem", _ => true);
        storage.SetResult(unknownIntent.ToString("D"));
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        cut.WaitForAssertion(() => Assert.Contains(unknownIntent.ToString("D"), GetStartDialog(cut).Error, StringComparison.Ordinal));
        Assert.Null(GetStartDialog(cut).PreparedRequest);
        Assert.Empty(probe.ResolutionRequests);
        Assert.Null(probe.QueuedRunId);
        storage.SetResult(null);
        await cut.Find("[data-testid='project-structure-process-start-new-intent']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => {
            Assert.Empty(GetStartDialog(cut).Error);
            Assert.NotNull(GetStartDialog(cut).LaunchAuthority);
            Assert.NotEqual(unknownIntent, GetStartDialog(cut).LaunchIntentId.Value);
        });
        Assert.Empty(probe.ResolutionRequests);
        Assert.Null(probe.QueuedRunId);
    }

    private static async Task PrepareReviewedAsync(IRenderedComponent<ProjectStructurePage> cut) {
        await cut.Find("[data-testid='project-structure-process-assignment-review-start']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => {
            Assert.NotNull(GetStartDialog(cut).PreparedRequest?.PreparedAdmissionId);
            Assert.False(GetStartDialog(cut).AssignmentsReviewed);
            Assert.Contains("Start reviewed run", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync(ProcessLaunchProbe probe, ProcessLinkSaveGate? saveGate = null)
        => ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<ISecretVault>(new InMemorySecretVault()));
            services.Replace(ServiceDescriptor.Singleton<IAgentReferenceDataProvider>(probe));
            services.Replace(ServiceDescriptor.Singleton<IProcessLaunchExecutorResolver>(probe));
            services.Replace(ServiceDescriptor.Singleton<IProcessRuntimeDispatchQueue>(probe));
            services.Replace(ServiceDescriptor.Singleton<IProcessLaunchArtifactInitializer>(probe));
            services.AddSingleton<IProcessLaunchVariableContributor>(probe);
            if (saveGate is not null) {
                services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
                    new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>())
                        .AddInterceptors(saveGate).Options));
            }
        });

    private static async Task<LaunchTarget> CreateTargetAsync(ComponentTestHarness harness, string name, bool linkDefinition = true) {
        var services = harness.Context.Services;
        var projectName = $"{name} {Guid.NewGuid():N}";
        var saved = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
            Name = projectName,
            Description = "Process launch scope component proof.",
            Objective = "Keep the reviewed project and target after navigation.",
            CurrentPhase = "Delivery"
        });
        Assert.True(saved.IsSuccess);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var target = await workbench.CreateObjectAsync(
            saved.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delivery target",
                "Onboarding block",
                $"Target context for {projectName}.",
                $"project:{saved.Value}",
                420,
                260,
                ObjectSubtype: "delivery"));
        var processNodeId = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(DefinitionId);
        if (linkDefinition) {
            await workbench.LinkObjectsAsync(saved.Value, target.Id, processNodeId, ProjectObjectLinkKind.Uses);
        }
        return new LaunchTarget(saved.Value, projectName, target.Id, processNodeId);
    }

    private static IRenderedComponent<ProjectStructurePage> RenderProject(
        ComponentTestHarness harness,
        LaunchTarget target) {
        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/projects/{target.ProjectId:D}/structure");
        return harness.Context.Render<ProjectStructurePage>(parameters =>
            parameters.Add(page => page.ProjectId, target.ProjectId));
    }

    private static void NavigateToProject(
        ComponentTestHarness harness,
        IRenderedComponent<ProjectStructurePage> cut,
        LaunchTarget target) {
        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/projects/{target.ProjectId:D}/structure");
        cut.Render(parameters => parameters.Add(page => page.ProjectId, target.ProjectId));
    }

    private static async Task OpenStartDialogAsync(
        IRenderedComponent<ProjectStructurePage> cut,
        LaunchTarget target) {
        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes,
            node => node.Id == target.TargetNodeId));
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(
            target.ProcessNodeId,
            "start-process",
            0,
            0));
        cut.WaitForElement("[data-testid='project-structure-process-start-continue']");
    }

    private static ProjectStructureProcessStartDialogState GetStartDialog(
        IRenderedComponent<ProjectStructurePage> cut)
        => Assert.Single(cut.FindComponents<ProjectStructureCanvasDialogs>()
            .Select(dialogs => dialogs.Instance.ProcessStartDialog)
            .OfType<ProjectStructureProcessStartDialogState>()
            .DistinctBy(dialog => dialog.DialogId));

    private static void AssertOrigin(ProcessLaunchExecutorResolutionRequest request, LaunchTarget target) {
        Assert.Equal(target.ProjectId.ToString("D"), request.Variables["ProjectId"]);
        Assert.Equal(target.ProjectName, request.Variables["ProjectName"]);
        Assert.Equal(target.TargetNodeId, request.Variables["ProjectNodeId"]);
        Assert.Equal(target.ProcessNodeId, request.Variables["ProcessNodeId"]);
        var context = request.Variables[ProjectStructureProcessLaunchContext.ContextSummaryVariableName];
        Assert.Contains(target.ProjectId.ToString("D"), context, StringComparison.Ordinal);
        Assert.Contains(target.TargetNodeId, context, StringComparison.Ordinal);
    }

    private static void AssertCurrentNavigation(ComponentTestHarness harness, LaunchTarget target)
        => Assert.EndsWith(
            $"/projects/{target.ProjectId:D}/structure",
            harness.Context.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);

    private sealed record LaunchTarget(Guid ProjectId, string ProjectName, string TargetNodeId, string ProcessNodeId);

    private sealed class ProcessLinkSaveGate : SaveChangesInterceptor {
        public Guid? ProjectId { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (ProjectId is { } project && eventData.Context?.ChangeTracker.Entries<ProjectObjectLinkRecord>()
                    .Any(entry => entry.State == EntityState.Added && entry.Entity.ProjectId == project) is true) {
                ProjectId = null;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class ProcessLaunchProbe :
        IAgentReferenceDataProvider,
        IProcessLaunchExecutorResolver,
        IProcessRuntimeDispatchQueue,
        IProcessLaunchArtifactInitializer,
        IProcessLaunchVariableContributor {
        public const string PreviewFailureMessage = "The earlier project preview failed.";
        public const string SelectedSourceVariable = "ScopeProofSelectedSource";
        public static readonly Guid ExecutorId = new("6812e3cd-ddf9-433b-9287-9ea5a9be67b4");
        public static readonly Guid AlternateExecutorId = new("2dcc33db-25c3-4b4b-af3c-91b6bbd3e6a4");
        private static readonly AgentDefinition AlternateExecutor = new(
            AlternateExecutorId,
            "Alternate staffing manager",
            "Staffing manager",
            "Capacity and staffing decisions for onboarding delivery.",
            "Review staffing feasibility.",
            AgentLifecycleStatus.Active,
            null,
            string.Empty,
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            0.2d,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: AgentWorkspaceToolAccessMetadata.Write("{}", new AgentWorkspaceToolAccessSettings {
                CanReadFiles = true,
                CanWriteFiles = true
            }),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["staffing-manager"],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        public bool PausePreview { get; set; }
        public bool PauseMetadata { get; set; }
        public bool FailPreview { get; set; }
        public bool PauseLaunch { get; set; }
        public Exception? LaunchAckFailure { get; set; }
        public TaskCompletionSource PreviewEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleasePreview { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LaunchEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseLaunch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ProcessLaunchExecutorResolutionRequest> ResolutionRequests { get; } = [];
        public List<ProcessLaunchPreparationContext> PreparedContexts { get; } = [];
        public ProcessRunId? QueuedRunId { get; private set; }

        public void Enrich(ProcessLaunchPreparationContext context, IDictionary<string, string> variables) {
            PreparedContexts.Add(context);
            variables[SelectedSourceVariable] = context.Source.SelectedItem.Id;
        }

        public async Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default) {
            if (PauseMetadata && request == AgentReferenceDataRequest.AgentsAndProviders(activeAgentsOnly: true)) {
                PauseMetadata = false;
                PreviewEntered.TrySetResult();
                await ReleasePreview.Task.WaitAsync(cancellationToken);
            }

            return new AgentReferenceDataSnapshot(
                request.Sections,
                [AlternateExecutor],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                DateTimeOffset.UtcNow,
                TimeSpan.Zero);
        }

        public async ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(
            ProcessLaunchExecutorResolutionRequest request,
            CancellationToken cancellationToken = default) {
            ResolutionRequests.Add(request);
            if (PausePreview) {
                PreviewEntered.TrySetResult();
                await ReleasePreview.Task.WaitAsync(cancellationToken);
            }
            if (FailPreview) {
                throw new InvalidOperationException(PreviewFailureMessage);
            }

            var steps = request.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
            var bindings = request.Plan.Steps
                .Where(step => step.IsExecutable)
                .Select(step => {
                    var roleKey = steps[step.StepKey].RoleAssignments.OrderBy(assignment => assignment.FallbackOrder).First().RoleKey;
                    var reviewed = request.ExecutorOverrides.FirstOrDefault(assignment =>
                        assignment.StepKey == step.StepKey && assignment.RoleKey == roleKey);
                    return new ProcessLaunchExecutorBinding(
                        step.StepKey,
                        roleKey,
                        reviewed?.ExecutorKind ?? ProcessLaunchExecutorKinds.Agent,
                        reviewed?.ExecutorId ?? ExecutorId.ToString("D"),
                        reviewed?.ExecutorDisplayName ?? "Scope test executor",
                        "sha256:process-scope-test-readiness",
                        "Resolved by the component scope test.");
                })
                .ToArray();
            return new ProcessLaunchExecutorResolution(bindings, []);
        }

        public async ValueTask EnqueueAsync(
            ProcessRuntimeDispatchQueueRequest request,
            CancellationToken cancellationToken = default) {
            QueuedRunId = request.RunId;
            if (PauseLaunch) {
                LaunchEntered.TrySetResult();
                await ReleaseLaunch.Task.WaitAsync(cancellationToken);
            }
            if (LaunchAckFailure is { } failure) {
                throw failure;
            }
        }

        public void EnqueueOrDefer(ProcessRuntimeDispatchQueueRequest request) {
            QueuedRunId = request.RunId;
        }

        public Task InitializeAsync(
            ProcessLaunchArtifactInitializationRequest request,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
