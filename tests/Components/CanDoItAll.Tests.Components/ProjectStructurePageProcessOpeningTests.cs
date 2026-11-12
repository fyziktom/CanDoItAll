using System.Security.Claims;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageProcessLaunchScopeTests {
    [Fact]
    public async Task Committed_process_link_is_observed_after_refresh_failure_without_another_write() {
        var probe = new ProcessLaunchProbe();
        var gate = new ProcessLinkSaveGate();
        await using var harness = await CreateHarnessAsync(probe, gate);
        var target = await CreateTargetAsync(harness, "Retained process link", linkDefinition: false);
        var cut = RenderProject(harness, target);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == target.TargetNodeId));
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(target.TargetNodeId, "add-process", 0, 0));
        await cut.WaitForElement($"[data-testid='project-structure-process-link-option-{DefinitionId:N}']").ClickAsync(new MouseEventArgs());
        gate.FailReadAfterSave = true;
        await cut.Find("[data-testid='project-structure-process-link-submit']").ClickAsync(new MouseEventArgs());
        var receipt = Assert.Single(cut.Instance.ProcessLinkReceipts);
        Assert.True(receipt.Committed);
        Assert.Contains("Observe original link", cut.Markup, StringComparison.Ordinal);
        await cut.Find("[data-testid='project-structure-process-link-submit']").ClickAsync(new MouseEventArgs());
        Assert.Same(receipt, Assert.Single(cut.Instance.ProcessLinkReceipts));
        var surface = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(target.ProjectId);
        Assert.Single(surface.Links, link => link.SourceId == target.TargetNodeId && link.TargetId == target.ProcessNodeId);
        Assert.Equal(target.TargetNodeId, GetStartDialog(cut).TargetNodeId);
    }

    [Fact]
    public async Task Retained_start_callback_cannot_prepare_a_same_target_successor() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Reopened process target");
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        var original = cut.FindComponents<ProjectStructureCanvasDialogs>().Single(component => component.Instance.RenderProcessStartDialog).Instance;
        var submit = original.ExecuteProcessStart;
        var close = original.CloseProcessStart;
        var firstId = GetStartDialog(cut).DialogId;
        await cut.InvokeAsync(() => close.InvokeAsync());
        await OpenStartDialogAsync(cut, target);
        var nextId = GetStartDialog(cut).DialogId;
        Assert.NotEqual(firstId, nextId);
        await cut.InvokeAsync(() => submit.InvokeAsync());
        Assert.Empty(probe.ResolutionRequests);
        Assert.Equal(nextId, GetStartDialog(cut).DialogId);
    }

    [Fact]
    public async Task Actor_replacement_rejects_the_original_process_start_callback() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Original process actor");
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        var submit = cut.FindComponents<ProjectStructureCanvasDialogs>().Single(component => component.Instance.RenderProcessStartDialog).Instance.ExecuteProcessStart;
        await cut.InvokeAsync(() => {
            cut.Instance.InsightsAuthentication = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        });
        await cut.InvokeAsync(() => submit.InvokeAsync());
        Assert.Empty(probe.ResolutionRequests);
        Assert.Null(probe.QueuedRunId);
    }

    [Fact]
    public async Task Old_role_identity_does_not_invalidate_a_saved_review() {
        var probe = new ProcessLaunchProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var target = await CreateTargetAsync(harness, "Saved process roles");
        var cut = RenderProject(harness, target);
        await OpenStartDialogAsync(cut, target);
        await cut.Find("[data-testid='project-structure-process-start-continue']").ClickAsync(new MouseEventArgs());
        await PrepareReviewedAsync(cut);
        var before = GetStartDialog(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ProjectStructureProcessAssignmentDialog>().Instance.SelectProcessStartCandidate
            .InvokeAsync(new(Guid.NewGuid(), ProcessLaunchProbe.AlternateExecutorId)));
        var after = GetStartDialog(cut);
        Assert.Equal(before.PreparedRequest, after.PreparedRequest);
        Assert.Equal(before.LaunchIntentId, after.LaunchIntentId);
        Assert.Equal(before.Roles, after.Roles);
    }

    [Fact]
    public async Task Duplicate_process_link_dispatch_retains_one_original_native_receipt() {
        var probe = new ProcessLaunchProbe();
        var gate = new ProcessLinkSaveGate();
        await using var harness = await CreateHarnessAsync(probe, gate);
        var target = await CreateTargetAsync(harness, "One process link", linkDefinition: false);
        var cut = RenderProject(harness, target);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes, node => node.Id == target.TargetNodeId));
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(target.TargetNodeId, "add-process", 0, 0));
        await cut.WaitForElement($"[data-testid='project-structure-process-link-option-{DefinitionId:N}']").ClickAsync(new MouseEventArgs());
        var submit = cut.FindComponents<ProjectStructureCanvasDialogs>().Single(component => !component.Instance.RenderProcessStartDialog).Instance.ExecuteProcessLink;
        gate.ProjectId = target.ProjectId;
        var first = cut.InvokeAsync(() => submit.InvokeAsync());
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cut.InvokeAsync(() => submit.InvokeAsync());
        } finally {
            gate.Release.TrySetResult();
        }
        await first;
        var receipt = Assert.Single(cut.Instance.ProcessLinkReceipts);
        Assert.True(receipt.Committed);
        Assert.Equal(DefinitionId, receipt.DefinitionId);
        var surface = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(target.ProjectId);
        Assert.Single(surface.Links, link => link.SourceId == target.TargetNodeId && link.TargetId == target.ProcessNodeId);
    }
}
