using System.Globalization;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessWorkspaceShellProjectionService(
    IProcessProjectionClock clock,
    ProcessDefinitionCatalogProjectionService definitionCatalogProjectionService,
    ProcessDefinitionEditorProjectionService definitionEditorProjectionService,
    ProcessDefinitionRoleEditorProjectionService definitionRoleEditorProjectionService,
    ProcessDefinitionCanvasEditorProjectionService definitionCanvasEditorProjectionService,
    ProcessDefinitionStepEditorProjectionService definitionStepEditorProjectionService,
    ProcessTemplateCatalogProjectionService templateCatalogProjectionService,
    ProcessRuntimeProjectionQueryService? runtimeProjectionQueryService = null,
    ProcessRuntimeProjectionCatchupService? projectionCatchupService = null)
{
    private const string WorkspaceContextPrefix = "processes:workspace";
    private const string ProjectContextPrefix = "processes:project";
    private const string RunContextSegment = "run";
    private const string LaunchPlanContextSegment = "launch-plan";

    public async Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequest(request);

        var observedAtUtc = clock.GetUtcNow();
        var authorization = new ProcessWorkspaceAuthorizationProjection(
            CanReadDefinitions: true,
            CanRefreshProjections: true,
            CanOpenAgentContext: true,
            CanEditDefinitions: false,
            CanLaunchRuns: true);
        var liveProcesses = await LoadLiveProcessesAsync(request, observedAtUtc, cancellationToken).ConfigureAwait(false);

        var definitionCatalog = await definitionCatalogProjectionService
            .GetCatalogAsync(request.Scope, request.DefinitionCatalogQuery, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var selectedEditor = definitionCatalog.SelectedItem is null
            ? null
            : await definitionEditorProjectionService
                .GetEditorAsync(request.Scope, definitionCatalog.SelectedItem.Key, cancellationToken)
                .ConfigureAwait(false);
        if (selectedEditor is not null)
        {
            var selectedRoleEditor = await definitionRoleEditorProjectionService
                .GetEditorAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken)
                .ConfigureAwait(false);
            var selectedStepEditor = await definitionStepEditorProjectionService
                .GetEditorAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken)
                .ConfigureAwait(false);
            selectedEditor = selectedEditor with
            {
                RoleEditor = selectedRoleEditor,
                Canvas = await definitionCanvasEditorProjectionService
                    .GetCanvasAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken)
                    .ConfigureAwait(false),
                StepEditor = selectedStepEditor,
                TemplateCatalog = await templateCatalogProjectionService
                    .GetCatalogAsync(request.Scope, selectedEditor.DefinitionKey, request.TemplateCatalogQuery, selectedStepEditor, cancellationToken)
                    .ConfigureAwait(false)
            };
        }

        definitionCatalog = definitionCatalog with
        {
            SelectedEditor = selectedEditor
        };

        return new ProcessWorkspaceShellProjection(
            request.Scope,
            request.Selection,
            ResolveTitle(request.Scope),
            ResolveSubtitle(request.Scope),
            definitionCatalog,
            CreateLiveRunSummary(liveProcesses),
            CreateRefreshProjection(request.ForceRefresh, observedAtUtc, liveProcesses?.Freshness),
            authorization,
            CreateTabs(definitionCatalog, liveProcesses),
            CreateCommands(authorization),
            CreateAgentEntry(request.Scope, request.Selection, authorization));
    }

    public Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
        ProcessDefinitionFeedDefaultsCommand command,
        CancellationToken cancellationToken = default)
        => definitionCatalogProjectionService.FeedDefaultDefinitionsAsync(command, cancellationToken);

    public Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(
        ProcessDefinitionEditorCommand command,
        CancellationToken cancellationToken = default)
        => definitionEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(
        ProcessDefinitionRoleEditorCommand command,
        CancellationToken cancellationToken = default)
        => definitionRoleEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default)
        => definitionCanvasEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(
        ProcessDefinitionStepEditorCommand command,
        CancellationToken cancellationToken = default)
        => definitionStepEditorProjectionService.ExecuteCommandAsync(command, cancellationToken);

    public async Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(
        ProcessTemplateImportCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);

        var stepEditor = await definitionStepEditorProjectionService
            .GetEditorAsync(command.Scope, command.TargetDefinitionKey, cancellationToken)
            .ConfigureAwait(false);
        return await templateCatalogProjectionService
            .ExecuteCommandAsync(command, stepEditor, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidateRequest(ProcessWorkspaceShellRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(request.Selection);
        ArgumentNullException.ThrowIfNull(request.DefinitionCatalogQuery);
        ArgumentNullException.ThrowIfNull(request.TemplateCatalogQuery);

        if (request.Scope.Kind == ProcessWorkspaceScopeKind.Project &&
            request.Scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped process workspace requires a project id.", nameof(request));
        }

        if (request.Scope.Kind == ProcessWorkspaceScopeKind.Global &&
            request.Scope.ProjectId is not null)
        {
            throw new ArgumentException("Global process workspace cannot carry a project id.", nameof(request));
        }
    }

    private static string ResolveTitle(ProcessWorkspaceShellScope scope)
        => scope.Kind == ProcessWorkspaceScopeKind.Project
            ? "Project processes"
            : "Processes";

    private static string ResolveSubtitle(ProcessWorkspaceShellScope scope)
        => scope.Kind == ProcessWorkspaceScopeKind.Project
            ? $"Projection-first project workspace for {scope.ProjectId:D}."
            : "Projection-first workspace for definitions, launches, live runs, and history.";

    private async Task<ProcessLiveProcessesResult?> LoadLiveProcessesAsync(
        ProcessWorkspaceShellRequest request,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        if (runtimeProjectionQueryService is null)
        {
            return null;
        }

        if (request.ForceRefresh && projectionCatchupService is not null)
        {
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
        }

        return await runtimeProjectionQueryService
            .GetLiveProcessesAsync(
                new ProcessLiveProcessesQuery(
                    observedAtUtc,
                    TimeSpan.FromDays(30),
                    Take: 100),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProcessLiveRunSummaryProjection CreateLiveRunSummary(ProcessLiveProcessesResult? liveProcesses)
    {
        if (liveProcesses is null)
        {
            return new ProcessLiveRunSummaryProjection(
                ActiveRunCount: 0,
                AttentionRunCount: 0,
                FailedRunCount: 0,
                LastEventAtUtc: null,
                Summary: "Runtime projection store is not registered for this workspace shell.");
        }

        var active = liveProcesses.Runs.Count(run => run.IsActive);
        var attention = liveProcesses.Runs.Count(run => run.Status == ProcessProjectedRunStatus.NeedsAttention);
        var failed = liveProcesses.Runs.Count(run => run.Status == ProcessProjectedRunStatus.Failed);
        var lastEventAtUtc = liveProcesses.Runs.Count == 0
            ? (DateTimeOffset?)null
            : liveProcesses.Runs.Max(run => run.LastEventAtUtc);

        return new ProcessLiveRunSummaryProjection(
            active,
            attention,
            failed,
            lastEventAtUtc,
            liveProcesses.Runs.Count == 0
                ? "No runtime runs are present in the current projection window."
                : $"{active.ToString(CultureInfo.InvariantCulture)} active run(s), {attention.ToString(CultureInfo.InvariantCulture)} needing attention, {failed.ToString(CultureInfo.InvariantCulture)} failed.");
    }

    private static ProcessWorkspaceProjectionRefreshProjection CreateRefreshProjection(
        bool forceRefresh,
        DateTimeOffset observedAtUtc,
        ProcessProjectionFreshness? freshness)
        => new(
            freshness is null && forceRefresh
                ? ProcessWorkspaceProjectionStatus.RefreshRequested
                : freshness is null
                    ? ProcessWorkspaceProjectionStatus.Ready
                    : ProcessWorkspaceProjectionStatus.Ready,
            observedAtUtc,
            freshness?.SourceGlobalSequence ?? 0,
            freshness?.Lag.BacklogEventCount ?? 0,
            freshness is null
                ? "Runtime projection has no events in the current window."
                : $"Runtime projection processed sequence {freshness.SourceGlobalSequence.ToString(CultureInfo.InvariantCulture)} with {freshness.Lag.BacklogEventCount.ToString(CultureInfo.InvariantCulture)} backlog event(s).");

    private static IReadOnlyList<ProcessWorkspaceTabProjection> CreateTabs(
        ProcessDefinitionCatalogProjection definitionCatalog,
        ProcessLiveProcessesResult? liveProcesses)
    {
        var definitionCount = definitionCatalog.PublishedDefinitionCount + definitionCatalog.DraftDefinitionCount;
        var definitionCountText = definitionCount.ToString(CultureInfo.InvariantCulture);
        var activeRunCountText = (liveProcesses?.Runs.Count(run => run.IsActive) ?? 0).ToString(CultureInfo.InvariantCulture);
        var historyCountText = (liveProcesses?.Runs.Count ?? 0).ToString(CultureInfo.InvariantCulture);

        return
        [
            new(
                ProcessWorkspaceTabKey.Definitions,
                "Definitions",
                "account_tree",
                "Definition catalog, template compatibility, and selected definition context.",
                definitionCountText,
                IsEnabled: true),
            new(
                ProcessWorkspaceTabKey.LaunchPlans,
                "Launch plans",
                "rocket_launch",
                "Launch planning entry point reserved for application commands.",
                "0",
                IsEnabled: true),
            new(
                ProcessWorkspaceTabKey.LiveRuns,
                "Live runs",
                "monitor_heart",
                "Live runtime projection surface.",
                activeRunCountText,
                IsEnabled: true),
            new(
                ProcessWorkspaceTabKey.History,
                "History",
                "history",
                "Read-only runtime history and legacy archive context.",
                historyCountText,
                IsEnabled: true)
        ];
    }

    private static IReadOnlyList<ProcessWorkspaceCommandProjection> CreateCommands(
        ProcessWorkspaceAuthorizationProjection authorization)
        =>
        [
            new(
                ProcessWorkspaceCommandKind.RefreshProjections,
                "Refresh",
                "refresh",
                authorization.CanRefreshProjections,
                authorization.CanRefreshProjections ? null : "Projection refresh is not authorized."),
            new(
                ProcessWorkspaceCommandKind.OpenAgentContext,
                "Agent context",
                "smart_toy",
                authorization.CanOpenAgentContext,
                authorization.CanOpenAgentContext ? null : "Agent context is not authorized."),
            new(
                ProcessWorkspaceCommandKind.CreateDefinition,
                "New definition",
                "add",
                authorization.CanEditDefinitions,
                "Definition editing is not available in this workspace shell."),
            new(
                ProcessWorkspaceCommandKind.FeedDefaults,
                "Feed defaults",
                "download",
                authorization.CanRefreshProjections,
                authorization.CanRefreshProjections ? null : "Projection refresh is not authorized."),
            new(
                ProcessWorkspaceCommandKind.LaunchRun,
                "Launch",
                "rocket_launch",
                authorization.CanLaunchRuns,
                authorization.CanLaunchRuns ? null : "Runtime launch commands are not available in this workspace shell."),
            new(
                ProcessWorkspaceCommandKind.OpenLiveDashboard,
                "Live dashboard",
                "open_in_new",
                IsEnabled: true,
                DisabledReason: null)
        ];

    private static ProcessWorkspaceAgentEntryProjection CreateAgentEntry(
        ProcessWorkspaceShellScope scope,
        ProcessWorkspaceSelectionProjection selection,
        ProcessWorkspaceAuthorizationProjection authorization)
    {
        if (!authorization.CanOpenAgentContext)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.WorkspaceContext,
                IsAvailable: false,
                "Agent context",
                WorkspaceContextPrefix,
                "Agent context is not authorized.");
        }

        if (selection.RunId.HasValue)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.RunContext,
                IsAvailable: true,
                "Open run agent context",
                BuildContextKey(scope, RunContextSegment, selection.RunId.Value),
                DisabledReason: null);
        }

        if (selection.LaunchPlanId.HasValue)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.LaunchPlanContext,
                IsAvailable: true,
                "Open launch-plan agent context",
                BuildContextKey(scope, LaunchPlanContextSegment, selection.LaunchPlanId.Value),
                DisabledReason: null);
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Project)
        {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.ProjectContext,
                IsAvailable: true,
                "Open project process agent context",
                $"{ProjectContextPrefix}:{scope.ProjectId:N}",
                DisabledReason: null);
        }

        return new ProcessWorkspaceAgentEntryProjection(
            ProcessWorkspaceAgentEntryKind.WorkspaceContext,
            IsAvailable: true,
            "Open process agent context",
            WorkspaceContextPrefix,
            DisabledReason: null);
    }

    private static string BuildContextKey(
        ProcessWorkspaceShellScope scope,
        string segment,
        Guid id)
    {
        var scopeKey = scope.Kind == ProcessWorkspaceScopeKind.Project
            ? $"{ProjectContextPrefix}:{scope.ProjectId:N}"
            : WorkspaceContextPrefix;

        return $"{scopeKey}:{segment}:{id:N}";
    }
}
