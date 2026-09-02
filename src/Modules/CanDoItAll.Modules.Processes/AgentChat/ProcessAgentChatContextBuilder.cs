using System.Globalization;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes.AgentChat;

internal enum ProcessAgentChatWorkspaceView
{
    Definition,
    Roles,
    Steps,
    Runs,
    Graphs,
    Analytics,
    Exchange,
    ManagerChat
}

internal enum ProcessAgentChatRunView
{
    Launch,
    Activity,
    Control,
    Execution,
    Graphs,
    Coordination,
    Evidence
}

internal enum ProcessAgentChatLiveView
{
    Activity,
    Agents,
    Graphs,
    ToolHistory
}

internal sealed record ProcessWorkspaceAgentChatContext(
    string NavigationUri,
    Guid? ProjectId,
    ProcessAgentChatWorkspaceView View,
    ProcessAgentChatRunView RunView,
    Guid? SelectedRunId,
    ProcessWorkspaceShellProjection? Shell,
    ProcessLiveProcessSnapshot? FocusedRun,
    ProcessTimelineEventProjection? FocusedEvent,
    AgentChatContextAccessState AccessState);

internal sealed record LiveProcessesAgentChatContext(
    string NavigationUri,
    Guid? ProjectId,
    ProcessAgentChatLiveView View,
    Guid? SelectedRunId,
    ProcessRuntimeHistoryWindow HistoryWindow,
    ProcessProjectedRunStatus? StatusFilter,
    ProcessWorkspaceShellProjection? Shell,
    ProcessLiveProcessSnapshot? FocusedRun,
    Guid? FilesRunId,
    ProcessRuntimeActiveAgentProjection? FocusedAgent,
    AgentChatContextAccessState AccessState);

internal static class ProcessAgentChatContextBuilder
{
    private const string Module = "processes";
    private const string WorkspaceSurface = "workspace";
    private const string LiveSurface = "live";
    internal const string WorkspaceSourceKind = "processes";
    internal const string LiveSourceKind = "processes-live";
    private const int MaximumBoundedLabelLength = 180;
    private const int MaximumBoundedIdentityLength = 500;
    private const int MaximumBoundedFactLength = 800;

    public static AgentChatContextSurface BuildWorkspaceSurface(
        ProcessWorkspaceAgentChatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateProjectId(context.ProjectId);
        ValidateEnum(context.View, nameof(context.View));
        ValidateEnum(context.RunView, nameof(context.RunView));
        ValidateEnum(context.AccessState, nameof(context.AccessState));

        var provenance = context.Shell?.Provenance;
        var hasSelection = HasPresentComponent(
            provenance,
            ProcessWorkspaceProvenanceComponent.Selection);
        var hasDefinition = HasPresentComponent(
            provenance,
            ProcessWorkspaceProvenanceComponent.DefinitionCatalog);
        var hasRuns = HasPresentComponent(
            provenance,
            ProcessWorkspaceProvenanceComponent.LiveRuns);
        var hasHistory = HasPresentComponent(
            provenance,
            ProcessWorkspaceProvenanceComponent.HistoryPage);
        var selectedDefinition = hasDefinition
            ? context.Shell?.DefinitionCatalog.SelectedItem
            : null;
        var focusedEvent = hasHistory ? context.FocusedEvent : null;
        var focusedRun = hasRuns ? context.FocusedRun : null;
        var effectiveRunId = hasSelection
            ? focusedEvent?.RunId.Value ??
              focusedRun?.RunId.Value ??
              context.SelectedRunId
            : null;
        var selectedRun = focusedRun?.RunId.Value == effectiveRunId
            ? focusedRun
            : hasRuns
                ? ResolveRun(context.Shell, effectiveRunId)
                : null;
        var definitionReference = BuildDefinitionReference(selectedDefinition);
        var runReference = BuildRunReference(selectedRun, effectiveRunId);
        var eventReference = BuildEventReference(focusedEvent);
        var primarySelection = ResolveWorkspacePrimarySelection(
            context,
            focusedRun is not null,
            definitionReference,
            runReference,
            eventReference);
        var selectedEntities = BuildSelectedEntities(
            primarySelection,
            definitionReference,
            runReference);
        var facts = BuildWorkspaceFacts(context, selectedDefinition, selectedRun, provenance);
        return new AgentChatContextSurface(
            BuildSource(WorkspaceSourceKind, WorkspaceSurface, context.ProjectId),
            context.ProjectId.HasValue ? "Project processes" : "Processes",
            new AgentChatSurfacePosition(
                Module,
                WorkspaceSurface,
                ResolveWorkspaceView(context.View, context.RunView),
                ResolveRoute(
                    context.NavigationUri,
                    context.ProjectId.HasValue
                        ? $"/projects/{context.ProjectId.Value:D}/processes"
                        : "/processes"),
                primarySelection,
                selectedEntities,
                facts),
            ResolveWorkspaceScope(context.ProjectId),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            accessState: context.AccessState);
    }

