using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkspaceAgentRecruitingTargetResolverTests
{
    private static readonly Guid CandidateAgentId =
        Guid.Parse("61485266-722b-4267-a672-62014f09837c");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 10, 30, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> TargetStateCases()
    {
        foreach (var state in Enum.GetValues<ExecutionState>())
        {
            yield return
            [
                AgentRecruitingTargetKind.AgentExecutionRun,
                state.ToString(),
                state is ExecutionState.Completed or ExecutionState.Failed
            ];
        }

        foreach (var state in Enum.GetValues<WorkflowRunState>())
        {
            yield return
            [
                AgentRecruitingTargetKind.WorkflowRun,
                state.ToString(),
                state is WorkflowRunState.Completed
                    or WorkflowRunState.Failed
                    or WorkflowRunState.Cancelled
            ];
        }

        foreach (var status in Enum.GetValues<ProcessProjectedRunStatus>())
        {
            yield return
            [
                AgentRecruitingTargetKind.ProcessRun,
                status.ToString(),
                status is ProcessProjectedRunStatus.Completed
                    or ProcessProjectedRunStatus.Failed
                    or ProcessProjectedRunStatus.Cancelled
            ];
        }
    }

    [Theory]
    [MemberData(nameof(TargetStateCases))]
    public async Task Resolve_existing_target_preserves_state_and_terminal_semantics(
        AgentRecruitingTargetKind kind,
        string state,
        bool expectedTerminal)
    {
        var fixture = new ResolverFixture();
        var targetId = Guid.NewGuid();
        await fixture.SeedAsync(kind, targetId, state);

        var resolution = await fixture.Resolver.ResolveAsync(
            new AgentRecruitingExecutionTarget(kind, targetId));

        Assert.True(resolution.Found);
        Assert.Equal(state, resolution.State);
        Assert.Equal(expectedTerminal, resolution.IsTerminal);
        Assert.Contains(CandidateAgentId, resolution.ParticipatingAgentIds ?? []);
    }

    [Theory]
    [InlineData(AgentRecruitingTargetKind.AgentExecutionRun)]
    [InlineData(AgentRecruitingTargetKind.WorkflowRun)]
    [InlineData(AgentRecruitingTargetKind.ProcessRun)]
    public async Task Resolve_missing_target_returns_scoped_not_found(
        AgentRecruitingTargetKind kind)
    {
        var fixture = new ResolverFixture();

        var resolution = await fixture.Resolver.ResolveAsync(
            new AgentRecruitingExecutionTarget(kind, Guid.NewGuid()));

        Assert.False(resolution.Found);
        Assert.Equal("not-found", resolution.State);
        Assert.False(resolution.IsTerminal);
        Assert.Null(resolution.ParticipatingAgentIds);
    }

    [Fact]
    public async Task Resolve_does_not_leak_targets_between_workspace_backing_stores()
    {
        var firstWorkspace = new ResolverFixture();
        var secondWorkspace = new ResolverFixture();
        var targets = new[]
        {
            (
                new AgentRecruitingExecutionTarget(
                    AgentRecruitingTargetKind.AgentExecutionRun,
                    Guid.NewGuid()),
                nameof(ExecutionState.Completed)),
            (
                new AgentRecruitingExecutionTarget(
                    AgentRecruitingTargetKind.WorkflowRun,
                    Guid.NewGuid()),
                nameof(WorkflowRunState.Cancelled)),
            (
                new AgentRecruitingExecutionTarget(
                    AgentRecruitingTargetKind.ProcessRun,
                    Guid.NewGuid()),
                nameof(ProcessProjectedRunStatus.Failed))
        };
        foreach (var (target, state) in targets)
        {
            await firstWorkspace.SeedAsync(target.Kind, target.Id, state);
        }

        foreach (var (target, expectedState) in targets)
        {
            var visible = await firstWorkspace.Resolver.ResolveAsync(target);
            var hidden = await secondWorkspace.Resolver.ResolveAsync(target);

            Assert.True(visible.Found);
            Assert.Equal(expectedState, visible.State);
            Assert.True(visible.IsTerminal);
            Assert.False(hidden.Found);
            Assert.Equal("not-found", hidden.State);
            Assert.False(hidden.IsTerminal);
            Assert.Null(hidden.ParticipatingAgentIds);
        }
    }

    [Fact]
    public async Task Resolve_workflow_does_not_treat_unexecuted_agent_node_as_participation()
    {
        var fixture = new ResolverFixture();
        var targetId = Guid.NewGuid();
        await fixture.SeedWorkflowAsync(
            targetId,
            WorkflowRunState.Failed,
            candidateExecuted: false);

        var resolution = await fixture.Resolver.ResolveAsync(
            new AgentRecruitingExecutionTarget(
                AgentRecruitingTargetKind.WorkflowRun,
                targetId));

        Assert.True(resolution.Found);
        Assert.True(resolution.IsTerminal);
        Assert.Empty(resolution.ParticipatingAgentIds ?? []);
    }

    private sealed class ResolverFixture
    {
        private readonly RecordingExecutionRunStore executionRuns = new();
        private readonly InMemoryWorkflowRunStore workflowRuns = new();
        private readonly RecordingWorkflowCatalogService workflowCatalog = new();
        private readonly InMemoryProcessProjectionStore processProjections = new();
        private readonly RecordingProcessRunRecordReader processRecords = new();

        public ResolverFixture()
        {
            var processQueries = new ProcessRuntimeProjectionQueryService(
                processProjections,
                ProcessProjectionJsonCodec.Default,
                new FixedProjectionClock());
            Resolver = new WorkspaceAgentRecruitingTargetResolver(
                executionRuns,
                workflowRuns,
                workflowCatalog,
                processQueries,
                processRecords);
        }

        public WorkspaceAgentRecruitingTargetResolver Resolver { get; }

        public async Task SeedAsync(
            AgentRecruitingTargetKind kind,
            Guid targetId,
            string state)
        {
            switch (kind)
            {
                case AgentRecruitingTargetKind.AgentExecutionRun:
                    executionRuns.Add(CreateExecutionRun(
                        targetId,
                        Enum.Parse<ExecutionState>(state)));
                    break;
                case AgentRecruitingTargetKind.WorkflowRun:
                    await SeedWorkflowAsync(
                        targetId,
                        Enum.Parse<WorkflowRunState>(state),
                        candidateExecuted: true);
                    break;
                case AgentRecruitingTargetKind.ProcessRun:
                    await processProjections.UpsertSnapshotAsync(
                        CreateProcessSnapshot(
                            targetId,
                            Enum.Parse<ProcessProjectedRunStatus>(state)));
                    processRecords.Add(targetId, CandidateAgentId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public async Task SeedWorkflowAsync(
            Guid targetId,
            WorkflowRunState state,
            bool candidateExecuted)
        {
            var workflowRun = CreateWorkflowRun(targetId, state);
            await workflowRuns.SaveRunAsync(workflowRun);
            workflowCatalog.Add(CreateWorkflowDefinition(workflowRun));
            if (!candidateExecuted)
            {
                return;
            }

            await workflowRuns.SaveEventAsync(new WorkflowEventRecord(
                Guid.NewGuid(),
                workflowRun.RunId,
                WorkflowEventKind.ExecutorInvoked,
                new WorkflowNodeId("candidate"),
                "Candidate node started.",
                "{}",
                Now));
        }
    }

    private static ExecutionRunRecord CreateExecutionRun(
        Guid runId,
        ExecutionState state)
        => new(
            Id: runId,
            AgentId: CandidateAgentId,
            ChatSessionId: null,
            Title: "Recruiting execution evidence",
            SourceKind: "test",
            SourceId: "resolver",
            CorrelationId: $"correlation-{runId:N}",
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Input",
            ResultSummary: "Result",
            ProviderName: "Test",
            Model: "test-model",
            State: state,
            Outcome: state switch
            {
                ExecutionState.Completed => RunOutcome.Succeeded,
                ExecutionState.Failed => RunOutcome.Failed,
                _ => null
            },
            CreatedAtUtc: Now,
            UpdatedAtUtc: Now,
            StartedAtUtc: state == ExecutionState.Idle ? null : Now,
            CompletedAtUtc: state is ExecutionState.Completed or ExecutionState.Failed
                ? Now
                : null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);

    private static WorkflowRunSnapshot CreateWorkflowRun(
        Guid runId,
        WorkflowRunState state)
        => new(
            new WorkflowRunId(runId),
            new WorkflowId(Guid.NewGuid()),
            new WorkflowVersionId(Guid.NewGuid()),
            state,
            WorkflowRuntimeBackendKind.InProcess,
            $"backend-{runId:N}",
            "Recruiting workflow evidence",
            Now,
            Now)
        {
            TerminalAtUtc = state is WorkflowRunState.Completed
                or WorkflowRunState.Failed
                or WorkflowRunState.Cancelled
                ? Now
                : null
        };

    private static WorkflowDefinitionDetail CreateWorkflowDefinition(
        WorkflowRunSnapshot run)
    {
        var nodeId = new WorkflowNodeId("candidate");
        var node = new WorkflowNode(
            nodeId,
            WorkflowNodeKind.AgentStep,
            "Candidate",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: CandidateAgentId,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Complete the recruiting assessment.",
                InputShape: null,
                ResultShape: null));
        var definition = new WorkflowDefinition(
            run.WorkflowId,
            run.VersionId,
            "Recruiting assessment workflow",
            "Runs the candidate through a bounded assessment.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(nodeId, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            Now,
            Now);
        return new WorkflowDefinitionDetail(
            definition,
            WorkflowValidationResult.Success);
    }

    private static ProcessProjectionSnapshot CreateProcessSnapshot(
        Guid runId,
        ProcessProjectedRunStatus status)
    {
        var processRunId = new ProcessRunId(runId);
        var detail = new ProcessRunDetailProjection(
            processRunId,
            processRunId,
            status,
            Now,
            Now,
            new ProcessProjectionFreshness(
                Now,
                SourceGlobalSequence: 1,
                new ProcessProjectionLag(
                    LatestKnownGlobalSequence: 1,
                    LastProcessedGlobalSequence: 1,
                    BacklogEventCount: 0)),
            []);
        return ProcessProjectionJsonCodec.Default.CreateSnapshot(
            ProcessRuntimeProjectionProjector.ProjectorName,
            ProcessRuntimeProjectionKeys.RunDetail(processRunId),
            detail,
            Now);
    }

    private sealed class FixedProjectionClock : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingExecutionRunStore : ISandboxWorkspaceExecutionRunStore
    {
        private readonly Dictionary<Guid, ExecutionRunRecord> runs = [];

        public void Add(ExecutionRunRecord run) => runs.Add(run.Id, run);

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(runs.Values.ToList());

        public Task<ExecutionRunRecord?> GetExecutionRunAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(runs.GetValueOrDefault(executionRunId));

        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                runs.TryGetValue(executionRunId, out var run)
                    ? new ExecutionRunDetail(run, null, [], [])
                    : null);

        public Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(
            ExecutionRunDetail detail,
            CancellationToken cancellationToken = default)
        {
            runs[detail.Run.Id] = detail.Run;
            return Task.FromResult(detail);
        }
    }

    private sealed class RecordingWorkflowCatalogService : IWorkflowCatalogService
    {
        private readonly Dictionary<(WorkflowId, WorkflowVersionId), WorkflowDefinitionDetail>
            definitions = [];

        public void Add(WorkflowDefinitionDetail detail)
        {
            definitions[(detail.Definition.Id, detail.Definition.VersionId)] = detail;
        }

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var detail = versionId.HasValue
                ? definitions.GetValueOrDefault((workflowId, versionId.Value))
                : definitions.Values.FirstOrDefault(item => item.Definition.Id == workflowId);
            return Task.FromResult(detail);
        }

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> SaveDefinitionAsync(
            WorkflowDefinitionSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
            WorkflowDefinitionStatusChangeRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ImportDefinitionAsync(
            WorkflowDefinitionImportRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteDefinitionAsync(
            WorkflowId workflowId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowValidationResult> ValidateDefinitionAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingProcessRunRecordReader : IProcessRunRecordReader
    {
        private readonly Dictionary<ProcessRunId, ProcessRunRecordSummary> records = [];

        public void Add(Guid runId, Guid participantAgentId)
        {
            var processRunId = new ProcessRunId(runId);
            records[processRunId] = CreateProcessRunRecordSummary(
                processRunId,
                participantAgentId);
        }

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default)
        {
            var selected = query.RunIds
                .Select(runId => records.GetValueOrDefault(runId))
                .OfType<ProcessRunRecordSummary>()
                .Take(query.Take)
                .ToArray();
            return Task.FromResult(
                new ProcessRunRecordPage(selected, NextCursor: null));
        }
    }

    private static ProcessRunRecordSummary CreateProcessRunRecordSummary(
        ProcessRunId runId,
        Guid participantAgentId)
    {
        return new ProcessRunRecordSummary(
            new ProcessRunRecordIdentity(
                runId,
                runId,
                ParentRunId: null,
                PlanId: null,
                DefinitionId: null,
                DefinitionVersionId: null,
                ProjectId: null),
            ProcessRunDisposition.Succeeded,
            ProcessRunRecordLifecycleState.Current,
            ProcessRunRecordCompleteness.Complete,
            ProcessRunEvidenceSource.All,
            ProcessRunEvidenceSource.None,
            [],
            ProcessRunFactsStatus.Completed,
            FactsAttemptCount: 1,
            FactsNextAttemptAtUtc: null,
            FactsLastErrorClass: null,
            FactsLastErrorDiagnosticReference: null,
            ProcessRunNarrativeStatus.Pending,
            NarrativeAttemptCount: 0,
            NarrativeNextAttemptAtUtc: null,
            NarrativeLastErrorClass: null,
            NarrativeLastErrorDiagnosticReference: null,
            new ProcessRunRecordMetrics(
                StartedAtUtc: Now,
                EndedAtUtc: Now,
                DurationMilliseconds: 0,
                TotalStepCount: 1,
                ExecutableStepCount: 1,
                CompletedStepCount: 1,
                FailedStepCount: 0,
                CancelledStepCount: 0,
                RepetitionCount: 0,
                ExecutionCount: 1,
                ReworkCount: 0,
                IncidentCount: 0,
                EscalationCount: 0,
                InputTokenCount: 0,
                CachedInputTokenCount: 0,
                OutputTokenCount: 0,
                ReasoningTokenCount: 0,
                TotalTokenCount: 0,
                EstimatedCost: 0,
                ActualCost: 0,
                ToolCallCount: 0,
                ArtifactCount: 0,
                SubprocessCount: 0),
            [new ProcessRunParticipantId(participantAgentId.ToString("D"))],
            Narrative: null,
            SourceGlobalSequence: 1,
            SourceRootSequence: 1,
            ProcessRunRecordSchema.CurrentVersion,
            Now);
    }

    private sealed class InMemoryProcessProjectionStore : IProcessProjectionStore
    {
        private readonly Dictionary<
            (ProcessProjectorName Projector, ProcessProjectionKey Key),
            ProcessProjectionSnapshot> snapshots = [];

        public Task UpsertSnapshotAsync(
            ProcessProjectionSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            snapshots[(snapshot.ProjectorName, snapshot.ProjectionKey)] = snapshot;
            return Task.CompletedTask;
        }

        public Task<ProcessProjectionSnapshot?> LoadSnapshotAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKey projectionKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                snapshots.GetValueOrDefault((projectorName, projectionKey)));

        public Task<IReadOnlyList<ProcessProjectionSnapshot>> LoadSnapshotsAsync(
            ProcessProjectorName projectorName,
            IReadOnlyList<ProcessProjectionKey> projectionKeys,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionSnapshot>>(
                projectionKeys
                    .Select(projectionKey => snapshots.GetValueOrDefault((projectorName, projectionKey)))
                    .OfType<ProcessProjectionSnapshot>()
                    .ToArray());

        public Task<IReadOnlyList<ProcessProjectionSnapshot>> ReadSnapshotsAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionKeyPrefix projectionKeyPrefix,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionSnapshot>>([]);

        public Task AppendHistoryAsync(
            ProcessProjectionHistoryRecord history,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ProcessProjectionHistoryRecord>> ReadHistoryAsync(
            ProcessProjectionHistoryQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionHistoryRecord>>([]);

        public Task SaveOffsetAsync(
            ProcessProjectorOffset offset,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ProcessProjectorOffset?> LoadOffsetAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessProjectorOffset?>(null);

        public Task WriteDeadLetterAsync(
            ProcessProjectionDeadLetter deadLetter,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ProcessProjectionDeadLetter>> ReadDeadLettersAsync(
            ProcessProjectorName projectorName,
            ProcessProjectionShardKey shardKey,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessProjectionDeadLetter>>([]);
    }
}
