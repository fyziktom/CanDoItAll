using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private const string RuntimePreviewActionId = "runtime:preview";
    private static readonly TimeSpan RuntimePreviewReadinessTimeout = TimeSpan.FromMinutes(2);
    private RuntimePreviewObservation? runtimePreviewWait;
    private readonly HashSet<(Guid Lifetime, string Node)> pendingRuntimeLaunches = [];
    private readonly HashSet<WorkspaceOwnedProcessIdentity> pendingRuntimeStops = [];
    private readonly Queue<ProjectStructureRuntimeLaunchResult> runtimeOutcomes = new();
    internal IReadOnlyList<ProjectStructureRuntimeLaunchResult> RuntimeOutcomes => runtimeOutcomes.ToArray();
    internal Task RuntimeObservation { get; private set; } = Task.CompletedTask;

    private sealed record RuntimePreviewObservation(ProjectStructureActionContext Context, ProjectStructureNode Node,
        WorkspaceOwnedProcessIdentity Identity, Uri Url, long SelectionRevision, CancellationTokenSource Cancellation);

    [Inject] private ProjectStructureRuntimeReadinessProbe RuntimeReadinessProbe { get; set; } = default!;

    private async Task LaunchRuntimeAsync(ProjectStructureNode node, ProjectStructureRuntimeLaunchMode mode,
        ProjectStructureActionContext? capturedContext = null) {
        var context = capturedContext ?? CaptureActionContext();
        var selection = insightsSelectionRevision;
        if (!IsCurrentAction(context) || !HasOriginalContentAuthority(context)) {
            return;
        }
        var key = (context.Admission.LifetimeId, node.Id);
        if (!pendingRuntimeLaunches.Add(key)) {
            return;
        }
        try {
            var resolution = RuntimeLauncher.Resolve(node);
            if (resolution.Plan is not { } plan) {
                RecordRuntimeOutcome(context, new(false, resolution.Message));
                return;
            }
            var launch = new ProjectRuntimeLaunchSession(context.Admission, node, plan, mode,
                () => IsCurrentAction(context) && HasOriginalContentAuthority(context) && insightsSelectionRevision == selection,
                ProjectWorkbenchService, RuntimeLauncher);
            var approval = ProjectStructureRuntimeLaunchApproval.NotGranted;
            if (launch.Plan.RequiresApproval) {
                var confirmed = await DialogService.OpenAsync<ProjectStructureRuntimeLaunchApprovalDialog>(
                    "Approve script launch?", new Dictionary<string, object?> {
                        [nameof(ProjectStructureRuntimeLaunchApprovalDialog.DisplayName)] = launch.Plan.DisplayName,
                        [nameof(ProjectStructureRuntimeLaunchApprovalDialog.Review)] = $"{mode}: {launch.Plan.DisplayCommand}\nWorking directory: {launch.Plan.WorkingDirectory}"
                    }, new DialogOptions {
                        Eyebrow = "Runtime policy", Subtitle = "Explicit shell scripts require confirmation for every launch.",
                        Size = ModalSize.Compact, DenseChrome = true,
                        TestId = "project-structure-runtime-launch-approval-dialog", AriaLabel = "Confirm explicit runtime script launch",
                        ChromeCloseResult = false
                    });
                if (confirmed is not true) {
                    RecordRuntimeOutcome(context, new(false, "The explicit script launch was not approved."));
                    return;
                }
                approval = ProjectStructureRuntimeLaunchApproval.OperatorConfirmed;
            }
            var result = await launch.LaunchAsync(approval, deferredCompletionCts.Token);
            RecordRuntimeOutcome(context, result);
            if (result.IsSuccess && result.Identity is { } accepted && mode == ProjectStructureRuntimeLaunchMode.Direct &&
                IsCurrentAction(context) && HasOriginalContentAuthority(context) && insightsSelectionRevision == selection &&
                TryResolveRuntimePreviewLink(node, out var link)) {
                RuntimeObservation = TrackAuthoringOperationAsync(OpenRuntimePreviewWhenServingAsync(context, node, accepted, link, selection));
            }
        } catch (Exception failure) {
            Logger.LogWarning(failure, "Runtime launch observation failed for original project {ProjectId}, lifetime {LifetimeId}, node {NodeId}.",
                context.Admission.ProjectId, context.Admission.LifetimeId, node.Id);
            RecordRuntimeOutcome(context, new(false, "No launch result was confirmed. Inspect the original runtime before repeating the action."));
        } finally {
            pendingRuntimeLaunches.Remove(key);
            await RenderAuthoringOutcomeAsync();
        }
    }

    private void RecordRuntimeOutcome(ProjectStructureActionContext context, ProjectStructureRuntimeLaunchResult result) {
        runtimeOutcomes.Enqueue(result);
        while (runtimeOutcomes.Count > 16) {
            runtimeOutcomes.Dequeue();
        }
        if (HasOriginalContentAuthority(context)) {
            ReportActionResult(context, result.Message, result.IsSuccess ? "mint" : "warn");
        }
        Logger.LogInformation("Runtime result for original project {ProjectId}, lifetime {LifetimeId}: accepted process {ProcessId}, observed {Observed}, successful {Successful}.",
            context.Admission.ProjectId, context.Admission.LifetimeId, result.Identity?.ProcessId, result.ObservationCompleted, result.IsSuccess);
    }

    private WorkspaceOwnedProcessIdentity? CurrentRuntimeIdentity(ProjectStructureNode node, ProjectStructureActionContext? context = null)
        => (context?.Admission ?? surface?.ExpectedProjectAdmission) is { } admission
            ? RuntimeLauncher.GetIdentity(node.Id, new(admission, node.RecordId)) : null;

    private Task<ProjectStructureRuntimeLaunchResult> StopRuntimeAsync(ProjectStructureNode node)
        => StopRuntimeAsync(node, CurrentRuntimeIdentity(node), CaptureActionContext());

    private async Task<ProjectStructureRuntimeLaunchResult> StopRuntimeAsync(ProjectStructureNode node,
        WorkspaceOwnedProcessIdentity? expected, ProjectStructureActionContext context) {
        if (expected is null || !IsCurrentAction(context) || !HasOriginalContentAuthority(context)) {
            return new(false, "The original owned session or its authority is unavailable. Reopen its runtime controls.");
        }
        if (!pendingRuntimeStops.Add(expected)) {
            return new(false, "This exact owned session is already being stopped.") { Identity = expected };
        }
        try {
            await InsightsAdmissions.RequireCurrentAsync(context.Admission, deferredCompletionCts.Token);
            if (!IsCurrentAction(context) || !HasOriginalContentAuthority(context)) {
                return new(false, "The original runtime authority changed before Stop.") { Identity = expected };
            }
            if (runtimePreviewWait?.Identity == expected) {
                CancelRuntimePreviewWait();
            }
            var result = await RuntimeLauncher.StopAsync(node.Id, expected, deferredCompletionCts.Token);
            RecordRuntimeOutcome(context, result);
            return result;
        } catch (Exception failure) {
            Logger.LogWarning(failure, "Stop could not be confirmed for original runtime node {NodeId}, process {ProcessId}.", node.Id, expected.ProcessId);
            var result = new ProjectStructureRuntimeLaunchResult(false, "Stop of the original process was not confirmed. Observe that exact session before retrying.") { Identity = expected };
            RecordRuntimeOutcome(context, result);
            return result;
        } finally {
            pendingRuntimeStops.Remove(expected);
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task StopWebPreviewRuntimeAsync(ProjectStructureWebPreviewDialogState? opening) {
        if (opening is not { CanStopRuntime: true, IsBusy: false, Context: { } context, OriginalNode: { } node, RuntimeIdentity: { } identity } ||
            webPreviewDialog?.OpeningId != opening.OpeningId || webPreviewDialog.IsBusy) {
            return;
        }
        webPreviewDialog = opening with { IsBusy = true, RuntimeStopError = string.Empty };
        var result = await StopRuntimeAsync(node, identity, context);
        if (webPreviewDialog?.OpeningId != opening.OpeningId) {
            return;
        }
        webPreviewDialog = result.IsSuccess && result.Identity == identity
            ? null
            : opening with { IsBusy = false, RuntimeStopError = result.Message };
    }

    private async Task OpenRuntimePreviewWhenServingAsync(ProjectStructureActionContext context, ProjectStructureNode node,
        WorkspaceOwnedProcessIdentity identity, ProjectStructureWebLink link, long selection) {
        CancelRuntimePreviewWait();
        var wait = new RuntimePreviewObservation(context, node, identity, link.Uri, selection,
            CancellationTokenSource.CreateLinkedTokenSource(deferredCompletionCts.Token));
        runtimePreviewWait = wait;
        if (IsCurrentRuntimeObservation(wait)) {
            ReportActionResult(context, $"Process {identity.ProcessId} was acquired. Waiting for {link.Uri.AbsoluteUri} to respond.");
            await RenderAuthoringOutcomeAsync();
        }
        try {
            var readiness = await RuntimeReadinessProbe.WaitUntilServingAsync(RuntimeLauncher, node.Id, identity,
                link.Uri, RuntimePreviewReadinessTimeout, wait.Cancellation.Token);
            await InvokeAsync(() => {
                if (!IsCurrentRuntimeObservation(wait)) {
                    return;
                }
                if (readiness.IsServing) {
                    webPreviewDialog = BuildWebPreviewDialog(node, link) with { RuntimeIdentity = identity, Context = context };
                }
                ReportActionResult(context, readiness.Message, readiness.IsServing ? "mint" : "warn");
                StateHasChanged();
            });
        } catch (OperationCanceledException) when (wait.Cancellation.IsCancellationRequested) {
        } catch (Exception failure) {
            Logger.LogWarning(failure, "Readiness observation failed for original runtime node {NodeId}, process {ProcessId}.", node.Id, identity.ProcessId);
            await InvokeAsync(() => {
                if (IsCurrentRuntimeObservation(wait)) {
                    ReportActionResult(context, $"Process {identity.ProcessId} was acquired, but readiness could not be observed. Its session has not been stopped.", "warn");
                    StateHasChanged();
                }
            });
        } finally {
            if (ReferenceEquals(runtimePreviewWait, wait)) {
                runtimePreviewWait = null;
            }
            wait.Cancellation.Dispose();
        }
    }

    private bool IsCurrentRuntimeObservation(RuntimePreviewObservation wait)
        => ReferenceEquals(runtimePreviewWait, wait) && !wait.Cancellation.IsCancellationRequested &&
            IsCurrentAction(wait.Context) && HasOriginalContentAuthority(wait.Context) && insightsSelectionRevision == wait.SelectionRevision &&
            RuntimeLauncher.GetIdentity(wait.Node.Id) == wait.Identity && ResolveNode(wait.Node.Id) is { } current &&
            current.RecordId == wait.Node.RecordId && current.MetadataJson == wait.Node.MetadataJson &&
            TryResolveRuntimePreviewLink(current, out var link) && link.Uri == wait.Url;

    private void CancelRuntimePreviewWait() {
        var wait = runtimePreviewWait;
        runtimePreviewWait = null;
        wait?.Cancellation.Cancel();
    }

    private static bool TryResolveRuntimePreviewLink(ProjectStructureNode node, out ProjectStructureWebLink link) {
        link = default!;
        if (!ProjectStructureNodeActionCapabilityResolver.IsRuntimeCapable(node) ||
            !ProjectStructureWebLinkResolver.TryResolve(node, out ProjectStructureWebLink? resolved) || resolved is not { Uri.IsLoopback: true }) {
            return false;
        }
        link = resolved;
        return true;
    }
}