    public static AgentChatContextSurface BuildLiveSurface(
        LiveProcessesAgentChatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateProjectId(context.ProjectId);
        ValidateEnum(context.View, nameof(context.View));
        ValidateEnum(context.HistoryWindow, nameof(context.HistoryWindow));
        ValidateEnum(context.AccessState, nameof(context.AccessState));
        if (context.StatusFilter.HasValue)
        {
            ValidateEnum(context.StatusFilter.Value, nameof(context.StatusFilter));
        }

        var provenance = context.Shell?.Provenance;
        var hasSelection = HasPresentComponent(
            provenance,
            ProcessWorkspaceProvenanceComponent.Selection);
        var hasRuns = HasPresentComponent(
            provenance,
            ProcessWorkspaceProvenanceComponent.LiveRuns);
        var hasActiveAgents = HasPresentComponent(
            provenance,
            ProcessWorkspaceProvenanceComponent.ActiveAgents);
        var focusedRun = hasRuns ? context.FocusedRun : null;
        var focusedAgent = hasActiveAgents ? context.FocusedAgent : null;
        var selectedRunId = hasSelection
            ? context.FilesRunId ??
              focusedAgent?.RunId ??
              focusedRun?.RunId.Value ??
              context.SelectedRunId
            : null;
        var selectedRun = focusedRun?.RunId.Value == selectedRunId
            ? focusedRun
            : hasRuns
                ? ResolveRun(context.Shell, selectedRunId)
                : null;
        var runReference = BuildRunReference(selectedRun, selectedRunId);
        var agentReference = BuildAgentReference(focusedAgent);
        var primarySelection = hasSelection && context.FilesRunId.HasValue
            ? runReference
            : agentReference ?? runReference;
        var selectedEntities = BuildSelectedEntities(
            primarySelection,
            runReference);
        var facts = BuildLiveFacts(context, selectedRun, provenance);

        return new AgentChatContextSurface(
            BuildSource(LiveSourceKind, LiveSurface, context.ProjectId),
            context.ProjectId.HasValue ? "Project live processes" : "Live processes",
            new AgentChatSurfacePosition(
                Module,
                LiveSurface,
                ResolveLiveView(context.View),
                ResolveRoute(
                    context.NavigationUri,
                    context.ProjectId.HasValue
                        ? $"/projects/{context.ProjectId.Value:D}/processes/live"
                        : "/processes/live"),
                primarySelection,
                selectedEntities,
                facts),
            ResolveWorkspaceScope(context.ProjectId),
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            accessState: context.AccessState);
    }

    private static AgentChatContextSource BuildSource(
        string sourceKind,
        string surface,
        Guid? projectId)
        => new(
            new AgentChatContextSourceKind(sourceKind),
            new AgentChatContextSourceId(
                projectId.HasValue
                    ? $"{surface}:project:{projectId.Value:D}"
                    : $"{surface}:global"));

    private static WorkspaceScopeDescriptor? ResolveWorkspaceScope(Guid? projectId)
        => projectId.HasValue
            ? WorkspaceScopeDescriptor.Project(projectId.Value.ToString("D"))
            : null;

    private static ProcessLiveProcessSnapshot? ResolveRun(
        ProcessWorkspaceShellProjection? shell,
        Guid? runId)
    {
        if (!runId.HasValue || runId == Guid.Empty)
        {
            return null;
        }

        return shell?.Runtime.Runs.FirstOrDefault(run => run.RunId.Value == runId.Value);
    }

    private static AgentChatContextEntityReference? BuildDefinitionReference(
        ProcessDefinitionCatalogItemProjection? definition)
        => definition is null
            ? null
            : new AgentChatContextEntityReference(
                "process-definition",
                BoundRequired(definition.Key.Value, MaximumBoundedIdentityLength, "definition"),
                BoundRequired(definition.Name, MaximumBoundedLabelLength, "Process definition"));

