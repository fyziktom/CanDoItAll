using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

public enum ProcessWorkspaceMockScenarioKind
{
    Operations
}

public sealed class ProcessWorkspaceMockProjectionFactory(IProcessProjectionClock clock)
{
    private static readonly Guid VendorRunId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid VendorActionStepId = Guid.Parse("33333333-3333-3333-3333-aaaaaaaaaaaa");

    public bool TryParseScenario(string? value, out ProcessWorkspaceMockScenarioKind scenario)
    {
        scenario = ProcessWorkspaceMockScenarioKind.Operations;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, "operations", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "live", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new ArgumentException($"Unknown LiveProcesses mock scenario '{value}'. Supported scenario: operations.");
    }

    public ProcessWorkspaceShellProjection CreateShell(
        ProcessWorkspaceShellProjection baseline,
        ProcessWorkspaceShellRequest request,
        ProcessWorkspaceMockScenarioKind scenario,
        IReadOnlySet<Guid> resolvedOperatorActionStepIds)
    {
        return scenario switch
        {
            ProcessWorkspaceMockScenarioKind.Operations => CreateOperationsShell(baseline, request, resolvedOperatorActionStepIds),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported LiveProcesses mock scenario.")
        };
    }

    private ProcessWorkspaceShellProjection CreateOperationsShell(
        ProcessWorkspaceShellProjection baseline,
        ProcessWorkspaceShellRequest request,
        IReadOnlySet<Guid> resolvedOperatorActionStepIds)
    {
        var now = clock.GetUtcNow();
        var freshness = new ProcessProjectionFreshness(
            now,
            SourceGlobalSequence: 984,
            new ProcessProjectionLag(984, 982, BacklogEventCount: 2));

        var runs = CreateRuns(now, freshness, resolvedOperatorActionStepIds);
        var selectedRunId = ResolveSelectedRunId(request, runs);
        var selectedRun = runs.FirstOrDefault(run => run.RunId.Value == selectedRunId);
        var events = CreateEvents(now, runs);
        var managerMessages = CreateManagerMessages(now, runs, resolvedOperatorActionStepIds);
        var activeAgents = CreateActiveAgents(now, runs);
        var toolUsage = CreateToolUsage(now);
        var metricPoints = CreateMetricPoints(now);
        var incidents = runs.SelectMany(run => run.Incidents).ToArray();

        var stats = new ProcessRuntimeStatsProjection(
            ObservedRunCount: runs.Count,
            ActiveRunCount: runs.Count(run => run.Status == ProcessProjectedRunStatus.Active),
            AttentionRunCount: runs.Count(RequiresAttention),
            FailedRunCount: runs.Count(run => run.Status == ProcessProjectedRunStatus.Failed),
            EventCount: events.Count,
            ManagerEventCount: managerMessages.Count,
            ToolCallCount: toolUsage.Sum(tool => tool.CallCount),
            DurationMs: checked((long)(now - runs.Min(run => run.FirstEventAtUtc)).TotalMilliseconds),
            InputTokens: 184_000,
            CachedInputTokens: 41_000,
            OutputTokens: 29_000,
            TotalTokens: 221_000,
            EstimatedCost: 14.25m,
            ActualCost: 11.80m);

        var runtime = new ProcessRuntimeWorkspaceProjection(
            request.RuntimeQuery?.HistoryWindow ?? ProcessRuntimeHistoryWindow.OneDay,
            request.RuntimeQuery?.EventPage ?? 0,
            request.RuntimeQuery?.EventPageSize ?? 25,
            HasMoreEvents: true,
            selectedRunId,
            selectedRun is null
                ? null
                : new ProcessRunDetailProjection(
                    selectedRun.RootRunId,
                    selectedRun.RunId,
                    selectedRun.Status,
                    selectedRun.FirstEventAtUtc,
                    selectedRun.LastEventAtUtc,
                    freshness,
                    selectedRun.RecentEvents),
            runs,
            events,
            incidents,
            managerMessages,
            activeAgents,
            stats,
            metricPoints,
            toolUsage,
            freshness,
            $"{runs.Count:N0} run(s), {stats.ActiveRunCount:N0} active, {stats.AttentionRunCount:N0} needing attention, {stats.EventCount:N0} event(s) in this mock window.",
            "Mock scenario: multi-team delivery is active, one run is blocked on operator rework, one run is waiting on a subprocess, and one failed run needs inspection.")
        {
            ReusableRuns = runs
        };

        return baseline with
        {
            LiveRuns = new ProcessLiveRunSummaryProjection(
                stats.ActiveRunCount,
                stats.AttentionRunCount,
                stats.FailedRunCount,
                events.Max(item => item.OccurredAtUtc),
                runtime.Summary),
            Refresh = new ProcessWorkspaceProjectionRefreshProjection(
                ProcessWorkspaceProjectionStatus.Ready,
                now,
                freshness.SourceGlobalSequence,
                freshness.Lag.BacklogEventCount,
                "Mock LiveProcesses projection generated for UX development."),
            Runtime = runtime
        };
    }

