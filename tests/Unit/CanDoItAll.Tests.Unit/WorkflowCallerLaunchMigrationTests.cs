using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowCallerLaunchMigrationTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 12, 19, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TestRunner_DraftAndExactRuns_UsePreviewLaunchIntentWithoutDuplicateValidation()
    {
        var saved = CreateDefinition(WorkflowLifecycleStatus.Active);
        var draft = CreateDefinition(WorkflowLifecycleStatus.Draft);
        var catalog = new RecordingCatalog(saved);
        var launchService = new RecordingLaunchService();
        var runner = new WorkflowTestRunner(
            catalog,
            launchService,
            new QueryOnlyRuntimeManager(),
            new InMemoryWorkflowRunStore());

        var draftResult = await runner.RunAsync(new WorkflowTestRunRequest(
            WorkflowId: null,
            VersionId: null,
            DraftDefinition: draft,
            InputJson: "{\"draft\":true}",
            RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
            ValidateOnly: false));
        var exactResult = await runner.RunAsync(new WorkflowTestRunRequest(
            saved.Id,
            saved.VersionId,
            DraftDefinition: null,
            InputJson: "{\"saved\":true}",
            RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
            ValidateOnly: false));

        Assert.True(draftResult.Succeeded, draftResult.ErrorMessage);
        Assert.True(exactResult.Succeeded, exactResult.ErrorMessage);
        Assert.Collection(
            launchService.Intents,
            intent => Assert.IsType<WorkflowDefinitionSelection.DraftPreview>(intent.Selection),
            intent => Assert.Equal(
                new WorkflowDefinitionSelection.ExactSavedVersion(saved.Id, saved.VersionId),
                Assert.IsType<WorkflowDefinitionSelection.ExactSavedVersion>(intent.Selection)));
        Assert.All(launchService.Intents, intent =>
        {
            Assert.Equal(WorkflowLaunchMode.Preview, intent.Mode);
            var origin = Assert.IsType<WorkflowLaunchOrigin.Preview>(intent.Origin);
            Assert.Equal(WorkflowLaunchActorKind.Service, origin.Actor.Kind);
            Assert.Equal("workflow-test-runner", origin.Actor.SubjectId);
            Assert.False(string.IsNullOrWhiteSpace(origin.CorrelationId.Value));
        });
        Assert.Equal(0, catalog.GetDefinitionCalls);
        Assert.Equal(0, catalog.ValidateDefinitionCalls);
    }

    [Fact]
    public void ProductionCallers_DependsOnLaunchBoundaryAndDoNotConstructLegacyStartRequests()
    {
        var apiSource = ReadSource("src", "App", "CanDoItAll.Web", "Api", "WorkflowsApi.cs");
        var schedulerSource = ReadSource(
            "src",
            "Modules",
            "CanDoItAll.Modules.SchedulerPlanner",
            "SchedulerPlannerService.cs");
        var projectSource = ReadSource(
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureWorkflowNodeService.cs");
        var projectIntentFactorySource = ReadSource(
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureWorkflowLaunchIntentFactory.cs");

        Assert.Contains("IWorkflowLaunchService", apiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceProcessRunId", apiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceProcessAssignmentId", apiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("runtimeManager.StartAsync", apiSource, StringComparison.Ordinal);

        Assert.Contains("SchedulerTargetLaunchContext", schedulerSource, StringComparison.Ordinal);
        Assert.Contains("WorkflowLaunchOrigin.SchedulerPlanRun", schedulerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("workflowRuntimeManager.StartAsync", schedulerSource, StringComparison.Ordinal);

        Assert.Contains("launchIntentFactory.Create", projectSource, StringComparison.Ordinal);
        Assert.Contains("WorkflowLaunchOrigin.ProjectStructureNode", projectIntentFactorySource, StringComparison.Ordinal);
        Assert.DoesNotContain("workflowRuntimeManager.StartAsync", projectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var startingStatus = BuildStatus", projectSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectStructureIntentFactory_UsesRealLineageAndStableRetryKey()
    {
        var factory = new ProjectStructureWorkflowLaunchIntentFactory();
        var definition = CreateDefinition(WorkflowLifecycleStatus.Active);
        var projectId = Guid.NewGuid();
        var agent = new ProjectStructureAgentContext(
            "agent-42",
            "Agent 42",
            "build-host",
            @"C:\repositories\CanDoItAll",
            "tests/workflow-launch",
            "session-99");

        var first = factory.Create(
            definition,
            projectId,
            "workflow-node-7",
            agent,
            "{}",
            WorkflowRuntimeBackendKind.InProcess,
            WorkflowPreviewSimulationPlan.Empty,
            previousRunId: null);
        var retry = factory.Create(
            definition,
            projectId,
            "workflow-node-7",
            agent,
            "{}",
            WorkflowRuntimeBackendKind.InProcess,
            WorkflowPreviewSimulationPlan.Empty,
            previousRunId: null);
        var nextRun = factory.Create(
            definition,
            projectId,
            "workflow-node-7",
            agent,
            "{}",
            WorkflowRuntimeBackendKind.InProcess,
            WorkflowPreviewSimulationPlan.Empty,
            WorkflowRunId.New());

        Assert.Equal(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            Assert.IsType<WorkflowDefinitionSelection.ExactSavedVersion>(first.Selection));
        Assert.Equal(WorkflowLaunchMode.Production, first.Mode);
        var origin = Assert.IsType<WorkflowLaunchOrigin.ProjectStructureNode>(first.Origin);
        Assert.Equal(projectId, origin.ProjectId);
        Assert.Equal("workflow-node-7", origin.NodeId.Value);
        Assert.Equal(WorkflowLaunchActorKind.Agent, origin.RequestingActor.Kind);
        Assert.Equal(agent.AgentId, origin.RequestingActor.SubjectId);
        Assert.Equal(agent.SessionId, origin.SessionId.Value);
        var firstKey = Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(first.Idempotency).Key;
        var retryKey = Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(retry.Idempotency).Key;
        var nextRunKey = Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(nextRun.Idempotency).Key;
        Assert.Equal(firstKey, retryKey);
        Assert.Equal(first.Origin.CorrelationId, retry.Origin.CorrelationId);
        Assert.NotEqual(firstKey, nextRunKey);
    }

    private static WorkflowDefinition CreateDefinition(WorkflowLifecycleStatus status)
        => new(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Caller migration fixture",
            "Launch-boundary test definition.",
            status,
            new WorkflowGraph(new WorkflowNodeId("start"), [], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            FixedUtcNow,
            FixedUtcNow);

    private static string ReadSource(params string[] segments)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. segments]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate CanDoItAll.slnx from the test output directory.");
    }

    private sealed class RecordingLaunchService : IWorkflowLaunchService
    {
        public List<WorkflowLaunchIntent> Intents { get; } = [];

        public Task<WorkflowLaunchResult> LaunchAsync(
            WorkflowLaunchIntent intent,
            CancellationToken cancellationToken = default)
        {
            Intents.Add(intent);
            var definition = intent.Selection switch
            {
                WorkflowDefinitionSelection.DraftPreview draft => draft.Definition,
                WorkflowDefinitionSelection.ExactSavedVersion exact => CreateDefinition(WorkflowLifecycleStatus.Active) with
                {
                    Id = exact.WorkflowId,
                    VersionId = exact.VersionId
                },
                _ => throw new InvalidOperationException("Fixture supports draft and exact selections only.")
            };
            var backend = new WorkflowRuntimeBackendDescriptor(
                WorkflowRuntimeBackendKind.InProcess,
                "In-process",
                IsDurable: false,
                SupportsStreaming: true,
                SupportsExternalRequests: true,
                SupportsDashboardObservability: true,
                "Fixture backend.");
            var resolved = new WorkflowResolvedRuntimeRequest(
                definition,
                intent.InputJson,
                backend,
                intent.PreviewSimulationPlan,
                intent.Mode,
                intent.Origin,
                intent.CompletionPolicy,
                intent.Idempotency,
                FixedUtcNow);
            var run = new WorkflowRunSnapshot(
                WorkflowRunId.New(),
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                backend.Kind,
                "fixture-run",
                "Completed.",
                FixedUtcNow,
                FixedUtcNow);
            return Task.FromResult(new WorkflowLaunchResult(
                run,
                resolved,
                WorkflowLaunchIdempotencyDisposition.NotRequested));
        }
    }

    private sealed class RecordingCatalog(WorkflowDefinition definition) : IWorkflowCatalogService
    {
        public int GetDefinitionCalls { get; private set; }

        public int ValidateDefinitionCalls { get; private set; }

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowCatalogItem>>([]);

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            GetDefinitionCalls++;
            return Task.FromResult<WorkflowDefinitionDetail?>(
                workflowId == definition.Id && versionId == definition.VersionId
                    ? new WorkflowDefinitionDetail(definition, WorkflowValidationResult.Success)
                    : null);
        }

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowDefinitionDetail?>(null);

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
            WorkflowDefinition candidate,
            CancellationToken cancellationToken = default)
        {
            ValidateDefinitionCalls++;
            return Task.FromResult(WorkflowValidationResult.Success);
        }
    }

    private sealed class QueryOnlyRuntimeManager : IWorkflowRuntimeManager
    {
        public Task<WorkflowRunSnapshot> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("WorkflowTestRunner must start through IWorkflowLaunchService.");

        public Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowRunSnapshot?>(null);

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowRunSnapshot>>([]);

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowEventRecord>>([]);

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowCheckpointRecord>>([]);

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunSnapshot> CancelAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunCancellationResult> RequestCancellationAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseResult> SubmitExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
