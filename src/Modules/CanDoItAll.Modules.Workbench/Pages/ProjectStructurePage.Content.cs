using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Content.UI.Analysis;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    [Inject] private CanDoItAll.Infrastructure.ControlPlane.ICanonicalRuntimeDatabase ContentDatabase { get; set; } = default!;
    private ProjectStructureAuthoringOpening? summaryOpening;
    private ProjectStructureAuthoringOpening? transcriptOpening;
    private readonly Dictionary<(ProjectWriteAdmission Project, string Node, ProjectStructureAuthoringOperation Action), ProjectStructureAuthoringOpening> contentActions = [];

    private async Task OpenSummaryAsync(string? nodeId = null, ProjectStructureActionContext? capturedContext = null) {
        var context = capturedContext ?? CaptureActionContext();
        var node = context.Surface.Nodes.FirstOrDefault(node => node.Id == (nodeId ?? selectedNode?.Id));
        if (!IsCurrentAction(context) || node is null) {
            return;
        }
        summaryOpening = new(context, node);
        summaryDialog = new(node.Id, node.Title, ProjectStructureSummaryBuilder.Build(context.Surface, node)) {
            OpeningId = summaryOpening.Id
        };
        await InvokeAsync(StateHasChanged);
    }

    private void CloseSummary() {
        summaryOpening = null;
        summaryDialog = null;
    }

    private Task ChangeSummaryStatusAsync(string nodeId, ChangeEventArgs args)
        => RunSummaryActionAsync(summaryOpening, summaryDialog, ProjectStructureAuthoringOperation.SummaryStatus, nodeId, args.Value?.ToString());

    private async Task RunSummaryActionAsync(ProjectStructureAuthoringOpening? opening, ProjectStructureSummaryDialogState? snapshot,
        ProjectStructureAuthoringOperation action, string? nodeId = null, string? status = null) {
        if (opening is null || snapshot is null || snapshot.OpeningId != opening.Id || !TryBeginAuthoring(opening, summaryOpening)) {
            return;
        }
        var context = opening.Context;
        var anchor = DateOnly.FromDateTime(DateTime.UtcNow);
        var outcome = NewContentOutcome(opening, action);
        var invoked = false;
        summaryDialog = summaryDialog! with { IsBusy = true, Message = string.Empty };
        try {
            if (action == ProjectStructureAuthoringOperation.SummaryStatus) {
                var row = context.Surface.Nodes.FirstOrDefault(node => node.Id == nodeId);
                if (row is null || !snapshot.Summary.Rows.Any(item => item.NodeId == row.Id) || !SummaryStatusOptions.Contains(status)) {
                    throw new ArgumentException("The original summary row or status is unavailable.");
                }
                invoked = true;
                var accepted = await ProjectWorkbenchService.UpdateContentStatusesAsync(context.Admission, [row], status!);
                if (accepted.Count == 0) {
                    throw new ProjectStructureEditConflictException();
                }
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Nodes = accepted,
                    Message = "The original summary status was saved." };
                RecordAuthoringOutcome(opening, outcome);
                if (IsCurrentAuthoring(opening, summaryOpening)) {
                    await ApplySurfaceNodeUpdatesAsync(accepted);
                }
            } else {
                ProjectObjectMediaPayload media;
                string subtype;
                string title;
                string metadata;
                if (action == ProjectStructureAuthoringOperation.ExportWorkbook) {
                    media = new($"{SanitizeExportName(snapshot.RootTitle)}-progress-summary.xlsx",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Convert.ToBase64String(ProjectStructureSummaryExporter.BuildWorkbook(snapshot.Summary)));
                    subtype = "excel";
                    title = $"{snapshot.RootTitle} progress workbook";
                    metadata = "{}";
                } else if (action == ProjectStructureAuthoringOperation.ExportGantt) {
                    media = await AssetCreationService.CreateTextAsync(ProjectFileSubtype.Mermaid,
                        $"{SanitizeExportName(snapshot.RootTitle)}-progress-summary.mmd",
                        ProjectStructureSummaryExporter.BuildMermaidGantt(snapshot.Summary, anchor), deferredCompletionCts.Token);
                    subtype = "mermaid";
                    title = $"{snapshot.RootTitle} gantt";
                    metadata = ProjectObjectMetadataSerializer.Serialize(new() {
                        File = new() { FileSubtype = ProjectFileSubtype.Mermaid, MermaidDiagramKind = MermaidDiagramKind.Gantt }
                    });
                } else {
                    throw new ArgumentException("Unknown summary export.");
                }
                if (!HasOriginalContentAuthority(context)) {
                    throw new ProjectStructureEditConflictException();
                }
                invoked = true;
                var created = await ProjectWorkbenchService.CreateObjectAsync(context.Surface.ProjectId,
                    new(ProjectObjectType.File, title, "Progress summary export", "Generated from the structure progress summary modal.",
                        snapshot.RootNodeId, ObjectSubtype: subtype, Media: media, MetadataJson: metadata) {
                        ExpectedProjectAdmission = context.Admission, ExpectedParticipants = [opening.Node]
                    }, deferredCompletionCts.Token);
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = created,
                    Message = $"{created.Title} was saved ({created.Id})." };
                RecordAuthoringOutcome(opening, outcome);
                if (IsCurrentAuthoring(opening, summaryOpening)) {
                    await RefreshCreatedActionAsync(context, created);
                }
            }
        } catch (Exception failure) {
            outcome = ContentFailure(opening, outcome, invoked, failure);
            RecordAuthoringOutcome(opening, outcome);
        } finally {
            opening.IsBusy = false;
            if (ReferenceEquals(opening, summaryOpening) && summaryDialog is not null) {
                summaryDialog = summaryDialog with { IsBusy = false, RequiresObservation = opening.RequiresObservation, Message = outcome.Message };
            }
            await RenderAuthoringOutcomeAsync();
        }
    }

    private ProjectStructureAuthoringOpening? BeginContentAction(ProjectStructureActionContext context, ProjectStructureNode node,
        ProjectStructureAuthoringOperation action) {
        if (!IsCurrentAction(context)) {
            return null;
        }
        var key = (context.Admission, node.Id, action);
        if (contentActions.TryGetValue(key, out var existing) && (existing.IsBusy || existing.RequiresObservation)) {
            return null;
        }
        var opening = new ProjectStructureAuthoringOpening(context, node) { IsBusy = true };
        contentActions[key] = opening;
        return opening;
    }

    private async Task ExportMindmapImageAsync(ProjectStructureNode? sourceNode = null, ProjectStructureActionContext? capturedContext = null) {
        var target = sourceNode ?? selectedNode;
        var canvas = workbenchRef;
        if (target is null || canvas is null) {
            return;
        }
        var context = capturedContext ?? CaptureActionContext();
        var opening = BeginContentAction(context, target, ProjectStructureAuthoringOperation.ExportCanvasImage);
        if (opening is null) {
            return;
        }
        var outcome = NewContentOutcome(opening, ProjectStructureAuthoringOperation.ExportCanvasImage);
        var invoked = false;
        try {
            var base64 = await canvas.CaptureImageAsync();
            if (string.IsNullOrWhiteSpace(base64) || !HasOriginalContentAuthority(context)) {
                throw new ProjectStructureEditConflictException();
            }
            invoked = true;
            var created = await ProjectWorkbenchService.CreateObjectAsync(context.Surface.ProjectId,
                new(ProjectObjectType.ImageAsset, $"{target.Title} mindmap image", "Canvas export",
                    "Generated from the current structure canvas viewport.", target.Id, ObjectSubtype: "png",
                    Media: new($"{SanitizeExportName(target.Title)}-mindmap.png", "image/png", base64)) {
                    ExpectedProjectAdmission = context.Admission, ExpectedParticipants = [target]
                });
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = created,
                Message = $"{created.Title} was saved ({created.Id})." };
            RecordAuthoringOutcome(opening, outcome);
            await RefreshCreatedActionAsync(context, created);
        } catch (Exception failure) {
            RecordAuthoringOutcome(opening, ContentFailure(opening, outcome, invoked, failure));
        } finally {
            EndContentAction(opening, ProjectStructureAuthoringOperation.ExportCanvasImage);
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task CreateTranscriptFromRecordingAsync(ProjectStructureNode? recordingNode = null, ProjectStructureActionContext? capturedContext = null) {
        var target = recordingNode ?? selectedNode;
        if (target?.ObjectType != ProjectObjectType.Recording) {
            return;
        }
        var context = capturedContext ?? CaptureActionContext();
        var opening = BeginContentAction(context, target, ProjectStructureAuthoringOperation.CreateTranscript);
        if (opening is null) {
            return;
        }
        var outcome = NewContentOutcome(opening, ProjectStructureAuthoringOperation.CreateTranscript);
        try {
            var created = await ProjectWorkbenchService.CreateObjectAsync(context.Surface.ProjectId,
                new(ProjectObjectType.Transcript, $"{target.Title} transcript", "Generated from recording",
                    $"Transcript scaffold created from recording '{target.Title}'.", target.Id, target.X + 280, target.Y + 120,
                    MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() { Transcript = new() { TranscriptText = string.Empty } }),
                    NodeReferences: new() { TranscriptRecordingNodeId = TryParseCustomNodeArtifactId(target.Id) }) {
                    ExpectedProjectAdmission = context.Admission, ExpectedParticipants = [target]
                });
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.PartialCommit, Node = created,
                Message = $"Transcript scaffold {created.Id} was saved; its recording link is pending." };
            RecordAuthoringOutcome(opening, outcome);
            var link = await ProjectWorkbenchService.LinkObjectsDetailedAsync(context.Admission, target.Id, created.Id,
                ProjectObjectLinkKind.DerivedFrom, [target, created]);
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Link = link,
                Message = $"Transcript scaffold {created.Id} was saved and linked to its recording. No audio recognition was requested." };
            RecordAuthoringOutcome(opening, outcome);
            await RefreshCreatedActionAsync(context, created);
        } catch (Exception failure) {
            RecordAuthoringOutcome(opening, ContentFailure(opening, outcome, true, failure));
        } finally {
            EndContentAction(opening, ProjectStructureAuthoringOperation.CreateTranscript);
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task OpenTranscriptActionAsync(ProjectLlmActionKind actionKind, string? nodeId = null, ProjectStructureActionContext? capturedContext = null) {
        var context = capturedContext ?? CaptureActionContext();
        var node = context.Surface.Nodes.FirstOrDefault(node => node.Id == (nodeId ?? selectedNode?.Id));
        if (!IsCurrentAction(context) || node?.ObjectType != ProjectObjectType.Transcript || !Enum.IsDefined(actionKind)) {
            return;
        }
        var opening = new ProjectStructureAuthoringOpening(context, node);
        transcriptOpening = opening;
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        pendingTranscriptAction = new(node.Id, node.Title, actionKind, null,
            metadata.Transcript?.LastProviderName ?? string.Empty, [], string.Empty) {
            OpeningId = opening.Id, Phase = ContentConfirmationPhase.Loading
        };
        try {
            var providers = (await ProviderRuntimeProfileSource.ListProvidersAsync(deferredCompletionCts.Token))
                .Where(provider => provider.IsEnabled && provider.Purpose == ProviderProfilePurpose.Chat).ToArray();
            if (IsCurrentAuthoring(opening, transcriptOpening)) {
                pendingTranscriptAction = pendingTranscriptAction! with { Providers = providers,
                    SelectedProviderId = providers.FirstOrDefault(provider => provider.Id == node.NodeReferences?.TranscriptProviderProfileId)?.Id ?? providers.FirstOrDefault()?.Id,
                    Phase = ContentConfirmationPhase.Ready };
            }
        } catch (Exception failure) {
            LogContentFailure(opening, ProjectStructureAuthoringOperation.TranscriptAnalysis, failure);
            if (IsCurrentAuthoring(opening, transcriptOpening)) {
                pendingTranscriptAction = pendingTranscriptAction! with { Phase = ContentConfirmationPhase.Ready,
                    Error = "Provider choices could not be loaded. Close and reopen this confirmation." };
            }
        }
        await RenderAuthoringOutcomeAsync();
    }

    private void CancelTranscriptAction() {
        pendingTranscriptAction = null;
        transcriptOpening = null;
    }

    private Task ExecuteTranscriptActionAsync() => ExecuteTranscriptActionAsync(transcriptOpening, pendingTranscriptAction);

    private async Task ExecuteTranscriptActionAsync(ProjectStructureAuthoringOpening? opening, ProjectStructureTranscriptActionDialogState? dialog) {
        if (opening is null || dialog is null || dialog.OpeningId != opening.Id || dialog.Phase != ContentConfirmationPhase.Ready ||
            !TryBeginAuthoring(opening, transcriptOpening)) {
            return;
        }
        var context = opening.Context;
        var node = opening.Node;
        var outcome = NewContentOutcome(opening, ProjectStructureAuthoringOperation.TranscriptAnalysis);
        try {
            var provider = dialog.Providers.FirstOrDefault(provider => provider.Id == dialog.SelectedProviderId)
                ?? throw new ArgumentException("Select an available provider before sending this transcript.");
            var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
            var transcriptText = string.IsNullOrWhiteSpace(metadata.Transcript?.TranscriptText) ? node.Notes : metadata.Transcript.TranscriptText;
            if (string.IsNullOrWhiteSpace(transcriptText)) {
                throw new ArgumentException("Transcript text is required before running an LLM action.");
            }
            await ProjectWorkbenchService.RequireContentCurrentAsync(context.Admission, node, deferredCompletionCts.Token);
            if (!HasOriginalContentAuthority(context)) {
                throw new ProjectStructureEditConflictException();
            }
            pendingTranscriptAction = dialog with { Phase = ContentConfirmationPhase.Submitting, Error = string.Empty };
            opening.RequiresObservation = true;
            outcome = outcome with { ProviderId = provider.Id, ExternalEffect = ProjectStructureExternalEffectState.Dispatched };
            var result = await ProviderPromptExecutionService.ExecuteAsync(new(provider.Id,
                BuildTranscriptPrompt(dialog.ActionKind, node.Title, transcriptText), ModelOverride: provider.DefaultModel,
                OutputFormat: "Markdown"), deferredCompletionCts.Token);
            if (result.IsFailure || result.Value is null) {
                throw new InvalidOperationException("The provider did not return an accepted result. Observe the original request before sending it again.");
            }
            outcome = outcome with { ExternalEffect = ProjectStructureExternalEffectState.Completed };
            if (!HasOriginalContentAuthority(context)) {
                throw new ProjectStructureEditConflictException();
            }
            metadata.Transcript ??= new();
            metadata.Transcript.TranscriptText = transcriptText;
            metadata.Transcript.LastActionKind = dialog.ActionKind;
            metadata.Transcript.LastProviderName = result.Value.ProviderName;
            metadata.Transcript.LastGeneratedAtUtc = DateTimeOffset.UtcNow;
            var references = node.NodeReferences?.Clone() ?? new();
            references.TranscriptProviderProfileId = provider.Id;
            var output = result.Value.OutputText.Trim();
            switch (dialog.ActionKind) {
                case ProjectLlmActionKind.Summarize:
                    metadata.Transcript.SummaryText = output;
                    break;
                case ProjectLlmActionKind.FindMyTasks:
                    metadata.Transcript.MyTasksText = output;
                    break;
                case ProjectLlmActionKind.FindOthersDeliveries:
                    metadata.Transcript.OthersDeliveriesText = output;
                    break;
                default:
                    throw new ArgumentException("Unknown transcript action.");
            }
            var updated = await ProjectWorkbenchService.UpdateContentMetadataAsync(context.Admission, node,
                ProjectObjectMetadataSerializer.SerializePreservingUnknownProperties(node.MetadataJson, metadata), "Review", references, deferredCompletionCts.Token)
                ?? throw new ProjectStructureEditConflictException();
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = updated,
                Message = $"{ResolveTranscriptActionLabel(dialog.ActionKind)} was saved for the original transcript." };
            RecordAuthoringOutcome(opening, outcome);
            if (IsCurrentAuthoring(opening, transcriptOpening)) {
                pendingTranscriptAction = dialog with { Phase = ContentConfirmationPhase.Completed, Error = outcome.Message };
                await ApplySurfaceNodeUpdatesAsync([updated]);
            }
        } catch (Exception failure) {
            outcome = ContentFailure(opening, outcome, outcome.ExternalEffect != ProjectStructureExternalEffectState.NotStarted, failure);
            RecordAuthoringOutcome(opening, outcome);
            if (IsCurrentAuthoring(opening, transcriptOpening)) {
                pendingTranscriptAction = dialog with { Error = outcome.Message,
                    Phase = opening.RequiresObservation ? ContentConfirmationPhase.ObservationRequired : ContentConfirmationPhase.Ready };
            }
        } finally {
            opening.IsBusy = false;
            await RenderAuthoringOutcomeAsync();
        }
    }

    private void HandleTranscriptProviderChanged(ChangeEventArgs args) {
        if (pendingTranscriptAction is not { Phase: ContentConfirmationPhase.Ready } dialog || transcriptOpening is not { IsBusy: false, RequiresObservation: false }) {
            return;
        }
        pendingTranscriptAction = dialog with { SelectedProviderId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : null, Error = string.Empty };
    }

    private static ProjectStructureAuthoringOutcome NewContentOutcome(ProjectStructureAuthoringOpening opening, ProjectStructureAuthoringOperation operation)
        => new(opening.Id, Guid.NewGuid(), opening.Context.Admission, operation, ProjectStructureAuthoringResultKind.Rejected,
            "The original content action was not accepted.") { SourceNodeId = opening.Node.Id };

    private ProjectStructureAuthoringOutcome ContentFailure(ProjectStructureAuthoringOpening opening,
        ProjectStructureAuthoringOutcome outcome, bool invoked, Exception failure) {
        LogContentFailure(opening, outcome.Operation, failure);
        var saved = outcome.Node is not null || outcome.Nodes.Count > 0;
        opening.RequiresObservation |= saved && outcome.Kind == ProjectStructureAuthoringResultKind.PartialCommit ||
            outcome.ExternalEffect != ProjectStructureExternalEffectState.NotStarted || invoked && !IsKnownGraphRejection(failure);
        return outcome with {
            Kind = saved ? outcome.Kind : outcome.ExternalEffect == ProjectStructureExternalEffectState.Completed
                ? ProjectStructureAuthoringResultKind.PartialCommit : opening.RequiresObservation
                    ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Rejected,
            Message = saved ? $"{outcome.Message} The follow-up did not finish. Observe the saved result before repeating this action. Original lifetime: {opening.Context.Admission.LifetimeId:D}."
                : outcome.ExternalEffect == ProjectStructureExternalEffectState.Completed
                    ? $"The provider request completed. Saving its result to the original target was refused or could not be confirmed. Do not resend this request. Original lifetime: {opening.Context.Admission.LifetimeId:D}."
                    : opening.RequiresObservation ? "The original action is unconfirmed. Observe its outcome before repeating it."
                        : $"The original target, content revision or input is no longer valid. Reopen the action from the current native state. Original lifetime: {opening.Context.Admission.LifetimeId:D}."
        };
    }

    private void LogContentFailure(ProjectStructureAuthoringOpening opening, ProjectStructureAuthoringOperation action, Exception failure)
        => Logger.LogWarning("Content action {Action} for project {ProjectId}, lifetime {LifetimeId}, opening {OpeningId} stopped with {FailureType}.",
            action, opening.Context.Admission.ProjectId, opening.Context.Admission.LifetimeId, opening.Id, failure.GetType().Name);

    private void EndContentAction(ProjectStructureAuthoringOpening opening, ProjectStructureAuthoringOperation action) {
        opening.IsBusy = false;
        if (!opening.RequiresObservation) {
            contentActions.Remove((opening.Context.Admission, opening.Node.Id, action));
        }
    }

    private bool HasOriginalContentAuthority(ProjectStructureActionContext context)
        => !deferredCompletionCts.IsCancellationRequested && ReferenceEquals(context.Actor, InsightsAuthentication) &&
            ContentDatabase.Profile.Profile.Id == context.Admission.DatabaseProfileId && ContentDatabase.Generation == context.RuntimeGeneration;
}