    private static Guid? ResolveSelectedRunId(
        ProcessWorkspaceShellRequest request,
        IReadOnlyList<ProcessLiveProcessSnapshot> runs)
    {
        var requested = request.RuntimeQuery?.SelectedRunId;
        return requested.HasValue && runs.Any(run => run.RunId.Value == requested.Value)
            ? requested.Value
            : null;
    }

    private static IReadOnlyList<ProcessLiveProcessSnapshot> CreateRuns(
        DateTimeOffset now,
        ProcessProjectionFreshness freshness,
        IReadOnlySet<Guid> resolvedOperatorActionStepIds)
    {
        var customerOnboarding = CreateRun(
            now,
            freshness,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ProcessProjectedRunStatus.Active,
            "Validate customer data",
            "customer-data-validation",
            ".NET Developer",
            "Agent Alpha is validating imported customer records.",
            firstMinutesAgo: 68,
            lastMinutesAgo: 2,
            attemptNumber: 2,
            eventSeed: 100);

        var invoiceProcessing = CreateRun(
            now,
            freshness,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ProcessProjectedRunStatus.Active,
            "Extract invoice data",
            "invoice-extraction",
            "Data extraction agent",
            "Agent Beta is extracting invoice fields from uploaded PDFs.",
            firstMinutesAgo: 52,
            lastMinutesAgo: 4,
            attemptNumber: 1,
            eventSeed: 200);

        var vendorOnboarding = CreateRun(
            now,
            freshness,
            VendorRunId,
            ProcessProjectedRunStatus.NeedsAttention,
            "External verification",
            "external-verification",
            "Verification manager",
            "Vendor risk verification found mismatched company registry data.",
            firstMinutesAgo: 126,
            lastMinutesAgo: 8,
            attemptNumber: 3,
            eventSeed: 300)
            with
            {
                OperatorActions = resolvedOperatorActionStepIds.Contains(VendorActionStepId)
                    ? []
                    :
                    [
                        new ProcessRuntimeOperatorActionProjection(
                            VendorRunId,
                            VendorActionStepId,
                            "external-verification",
                            ProcessRuntimeStepStatus.Blocked.ToString(),
                            "verification-manager",
                            "Verification manager",
                            "Agent Gamma",
                            ProcessRuntimeOperatorActionKind.RequestRework,
                            "Request rework",
                            "The registry lookup returned conflicting legal-address data and needs an operator-approved retry.",
                            IsEnabled: true,
                            DisabledReason: null)
                        {
                            ProblemSummary = "External verification is blocked because registry data conflicts with the vendor-provided legal address.",
                            RequiredOperatorDecision = "Approve rework to retry external verification with the alternate registry source and preserve accepted onboarding artifacts.",
                            RecommendedInstruction = "Manager-approved rework for step 'external-verification'. Retry registry verification with the alternate data source, keep accepted artifacts, and continue the vendor onboarding run.",
                            PrimaryRootCause = true
                        }
                    ],
                Incidents =
                [
                    new ProcessIncidentProjection(
                        "mock-incident-vendor-verification",
                        new ProcessRunId(VendorRunId),
                        new ProcessRunId(VendorRunId),
                        "ManagerIncident",
                        "NeedsAttention",
                        "Raised",
                        "Registry data conflicts with vendor-provided legal address.",
                        "mock:vendor-verification",
                        now.AddMinutes(-8))
                ]
            };

        if (resolvedOperatorActionStepIds.Contains(VendorActionStepId))
        {
            vendorOnboarding = vendorOnboarding with
            {
                Status = ProcessProjectedRunStatus.Active,
                Incidents = [],
                CurrentStep = vendorOnboarding.CurrentStep! with
                {
                    StepStatus = ProcessRuntimeStepStatus.Ready.ToString(),
                    Summary = "Verification retry has been approved and is queued for dispatch."
                }
            };
        }

        var orderFulfillment = CreateRun(
            now,
            freshness,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ProcessProjectedRunStatus.Active,
            "Reserve inventory",
            "reserve-inventory",
            "Inventory coordinator",
            "Order fulfillment is waiting for the warehouse subprocess to confirm stock reservation.",
            firstMinutesAgo: 44,
            lastMinutesAgo: 5,
            attemptNumber: 1,
            eventSeed: 400)
            with
            {
                WaitingOnChildRuns =
                [
                    new ProcessRuntimeChildRunWaitProjection(
                        Guid.Parse("44444444-4444-4444-4444-444444444444"),
                        Guid.Parse("44444444-4444-4444-4444-aaaaaaaaaaaa"),
                        "reserve-inventory",
                        ProcessRuntimeStepStatus.Running.ToString(),
                        Guid.Parse("44444444-4444-4444-4444-bbbbbbbbbbbb"),
                        ProcessProjectedRunStatus.Active.ToString(),
                        "warehouse-reservation",
                        ProcessRuntimeStepStatus.Running.ToString(),
                        "Warehouse reservation subprocess is confirming stock before fulfillment continues.")
                ]
            };

        var paymentReconciliation = CreateRun(
            now,
            freshness,
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            ProcessProjectedRunStatus.Failed,
            "Match transactions",
            "match-transactions",
            "Payment reconciler",
            "Payment reconciliation failed because a gateway export could not be parsed.",
            firstMinutesAgo: 92,
            lastMinutesAgo: 16,
            attemptNumber: 2,
            eventSeed: 500)
            with
            {
                Incidents =
                [
                    new ProcessIncidentProjection(
                        "mock-incident-payment-export",
                        new ProcessRunId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                        new ProcessRunId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                        "RuntimeFailure",
                        "Failed",
                        "Raised",
                        "Gateway export parser failed on malformed settlement row.",
                        "mock:payment-parser",
                        now.AddMinutes(-16))
                ]
            };

        var statementGeneration = CreateRun(
            now,
            freshness,
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            ProcessProjectedRunStatus.Completed,
            "Generate statements",
            "generate-statements",
            "Statement generator",
            "Statement generation completed with final PDF and reconciliation report.",
            firstMinutesAgo: 78,
            lastMinutesAgo: 21,
            attemptNumber: 1,
            eventSeed: 600);

        return
        [
            customerOnboarding,
            invoiceProcessing,
            vendorOnboarding,
            orderFulfillment,
            paymentReconciliation,
            statementGeneration
        ];
    }

