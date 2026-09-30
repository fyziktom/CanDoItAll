using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Playwright.StorageRecovery;

internal static partial class RecoveryBrowserSeed {
    private static async Task<Fixture> CreateFixtureAsync(IServiceProvider provider, bool prepareOutput = true, WorkflowStructureOperatorSurface sourceSurface = WorkflowStructureOperatorSurface.UserInterface) {
        var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var saved = await projects.SaveAsync(new ProjectEditorModel { Name = "Workflow delivery fixture", CurrentPhase = "Execution" });
        Assert.True(saved.IsSuccess);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var definition = Definition();
        var parent = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(saved.Value);
        var request = new ProjectObjectCreateRequest(ProjectObjectType.WorkItem, $"Contribution {Guid.NewGuid():N}", "", "Initial notes", parent,
            ObjectSubtype: "task", MetadataJson: "{}", PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent);
        var authority = await services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().CaptureAsync(saved.Value,
            ProjectStructureWorkflowAuthoritySource.LocalOperator(sourceSurface));
        var plan = new WorkflowStructureOutputPlan(new(WorkflowExecutionOccurrence.Start(WorkflowRunId.New())
                .Advance(definition.VersionId, new("effect")), 0), definition.VersionId, new("effect"), saved.Value,
            new(parent), await owner.ReadWorkflowTargetBindingAsync(saved.Value, parent), WorkflowStructureOutputKind.Task,
            WorkflowStructureOutputRole.RequiredResult, string.Empty) {
            ProjectLifetime = authority.ProjectScope!.Find(saved.Value),
            SourceAuthorityFingerprint = WorkflowStructureAuthorityFingerprint.Create(authority)
        };
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        request = BindRequest(request, plan, authority);
        await SaveRunAsync(services, plan.Identity.Occurrence.RunId, definition,
            new WorkflowLaunchOrigin.Preview(authority.Principal, new("workflow-output-fixture")) { StructureAuthority = authority });
        if (prepareOutput) {
            await OutputStore(services).PrepareAsync(plan);
        }
        return new(saved.Value, owner, Factory(services), definition, plan, request, scope);
    }

    private static PersistentWorkflowStructureOutputStore OutputStore(IServiceProvider services) =>
        new(services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>(), TimeProvider.System,
            services.GetRequiredService<IWorkflowStructureSourceAuthorityPolicy>(),
            services.GetRequiredService<CoordinatedDatabaseTransaction>(), services.GetRequiredService<DbContextOptions<WorkflowDbContext>>());

    private static ProjectObjectCreateRequest BindRequest(ProjectObjectCreateRequest request, WorkflowStructureOutputPlan plan,
        WorkflowStructureAuthority authority) => request with {
        ExpectedProjectAdmission = ProjectStructureWorkflowAuthorityService.ToProjectAdmission(plan.ProjectLifetime!),
        WorkflowMutationAdmission = new(authority, plan.ProjectLifetime!, plan.Kind == WorkflowStructureOutputKind.Asset
            ? WorkflowStructureAuthorityUse.AssetOutput : WorkflowStructureAuthorityUse.TaskOutput, plan)
    };

    private static OwnerFactory Factory(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<WorkbenchDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(interceptors);
        return new(options.Options);
    }

    private static ProjectWorkbenchService Owner(IServiceProvider services, IDbContextFactory<WorkbenchDbContext> factory)
        => new(factory, services.GetRequiredService<ProjectStructureMutationScopeFactory>(),
            services.GetRequiredService<ProjectRecordQueryService>(), services.GetRequiredService<IClock>(), services.GetRequiredService<ProjectAssetStorageService>(),
            services.GetRequiredService<ProjectStructureAssemblyService>(),
            services.GetRequiredService<ProjectWorkbenchRelationService>(), services.GetRequiredService<ProjectWorkbenchLifecycleService>(),
            services.GetRequiredService<ProjectWorkbenchCommandService>(), services.GetRequiredService<ProjectWorkbenchCrossModuleMutationService>(),
            services.GetRequiredService<ProjectStructureRuntimeNodeMetadataBoundary>());

    private static async Task<WorkflowRunSnapshot> SaveRunAsync(IServiceProvider services, WorkflowRunId id,
        WorkflowDefinition definition, WorkflowLaunchOrigin? origin = null) {
        var now = DateTimeOffset.UtcNow;
        origin ??= (await services.GetRequiredService<IWorkflowRunStore>().GetRunAsync(id))?.Origin;
        var run = new WorkflowRunSnapshot(id, definition.Id, definition.VersionId, WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess, id.ToString(), "Completed", now, now) { Origin = origin };
        await services.GetRequiredService<IWorkflowRunStore>().SaveRunAsync(run);
        return run;
    }

    private static WorkflowDefinition Definition() {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
        var now = DateTimeOffset.UtcNow;
        return new(WorkflowId.New(), WorkflowVersionId.New(), "Fixture", "Fixture", WorkflowLifecycleStatus.Active,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty)]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), now, now);
    }

    private sealed record Fixture(Guid ProjectId, ProjectWorkbenchService Owner, OwnerFactory Factory,
        WorkflowDefinition Definition, WorkflowStructureOutputPlan Plan, ProjectObjectCreateRequest Request, AsyncServiceScope Scope) : IAsyncDisposable {
        public IServiceProvider Services => Scope.ServiceProvider;
        public ValueTask DisposeAsync() => Scope.DisposeAsync();
    }
    private sealed class OwnerFactory(DbContextOptions<WorkbenchDbContext> options) : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(options);
        public Task<WorkbenchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class CommitFault(bool afterCommit, Func<bool>? ready = null) : DbTransactionInterceptor {
        public ArgumentException Failure { get; } = new("Injected native receipt acknowledgement failure.");
        public bool SawReceiptInsert { get; set; }
        private bool thrown;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) {
            if (!afterCommit && (ready?.Invoke() ?? SawReceiptInsert) && !thrown) {
                thrown = true;
                throw Failure;
            }

            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (afterCommit && (ready?.Invoke() ?? SawReceiptInsert) && !thrown) {
                thrown = true;
                throw Failure;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ObserveReceiptCommands(CommitFault fault, string table = "Workbench_WorkflowContributionReceipts") : DbCommandInterceptor {
        private bool sawNativeInsert;
        private bool sawReceiptUpdate;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (table == "Workbench_WorkflowContributionReceipts") {
                sawNativeInsert |= command.CommandText.Contains("INSERT INTO \"Workbench_ProjectObjects\"", StringComparison.Ordinal);
                sawReceiptUpdate |= command.CommandText.Contains("UPDATE \"Workbench_WorkflowContributionReceipts\"", StringComparison.Ordinal) &&
                    command.CommandText.Contains("\"ReceiptJson\"", StringComparison.Ordinal);
                fault.SawReceiptInsert = sawNativeInsert && sawReceiptUpdate;
            } else if (command.CommandText.Contains($"INSERT INTO \"{table}\"", StringComparison.Ordinal)) {
                fault.SawReceiptInsert = true;
            }

            return ValueTask.FromResult(result);
        }
    }
}
