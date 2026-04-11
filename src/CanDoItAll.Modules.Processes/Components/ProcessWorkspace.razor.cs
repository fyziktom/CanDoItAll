using System.Text.Json;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace : ComponentBase, IDisposable
{
    private const string DefinitionCanvasSelectTool = "authoring";
    private const string DefinitionCanvasDeleteTool = "delete";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<SecondaryTabItem> DetailTabs =
    [
        new("definition", "Definition", Description: "Identity, governance, and publication controls."),
        new("roles", "Roles", Description: "Role-first staffing semantics and executor intent."),
        new("steps", "Steps", Description: "Typed workflow steps, bindings, artifacts, and authoring canvas."),
        new("runs", "Runs", Description: "Runtime state, assignments, work briefs, and evidence capture."),
        new("analytics", "Analytics", Description: "Economics, conformance, capability gaps, and improvement signals."),
        new("exchange", "Exchange", Description: "Import, export, and future executor-registry seam review.")
    ];

    [Inject]
    private ProcessesService ProcessesService { get; set; } = default!;

    [Inject]
    private ProcessDevelopmentSeedService SeedService { get; set; } = default!;

    [Inject]
    private ProcessCanvasSurfaceFactory CanvasSurfaceFactory { get; set; } = default!;

    [Inject]
    private ProjectsService ProjectsService { get; set; } = default!;

    [Parameter]
    public Guid? ProjectId { get; set; }

    [SupplyParameterFromQuery(Name = "processId")]
    private Guid? ProcessIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = "runId")]
    private Guid? RunIdQuery { get; set; }

    private IReadOnlyList<ProcessDefinitionListItem> definitions = [];
    private IReadOnlyList<ProcessRunListItem> runs = [];
    private IReadOnlyList<ProcessStepRunViewModel> stepRuns = [];
    private IReadOnlyList<ProcessDecisionViewModel> decisions = [];
    private IReadOnlyList<ProcessArtifactViewModel> artifacts = [];
    private IReadOnlyList<ProcessRunAssignmentViewModel> assignments = [];
    private IReadOnlyList<ProcessWorkBriefViewModel> workBriefs = [];
    private IReadOnlyList<ProcessConformanceObservationViewModel> conformanceObservations = [];
    private IReadOnlyList<ProcessImprovementViewModel> improvements = [];
    private IReadOnlyList<ProcessExecutorRegistryOption> executorOptions = [];
    private IReadOnlyList<ProjectPartyOption> partyOptions = [];

    private ProcessAnalyticsSummary analytics = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private ProcessDefinitionEditorModel editor = new();
    private CanvasWorkbenchSurface? canvasSurface;
    private CanvasWorkbenchUiState definitionCanvasUiState = CreateDefaultDefinitionCanvasUiState();
    private CanvasWorkbenchUiState runtimeCanvasUiState = CreateDefaultRuntimeCanvasUiState();
    private string definitionCanvasTool = DefinitionCanvasSelectTool;

    private Guid? selectedProcessId;
    private Guid? selectedRunId;
    private Guid? selectedAssignmentId;
    private string detailTab = "definition";
    private string definitionSearch = string.Empty;
    private string runNameDraft = string.Empty;
    private ProcessOperatingMode runOperatingMode = ProcessOperatingMode.AssistedExecution;
    private Guid? artifactStepRunId;
    private string artifactTitle = string.Empty;
    private ProcessArtifactKind artifactKind = ProcessArtifactKind.Evidence;
    private ProcessArtifactTrustStatus artifactTrustStatus = ProcessArtifactTrustStatus.ReviewRequired;
    private ProcessSensitivityLevel artifactSensitivityLevel = ProcessSensitivityLevel.Internal;
    private string artifactProvenance = string.Empty;
    private string artifactAllowedUsage = string.Empty;
    private string artifactReview = string.Empty;
    private Guid? assignmentPartyId;
    private string assignmentDisplayName = string.Empty;
    private string assignmentExecutorKind = "person";
    private string assignmentBindingReason = string.Empty;
    private bool assignmentIsFallback;
    private Dictionary<Guid, Guid?> runtimeBranchOutcomeSelections = [];
    private string exportJson = string.Empty;
    private string importJson = string.Empty;
    private string projectName = string.Empty;
    private string message = string.Empty;
    private bool isError;
    private bool hasLoadedParameters;
    private Guid? loadedProjectId;
    private Guid? loadedProcessQueryId;
    private Guid? loadedRunQueryId;

    private IReadOnlyList<ProcessDefinitionListItem> FilteredDefinitions => definitions
        .Where(definition =>
            string.IsNullOrWhiteSpace(definitionSearch) ||
            definition.Name.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase) ||
            definition.Summary.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase) ||
            definition.ValueStatement.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(definition => definition.UpdatedAtUtc)
        .ToList();

    private ProcessDefinitionListItem? SelectedDefinitionSummary
        => selectedProcessId.HasValue
            ? definitions.FirstOrDefault(definition => definition.Id == selectedProcessId.Value)
            : null;

    private ProcessRunListItem? SelectedRun
        => selectedRunId.HasValue
            ? runs.FirstOrDefault(run => run.Id == selectedRunId.Value)
            : null;

    private ProcessRunAssignmentViewModel? SelectedAssignment
        => selectedAssignmentId.HasValue
            ? assignments.FirstOrDefault(item => item.Id == selectedAssignmentId.Value)
            : null;

    private string PageEyebrow
        => ProjectId.HasValue
            ? "Project processes"
            : "Processes";

    private string PageTitleText
        => ProjectId.HasValue && !string.IsNullOrWhiteSpace(projectName)
            ? $"{projectName} processes"
            : "Process management";

    private string EditorTitle
        => string.IsNullOrWhiteSpace(editor.Name)
            ? "New process definition"
            : editor.Name;

    private string SelectedDefinitionTone
        => ResolveDefinitionStatusTone(editor.Status);

    private int SelectedDetailTabIndex
        => ResolveDetailTabIndex(detailTab);

    protected override async Task OnParametersSetAsync()
    {
        if (hasLoadedParameters &&
            loadedProjectId == ProjectId &&
            loadedProcessQueryId == ProcessIdQuery &&
            loadedRunQueryId == RunIdQuery)
        {
            return;
        }

        hasLoadedParameters = true;
        loadedProjectId = ProjectId;
        loadedProcessQueryId = ProcessIdQuery;
        loadedRunQueryId = RunIdQuery;
        await LoadWorkspaceAsync();
    }

    public void Dispose()
    {
        CancelPendingDefinitionCanvasPersistence();
    }

    private async Task LoadWorkspaceAsync()
    {
        if (ProjectId.HasValue)
        {
            var project = await ProjectsService.GetAsync(ProjectId.Value);
            projectName = project.Name;
        }
        else
        {
            projectName = string.Empty;
        }

        definitions = await ProcessesService.ListDefinitionsAsync(ProjectId);
        var nextSelectedProcessId = ResolveSelectedProcessId();
        if (nextSelectedProcessId != selectedProcessId)
        {
            await FlushPendingDefinitionCanvasPersistenceAsync();
            selectedCanvasNodeId = null;
            ResetDefinitionCanvasState();
            ResetRuntimeCanvasState();
        }

        selectedProcessId = nextSelectedProcessId;
        editor = await ProcessesService.GetEditorAsync(selectedProcessId, ProjectId);
        executorOptions = await ProcessesService.ListExecutorOptionsAsync();
        analytics = await ProcessesService.GetAnalyticsAsync(selectedProcessId, ProjectId);
        improvements = await ProcessesService.ListImprovementsAsync(selectedProcessId);

        if (ProjectId.HasValue)
        {
            partyOptions = await ProcessesService.ListPartyOptionsAsync(ProjectId.Value);
        }
        else
        {
            partyOptions = [];
        }

        if (selectedProcessId.HasValue)
        {
            runs = await ProcessesService.ListRunsAsync(selectedProcessId, ProjectId);
        }
        else
        {
            runs = [];
        }

        var nextSelectedRunId = ResolveSelectedRunId();
        if (nextSelectedRunId != selectedRunId)
        {
            selectedCanvasNodeId = null;
            ResetRuntimeCanvasState();
        }

        selectedRunId = nextSelectedRunId;
        await LoadRunDetailsAsync();
        RefreshCanvasSurface();
        StateHasChanged();
    }

    private async Task LoadRunDetailsAsync()
    {
        if (!selectedRunId.HasValue)
        {
            stepRuns = [];
            decisions = [];
            artifacts = [];
            assignments = [];
            workBriefs = [];
            conformanceObservations = [];
            selectedAssignmentId = null;
            artifactStepRunId = null;
            return;
        }

        stepRuns = await ProcessesService.ListStepRunsAsync(selectedRunId.Value);
        decisions = await ProcessesService.ListDecisionRecordsAsync(selectedRunId.Value);
        artifacts = await ProcessesService.ListArtifactsAsync(selectedRunId.Value);
        assignments = await ProcessesService.ListAssignmentsAsync(selectedRunId.Value);
        workBriefs = await ProcessesService.ListWorkBriefsAsync(selectedRunId.Value);
        conformanceObservations = await ProcessesService.ListConformanceObservationsAsync(selectedRunId.Value);
        var refreshedRuntimeBranchSelections = new Dictionary<Guid, Guid?>();
        foreach (var stepRun in stepRuns) {
            runtimeBranchOutcomeSelections.TryGetValue(stepRun.Id, out var selectedBranchOutcomeId);
            if (selectedBranchOutcomeId.HasValue &&
                stepRun.AvailableBranchOutcomes.All(item => item.Id != selectedBranchOutcomeId.Value)) {
                selectedBranchOutcomeId = null;
            }

            refreshedRuntimeBranchSelections[stepRun.Id] = selectedBranchOutcomeId ?? stepRun.SelectedBranchOutcomeId;
        }

        runtimeBranchOutcomeSelections = refreshedRuntimeBranchSelections;

        if (!selectedAssignmentId.HasValue || assignments.All(item => item.Id != selectedAssignmentId.Value))
        {
            selectedAssignmentId = assignments.FirstOrDefault()?.Id;
        }

        ApplyAssignmentSelection();

        if (!artifactStepRunId.HasValue || stepRuns.All(item => item.Id != artifactStepRunId.Value))
        {
            artifactStepRunId = stepRuns.FirstOrDefault()?.Id;
        }
    }

    private Guid? ResolveSelectedProcessId()
    {
        if (ProcessIdQuery.HasValue && definitions.Any(definition => definition.Id == ProcessIdQuery.Value))
        {
            return ProcessIdQuery.Value;
        }

        if (selectedProcessId.HasValue && definitions.Any(definition => definition.Id == selectedProcessId.Value))
        {
            return selectedProcessId.Value;
        }

        return definitions.FirstOrDefault()?.Id;
    }

    private Guid? ResolveSelectedRunId()
    {
        if (RunIdQuery.HasValue && runs.Any(run => run.Id == RunIdQuery.Value))
        {
            return RunIdQuery.Value;
        }

        if (selectedRunId.HasValue && runs.Any(run => run.Id == selectedRunId.Value))
        {
            return selectedRunId.Value;
        }

        return runs.FirstOrDefault()?.Id;
    }

    private void RefreshCanvasSurface()
    {
        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
        canvasSurface = detailTab == "runs" && SelectedRun is not null
            ? CanvasSurfaceFactory.BuildRunSurface(SelectedRun, stepRuns, selectedCanvasNodeId)
            : CanvasSurfaceFactory.BuildDefinitionSurface(editor, selectedCanvasNodeId, definitionCanvasTool);

        var uiState = BuildCanvasUiState(canvasSurface, ResolveStoredCanvasUiState());
        canvasSurface.UiState = uiState;
        StoreCanvasUiState(uiState);

        if (string.Equals(selectedCanvasNodeId, NoCanvasSelection, StringComparison.Ordinal))
        {
            return;
        }

        var synchronizedSelection = uiState.SelectedNodeIds.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(synchronizedSelection))
        {
            selectedCanvasNodeId = synchronizedSelection;
            return;
        }

        if (selectedCanvasNodeId is not null)
        {
            selectedCanvasNodeId = null;
        }
    }

    private async Task CreateNewAsync()
    {
        await FlushPendingDefinitionCanvasPersistenceAsync();
        selectedProcessId = null;
        selectedRunId = null;
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        editor = await ProcessesService.GetEditorAsync(null, ProjectId);
        detailTab = "definition";
        runs = [];
        improvements = [];
        analytics = await ProcessesService.GetAnalyticsAsync(null, ProjectId);
        await LoadRunDetailsAsync();
        CloseCanvasEditor();
        canvasActionDialog = null;
        RefreshCanvasSurface();
        ClearMessage();
    }

    private async Task SelectDefinitionAsync(Guid definitionId)
    {
        await FlushPendingDefinitionCanvasPersistenceAsync();
        selectedProcessId = definitionId;
        detailTab = "definition";
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        await LoadWorkspaceAsync();
    }

    private async Task SaveAsync()
    {
        CancelPendingDefinitionCanvasPersistence();
        await WaitForDefinitionCanvasPersistenceIdleAsync();
        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
        var result = await ProcessesService.SaveAsync(editor);
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedProcessId = result.Value;
        await LoadWorkspaceAsync();
        SetMessage("Process definition saved.");
    }

    private async Task PublishAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            SetError("Save the process definition before publishing it.");
            return;
        }

        var result = await ProcessesService.PublishAsync(selectedProcessId.Value);
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        SetMessage("Process definition published.");
    }

    private async Task DeleteAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            return;
        }

        await ProcessesService.DeleteAsync(selectedProcessId.Value);
        selectedProcessId = null;
        selectedRunId = null;
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        await LoadWorkspaceAsync();
        SetMessage("Process definition deleted.");
    }

    private async Task SeedBaselineAsync()
    {
        var result = await SeedService.SeedBaselineAsync(ProjectId);
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedProcessId = result.Value!.PrimaryDefinitionId;
        selectedRunId = result.Value.SeededRunIds.FirstOrDefault();
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        detailTab = "runs";
        await LoadWorkspaceAsync();
        SetMessage("Development seed baseline prepared.");
    }

    private async Task StartRunAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            SetError("Select a process definition before starting a run.");
            return;
        }

        var result = await ProcessesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = selectedProcessId.Value,
                ProjectId = ProjectId,
                RunName = string.IsNullOrWhiteSpace(runNameDraft) ? string.Empty : runNameDraft,
                OperatingMode = runOperatingMode,
                TriggerReason = "Started from process workspace."
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedRunId = result.Value;
        selectedCanvasNodeId = null;
        ResetRuntimeCanvasState();
        detailTab = "runs";
        runNameDraft = string.Empty;
        await LoadWorkspaceAsync();
        SetMessage("Process run started.");
    }

    private async Task SelectRunAsync(Guid runId)
    {
        selectedRunId = runId;
        selectedCanvasNodeId = null;
        ResetRuntimeCanvasState();
        await LoadRunDetailsAsync();
        RefreshCanvasSurface();
    }

    private async Task ApplyStepStatusAsync(Guid stepRunId, ProcessStepRunStatus targetStatus)
    {
        var selectedBranchOutcomeId = targetStatus == ProcessStepRunStatus.Completed
            ? ResolveSelectedBranchOutcomeId(stepRunId)
            : null;
        var result = await ProcessesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRunId,
                TargetStatus = targetStatus,
                Reason = BuildTransitionReason(targetStatus, stepRunId, selectedBranchOutcomeId),
                SelectedBranchOutcomeId = selectedBranchOutcomeId,
                DecidedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage($"Step updated to {targetStatus}.");
    }

    private async Task ExportAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            return;
        }

        var envelope = await ProcessesService.ExportAsync(selectedProcessId.Value);
        exportJson = JsonSerializer.Serialize(envelope, JsonOptions);
        detailTab = "exchange";
        SetMessage("Process definition exported.");
    }

    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(importJson))
        {
            SetError("Paste an import envelope before running import.");
            return;
        }

        ProcessImportExportEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ProcessImportExportEnvelope>(importJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            SetError($"Import envelope is not valid JSON. {exception.Message}");
            return;
        }

        if (envelope is null)
        {
            SetError("Import envelope could not be parsed.");
            return;
        }

        var result = await ProcessesService.ImportAsync(envelope);
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedProcessId = result.Value;
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        await LoadWorkspaceAsync();
        SetMessage("Process definition imported.");
    }

    private void AddRole()
    {
        editor.Roles.Add(new ProcessRoleEditorModel
        {
            Id = Guid.NewGuid(),
            DisplayName = $"Role {editor.Roles.Count + 1}",
            DefaultAllocationPercent = 100
        });
        RefreshCanvasSurface();
    }

    private void RemoveRole(ProcessRoleEditorModel role, bool refreshSurface = true)
    {
        editor.Roles.Remove(role);
        foreach (var step in editor.Steps)
        {
            step.RoleAssignments.RemoveAll(item => item.RoleRequirementId == role.Id);
            if (step.DecisionRoleRequirementId == role.Id)
            {
                step.DecisionRoleRequirementId = null;
            }
        }

        if (refreshSurface)
        {
            RefreshCanvasSurface();
        }
    }

    private void AddStep()
    {
        var previousStep = editor.Steps.LastOrDefault();
        editor.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = $"Step {editor.Steps.Count + 1}",
            StepKind = ProcessStepKind.Work,
            TargetLeadHours = 1,
            CanvasX = 140 + (editor.Steps.Count * 280),
            CanvasY = 180,
            Dependencies = previousStep?.Id.HasValue == true
                ? [new ProcessStepDependencyEditorModel { Id = Guid.NewGuid(), DependsOnStepId = previousStep.Id }]
                : []
        });
        RefreshCanvasSurface();
    }

    private void RemoveStep(ProcessStepEditorModel step, bool refreshSurface = true)
    {
        editor.Steps.Remove(step);
        foreach (var candidate in editor.Steps)
        {
            SetStepDependencies(
                candidate,
                ProcessCanvasBranching.GetOrderedDependencies(candidate)
                    .Where(dependency => dependency.DependsOnStepId != step.Id));
        }

        if (refreshSurface)
        {
            RefreshCanvasSurface();
        }
    }

    private void AddBranchOutcome(ProcessStepEditorModel step)
    {
        if (editor.Steps.Contains(step))
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
        }
        else
        {
            ProcessCanvasBranching.NormalizeStepDraft(step);
        }

        var customOutcomeCount = ProcessCanvasBranching.GetCustomBranchOutcomes(step).Count;
        step.BranchOutcomes.Add(new ProcessStepBranchOutcomeEditorModel
        {
            Id = Guid.NewGuid(),
            Key = $"outcome-{customOutcomeCount + 1}",
            Title = $"Outcome {customOutcomeCount + 1}"
        });
        if (editor.Steps.Contains(step))
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
        }
        else
        {
            ProcessCanvasBranching.NormalizeStepDraft(step);
        }

        RefreshCanvasSurface();
    }

    private void RemoveBranchOutcome(ProcessStepEditorModel step, ProcessStepBranchOutcomeEditorModel branchOutcome)
    {
        if (ProcessCanvasBranching.IsSystemOutcome(branchOutcome))
        {
            return;
        }

        step.BranchOutcomes.Remove(branchOutcome);
        if (!branchOutcome.Id.HasValue)
        {
            ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
            RefreshCanvasSurface();
            return;
        }

        foreach (var candidate in editor.Steps)
        {
            SetStepDependencies(
                candidate,
                ProcessCanvasBranching.GetOrderedDependencies(candidate)
                    .Where(dependency => dependency.DependsOnBranchOutcomeId != branchOutcome.Id.Value));
        }

        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
        RefreshCanvasSurface();
    }

    private void AddRoleAssignment(ProcessStepEditorModel step)
    {
        step.RoleAssignments.Add(new ProcessStepRoleRequirementEditorModel
        {
            RoleRequirementId = editor.Roles.FirstOrDefault()?.Id,
            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
            IsRequired = true
        });
        RefreshCanvasSurface();
    }

    private void RemoveRoleAssignment(ProcessStepEditorModel step, ProcessStepRoleRequirementEditorModel assignment)
    {
        step.RoleAssignments.Remove(assignment);
        RefreshCanvasSurface();
    }

    private void AddArtifact(ProcessStepEditorModel step)
    {
        step.ArtifactExpectations.Add(new ProcessArtifactExpectationEditorModel
        {
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "New artifact",
            IsRequired = true,
            RetentionDays = 90
        });
        RefreshCanvasSurface();
    }

    private void RemoveArtifact(ProcessStepEditorModel step, ProcessArtifactExpectationEditorModel artifact)
    {
        step.ArtifactExpectations.Remove(artifact);
        RefreshCanvasSurface();
    }

    private Task HandleDetailTabChanged(int index)
    {
        detailTab = ResolveDetailTabKey(index);
        RefreshCanvasSurface();
        return Task.CompletedTask;
    }

    private void SelectAssignment(Guid assignmentId)
    {
        selectedAssignmentId = assignmentId;
        ApplyAssignmentSelection();
    }

    private void ApplyAssignmentSelection()
    {
        var assignment = SelectedAssignment;
        if (assignment is null)
        {
            assignmentPartyId = null;
            assignmentDisplayName = string.Empty;
            assignmentExecutorKind = "person";
            assignmentBindingReason = string.Empty;
            assignmentIsFallback = false;
            return;
        }

        assignmentPartyId = assignment.PartyId;
        assignmentDisplayName = assignment.DisplayName;
        assignmentExecutorKind = string.IsNullOrWhiteSpace(assignment.ExecutorKind) ? "person" : assignment.ExecutorKind;
        assignmentBindingReason = assignment.BindingReason;
        assignmentIsFallback = assignment.IsFallback;
    }

    private async Task ResolveSelectedAssignmentAsync()
    {
        var assignment = SelectedAssignment;
        if (assignment is null || !selectedRunId.HasValue)
        {
            SetError("Select a run assignment before resolving it.");
            return;
        }

        var result = await ProcessesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = selectedRunId.Value,
                RoleRequirementId = assignment.RoleRequirementId,
                StepDefinitionId = assignment.StepDefinitionId,
                PartyId = assignmentPartyId,
                DisplayName = assignmentDisplayName,
                ExecutorKind = assignmentExecutorKind,
                BindingReason = string.IsNullOrWhiteSpace(assignmentBindingReason)
                    ? "Resolved from the process workspace."
                    : assignmentBindingReason,
                IsFallback = assignmentIsFallback
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Run assignment resolved.");
    }

    private async Task RecordArtifactAsync()
    {
        if (!selectedRunId.HasValue)
        {
            SetError("Start or select a run before recording artifacts.");
            return;
        }

        var result = await ProcessesService.RecordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = selectedRunId.Value,
                StepRunId = artifactStepRunId,
                ArtifactKind = artifactKind,
                Title = artifactTitle,
                TrustStatus = artifactTrustStatus,
                SensitivityLevel = artifactSensitivityLevel,
                ProvenanceSummary = artifactProvenance,
                AllowedFutureUsageSummary = artifactAllowedUsage,
                ReviewSummary = artifactReview
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        artifactTitle = string.Empty;
        artifactProvenance = string.Empty;
        artifactAllowedUsage = string.Empty;
        artifactReview = string.Empty;
        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Artifact recorded.");
    }

    private string BuildDefinitionSummary(ProcessDefinitionListItem definition)
    {
        var scope = string.IsNullOrWhiteSpace(definition.ProjectName) ? "Global" : definition.ProjectName;
        return $"{scope} / v{definition.LatestVersionNumber} / {definition.RoleCount} roles / {definition.StepCount} steps";
    }

    private static string BuildRunSummary(ProcessRunListItem run)
    {
        return $"{run.Status} / {run.CompletedStepCount} of {run.TotalStepCount} steps / {run.CapabilityGapCount} gaps";
    }

    private string BuildTransitionReason(ProcessStepRunStatus status, Guid stepRunId, Guid? selectedBranchOutcomeId)
    {
        var branchOutcomeTitle = selectedBranchOutcomeId.HasValue
            ? stepRuns
                .FirstOrDefault(item => item.Id == stepRunId)?
                .AvailableBranchOutcomes
                .FirstOrDefault(item => item.Id == selectedBranchOutcomeId.Value)?
                .Title
            : null;
        return status switch
        {
            ProcessStepRunStatus.InProgress => "Work started from the runtime workspace.",
            ProcessStepRunStatus.Completed when !string.IsNullOrWhiteSpace(branchOutcomeTitle) => $"Work completed from the runtime workspace with branch outcome '{branchOutcomeTitle}'.",
            ProcessStepRunStatus.Completed => "Work completed from the runtime workspace.",
            ProcessStepRunStatus.Blocked => "Blocked from the runtime workspace for review.",
            ProcessStepRunStatus.Refused => "Executor recorded a safe refusal from the runtime workspace.",
            ProcessStepRunStatus.WaitingApproval => "Approval was requested from the runtime workspace.",
            ProcessStepRunStatus.Failed => "Failure was captured from the runtime workspace.",
            ProcessStepRunStatus.Skipped => "Step was skipped from the runtime workspace.",
            _ => "State updated from the runtime workspace."
        };
    }

    private Guid? ResolveSelectedBranchOutcomeId(Guid stepRunId)
    {
        return runtimeBranchOutcomeSelections.TryGetValue(stepRunId, out var selectedBranchOutcomeId)
            ? selectedBranchOutcomeId
            : stepRuns.FirstOrDefault(item => item.Id == stepRunId)?.SelectedBranchOutcomeId;
    }

    private Task UpdateRuntimeBranchOutcomeSelectionAsync(Guid stepRunId, Guid? branchOutcomeId)
    {
        runtimeBranchOutcomeSelections[stepRunId] = branchOutcomeId;
        return Task.CompletedTask;
    }

    private IReadOnlyList<ProcessStepBranchOutcomeEditorModel> GetDependencyOutcomeOptions(ProcessStepEditorModel step)
    {
        var dependencyStepId = ProcessCanvasBranching.GetOrderedDependencies(step)
            .FirstOrDefault()?.DependsOnStepId;
        if (!dependencyStepId.HasValue)
        {
            return [];
        }

        return editor.Steps
            .FirstOrDefault(candidate => candidate.Id == dependencyStepId.Value)?
            .BranchOutcomes
            ?? [];
    }

    private static void SetStepDependencies(
        ProcessStepEditorModel step,
        IEnumerable<ProcessStepDependencyEditorModel> dependencies)
    {
        var materialized = dependencies
            .Where(dependency => dependency.DependsOnStepId.HasValue)
            .Select(dependency => new ProcessStepDependencyEditorModel
            {
                Id = dependency.Id ?? Guid.NewGuid(),
                DependsOnStepId = dependency.DependsOnStepId,
                DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
            })
            .ToList();
        step.Dependencies = materialized;
        var primaryDependency = materialized.FirstOrDefault();
        step.DependsOnStepId = primaryDependency?.DependsOnStepId;
        step.DependsOnBranchOutcomeId = primaryDependency?.DependsOnBranchOutcomeId;
    }

    private string ResolveRoleName(Guid? roleId)
    {
        if (!roleId.HasValue)
        {
            return "Unbound";
        }

        return editor.Roles.FirstOrDefault(role => role.Id == roleId.Value)?.DisplayName ?? "Unknown role";
    }

    private string ResolveDefinitionStatusTone(ProcessDefinitionStatus status)
    {
        return status switch
        {
            ProcessDefinitionStatus.Published => "info",
            ProcessDefinitionStatus.Archived => "neutral",
            _ => "warning"
        };
    }

    private static string ResolveRunTone(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Completed => "mint",
            ProcessRunStatus.Active => "info",
            ProcessRunStatus.Blocked => "warning",
            ProcessRunStatus.Failed => "danger",
            ProcessRunStatus.Cancelled => "neutral",
            _ => "neutral"
        };
    }

    private static bool CanApplyRuntimeStatus(ProcessStepRunViewModel? stepRun, ProcessStepRunStatus targetStatus)
    {
        return stepRun is not null &&
            stepRun.Status != targetStatus &&
            ProcessStepRunTransitions.IsAllowed(stepRun.Status, targetStatus);
    }

    private static string ResolveStepTone(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Completed => "mint",
            ProcessStepRunStatus.InProgress => "info",
            ProcessStepRunStatus.Blocked => "danger",
            ProcessStepRunStatus.Refused => "warning",
            ProcessStepRunStatus.WaitingApproval => "accent",
            ProcessStepRunStatus.Failed => "danger",
            _ => "neutral"
        };
    }

    private static string ResolveConformanceTone(ProcessConformanceSeverity severity)
    {
        return severity switch
        {
            ProcessConformanceSeverity.Critical => "danger",
            ProcessConformanceSeverity.High => "warning",
            ProcessConformanceSeverity.Moderate => "info",
            _ => "neutral"
        };
    }

    private CanvasWorkbenchUiState ResolveStoredCanvasUiState()
        => IsRuntimeCanvasActive
            ? runtimeCanvasUiState
            : definitionCanvasUiState;

    private void StoreCanvasUiState(CanvasWorkbenchUiState uiState)
    {
        var storedState = CloneCanvasUiState(uiState);
        if (IsRuntimeCanvasActive)
        {
            runtimeCanvasUiState = storedState;
        }
        else
        {
            definitionCanvasUiState = storedState;
        }
    }

    private CanvasWorkbenchUiState BuildCanvasUiState(CanvasWorkbenchSurface surface, CanvasWorkbenchUiState storedUiState)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(storedUiState);

        var uiState = CloneCanvasUiState(storedUiState);
        var availableNodeIds = surface.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (string.Equals(selectedCanvasNodeId, NoCanvasSelection, StringComparison.Ordinal))
        {
            uiState.SelectedNodeIds = [];
        }
        else if (!string.IsNullOrWhiteSpace(selectedCanvasNodeId) && availableNodeIds.Contains(selectedCanvasNodeId))
        {
            uiState.SelectedNodeIds = [selectedCanvasNodeId];
        }
        else
        {
            uiState.SelectedNodeIds = uiState.SelectedNodeIds
                .Where(availableNodeIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (uiState.SelectedNodeIds.Count == 0)
            {
                uiState.SelectedNodeIds = surface.UiState.SelectedNodeIds
                    .Where(availableNodeIds.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }

        if (string.IsNullOrWhiteSpace(uiState.ActiveInspectorTab))
        {
            uiState.ActiveInspectorTab = surface.UiState.ActiveInspectorTab;
        }

        return uiState;
    }

    private void ResetDefinitionCanvasState()
    {
        definitionCanvasTool = DefinitionCanvasSelectTool;
        definitionCanvasUiState = CreateDefaultDefinitionCanvasUiState();
    }

    private void ResetRuntimeCanvasState()
    {
        runtimeCanvasUiState = CreateDefaultRuntimeCanvasUiState();
    }

    private static CanvasWorkbenchUiState CreateDefaultDefinitionCanvasUiState()
        => new()
        {
            ActiveInspectorTab = "definition"
        };

    private Task SelectDefinitionCanvasToolAsync()
    {
        SetDefinitionCanvasTool(DefinitionCanvasSelectTool);
        return Task.CompletedTask;
    }

    private Task DeleteDefinitionCanvasToolAsync()
    {
        SetDefinitionCanvasTool(DefinitionCanvasDeleteTool);
        return Task.CompletedTask;
    }

    private void SetDefinitionCanvasTool(string tool)
    {
        definitionCanvasTool = string.Equals(tool, DefinitionCanvasDeleteTool, StringComparison.Ordinal)
            ? DefinitionCanvasDeleteTool
            : DefinitionCanvasSelectTool;
        if (IsDefinitionCanvasActive)
        {
            RefreshCanvasSurface();
        }
    }

    private static CanvasWorkbenchUiState CreateDefaultRuntimeCanvasUiState()
        => new()
        {
            ActiveInspectorTab = "runtime"
        };

    private static CanvasWorkbenchUiState CloneCanvasUiState(CanvasWorkbenchUiState uiState)
        => CanvasWorkbenchUiState.Parse(uiState.ToJson());

    private static int ResolveDetailTabIndex(string key)
    {
        for (var index = 0; index < DetailTabs.Count; index++)
        {
            if (string.Equals(DetailTabs[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private static string ResolveDetailTabKey(int index)
    {
        if (index >= 0 && index < DetailTabs.Count)
        {
            return DetailTabs[index].Key;
        }

        return DetailTabs[0].Key;
    }

    private void SetMessage(string value)
    {
        message = value;
        isError = false;
    }

    private void SetError(string value)
    {
        message = value;
        isError = true;
    }

    private void SetError(IEnumerable<Error> errors)
    {
        SetError(string.Join(" ", errors.Select(error => error.Message)));
    }

    private void ClearMessage()
    {
        message = string.Empty;
        isError = false;
    }
}