    private static ProcessLiveProcessSnapshot CreateRun(
        DateTimeOffset now,
        ProcessProjectionFreshness freshness,
        Guid runIdValue,
        ProcessProjectedRunStatus status,
        string stepTitle,
        string stepKey,
        string roleDisplayName,
        string summary,
        int firstMinutesAgo,
        int lastMinutesAgo,
        int attemptNumber,
        int eventSeed)
    {
        var runId = new ProcessRunId(runIdValue);
        var events = new[]
        {
            CreateEvent(now, eventSeed, runId, "ProcessRunActivated", $"{stepTitle} run started.", firstMinutesAgo),
            CreateEvent(now, eventSeed + 1, runId, "StepRunning", summary, lastMinutesAgo + 4),
            CreateEvent(now, eventSeed + 2, runId, status == ProcessProjectedRunStatus.Failed ? "StepFailed" : "ToolCallCompleted", $"Latest update: {summary}", lastMinutesAgo)
        };

        return new ProcessLiveProcessSnapshot(
            runId,
            runId,
            status,
            IsActive: status is ProcessProjectedRunStatus.Active or ProcessProjectedRunStatus.NeedsAttention,
            now.AddMinutes(-firstMinutesAgo),
            now.AddMinutes(-lastMinutesAgo),
            freshness,
            events,
            Incidents: [])
        {
            ProcessName = stepTitle,
            CurrentStep = new ProcessRuntimeCurrentStepProjection(
                runIdValue,
                CreateStepInstanceId(runIdValue),
                stepKey,
                status == ProcessProjectedRunStatus.Failed
                    ? ProcessRuntimeStepStatus.Failed.ToString()
                    : status == ProcessProjectedRunStatus.NeedsAttention
                        ? ProcessRuntimeStepStatus.Blocked.ToString()
                        : ProcessRuntimeStepStatus.Running.ToString(),
                roleDisplayName.Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant(),
                roleDisplayName,
                roleDisplayName,
                attemptNumber,
                IsWorking: status == ProcessProjectedRunStatus.Active,
                IsLeaseExpired: false,
                now.AddMinutes(-lastMinutesAgo),
                now.AddMinutes(-Math.Max(firstMinutesAgo - 8, lastMinutesAgo + 1)),
                now.AddMinutes(20),
                summary)
        };
    }

