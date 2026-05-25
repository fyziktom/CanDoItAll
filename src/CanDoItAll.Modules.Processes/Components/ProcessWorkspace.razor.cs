using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace : ComponentBase, IDisposable, IAsyncDisposable
{
    private const string DefinitionCanvasSelectTool = "authoring";
    private const string DefinitionCanvasDeleteTool = "delete";
    private const string CompactHelpPopoverRootClass = "pf-help-popover flex items-center";
    private const string CompactHelpPopoverToggleClass = "pf-help-popover__toggle pf-help-popover__toggle--compact";
    private const string DetailTabDefinition = "definition";
    private const string DetailTabRoles = "roles";
    private const string DetailTabSteps = "steps";
    private const string DetailTabRuns = "runs";
    private const string DetailTabAnalytics = "analytics";
    private const string DetailTabExchange = "exchange";
    private const string DetailTabManagerChat = "manager-chat";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<SecondaryTabItem> DetailTabs =
    [
        new(DetailTabDefinition, "Definition", Description: "Identity, governance, and publication controls."),
        new(DetailTabRoles, "Roles", Description: "Role-first staffing semantics and executor intent."),
        new(DetailTabSteps, "Steps", Description: "Typed workflow steps, bindings, artifacts, and authoring canvas."),
        new(DetailTabRuns, "Runs", Description: "Runtime state, assignments, work briefs, and evidence capture."),
        new(DetailTabAnalytics, "Analytics", Description: "Economics, conformance, capability gaps, and improvement signals."),
        new(DetailTabExchange, "Exchange", Description: "Import, export, and future executor-registry seam review."),
        new(DetailTabManagerChat, "Manager chat", Description: "Conversation with the responsible process manager agent.")
    ];

    [Inject]
    private ProcessesService ProcessesService { get; set; } = default!;

    [Inject]
    private ProcessDevelopmentSeedService SeedService { get; set; } = default!;

    [Inject]
    private ProcessCanvasSurfaceFactory CanvasSurfaceFactory { get; set; } = default!;

    [Inject]
    private ProcessTemplateCatalogService ProcessTemplateCatalogService { get; set; } = default!;

    [Inject]
    private ProjectsService ProjectsService { get; set; } = default!;

    [Inject]
    private ProcessWorkspaceRunDetailsLoader RunDetailsLoader { get; set; } = default!;

    [Inject]
    private ProcessRuntimeStateOverviewService RuntimeStateOverviewService { get; set; } = default!;

    [Inject]
    private IProcessObservationService ProcessObservationService { get; set; } = default!;

    [Inject]
    private IProcessObservationInvalidator ProcessObservationInvalidator { get; set; } = default!;

    [Inject]
    private ProcessObservationDashboardState ObservationDashboardState { get; set; } = default!;

    [Inject]
    private DialogService DialogService { get; set; } = default!;

    [Inject]
    private IProcessEscalationService EscalationService { get; set; } = default!;

    [Inject]
    private IAgentFrameworkWorkspaceService AgentWorkspaceService { get; set; } = default!;

    [Inject]
    private ProcessCatalogWarmupService CatalogWarmupService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

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
    private IReadOnlyList<ProcessOutboxRecordViewModel> outboxRecords = [];
    private IReadOnlyList<ProcessRunAssignmentViewModel> assignments = [];
    private IReadOnlyList<ProcessWorkBriefViewModel> workBriefs = [];
    private IReadOnlyList<ProcessConformanceObservationViewModel> conformanceObservations = [];
    private IReadOnlyList<ProcessExecutionRunViewModel> executionRuns = [];
    private IReadOnlyList<ProcessWorkflowRunViewModel> workflowRuns = [];
    private IReadOnlyList<ProcessEscalationViewModel> processEscalations = [];
    private IReadOnlyList<ProcessOperatorApprovalViewModel> operatorApprovals = [];
    private IReadOnlyList<ProcessAttemptTimelineEntryViewModel> attemptTimeline = [];
    private IReadOnlyList<ProcessActiveRunSummaryViewModel> activeRunSummaries = [];
    private IReadOnlyList<ProcessImprovementViewModel> improvements = [];
    private IReadOnlyList<ProcessExecutorRegistryOption> executorOptions = [];
    private IReadOnlyList<ProcessWorkflowDefinitionOption> workflowOptions = [];
    private IReadOnlyList<ProjectPartyOption> partyOptions = [];
    private IReadOnlyList<ProcessManagerAgentOption> managerAgentOptions = [];

    private ProcessRuntimeStateOverview runtimeStateOverview = ProcessRuntimeStateOverview.Empty(null);
    private ProcessAnalyticsSummary analytics = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private ProcessRunHealthSummaryViewModel selectedRunHealth = ProcessRunHealthSummaryViewModel.Empty;
    private ProcessDefinitionEditorModel editor = new();
    private CanvasWorkbenchSurface? canvasSurface;
    private CanvasWorkbenchUiState definitionCanvasUiState = CreateDefaultDefinitionCanvasUiState();
    private CanvasWorkbenchUiState runtimeCanvasUiState = CreateDefaultRuntimeCanvasUiState();
    private string definitionCanvasTool = DefinitionCanvasSelectTool;

    private Guid? selectedProcessId;
    private Guid? selectedRunId;
    private Guid? selectedAssignmentId;
    private string detailTab = DetailTabDefinition;
    private string definitionSearch = string.Empty;
    private readonly HashSet<string> expandedProcessTreeNodeIds = [];
    private readonly ProcessRunListFilterState runHistoryFilter = new();
    private readonly ProcessRunListFilterState analyticsRunFilter = new();
    private readonly ProcessImprovementFilterState improvementFilter = new();
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
    private Guid? assignmentWorkflowDefinitionId;
    private Guid? assignmentWorkflowVersionId;
    private string assignmentDisplayName = string.Empty;
    private string assignmentExecutorKind = "person";
    private string assignmentBindingReason = string.Empty;
    private bool assignmentIsFallback;
    private bool assignmentAllowsDirectMessaging = true;
    private Guid? directMessageSourceRoleRequirementId;
    private Guid? directMessageTargetRoleRequirementId;
    private string directMessageBody = string.Empty;
    private Guid? operatorReworkStepRunId;
    private string operatorReworkDirective = string.Empty;
    private string operatorManagerDirective = string.Empty;
    private string operatorEscalationOwner = "process-workspace";
    private string operatorEscalationResolution = string.Empty;
    private string operatorApprovalDecisionSummary = string.Empty;
    private Dictionary<Guid, Guid?> runtimeBranchOutcomeSelections = [];
    private IReadOnlyList<ProcessDirectMessageThreadViewModel> directMessageThreads = [];
    private string exportJson = string.Empty;
    private string importJson = string.Empty;
    private string projectName = string.Empty;
    private bool isFeedingDefaults;
    private readonly CancellationTokenSource componentLifetimeCts = new();
    private CancellationTokenSource? deferredWorkspaceLoadCts;
    private Task? deferredWorkspaceLoadTask;
    private Guid? stoppingRunId;
    private bool hasLoadedParameters;
    private Guid? loadedProjectId;
    private Guid? loadedProcessQueryId;
    private Guid? loadedRunQueryId;
    private bool isEditorLoading;
    private bool executorOptionsLoaded;
    private bool workflowOptionsLoaded;
    private bool managerAgentOptionsLoaded;
    private Guid? partyOptionsLoadedProjectId;
    private bool analyticsLoaded;
    private Guid? analyticsLoadedProcessId;
    private Guid? analyticsLoadedProjectId;
    private bool improvementsLoaded;
    private Guid? improvementsLoadedProcessId;

    private IReadOnlyList<ProcessDefinitionListItem> FilteredDefinitions => definitions
        .Where(definition =>
            string.IsNullOrWhiteSpace(definitionSearch) ||
            definition.Name.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase) ||
            definition.Summary.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase) ||
            definition.ValueStatement.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(definition => definition.UpdatedAtUtc)
        .ToList();

    private IReadOnlyList<TreeViewNode> ProcessDefinitionTreeNodes
        => ProcessDefinitionTreeNodeBuilder.Build(
            FilteredDefinitions,
            selectedProcessId,
            expandedProcessTreeNodeIds);

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

    private IReadOnlyList<ProcessSubprocessDefinitionOption> SubprocessDefinitionOptions
        => definitions
            .Where(definition => editor.Id != definition.Id)
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition => new ProcessSubprocessDefinitionOption(
                definition.Id,
                definition.Name,
                definition.Status,
                string.IsNullOrWhiteSpace(definition.ProjectName) ? "Global" : definition.ProjectName))
            .ToList();

    private IReadOnlyList<ProcessRunAssignmentViewModel> DirectMessageAssignments
        => assignments
            .Where(item =>
                !item.StepDefinitionId.HasValue &&
                !ProcessExecutorKindNames.IsWorkflow(item.ExecutorKind))
            .OrderBy(
                item => string.IsNullOrWhiteSpace(item.RoleDisplayName)
                    ? ResolveRoleName(item.RoleRequirementId)
                    : item.RoleDisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

    private string PageEyebrow
        => ProjectId.HasValue
            ? "Project processes"
            : "Processes";

    private string PageTitleText
        => ProjectId.HasValue && !string.IsNullOrWhiteSpace(projectName)
            ? $"{projectName} processes"
            : "Process management";

    private string EditorTitle
        => !string.IsNullOrWhiteSpace(editor.Name)
            ? editor.Name
            : SelectedDefinitionSummary?.Name ?? "New process definition";

    private string SelectedDefinitionTone
        => ResolveDefinitionStatusTone(editor.Status);

    private int SelectedDefinitionRoleCount
        => isEditorLoading && SelectedDefinitionSummary is not null
            ? SelectedDefinitionSummary.RoleCount
            : editor.Roles.Count;

    private int SelectedDefinitionStepCount
        => isEditorLoading && SelectedDefinitionSummary is not null
            ? SelectedDefinitionSummary.StepCount
            : editor.Steps.Count;

    private bool ShouldShowEditorLoadingState
        => isEditorLoading && selectedProcessId.HasValue;

    private int SelectedDetailTabIndex
        => ResolveDetailTabIndex(detailTab);

    private string DefinitionCountText => FormatCount(definitions.Count, "definition", "definitions");

    private string VisibleDefinitionCountText => $"{FilteredDefinitions.Count} visible";

    private string ActiveRunCountText => FormatCount(runtimeStateOverview.Totals.Active, "active run", "active runs");

    private string BlockedRunCountText => FormatCount(runtimeStateOverview.Totals.Blocked, "blocked run", "blocked runs");

    private string FailedRunCountText => FormatCount(runtimeStateOverview.Totals.Failed, "failed run", "failed runs");

    private string ImprovementCountText => FormatCount(analytics.ImprovementCandidateCount, "improvement", "improvements");

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

    private static string FormatCount(int count, string singularLabel, string pluralLabel)
    {
        var label = count == 1 ? singularLabel : pluralLabel;
        return $"{count} {label}";
    }

    private ProcessRunStatusCounts ResolveDefinitionRunCounts(Guid definitionId)
    {
        return runtimeStateOverview.GetDefinition(definitionId).RunCounts;
    }

    private void SetMessage(string value)
    {
        NotificationService.Success("Processes updated", value);
    }

    private void SetError(string value)
    {
        NotificationService.Error("Processes update failed", value);
    }

    private void SetError(IEnumerable<Error> errors)
    {
        SetError(string.Join(" ", errors.Select(error => error.Message)));
    }

    private void ClearMessage()
    {
    }

    private async Task FeedDefaultsAsync()
    {
        if (isFeedingDefaults)
        {
            return;
        }

        isFeedingDefaults = true;
        ClearMessage();

        try
        {
            await CatalogWarmupService.WarmupAsync(synchronizeExistingDefinitions: true);
            InvalidateObservationState();
            await LoadWorkspaceAsync();
            SetMessage("Default processes were synchronized from the current template pack.");
        }
        catch (Exception exception)
        {
            SetError($"Failed to synchronize default processes. {exception.Message}");
        }
        finally
        {
            isFeedingDefaults = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void InvalidateObservationState(Guid? definitionId = null, Guid? runId = null)
    {
        RuntimeStateOverviewService.Invalidate();
        if (runId.HasValue)
        {
            var definitionForRun = runs.FirstOrDefault(item => item.Id == runId.Value)?.ProcessDefinitionId ??
                definitionId ??
                selectedProcessId ??
                Guid.Empty;
            ProcessObservationInvalidator.NotifyRunChanged(new ProcessRunObservationKey(ProjectId, definitionForRun, runId.Value));
            return;
        }

        if (definitionId.HasValue)
        {
            ProcessObservationInvalidator.NotifyDefinitionChanged(new ProcessDefinitionObservationKey(ProjectId, definitionId.Value));
            return;
        }

        ProcessObservationInvalidator.NotifyProjectChanged(ProjectId);
    }
}
