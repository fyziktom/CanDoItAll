using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

public interface IProcessRunAutomationDispatchService
{
    Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync = null,
        CancellationToken cancellationToken = default);
}

internal sealed partial class ProcessRunAutomationDispatchService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IServiceScopeFactory serviceScopeFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IAgentFrameworkWorkspaceService workspaceService,
    IStoragePlacementService storagePlacementService,
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    ProcessWorkflowRunCoordinator workflowRunCoordinator,
    IOptions<ProcessRuntimeOptions> processRuntimeOptions,
    IClock clock,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessRunAutomationDispatchService
{
    private const string AutomationActor = "process-automation-dispatch";
    private const string ExternalTargetAliasRoot = "external-target";
    private const string ProcessStepOutcomeFinalizerMissingErrorCode = "agent.finalizer.missing";
    private const string ProcessStepOutcomeFinalizerMultipleCallsErrorCode = "agent.finalizer.multiple_calls";
    private const int DefaultMaxExecutionAttempts = 3;
    private const int ConcreteImplementationMaxExecutionAttempts = 5;
    private const int MaxBrowserSnapshotInspectionCharacters = 262_144;
    private static readonly TimeSpan ProviderFallbackHealthProbeTimeout = TimeSpan.FromSeconds(15);
    private const string ProcessMockSessionFlagPropertyName = "processMockAgent";
    private const string ProcessMockRoleKeyPropertyName = "roleKey";
    private const string ProcessMockArtifactRootPropertyName = "artifactRoot";
    private const string ProcessMockBranchOutcomeKeyPropertyName = "branchOutcomeKey";
    private const string ProcessMockProductOwnerRoleKey = "product-owner";
    private const string ProcessMockArchitectRoleKey = "architect";
    private const string ProcessMockDeveloperRoleKey = "developer";
    private const string ProcessMockQaRoleKey = "qa";
    private const string ProcessMockRepairDeveloperRoleKey = "repair-developer";
    private const string ProcessMockReleaseManagerRoleKey = "release-manager";
    private const string ProcessMockBranchRepairsRequired = "repairs-required";
    private const string ProcessMockBranchApproved = "approved";
    private static readonly TimeSpan FreshInProgressRecoveryGracePeriod = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StaleAutomationExecutionRunTimeout = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> StepDispatchGuards = [];
    private static readonly Regex RequiredToolNameRegex = new(
        @"\b(?:workspace|browser|project_structure|image_generation)_[a-z0-9_]+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WorkspacePathInToolRequestRegex = new(
        @"(?<path>[A-Za-z]:\\[^`""'\r\n\s]+|external-target[\\/][^\s`""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ManagedWorkspacePathRegex = new(
        @"(?<path>(?:artifacts|output|integration-map|data)/(?:scopes/[^\s`""']+|[^\s`""']+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] NegatedRequiredToolPhrases =
    [
        "do not",
        "don't",
        "must not",
        "should not",
        "shall not",
        "cannot",
        "can't",
        "never",
        "without"
    ];
    private static readonly HashSet<string> NonCriticalWorkspaceProcessToolNames =
    [
        "workspace_git_diff",
        "workspace_git_status"
    ];
    private static readonly HashSet<string> RequiredBrowserEvidenceToolNames =
    [
        "browser_console_messages",
        "browser_network_requests",
        "browser_snapshot",
        "browser_take_screenshot"
    ];
    private static readonly string[] GovernedInspectionToolNames =
    [
        "workspace_stat_path",
        "workspace_read_file"
    ];
    private static readonly HashSet<string> ConcurrentAutomationSessionBusyMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        "This session already has an active execution run. Wait for it to finish before sending a new prompt.",
        "This session has pending tool approvals. Approve or reject them before sending a new prompt."
    };
    private static readonly string[] ImplementationProofToolNames =
    [
        "workspace_stat_path",
        "workspace_read_file"
    ];
    private static readonly HashSet<string> CurrentAttemptOnlyImplementationProofToolNames =
    [
        "workspace_stat_path",
        "workspace_read_file"
    ];
    private static readonly HashSet<string> CurrentAttemptOnlyBrowserProofToolNames =
    [
        "browser_console_messages",
        "browser_snapshot",
        "browser_take_screenshot"
    ];
    private static readonly HashSet<string> ConcreteProductMutationToolNames =
    [
        "workspace_write_file",
        "workspace_append_file",
        "workspace_move_path",
        "workspace_delete_path",
        "workspace_create_directory"
    ];
    private static readonly HashSet<string> ConcreteProductSourceWriteToolNames =
    [
        "workspace_write_file",
        "workspace_append_file",
        "workspace_move_path"
    ];
    private static readonly string[] ImplicitBrowserProofToolNames =
    [
        "browser_console_messages",
        "browser_snapshot",
        "browser_take_screenshot"
    ];
    private static readonly HashSet<string> ArtifactTitleNoiseTokens = new(StringComparer.Ordinal)
    {
        "artifact",
        "artifacts",
        "brief",
        "briefs",
        "checklist",
        "checklists",
        "doc",
        "docs",
        "document",
        "documents",
        "evidence",
        "file",
        "files",
        "note",
        "notes",
        "output",
        "outputs",
        "packet",
        "packets",
        "record",
        "records",
        "report",
        "reports"
    };
    private static readonly HashSet<string> ArtifactContentNoiseTokens = new(StringComparer.Ordinal)
    {
        "and",
        "are",
        "capture",
        "captured",
        "create",
        "created",
        "form",
        "must",
        "required",
        "should",
        "the",
        "this",
        "with"
    };
    private sealed record PrefetchedProjectStructureGrounding(string PromptSummary, IReadOnlyList<string> SatisfiedToolNames)
    {
        public static PrefetchedProjectStructureGrounding Empty { get; } = new(string.Empty, []);

        public bool HasPromptSummary => !string.IsNullOrWhiteSpace(PromptSummary);
    }
    private sealed record PrefetchedArtifactInspectionGrounding(string PromptSummary, IReadOnlyList<string> SatisfiedToolNames)
    {
        public static PrefetchedArtifactInspectionGrounding Empty { get; } = new(string.Empty, []);

        public bool HasPromptSummary => !string.IsNullOrWhiteSpace(PromptSummary);
    }
    private sealed record ProjectStructureGroundingNodeData(
        string Id,
        string ParentId,
        string ObjectType,
        string ObjectSubtype,
        string Title,
        string Subtitle,
        string Status,
        string Notes,
        string MetadataJson);

    private readonly record struct ProjectStructureExternalTargetHint(
        string AbsolutePath,
        string MappedAlias,
        string SourceNodeId,
        string SourceNodeTitle);

}
