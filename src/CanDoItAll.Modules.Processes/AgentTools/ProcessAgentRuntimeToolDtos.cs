using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Processes;

public sealed class InternalProcessTemplateImportRequest
{
    public string ProcessKey { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string DefinitionName { get; set; } = string.Empty;

    public bool AutoPublish { get; set; } = true;
}

public sealed class ProcessDefinitionRoleAddRequest
{
    public Guid DefinitionId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string Responsibilities { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public ProjectPartyAssignmentRole? PreferredProjectAssignmentRole { get; set; }

    public bool IsRequired { get; set; } = true;

    public bool AllowsFallback { get; set; } = true;

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; } = 100;

    public string SnapshotSummary { get; set; } = string.Empty;

    public double? CanvasX { get; set; }

    public double? CanvasY { get; set; }

    public bool PublishIfValid { get; set; }
}

public sealed record ProcessDefinitionRoleAddResult(
    Guid DefinitionId,
    Guid RoleRequirementId,
    string RoleName,
    bool PublishAttempted,
    bool Published,
    string PublishErrorCode,
    string PublishErrorMessage);

public sealed record InternalProcessRunDetailToolData(
    ProcessRunListItem Run,
    ProcessRunHealthSummaryViewModel Health,
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessDecisionViewModel> DecisionRecords,
    IReadOnlyList<ProcessArtifactViewModel> Artifacts,
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations,
    IReadOnlyList<ProcessImprovementViewModel> Improvements);

public sealed record InternalProcessTemplateDetailToolData(
    ProcessTemplateCatalogItem Summary,
    ProcessTemplateDefinition Template,
    string CompatibilityReportMarkdown,
    IReadOnlyList<string> SupportingFiles);
