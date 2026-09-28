using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string RuntimePreviewActionId = "runtime:preview";
    private static readonly TimeSpan RuntimePreviewReadinessTimeout = TimeSpan.FromMinutes(2);
    private CancellationTokenSource? runtimePreviewWait;

    [Inject]
    private ProjectStructureRuntimeReadinessProbe RuntimeReadinessProbe { get; set; } = default!;

    private async Task LaunchRuntimeAsync(
        ProjectStructureNode node,
        ProjectStructureRuntimeLaunchMode mode)
    {
        var approval = ProjectStructureRuntimeLaunchApproval.NotGranted;
        var resolution = RuntimeLauncher.Resolve(node);
        if (resolution.Plan is { RequiresApproval: true } plan)
        {
            var confirmed = await DialogService.OpenAsync<ProjectStructureRuntimeLaunchApprovalDialog>(
                "Approve script launch?",
                new Dictionary<string, object?>
                {
                    [nameof(ProjectStructureRuntimeLaunchApprovalDialog.DisplayName)] = plan.DisplayName
                },
                new DialogOptions
                {
                    Eyebrow = "Runtime policy",
                    Subtitle = "Explicit shell scripts require confirmation for every launch.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    TestId = "project-structure-runtime-launch-approval-dialog",
                    AriaLabel = "Confirm explicit runtime script launch",
                    ChromeCloseResult = false
                });
            if (confirmed is not true)
            {
                workflowFeedback = "The explicit script launch was not approved.";
                workflowFeedbackTone = "warn";
                await InvokeAsync(StateHasChanged);
                return;
            }

            approval = ProjectStructureRuntimeLaunchApproval.OperatorConfirmed;
        }

        var result = await RuntimeLauncher.LaunchAsync(node, mode, approval);
        workflowFeedback = result.Message;
        workflowFeedbackTone = result.IsSuccess ? "mint" : "warn";
        await InvokeAsync(StateHasChanged);
        if (result.IsSuccess &&
            mode == ProjectStructureRuntimeLaunchMode.Direct &&
            TryResolveRuntimePreviewLink(node, out var link))
        {
            _ = OpenRuntimePreviewWhenServingAsync(node, link);
        }
    }

    private async Task<ProjectStructureRuntimeLaunchResult> StopRuntimeAsync(ProjectStructureNode node) {
        CancelRuntimePreviewWait();
        var result = await RuntimeLauncher.StopAsync(node);
        workflowFeedback = result.Message;
        workflowFeedbackTone = result.IsSuccess ? "mint" : "warn";
        await InvokeAsync(StateHasChanged);
        return result;
    }

    private async Task StopWebPreviewRuntimeAsync(string nodeId) {
        var dialog = webPreviewDialog;
        if (dialog is not { CanStopRuntime: true } ||
            !string.Equals(dialog.NodeId, nodeId, StringComparison.Ordinal) ||
            ResolveNode(nodeId) is not { } node) {
            return;
        }

        var result = await StopRuntimeAsync(node);
        if (!ReferenceEquals(webPreviewDialog, dialog)) {
            return;
        }

        webPreviewDialog = result.IsSuccess
            ? null
            : dialog with {
                CanStopRuntime = RuntimeLauncher.IsRunning(nodeId),
                RuntimeStopError = result.Message
            };
    }

    // A runtime node's web link is only useful while its process serves it; opening it earlier shows a
    // connection error in the embedded frame.
    private async Task OpenRuntimePreviewWhenServingAsync(ProjectStructureNode node, ProjectStructureWebLink link)
    {
        CancelRuntimePreviewWait();
        var wait = new CancellationTokenSource();
        runtimePreviewWait = wait;
        var projectId = ProjectId;
        var navigation = AgentChatNavigationFence;
        workflowFeedback = $"Started {node.Title}. The preview opens when {link.Uri.AbsoluteUri} responds.";
        workflowFeedbackTone = "mint";
        await InvokeAsync(StateHasChanged);

        ProjectStructureRuntimeReadiness readiness;
        try
        {
            readiness = await RuntimeReadinessProbe.WaitUntilServingAsync(
                RuntimeLauncher,
                node.Id,
                link.Uri,
                RuntimePreviewReadinessTimeout,
                wait.Token);
        }
        catch (OperationCanceledException) when (wait.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            Logger.LogWarning(exception, "Runtime preview readiness could not be observed. NodeId={NodeId} Url={Url}", node.Id, link.Uri);
            readiness = new(
                ProjectStructureRuntimeReadinessStatus.Stopped,
                $"The runtime started, but its readiness at {link.Uri.AbsoluteUri} could not be checked. Open the preview manually.");
        }
        finally
        {
            if (ReferenceEquals(runtimePreviewWait, wait))
            {
                runtimePreviewWait = null;
            }

            wait.Dispose();
        }

        if (ProjectId != projectId || AgentChatNavigationFence != navigation)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            if (readiness.IsServing)
            {
                OpenWebPreviewDialog(ResolveNode(node.Id) ?? node, link);
            }

            workflowFeedback = readiness.IsServing
                ? $"{node.Title} is serving {link.Uri.AbsoluteUri}."
                : readiness.Message;
            workflowFeedbackTone = readiness.IsServing ? "mint" : "warn";
            StateHasChanged();
        });
    }

    private void CancelRuntimePreviewWait()
    {
        var wait = runtimePreviewWait;
        runtimePreviewWait = null;
        wait?.Cancel();
    }

    private static bool TryResolveRuntimePreviewLink(ProjectStructureNode node, out ProjectStructureWebLink link)
    {
        link = default!;
        if (!ProjectStructureNodeActionCapabilityResolver.IsRuntimeCapable(node) ||
            !ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? resolved) ||
            resolved is not { Uri.IsLoopback: true })
        {
            return false;
        }

        link = resolved;
        return true;
    }
}
