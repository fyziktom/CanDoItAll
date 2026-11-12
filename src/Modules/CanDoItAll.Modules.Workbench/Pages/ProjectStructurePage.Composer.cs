using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Structure.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private sealed record ProjectStructureComposerOpening(
        ProjectStructureAuthoringOpening Ownership,
        CanvasWorkbenchCreateActionRequest? Create,
        CanvasWorkbenchNodeEditRequest? Edit) {
        public ProjectStructureNodeEditingSession? Session { get; set; }
    }

    [Inject] private ProjectStructureNodeSnapshotReader NodeSnapshots { get; set; } = default!;

    private readonly List<IAsyncDisposable> retiredAuthoringSessions = [];
    private StructureComposerLeaveGuard? composerLeaveGuard;
    private ProjectStructureComposerOpening? composerOpening;
    private readonly Dictionary<Guid, ProjectStructureComposerOpening> composerOpenings = [];
    private readonly HashSet<Guid> seenComposerOpenings = [];
    private ProjectStructureActionContext? composerContext;

    private Task<CanvasWorkbenchComposerCloseRequest?> ReadComposerCloseRequestAsync()
        => workbenchRef?.ReadComposerCloseRequestAsync(StructureContextId) ?? Task.FromResult<CanvasWorkbenchComposerCloseRequest?>(null);

    private Task<bool> ConfirmComposerCloseAsync(CanvasWorkbenchComposerCloseRequest request)
        => composerLeaveGuard?.RequestLeaveAsync(request) ?? Task.FromResult(false);

    private async Task CaptureComposerAsync(CanvasWorkbenchComposerOpening opening) {
        await DisposeRetiredNodeSessionsAsync();
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
        if (composerOpening?.Session is { } previousSession) {
            await previousSession.DisposeAsync();
        }
        foreach (var previous in composerOpenings.Values) {
            if (previous.Session is { } session) {
                await session.DisposeAsync();
            }
        }
        composerOpenings.Clear();
        var captured = new ProjectStructureComposerOpening(new(context, node, opening.OpeningId), create, opening.EditRequest);
        composerOpening = captured;
        composerOpenings.Add(opening.OpeningId, captured);
        ProjectStructureNodeForm? form = null;
        if (opening.EditRequest is not null && node.ObjectType == ProjectObjectType.Note) {
            form = new(new([], inlineNote: true), null);
        } else if (create?.PlacementKind == "edit" && TryBuildNodeEditModel(node, out var model) &&
            TryResolveEditAction(create.ActionId, out var actionId) &&
            ProjectStructureCanvasCatalog.TryResolveCreateDefinition(actionId, out var definition)) {
            form = new(new(model.Action.InputFields, showDefaultFields: model.Action.ShowDefaultTextFields), definition);
        }
        if (form is not null) {
            var session = new ProjectStructureNodeEditingSession(opening.OpeningId, context.Admission, node, form,
                new EditingObservation(ProjectCreateChanges, ProjectCreateUiAccess,
                    new(ProjectCreateProjectChanges.Scope, EditingOwner.ProjectStructure), ProjectCreateObservationLogger),
                token => NodeSnapshots.ReadAsync(context.Admission, node.Id, token),
                () => ReferenceEquals(composerOpening, captured) && IsCurrentAction(context));
            captured.Session = session;
            if (!form.MatchesOpening(opening, node)) {
                session.ReportFailure("The displayed node changed while this editor was opening. Copy its retained text, then reopen the current node.");
                session.Retire();
                return;
            }
            await RunNodeComparisonAsync(session, () => session.OpenAsync(() => deferredCompletionCts.IsCancellationRequested
                ? Task.CompletedTask : InvokeAsync(StateHasChanged)));
        }
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
        var target = captured.Session?.Baseline ?? opening.Node;
        var submissionId = create?.ComposerSubmissionId ?? edit?.ComposerSubmissionId ?? Guid.NewGuid();
        var rendererContextId = StructureContextId;
        var handedOff = false;
        var operation = edit is not null || captured.Create?.PlacementKind == "edit"
            ? ProjectStructureAuthoringOperation.EditNode : ProjectStructureAuthoringOperation.CreateNode;
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, submissionId, context.Admission,
            operation, ProjectStructureAuthoringResultKind.Rejected, "The original editor request is no longer valid.") { SourceNodeId = target.Id };
        var invoked = false;
        try {
            if (operation == ProjectStructureAuthoringOperation.EditNode && captured.Session is null) {
                throw new InvalidOperationException("The original node editor has no current Structure access. Copy its text and reopen it.");
            }
            ProjectStructureNode? result;
            if (edit is not null && captured.Edit is { } originalEdit && edit.NodeId == originalEdit.NodeId && target.ObjectType == ProjectObjectType.Note) {
                if (captured.Session is { } session) {
                    await session.AuthorizeWriteAsync(deferredCompletionCts.Token);
                }
                invoked = true;
                result = await ProjectWorkbenchService.UpdateObjectAsync(context.Surface.ProjectId, target.Id,
                    new(edit.HasExplicitTitle ? edit.Title : ProjectStructureNodeHelpers.BuildSimpleNoteTitle(edit.Notes), target.Subtitle, edit.Notes,
                        target.StartUtc, target.EndUtc, target.MetadataJson, target.DurationSeconds, target.NodeReferences) {
                        ExpectedProjectAdmission = context.Admission, ExpectedNode = target
                    }, authorizeCommit: captured.Session is { } inlineSession ? inlineSession.AuthorizeWriteAsync : null);
            } else if (create is not null && captured.Create is { } original && create.ActionId == original.ActionId &&
                create.SourceNodeId == original.SourceNodeId && (create.ParentNodeId is null || create.ParentNodeId == original.ParentNodeId)) {
                var submitted = original with { Title = create.Title, Subtitle = create.Subtitle, Notes = create.Notes,
                    InputValues = create.InputValues?.Select(value => new CanvasWorkbenchInputValue { Key = value.Key, Value = value.Value }).ToArray(),
                    UploadedFile = create.UploadedFile };
                if (TryResolveEditAction(original.ActionId, out var actionId) &&
                    ProjectStructureCanvasCatalog.TryResolveCreateDefinition(actionId, out var definition)) {
                    var composed = captured.Session is { } nodeSession
                        ? nodeSession.Form.Compose(target, submitted) : ProjectStructureNodeEditor.ComposeUpdate(definition, target, submitted);
                    var update = composed with {
                        ExpectedProjectAdmission = context.Admission, ExpectedNode = target
                    };
                    if (captured.Session is { } session) {
                        await session.AuthorizeWriteAsync(deferredCompletionCts.Token);
                    }
                    invoked = true;
                    result = await ProjectWorkbenchService.UpdateObjectAsync(context.Surface.ProjectId, target.Id, update,
                        authorizeCommit: captured.Session is { } activeSession ? activeSession.AuthorizeWriteAsync : null);
                } else if (ProjectStructureCanvasCatalog.TryResolveCreateDefinition(original.ActionId, out definition)) {
                    if (IsGeneratedImageAssetCreateAction(original.ActionId)) {
                        await TryCreateGeneratedImageAssetAsync(definition, submitted, context);
                        handedOff = true;
                        return;
                    }
                    if (IsTaskCreateAction(original.ActionId) || IsSecretReferenceCreateAction(original.ActionId) ||
                        ProjectStructureCanvasCatalog.IsTextAssetAuthoringDefinition(definition)) {
                        await HandleCreateActionAsync(submitted with { ComposerOpeningId = null });
                        handedOff = true;
                        return;
                    }
                    invoked = true;
                    result = await CreateObjectAsync(context, definition, submitted,
                        configureRequest: request => request with { ExpectedProjectAdmission = context.Admission,
                            ExpectedParticipants = context.Surface.Nodes.Where(node => node.Id == target.Id || node.Id == submitted.ParentNodeId).ToArray() },
                        onNodeCommitted: node => {
                            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, Node = node, Message = $"{node.Title} was created ({node.Id})." };
                            RecordAuthoringOutcome(opening, outcome);
                        },
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
                    outcome = outcome with { Failure = failure,
                        Message = $"{outcome.Message} Readback failed. Reload the original project; do not repeat the write." };
                    RecordAuthoringOutcome(opening, outcome);
                }
            }
        } catch (Exception failure) {
            opening.RequiresObservation = outcome.Node is not null || invoked && failure is not (ArgumentException or InvalidDataException or
                ProjectStructureEditConflictException or ProjectWriteAdmissionRejectedException or UnauthorizedAccessException or
                ProjectStructureAgentException { EffectState: AgentToolEffectState.None or AgentToolEffectState.NotCommitted });
            outcome = outcome with {
                Kind = outcome.Node is not null ? ProjectStructureAuthoringResultKind.PartialCommit :
                    opening.RequiresObservation ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Rejected,
                Failure = failure,
                Message = outcome.Node is not null ? $"{outcome.Message} Follow-up failed. Observe the original node before continuing." :
                    opening.RequiresObservation ? "The original write is unconfirmed. Observe the original project before trying again." : failure.Message
            };
            RecordAuthoringOutcome(opening, outcome);
        } finally {
            if (!opening.RequiresObservation && outcome.Kind != ProjectStructureAuthoringResultKind.Rejected &&
                ReferenceEquals(composerOpening, captured)) {
                captured.Session?.Retire();
            }
            await RenderAuthoringOutcomeAsync();
            if (workbenchRef is not null && !deferredCompletionCts.IsCancellationRequested) {
                var kind = handedOff ? CanvasWorkbenchComposerResultKind.HandedOff : outcome.Kind switch {
                    ProjectStructureAuthoringResultKind.Committed => CanvasWorkbenchComposerResultKind.Committed,
                    ProjectStructureAuthoringResultKind.Rejected => CanvasWorkbenchComposerResultKind.Rejected,
                    _ => CanvasWorkbenchComposerResultKind.Unconfirmed
                };
                await workbenchRef.AcknowledgeComposerAsync(rendererContextId, new(opening.Id, submissionId, kind,
                    handedOff ? "The request was passed to its detail editor." : outcome.Message));
            }
        }
    }

    private void RetireNodeSession(ProjectStructureNodeEditingSession? session) {
        if (session is null) {
            return;
        }
        session.Retire();
        if (!retiredAuthoringSessions.Contains(session)) {
            retiredAuthoringSessions.Add(session);
        }
    }

    private async Task DisposeRetiredNodeSessionsAsync() {
        var sessions = retiredAuthoringSessions.ToArray();
        await Task.WhenAll(sessions.Select(session => session.DisposeAsync().AsTask()));
        retiredAuthoringSessions.RemoveAll(sessions.Contains);
    }

    private Task RunNodeComparisonAsync(ProjectStructureNodeEditingSession session, Func<Task> action)
        => TrackAuthoringOperationAsync(RunNodeComparisonCoreAsync(session, action));

    private async Task RunNodeComparisonCoreAsync(ProjectStructureNodeEditingSession session, Func<Task> action) {
        try {
            var operation = action();
            await InvokeAsync(StateHasChanged);
            await operation;
        } catch (OperationCanceledException) when (deferredCompletionCts.IsCancellationRequested) {
        } catch (Exception failure) {
            Logger.LogWarning(failure, "Structure node comparison for opening {OpeningId} failed.", session.OpeningId);
            session.ReportFailure(failure is ArgumentException or InvalidOperationException or ProjectStructureEditConflictException or
                ProjectWriteAdmissionRejectedException or UnauthorizedAccessException ? failure.Message :
                "The comparison could not be completed. Your editor and result entries are retained; retry the read.");
        }
    }

    private Task CompareComposerAsync(CanvasWorkbenchComposerDraft draft) {
        var captured = composerOpening;
        if (captured?.Ownership.Id != draft.OpeningId || captured.Session is not { } session || workbenchRef is null) {
            return Task.CompletedTask;
        }
        var renderer = workbenchRef;
        var contextId = StructureContextId;
        return RunNodeComparisonAsync(session, () => session.CompareAsync(draft, () => renderer.ReadComposerDraftAsync(contextId)));
    }

    private async Task RefreshNodeComparisonAsync(ProjectStructureNodeEditingSession session) {
        if (composerOpening?.Session != session || workbenchRef is null) {
            return;
        }
        var draft = await workbenchRef.ReadComposerDraftAsync(StructureContextId);
        if (draft is not null) {
            await CompareComposerAsync(draft);
        }
    }

    private Task ApplyNodeComparisonAsync(ProjectStructureNodeEditingSession session, bool undo = false) {
        if (composerOpening is not { } captured || captured.Session != session || workbenchRef is null) {
            return Task.CompletedTask;
        }
        var renderer = workbenchRef;
        var contextId = StructureContextId;
        return RunNodeComparisonAsync(session, async () => {
            if (undo) {
                await session.UndoAsync(request => renderer.ApplyComposerDraftAsync(contextId, request));
            } else if (await session.ApplyAsync(request => renderer.ApplyComposerDraftAsync(contextId, request))) {
                composerOpenings[session.OpeningId] = captured;
            }
        });
    }

}
