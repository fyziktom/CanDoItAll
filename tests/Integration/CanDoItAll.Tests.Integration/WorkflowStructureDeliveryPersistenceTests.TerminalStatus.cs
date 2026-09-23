using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completed_workflow_survives_terminal_status_commit_fault_and_restarts_only_delivery(bool afterCommit) {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-terminal-delivery");
        var profile = environment.CreatePostgreSqlProfile("terminal-delivery");
        ProjectWorkflowAdmission admission;
        string completedEvidence;
        Guid projectId;
        string nodeId;
        await using (var app = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = RemoveAutomaticDelivery
        })) {
            await using var fixture = await CreateFixtureAsync(app, prepareOutput: false);
            projectId = fixture.ProjectId;
            var template = Definition();
            var definition = await fixture.Services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(
                new(null, null, "Terminal delivery", "Synthetic terminal transaction failure", WorkflowLifecycleStatus.Active,
                    template.Graph, template.RuntimePolicy));
            var node = await fixture.Owner.CreateObjectAsync(projectId,
                new(ProjectObjectType.WorkflowDefinition, "Terminal workflow", "", "", fixture.Request.ParentNodeKey,
                    MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                        Workflow = new() { WorkflowId = definition.Id, WorkflowVersionId = definition.VersionId }
                    })));
            nodeId = node.Id;
            var source = fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
            var authoritySource = ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface);
            var authority = await source.PrepareLaunchAsync(await source.CaptureAsync(projectId, authoritySource), definition);
            var actor = new ProjectStructureAgentContext("terminal-operator", "Operator", "fixture", "", "", "terminal-session") {
                WorkflowAuthority = authoritySource
            };
            admission = await fixture.Owner.PrepareWorkflowAdmissionAsync(projectId, nodeId, Guid.NewGuid(), true,
                definition, "{\"message\":\"original terminal input\"}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor);
            var authorization = fixture.Services.GetRequiredService<IWorkflowLaunchAuthorizationScopeResolver>().Resolve(admission.LaunchIntent.Origin);
            var authorizedOrigin = admission.LaunchIntent.Origin with {
                AuthorizationScope = authorization.Scope, AuthorizationPolicyFingerprint = authorization.PolicyFingerprint
            };
            var nodeBefore = await ReadWorkflowNodeAsync(fixture.Factory, projectId, nodeId);
            var commands = new ObserveTerminalStatusCommands();
            var fault = new CommitFault(afterCommit, () => commands.SawBothUpdates);
            var failingOwner = Owner(fixture.Services, Factory(fixture.Services, commands, fault));
            var launches = new ObserveWorkflowLaunches(fixture.Services.GetRequiredService<IWorkflowLaunchService>());
            var failingNodes = ActivatorUtilities.CreateInstance<ProjectStructureWorkflowNodeService>(fixture.Services, failingOwner, launches);
            var failure = await Assert.ThrowsAsync<ArgumentException>(() => failingNodes.ReconcileAsync(admission.Binding.IntentId));
            Assert.Same(fault.Failure, failure);
            Assert.True(commands.SawBothUpdates);
            Assert.NotNull(commands.Transaction);
            Assert.Equal(1, launches.Calls);
            var store = fixture.Services.GetRequiredService<IWorkflowRunStore>();
            var run = Assert.IsType<WorkflowRunSnapshot>(await store.GetRunAsync(admission.Binding.RunId));
            Assert.Equal(WorkflowRunState.Completed, run.State);
            Assert.Equal(definition.Id, run.WorkflowId);
            Assert.Equal(definition.VersionId, run.VersionId);
            Assert.Equal(JsonSerializer.Serialize(authorizedOrigin), JsonSerializer.Serialize(run.Origin));
            completedEvidence = await TerminalExecutionEvidenceAsync(fixture.Services, run.RunId);
            await using var database = await fixture.Factory.CreateDbContextAsync();
            var saved = await database.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking().SingleAsync(row => row.IntentId == admission.Binding.IntentId);
            Assert.Equal(afterCommit, saved.DeliveryFinished);
            var currentNode = await ReadWorkflowNodeAsync(fixture.Factory, projectId, nodeId);
            if (afterCommit) {
                Assert.Equal(ProjectWorkflowDeliveryState.Applied, saved.Delivery);
                Assert.Equal(100, currentNode.ProgressPercent);
                Assert.Equal(run.RunId, ProjectObjectMetadataSerializer.Parse(currentNode.MetadataJson).Workflow!.LastRunId);
            } else {
                Assert.Equal(nodeBefore, currentNode);
                Assert.Equal(admission.Delivery, saved.Delivery);
            }
        }

        await using var restarted = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = RemoveAutomaticDelivery
        });
        await using var scope = restarted.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var savedAdmission = Assert.IsType<ProjectWorkflowAdmission>(await owner.FindWorkflowAdmissionAsync(admission.Binding.IntentId));
        Assert.Equal(JsonSerializer.Serialize(admission.Binding), JsonSerializer.Serialize(savedAdmission.Binding));
        Assert.Equal(JsonSerializer.Serialize(admission.LaunchIntent), JsonSerializer.Serialize(savedAdmission.LaunchIntent));
        var recoveredLaunches = new ObserveWorkflowLaunches(services.GetRequiredService<IWorkflowLaunchService>());
        var nodes = ActivatorUtilities.CreateInstance<ProjectStructureWorkflowNodeService>(services, recoveredLaunches);
        Assert.Equal(ProjectWorkflowDeliveryState.Applied, await nodes.ReconcileAsync(admission.Binding.IntentId));
        var applied = await ReadWorkflowNodeAsync(Factory(services), projectId, nodeId);
        Assert.Equal(100, applied.ProgressPercent);
        Assert.Equal(admission.Binding.RunId, (await nodes.GetStatusAsync(projectId, nodeId)).RunId);
        Assert.Equal(ProjectWorkflowDeliveryState.Applied, await nodes.ReconcileAsync(admission.Binding.IntentId));
        Assert.Equal(applied, await ReadWorkflowNodeAsync(Factory(services), projectId, nodeId));
        Assert.Equal(completedEvidence, await TerminalExecutionEvidenceAsync(services, admission.Binding.RunId));
        Assert.Equal(0, recoveredLaunches.Calls);
        await using var workflow = await services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
        var runs = await workflow.Set<WorkflowRunRecordEntity>().Where(row => row.WorkflowId == admission.Definition.Id.Value).ToArrayAsync();
        Assert.Equal(admission.Binding.RunId.Value, Assert.Single(runs).RunId);
        await using var workbench = await Factory(services).CreateDbContextAsync();
        var delivered = await workbench.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking().SingleAsync(row => row.IntentId == admission.Binding.IntentId);
        Assert.True(delivered.DeliveryFinished);
        Assert.Equal(ProjectWorkflowDeliveryState.Applied, delivered.Delivery);
    }

    private static async Task<string> TerminalExecutionEvidenceAsync(IServiceProvider services, WorkflowRunId runId) {
        var store = services.GetRequiredService<IWorkflowRunStore>();
        return JsonSerializer.Serialize(new {
            Run = await store.GetRunAsync(runId), Events = await store.ListEventsAsync(runId),
            Artifacts = await store.ListArtifactsAsync(runId),
            Outputs = await services.GetRequiredService<IWorkflowStructureOutputStore>().ListAsync(runId)
        });
    }

    private sealed class ObserveWorkflowLaunches(IWorkflowLaunchService inner) : IWorkflowLaunchService {
        public int Calls { get; private set; }
        public Task<WorkflowLaunchResult> LaunchAsync(WorkflowLaunchIntent intent, CancellationToken cancellationToken = default) {
            Calls++;
            return inner.LaunchAsync(intent, cancellationToken);
        }
    }

    private sealed class ObserveTerminalStatusCommands : DbCommandInterceptor {
        private bool sawNode;
        private bool sawAdmission;
        private DbConnection? connection;
        public DbTransaction? Transaction { get; private set; }
        public bool SawBothUpdates => sawNode && sawAdmission;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            var node = command.CommandText.Contains("UPDATE \"Workbench_ProjectObjects\"", StringComparison.Ordinal);
            var admission = command.CommandText.Contains("UPDATE \"Workbench_WorkflowAdmissions\"", StringComparison.Ordinal);
            if (node || admission) {
                Assert.NotNull(command.Transaction);
                Assert.NotNull(command.Connection);
                connection ??= command.Connection;
                Transaction ??= command.Transaction;
                Assert.Same(connection, command.Connection);
                Assert.Same(Transaction, command.Transaction);
                sawNode |= node;
                sawAdmission |= admission;
            }
            return ValueTask.FromResult(result);
        }
    }
}
