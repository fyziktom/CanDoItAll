using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GenericMemorySourceScope = CanDoItAll.Memory.Abstractions.MemorySourceScope;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeSourceGatewayAdapterTests
{
    private static readonly Guid RunId = Guid.Parse("7b367e2d-62fa-4cfb-b6b5-7461b651ef70");
    private static readonly Guid RootRunId = Guid.Parse("642e3fe0-2d84-42da-aab8-eb62ebdc1f6a");
    private static readonly Guid PlanId = Guid.Parse("c1796469-6b40-48d5-ae78-43f44439e356");
    private static readonly Guid StepInstanceId = Guid.Parse("86dbf74a-5673-42c1-b36c-194d2f86f595");
    private static readonly Guid StepDefinitionId = Guid.Parse("4d9957d0-8ea3-4cad-a8bb-7dde7f7bc41a");
    private static readonly Guid ClaimToken = Guid.Parse("8c724d03-2b71-439f-b715-a454f2aa1c2c");
    private static readonly Guid ResultKey = Guid.Parse("47f67eed-1950-4cd9-a125-fc9a639b37c2");
    private static readonly Guid SlotId = Guid.Parse("9db5cab1-9189-42cb-875d-191f27769d47");
    private static readonly Guid ArtifactId = Guid.Parse("fbd0c15a-c7b8-4d3c-82bd-61f92b3e88b4");
    private static readonly Guid EventId = Guid.Parse("6bdfad75-e946-42cc-81c1-25b8ec2fe01b");
    private static readonly Guid LedgerEventId = Guid.Parse("1823da2d-c204-4688-8a42-f20068b23902");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T17:00:00Z");

    [Fact]
    public async Task Process_runtime_source_provider_exposes_run_step_agent_artifact_and_completion_context()
    {
        var services = new ServiceCollection();
        var executionObservationReader = new FakeProcessExecutionObservationReader();
        services.AddSingleton<IProcessExecutionObservationReader>(executionObservationReader);
        services.AddDbContextFactory<ProcessPersistenceDbContext>(options =>
            options.UseInMemoryDatabase($"process-source-{Guid.NewGuid():N}"));
        services.AddScoped<IProcessRuntimeEvidenceSourceProvider, ProcessRuntimeEvidenceSourceProvider>();
        using var serviceProvider = services.BuildServiceProvider();
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>();
        await SeedProcessRuntimeAsync(dbContextFactory);
        executionObservationReader.SetObservations(
        [
            new ProcessExecutionObservation(
                Guid.Parse("2b6878b1-84fb-492e-962f-4a71c5215418"),
                new ProcessRunId(RunId),
                new ProcessStepInstanceId(StepInstanceId),
                Guid.Parse("a2191831-aa2f-4790-a13d-70b35f140da9"),
                "Builder Agent",
                "OpenAI",
                "gpt-test",
                "Completed",
                "Succeeded",
                Now.AddMinutes(-8),
                Now.AddMinutes(-1),
                Now.AddMinutes(-7),
                Now.AddMinutes(-1),
                "Use runtime context token=agent-secret",
                "Created the requested artifact.",
                [new ProcessExecutionActivityObservation(Now.AddMinutes(-6), "Running", "tool", "Read token=activity-secret")],
                [new ProcessExecutionToolObservation("write_file", "workspace", "write secret path", "ok", Now.AddMinutes(-5), Now.AddMinutes(-4))],
                [new ProcessExecutionArtifactObservation("file", "Result", "artifacts/process-runs/result.md", "artifact token=artifact-secret", Now.AddMinutes(-3))],
                "")]
        );
        var provider = serviceProvider.GetRequiredService<IProcessRuntimeEvidenceSourceProvider>();

        var snapshot = await provider.ReadSnapshotAsync(new ProcessRuntimeEvidenceSourceRequest(RunId, Take: 100));

        Assert.Equal(MemorySourceKind.ProcessRuntime, snapshot.Manifest.SourceKind);
        Assert.Equal(RunId, snapshot.Manifest.ScopeId);
        Assert.Equal(MemorySourceSnapshotProviderVersions.ProcessRuntime, snapshot.Manifest.ProviderVersion);
        Assert.Contains(snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessDefinition);
        Assert.Contains(snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessRun);
        Assert.Contains(snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessStepEvidence);

        var assignment = Assert.Single(snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessRunAssignment);
        Assert.Equal(MemorySourceAccessMode.Redacted, assignment.Permission.AccessMode);
        Assert.Equal(MemorySourceHashClassification.RestrictedIntegrity, assignment.HashPolicy.Classification);
        Assert.Contains("[REDACTED]", assignment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("assignment-secret", assignment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-process-secret", assignment.Content, StringComparison.Ordinal);

        Assert.Contains(
            snapshot.Items,
            item => item.EntityKind == MemorySourceEntityKind.ProcessAgentSession &&
                    item.References.Any(reference =>
                        reference.ReferenceKind == "execution-run" &&
                        reference.ReferenceId == "2b6878b1-84fb-492e-962f-4a71c5215418"));
        var executionSession = snapshot.Items.Single(item =>
            item.EntityKind == MemorySourceEntityKind.ProcessAgentSession &&
            item.References.Any(reference => reference.ReferenceKind == "execution-run"));
        Assert.Equal(MemorySourceAccessMode.Redacted, executionSession.Permission.AccessMode);
        Assert.DoesNotContain("agent-secret", executionSession.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("activity-secret", executionSession.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("artifact-secret", executionSession.Content, StringComparison.Ordinal);

        var artifact = Assert.Single(snapshot.Items, item =>
            item.EntityKind == MemorySourceEntityKind.ProcessArtifact &&
            item.StorageReference is not null);
        Assert.Equal("artifact-id", artifact.StorageReference?.LocatorKind);
        Assert.Equal(ArtifactId.ToString("D"), artifact.StorageReference?.Locator);
        Assert.Contains("content-hash-1", artifact.Content, StringComparison.Ordinal);

        var completion = Assert.Single(snapshot.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessCompletionOutcome);
        Assert.Equal("process-runtime-completion", completion.Metadata["feedbackHook"]);
        Assert.Contains("Completed steps: 1", completion.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_gateway_rejects_denied_process_scope_before_provider_dispatch()
    {
        var provider = new CountingProcessRuntimeEvidenceSourceProvider();
        var adapter = new ProcessRuntimeMemorySourceGatewayAdapter(provider);
        var gateway = new MemorySourceGateway([adapter], [MemorySourceKind.ProcessRuntime]);
        var request = new MemorySourceGatewayRequest(
            MemorySourceKind.ProcessRuntime,
            RunId,
            GenericMemorySourceScope.Agent,
            Cursor: null,
            Take: null,
            MemorySourceGatewayPolicy.AllowScopes(
                [MemorySourceKind.ProcessRuntime],
                [GenericMemorySourceScope.Process]),
            RequesterId: "unit-test");

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.DeniedSourceScope, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Equal(0, provider.ReadCount);
    }

    [Fact]
    public async Task Workflow_gateway_adapter_translates_scope_id_into_workflow_run_request()
    {
        var provider = new CapturingWorkflowRuntimeEvidenceSourceProvider(CreateWorkflowSnapshot(RunId));
        var adapter = new WorkflowRuntimeMemorySourceGatewayAdapter(provider);
        var request = new MemorySourceGatewayRequest(
            MemorySourceKind.WorkflowRuntime,
            RunId,
            GenericMemorySourceScope.Workflow,
            Cursor: null,
            Take: 25,
            MemorySourceGatewayPolicy.AllowScopes(
                [MemorySourceKind.WorkflowRuntime],
                [GenericMemorySourceScope.Workflow]),
            RequesterId: "unit-test");

        var snapshot = await adapter.ReadSnapshotAsync(request);

        Assert.Same(provider.Snapshot, snapshot);
        Assert.Equal(RunId, provider.LastRequest?.RunId);
        Assert.Equal(25, provider.LastRequest?.Take);
    }

    [Fact]
    public void Process_and_agent_framework_modules_register_runtime_source_gateway_adapters()
    {
        var configuration = new ConfigurationBuilder().Build();
        var processServices = new ServiceCollection();
        var agentFrameworkServices = new ServiceCollection();

        processServices.AddProcessesModule(configuration);
        agentFrameworkServices.AddAgentFrameworkModule(configuration);

        Assert.Contains(
            processServices,
            descriptor => descriptor.ServiceType == typeof(IProcessRuntimeEvidenceSourceProvider) &&
                descriptor.ImplementationType == typeof(ProcessRuntimeEvidenceSourceProvider) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            processServices,
            descriptor => descriptor.ServiceType == typeof(IMemorySourceGatewayAdapter) &&
                descriptor.ImplementationType == typeof(ProcessRuntimeMemorySourceGatewayAdapter) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            agentFrameworkServices,
            descriptor => descriptor.ServiceType == typeof(IMemorySourceGatewayAdapter) &&
                descriptor.ImplementationType == typeof(WorkflowRuntimeMemorySourceGatewayAdapter) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            agentFrameworkServices,
            descriptor => descriptor.ServiceType == typeof(IProcessRuntimeEvidenceSourceProvider) &&
                descriptor.ImplementationType == typeof(UnavailableProcessRuntimeEvidenceSourceProvider) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static async Task SeedProcessRuntimeAsync(
        IDbContextFactory<ProcessPersistenceDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.InstancePlans.Add(new ProcessInstancePlanEntity
        {
            PlanId = PlanId,
            RootPlanId = PlanId,
            ParentPlanId = null,
            ParentStepId = null,
            DefinitionId = Guid.Parse("16994769-4242-494d-af5c-e8a4682cb2b7"),
            DefinitionVersionId = Guid.Parse("b6e18709-43f1-4e55-a793-61c41a2e0446"),
            PlanHash = "plan-hash",
            PlanSchemaVersion = "process-plan-v1",
            DefinitionContentHash = "definition-hash",
            PayloadJson = """{"steps":["build"],"apiKey":"sk-plan-secret"}""",
            CreatedAtUtc = Now.AddMinutes(-20)
        });
        var state = new ProcessRuntimeStateEntity
        {
            RunId = RunId,
            RootRunId = RootRunId,
            PlanId = PlanId,
            PlanHash = "plan-hash",
            Status = ProcessRuntimeStatus.Completed,
            UpdatedAtUtc = Now,
            ConcurrencyToken = Guid.Parse("387f65c2-9156-4d05-ac77-a04424e63910")
        };
        state.Steps.Add(new ProcessRuntimeStepEntity
        {
            RunId = RunId,
            StepInstanceId = StepInstanceId,
            StepDefinitionId = StepDefinitionId,
            Status = ProcessRuntimeStepStatus.Completed,
            IsExecutable = true,
            AttemptNumber = 1,
            DependencyStepIds = string.Empty,
            RequiredArtifactSlotIds = SlotId.ToString("D"),
            ActiveClaimToken = null,
            CompletedResultKey = ResultKey
        });
        state.Claims.Add(new ProcessDispatchClaimEntity
        {
            RunId = RunId,
            ClaimToken = ClaimToken,
            StepInstanceId = StepInstanceId,
            OwnerId = "agent:builder",
            Status = DispatchClaimStatus.Completed,
            AttemptNumber = 1,
            CreatedAtUtc = Now.AddMinutes(-10),
            ExpiresAtUtc = Now.AddMinutes(5),
            RenewedAtUtc = Now.AddMinutes(-3),
            ResultIdempotencyKey = ResultKey
        });
        state.ResultReceipts.Add(new ProcessStrategyResultReceiptEntity
        {
            RunId = RunId,
            StepInstanceId = StepInstanceId,
            StrategyId = "standard.agent",
            IdempotencyKey = ResultKey,
            Outcome = "Succeeded",
            AppliedStepStatus = ProcessRuntimeStepStatus.Completed,
            ResultHash = "result-hash"
        });
        state.AvailableArtifactSlots.Add(new ProcessRuntimeAvailableArtifactSlotEntity
        {
            RunId = RunId,
            SlotId = SlotId
        });
        dbContext.RuntimeStates.Add(state);
        dbContext.RuntimeStepAssignments.Add(new ProcessRuntimeStepAssignmentEntity
        {
            RunId = RunId,
            StepInstanceId = StepInstanceId,
            PlanId = PlanId,
            StepKey = "build",
            RoleKey = "developer",
            RoleResourceKey = "agent-builder",
            RoleDisplayName = "Developer",
            ExecutorKind = "agent",
            ExecutorId = "builder",
            ExecutorDisplayName = "Builder Agent",
            Prompt = "Implement the change token=assignment-secret",
            ReadinessHash = "ready-hash",
            AssignmentReason = "unit-test",
            ProducedArtifactSlotIds = SlotId.ToString("D"),
            RequiredArtifactSlotIds = string.Empty,
            AllowedOperations = "edit;test",
            OperationTargetScope = "workspace",
            LaunchVariablesJson = """{"apiKey":"sk-process-secret"}""",
            BranchGateSourceStepKey = null,
            BranchGateRequiredOutcomeKey = null,
            CreatedAtUtc = Now.AddMinutes(-11)
        });
        dbContext.RuntimeEvents.Add(new ProcessRuntimeEventEntity
        {
            GlobalSequence = 1,
            RootSequence = 1,
            EventId = EventId,
            RootRunId = RootRunId,
            RunId = RunId,
            CorrelationId = "correlation-1",
            CausationId = null,
            ActorKind = "Agent",
            ActorId = "builder",
            SchemaVersion = "process-event-v1",
            Sensitivity = MemorySourceSensitivity.Internal.ToString(),
            OccurredAtUtc = Now.AddMinutes(-2),
            EventType = ProcessRuntimeEventTypes.StepCompleted.Value,
            PayloadHash = "payload-hash"
        });
        dbContext.ArtifactLedgerEvents.Add(new ProcessArtifactLedgerEventEntity
        {
            LedgerEventId = LedgerEventId,
            EventId = EventId,
            SlotId = SlotId,
            ArtifactId = ArtifactId,
            ContentHash = "content-hash-1"
        });
        dbContext.ProjectionHistory.Add(new ProcessProjectionHistoryEntity
        {
            ProjectorName = "runtime-workspace",
            ProjectionKey = "run:workspace",
            GlobalSequence = 1,
            RootRunId = RootRunId,
            RunId = RunId,
            OccurredAtUtc = Now.AddMinutes(-2),
            EventType = ProcessRuntimeEventTypes.StepCompleted.Value,
            SchemaVersion = "projection-v1",
            PayloadJson = """{"summary":"ok","secret":"projection-secret"}""",
            PayloadHash = "projection-hash",
            Sensitivity = MemorySourceSensitivity.Internal.ToString()
        });
        dbContext.ProjectionDeadLetters.Add(new ProcessProjectionDeadLetterEntity
        {
            DeadLetterId = Guid.Parse("e71f653d-13a9-47dc-aafe-a8c8b0472dbc"),
            ProjectorName = "runtime-workspace",
            ShardKey = "run",
            EventId = EventId,
            GlobalSequence = 1,
            ErrorClass = "Projection.Json",
            DiagnosticReference = "diag-1",
            RetryPolicy = "manual",
            DeadLetteredAtUtc = Now.AddMinutes(-1)
        });
        await dbContext.SaveChangesAsync();
    }

    private static MemorySourceSnapshot CreateWorkflowSnapshot(Guid runId)
    {
        var itemId = MemorySourceItemId.Create(
            MemorySourceKind.WorkflowRuntime,
            runId,
            MemorySourceEntityKind.WorkflowRun,
            runId.ToString("D"));
        var item = new MemorySourceItem(
            itemId,
            MemorySourceKind.WorkflowRuntime,
            MemorySourceEntityKind.WorkflowRun,
            $"Workflow run {runId:D}",
            "State: Completed",
            MemorySourceSnapshotHasher.Compute(runId.ToString("D")),
            Now,
            Now,
            new MemorySourceProvenance(
                MemorySourceKind.WorkflowRuntime,
                runId,
                MemorySourceEntityKind.WorkflowRun,
                runId.ToString("D"),
                $"/workflows/runs/{runId:D}"),
            new MemorySourcePermissionContext(
                MemorySourceAccessMode.ReadOnly,
                MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: false,
                "unit-test",
                "unit-test"),
            Layout: null,
            Links: [],
            References: [],
            StorageReference: null,
            Metadata: new Dictionary<string, string>());
        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MemorySourceKind.WorkflowRuntime, runId, item.ContentHash),
                MemorySourceKind.WorkflowRuntime,
                runId,
                Now,
                TotalItemCount: 1,
                NextCursor: null,
                HasMore: false,
                MemorySourceSnapshotPageStatus.EndOfSource,
                MemorySourceSnapshotHashScope.FullSnapshot,
                MemorySourceSnapshotProviderVersions.WorkflowRuntime),
            [item]);
    }

    private sealed class FakeProcessExecutionObservationReader : IProcessExecutionObservationReader
    {
        private IReadOnlyList<ProcessExecutionObservation> observations = [];

        public void SetObservations(IReadOnlyList<ProcessExecutionObservation> value)
        {
            observations = value;
        }

        public ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
            ProcessExecutionObservationQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runIds = query.RunIds.ToHashSet();
            return ValueTask.FromResult<IReadOnlyList<ProcessExecutionObservation>>(
                observations
                    .Where(item => runIds.Contains(item.RunId))
                    .Take(query.TakePerRun * Math.Max(1, query.RunIds.Count))
                    .ToArray());
        }
    }

    private sealed class CountingProcessRuntimeEvidenceSourceProvider : IProcessRuntimeEvidenceSourceProvider
    {
        public int ReadCount { get; private set; }

        public Task<MemorySourceSnapshot> ReadSnapshotAsync(
            ProcessRuntimeEvidenceSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            throw new InvalidOperationException("The gateway should reject the denied process scope before dispatch.");
        }
    }

    private sealed class CapturingWorkflowRuntimeEvidenceSourceProvider(MemorySourceSnapshot snapshot) : IWorkflowRuntimeEvidenceSourceProvider
    {
        public MemorySourceSnapshot Snapshot { get; } = snapshot;

        public WorkflowRuntimeEvidenceSourceRequest? LastRequest { get; private set; }

        public Task<MemorySourceSnapshot> ReadSnapshotAsync(
            WorkflowRuntimeEvidenceSourceRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Snapshot);
        }
    }
}