    private static Guid CreateStepInstanceId(Guid runIdValue)
        => runIdValue == VendorRunId
            ? VendorActionStepId
            : Guid.Parse($"{runIdValue:N}"[..20] + "aaaaaaaaaaaa");

    private static IReadOnlyList<ProcessTimelineEventProjection> CreateEvents(
        DateTimeOffset now,
        IReadOnlyList<ProcessLiveProcessSnapshot> runs)
        => runs
            .SelectMany(run => run.RecentEvents.Select(item => new ProcessTimelineEventProjection(
                item.EventId,
                item.GlobalSequence,
                item.RootRunId,
                item.RunId,
                item.EventType,
                item.OccurredAtUtc,
                item.Sensitivity,
                item.Summary,
                item.RestrictedDiagnosticReference)))
            .Concat(
            [
                CreateTimelineEvent(now, 900, runs[2].RunId, "ManagerIncidentRaised", "Verification manager raised an operator decision for vendor onboarding.", 8),
                CreateTimelineEvent(now, 901, runs[3].RunId, "ChildRunWaiting", "Order fulfillment is waiting for warehouse reservation subprocess.", 5),
                CreateTimelineEvent(now, 902, runs[5].RunId, "ProcessRunCompleted", "Statement generation completed and final artifacts were accepted.", 21)
            ])
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToArray();

    private static IReadOnlyList<ProcessManagerMessageProjection> CreateManagerMessages(
        DateTimeOffset now,
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        IReadOnlySet<Guid> resolvedOperatorActionStepIds)
    {
        var messages = new List<ProcessManagerMessageProjection>();

        messages.Add(resolvedOperatorActionStepIds.Contains(VendorActionStepId)
            ? new ProcessManagerMessageProjection(
                "mock-manager-vendor-resolved",
                runs[2].RunId,
                runs[2].RunId,
                "Manager Note",
                "External verification rework was approved and the run returned to the dispatch queue.",
                now.AddMinutes(-1),
                ProcessProjectedSensitivity.Normal,
                RestrictedDiagnosticReference: null)
            : new ProcessManagerMessageProjection(
                "mock-manager-vendor",
                runs[2].RunId,
                runs[2].RunId,
                "Manager Incident Raised",
                "Vendor onboarding is blocked until external verification is retried with the alternate registry source.",
                now.AddMinutes(-8),
                ProcessProjectedSensitivity.Normal,
                RestrictedDiagnosticReference: null));

        messages.Add(
            new ProcessManagerMessageProjection(
                "mock-manager-payment",
                runs[4].RunId,
                runs[4].RunId,
                "Runtime Failure",
                "Payment reconciliation failed on malformed settlement export; inspect parser output before rerun.",
                now.AddMinutes(-16),
                ProcessProjectedSensitivity.Normal,
                RestrictedDiagnosticReference: null));

        return messages;
    }

    private static IReadOnlyList<ProcessRuntimeActiveAgentProjection> CreateActiveAgents(
        DateTimeOffset now,
        IReadOnlyList<ProcessLiveProcessSnapshot> runs)
        =>
        [
            CreateAgent(now, runs[0], "Agent Alpha", ".NET Developer", "Validating customer records and preparing final data quality proof."),
            CreateAgent(now, runs[1], "Agent Beta", "Data extraction agent", "Extracting invoice totals and purchase-order references."),
            CreateAgent(now, runs[3], "Agent Delta", "Inventory coordinator", "Monitoring warehouse subprocess status and reservation timeout.")
        ];