    private static AgentChatContextEntityReference? BuildRunReference(
        ProcessLiveProcessSnapshot? run,
        Guid? requestedRunId)
    {
        var runId = run?.RunId.Value ?? requestedRunId;
        if (!runId.HasValue || runId == Guid.Empty)
        {
            return null;
        }

        return new AgentChatContextEntityReference(
            "process-run",
            runId.Value.ToString("D"),
            BuildRunDisplayName(run, runId.Value));
    }

    private static AgentChatContextEntityReference? BuildEventReference(
        ProcessTimelineEventProjection? runtimeEvent)
        => runtimeEvent is null
            ? null
            : new AgentChatContextEntityReference(
                "process-event",
                runtimeEvent.EventId.Value.ToString("D"),
                BoundRequired(runtimeEvent.EventType, MaximumBoundedLabelLength, "Runtime event"));

    private static AgentChatContextEntityReference? BuildAgentReference(
        ProcessRuntimeActiveAgentProjection? agent)
    {
        if (agent is null)
        {
            return null;
        }

        var agentIdentity = agent.AgentId ?? agent.ExecutionRunId ?? agent.StepInstanceId;
        var displayName = string.IsNullOrWhiteSpace(agent.AgentName)
            ? agent.ExecutorDisplayName
            : agent.AgentName;
        return new AgentChatContextEntityReference(
            "process-agent",
            agentIdentity.ToString("D"),
            BoundRequired(displayName, MaximumBoundedLabelLength, "Active agent"));
    }

    private static AgentChatContextEntityReference? ResolveWorkspacePrimarySelection(
        ProcessWorkspaceAgentChatContext context,
        bool hasFocusedRun,
        AgentChatContextEntityReference? definitionReference,
        AgentChatContextEntityReference? runReference,
        AgentChatContextEntityReference? eventReference)
    {
        if (eventReference is not null)
        {
            return eventReference;
        }

        if (hasFocusedRun && runReference is not null)
        {
            return runReference;
        }

        return context.View is ProcessAgentChatWorkspaceView.Runs or
            ProcessAgentChatWorkspaceView.Graphs or
            ProcessAgentChatWorkspaceView.ManagerChat
                ? runReference ?? definitionReference
                : definitionReference ?? runReference;
    }

    private static IReadOnlyList<AgentChatContextEntityReference> BuildSelectedEntities(
        AgentChatContextEntityReference? primary,
        AgentChatContextEntityReference? candidate)
    {
        return candidate is null || IsSameEntity(primary, candidate)
            ? []
            : [candidate];
    }

    private static IReadOnlyList<AgentChatContextEntityReference> BuildSelectedEntities(
        AgentChatContextEntityReference? primary,
        AgentChatContextEntityReference? first,
        AgentChatContextEntityReference? second)
    {
        var includeFirst = first is not null && !IsSameEntity(primary, first);
        var includeSecond = second is not null &&
                            !IsSameEntity(primary, second) &&
                            (!includeFirst || !IsSameEntity(first, second));
        if (includeFirst)
        {
            return includeSecond ? [first!, second!] : [first!];
        }

        return includeSecond ? [second!] : [];
    }

