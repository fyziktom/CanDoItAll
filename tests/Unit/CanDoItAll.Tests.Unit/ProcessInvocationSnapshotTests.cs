using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes.AgentChat;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessInvocationSnapshotTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid SelectedRunId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Capture_is_bounded_redacted_immutable_and_preserves_typed_provenance()
    {
        var sourceRuns = CreateRuns(
            ProcessInvocationSnapshotMapper.MaximumCapturedRunCount + 8,
            ProcessInvocationSnapshotMapper.MaximumRecentEventsPerRun + 2);
        var sourceAgents = CreateAgents(
            ProcessInvocationSnapshotMapper.MaximumCapturedActiveAgentCount + 8);
        var selectionFingerprint = ProcessProjectionContentFingerprintFactory.Create(
            ProcessWorkspaceProvenanceComponent.Selection,
            new { SelectedRunId });
        var provenance = CreatePresentProvenance() with
        {
            Selection = ProcessProjectionComponentProvenance.Present(
                ProcessProjectionComponentSource.Request,
                selectionFingerprint)
        };
        var shell = CreateShell(sourceRuns, sourceAgents, provenance);
        var publication = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(shell),
            new DatabaseProfileGeneration(7),
            Now,
            previousPublication: null);
        var capture = Assert.IsType<ProcessInvocationSnapshotCapture>(
            publication.SnapshotCapture);
        var snapshot = capture.Snapshot;

        sourceRuns.Clear();
        sourceAgents.Clear();

        Assert.Equal(ProcessInvocationSnapshotMapper.MaximumCapturedRunCount, snapshot.Runs.Length);
        Assert.Equal(
            ProcessInvocationSnapshotMapper.MaximumCapturedActiveAgentCount,
            snapshot.ActiveAgents.Length);
        Assert.All(
            snapshot.Runs,
            run => Assert.InRange(
                run.RecentEvents.Length,
                0,
                ProcessInvocationSnapshotMapper.MaximumRecentEventsPerRun));
        Assert.Equal(
            ProcessInvocationSnapshotMapper.MaximumCapturedRunCount + 8,
            snapshot.Coverage.SourceRunCount);
        Assert.False(snapshot.Coverage.HasCompleteRuns);
        Assert.False(snapshot.Coverage.HasCompleteEvents);
        Assert.False(snapshot.Coverage.HasCompleteAgents);
        Assert.True(snapshot.Coverage.RedactedValueCount > 0);
        Assert.DoesNotContain(
            "super-secret",
            capture.AttachmentDraft.ContentFingerprint.Value,
            StringComparison.OrdinalIgnoreCase);
        var serializedFragment = Assert.Single(publication.ContributorPublications)
            .Fragment
            .Content;
        Assert.DoesNotContain("super-secret", serializedFragment, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\private", serializedFragment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", serializedFragment, StringComparison.Ordinal);
        Assert.Contains("[PATH OMITTED]", serializedFragment, StringComparison.Ordinal);
        Assert.Equal(
            selectionFingerprint,
            snapshot.Provenance
                .Single(item => item.Component == ProcessWorkspaceProvenanceComponent.Selection)
                .ContentFingerprint);
        Assert.Equal(
            Enum.GetValues<ProcessWorkspaceProvenanceComponent>().Length,
            snapshot.Provenance.Length);
        Assert.Contains(
            ProcessInvocationSnapshotOmission.Diagnostics,
            snapshot.Coverage.Omissions);
        Assert.Contains(
            ProcessInvocationSnapshotOmission.ResultLineage,
            snapshot.Coverage.Omissions);
        Assert.Contains(
            ProcessInvocationSnapshotOmission.ArtifactPaths,
            snapshot.Coverage.Omissions);
    }

    [Fact]
    public void Equivalent_publication_reuses_before_deadline_expires_with_source_and_renews_only_after_refresh()
    {
        var sourceRuns = CreateRuns(
            ProcessInvocationSnapshotMapper.MaximumCapturedRunCount,
            ProcessInvocationSnapshotMapper.MaximumRecentEventsPerRun,
            useMaximumText: true);
        var sourceAgents = CreateAgents(
            ProcessInvocationSnapshotMapper.MaximumCapturedActiveAgentCount,
            useMaximumText: true);
        var shell = CreateShell(
            sourceRuns,
            sourceAgents,
            CreatePresentProvenance());
        var first = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(shell),
            new DatabaseProfileGeneration(3),
            Now,
            previousPublication: null);
        var second = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(shell),
            new DatabaseProfileGeneration(3),
            Now.AddMinutes(1),
            first);
        var third = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(shell),
            new DatabaseProfileGeneration(3),
            Now.Add(ProcessInvocationSnapshotMapper.FreshnessLifetime),
            second);
        var refreshedAtUtc = Now.Add(ProcessInvocationSnapshotMapper.FreshnessLifetime);
        var refreshedShell = CreateShell(
            sourceRuns,
            sourceAgents,
            CreatePresentProvenance(refreshedAtUtc, sourceGlobalSequence: 2),
            refreshedAtUtc,
            sourceGlobalSequence: 2);
        var fourth = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(refreshedShell),
            new DatabaseProfileGeneration(3),
            refreshedAtUtc,
            third);
        var firstCapture = Assert.IsType<ProcessInvocationSnapshotCapture>(first.SnapshotCapture);
        var secondCapture = Assert.IsType<ProcessInvocationSnapshotCapture>(second.SnapshotCapture);
        var fourthCapture = Assert.IsType<ProcessInvocationSnapshotCapture>(fourth.SnapshotCapture);
        var fragment = Assert.Single(second.ContributorPublications).Fragment.Content;

        Assert.Same(firstCapture, secondCapture);
        Assert.Equal(
            firstCapture.AttachmentDraft.CapturedAtUtc,
            secondCapture.AttachmentDraft.CapturedAtUtc);
        Assert.Null(third.SnapshotCapture);
        Assert.Empty(Assert.Single(third.ContributorPublications).AttachmentDrafts);
        Assert.NotSame(secondCapture, fourthCapture);
        Assert.Equal(
            refreshedAtUtc,
            fourthCapture.AttachmentDraft.CapturedAtUtc);
        Assert.Equal(
            refreshedAtUtc.Add(ProcessInvocationSnapshotMapper.FreshnessLifetime),
            fourthCapture.AttachmentDraft.FreshUntilUtc);
        Assert.InRange(
            fragment.Length,
            1,
            AgentChatContextFragment.MaximumContentLength);
    }

    [Fact]
    public void Future_or_invalid_shell_refresh_metadata_is_not_published()
    {
        var runs = CreateRuns(1, 1);
        var agents = CreateAgents(1);
        var futureShell = CreateShell(
            runs,
            agents,
            CreatePresentProvenance(),
            Now.AddSeconds(1));
        var invalidBacklogShell = CreateShell(
            runs,
            agents,
            CreatePresentProvenance(),
            Now,
            backlogEventCount: -1);

        var future = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(futureShell),
            new DatabaseProfileGeneration(5),
            Now,
            previousPublication: null);
        var invalidBacklog = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(invalidBacklogShell),
            new DatabaseProfileGeneration(5),
            Now,
            previousPublication: null);

        Assert.Null(future.SnapshotCapture);
        Assert.Empty(Assert.Single(future.ContributorPublications).AttachmentDrafts);
        Assert.Null(invalidBacklog.SnapshotCapture);
        Assert.Empty(Assert.Single(invalidBacklog.ContributorPublications).AttachmentDrafts);
    }

    [Fact]
    public void Workspace_and_live_surfaces_copy_the_same_held_runtime_snapshot()
    {
        var runs = CreateRuns(2, 2);
        var agents = CreateAgents(2);
        var shell = CreateShell(runs, agents, CreatePresentProvenance());
        var workspace = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(shell),
            new DatabaseProfileGeneration(4),
            Now,
            previousPublication: null);
        var live = ProcessInvocationSnapshotMapper.BuildLivePublication(
            new LiveProcessesAgentChatContext(
                "https://localhost/processes/live",
                ProjectId: null,
                ProcessAgentChatLiveView.Activity,
                SelectedRunId,
                ProcessRuntimeHistoryWindow.OneDay,
                StatusFilter: null,
                shell,
                FocusedRun: null,
                FilesRunId: null,
                FocusedAgent: null,
                AgentChatContextAccessState.Ready),
            new DatabaseProfileGeneration(4),
            Now,
            previousPublication: null);
        var workspaceSnapshot = Assert.IsType<ProcessInvocationSnapshotCapture>(
            workspace.SnapshotCapture).Snapshot;
        var liveSnapshot = Assert.IsType<ProcessInvocationSnapshotCapture>(
            live.SnapshotCapture).Snapshot;

        Assert.Equal(
            workspaceSnapshot.Runs.Select(run => run.RunId),
            liveSnapshot.Runs.Select(run => run.RunId));
        Assert.Equal(workspaceSnapshot.Usage, liveSnapshot.Usage);
        Assert.Equal(
            workspaceSnapshot.Provenance.ToArray(),
            liveSnapshot.Provenance.ToArray());
        Assert.Equal(workspaceSnapshot.SelectedRunId, liveSnapshot.SelectedRunId);
    }

    [Fact]
    public void Non_present_provenance_suppresses_residual_component_data()
    {
        var shell = CreateShell(
            CreateRuns(2, 2),
            CreateAgents(2),
            ProcessWorkspaceProvenanceVector.Empty);

        var publication = ProcessInvocationSnapshotMapper.BuildWorkspacePublication(
            CreateWorkspaceContext(shell),
            new DatabaseProfileGeneration(9),
            Now,
            previousPublication: null);
        var snapshot = Assert.IsType<ProcessInvocationSnapshotCapture>(
            publication.SnapshotCapture).Snapshot;
        var fragment = Assert.Single(publication.ContributorPublications).Fragment.Content;

        Assert.Equal("unavailable", snapshot.View);
        Assert.Equal("/", snapshot.Route);
        Assert.Null(snapshot.ProjectId);
        Assert.Null(snapshot.HistoryWindow);
        Assert.Null(snapshot.SelectedRunId);
        Assert.Null(snapshot.SelectedDefinition);
        Assert.Empty(snapshot.Runs);
        Assert.Null(snapshot.SelectedRunDetail);
        Assert.Null(snapshot.FocusedEvent);
        Assert.Null(snapshot.FocusedAgent);
        Assert.Empty(snapshot.ActiveAgents);
        Assert.Null(snapshot.Usage);
        Assert.Equal(string.Empty, snapshot.AttentionSummary);
        Assert.Null(publication.Surface.Position.PrimarySelection);
        Assert.DoesNotContain(
            publication.Surface.Position.Facts,
            fact => fact.Name is
                "definition-count" or
                "loaded-run-count" or
                "run-status" or
                "runtime-history");
        Assert.DoesNotContain("super-secret", fragment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("notRequested", fragment, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessWorkspaceAgentChatContext CreateWorkspaceContext(
        ProcessWorkspaceShellProjection shell)
        => new(
            "https://localhost/processes",
            ProjectId: null,
            ProcessAgentChatWorkspaceView.ManagerChat,
            ProcessAgentChatRunView.Activity,
            SelectedRunId,
            shell,
            FocusedRun: null,
            FocusedEvent: null,
            AgentChatContextAccessState.Ready);

    private static List<ProcessLiveProcessSnapshot> CreateRuns(
        int count,
        int eventCount,
        bool useMaximumText = false)
    {
        var text = useMaximumText
            ? new string('x', 1_000)
            : @"token=super-secret C:\private\runtime\artifact.json";
        var runs = new List<ProcessLiveProcessSnapshot>(count);
        for (var runIndex = 0; runIndex < count; runIndex++)
        {
            var runId = runIndex == 0 ? SelectedRunId : GuidFromInt(runIndex + 100);
            var processRunId = new ProcessRunId(runId);
            var events = Enumerable.Range(0, eventCount)
                .Select(eventIndex => new ProcessLiveRunEventProjection(
                    new RuntimeEventId(GuidFromInt((runIndex * 100) + eventIndex + 1_000)),
                    GlobalSequence: (runIndex * 100) + eventIndex,
                    processRunId,
                    processRunId,
                    text,
                    Now.AddMinutes(-eventIndex),
                    ProcessProjectedSensitivity.Normal,
                    text,
                    RestrictedDiagnosticReference: @"C:\private\diagnostic.json"))
                .ToArray();
            runs.Add(new ProcessLiveProcessSnapshot(
                processRunId,
                processRunId,
                ProcessProjectedRunStatus.Active,
                IsActive: true,
                Now.AddHours(-1),
                Now,
                CreateFreshness(),
                events,
                Incidents: [])
            {
                ProjectName = text,
                ProcessName = text,
                ExecutableStepCount = 4,
                CompletedStepCount = 2,
                TerminalStepCount = 2,
                ProgressLabel = text,
                CurrentStep = new ProcessRuntimeCurrentStepProjection(
                    runId,
                    GuidFromInt(runIndex + 2_000),
                    text,
                    text,
                    text,
                    text,
                    text,
                    AttemptNumber: 1,
                    IsWorking: true,
                    IsLeaseExpired: false,
                    Now,
                    ClaimedAtUtc: null,
                    LeaseExpiresAtUtc: null,
                    text)
            });
        }

        return runs;
    }

    private static List<ProcessRuntimeActiveAgentProjection> CreateAgents(
        int count,
        bool useMaximumText = false)
    {
        var text = useMaximumText
            ? new string('y', 1_000)
            : "authorization=Bearer super-secret";
        return Enumerable.Range(0, count)
            .Select(index => new ProcessRuntimeActiveAgentProjection(
                index == 0 ? SelectedRunId : GuidFromInt(index + 3_000),
                GuidFromInt(index + 4_000),
                text,
                text,
                text,
                text,
                text,
                text,
                text,
                IsWorking: true,
                IsLeaseExpired: false,
                Now,
                ClaimedAtUtc: null,
                LeaseExpiresAtUtc: null,
                text)
            {
                AgentId = GuidFromInt(index + 5_000),
                ExecutionRunId = GuidFromInt(index + 6_000),
                AgentName = text,
                ProviderName = "provider-must-not-be-copied",
                Model = "model-must-not-be-copied",
                RecentActivities =
                [
                    new ProcessRuntimeActiveAgentActivityProjection(
                        Now,
                        "Running",
                        "Provider payload",
                        "payload-must-not-be-copied")
                ],
                Artifacts =
                [
                    new ProcessRuntimeActiveAgentArtifactProjection(
                        "result",
                        "artifact",
                        @"C:\private\artifact.txt",
                        "artifact-path-must-not-be-copied",
                        Now)
                ]
            })
            .ToList();
    }

    private static ProcessWorkspaceShellProjection CreateShell(
        IReadOnlyList<ProcessLiveProcessSnapshot> runs,
        IReadOnlyList<ProcessRuntimeActiveAgentProjection> agents,
        ProcessWorkspaceProvenanceVector provenance,
        DateTimeOffset? observedAtUtc = null,
        long sourceGlobalSequence = 1,
        int backlogEventCount = 0)
    {
        var normalizedObservedAtUtc = observedAtUtc ?? Now;
        var selectedDefinition = new ProcessDefinitionCatalogItemProjection(
            new ProcessDefinitionCatalogItemKey("token=super-secret"),
            ProcessDefinitionCatalogScopeKind.Global,
            @"C:\private\definition.json",
            "Summary must not be copied.",
            ProcessDefinitionCatalogItemStatus.Published,
            "high",
            "managed",
            Now,
            CompatibilityIssueCount: 0);
        var runtime = new ProcessRuntimeWorkspaceProjection(
            ProcessRuntimeHistoryWindow.OneDay,
            EventPage: 0,
            EventPageSize: 25,
            HasMoreEvents: false,
            SelectedRunId,
            SelectedRun: null,
            runs,
            Events: [],
            Incidents: [],
            ManagerMessages: [],
            agents,
            new ProcessRuntimeStatsProjection(
                ObservedRunCount: runs.Count,
                ActiveRunCount: runs.Count,
                AttentionRunCount: 0,
                FailedRunCount: 0,
                EventCount: runs.Sum(run => run.RecentEvents.Count),
                ManagerEventCount: 0,
                ToolCallCount: 0,
                DurationMs: 1_000,
                InputTokens: 10,
                CachedInputTokens: 2,
                OutputTokens: 4,
                TotalTokens: 14,
                EstimatedCost: 0.01m,
                ActualCost: 0.01m),
            MetricPoints: [],
            ToolUsage: [],
            CreateFreshness(
                normalizedObservedAtUtc,
                sourceGlobalSequence,
                latestKnownGlobalSequence: sourceGlobalSequence + backlogEventCount,
                lastProcessedGlobalSequence: sourceGlobalSequence,
                backlogEventCount: backlogEventCount),
            "Runtime summary.",
            @"token=super-secret C:\private\attention.txt")
        {
            Provenance = provenance
        };
        return new ProcessWorkspaceShellProjection(
            ProcessWorkspaceShellScope.Global,
            new ProcessWorkspaceSelectionProjection(
                ProcessId: null,
                RunId: SelectedRunId,
                LaunchPlanId: null),
            "Processes",
            "Runtime",
            new ProcessDefinitionCatalogProjection(
                PublishedDefinitionCount: 1,
                DraftDefinitionCount: 0,
                TemplateCompatibilityIssueCount: 0,
                Summary: "Definitions",
                SearchText: string.Empty,
                selectedDefinition.Key,
                ScopeGroups: [],
                Items: [selectedDefinition],
                selectedDefinition,
                SelectedEditor: null,
                LastCommandReceipt: null),
            new ProcessLiveRunSummaryProjection(
                runs.Count,
                AttentionRunCount: 0,
                FailedRunCount: 0,
                LastEventAtUtc: Now,
                "Runs"),
            new ProcessWorkspaceProjectionRefreshProjection(
                ProcessWorkspaceProjectionStatus.Ready,
                normalizedObservedAtUtc,
                sourceGlobalSequence,
                backlogEventCount,
                "Ready"),
            new ProcessWorkspaceAuthorizationProjection(
                CanReadDefinitions: true,
                CanRefreshProjections: true,
                CanOpenAgentContext: true,
                CanEditDefinitions: false,
                CanLaunchRuns: false),
            Tabs: [],
            Commands: [],
            new ProcessWorkspaceAgentEntryProjection(
                ProcessWorkspaceAgentEntryKind.WorkspaceContext,
                IsAvailable: true,
                "Agent context",
                "processes:workspace",
                DisabledReason: null))
        {
            Runtime = runtime,
            Provenance = provenance
        };
    }

    private static ProcessProjectionFreshness CreateFreshness(
        DateTimeOffset? observedAtUtc = null,
        long sourceGlobalSequence = 1,
        long latestKnownGlobalSequence = 1,
        long lastProcessedGlobalSequence = 1,
        int backlogEventCount = 0)
        => new(
            observedAtUtc ?? Now,
            sourceGlobalSequence,
            new ProcessProjectionLag(
                latestKnownGlobalSequence,
                lastProcessedGlobalSequence,
                backlogEventCount));

    private static ProcessWorkspaceProvenanceVector CreatePresentProvenance(
        DateTimeOffset? observedAtUtc = null,
        long sourceGlobalSequence = 1)
    {
        ProcessProjectionComponentProvenance Present(
            ProcessWorkspaceProvenanceComponent component)
            => ProcessProjectionComponentProvenance.Present(
                ProcessProjectionComponentSource.ShellProjection,
                ProcessProjectionContentFingerprintFactory.Create(
                    component,
                    new
                    {
                        Component = component.ToString(),
                        SourceSequence = sourceGlobalSequence
                    }),
                CreateFreshness(
                    observedAtUtc,
                    sourceGlobalSequence,
                    sourceGlobalSequence,
                    sourceGlobalSequence));

        return new ProcessWorkspaceProvenanceVector(
            Present(ProcessWorkspaceProvenanceComponent.Selection),
            Present(ProcessWorkspaceProvenanceComponent.ShellRefresh),
            Present(ProcessWorkspaceProvenanceComponent.DefinitionCatalog),
            Present(ProcessWorkspaceProvenanceComponent.LiveRunSummary),
            Present(ProcessWorkspaceProvenanceComponent.LiveRuns),
            Present(ProcessWorkspaceProvenanceComponent.SelectedRunDetail),
            Present(ProcessWorkspaceProvenanceComponent.SelectedRunRecord),
            Present(ProcessWorkspaceProvenanceComponent.HistoryPage),
            Present(ProcessWorkspaceProvenanceComponent.MetricHistory),
            Present(ProcessWorkspaceProvenanceComponent.ActiveAgents),
            Present(ProcessWorkspaceProvenanceComponent.UsageTelemetry),
            Present(ProcessWorkspaceProvenanceComponent.DerivedProjection));
    }

    private static Guid GuidFromInt(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }
}
