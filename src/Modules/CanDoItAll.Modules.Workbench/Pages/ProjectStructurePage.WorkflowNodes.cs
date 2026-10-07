using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    [Inject]
    private ProjectStructureWorkflowNodeService WorkflowNodeService { get; set; } = default!;

    private ProjectStructureWorkflowAddDialogState? workflowAddDialog;
    private ProjectStructureWorkflowStartDialogState? workflowStartDialog;
    private ProjectStructureWorkflowRunStatus? selectedWorkflowStatus;
    private long workflowAddDialogRefreshVersion;
    private ProjectStructureAuthoringOpening? workflowAddOpening;
    private ProjectStructureAuthoringOpening? workflowStartOpening;

    private bool IsCurrentWorkflowOpening(ProjectStructureAuthoringOpening opening, ProjectStructureAuthoringOpening? current)
        => IsCurrentAuthoring(opening, current) && HasOriginalContentAuthority(opening.Context);

    private async Task OpenAddWorkflowDialogAsync(ProjectStructureNode node)
    {
        var opening = new ProjectStructureAuthoringOpening(CaptureActionContext(), node);
        workflowAddOpening = opening;
        workflowAddDialog = null;
        var refreshVersion = ++workflowAddDialogRefreshVersion;
        var selectionRevision = insightsSelectionRevision;
        bool IsCurrent() => IsCurrentWorkflowOpening(opening, workflowAddOpening) &&
            refreshVersion == workflowAddDialogRefreshVersion && selectionRevision == insightsSelectionRevision;
        var openedSurface = opening.Context.Surface;
        var mutationOwner = CreateProjectStructureUiAgentContext() with { ExpectedProjectAdmission = openedSurface.ExpectedProjectAdmission };

        CloseQuickActionDialog();

        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.SelectedNodeIds = selectedNodeIds
            .Where(nodeId => !string.Equals(nodeId, node.Id, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        try
        {
            var options = await WorkflowNodeService.GetAddOptionsAsync(
                openedSurface.ProjectId,
                node.Id,
                new ProjectStructureWorkflowAddOptionsInput(InputSettings: inputSettings) {
                    ExpectedProject = opening.Context.Admission, ExpectedParent = opening.Node
                });
            if (!IsCurrent()) {
                return;
            }
            workflowAddDialog = new ProjectStructureWorkflowAddDialogState(
                node.Id,
                node.Title,
                options.Workflows,
                options.SelectedWorkflowId,
                options.SelectedVersionId,
                options.InputSettings,
                options.Preview,
                string.Join(" ", options.Warnings)) { OpeningId = opening.Id, OpenedSurface = openedSurface, MutationOwner = mutationOwner };
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            if (!IsCurrent()) {
                return;
            }
            workflowAddDialog = BuildWorkflowAddErrorDialog(node, inputSettings, FormatWorkflowUiException(exception)) with {
                OpeningId = opening.Id, OpenedSurface = openedSurface, MutationOwner = mutationOwner
            };
            Logger.LogWarning(
                exception,
                "Project structure workflow add dialog failed to load. ProjectId={ProjectId} ParentNodeId={ParentNodeId}",
                openedSurface.ProjectId,
                node.Id);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void CloseWorkflowAddDialog()
    {
        workflowAddDialogRefreshVersion++;
        workflowAddOpening = null;
        workflowAddDialog = null;
    }

    private async Task HandleWorkflowAddSelectionChanged(WorkflowId workflowId)
    {
        await RefreshWorkflowAddDialogAsync(dialog => dialog with
        {
            SelectedWorkflowId = workflowId,
            SelectedVersionId = null,
            Error = string.Empty
        });
    }

    private Task HandleWorkflowAddIncludeParentSubtreeChanged(ChangeEventArgs args)
        => UpdateWorkflowAddInputAsync(settings =>
        {
            settings.IncludeParentSubtree = ParseCheckboxValue(args);
            return settings;
        });

    private Task HandleWorkflowAddIncludeAssetsChanged(ChangeEventArgs args)
        => UpdateWorkflowAddInputAsync(settings =>
        {
            settings.IncludeAssets = ParseCheckboxValue(args);
            return settings;
        });

    private Task HandleWorkflowAddManualInputChanged(ChangeEventArgs args)
        => UpdateWorkflowAddInputAsync(settings =>
        {
            settings.ManualInputJson = args.Value?.ToString() ?? string.Empty;
            return settings;
        });

    private Task HandleWorkflowAddSourceKindChanged(ChangeEventArgs args)
        => UpdateWorkflowAddSourceAsync(source =>
        {
            var value = args.Value?.ToString();
            return Enum.TryParse<ProjectStructureWorkflowInputSourceKind>(value, ignoreCase: true, out var parsedKind) &&
                   Enum.IsDefined(parsedKind)
                ? source with { Kind = parsedKind }
                : source;
        });

    private Task HandleWorkflowAddSourceKeyChanged(ChangeEventArgs args)
        => UpdateWorkflowAddSourceAsync(source => source with { Key = args.Value?.ToString() ?? string.Empty });

    private Task HandleWorkflowAddSourceLabelChanged(ChangeEventArgs args)
        => UpdateWorkflowAddSourceAsync(source => source with { Label = args.Value?.ToString() ?? string.Empty });

    private Task HandleWorkflowAddSourceValueChanged(ChangeEventArgs args)
        => UpdateWorkflowAddSourceAsync(source => source with { Value = args.Value?.ToString() ?? string.Empty });

    private async Task ExecuteWorkflowAddAsync()
    {
        if (workflowAddDialog is not { } dialog || workflowAddOpening is not { } opening ||
            !IsCurrentWorkflowOpening(opening, workflowAddOpening) || opening.IsBusy || opening.RequiresObservation)
        {
            return;
        }

        var openedSurface = dialog.OpenedSurface ?? throw new InvalidOperationException("Reopen this editor to capture its project.");
        var mutationOwner = dialog.MutationOwner ?? throw new InvalidOperationException("Reopen this editor to capture its project.");
        if (!dialog.SelectedWorkflowId.HasValue)
        {
            workflowAddDialog = workflowAddDialog with { Error = "Select a workflow before continuing." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        opening.IsBusy = true;
        workflowAddDialogRefreshVersion++;
        workflowAddDialog = dialog with { IsBusy = true, Error = string.Empty };
        var selectionRevision = insightsSelectionRevision;
        bool IsCurrent() => IsCurrentWorkflowOpening(opening, workflowAddOpening) && insightsSelectionRevision == selectionRevision;
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, Guid.NewGuid(), opening.Context.Admission,
            ProjectStructureAuthoringOperation.AddWorkflow, ProjectStructureAuthoringResultKind.Unconfirmed,
            "The original Workflow creation has not been confirmed.") {
            SourceNodeId = dialog.ParentNodeId, WorkflowId = dialog.SelectedWorkflowId, WorkflowVersionId = dialog.SelectedVersionId
        };
        var dispatched = false;
        await InvokeAsync(StateHasChanged);
        try
        {
            _ = ProjectStructureWorkflowInputSettingsNormalizer.Normalize(dialog.InputSettings);
            var parentNode = openedSurface.Nodes.FirstOrDefault(node => node.Id == dialog.ParentNodeId)
                ?? throw new InvalidOperationException("The selected project-structure node is no longer available.");
            string addedNodeTitle;
            string createdWorkflowNodeId;
            if (IsCanonicalTaskNode(parentNode))
            {
                var selectedWorkflowId = dialog.SelectedWorkflowId.Value;
                var selectedOption = dialog.Options.FirstOrDefault(option =>
                    option.WorkflowId == selectedWorkflowId);
                var selectedVersionId = dialog.SelectedVersionId
                    ?? selectedOption?.VersionId
                    ?? throw new InvalidOperationException(
                        "The selected workflow version is no longer available.");
                var execution = ProjectStructureTaskEditStatePolicy.Read(parentNode).Execution;
                dispatched = true;
                var attached = await TaskResourceAttachmentService.AttachAsync(
                    openedSurface.ProjectId,
                    parentNode.Id,
                    new ProjectStructureTaskResourceAttachRequest(
                        new ProjectStructureTaskResourceSelection(
                            ProjectStructureTaskResourceKind.Workflow,
                            selectedWorkflowId.Value,
                            selectedVersionId.Value),
                        execution,
                        dialog.InputSettings) { ExpectedProjectAdmission = mutationOwner.ExpectedProjectAdmission },
                    mutationOwner);
                if (string.IsNullOrWhiteSpace(attached.CreatedNodeId))
                {
                    throw new InvalidOperationException(
                        "The workflow attachment did not return its created project-structure node id.");
                }

                addedNodeTitle = selectedOption?.DisplayName ?? "Workflow";
                createdWorkflowNodeId = attached.CreatedNodeId;
                outcome = outcome with { WorkflowId = selectedWorkflowId, WorkflowVersionId = selectedVersionId };
            }
            else
            {
                dispatched = true;
                var created = await WorkflowNodeService.CreateAsync(
                    openedSurface.ProjectId,
                    dialog.ParentNodeId,
                    new ProjectStructureWorkflowNodeCreateInput(
                        dialog.SelectedWorkflowId.Value,
                        dialog.SelectedVersionId,
                        InputSettings: dialog.InputSettings,
                        X: parentNode.X + 320,
                        Y: parentNode.Y + 120) { ExpectedParent = opening.Node },
                    mutationOwner);
                addedNodeTitle = created.Node.Title;
                createdWorkflowNodeId = created.Node.Id;
                outcome = outcome with { WorkflowId = created.WorkflowId, WorkflowVersionId = created.WorkflowVersionId };
            }

            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed,
                NodeIds = [createdWorkflowNodeId], Message = $"{addedNodeTitle} was added under {parentNode.Title}." };
            RecordAuthoringOutcome(opening, outcome, IsCurrent());
            if (IsCurrent()) {
                workflowAddDialog = null;
                selectedWorkflowStatus = null;
                await RefreshAuthoringSurfaceAsync(opening, outcome, createdWorkflowNodeId, IsCurrent);
                if (IsCurrentWorkflowOpening(opening, workflowAddOpening)) {
                    await TryRefreshWorkflowStatusAsync(createdWorkflowNodeId, reloadSurface: false);
                }
            }
        }
        catch (Exception exception) {
            if (exception is ProjectStructureAgentException { Details: ProjectStructureTaskAttachmentFailureFacts attachment }) {
                outcome = outcome with { WorkflowAttachmentFailure = attachment,
                    NodeIds = attachment.CreatedNodeId is { } created ? [created] : [],
                    Kind = attachment.CompensationSucceeded == true ? ProjectStructureAuthoringResultKind.Compensated : ProjectStructureAuthoringResultKind.PartialCommit };
            }
            opening.RequiresObservation = outcome.Kind is ProjectStructureAuthoringResultKind.Committed or
                ProjectStructureAuthoringResultKind.Compensated or ProjectStructureAuthoringResultKind.PartialCommit || dispatched && !IsKnownWorkflowRejection(exception);
            var message = outcome.Kind == ProjectStructureAuthoringResultKind.Committed
                ? "The Workflow was added, but its status could not be refreshed. Observe the original node; do not add it again."
                : outcome.WorkflowAttachmentFailure is not null ? FormatWorkflowUiException(exception)
                : opening.RequiresObservation ? "Workflow creation is unconfirmed. Inspect the original project before adding it again."
                : FormatWorkflowUiException(exception);
            RecordAuthoringOutcome(opening, outcome with { Failure = exception, Message = message,
                Kind = opening.RequiresObservation ? outcome.Kind : ProjectStructureAuthoringResultKind.Rejected }, IsCurrent());
            if (IsCurrent() && outcome.Kind != ProjectStructureAuthoringResultKind.Committed) {
                workflowAddDialog = dialog with { IsBusy = false, RequiresObservation = opening.RequiresObservation, Error = message };
            }
        } finally {
            opening.IsBusy = false;
            await RenderAuthoringOutcomeAsync();
        }
    }

    private async Task OpenStartWorkflowDialogAsync(ProjectStructureNode node)
    {
        var actionContext = CaptureActionContext();
        var opening = new ProjectStructureAuthoringOpening(actionContext, node);
        workflowStartOpening = opening;
        workflowStartDialog = null;
        var selectionRevision = insightsSelectionRevision;
        bool IsCurrent() => IsCurrentWorkflowOpening(opening, workflowStartOpening) && insightsSelectionRevision == selectionRevision;
        var openedSurface = surface ?? throw new InvalidOperationException("Reload the project before starting a Workflow.");
        var projectId = ProjectId;
        var mutationOwner = CreateProjectStructureUiAgentContext(projectId) with { ExpectedProjectAdmission = openedSurface.ExpectedProjectAdmission };
        CloseQuickActionDialog();

        if (node.ObjectType != ProjectObjectType.WorkflowDefinition)
        {
            workflowFeedback = "The selected node is not a workflow node.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var status = await TryRefreshWorkflowStatusAsync(node.Id, reloadSurface: false);
        if (!IsCurrent()) {
            return;
        }
        ProjectStructureWorkflowStartOptionsResult? startOptions = null;
        var error = string.Empty;
        try
        {
            startOptions = await WorkflowNodeService.GetStartOptionsAsync(projectId, node.Id);
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            error = FormatWorkflowUiException(exception);
        }

        if (!IsCurrent()) {
            return;
        }

        workflowStartDialog = new ProjectStructureWorkflowStartDialogState(
            node.Id,
            node.Title,
            status,
            startOptions?.SimulationOptions ?? [],
            startOptions?.PreferredBackend ?? WorkflowRuntimeBackendKind.InProcess,
            startOptions?.RequestedBackend ?? WorkflowRuntimeBackendKind.InProcess,
            startOptions?.BackendOptions ?? [],
            startOptions?.BackendWarning ?? string.Empty,
            [],
            false,
            error) {
            ProjectId = projectId,
            OpeningId = opening.Id,
            IntentId = Guid.NewGuid(),
            MutationOwner = mutationOwner
        };

        await InvokeAsync(StateHasChanged);
    }

    private void CloseWorkflowStartDialog()
    {
        workflowStartOpening = null;
        workflowStartDialog = null;
    }

    private async Task ExecuteWorkflowStartAsync()
    {
        if (workflowStartDialog is not { } dialog || workflowStartOpening is not { } opening ||
            !IsCurrentWorkflowOpening(opening, workflowStartOpening) || opening.IsBusy)
        {
            return;
        }

        opening.IsBusy = true;
        var selectionRevision = insightsSelectionRevision;
        bool IsCurrent() => IsCurrentWorkflowOpening(opening, workflowStartOpening) && selectionRevision == insightsSelectionRevision;
        var owner = dialog.MutationOwner ?? throw new InvalidOperationException("Reopen this Workflow editor to capture its original project.");
        var outcome = new ProjectStructureAuthoringOutcome(opening.Id, dialog.IntentId, opening.Context.Admission,
            ProjectStructureAuthoringOperation.StartWorkflow,
            dialog.AcceptedStart is null ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Committed,
            "The original Workflow launch awaits observation.") { SourceNodeId = dialog.NodeId, WorkflowStart = dialog.AcceptedStart };
        workflowStartDialog = dialog with
        {
            IsBusy = true,
            Error = string.Empty
        };
        await InvokeAsync(StateHasChanged);

        try
        {
            var started = opening.RequiresObservation
                ? await WorkflowNodeService.ObserveStartAsync(dialog.ProjectId, dialog.NodeId, dialog.IntentId, owner)
                : await WorkflowNodeService.StartAsync(
                dialog.ProjectId,
                dialog.NodeId,
                new ProjectStructureWorkflowNodeStartInput(
                    dialog.RequestedBackend,
                    RequestedBy: "project-structure-ui",
                    SimulatedNodeIds: dialog.SimulatedNodeIds,
                    IntentId: dialog.IntentId) { ExpectedNode = opening.Node },
                owner);
            if (started is null) {
                opening.RequiresObservation = true;
                RecordAuthoringOutcome(opening, outcome, IsCurrent());
                if (IsCurrent()) {
                    workflowStartDialog = dialog with { IsBusy = false, RequiresObservation = true,
                        Error = "The original intent has no observable admission yet. Observe again; this action will not submit another start." };
                }
                return;
            }
            outcome = outcome with { Kind = ProjectStructureAuthoringResultKind.Committed, WorkflowStart = started,
                WorkflowId = started.WorkflowId, WorkflowVersionId = started.WorkflowVersionId,
                Message = started.RunAdmissionObserved
                ? $"{dialog.NodeTitle} has a recorded workflow run."
                : $"{dialog.NodeTitle} launch was recorded and is waiting to start." };
            opening.RequiresObservation = true;
            RecordAuthoringOutcome(opening, outcome, IsCurrent());
            if (IsCurrent()) {
                selectedWorkflowStatus = started.Status;
                workflowStartDialog = dialog with { Status = started.Status, IsBusy = false, RequiresObservation = true, AcceptedStart = started,
                    Error = string.Join(" ", started.Warnings) };
                await RefreshAuthoringSurfaceAsync(opening, outcome, dialog.NodeId, IsCurrent);
            }
        }
        catch (Exception exception) {
            opening.RequiresObservation |= !IsKnownWorkflowRejection(exception);
            var message = opening.RequiresObservation
                ? "The original Workflow start could not be observed. Observe the same intent; do not start again."
                : FormatWorkflowUiException(exception);
            RecordAuthoringOutcome(opening, outcome with { Failure = exception, Message = message,
                Kind = opening.RequiresObservation ? outcome.Kind : ProjectStructureAuthoringResultKind.Rejected }, IsCurrent());
            if (IsCurrent()) {
                workflowStartDialog = dialog with { IsBusy = false, RequiresObservation = opening.RequiresObservation, Error = message };
            }
        } finally {
            opening.IsBusy = false;
            await RenderAuthoringOutcomeAsync();
        }
    }

    private void HandleWorkflowStartSimulationChanged(ProjectStructureWorkflowStartSimulationChange change)
    {
        if (workflowStartDialog is null || workflowStartOpening is not { IsBusy: false, RequiresObservation: false } opening ||
            !IsCurrentWorkflowOpening(opening, workflowStartOpening))
        {
            return;
        }

        var selected = workflowStartDialog.SimulatedNodeIds
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (change.IsEnabled)
        {
            selected.Add(change.NodeId);
        }
        else
        {
            selected.Remove(change.NodeId);
        }

        workflowStartDialog = workflowStartDialog with
        {
            SimulatedNodeIds = selected
                .OrderBy(nodeId => nodeId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Error = string.Empty
        };
    }

    private async Task RefreshSelectedWorkflowStatusAsync(string? nodeId)
    {
        if (ResolveNode(nodeId) is not { ObjectType: ProjectObjectType.WorkflowDefinition })
        {
            selectedWorkflowStatus = null;
            return;
        }

        await TryRefreshWorkflowStatusAsync(nodeId, reloadSurface: true);
    }

    private async Task<ProjectStructureWorkflowRunStatus?> TryRefreshWorkflowStatusAsync(
        string? nodeId,
        bool reloadSurface)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            selectedWorkflowStatus = null;
            return null;
        }

        var context = CaptureActionContext();
        var selectionRevision = insightsSelectionRevision;
        bool IsCurrent() => IsCurrentAction(context) && insightsSelectionRevision == selectionRevision;
        try
        {
            await InsightsAdmissions.RequireCurrentAsync(context.Admission, deferredCompletionCts.Token);
            var status = await WorkflowNodeService.GetStatusAsync(context.Surface.ProjectId, nodeId);
            await InsightsAdmissions.RequireCurrentAsync(context.Admission, deferredCompletionCts.Token);
            if (!IsCurrent()) {
                return status;
            }
            if (selectedNodeIds.Contains(nodeId, StringComparer.Ordinal))
            {
                selectedWorkflowStatus = status;
            }

            if (reloadSurface)
            {
                await ReloadSurfaceAsync(nodeId);
            }

            return status;
        }
        catch (OperationCanceledException) when (!IsCurrent()) {
            return null;
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            if (IsCurrent()) {
                selectedWorkflowStatus = null;
                workflowFeedback = FormatWorkflowUiException(exception);
                workflowFeedbackTone = "warn";
            }
            Logger.LogWarning(
                exception,
                "Project structure workflow status refresh failed. ProjectId={ProjectId} NodeId={NodeId}",
                context.Surface.ProjectId,
                nodeId);
            return null;
        }
    }

    private Task UpdateWorkflowAddInputAsync(Func<ProjectStructureWorkflowInputSettings, ProjectStructureWorkflowInputSettings> update)
        => RefreshWorkflowAddDialogAsync(dialog =>
        {
            var settings = update(CloneWorkflowInputSettings(dialog.InputSettings));
            return dialog with
            {
                InputSettings = settings,
                Error = string.Empty
            };
        });

    private Task UpdateWorkflowAddSourceAsync(
        Func<ProjectStructureWorkflowInputSource, ProjectStructureWorkflowInputSource> update)
        => UpdateWorkflowAddInputAsync(settings =>
        {
            var source = NormalizeWorkflowInputSource(update(ResolveWorkflowInputSource(settings)));
            settings.AdditionalSources = [source with { IsEnabled = !string.IsNullOrWhiteSpace(source.Value) },
                .. settings.AdditionalSources.Skip(1)];
            return settings;
        });

    private async Task RefreshWorkflowAddDialogAsync(
        Func<ProjectStructureWorkflowAddDialogState, ProjectStructureWorkflowAddDialogState> update)
    {
        if (workflowAddDialog is null || workflowAddOpening is not { } opening ||
            !IsCurrentWorkflowOpening(opening, workflowAddOpening) || opening.IsBusy || opening.RequiresObservation)
        {
            return;
        }

        var refreshVersion = ++workflowAddDialogRefreshVersion;
        var requested = update(workflowAddDialog);
        workflowAddDialog = requested;
        try
        {
            var options = await WorkflowNodeService.GetAddOptionsAsync(
                opening.Context.Surface.ProjectId,
                requested.ParentNodeId,
                new ProjectStructureWorkflowAddOptionsInput(
                requested.SelectedWorkflowId,
                requested.SelectedVersionId,
                requested.InputSettings,
                requested.InputSettings.SelectedNodeIds) { ExpectedProject = opening.Context.Admission, ExpectedParent = opening.Node });
            if (refreshVersion != workflowAddDialogRefreshVersion || !IsCurrentWorkflowOpening(opening, workflowAddOpening) ||
                opening.IsBusy || opening.RequiresObservation || workflowAddDialog is null)
            {
                return;
            }

            workflowAddDialog = requested with
            {
                Options = options.Workflows,
                SelectedWorkflowId = options.SelectedWorkflowId,
                SelectedVersionId = options.SelectedVersionId,
                InputSettings = requested.InputSettings,
                Preview = options.Preview,
                Error = string.Join(" ", options.Warnings)
            };
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            if (refreshVersion != workflowAddDialogRefreshVersion || !IsCurrentWorkflowOpening(opening, workflowAddOpening) ||
                opening.IsBusy || opening.RequiresObservation || workflowAddDialog is null)
            {
                return;
            }

            workflowAddDialog = requested with { Error = FormatWorkflowUiException(exception) };
        }

        await InvokeAsync(StateHasChanged);
    }

    private static ProjectStructureWorkflowAddDialogState BuildWorkflowAddErrorDialog(
        ProjectStructureNode node,
        ProjectStructureWorkflowInputSettings inputSettings,
        string error)
    {
        var preview = new ProjectStructureWorkflowInputPreview(
            string.Empty,
            "{}",
            []);
        return new ProjectStructureWorkflowAddDialogState(
            node.Id,
            node.Title,
            [],
            null,
            null,
            inputSettings,
            preview,
            error);
    }

    private ProjectStructureAgentContext CreateProjectStructureUiAgentContext(Guid? projectId = null)
        => new(
            "project-structure-ui",
            "Project structure UI",
            Environment.MachineName,
            AppContext.BaseDirectory,
            string.Empty,
            (projectId ?? ProjectId).ToString("D")) {
            WorkflowAuthority = ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface)
        };

    private static ProjectStructureWorkflowInputSettings CloneWorkflowInputSettings(
        ProjectStructureWorkflowInputSettings inputSettings)
        => new()
        {
            IncludeProject = inputSettings.IncludeProject,
            IncludeParentNode = inputSettings.IncludeParentNode,
            IncludeParentNodeDetails = inputSettings.IncludeParentNodeDetails,
            IncludeParentSubtree = inputSettings.IncludeParentSubtree,
            IncludeAssets = inputSettings.IncludeAssets,
            SelectedNodeIds = inputSettings.SelectedNodeIds.ToList(),
            AdditionalSources = inputSettings.AdditionalSources.ToList(),
            ManualInputJson = inputSettings.ManualInputJson
        };

    private static ProjectStructureWorkflowInputSource ResolveWorkflowInputSource(
        ProjectStructureWorkflowInputSettings settings)
        => settings.AdditionalSources.FirstOrDefault()
           ?? new ProjectStructureWorkflowInputSource(
               ProjectStructureWorkflowInputSourceKind.FilePath,
               "source",
               "Additional source",
               string.Empty);

    private static ProjectStructureWorkflowInputSource NormalizeWorkflowInputSource(
        ProjectStructureWorkflowInputSource source)
        => source with
        {
            Key = string.IsNullOrWhiteSpace(source.Key) ? "source" : source.Key.Trim(),
            Label = source.Label?.Trim() ?? string.Empty,
            Value = source.Value?.Trim() ?? string.Empty
        };

    private static bool ParseCheckboxValue(ChangeEventArgs args)
    {
        return args.Value switch
        {
            bool value => value,
            string value when bool.TryParse(value, out var parsed) => parsed,
            _ => false
        };
    }

    private static bool IsWorkflowUiException(Exception exception)
        => exception is ProjectStructureAgentException or InvalidOperationException or ArgumentException or KeyNotFoundException;

    private static bool IsKnownWorkflowRejection(Exception exception)
        => IsKnownGraphRejection(exception) || exception is ProjectStructureAgentException {
            CanRetryWithCorrectedInput: true, EffectState: AgentToolEffectState.None or AgentToolEffectState.NotCommitted
        };

    private static string FormatWorkflowUiException(Exception exception)
        => WorkflowFailureDisplayFormatter.ToUserMessage(exception.GetBaseException().Message);
}