    private static bool IsSameEntity(
        AgentChatContextEntityReference? left,
        AgentChatContextEntityReference right)
        => left is not null &&
           string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) &&
           string.Equals(left.Id, right.Id, StringComparison.Ordinal);

    private static IReadOnlyList<AgentChatContextPositionFact> BuildWorkspaceFacts(
        ProcessWorkspaceAgentChatContext context,
        ProcessDefinitionCatalogItemProjection? selectedDefinition,
        ProcessLiveProcessSnapshot? selectedRun,
        ProcessWorkspaceProvenanceVector? provenance)
    {
        var facts = BuildBaseFacts(context.ProjectId, context.Shell, context.AccessState, provenance);
        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.Selection) &&
            context.View == ProcessAgentChatWorkspaceView.Runs)
        {
            AddFact(facts, "run-view", ResolveRunView(context.RunView));
        }
        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.HistoryPage))
        {
            AddFact(facts, "runtime-history", context.Shell?.Runtime.HistoryWindow.ToString());
        }

        AddDefinitionFacts(facts, selectedDefinition);
        AddRunFacts(facts, selectedRun);

        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.HistoryPage) &&
            context.FocusedEvent is not null)
        {
            AddFact(facts, "focused-dialog", "runtime-event");
            AddFact(facts, "event-type", context.FocusedEvent.EventType);
            AddFact(facts, "event-sensitivity", context.FocusedEvent.Sensitivity.ToString());
        }
        else if (HasPresentComponent(
                     provenance,
                     ProcessWorkspaceProvenanceComponent.LiveRuns) &&
                 context.FocusedRun is not null)
        {
            AddFact(facts, "focused-dialog", "run-detail");
        }

        return facts;
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildLiveFacts(
        LiveProcessesAgentChatContext context,
        ProcessLiveProcessSnapshot? selectedRun,
        ProcessWorkspaceProvenanceVector? provenance)
    {
        var facts = BuildBaseFacts(context.ProjectId, context.Shell, context.AccessState, provenance);
        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.HistoryPage))
        {
            AddFact(facts, "runtime-history", context.HistoryWindow.ToString());
        }

        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.Selection))
        {
            AddFact(facts, "status-filter", context.StatusFilter?.ToString() ?? "All");
        }

        AddRunFacts(facts, selectedRun);

        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.Selection) &&
            context.FilesRunId.HasValue)
        {
            AddFact(facts, "focused-dialog", "run-files");
        }
        else if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.ActiveAgents) &&
                 context.FocusedAgent is not null)
        {
            AddFact(facts, "focused-dialog", "agent-detail");
            AddFact(facts, "agent-status", context.FocusedAgent.Status);
            AddFact(facts, "agent-step", context.FocusedAgent.StepKey);
            AddFact(facts, "agent-role", context.FocusedAgent.RoleKey);
        }
        else if (HasPresentComponent(
                     provenance,
                     ProcessWorkspaceProvenanceComponent.LiveRuns) &&
                 context.FocusedRun is not null)
        {
            AddFact(facts, "focused-dialog", "run-detail");
        }

        return facts;
    }

    private static List<AgentChatContextPositionFact> BuildBaseFacts(
        Guid? projectId,
        ProcessWorkspaceShellProjection? shell,
        AgentChatContextAccessState accessState,
        ProcessWorkspaceProvenanceVector? provenance)
    {
        var facts = new List<AgentChatContextPositionFact>(20);
        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.Selection))
        {
            AddFact(facts, "scope", projectId.HasValue ? "project" : "global");
            AddFact(facts, "project-id", projectId?.ToString("D"));
        }

        AddFact(facts, "context-state", accessState.ToString());
        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.ShellRefresh))
        {
            AddFact(facts, "projection-status", shell?.Refresh.Status.ToString());
        }

        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.DefinitionCatalog))
        {
            AddFact(
                facts,
                "definition-count",
                shell is null
                    ? null
                    : (shell.DefinitionCatalog.PublishedDefinitionCount + shell.DefinitionCatalog.DraftDefinitionCount)
                        .ToString(CultureInfo.InvariantCulture));
        }

        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.LiveRuns))
        {
            AddFact(
                facts,
                "loaded-run-count",
                shell?.Runtime.Runs.Count.ToString(CultureInfo.InvariantCulture));
        }

        if (HasPresentComponent(provenance, ProcessWorkspaceProvenanceComponent.LiveRunSummary))
        {
            AddFact(
                facts,
                "active-run-count",
                shell?.LiveRuns.ActiveRunCount.ToString(CultureInfo.InvariantCulture));
            AddFact(
                facts,
                "attention-run-count",
                shell?.LiveRuns.AttentionRunCount.ToString(CultureInfo.InvariantCulture));
            AddFact(
                facts,
                "failed-run-count",
                shell?.LiveRuns.FailedRunCount.ToString(CultureInfo.InvariantCulture));
        }

        return facts;
    }

    private static void AddDefinitionFacts(
        ICollection<AgentChatContextPositionFact> facts,
        ProcessDefinitionCatalogItemProjection? definition)
    {
        if (definition is null)
        {
            return;
        }

        AddFact(facts, "definition-status", definition.Status.ToString());
        AddFact(facts, "definition-scope", definition.ScopeKind.ToString());
        AddFact(facts, "definition-criticality", definition.Criticality);
        AddFact(facts, "definition-operating-mode", definition.OperatingMode);
        AddFact(
            facts,
            "definition-compatibility-issues",
            definition.CompatibilityIssueCount.ToString(CultureInfo.InvariantCulture));
    }

    private static void AddRunFacts(
        ICollection<AgentChatContextPositionFact> facts,
        ProcessLiveProcessSnapshot? run)
    {
        if (run is null)
        {
            return;
        }

        AddFact(facts, "run-status", run.Status.ToString());
        AddFact(facts, "run-project-id", run.ProjectId?.ToString("D"));
        AddFact(facts, "run-project-name", run.ProjectName);
        AddFact(facts, "run-process-name", run.ProcessName);
        AddFact(facts, "run-is-subprocess", run.IsSubprocess.ToString(CultureInfo.InvariantCulture));
        AddFact(facts, "run-progress", run.ProgressLabel);
        AddFact(facts, "current-step", run.CurrentStep?.StepKey);
        AddFact(facts, "current-step-status", run.CurrentStep?.StepStatus);
        AddFact(facts, "current-step-role", run.CurrentStep?.RoleKey);
    }

    private static void AddFact(
        ICollection<AgentChatContextPositionFact> facts,
        string name,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        facts.Add(new AgentChatContextPositionFact(
            name,
            BoundRequired(value, MaximumBoundedFactLength, "Unavailable")));
    }

    private static string ResolveWorkspaceView(
        ProcessAgentChatWorkspaceView view,
        ProcessAgentChatRunView runView)
        => view switch
        {
            ProcessAgentChatWorkspaceView.Definition => "definition",
            ProcessAgentChatWorkspaceView.Roles => "roles",
            ProcessAgentChatWorkspaceView.Steps => "steps",
            ProcessAgentChatWorkspaceView.Runs => $"runs.{ResolveRunView(runView)}",
            ProcessAgentChatWorkspaceView.Graphs => "graphs",
            ProcessAgentChatWorkspaceView.Analytics => "analytics",
            ProcessAgentChatWorkspaceView.Exchange => "exchange",
            ProcessAgentChatWorkspaceView.ManagerChat => "manager-chat",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The process workspace view is undefined.")
        };

    private static string ResolveRunView(ProcessAgentChatRunView view)
        => view switch
        {
            ProcessAgentChatRunView.Launch => "launch",
            ProcessAgentChatRunView.Activity => "activity",
            ProcessAgentChatRunView.Control => "control",
            ProcessAgentChatRunView.Execution => "execution",
            ProcessAgentChatRunView.Graphs => "graphs",
            ProcessAgentChatRunView.Coordination => "coordination",
            ProcessAgentChatRunView.Evidence => "evidence",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The process run view is undefined.")
        };

    private static string ResolveLiveView(ProcessAgentChatLiveView view)
        => view switch
        {
            ProcessAgentChatLiveView.Activity => "activity",
            ProcessAgentChatLiveView.Agents => "agents",
            ProcessAgentChatLiveView.Graphs => "graphs",
            ProcessAgentChatLiveView.ToolHistory => "tool-history",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The live processes view is undefined.")
        };

    private static string BuildRunDisplayName(
        ProcessLiveProcessSnapshot? run,
        Guid runId)
    {
        var runLabel = $"Run {runId.ToString("N")[..8]}";
        return string.IsNullOrWhiteSpace(run?.ProcessName)
            ? runLabel
            : BoundRequired($"{run.ProcessName} · {runLabel}", MaximumBoundedLabelLength, runLabel);
    }

    private static string ResolveRoute(string navigationUri, string fallbackRoute)
    {
        var route = Uri.TryCreate(navigationUri, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.AbsolutePath
            : navigationUri.Split(['?', '#'], 2, StringSplitOptions.None)[0];
        if (string.IsNullOrWhiteSpace(route) ||
            !route.StartsWith('/') ||
            route.Length > AgentChatPositionLimits.MaximumRouteLength)
        {
            return fallbackRoute;
        }

        return route;
    }

    private static string BoundRequired(
        string? value,
        int maximumLength,
        string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value;
        var builder = new StringBuilder(Math.Min(source.Length, maximumLength));
        var previousWasWhitespace = false;
        foreach (var character in source.Trim())
        {
            var isWhitespace = char.IsControl(character) || char.IsWhiteSpace(character);
            if (isWhitespace)
            {
                if (!previousWasWhitespace && builder.Length > 0 && builder.Length < maximumLength)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            if (builder.Length >= maximumLength)
            {
                break;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        var bounded = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(bounded) ? fallback : bounded;
    }

    private static void ValidateProjectId(Guid? projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project-scoped Processes context requires a non-empty project id.", nameof(projectId));
        }
    }

    private static bool HasPresentComponent(
        ProcessWorkspaceProvenanceVector? vector,
        ProcessWorkspaceProvenanceComponent component)
        => vector?.GetComponent(component).State == ProcessProjectionComponentState.Present;

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The Processes context value is undefined.");
        }
    }
}
