using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class WorkflowHttpSecretAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mapped_Workflow_read_retains_original_project_after_deferral_and_old_child_format(bool legacy) {
        await using var fixture = await CreateMappedReadFixtureAsync(deferred: true, legacy);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(6);
        var result = await InvokeMappedReadAsync(fixture);
        Assert.Contains(fixture.Project.ProjectId.ToString("D"), result.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Null(fixture.Run.Origin!.StructureAuthority);
        var other = await fixture.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Unrelated current project" });
        Assert.True(other.IsSuccess);
        using var scope = WorkflowExecutorExecutionAuditScope.Push(fixture.Run.RunId, fixture.Run.Origin);
        var context = new WorkflowStructureReadContext(WorkflowExecutionOccurrence.Start(fixture.Run.RunId), fixture.Definition.VersionId, fixture.Node.Id);
        var gateway = fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>();
        var listed = await gateway.ListWorkflowProjectsAsync(context);
        Assert.Equal(fixture.Project.ProjectId, Assert.Single(listed).Id);
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => gateway.ReadWorkflowStructureAsync(other.Value, new(), context));
    }

    [Fact]
    public async Task Active_mapped_claim_can_read_before_launch_completion_receipt_exists() {
        await using var fixture = await CreateMappedReadFixtureAsync(deferred: false, legacy: false);
        await using var database = await fixture.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
        Assert.False(await database.Set<WorkflowLaunchIdempotencyRecordEntity>().AnyAsync(row => row.ReservedRunId == fixture.Run.RunId.Value));
        Assert.Contains(fixture.Project.ProjectId.ToString("D"), (await InvokeMappedReadAsync(fixture)).PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MappedReadRevocation.Cancel)]
    [InlineData(MappedReadRevocation.Rework)]
    [InlineData(MappedReadRevocation.Assignment)]
    [InlineData(MappedReadRevocation.ProjectReplacement)]
    [InlineData(MappedReadRevocation.MissingReceipt)]
    [InlineData(MappedReadRevocation.AmbiguousChild)]
    public async Task Deferred_mapped_read_rechecks_current_parent_and_exact_child_receipt(MappedReadRevocation change) {
        await using var fixture = await CreateMappedReadFixtureAsync(deferred: true, legacy: false);
        _ = await InvokeMappedReadAsync(fixture);
        switch (change) {
            case MappedReadRevocation.Cancel:
                await fixture.Parent.CancelAsync();
                break;
            case MappedReadRevocation.Rework:
                await fixture.Parent.ReworkAsync(deferAgain: true);
                break;
            case MappedReadRevocation.Assignment:
                await using (var database = fixture.Parent.Context()) {
                    (await database.RuntimeStepAssignments.SingleAsync()).WorkflowId = Guid.NewGuid();
                    await database.SaveChangesAsync();
                }
                break;
            case MappedReadRevocation.ProjectReplacement:
                await fixture.Parent.ReplaceProjectAsync();
                break;
            case MappedReadRevocation.MissingReceipt:
                await using (var database = await fixture.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync()) {
                    database.Remove(await database.Set<WorkflowLaunchIdempotencyRecordEntity>().SingleAsync(row => row.ReservedRunId == fixture.Run.RunId.Value));
                    await database.SaveChangesAsync();
                }
                break;
            case MappedReadRevocation.AmbiguousChild:
                await using (var database = await fixture.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync()) {
                    database.Add(WorkflowRunRecordEntity.FromSnapshot(fixture.Run with { RunId = WorkflowRunId.New() }));
                    await database.SaveChangesAsync();
                }
                break;
        }
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeMappedReadAsync(fixture));
        Assert.Equal(WorkflowRunRecordEntity.SerializeOrigin(fixture.Run.Origin), WorkflowRunRecordEntity.SerializeOrigin(
            (await fixture.Services.GetRequiredService<IWorkflowRunStore>().GetRunAsync(fixture.Run.RunId))!.Origin));
    }

    [Fact]
    public async Task Old_mapped_child_without_retained_parent_profile_cannot_acquire_read_authority() {
        await using var fixture = await CreateMappedReadFixtureAsync(deferred: true, legacy: true);
        await using (var database = fixture.Parent.Context()) {
            var state = await database.RuntimeStates.SingleAsync();
            state.LaunchAdmissionId = null;
            state.ProjectAdmissionDatabaseProfileId = null;
            state.ProjectAdmissionProjectId = null;
            state.ProjectAdmissionLifetimeId = null;
            await database.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeMappedReadAsync(fixture));
        Assert.IsType<WorkflowLaunchOrigin.ProcessAssignment>((await fixture.Services.GetRequiredService<IWorkflowRunStore>()
            .GetRunAsync(fixture.Run.RunId))!.Origin);
    }

    public enum MappedReadRevocation { Cancel, Rework, Assignment, ProjectReplacement, MissingReceipt, AmbiguousChild }

    private static async Task<MappedReadFixture> CreateMappedReadFixtureAsync(bool deferred, bool legacy, LlmCallComponent? disclosureComponent = null) {
        var clock = new Clock { Now = DateTimeOffset.UtcNow };
        var application = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            foreach (var registration in services.Where(item => item.ImplementationType == typeof(ProjectStructureWorkflowDeliveryWorker)).ToArray()) {
                services.Remove(registration);
            }
        } });
        var scope = application.Services.CreateAsyncScope();
        try {
            var services = scope.ServiceProvider;
            var node = new WorkflowNode(new("read"), WorkflowNodeKind.Executor, "Mapped project read", [],
                new(null, null, null, null, "", WorkflowValueShape.Text, WorkflowValueShape.Text) {
                    ExecutorId = WorkflowExecutorIds.ProjectStructure, ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
                });
            var definition = new WorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), "Mapped read", "Synthetic owner read",
                WorkflowLifecycleStatus.Active, new(node.Id, [node], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), clock.Now, clock.Now);
            var parent = await MappedParent.CreateAsync(services, definition);
            var captured = await parent.Source.CaptureAsync(new(parent.Assignment.RunId.Value), new(parent.Assignment.StepInstanceId.Value),
                parent.Claim.Value, parent.Contract.ContractHash);
            var dispatch = await services.GetRequiredService<IProcessWorkflowDispatchAuthorityReader>().ReadAsync(new(new(parent.Assignment.RunId.Value),
                new(parent.Assignment.StepInstanceId.Value), new(parent.Claim.Value), parent.Contract.ContractHash));
            var originalProject = Assert.IsType<CanDoItAll.Processes.Runtime.ProcessProjectAdmission>(dispatch.SourceAuthority!.ProjectAdmission);
            var project = new ProjectWriteAdmission(originalProject.DatabaseProfileId, originalProject.ProjectId, originalProject.LifetimeId);
            node = node with { Settings = node.Settings with { ExecutorSettingsJson = WorkflowExecutorJson.Serialize(new WorkflowProjectStructureExecutorSettings {
                Operation = WorkflowProjectStructureOperation.ReadTree, ProjectId = project.ProjectId
            }) } };
            definition = definition with { Graph = new(node.Id, [node], []) };
            if (disclosureComponent is not null) {
                var provider = new WorkflowNode(new("provider"), WorkflowNodeKind.LlmCall, "Mapped provider disclosure", [],
                    new(disclosureComponent.Id, null, null, null, "Summarize the admitted result.", WorkflowValueShape.Text, WorkflowValueShape.Text));
                definition = definition with { Graph = new(node.Id, [node, provider],
                    [new(new("read-provider"), node.Id, null, provider.Id, null, WorkflowEdgeKind.Direct, "")]) };
            }
            WorkflowLaunchOrigin origin = legacy ? new WorkflowLaunchOrigin.ProcessAssignment(captured.Dispatch.ProcessRun, captured.Dispatch.Assignment, captured.CorrelationId) {
                AuthorizationScope = captured.AuthorizationScope, AuthorizationPolicyFingerprint = captured.AuthorizationPolicyFingerprint
            } : captured;
            var run = new WorkflowRunSnapshot(WorkflowRunId.New(), definition.Id, definition.VersionId, WorkflowRunState.Running,
                WorkflowRuntimeBackendKind.InProcess, "mapped-read", "Running", clock.Now, clock.Now) { Origin = origin };
            var factory = services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
            await using (var database = await factory.CreateDbContextAsync()) {
                database.Add(WorkflowDefinitionRecord.FromDefinition(definition, 1));
                if (legacy) {
                    database.Add(WorkflowRunRecordEntity.FromSnapshot(run));
                }
                await database.SaveChangesAsync();
            }
            if (!legacy) {
                await services.GetRequiredService<IWorkflowRunStore>().CreateRunWithStartedEventAsync(run,
                    new(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "Read", "{}", clock.Now) {
                        DisclosureDeclaration = disclosureComponent is null ? null : new(run.RunId, definition.Id, definition.VersionId,
                            WorkflowProviderDisclosureContent.Definition(definition), WorkflowProviderDisclosureContent.Source(origin),
                            WorkflowProviderDisclosureProtocol.Current)
                    });
            }
            if (deferred) {
                var key = new WorkflowLaunchIdempotencyKey($"process-assignment:{parent.Assignment.RunId.Value:N}:{parent.Assignment.StepInstanceId.Value:N}");
                var intent = new WorkflowLaunchIntent(new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
                    WorkflowLaunchMode.Production, origin, parent.InputJson, WorkflowLaunchCompletionPolicy.WaitForStopped,
                    new WorkflowLaunchIdempotency.CallerSupplied(key));
                var receiptScope = WorkflowLaunchIdempotencyRequestFactory.CreateScope(intent, key);
                var receipts = new PersistentWorkflowLaunchIdempotencyStore(factory);
                var token = new WorkflowLaunchIdempotencyClaimToken(Guid.NewGuid());
                var claimed = await receipts.TryClaimAsync(receiptScope, WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(intent, parent.InputJson),
                    token, run.RunId, clock.Now, clock.Now.AddMinutes(5));
                Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, claimed.Outcome);
                var backend = new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]).GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
                Assert.True(await receipts.TryCompleteClaimAsync(receiptScope, token, new(run,
                    new(definition, parent.InputJson, backend, WorkflowPreviewSimulationPlan.Empty, intent.Mode, origin,
                        intent.CompletionPolicy, intent.Idempotency, clock.Now), clock.Now)));
                await parent.DeferAsync();
            }
            return new(application, scope, parent, project, definition, node, run, clock);
        } catch {
            await scope.DisposeAsync();
            await application.DisposeAsync();
            throw;
        }
    }

    private static async Task<WorkflowNodeExecutionResult> InvokeMappedReadAsync(MappedReadFixture fixture) {
        var executor = new ProjectStructureWorkflowExecutor(fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>());
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor], timeProvider: fixture.Clock);
        using var scope = WorkflowExecutorExecutionAuditScope.Push(fixture.Run.RunId, fixture.Run.Origin);
        return await invoker.ExecuteAsync(fixture.Definition, fixture.Node, new(fixture.Parent.InputJson), new WorkflowExecutorInvocationContext {
            ExecutionOccurrence = WorkflowExecutionOccurrence.Start(fixture.Run.RunId).Advance(fixture.Definition.VersionId, fixture.Node.Id)
        });
    }

    private sealed record MappedReadFixture(TestApplication Application, AsyncServiceScope Scope, MappedParent Parent, ProjectWriteAdmission Project,
        WorkflowDefinition Definition, WorkflowNode Node, WorkflowRunSnapshot Run, Clock Clock) : IAsyncDisposable {
        public IServiceProvider Services => Scope.ServiceProvider;
        public async ValueTask DisposeAsync() {
            await Scope.DisposeAsync();
            await Application.DisposeAsync();
        }
    }
}
