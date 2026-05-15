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

    private async Task OpenAddWorkflowDialogAsync(ProjectStructureNode node)
    {
        CloseQuickActionDialog();

        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
        inputSettings.SelectedNodeIds = selectedNodeIds
            .Where(nodeId => !string.Equals(nodeId, node.Id, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        try
        {
            var options = await WorkflowNodeService.GetAddOptionsAsync(
                ProjectId,
                node.Id,
                new ProjectStructureWorkflowAddOptionsInput(InputSettings: inputSettings));
            workflowAddDialogRefreshVersion++;
            workflowAddDialog = new ProjectStructureWorkflowAddDialogState(
                node.Id,
                node.Title,
                options.Workflows,
                options.SelectedWorkflowId,
                options.SelectedVersionId,
                options.InputSettings,
                options.Preview,
                string.Join(" ", options.Warnings));
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            workflowAddDialogRefreshVersion++;
            workflowAddDialog = BuildWorkflowAddErrorDialog(node, inputSettings, exception.GetBaseException().Message);
            Logger.LogWarning(
                exception,
                "Project structure workflow add dialog failed to load. ProjectId={ProjectId} ParentNodeId={ParentNodeId}",
                ProjectId,
                node.Id);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void CloseWorkflowAddDialog()
    {
        workflowAddDialogRefreshVersion++;
        workflowAddDialog = null;
    }

    private async Task HandleWorkflowAddSelectionChanged(ChangeEventArgs args)
    {
        var workflowId = Guid.TryParse(args.Value?.ToString(), out var parsedWorkflowId) && parsedWorkflowId != Guid.Empty
            ? new WorkflowId(parsedWorkflowId)
            : (WorkflowId?)null;

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
        if (workflowAddDialog is null)
        {
            return;
        }

        var dialog = workflowAddDialog;
        if (!dialog.SelectedWorkflowId.HasValue)
        {
            workflowAddDialog = workflowAddDialog with { Error = "Select a workflow before continuing." };
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            var parentNode = ResolveNode(dialog.ParentNodeId);
            var created = await WorkflowNodeService.CreateAsync(
                ProjectId,
                dialog.ParentNodeId,
                new ProjectStructureWorkflowNodeCreateInput(
                    dialog.SelectedWorkflowId.Value,
                    dialog.SelectedVersionId,
                    InputSettings: dialog.InputSettings,
                    X: parentNode?.X + 320,
                    Y: parentNode?.Y + 120),
                CreateProjectStructureUiAgentContext());

            workflowAddDialog = null;
            selectedWorkflowStatus = null;
            workflowFeedback = $"{created.Node.Title} was added under {parentNode?.Title ?? "the selected node"}.";
            workflowFeedbackTone = "mint";
            await ReloadSurfaceAsync(created.Node.Id);
            await TryRefreshWorkflowStatusAsync(created.Node.Id, reloadSurface: true);
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            workflowAddDialog = dialog with { Error = exception.GetBaseException().Message };
            Logger.LogWarning(
                exception,
                "Project structure workflow node creation failed. ProjectId={ProjectId} ParentNodeId={ParentNodeId} WorkflowId={WorkflowId}",
                ProjectId,
                dialog.ParentNodeId,
                dialog.SelectedWorkflowId?.ToString() ?? string.Empty);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenStartWorkflowDialogAsync(ProjectStructureNode node)
    {
        CloseQuickActionDialog();

        if (node.ObjectType != ProjectObjectType.WorkflowDefinition)
        {
            workflowFeedback = "The selected node is not a workflow node.";
            workflowFeedbackTone = "warn";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var status = await TryRefreshWorkflowStatusAsync(node.Id, reloadSurface: false);
        ProjectStructureWorkflowStartOptionsResult? startOptions = null;
        var error = string.Empty;
        try
        {
            startOptions = await WorkflowNodeService.GetStartOptionsAsync(ProjectId, node.Id);
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            error = exception.GetBaseException().Message;
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
            error);

        await InvokeAsync(StateHasChanged);
    }

    private void CloseWorkflowStartDialog()
    {
        workflowStartDialog = null;
    }

    private async Task ExecuteWorkflowStartAsync()
    {
        if (workflowStartDialog is null)
        {
            return;
        }

        var dialog = workflowStartDialog;
        workflowStartDialog = dialog with
        {
            IsBusy = true,
            Error = string.Empty
        };
        await InvokeAsync(StateHasChanged);

        try
        {
            var started = await WorkflowNodeService.StartAsync(
                ProjectId,
                dialog.NodeId,
                new ProjectStructureWorkflowNodeStartInput(
                    dialog.RequestedBackend,
                    RequestedBy: "project-structure-ui",
                    SimulatedNodeIds: dialog.SimulatedNodeIds),
                CreateProjectStructureUiAgentContext());
            selectedWorkflowStatus = started.Status;
            workflowStartDialog = null;
            workflowFeedback = $"{dialog.NodeTitle} started from project structure.";
            workflowFeedbackTone = started.Status.State == WorkflowRunState.Failed ? "warn" : "mint";
            await ReloadSurfaceAsync(dialog.NodeId);
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            var message = exception.GetBaseException().Message;
            var status = await TryRefreshWorkflowStatusAsync(dialog.NodeId, reloadSurface: true);
            workflowStartDialog = dialog with
            {
                Status = status,
                IsBusy = false,
                Error = message
            };
            workflowFeedback = message;
            workflowFeedbackTone = "warn";
            Logger.LogWarning(
                exception,
                "Project structure workflow start failed. ProjectId={ProjectId} NodeId={NodeId}",
                ProjectId,
                dialog.NodeId);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void HandleWorkflowStartSimulationChanged(ProjectStructureWorkflowStartSimulationChange change)
    {
        if (workflowStartDialog is null)
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

        try
        {
            var status = await WorkflowNodeService.GetStatusAsync(ProjectId, nodeId);
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
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            selectedWorkflowStatus = null;
            workflowFeedback = exception.GetBaseException().Message;
            workflowFeedbackTone = "warn";
            Logger.LogWarning(
                exception,
                "Project structure workflow status refresh failed. ProjectId={ProjectId} NodeId={NodeId}",
                ProjectId,
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
            settings.AdditionalSources =
            [
                source with
                {
                    IsEnabled = !string.IsNullOrWhiteSpace(source.Value)
                }
            ];
            return settings;
        });

    private async Task RefreshWorkflowAddDialogAsync(
        Func<ProjectStructureWorkflowAddDialogState, ProjectStructureWorkflowAddDialogState> update)
    {
        if (workflowAddDialog is null)
        {
            return;
        }

        var refreshVersion = ++workflowAddDialogRefreshVersion;
        var requested = update(workflowAddDialog);
        workflowAddDialog = requested;
        try
        {
            var options = await WorkflowNodeService.GetAddOptionsAsync(
                ProjectId,
                requested.ParentNodeId,
                new ProjectStructureWorkflowAddOptionsInput(
                requested.SelectedWorkflowId,
                requested.SelectedVersionId,
                requested.InputSettings,
                requested.InputSettings.SelectedNodeIds));
            if (refreshVersion != workflowAddDialogRefreshVersion || workflowAddDialog is null)
            {
                return;
            }

            workflowAddDialog = requested with
            {
                Options = options.Workflows,
                SelectedWorkflowId = options.SelectedWorkflowId,
                SelectedVersionId = options.SelectedVersionId,
                InputSettings = options.InputSettings,
                Preview = options.Preview,
                Error = string.Join(" ", options.Warnings)
            };
        }
        catch (Exception exception) when (IsWorkflowUiException(exception))
        {
            if (refreshVersion != workflowAddDialogRefreshVersion || workflowAddDialog is null)
            {
                return;
            }

            workflowAddDialog = requested with { Error = exception.GetBaseException().Message };
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

    private ProjectStructureAgentContext CreateProjectStructureUiAgentContext()
        => new(
            "project-structure-ui",
            "Project structure UI",
            Environment.MachineName,
            AppContext.BaseDirectory,
            string.Empty,
            ProjectId.ToString("D"));

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
}
