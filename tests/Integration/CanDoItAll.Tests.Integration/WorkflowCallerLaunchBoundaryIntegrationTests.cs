using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowCallerLaunchBoundaryIntegrationTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 12, 19, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task WorkflowApi_StartsExactAndLatestActiveWithAuthenticatedServerOriginAndRejectsSpoofedLineage()
    {
        var launchService = new RecordingLaunchService();
        var runtimeManager = new OutcomeRuntimeManager();
        await using var host = await CreateApiHostAsync(
            launchService,
            runtimeManager,
            jwtEnabled: true);
        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var token = tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = "workflow-api-user",
            DisplayName = "Workflow API User"
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var workflowId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var exactResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/runs/start",
            new
            {
                workflowId,
                versionId,
                inputJson = "{\"kind\":\"exact\"}",
                requestedBackend = WorkflowRuntimeBackendKind.InProcess
            });
        var latestResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/runs/start",
            new
            {
                workflowId,
                inputJson = "{\"kind\":\"latest\"}",
                requestedBackend = WorkflowRuntimeBackendKind.InProcess
            });
        var spoofedResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/runs/start",
            new
            {
                workflowId,
                versionId,
                sourceProcessRunId = Guid.NewGuid(),
                sourceProcessAssignmentId = Guid.NewGuid()
            });

        Assert.Equal(HttpStatusCode.OK, exactResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, latestResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, spoofedResponse.StatusCode);
        Assert.Collection(
            launchService.Intents,
            intent => Assert.Equal(
                new WorkflowDefinitionSelection.ExactSavedVersion(
                    new WorkflowId(workflowId),
                    new WorkflowVersionId(versionId)),
                Assert.IsType<WorkflowDefinitionSelection.ExactSavedVersion>(intent.Selection)),
            intent => Assert.Equal(
                new WorkflowDefinitionSelection.LatestActive(new WorkflowId(workflowId)),
                Assert.IsType<WorkflowDefinitionSelection.LatestActive>(intent.Selection)));
        Assert.All(launchService.Intents, intent =>
        {
            Assert.Equal(WorkflowLaunchMode.Production, intent.Mode);
            var origin = Assert.IsType<WorkflowLaunchOrigin.Api>(intent.Origin);
            Assert.Equal(WorkflowLaunchActorKind.User, origin.Actor.Kind);
            Assert.Equal("workflow-api-user", origin.Actor.SubjectId);
            Assert.False(string.IsNullOrWhiteSpace(origin.CorrelationId.Value));
        });
    }

    [Fact]
    public async Task WorkflowApi_MapsTypedCancellationAndExternalResponseOutcomesToHttpSemantics()
    {
        var launchService = new RecordingLaunchService();
        var runtimeManager = new OutcomeRuntimeManager();
        await using var host = await CreateApiHostAsync(
            launchService,
            runtimeManager,
            jwtEnabled: false);

        runtimeManager.CancellationResult = new WorkflowRunCancellationResult(
            WorkflowRunCancellationOutcome.NotFound,
            Run: null,
            "Run missing.");
        var cancellationNotFound = await host.Client.PostAsJsonAsync(
            $"/api/workflows/runs/{Guid.NewGuid():D}/cancel",
            new { });
        runtimeManager.CancellationResult = new WorkflowRunCancellationResult(
            WorkflowRunCancellationOutcome.BackendNotCancellable,
            Run: null,
            "Backend cannot cancel.");
        var cancellationUnsupported = await host.Client.PostAsJsonAsync(
            $"/api/workflows/runs/{Guid.NewGuid():D}/cancel",
            new { });
        runtimeManager.ExternalResponseResult = new WorkflowExternalResponseResult(
            WorkflowExternalResponseOutcome.AlreadyResponded,
            Run: null,
            Request: null,
            "Already responded.");
        var responseConflict = await host.Client.PostAsJsonAsync(
            $"/api/workflows/external-requests/{Guid.NewGuid():D}/response",
            new { responseJson = "{}" });
        runtimeManager.ExternalResponseResult = new WorkflowExternalResponseResult(
            WorkflowExternalResponseOutcome.BackendUnavailable,
            Run: null,
            Request: null,
            "Backend unavailable.");
        var responseUnavailable = await host.Client.PostAsJsonAsync(
            $"/api/workflows/external-requests/{Guid.NewGuid():D}/response",
            new { responseJson = "{}" });
        runtimeManager.ExternalResponseResult = new WorkflowExternalResponseResult(
            WorkflowExternalResponseOutcome.ResumeFailed,
            Run: null,
            Request: null,
            "Resume failed.");
        var responseFailed = await host.Client.PostAsJsonAsync(
            $"/api/workflows/external-requests/{Guid.NewGuid():D}/response",
            new { responseJson = "{}" });

        Assert.Equal(HttpStatusCode.NotFound, cancellationNotFound.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancellationUnsupported.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, responseConflict.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, responseUnavailable.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, responseFailed.StatusCode);
    }

    [Fact]
    public async Task SchedulerLauncher_UsesPersistedPlanRunLineageAndStableIdempotency()
    {
        var launchService = new RecordingLaunchService();
        var runtimeManager = new OutcomeRuntimeManager();
        var launcher = new SchedulerTargetLauncher(launchService, runtimeManager);
        var workflowId = WorkflowId.New();
        var workflowVersionId = WorkflowVersionId.New();
        var plan = new SchedulerPlan
        {
            Id = Guid.NewGuid(),
            TargetKind = SchedulerPlanTargetKind.Workflow,
            TargetId = workflowId.Value,
            TargetVersionId = workflowVersionId.Value,
            InputJson = "{\"scheduled\":true}"
        };
        var context = new SchedulerTargetLaunchContext(
            plan.Id,
            Guid.NewGuid(),
            new WorkflowSchedulerFireId(Guid.NewGuid()),
            FixedUtcNow,
            new WorkflowLaunchCorrelationId(Guid.NewGuid()));

        await launcher.LaunchAsync(plan, context);
        await launcher.LaunchAsync(plan, context);

        Assert.Equal(2, launchService.Intents.Count);
        Assert.All(launchService.Intents, intent =>
        {
            Assert.Equal(
                new WorkflowDefinitionSelection.ExactSavedVersion(workflowId, workflowVersionId),
                Assert.IsType<WorkflowDefinitionSelection.ExactSavedVersion>(intent.Selection));
            var origin = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(intent.Origin);
            Assert.Equal(context.PlanId, origin.PlanId);
            Assert.Equal(context.PlanRunId, origin.PlanRunId);
            Assert.Equal(context.SchedulerFireId, origin.FireId);
            Assert.Equal(context.FiredAtUtc, origin.FiredAtUtc);
            Assert.Equal(context.CorrelationId, origin.CorrelationId);
            var idempotency = Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(intent.Idempotency);
            Assert.Equal(context.IdempotencyKey, idempotency.Key);
        });
        Assert.Equal(
            Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(launchService.Intents[0].Idempotency).Key,
            Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(launchService.Intents[1].Idempotency).Key);
    }

    private static Task<ApiTestHost> CreateApiHostAsync(
        IWorkflowLaunchService launchService,
        IWorkflowRuntimeManager runtimeManager,
        bool jwtEnabled)
    {
        return ApiTestHost.CreateAsync(
            jwtEnabled,
            services =>
            {
                services.RemoveAll<IWorkflowLaunchService>();
                services.RemoveAll<IWorkflowRuntimeManager>();
                services.AddSingleton(launchService);
                services.AddSingleton(runtimeManager);
            },
            useInMemoryDatabase: true);
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
                WorkflowDefinitionSelection.ExactSavedVersion exact => CreateDefinition(
                    exact.WorkflowId,
                    exact.VersionId),
                WorkflowDefinitionSelection.LatestActive latest => CreateDefinition(
                    latest.WorkflowId,
                    WorkflowVersionId.New()),
                _ => throw new InvalidOperationException("Fixture supports saved selections only.")
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
                FixedUtcNow)
            {
                Origin = intent.Origin,
                TerminalAtUtc = FixedUtcNow
            };
            var disposition = intent.Idempotency is WorkflowLaunchIdempotency.CallerSupplied
                ? WorkflowLaunchIdempotencyDisposition.EnforcedNewRun
                : WorkflowLaunchIdempotencyDisposition.NotRequested;
            return Task.FromResult(new WorkflowLaunchResult(run, resolved, disposition));
        }

        private static WorkflowDefinition CreateDefinition(
            WorkflowId workflowId,
            WorkflowVersionId versionId)
            => new(
                workflowId,
                versionId,
                "Caller integration fixture",
                "Workflow caller launch-boundary fixture.",
                WorkflowLifecycleStatus.Active,
                new WorkflowGraph(new WorkflowNodeId("start"), [], []),
                new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.InProcess,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: false,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false),
                FixedUtcNow,
                FixedUtcNow);
    }

    private sealed class OutcomeRuntimeManager : IWorkflowRuntimeManager
    {
        public WorkflowRunCancellationResult CancellationResult { get; set; } = new(
            WorkflowRunCancellationOutcome.CancellationRequested,
            Run: null,
            "Cancellation requested.");

        public WorkflowExternalResponseResult ExternalResponseResult { get; set; } = new(
            WorkflowExternalResponseOutcome.Accepted,
            Run: null,
            Request: null,
            "Response accepted.");

        public Task<WorkflowRunSnapshot> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("API and scheduler callers must use IWorkflowLaunchService.");

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
            => throw new InvalidOperationException("API cancellation must use the typed outcome method.");

        public Task<WorkflowRunCancellationResult> RequestCancellationAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CancellationResult);

        public Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("API responses must use the typed outcome method.");

        public Task<WorkflowExternalResponseResult> SubmitExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExternalResponseResult);
    }

}
