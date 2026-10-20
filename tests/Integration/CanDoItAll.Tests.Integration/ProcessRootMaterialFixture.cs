using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

internal sealed record ProcessRootMaterialFixture(ProjectTaskPlanOperationFixture Sql, ProcessRuntimeStateSnapshot State,
    ProcessDecisionRunAdmission Root, ProcessLaunchAuthority Source) : IAsyncDisposable {
    internal IServiceProvider Services => Sql.Services;
    internal IProcessDecisionRootCapturer Capturer => Services.GetServices<IProcessDecisionRootCapturer>().Single();

    internal static async Task<ProcessRootMaterialFixture> CreateAsync(int steps = 1) {
        var sql = await ProjectTaskPlanOperationFixture.CreateAsync();
        try {
            var source = await sql.Services.GetRequiredService<ProjectProcessLaunchAuthorityService>()
                .CaptureLocalAsync(sql.Project.ProjectId, ProcessLaunchOperatorSurface.UserInterface);
            var prepared = ProcessPreparedLaunchScalingFixture.Create(steps, 16_384, source);
            var initial = prepared.InitialCommit;
            var policy = new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.PauseForReview,
                new(0, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10)));
            var capabilities = new DecisionDataPolicyCapabilities(true, true, false, true, true, true,
                PublicationFreshness.RequireCurrent, policy.Budget);
            var state = initial.OriginalState with { Status = ProcessRuntimeStatus.Active, UpdatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
            ProcessDecisionRunAdmission root = new(state.RunId, new(Guid.NewGuid()), sql.Project.DatabaseProfileId,
                sql.Binding.Namespace.Value, Guid.NewGuid(), initial.InitialPlan!.Definition.VersionId,
                policy, null, null, capabilities, [], policy);
            root = root with { MaterialScope = new(new(Guid.NewGuid()), root.Funding, root.DatabaseProfileId,
                root.WorkspaceNamespaceId, ProjectTaskPlanProcessContract.Content) };
            initial = initial with {
                OriginalState = state, Mutation = initial.Mutation with { State = state }, InitialRunDecisionAdmission = root,
                InitialAssignments = initial.InitialAssignments!.Select(assignment => assignment with {
                    DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, assignment.StepInstanceId) with {
                        ExecutionContract = ProjectTaskPlanProcessContract.Execution
                    }
                }).ToArray()
            };
            var saved = await sql.Services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(prepared with { InitialCommit = initial });
            await using var context = Context(sql.Services);
            var owner = Owner(context, sql.Services);
            state = (await owner.CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).State;
            return new(sql, state, root, source);
        } catch {
            await sql.DisposeAsync();
            throw;
        }
    }

    internal static EfProcessRuntimeUnitOfWork Owner(ProcessPersistenceDbContext context, IServiceProvider services)
        => new(context, services.GetRequiredService<TimeProvider>(), services.GetRequiredService<CanDoItAll.Infrastructure.Persistence.CoordinatedDatabaseTransaction>(),
            services.GetRequiredService<IProcessProjectAdmissionPolicy>(), services.GetRequiredService<IProcessLaunchAuthorityPolicy>(),
            runtimeDatabase: services.GetRequiredService<ICanonicalRuntimeDatabase>());

    internal EfProcessDecisionRootQuery Query(ProcessPersistenceDbContext context)
        => new(context, Services.GetRequiredService<ICanonicalRuntimeDatabase>());

    internal ProcessDecisionRootPreparationCoordinator Coordinator(ProcessPersistenceDbContext context,
        IProcessDecisionRootCapturer? capturer = null, IProcessRuntimeUnitOfWork? commit = null, TimeProvider? clock = null) {
        var owner = Owner(context, Services);
        return new(Query(context), [capturer ?? Capturer], owner, owner, commit ?? owner, clock ?? TimeProvider.System,
            NullLogger<ProcessDecisionRootPreparationCoordinator>.Instance);
    }

    internal ProcessDecisionRootCaptureRequest CaptureRequest() => new(Source, Root.MaterialScope!,
        new(Root.MaterialScope!.ScopeId, 1, new(Guid.NewGuid())));

    public ValueTask DisposeAsync() => Sql.DisposeAsync();
}
