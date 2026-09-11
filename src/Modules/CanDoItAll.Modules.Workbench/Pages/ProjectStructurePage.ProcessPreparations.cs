using System.Collections.Frozen;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private async Task<bool> TryRestoreProcessStartAsync(ProjectStructureProcessStartDialogState dialog,
        ProcessLaunchAuthority caller, string storageKey) {
        if (IsCurrentProcessStart(dialog)) {
            processStartDialog = dialog with { IntentStorageKey = storageKey };
        }
        var stored = await JSRuntime.InvokeAsync<string?>("sessionStorage.getItem", storageKey);
        if (string.IsNullOrEmpty(stored)) {
            return false;
        }
        if (!Guid.TryParse(stored, out var intentId) || intentId == Guid.Empty) {
            throw new InvalidOperationException("The browser process launch identity is invalid and requires reconciliation.");
        }
        var saved = await ProcessLaunchPreparations.FindByIntentAsync(new(intentId))
            ?? throw new InvalidOperationException($"Process launch intent {intentId:D} has no observable preparation yet. Reopen this dialog after the original request completes.");
        var request = ProcessLaunchProducerRequests.Restore(saved, caller, execute: true);
        if (request.ProjectId != dialog.ProjectId || request.ProjectNodeId != dialog.TargetNodeId ||
                request.ProcessDefinitionId?.Value != dialog.ProcessDefinitionId) {
            throw new InvalidOperationException("The retained process preparation belongs to another Structure selection.");
        }
        var metadata = await LoadProcessStartAgentMetadataAsync(CancellationToken.None);
        if (!IsCurrentProcessStart(dialog)) {
            return true;
        }
        processStartAgentMetadataById = metadata;
        var accepted = saved.AcceptedAtUtc is not null;
        processStartDialog = MapProcessStartDialogState(dialog, saved.Preparation.Review,
            accepted ? $"Process {saved.Preparation.InitialCommit.Mutation.State.RunId.Value:D} was already accepted. Its retained launch can be observed or resumed."
                : "The saved launch plan was restored. Review these exact assignments before starting.", string.Empty) with {
            LaunchIntentId = new(intentId),
            LaunchAuthority = request.Authority,
            LaunchLinkTarget = request.LinkTarget,
            LinkStartedRun = request.LinkTarget is not null,
            LaunchVariables = request.Variables.ToFrozenDictionary(StringComparer.Ordinal),
            PreparedRequest = request,
            IntentStorageKey = storageKey,
            LaunchObservation = new(saved.Preparation.AdmissionId,
                accepted ? saved.Preparation.InitialCommit.Mutation.State.RunId : null,
                saved.State, saved.LinkDeliveryState, saved.PublicFailure)
        };
        await InvokeAsync(StateHasChanged);
        return true;
    }

    private async Task PrepareAnotherProcessLaunchAsync() {
        if (processStartDialog is not { IsBusy: false, IntentStorageKey: { } storageKey } dialog || !IsCurrentProcessStart(dialog)) {
            return;
        }
        await JSRuntime.InvokeVoidAsync("sessionStorage.removeItem", storageKey);
        if (!IsCurrentProcessStart(dialog)) {
            return;
        }
        var target = ResolveNode(dialog.TargetNodeId);
        if (target is null) {
            processStartDialog = dialog with { Error = "The original launch target was removed. Select an existing target for a new launch." };
            return;
        }
        await OpenProcessDialogAsync(dialog.ProcessDefinitionId, dialog.NodeId, dialog.NodeTitle, target, dialog.EstimateOnlyMode);
    }

    private async Task PrepareReviewedProcessStartAsync(ProjectStructureProcessStartDialogState dialog) {
        if (!IsCurrentProcessStart(dialog)) {
            return;
        }
        try {
            var authority = dialog.LaunchAuthority
                ?? throw new InvalidOperationException("The process dialog has no captured launch authority. Reopen it before preparing a launch.");
            var storageKey = dialog.IntentStorageKey
                ?? throw new InvalidOperationException("The process dialog has no retained caller intent location.");
            var request = dialog.PreparedRequest ?? CreateProcessLaunchRequest(dialog, execute: false, runReadiness: true) with {
                CallerIntentId = dialog.LaunchIntentId,
                Authority = authority,
                ProjectAdmission = authority.ProjectAdmission,
                LinkTarget = dialog.LaunchLinkTarget
            };
            processStartDialog = dialog with {
                IsBusy = true,
                PreparedRequest = request,
                AssignmentsReviewed = false,
                Error = string.Empty,
                StatusMessage = "Saving the selected launch plan for review."
            };
            await InvokeAsync(StateHasChanged);
            await JSRuntime.InvokeVoidAsync("sessionStorage.setItem", storageKey, dialog.LaunchIntentId.Value.ToString("D"));
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }
            using var timeout = new CancellationTokenSource(ProcessStartPreviewTimeout);
            var metadata = await LoadProcessStartAgentMetadataAsync(timeout.Token);
            var prepared = await ProcessLaunchService.PreviewAsync(request, timeout.Token);
            if (!IsCurrentProcessStart(dialog)) {
                return;
            }
            processStartAgentMetadataById = metadata;
            var admission = prepared.Observation?.AdmissionId;
            processStartDialog = MapProcessStartDialogState(dialog, prepared.LaunchPlan,
                admission is null ? "Resolve the launch findings before preparing this intent again."
                    : "The launch plan is saved. Review these exact assignments, then select Start reviewed run.",
                string.Join(" ", prepared.Warnings)) with {
                PreparedRequest = request with { PreparedAdmissionId = admission },
                LaunchObservation = prepared.Observation
            };
            QueueProcessStartHistoricalEstimateRefresh(prepared.LaunchPlan, processStartEstimateAssignmentCount);
            await InvokeAsync(StateHasChanged);
        } catch (Exception exception) {
            await SetProcessActionExceptionAsync(exception, "saving the reviewed process launch", dialog);
        }
    }
}
