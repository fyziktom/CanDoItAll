using System.Globalization;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessWorkspaceShellProjectionService(
    IProcessProjectionClock clock,
    ProcessDefinitionCatalogProjectionService definitionCatalogProjectionService,
    ProcessDefinitionEditorProjectionService definitionEditorProjectionService,
    ProcessDefinitionRoleEditorProjectionService definitionRoleEditorProjectionService)
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
            CanLaunchRuns: false);

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
            selectedEditor = selectedEditor with
            {
                RoleEditor = selectedRoleEditor
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
            CreateLiveRunSummary(),
            CreateRefreshProjection(request.ForceRefresh, observedAtUtc),
            authorization,
            CreateTabs(definitionCatalog),
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

    private static void ValidateRequest(ProcessWorkspaceShellRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(request.Selection);
        ArgumentNullException.ThrowIfNull(request.DefinitionCatalogQuery);

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

    private static ProcessLiveRunSummaryProjection CreateLiveRunSummary()
        => new(
            ActiveRunCount: 0,
            AttentionRunCount: 0,
            FailedRunCount: 0,
            LastEventAtUtc: null,
            Summary: "Runtime projection snapshots are not available in this workspace shell.");

    private static ProcessWorkspaceProjectionRefreshProjection CreateRefreshProjection(
        bool forceRefresh,
        DateTimeOffset observedAtUtc)
        => new(
            forceRefresh
                ? ProcessWorkspaceProjectionStatus.RefreshRequested
                : ProcessWorkspaceProjectionStatus.ProjectionStoreUnavailable,
            observedAtUtc,
            SourceGlobalSequence: 0,
            BacklogEventCount: 0,
            forceRefresh
                ? "Projection refresh was requested through the application boundary."
                : "Projection store integration is pending; runtime data is intentionally not read by the UI shell.");

    private static IReadOnlyList<ProcessWorkspaceTabProjection> CreateTabs(
        ProcessDefinitionCatalogProjection definitionCatalog)
    {
        var definitionCount = definitionCatalog.PublishedDefinitionCount + definitionCatalog.DraftDefinitionCount;
        var definitionCountText = definitionCount.ToString(CultureInfo.InvariantCulture);

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
                "0",
                IsEnabled: true),
            new(
                ProcessWorkspaceTabKey.History,
                "History",
                "history",
                "Read-only runtime history and legacy archive context.",
                "0",
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
                "Runtime launch commands are not available in this workspace shell."),
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
