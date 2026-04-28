using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace : ComponentBase, IDisposable, IAsyncDisposable
{
    private const string DefinitionCanvasSelectTool = "authoring";
    private const string DefinitionCanvasDeleteTool = "delete";
    private const string CompactHelpPopoverRootClass = "pf-help-popover flex items-center";
    private const string CompactHelpPopoverToggleClass = "pf-help-popover__toggle pf-help-popover__toggle--compact";

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
    private ProcessTemplateCatalogService ProcessTemplateCatalogService { get; set; } = default!;

    [Inject]
    private ProjectsService ProjectsService { get; set; } = default!;

    [Inject]
    private ProcessWorkspaceRunDetailsLoader RunDetailsLoader { get; set; } = default!;

    [Inject]
    private IProcessEscalationService EscalationService { get; set; } = default!;

    [Inject]
    private IAgentFrameworkWorkspaceService AgentWorkspaceService { get; set; } = default!;

    [Inject]
    private ProcessCatalogWarmupService CatalogWarmupService { get; set; } = default!;

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
    private IReadOnlyList<ProcessEscalationViewModel> processEscalations = [];
    private IReadOnlyList<ProcessOperatorApprovalViewModel> operatorApprovals = [];
    private IReadOnlyList<ProcessAttemptTimelineEntryViewModel> attemptTimeline = [];
    private IReadOnlyList<ProcessActiveRunSummaryViewModel> activeRunSummaries = [];
    private IReadOnlyList<ProcessImprovementViewModel> improvements = [];
    private IReadOnlyList<ProcessExecutorRegistryOption> executorOptions = [];
    private IReadOnlyList<ProjectPartyOption> partyOptions = [];

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
    private bool assignmentAllowsDirectMessaging = true;
    private Guid? directMessageSourceRoleRequirementId;
    private Guid? directMessageTargetRoleRequirementId;
    private string directMessageBody = string.Empty;
    private Guid? operatorReworkStepRunId;
    private string operatorReworkDirective = string.Empty;
    private string operatorEscalationOwner = "process-workspace";
    private string operatorEscalationResolution = string.Empty;
    private string operatorApprovalDecisionSummary = string.Empty;
    private Dictionary<Guid, Guid?> runtimeBranchOutcomeSelections = [];
    private IReadOnlyList<ProcessDirectMessageThreadViewModel> directMessageThreads = [];
    private string exportJson = string.Empty;
    private string importJson = string.Empty;
    private string projectName = string.Empty;
    private string message = string.Empty;
    private bool isError;
    private bool isFeedingDefaults;
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

    private IReadOnlyList<ProcessRunAssignmentViewModel> DirectMessageAssignments
        => assignments
            .Where(item => !item.StepDefinitionId.HasValue)
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
        => string.IsNullOrWhiteSpace(editor.Name)
            ? "New process definition"
            : editor.Name;

    private string SelectedDefinitionTone
        => ResolveDefinitionStatusTone(editor.Status);

    private int SelectedDetailTabIndex
        => ResolveDetailTabIndex(detailTab);

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
}
