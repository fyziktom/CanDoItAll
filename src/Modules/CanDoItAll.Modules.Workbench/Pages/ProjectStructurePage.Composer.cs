using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private sealed record ProjectStructureComposerOpening(
        ProjectStructureAuthoringOpening Ownership,
        CanvasWorkbenchCreateActionRequest? Create,
        CanvasWorkbenchNodeEditRequest? Edit);

    private ProjectStructureComposerOpening? composerOpening;
    private readonly Dictionary<Guid, ProjectStructureComposerOpening> composerOpenings = [];
    private readonly HashSet<Guid> seenComposerOpenings = [];
    private ProjectStructureActionContext? composerContext;

    private void CaptureComposer(CanvasWorkbenchComposerOpening opening) {
        if (composerContext is not null && !IsCurrentAction(composerContext)) {
            composerOpenings.Clear();
            seenComposerOpenings.Clear();
        }
        if (opening.OpeningId == Guid.Empty || !seenComposerOpenings.Add(opening.OpeningId)) {
            return;
        }
        var context = CaptureActionContext();
        composerContext = context;
        var nodeId = opening.EditRequest?.NodeId ?? opening.CreateRequest?.SourceNodeId ?? opening.CreateRequest?.ParentNodeId;
        var node = context.Surface.Nodes.FirstOrDefault(item => item.Id == nodeId)
            ?? (nodeId is null ? context.Surface.Nodes.FirstOrDefault(item => item.ProjectRole == ProjectStructureProjectRole.ActiveProject) : null);
        if (node is null) {
            composerOpening = null;
            return;
        }
        var create = opening.CreateRequest;
        if (create is not null && !TryResolveEditAction(create.ActionId, out _)) {
            create = create with { ParentNodeId = CanvasAdapters.ProjectStructurePlacementPolicy.ResolveParentNodeId(node, create) ?? node.Id };
        }
        composerOpening = new(new(context, node, opening.OpeningId), create, opening.EditRequest);
        composerOpenings.Add(opening.OpeningId, composerOpening);
    }

    private async Task SubmitComposerAsync(CanvasWorkbenchCreateActionRequest? create, CanvasWorkbenchNodeEditRequest? edit) {
        var id = create?.ComposerOpeningId ?? edit?.ComposerOpeningId;
        if (id is not { } openingId || !composerOpenings.Remove(openingId, out var captured) ||
            !IsCurrentAction(captured.Ownership.Context)) {
            return;
        }
        captured.Ownership.IsBusy = true;
        var operation = ExecuteComposerAsync(captured, create, edit);
        authoringOperations.Add(operation);
        try {
            await operation;
        } finally {
            authoringOperations.Remove(operation);
        }
    }

    private async Task ExecuteComposerAsync(ProjectStructureComposerOpening captured,
        CanvasWorkbenchCreateActionRequest? create, CanvasWorkbenchNodeEditRequest? edit) {
        var opening = captured.Ownership;
        var context = opening.Context;
        var target = opening.Node;
        var submissionId = Guid.NewGuid();
        var operation = edit is not null || captured.Create?.PlacementKind == "edit"
            ? ProjectStructureAuthoringOperation.EditNode : ProjectStructureAuthoringOperation.CreateNode;
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, submissionId, context.Admission,
            operation, ProjectStructureAuthoringResultKind.Rejected, "The original editor request is no longer valid.") { SourceNodeId = target.Id };
        var invoked = false;
        try {
            ProjectStructureNode? result;
            if (edit is not null && captured.Edit is { } originalEdit && edit.NodeId == originalEdit.NodeId && target.ObjectType == ProjectObjectType.Note) {
                invoked = true;
                result = await ProjectWorkbenchService.UpdateObjectAsync(context.Surface.ProjectId, target.Id,
                    new(ProjectStructureNodeHelpers.BuildSimpleNoteTitle(edit.Notes), target.Subtitle, edit.Notes,
                        target.StartUtc, target.EndUtc, target.MetadataJson, target.DurationSeconds, target.NodeReferences) {
                        ExpectedProjectAdmission = context.Admission, ExpectedNode = target
                    });
            } else if (create is not null && captured.Create is { } original && create.ActionId == original.ActionId &&
                create.SourceNodeId == original.SourceNodeId && (create.ParentNodeId is null || create.ParentNodeId == original.ParentNodeId)) {
                var submitted = original with { Title = create.Title, Subtitle = create.Subtitle, Notes = create.Notes,
                    InputValues = create.InputValues?.Select(value => new CanvasWorkbenchInputValue { Key = value.Key, Value = value.Value }).ToArray(),
                    UploadedFile = create.UploadedFile };
                if (TryResolveEditAction(original.ActionId, out var actionId) &&
                    ProjectStructureCanvasCatalog.TryResolveCreateDefinition(actionId, out var definition)) {
                    var update = ProjectStructureNodeEditor.ComposeUpdate(definition, target, submitted) with {
                        ExpectedProjectAdmission = context.Admission, ExpectedNode = target
                    };
                    invoked = true;
                    result = await ProjectWorkbenchService.UpdateObjectAsync(context.Surface.ProjectId, target.Id, update);
                } else if (ProjectStructureCanvasCatalog.TryResolveCreateDefinition(original.ActionId, out definition)) {
                    if (IsGeneratedImageAssetCreateAction(original.ActionId)) {
                        await TryCreateGeneratedImageAssetAsync(definition, submitted, context);
                        return;
                    }
                    if (IsTaskCreateAction(original.ActionId) || IsSecretReferenceCreateAction(original.ActionId) ||
                        ProjectStructureCanvasCatalog.IsTextAssetAuthoringDefinition(definition)) {
                        await HandleCreateActionAsync(submitted with { ComposerOpeningId = null });
                        return;
                    }
                    invoked = true;
                    result = await CreateObjectAsync(definition, submitted,
                        configureRequest: request => request with { ExpectedProjectAdmission = context.Admission,
                            ExpectedParticipants = context.Surface.Nodes.Where(node => node.Id == target.Id || node.Id == submitted.ParentNodeId).ToArray() },
                        onNodeCommitted: node => {
                            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = node, Message = $"{node.Title} was created ({node.Id})." };
                            RecordAuthoringOutcome(opening, outcome);
                        }, capturedSurface: context.Surface, capturedNavigationRevision: context.NavigationRevision,
                        canPublish: () => ReferenceEquals(composerOpening, captured));
                } else {
                    RecordAuthoringOutcome(opening, outcome);
                    return;
                }
            } else {
                RecordAuthoringOutcome(opening, outcome);
                return;
            }
            if (result is null) {
                outcome = outcome with { Message = "The original node is no longer available." };
            } else if (outcome.Node is null) {
                outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = result, Message = $"{result.Title} was updated." };
            }
            if (operation == ProjectStructureAuthoringOperation.EditNode || result is null) {
                RecordAuthoringOutcome(opening, outcome);
            }
            if (result is not null && IsCurrentAction(context) && ReferenceEquals(composerOpening, captured)) {
                try {
                    await ApplySurfaceNodeUpdatesAsync([result]);
                } catch (Exception failure) {
                    RecordAuthoringOutcome(opening, outcome with { Failure = failure,
                        Message = $"{outcome.Message} Readback failed. Reload the original project; do not repeat the write." });
                }
            }
        } catch (Exception failure) {
            opening.RequiresObservation = invoked && failure is not (ArgumentException or InvalidDataException or
                ProjectStructureEditConflictException or ProjectWriteAdmissionRejectedException);
            RecordAuthoringOutcome(opening, outcome with {
                Kind = outcome.Node is not null ? ProjectStructureAuthoringResultKind.PartialCommit :
                    opening.RequiresObservation ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Rejected,
                Failure = failure,
                Message = outcome.Node is not null ? $"{outcome.Message} Follow-up failed. Observe the original node before continuing." :
                    opening.RequiresObservation ? "The original write is unconfirmed. Observe the original project before trying again." : failure.Message
            });
        } finally {
            if (!opening.RequiresObservation && ReferenceEquals(composerOpening, captured)) {
                composerOpening = null;
            }
            await RenderAuthoringOutcomeAsync();
        }
    }
}
