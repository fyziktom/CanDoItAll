using System.Globalization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.UiSandbox;

public static class ProcessScenarioData {
    public static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 30, 0, TimeSpan.Zero);
    public static readonly Guid ProjectSubprocessRunId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid ProjectSubprocessProjectId = Guid.Parse("12121212-3434-5656-7878-909090909090");
    private static string ResolveProcessName(Guid runId) => runId == ProjectSubprocessRunId
        ? "Long-running customer onboarding process with multiple external approvals" : "Blazor app delivery";

    public static ProcessWorkspaceShellProjection CreateShell(
        ProcessWorkspaceShellRequest request,
        ProcessDefinitionCatalogCommandReceipt? lastReceipt) {
        var catalog = CreateDefinitionCatalog(
            request.DefinitionCatalogQuery,
            request.TemplateCatalogQuery,
            lastReceipt,
            request.DefinitionLoadOptions ?? ProcessDefinitionWorkspaceLoadOptions.Full);
        var authorization = new ProcessWorkspaceAuthorizationProjection(
            CanReadDefinitions: true,
            CanRefreshProjections: true,
            CanOpenAgentContext: true,
            CanEditDefinitions: true,
            CanLaunchRuns: true);
        var runtime = CreateRuntimeWorkspace(request);
        var provenance = CreateTestProvenance(request, runtime);
        runtime = runtime with {
            Provenance = provenance
        };

        return new ProcessWorkspaceShellProjection(
            request.Scope,
            request.Selection,
            request.Scope.Kind == ProcessWorkspaceScopeKind.Project ? "Project processes" : "Processes",
            "Projection-first process workspace.",
            catalog,
            new ProcessLiveRunSummaryProjection(0, 0, 0, null, "Runtime projection snapshots are not available in this workspace shell."),
            new ProcessWorkspaceProjectionRefreshProjection(
                request.ForceRefresh
                    ? ProcessWorkspaceProjectionStatus.RefreshRequested
                    : ProcessWorkspaceProjectionStatus.ProjectionStoreUnavailable,
                Now,
                SourceGlobalSequence: 0,
                BacklogEventCount: 0,
                request.ForceRefresh
                    ? "Projection refresh was requested through the application boundary."
                    : "Projection store integration is pending; runtime data is intentionally not read by the UI shell."),
            authorization,
            CreateTabs(),
            CreateCommands(),
            CreateAgentEntry(request)) {
            Runtime = runtime,
            Provenance = provenance,
            ProjectBinding = request.Scope.ProjectId is { } projectId
                ? new(Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), projectId, Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222")) : null
        };
    }

    public static ProcessWorkspaceProvenanceVector CreateTestProvenance(
        ProcessWorkspaceShellRequest request,
        ProcessRuntimeWorkspaceProjection runtime) {
        var options = request.RuntimeQuery?.LoadOptions ?? ProcessRuntimeWorkspaceLoadOptions.Full;

        ProcessProjectionComponentProvenance Present(
            ProcessWorkspaceProvenanceComponent component,
            object content)
            => ProcessProjectionComponentProvenance.Present(
                ProcessProjectionComponentSource.ShellProjection,
                ProcessProjectionContentFingerprintFactory.Create(component, content),
                runtime.Freshness);

        ProcessProjectionComponentProvenance Requested(
            bool include,
            ProcessWorkspaceProvenanceComponent component,
            object content)
            => include
                ? Present(component, content)
                : ProcessProjectionComponentProvenance.NotRequested(
                    ProcessProjectionComponentAbsenceReason.LoadOptionDisabled);

        var selectedRunRequested = options.IncludeSelectedRun;
        var selectedRunPresent = selectedRunRequested && runtime.SelectedRun is not null;
        var selectedRunProvenance = !selectedRunRequested
            ? ProcessProjectionComponentProvenance.NotRequested(
                ProcessProjectionComponentAbsenceReason.LoadOptionDisabled)
            : selectedRunPresent
                ? Present(
                    ProcessWorkspaceProvenanceComponent.SelectedRunDetail,
                    runtime.SelectedRun!)
                : ProcessProjectionComponentProvenance.Absent(
                    ProcessProjectionComponentSource.ShellProjection,
                    ProcessProjectionComponentAbsenceReason.NoSelection);
        var selectedRecordProvenance = !selectedRunRequested
            ? ProcessProjectionComponentProvenance.NotRequested(
                ProcessProjectionComponentAbsenceReason.LoadOptionDisabled)
            : selectedRunPresent
                ? Present(
                    ProcessWorkspaceProvenanceComponent.SelectedRunRecord,
                    new {
                        runtime.SelectedRunId,
                        runtime.SelectedRun!.Status
                    })
                : ProcessProjectionComponentProvenance.Absent(
                    ProcessProjectionComponentSource.ShellProjection,
                    ProcessProjectionComponentAbsenceReason.NoSelection);

        return new ProcessWorkspaceProvenanceVector(
            Present(
                ProcessWorkspaceProvenanceComponent.Selection,
                new {
                    request.Scope,
                    request.Selection,
                    runtime.SelectedRunId
                }),
            Present(
                ProcessWorkspaceProvenanceComponent.ShellRefresh,
                new {
                    request.ForceRefresh,
                    runtime.Freshness
                }),
            Present(
                ProcessWorkspaceProvenanceComponent.DefinitionCatalog,
                request.DefinitionCatalogQuery),
            Present(
                ProcessWorkspaceProvenanceComponent.LiveRunSummary,
                new {
                    runtime.Stats.ActiveRunCount,
                    runtime.Stats.AttentionRunCount,
                    runtime.Stats.FailedRunCount
                }),
            Present(
                ProcessWorkspaceProvenanceComponent.LiveRuns,
                runtime.Runs.Select(run => new {
                    run.RunId,
                    run.Status,
                    run.LastEventAtUtc
                }).ToArray()),
            selectedRunProvenance,
            selectedRecordProvenance,
            Requested(
                options.IncludeHistory,
                ProcessWorkspaceProvenanceComponent.HistoryPage,
                runtime.Events),
            Requested(
                options.IncludeMetricHistory,
                ProcessWorkspaceProvenanceComponent.MetricHistory,
                runtime.MetricPoints),
            Requested(
                options.IncludeActiveAgents,
                ProcessWorkspaceProvenanceComponent.ActiveAgents,
                runtime.ActiveAgents),
            Requested(
                options.IncludeUsageTelemetry,
                ProcessWorkspaceProvenanceComponent.UsageTelemetry,
                runtime.Stats),
            Present(
                ProcessWorkspaceProvenanceComponent.DerivedProjection,
                new {
                    runtime.Stats,
                    runtime.AttentionSummary
                }));
    }

    public static ProcessRuntimeWorkspaceProjection CreateRuntimeWorkspace(ProcessWorkspaceShellRequest request) {
        var loadOptions = request.RuntimeQuery?.LoadOptions ?? ProcessRuntimeWorkspaceLoadOptions.Full;
        var runId = new ProcessRunId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        var secondRunId = new ProcessRunId(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        var shouldResolveSelectedRun = loadOptions.IncludeSelectedRun ||
            loadOptions.IncludeHistory ||
            loadOptions.IncludeMetricHistory ||
            loadOptions.IncludeActiveAgents;
        ProcessRunId? selectedRunId = shouldResolveSelectedRun
            ? request.RuntimeQuery?.SelectedRunId == secondRunId.Value
                ? secondRunId
                : request.RuntimeQuery?.SelectedRunId == runId.Value
                    ? runId
                    : request.RuntimeQuery?.AutoSelectRun != false
                        ? runId
                        : null
            : null;
        var freshness = new ProcessProjectionFreshness(
            Now,
            SourceGlobalSequence: 12,
            new ProcessProjectionLag(12, 12, BacklogEventCount: 0));
        var eventRunId = selectedRunId ?? runId;
        var events = loadOptions.IncludeHistory ? CreateRuntimeEvents(eventRunId) : [];
        var metricEvents = loadOptions.IncludeMetricHistory ? CreateRuntimeEvents(eventRunId) : [];
        var selectedRun = selectedRunId is null || !loadOptions.IncludeSelectedRun
            ? null
            : new ProcessRunDetailProjection(
                selectedRunId.Value,
                selectedRunId.Value,
                selectedRunId == runId ? ProcessProjectedRunStatus.NeedsAttention : ProcessProjectedRunStatus.Active,
                Now.AddMinutes(-35),
                Now.AddMinutes(-2),
                freshness,
                CreateRuntimeEvents(selectedRunId.Value).Select(ToLiveRunEvent).ToArray());
        var runs = new[] {
            CreateLiveRun(runId, ProcessProjectedRunStatus.NeedsAttention, freshness, CreateRuntimeEvents(runId)),
            CreateLiveRun(secondRunId, ProcessProjectedRunStatus.Active, freshness, CreateRuntimeEvents(secondRunId, startSequence: 20))
        };
        var historyWindow = request.RuntimeQuery?.HistoryWindow ?? ProcessRuntimeHistoryWindow.OneDay;
        var page = request.RuntimeQuery?.EventPage ?? 0;
        var pageSize = request.RuntimeQuery?.EventPageSize ?? 25;
        var useManagerChatUsageTelemetry = loadOptions.IncludeUsageTelemetry &&
            loadOptions.IncludeSelectedRun &&
            !loadOptions.IncludeMetricHistory;

        return new ProcessRuntimeWorkspaceProjection(
            historyWindow,
            page,
            pageSize,
            HasMoreEvents: false,
            selectedRunId?.Value,
            selectedRun,
            runs,
            events,
            runs.SelectMany(run => run.Incidents).ToArray(),
            loadOptions.IncludeMetricHistory
                ?
                [
                    new ProcessManagerMessageProjection(
                        "manager-message-test",
                        eventRunId,
                        eventRunId,
                        "Manager Incident Raised",
                        "Manager incident raised; operator review is required.",
                        Now.AddMinutes(-2),
                        ProcessProjectedSensitivity.Normal,
                        RestrictedDiagnosticReference: null)
                ]
                : [],
            loadOptions.IncludeActiveAgents
                ?
                [
                    new ProcessRuntimeActiveAgentProjection(
                        eventRunId.Value,
                        Guid.Parse("99999999-9999-9999-9999-999999999999"),
                        $"Run {eventRunId.Value.ToString("N")[..8]}",
                        "implementation",
                        "lead-engineer",
                        "agent",
                        "agent-dotnet-developer",
                        ".NET Developer",
                        "Running",
                        IsWorking: true,
                        IsLeaseExpired: false,
                        Now.AddMinutes(-1),
                        Now.AddMinutes(-30),
                        Now.AddMinutes(20),
                        ".NET Developer is Running on implementation as lead-engineer.")
                ]
                : [],
            new ProcessRuntimeStatsProjection(
                ObservedRunCount: runs.Length,
                ActiveRunCount: 2,
                AttentionRunCount: 1,
                FailedRunCount: 0,
                EventCount: loadOptions.IncludeMetricHistory ? metricEvents.Count : events.Count,
                ManagerEventCount: loadOptions.IncludeMetricHistory ? 1 : 0,
                ToolCallCount: loadOptions.IncludeMetricHistory ? metricEvents.Count : events.Count,
                DurationMs: 33 * 60 * 1000,
                InputTokens: useManagerChatUsageTelemetry ? 1_234 : 0,
                CachedInputTokens: useManagerChatUsageTelemetry ? 234 : 0,
                OutputTokens: useManagerChatUsageTelemetry ? 432 : 0,
                TotalTokens: useManagerChatUsageTelemetry ? 1_666 : 0,
                EstimatedCost: useManagerChatUsageTelemetry ? 0.130000m : 0m,
                ActualCost: useManagerChatUsageTelemetry ? 0.123456m : 0m),
            metricEvents.Select(runtimeEvent => new ProcessRuntimeMetricPointProjection(
                runtimeEvent.OccurredAtUtc,
                EventCount: 1,
                ManagerEventCount: runtimeEvent.EventType.StartsWith("Manager", StringComparison.Ordinal) ? 1 : 0,
                ToolCallCount: 1,
                DurationMs: 60_000,
                InputTokens: 0,
                CachedInputTokens: 0,
                OutputTokens: 0,
                TotalTokens: 0,
                EstimatedCost: 0m,
                ActualCost: 0m)).ToArray(),
            loadOptions.IncludeMetricHistory
                ?
                [
                    new ProcessRuntimeToolUsageProjection("Step Running", 1, Now.AddMinutes(-30), "1 event, latest test."),
                    new ProcessRuntimeToolUsageProjection("Manager Incident Raised", 1, Now.AddMinutes(-2), "1 event, latest test.")
                ]
                : [],
            freshness,
            $"2 run(s), 2 active, 1 needing attention, {events.Count.ToString(CultureInfo.InvariantCulture)} event(s) on this page.",
            "Cause: Manager incident raised. Next action: open the selected run and review manager messages.") {
            ReusableRuns = runs
        };
    }

    public static ProcessLiveProcessSnapshot CreateLiveRun(
        ProcessRunId runId,
        ProcessProjectedRunStatus status,
        ProcessProjectionFreshness freshness,
        IReadOnlyList<ProcessTimelineEventProjection> events) {
        IReadOnlyList<ProcessIncidentProjection> incidents = status == ProcessProjectedRunStatus.NeedsAttention
            ?
            [
                new ProcessIncidentProjection(
                    "incident-test",
                    runId,
                    runId,
                    "ManagerIncident",
                    "NeedsAttention",
                    "Raised",
                    "Manager incident raised",
                    "runtime-event:test",
                    Now.AddMinutes(-2))
            ]
            : Array.Empty<ProcessIncidentProjection>();

        var snapshot = new ProcessLiveProcessSnapshot(
            runId,
            runId,
            status,
            IsActive: status is ProcessProjectedRunStatus.Active or ProcessProjectedRunStatus.NeedsAttention,
            Now.AddMinutes(-35),
            Now.AddMinutes(-2),
            freshness,
            events.Select(ToLiveRunEvent).ToArray(),
            incidents);
        snapshot = snapshot with { ProcessName = ResolveProcessName(runId.Value) };
        if (runId.Value == ProjectSubprocessRunId) {
            snapshot = CreateProjectSubprocessLiveRun(snapshot);
        }

        return status == ProcessProjectedRunStatus.NeedsAttention
            ? snapshot with {
                OperatorActions =
                [
                    new ProcessRuntimeOperatorActionProjection(
                        runId.Value,
                        Guid.Parse("99999999-9999-9999-9999-999999999998"),
                        "implement-code-change",
                        "Blocked",
                        "dotnet-developer",
                        ".NET Developer",
                        ".NET Developer",
                        ProcessRuntimeOperatorActionKind.RequestRework,
                        "Approve rework",
                        "Root action: approve manager-guided rework for implement-code-change after Blocked. Last strategy outcome: NeedsManager. Assigned role: .NET Developer. Executor: .NET Developer.",
                        IsEnabled: true,
                        DisabledReason: null) {
                        ProblemSummary = "implement-code-change is Blocked on attempt 1. The last strategy outcome was NeedsManager and the runtime applied Blocked. This is the actionable upstream step for role .NET Developer, currently assigned to .NET Developer.",
                        RequiredOperatorDecision = "Approve rework to return implement-code-change from Blocked to Ready and let the process manager dispatch .NET Developer again. Add an operator note if the agent needs extra context.",
                        RecommendedInstruction = "Manager-approved rework for step 'implement-code-change'. Resolve the previous NeedsManager outcome, preserve accepted upstream artifacts, produce the required evidence for role '.NET Developer', and continue the process. Previous executor: .NET Developer. Step status before rework: Blocked.",
                        PrimaryRootCause = true
                    }
                ]
            }
            : snapshot;
    }

    public static ProcessLiveProcessSnapshot CreateProjectSubprocessLiveRun(ProcessLiveProcessSnapshot snapshot) {
        var currentStepId = Guid.Parse("88888888-8888-8888-8888-aaaaaaaaaaaa");
        var childRunId = Guid.Parse("88888888-8888-8888-8888-bbbbbbbbbbbb");

        return snapshot with {
            ProjectId = ProjectSubprocessProjectId,
            ProjectName = "Apollo Delivery",
            IsSubprocess = true,
            CurrentStep = new ProcessRuntimeCurrentStepProjection(
                snapshot.RunId.Value,
                currentStepId,
                "await-child-artifacts",
                "Waiting",
                "process-manager",
                "Process manager",
                "Process manager",
                AttemptNumber: 1,
                IsWorking: false,
                IsLeaseExpired: false,
                Now.AddMinutes(-1),
                ClaimedAtUtc: null,
                LeaseExpiresAtUtc: null,
                "Process manager is waiting for child process evidence."),
            WaitingOnChildRuns =
            [
                new ProcessRuntimeChildRunWaitProjection(
                    snapshot.RunId.Value,
                    currentStepId,
                    "await-child-artifacts",
                    "Waiting",
                    childRunId,
                    "Active",
                    "collect-child-evidence",
                    "Running",
                    "Process manager is waiting for child process evidence.")
            ]
        };
    }

    public static IReadOnlyList<ProcessTimelineEventProjection> CreateRuntimeEvents(ProcessRunId runId, int startSequence = 10)
        =>
        [
            new ProcessTimelineEventProjection(
                RuntimeEventId.New(),
                startSequence,
                runId,
                runId,
                "ProcessRunActivated",
                Now.AddMinutes(-35),
                ProcessProjectedSensitivity.Normal,
                "ProcessRunActivated",
                RestrictedDiagnosticReference: null),
            new ProcessTimelineEventProjection(
                RuntimeEventId.New(),
                startSequence + 1,
                runId,
                runId,
                "StepRunning",
                Now.AddMinutes(-30),
                ProcessProjectedSensitivity.Normal,
                "StepRunning",
                RestrictedDiagnosticReference: null),
            new ProcessTimelineEventProjection(
                RuntimeEventId.New(),
                startSequence + 2,
                runId,
                runId,
                "ManagerIncidentRaised",
                Now.AddMinutes(-2),
                ProcessProjectedSensitivity.Normal,
                "ManagerIncidentRaised",
                RestrictedDiagnosticReference: null)
        ];

    public static ProcessLiveRunEventProjection ToLiveRunEvent(ProcessTimelineEventProjection runtimeEvent)
        => new(
            runtimeEvent.EventId,
            runtimeEvent.GlobalSequence,
            runtimeEvent.RootRunId,
            runtimeEvent.RunId,
            runtimeEvent.EventType,
            runtimeEvent.OccurredAtUtc,
            runtimeEvent.Sensitivity,
            runtimeEvent.Summary,
            runtimeEvent.RestrictedDiagnosticReference);

    public static ProcessDefinitionCatalogProjection CreateDefinitionCatalog(
        ProcessDefinitionCatalogQueryProjection query,
        ProcessTemplateCatalogQueryProjection templateQuery,
        ProcessDefinitionCatalogCommandReceipt? lastReceipt,
        ProcessDefinitionWorkspaceLoadOptions loadOptions) {
        var items = new[] {
            new ProcessDefinitionCatalogItemProjection(
                new ProcessDefinitionCatalogItemKey("blazor-app-delivery"),
                ProcessDefinitionCatalogScopeKind.Global,
                "Blazor app delivery",
                "Build and prove a Blazor application.",
                ProcessDefinitionCatalogItemStatus.TemplateDefault,
                "High",
                "GovernedLive",
                Now,
                CompatibilityIssueCount: 0),
            new ProcessDefinitionCatalogItemProjection(
                new ProcessDefinitionCatalogItemKey("architecture-decision-governance"),
                ProcessDefinitionCatalogScopeKind.Global,
                "Architecture decision governance",
                "Review and approve architecture decisions.",
                ProcessDefinitionCatalogItemStatus.TemplateDefault,
                "Medium",
                "Assisted",
                Now,
                CompatibilityIssueCount: 0)
        };
        ProcessDefinitionCatalogItemProjection[] scopeFiltered = query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Project
            ? []
            : items;
        var filtered = string.IsNullOrWhiteSpace(query.SearchText)
            ? scopeFiltered
            : scopeFiltered
                .Where(item => item.Name.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                               item.Key.Value.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        var selected = query.SelectedDefinitionKey is { } selectedKey
            ? filtered.FirstOrDefault(item => item.Key == selectedKey)
            : filtered.FirstOrDefault();

        return new ProcessDefinitionCatalogProjection(
            PublishedDefinitionCount: items.Length,
            DraftDefinitionCount: 0,
            TemplateCompatibilityIssueCount: 0,
            string.IsNullOrWhiteSpace(query.SearchText)
                ? "2 default definition(s) loaded from template pack test."
                : $"{filtered.Length} definition(s) match '{query.SearchText}'.",
            query.SearchText ?? string.Empty,
            selected?.Key,
            [
                new(ProcessDefinitionCatalogScopeKind.All, "All definitions", "All visible definitions.", items.Length, query.ScopeFilter == ProcessDefinitionCatalogScopeKind.All),
                new(ProcessDefinitionCatalogScopeKind.Global, "Global defaults", "Template-backed defaults.", items.Length, query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Global),
                new(ProcessDefinitionCatalogScopeKind.Project, "Project", "Project-specific definitions.", Count: 0, IsSelected: query.ScopeFilter == ProcessDefinitionCatalogScopeKind.Project)
            ],
            filtered,
            selected,
            selected is null || !loadOptions.IncludeSelectedEditor ? null : CreateEditor(selected.Key, templateQuery, loadOptions),
            lastReceipt);
    }

    public static ProcessDefinitionEditorProjection CreateEditor(
        ProcessDefinitionCatalogItemKey key,
        ProcessTemplateCatalogQueryProjection? templateQuery = null,
        ProcessDefinitionWorkspaceLoadOptions? loadOptions = null) {
        loadOptions ??= ProcessDefinitionWorkspaceLoadOptions.Full;
        var draft = new ProcessDefinitionEditorDraftProjection(
            key,
            new ProcessDefinitionEditorIdentityProjection(
                key.Value == "blazor-app-delivery" ? "Blazor app delivery" : "Architecture decision governance",
                "Global",
                "Delivery requester",
                "Delivery owner",
                "Build and prove the process.",
                "Deliver a useful process."),
            new ProcessDefinitionEditorGovernanceProjection(
                ProcessDefinitionCriticalityLevel.High,
                ProcessDefinitionAutonomyLevel.Guarded,
                ProcessDefinitionOperatingModeKind.GovernedLive,
                ProcessDefinitionAuthoringStatus.TemplateDefault,
                "Manager override.",
                "Governance notes.",
                "Change summary.",
                "Governance policy."),
            new ProcessDefinitionEditorContractProjection(
                "Interface contract.",
                "Constitution rule.",
                "Operating mode summary."),
            new ProcessDefinitionEditorSimulationProjection(
                "Safe deterministic simulation.",
                StepCount: 5,
                RequiredRoleCount: 2,
                RequiredArtifactExpectationCount: 3,
                IsReadyForSimulation: true));

        var editor = CreateEditor(
            key,
            draft,
            ProcessDefinitionAuthoringStatus.TemplateDefault,
            new ProcessDefinitionEditorVersionToken($"template:{key.Value}"),
            new ProcessDefinitionEditorLintProjection([]),
            lastReceipt: null);
        return editor with {
            RoleEditor = loadOptions.IncludeRoleEditor ? editor.RoleEditor : null,
            Canvas = loadOptions.IncludeCanvas ? editor.Canvas : null,
            StepEditor = loadOptions.IncludeStepEditor ? editor.StepEditor : null,
            TemplateCatalog = loadOptions.IncludeTemplateCatalog
                ? CreateTemplateCatalog(
                    key,
                    templateQuery ?? new ProcessTemplateCatalogQueryProjection(
                        SearchText: null,
                        ProcessTemplateCatalogCategoryKind.All,
                        SelectedItemKey: null,
                        ProcessTemplateCatalogPreviewTabKind.Overview,
                        Take: 50),
                    lastReceipt: null,
                    importedComponents: [])
                : null
        };
    }

    public static ProcessDefinitionEditorProjection CreateEditor(
        ProcessDefinitionCatalogItemKey key,
        ProcessDefinitionEditorDraftProjection draft,
        ProcessDefinitionAuthoringStatus status,
        ProcessDefinitionEditorVersionToken versionToken,
        ProcessDefinitionEditorLintProjection lint,
        ProcessDefinitionEditorCommandReceipt? lastReceipt)
        => new(
            key,
            versionToken,
            status,
            draft.Identity,
            draft.Governance with { WorkingStatus = status },
            draft.Contracts,
            draft.Simulation,
            lint,
            [
                new(ProcessDefinitionEditorCommandKind.SaveDraft, "Save draft", "save", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionEditorCommandKind.Publish, "Publish", "publish", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionEditorCommandKind.Archive, "Archive", "archive", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionEditorCommandKind.Delete, "Delete", "delete", IsEnabled: true, DisabledReason: null)
            ],
            lastReceipt) {
            RoleEditor = CreateRoleEditor(key),
            Canvas = CreateCanvas(key),
            StepEditor = CreateStepEditor(key)
        };

    public static ProcessDefinitionRoleEditorProjection CreateRoleEditor(ProcessDefinitionCatalogItemKey key) {
        var draft = CreateRoleDraft();
        return CreateRoleEditor(
            key,
            draft,
            new ProcessDefinitionRoleEditorVersionToken($"template:{key.Value}:roles"),
            new ProcessDefinitionRoleLintProjection([]),
            lastReceipt: null);
    }

    public static ProcessDefinitionRoleEditorProjection CreateRoleEditor(
        ProcessDefinitionCatalogItemKey key,
        ProcessDefinitionRoleDraftProjection draft,
        ProcessDefinitionRoleEditorVersionToken versionToken,
        ProcessDefinitionRoleLintProjection lint,
        ProcessDefinitionRoleCommandReceipt? lastReceipt) {
        var role = new ProcessDefinitionRoleProjection(
            draft.RoleKey,
            draft.DisplayName,
            draft.SnapshotSummary,
            draft,
            StepBindingCount: 1);
        return new ProcessDefinitionRoleEditorProjection(
            key,
            versionToken,
            role.RoleKey,
            [role],
            role,
            [
                new ProcessDefinitionRoleTemplateActionProjection(
                    new ProcessDefinitionRoleTemplateActionKey("role-template.solution-architect"),
                    "Solution architect template",
                    "Owns architecture decisions and technical tradeoffs.",
                    new ProcessDefinitionRoleKey("solution-architect"),
                    "solution-architect",
                    "Solution architect next",
                    ProcessDefinitionRoleExecutorKind.PersonOrAgent,
                    DefaultAllocationPercent: 60)
            ],
            [
                new ProcessDefinitionStepRoleBindingProjection(
                    new ProcessDefinitionStepKey("architecture-decision"),
                    "Architecture decision",
                    draft.RoleKey,
                    draft.DisplayName,
                    ProcessStepRoleResponsibilityKind.Approver,
                    IsRequired: true,
                    FallbackOrder: 1,
                    "Rebind to the architecture board when the primary owner is unavailable.")
            ],
            lint,
            [
                new(ProcessDefinitionRoleCommandKind.AddRole, "Add role", "add", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionRoleCommandKind.SaveRole, "Save role", "save", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionRoleCommandKind.ApplyTemplate, "Apply template", "content_copy", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionRoleCommandKind.DeleteRole, "Delete role", "delete", IsEnabled: true, DisabledReason: null)
            ],
            lastReceipt);
    }

    public static ProcessDefinitionRoleDraftProjection CreateRoleDraft()
        => new(
            new ProcessDefinitionRoleKey("solution-architect"),
            "Solution architect",
            "Own architecture decisions and technical tradeoffs.",
            "Assign a senior architecture owner before launch planning.",
            ProcessDefinitionRoleExecutorKind.PersonOrAgent,
            new ProcessDefinitionWorkflowPreferenceProjection(
                ProcessDefinitionRoleWorkflowPreferenceKind.SpecificWorkflow,
                WorkflowDefinitionId: null,
                WorkflowVersionId: null,
                "No workflow selected"),
            ProcessDefinitionRoleProjectAssignmentKind.Architect,
            IsRequired: true,
            AllowsFallback: true,
            RequiresExplicitApproval: true,
            DefaultAllocationPercent: 60,
            "process-role-template/solution-architect",
            "Solution architect v1",
            "Architecture role template snapshot.",
            ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate,
            "Resolved from process-role-template/solution-architect.");

    public static ProcessDefinitionStepEditorProjection CreateStepEditor(ProcessDefinitionCatalogItemKey key)
        => CreateStepEditor(
            key,
            CreateStepDraft(),
            new ProcessDefinitionStepEditorVersionToken($"template:{key.Value}:steps"),
            new ProcessDefinitionStepLintProjection([]),
            lastReceipt: null,
            commandKind: null);

    public static ProcessDefinitionStepEditorProjection CreateStepEditor(
        ProcessDefinitionCatalogItemKey key,
        ProcessDefinitionStepDraftProjection draft,
        ProcessDefinitionStepEditorVersionToken versionToken,
        ProcessDefinitionStepLintProjection lint,
        ProcessDefinitionStepCommandReceipt? lastReceipt,
        ProcessDefinitionStepCommandKind? commandKind) {
        var projectedDraft = commandKind switch {
            ProcessDefinitionStepCommandKind.AddBranchOutcome => draft with {
                BranchOutcomes =
                [
                    .. draft.BranchOutcomes,
                    new ProcessDefinitionBranchOutcomeProjection(
                        new ProcessDefinitionBranchOutcomeKey("architecture-decision-route-2"),
                        "Route 2",
                        "Second typed route.",
                        new ProcessDefinitionRouteTargetProjection(
                            ProcessDefinitionRouteTargetKind.NextStep,
                            StepKey: null,
                            ArtifactExpectationKey: null,
                            "Next step"),
                        IsBackwardRoute: false,
                        new ProcessDefinitionLoopBudgetProjection(
                            IsRequired: false,
                            MaximumRepeats: 0,
                            FingerprintPolicyKey: string.Empty,
                            ProcessDefinitionRouteTargetKind.Escalate))
                ]
            },
            ProcessDefinitionStepCommandKind.AddArtifactExpectation => draft with {
                ArtifactExpectations =
                [
                    .. draft.ArtifactExpectations,
                    new ProcessDefinitionArtifactExpectationProjection(
                        new ProcessDefinitionArtifactExpectationKey("architecture-decision-evidence"),
                        "architecture-decision-evidence",
                        "Architecture decision evidence",
                        ProcessDefinitionArtifactKind.Evidence,
                        IsRequired: true,
                        ProcessDefinitionArtifactTrustRequirement.ReviewRequired,
                        ProcessDefinitionArtifactSensitivityLevel.Internal,
                        RetentionDays: 365,
                        WorkflowOutputId: string.Empty,
                        WorkflowOutputName: string.Empty,
                        ProcessDefinitionWorkflowOutputKind.Unspecified,
                        SubprocessChildArtifactExpectationId: null,
                        SubprocessChildStepKey: string.Empty,
                        SubprocessChildArtifactTitle: string.Empty,
                        AllowedFutureUsageSummary: "Reusable for route replay.",
                        ValidationRequirementSummary: "Must identify evidence source.")
                ]
            },
            _ => draft
        };

        return new ProcessDefinitionStepEditorProjection(
            key,
            versionToken,
            projectedDraft.Basic.StepKey,
            [
                new ProcessDefinitionStepListItemProjection(
                    projectedDraft.Basic.StepKey,
                    projectedDraft.Basic.Title,
                    projectedDraft.Basic.Subtitle,
                    projectedDraft.Basic.StepKind,
                    Order: 0,
                    IsSelected: true)
            ],
            [projectedDraft],
            projectedDraft,
            [
                new ProcessDefinitionSubprocessOptionProjection(
                    new ProcessDefinitionCatalogItemKey("delivery-default"),
                    "Delivery default",
                    "Default delivery subprocess.")
            ],
            [
                new(ProcessDefinitionStepCommandKind.SaveStep, "Save step", "save", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionStepCommandKind.AddBranchOutcome, "Add route", "alt_route", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionStepCommandKind.AddArtifactExpectation, "Add artifact", "inventory_2", IsEnabled: true, DisabledReason: null),
                new(ProcessDefinitionStepCommandKind.MapSubprocess, "Map subprocess", "account_tree", IsEnabled: true, DisabledReason: null)
            ],
            lint,
            lastReceipt);
    }

    public static ProcessTemplateCatalogProjection CreateTemplateCatalog(
        ProcessDefinitionCatalogItemKey definitionKey,
        ProcessTemplateCatalogQueryProjection query,
        ProcessTemplateImportCommandReceipt? lastReceipt,
        IReadOnlyList<ProcessTemplateImportedComponentProjection> importedComponents) {
        var allItems = new[] {
            new ProcessTemplateCatalogItemProjection(
                new ProcessTemplateCatalogItemKey("process:blazor-app-delivery"),
                ProcessTemplateCatalogItemKind.Process,
                "Blazor app delivery",
                "Build and prove a Blazor application.",
                "blazor-app-delivery",
                "blazor-app-delivery",
                "Process",
                [new("Source", "blazor-app-delivery")],
                IsSelected: false),
            new ProcessTemplateCatalogItemProjection(
                new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"),
                ProcessTemplateCatalogItemKind.Role,
                "Solution architect",
                "Owns architecture decisions and technical tradeoffs.",
                "blazor-app-delivery",
                "solution-architect",
                "Role",
                [new("Executor", "person-or-agent")],
                IsSelected: false),
            new ProcessTemplateCatalogItemProjection(
                new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record"),
                ProcessTemplateCatalogItemKind.Artifact,
                "Architecture decision record",
                "Must include selected option and rationale.",
                "blazor-app-delivery",
                "architecture-decision-record",
                "Artifact",
                [new("Artifact", "Deliverable")],
                IsSelected: false)
        };
        var categoryFiltered = query.Category switch {
            ProcessTemplateCatalogCategoryKind.Processes => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Process),
            ProcessTemplateCatalogCategoryKind.Roles => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Role),
            ProcessTemplateCatalogCategoryKind.Artifacts => allItems.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Artifact),
            _ => allItems
        };
        var filtered = string.IsNullOrWhiteSpace(query.SearchText)
            ? categoryFiltered.ToArray()
            : categoryFiltered
                .Where(item => item.Title.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                               item.SourceComponentKey.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        var selected = query.SelectedItemKey is { } selectedKey
            ? filtered.FirstOrDefault(item => item.Key == selectedKey)
            : filtered.FirstOrDefault();
        var selectedQuery = query with { SelectedItemKey = selected?.Key ?? query.SelectedItemKey };
        var importedKeys = importedComponents.Select(component => component.ItemKey).ToHashSet();
        var projectedItems = filtered
            .Select(item => item with {
                IsSelected = selected?.Key == item.Key,
                Facts = importedKeys.Contains(item.Key)
                    ? [.. item.Facts, new ProcessTemplateCatalogFactProjection("Import", "Imported")]
                    : item.Facts
            })
            .ToArray();
        var preview = selected is null
            ? null
            : new ProcessTemplateCatalogPreviewProjection(
                selected.Key,
                selected.Kind,
                selected.Title,
                selected.Summary,
                "processes/blazor-app-delivery/definition.json",
                "sha256:test-template-hash",
                "Generated projections are derived from canonical JSON.",
                "# Blazor app delivery\n\nGenerated from canonical JSON process template `blazor-app-delivery`.",
                "flowchart TD\n    process[\"Blazor app delivery\"]\n    step[\"Architecture decision\"]\n    process --> step",
                "{\"key\":\"blazor-app-delivery\",\"displayName\":\"Blazor app delivery\"}",
                [
                    new("process:blazor-app-delivery", ParentNodeKey: null, ProcessTemplateStructureNodeKind.Process, "Blazor app delivery", "Build and prove a Blazor application.", Depth: 0),
                    new("process:blazor-app-delivery:steps", "process:blazor-app-delivery", ProcessTemplateStructureNodeKind.Section, "Steps", "1 step", Depth: 1),
                    new("process:blazor-app-delivery:steps:architecture-decision", "process:blazor-app-delivery:steps", ProcessTemplateStructureNodeKind.Step, "Architecture decision", "Governed review step.", Depth: 2)
                ],
                [
                    new(
                        new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"),
                        ProcessTemplateCatalogItemKind.Role,
                        "Solution architect",
                        "Owns architecture decisions and technical tradeoffs.",
                        "blazor-app-delivery",
                        "solution-architect",
                        importedKeys.Contains(new ProcessTemplateCatalogItemKey("role:blazor-app-delivery:solution-architect"))),
                    new(
                        new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record"),
                        ProcessTemplateCatalogItemKind.Artifact,
                        "Architecture decision record",
                        "Must include selected option and rationale.",
                        "blazor-app-delivery",
                        "architecture-decision-record",
                        importedKeys.Contains(new ProcessTemplateCatalogItemKey("artifact:blazor-app-delivery:architecture-decision:architecture-decision-record")))
                ]);

        return new ProcessTemplateCatalogProjection(
            definitionKey,
            lastReceipt?.VersionToken ?? new ProcessTemplateCatalogVersionToken("templates:test:0"),
            selectedQuery,
            string.IsNullOrWhiteSpace(query.SearchText)
                ? "3 template catalog item(s) from pack test-pack."
                : $"{filtered.Length} template catalog item(s) match '{query.SearchText}'.",
            "test-pack",
            "Template catalog is projected from canonical JSON.",
            [
                new(ProcessTemplateCatalogCategoryKind.All, "All", "All template items.", allItems.Length, query.Category == ProcessTemplateCatalogCategoryKind.All),
                new(ProcessTemplateCatalogCategoryKind.Processes, "Processes", "Process templates.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Processes),
                new(ProcessTemplateCatalogCategoryKind.Roles, "Roles", "Role components.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Roles),
                new(ProcessTemplateCatalogCategoryKind.Artifacts, "Artifacts", "Artifact components.", 1, query.Category == ProcessTemplateCatalogCategoryKind.Artifacts)
            ],
            projectedItems,
            selected,
            preview,
            [
                new(
                    new ProcessDefinitionStepKey("architecture-decision"),
                    "Architecture decision",
                    "Governed review step",
                    IsDefaultTarget: true)
            ],
            [
                new(ProcessTemplateImportCommandKind.ImportProcess, "Import process", "account_tree", selected?.Kind == ProcessTemplateCatalogItemKind.Process, null),
                new(ProcessTemplateImportCommandKind.ImportRole, "Import role", "badge", selected?.Kind == ProcessTemplateCatalogItemKind.Role, null),
                new(ProcessTemplateImportCommandKind.ImportArtifact, "Import artifact", "inventory_2", selected?.Kind == ProcessTemplateCatalogItemKind.Artifact, null)
            ],
            importedComponents,
            lastReceipt);
    }

    public static ProcessDefinitionStepDraftProjection CreateStepDraft()
        => new(
            new ProcessDefinitionStepBasicDraftProjection(
                new ProcessDefinitionStepKey("architecture-decision"),
                "Architecture decision",
                "Governed review step",
                "Choose an architecture route from typed outcomes.",
                ProcessDefinitionStepKind.Decision,
                TargetLeadHours: 12,
                AllowsManualSkip: false,
                AllowsSafeRefusal: true,
                RequiresApproval: true,
                RequiresDecisionRecord: true,
                new ProcessDefinitionRoleKey("solution-architect")),
            new ProcessDefinitionStepOperationContractProjection(
                ProcessDefinitionStepTargetScopeKind.ExternalArtifactDestination,
                [
                    ProcessDefinitionStepOperationKind.ReadProcessContext,
                    ProcessDefinitionStepOperationKind.WriteExternalArtifactDestination
                ]),
            new ProcessDefinitionStepContractsProjection(
                "Architecture concern and project context.",
                "Architecture decision record.",
                "Decision evidence and route rationale.",
                "Solution architect decides the route.",
                "Escalate when evidence is contradictory."),
            [
                new ProcessDefinitionBranchOutcomeProjection(
                    new ProcessDefinitionBranchOutcomeKey("approved"),
                    "Approved",
                    "Route to the approved implementation lane.",
                    new ProcessDefinitionRouteTargetProjection(
                        ProcessDefinitionRouteTargetKind.NextStep,
                        StepKey: null,
                        ArtifactExpectationKey: null,
                        "Next step"),
                    IsBackwardRoute: false,
                    new ProcessDefinitionLoopBudgetProjection(
                        IsRequired: false,
                        MaximumRepeats: 0,
                        FingerprintPolicyKey: string.Empty,
                        ProcessDefinitionRouteTargetKind.Escalate))
            ],
            [
                new ProcessDefinitionStepRoleBindingProjection(
                    new ProcessDefinitionStepKey("architecture-decision"),
                    "Architecture decision",
                    new ProcessDefinitionRoleKey("solution-architect"),
                    "Solution architect",
                    ProcessStepRoleResponsibilityKind.Approver,
                    IsRequired: true,
                    FallbackOrder: 1,
                    "Rebind to the architecture board when unavailable.")
            ],
            [
                new ProcessDefinitionArtifactExpectationProjection(
                    new ProcessDefinitionArtifactExpectationKey("architecture-decision-record"),
                    "architecture-decision-record",
                    "Architecture decision record",
                    ProcessDefinitionArtifactKind.Deliverable,
                    IsRequired: true,
                    ProcessDefinitionArtifactTrustRequirement.ReviewRequired,
                    ProcessDefinitionArtifactSensitivityLevel.Internal,
                    RetentionDays: 365,
                    WorkflowOutputId: "adr-output",
                    WorkflowOutputName: "Architecture decision record",
                    ProcessDefinitionWorkflowOutputKind.Artifact,
                    SubprocessChildArtifactExpectationId: null,
                    SubprocessChildStepKey: string.Empty,
                    SubprocessChildArtifactTitle: string.Empty,
                    AllowedFutureUsageSummary: "Reusable for implementation planning.",
                    ValidationRequirementSummary: "Must include selected option and rationale.")
            ],
            new ProcessDefinitionSubprocessMappingProjection(
                ProcessKey: string.Empty,
                DefinitionSnapshotName: string.Empty,
                ChildArtifactMappings: []));

    public static ProcessDefinitionCanvasEditorProjection CreateCanvas(
        ProcessDefinitionCatalogItemKey key,
        ProcessDefinitionCanvasVersionToken? versionToken = null,
        ProcessDefinitionCanvasCommandReceipt? receipt = null,
        ProcessDefinitionCanvasCommandKind? commandKind = null) {
        var stepKey = new ProcessDefinitionCanvasNodeKey("step:architecture-decision");
        var branchKey = new ProcessDefinitionCanvasNodeKey("branch:architecture-decision");
        var roleKey = new ProcessDefinitionCanvasNodeKey("role:solution-architect");
        var artifactKey = new ProcessDefinitionCanvasNodeKey("artifact:architecture-decision:adr");
        var nodes = new[] {
            CreateCanvasNode(
                stepKey,
                ProcessDefinitionCanvasNodeKind.Step,
                commandKind == ProcessDefinitionCanvasCommandKind.AddStep ? "Implementation" : "Architecture decision",
                "Governed review step",
                "Select the architecture decision step without losing editor context.",
                160,
                220,
                "info",
                new ProcessDefinitionStepKey("architecture-decision"),
                RoleKey: null,
                ArtifactKey: null,
                ["Step"]),
            CreateCanvasNode(
                branchKey,
                ProcessDefinitionCanvasNodeKind.BranchRouter,
                "Architecture decision routes",
                "Typed branch router",
                "Route labels are display text; the route target stays typed.",
                420,
                110,
                "warning",
                new ProcessDefinitionStepKey("architecture-decision"),
                RoleKey: null,
                ArtifactKey: null,
                ["Branch"]),
            CreateCanvasNode(
                roleKey,
                ProcessDefinitionCanvasNodeKind.Role,
                "Solution architect",
                "person-or-agent",
                "Architecture authority for the selected step.",
                160,
                40,
                "success",
                StepKey: null,
                RoleKey: new ProcessDefinitionRoleKey("solution-architect"),
                ArtifactKey: null,
                ["Required"]),
            CreateCanvasNode(
                artifactKey,
                ProcessDefinitionCanvasNodeKind.Artifact,
                "Architecture decision record",
                "Deliverable",
                "Required evidence for the selected step.",
                160,
                370,
                "accent",
                new ProcessDefinitionStepKey("architecture-decision"),
                RoleKey: null,
                ArtifactKey: "architecture-decision-record",
                ["Artifact"])
        };
        var edges = new[] {
            new ProcessDefinitionCanvasEdgeProjection(
                new ProcessDefinitionCanvasEdgeKey("branch-route:architecture-decision:router"),
                ProcessDefinitionCanvasEdgeKind.BranchRoute,
                stepKey,
                branchKey,
                "approved",
                "Typed route from architecture decision to the approved lane.",
                "warning",
                IsBackwardRoute: false),
            new ProcessDefinitionCanvasEdgeProjection(
                new ProcessDefinitionCanvasEdgeKey("role-binding:solution-architect:architecture-decision"),
                ProcessDefinitionCanvasEdgeKind.RoleBinding,
                roleKey,
                stepKey,
                "Approver",
                "Solution architect approves the architecture decision.",
                "success",
                IsBackwardRoute: false),
            new ProcessDefinitionCanvasEdgeProjection(
                new ProcessDefinitionCanvasEdgeKey("artifact:architecture-decision:adr"),
                ProcessDefinitionCanvasEdgeKind.ArtifactExpectation,
                stepKey,
                artifactKey,
                "evidence",
                "Architecture decision record is required evidence.",
                "accent",
                IsBackwardRoute: false)
        };

        return new ProcessDefinitionCanvasEditorProjection(
            key,
            versionToken ?? new ProcessDefinitionCanvasVersionToken($"template:{key.Value}:canvas"),
            new ProcessDefinitionCanvasViewportProjection(960, 560, "Test canvas bounds."),
            nodes,
            edges,
            [
                new ProcessDefinitionCanvasToolboxActionProjection(
                    new ProcessDefinitionCanvasToolboxActionKey("process-step.implementation"),
                    ProcessDefinitionCanvasToolboxActionKind.Step,
                    "Implementation",
                    "Add an implementation step.",
                    "add",
                    IsEnabled: true,
                    DisabledReason: null),
                new ProcessDefinitionCanvasToolboxActionProjection(
                    new ProcessDefinitionCanvasToolboxActionKey("process-step.decision"),
                    ProcessDefinitionCanvasToolboxActionKind.BranchRouter,
                    "Decision router",
                    "Add a typed branch router to the selected step.",
                    "alt_route",
                    IsEnabled: true,
                    DisabledReason: null),
                new ProcessDefinitionCanvasToolboxActionProjection(
                    new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-role-binding"),
                    ProcessDefinitionCanvasToolboxActionKind.RoleBinding,
                    "Role binding",
                    "Connect the selected step to a role.",
                    "badge",
                    IsEnabled: true,
                    DisabledReason: null),
                new ProcessDefinitionCanvasToolboxActionProjection(
                    new ProcessDefinitionCanvasToolboxActionKey("process-canvas.add-artifact-expectation"),
                    ProcessDefinitionCanvasToolboxActionKind.ArtifactExpectation,
                    "Artifact expectation",
                    "Attach required evidence to the selected step.",
                    "inventory_2",
                    IsEnabled: true,
                    DisabledReason: null)
            ],
            new ProcessDefinitionCanvasSelectionProjection(
                ProcessDefinitionCanvasSelectionKind.Step,
                stepKey,
                EdgeKey: null,
                "Architecture decision",
                "Select the architecture decision step without losing editor context.",
                "architecture-decision",
                ["Step"]),
            [
                new ProcessDefinitionCanvasCommandProjection(
                    ProcessDefinitionCanvasCommandKind.Recompose,
                    "Recompose",
                    "auto_fix_high",
                    IsEnabled: true,
                    DisabledReason: null)
            ],
            receipt);
    }

    public static ProcessDefinitionCanvasEditorNodeProjection CreateCanvasNode(
        ProcessDefinitionCanvasNodeKey nodeKey,
        ProcessDefinitionCanvasNodeKind kind,
        string title,
        string subtitle,
        string summary,
        double x,
        double y,
        string tone,
        ProcessDefinitionStepKey? StepKey,
        ProcessDefinitionRoleKey? RoleKey,
        string? ArtifactKey,
        IReadOnlyList<string> badges)
        => new(
            nodeKey,
            kind,
            title,
            subtitle,
            summary,
            x,
            y,
            Width: kind == ProcessDefinitionCanvasNodeKind.BranchRouter ? 168 : 220,
            Height: kind == ProcessDefinitionCanvasNodeKind.Artifact ? 72 : 92,
            tone,
            StepKey,
            RoleKey,
            ArtifactKey,
            badges,
            CreateCanvasPorts(kind));

    public static IReadOnlyList<ProcessDefinitionCanvasPortProjection> CreateCanvasPorts(
        ProcessDefinitionCanvasNodeKind kind)
        => kind switch {
            ProcessDefinitionCanvasNodeKind.Step =>
            [
                new("in", ProcessDefinitionCanvasPortKind.StructuralInput, "Input", 0, 46),
                new("out", ProcessDefinitionCanvasPortKind.StructuralOutput, "Output", 220, 46),
                new("role", ProcessDefinitionCanvasPortKind.RoleBinding, "Role", 110, 0),
                new("artifact", ProcessDefinitionCanvasPortKind.ArtifactExpectation, "Artifact", 110, 92)
            ],
            ProcessDefinitionCanvasNodeKind.BranchRouter =>
            [
                new("in", ProcessDefinitionCanvasPortKind.StructuralInput, "Decision input", 0, 46),
                new("out", ProcessDefinitionCanvasPortKind.BranchOutcome, "Outcome", 168, 46)
            ],
            ProcessDefinitionCanvasNodeKind.Role =>
            [
                new("role-out", ProcessDefinitionCanvasPortKind.RoleBinding, "Responsibility", 220, 46)
            ],
            ProcessDefinitionCanvasNodeKind.Artifact =>
            [
                new("artifact-in", ProcessDefinitionCanvasPortKind.ArtifactExpectation, "Expectation", 0, 36)
            ],
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary =>
            [
                new("subprocess-in", ProcessDefinitionCanvasPortKind.SubprocessBoundary, "Child process", 0, 46)
            ],
            _ => []
        };

    public static ProcessDefinitionEditorLintProjection CreateEditorLint(
        ProcessDefinitionEditorCommand command) {
        if (!string.IsNullOrWhiteSpace(command.Draft.Identity.Name)) {
            return new ProcessDefinitionEditorLintProjection([]);
        }

        return new ProcessDefinitionEditorLintProjection(
        [
            new ProcessDefinitionEditorLintIssueProjection(
                "processes.definition.identity.name-required",
                ProcessDefinitionEditorLintSeverity.Error,
                ProcessDefinitionEditorLintSection.Identity,
                "Definition name is required.",
                "Enter a stable, user-facing definition name.")
        ]);
    }

    public static ProcessDefinitionRoleLintProjection CreateRoleLint(
        ProcessDefinitionRoleEditorCommand command) {
        if (!string.IsNullOrWhiteSpace(command.Draft.DisplayName) &&
            command.Draft.PreferredExecutorKind != ProcessDefinitionRoleExecutorKind.Unspecified &&
            command.Draft.DefaultAllocationPercent is >= 0 and <= 100) {
            return new ProcessDefinitionRoleLintProjection([]);
        }

        return new ProcessDefinitionRoleLintProjection(
        [
            new ProcessDefinitionRoleLintIssueProjection(
                "processes.definition.role.execution.invalid",
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Execution,
            "Role execution fields are invalid.",
            "Choose a typed executor kind and bounded allocation.")
        ]);
    }

    public static ProcessDefinitionStepLintProjection CreateStepLint(
        ProcessDefinitionStepEditorCommand command) {
        if (!string.IsNullOrWhiteSpace(command.Draft.Basic.Title) &&
            command.Draft.OperationContract.TargetScope != ProcessDefinitionStepTargetScopeKind.Unspecified) {
            return new ProcessDefinitionStepLintProjection([]);
        }

        return new ProcessDefinitionStepLintProjection(
        [
            new ProcessDefinitionStepLintIssueProjection(
                "processes.definition.step.invalid",
                ProcessDefinitionStepLintSeverity.Error,
                ProcessDefinitionStepLintSection.Basic,
                "Step fields are invalid.",
                "Enter a title and choose an explicit operation target scope.")
        ]);
    }

    public static IReadOnlyList<ProcessWorkspaceTabProjection> CreateTabs()
        =>
        [
            new(ProcessWorkspaceTabKey.Definitions, "Definitions", "account_tree", "Definition catalog.", "2", IsEnabled: true),
            new(ProcessWorkspaceTabKey.LaunchPlans, "Launch plans", "rocket_launch", "Launch plans.", "0", IsEnabled: true),
            new(ProcessWorkspaceTabKey.LiveRuns, "Live runs", "monitor_heart", "Live runs.", "0", IsEnabled: true),
            new(ProcessWorkspaceTabKey.History, "History", "history", "History.", "0", IsEnabled: true)
        ];

    public static IReadOnlyList<ProcessWorkspaceCommandProjection> CreateCommands()
        =>
        [
            new(ProcessWorkspaceCommandKind.RefreshProjections, "Refresh", "refresh", IsEnabled: true, DisabledReason: null),
            new(ProcessWorkspaceCommandKind.OpenAgentContext, "Agent context", "smart_toy", IsEnabled: true, DisabledReason: null),
            new(ProcessWorkspaceCommandKind.CreateDefinition, "New definition", "add", IsEnabled: false, "Definition editing is not available in this workspace shell."),
            new(ProcessWorkspaceCommandKind.FeedDefaults, "Feed defaults", "download", IsEnabled: true, DisabledReason: null),
            new(ProcessWorkspaceCommandKind.LaunchRun, "Launch", "rocket_launch", IsEnabled: true, DisabledReason: null),
            new(ProcessWorkspaceCommandKind.OpenLiveDashboard, "Live dashboard", "open_in_new", IsEnabled: true, DisabledReason: null)
        ];

    public static ProcessWorkspaceAgentEntryProjection CreateAgentEntry(ProcessWorkspaceShellRequest request) {
        if (request.Selection.RunId is { } runId) {
            return new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.RunContext,
                IsAvailable: true,
                "Open run agent context",
                $"processes:workspace:run:{runId:N}",
                DisabledReason: null);
        }

        return new ProcessWorkspaceAgentEntryProjection(
            ProcessWorkspaceAgentEntryKind.WorkspaceContext,
            IsAvailable: true,
            "Open process agent context",
            "processes:workspace",
            DisabledReason: null);
    }
}