    private static ProcessRuntimeActiveAgentProjection CreateAgent(
        DateTimeOffset now,
        ProcessLiveProcessSnapshot run,
        string name,
        string role,
        string summary)
        => new(
            run.RunId.Value,
            run.CurrentStep?.StepInstanceId ?? Guid.NewGuid(),
            $"Run {run.RunId.Value.ToString("N")[..8]}",
            run.CurrentStep?.StepKey ?? "current-step",
            role.Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant(),
            "agent",
            name.Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant(),
            name,
            "Running",
            IsWorking: true,
            IsLeaseExpired: false,
            now.AddMinutes(-1),
            now.AddMinutes(-12),
            now.AddMinutes(18),
            summary)
        {
            AgentName = name,
            ProviderName = "Mock provider",
            Model = "mock-live-processes",
            ExecutionState = "Running",
            CurrentActivity = summary,
            ObservationSource = "Mock projection scenario",
            RecentActivities =
            [
                new ProcessRuntimeActiveAgentActivityProjection(now.AddMinutes(-3), "Running", "Tool use", summary)
            ],
            RecentTools =
            [
                new ProcessRuntimeActiveAgentToolProjection("browser_snapshot", "browser", "Inspect current page.", "Snapshot captured.", now.AddMinutes(-6), now.AddMinutes(-5)),
                new ProcessRuntimeActiveAgentToolProjection("dotnet test", "shell", "Run focused tests.", "Tests passed.", now.AddMinutes(-4), now.AddMinutes(-3))
            ]
        };

    private static IReadOnlyList<ProcessRuntimeToolUsageProjection> CreateToolUsage(DateTimeOffset now)
        =>
        [
            new("browser_snapshot", 34, now.AddMinutes(-2), "Browser snapshots and screenshots for UI verification."),
            new("playwright screenshot", 18, now.AddMinutes(-4), "Large-screen visual proof captures."),
            new("dotnet build", 11, now.AddMinutes(-6), ".NET build validation."),
            new("dotnet test", 16, now.AddMinutes(-7), "Focused unit/component/integration tests."),
            new("project structure update", 9, now.AddMinutes(-9), "Project-structure evidence and summary updates."),
            new("workspace file edit", 27, now.AddMinutes(-3), "Source and proof file updates."),
            new("artifact write", 12, now.AddMinutes(-5), "Screenshots and reports written to artifacts."),
            new("agent message", 8, now.AddMinutes(-12), "Manager and role coordination messages.")
        ];

    private static IReadOnlyList<ProcessRuntimeMetricPointProjection> CreateMetricPoints(DateTimeOffset now)
        => Enumerable.Range(0, 18)
            .Select(index => new ProcessRuntimeMetricPointProjection(
                now.AddMinutes(-90 + (index * 5)),
                EventCount: 2 + (index % 4),
                ManagerEventCount: index is 7 or 13 ? 1 : 0,
                ToolCallCount: 7 + (index % 5),
                DurationMs: 60_000 + (index * 3_000),
                InputTokens: 4_000 + (index * 180),
                CachedInputTokens: 1_200,
                OutputTokens: 700 + (index * 40),
                TotalTokens: 4_900 + (index * 230),
                EstimatedCost: 0.35m + (index * 0.02m),
                ActualCost: 0.28m + (index * 0.015m)))
            .ToArray();

    private static ProcessLiveRunEventProjection CreateEvent(
        DateTimeOffset now,
        long sequence,
        ProcessRunId runId,
        string eventType,
        string summary,
        int minutesAgo)
        => new(
            RuntimeEventId.New(),
            sequence,
            runId,
            runId,
            eventType,
            now.AddMinutes(-minutesAgo),
            ProcessProjectedSensitivity.Normal,
            summary,
            RestrictedDiagnosticReference: null);

    private static ProcessTimelineEventProjection CreateTimelineEvent(
        DateTimeOffset now,
        long sequence,
        ProcessRunId runId,
        string eventType,
        string summary,
        int minutesAgo)
        => new(
            RuntimeEventId.New(),
            sequence,
            runId,
            runId,
            eventType,
            now.AddMinutes(-minutesAgo),
            ProcessProjectedSensitivity.Normal,
            summary,
            RestrictedDiagnosticReference: null);

    private static bool RequiresAttention(ProcessLiveProcessSnapshot run)
        => run.Status is ProcessProjectedRunStatus.NeedsAttention or ProcessProjectedRunStatus.Failed ||
           run.OperatorActions.Any(action => action.IsEnabled) ||
           run.Incidents.Count > 0;
}
