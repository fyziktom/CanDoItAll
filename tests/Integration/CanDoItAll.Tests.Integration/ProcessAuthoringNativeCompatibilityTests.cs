using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringNativeCompatibilityTests {
    private const string ProbeVariable = "NativeAuthoringScopeProbe";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_native_project_launch_retains_scope_and_independent_exact_lookup(bool published) {
        await using var app = await TestApplication.CreateAsync(new());
        var key = "software-delivery";
        if (published) {
            key = await ProcessAuthoringPublicationTests.SeedAsync(app.Services);
            await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Publish, "Native publication");
        }
        Guid projectId;
        ProcessLaunchResult launched;
        var variables = new Dictionary<string, string> { [ProbeVariable] = Guid.NewGuid().ToString("N") };
        await using (var scope = app.Services.CreateAsyncScope()) {
            projectId = await CreateProjectAsync(scope.ServiceProvider);
            launched = await scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>()
                .LaunchAsync(Request(key, projectId, variables));
            Assert.NotNull(launched.RunId);
        }
        await using var independent = app.Services.CreateAsyncScope();
        var services = independent.ServiceProvider;
        var state = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(launched.RunId!.Value);
        Assert.NotNull(state);
        Assert.Null(state.ProjectAdmission);
        var plan = await services.GetRequiredService<IProcessInstancePlanStore>().LoadAsync(state.PlanId);
        var captured = Assert.IsType<ProcessExecutableDefinitionClosure>(plan!.ExecutableDefinitions);
        var current = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId);
        Assert.NotNull(current);
        Assert.Equal(current.DatabaseProfileId, captured.DatabaseProfileId);
        Assert.Equal(current.ProjectId, captured.ProjectId);
        Assert.Equal(current.LifetimeId, captured.ProjectLifetimeId);
        Assert.Equal(published, captured.Definitions[key].PublicationId is not null);
        var launch = services.GetRequiredService<ProcessLaunchApplicationService>();
        var found = await launch.FindExistingLaunchAsync(new(key, null, projectId, variables));
        Assert.Equal(launched.RunId, found?.RunId);
        Assert.Equal(launched.LaunchPlan.PlanHash, found!.LaunchPlan.PlanHash);
        var anotherProject = await CreateProjectAsync(services);
        Assert.Null(await launch.FindExistingLaunchAsync(new(key, null, anotherProject, variables)));
        Assert.Null(await launch.FindExistingLaunchAsync(new(key, null, null, variables)));
        var projects = services.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(projectId);
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "Recreated lookup project" })).IsSuccess);
        await using var recreated = app.Services.CreateAsyncScope();
        Assert.Null(await recreated.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>()
            .FindExistingLaunchAsync(new(key, null, projectId, variables)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_native_preparation_rechecks_project_lifetime_at_actual_commit(bool recreate) {
        var gate = new CommitGate();
        await using var app = await CreateHeldApplicationAsync(gate);
        await using var scope = app.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var launching = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>()
            .LaunchAsync(Request("software-delivery", projectId, new Dictionary<string, string>()), deadline.Token);
        try {
            await gate.Entered.Task.WaitAsync(deadline.Token);
            await using var retiring = app.Services.CreateAsyncScope();
            var projects = retiring.ServiceProvider.GetRequiredService<ProjectsService>();
            await projects.DeleteAsync(projectId, deadline.Token);
            if (recreate) {
                Assert.True((await projects.CreateAsync(projectId, new() { Name = "Replacement native project" }, cancellationToken: deadline.Token)).IsSuccess);
            }
        } finally {
            gate.Release.TrySetResult();
        }
        var rejection = await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => launching);
        var initial = Assert.IsType<ProcessRuntimeCommitRequest>(gate.Request);
        var captured = initial.InitialPlan!.ExecutableDefinitions!;
        Assert.Equal(captured.ProjectLifetimeId, rejection.Admission.LifetimeId);
        Assert.Equal(projectId, rejection.Admission.ProjectId);
        await AssertUnacceptedAsync(app.Services, initial);
    }

    [Fact]
    public async Task Direct_native_preparation_rechecks_archive_at_actual_commit() {
        var gate = new CommitGate();
        await using var app = await CreateHeldApplicationAsync(gate);
        var key = await ProcessAuthoringPublicationTests.SeedAsync(app.Services);
        await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Publish, "Native reviewed publication");
        await using var scope = app.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var launching = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>()
            .LaunchAsync(Request(key, projectId, new Dictionary<string, string>()), deadline.Token);
        try {
            await gate.Entered.Task.WaitAsync(deadline.Token);
            await ProcessAuthoringPublicationTests.ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Archive, "Archived native publication");
        } finally {
            gate.Release.TrySetResult();
        }
        var rejection = await Assert.ThrowsAsync<InvalidOperationException>(() => launching);
        Assert.Contains("archived", rejection.Message, StringComparison.Ordinal);
        var initial = Assert.IsType<ProcessRuntimeCommitRequest>(gate.Request);
        Assert.NotNull(initial.InitialPlan!.ExecutableDefinitions!.Definitions[key].PublicationId);
        await AssertUnacceptedAsync(app.Services, initial);
    }

    private static ProcessLaunchRequest Request(string key, Guid projectId, IReadOnlyDictionary<string, string> variables)
        => new(key, null, null, projectId, $"project:{projectId:D}", "native-scope-compatibility", variables, false, false);

    private static async Task<Guid> CreateProjectAsync(IServiceProvider services) {
        var id = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(id, new() { Name = "Native process project" })).IsSuccess);
        return id;
    }

    private static Task<TestApplication> CreateHeldApplicationAsync(CommitGate gate)
        => TestApplication.CreateAsync(new() { ConfigureServices = services => {
            services.AddScoped<IProcessRuntimeUnitOfWork>(provider => new HeldCommit(provider.GetRequiredService<EfProcessRuntimeUnitOfWork>(), gate));
        } });

    private static async Task AssertUnacceptedAsync(IServiceProvider services, ProcessRuntimeCommitRequest request) {
        await using var scope = services.CreateAsyncScope();
        var prepared = await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .GetAsync(request.InitialLaunchAdmission!.AdmissionId);
        Assert.NotNull(prepared);
        Assert.Null(prepared.AcceptedAtUtc);
        Assert.Null(prepared.Preparation.InitialCommit.Mutation.State.ProjectAdmission);
        Assert.Equal(request.InitialPlan!.PlanHash, prepared.Preparation.InitialCommit.InitialPlan!.PlanHash);
        await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Empty(await database.RuntimeStates.ToArrayAsync());
        Assert.Empty(await database.InstancePlans.ToArrayAsync());
        Assert.Empty(await database.RuntimeStepAssignments.ToArrayAsync());
        Assert.Single(await database.PreparedLaunches.ToArrayAsync());
    }

    private sealed class CommitGate {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProcessRuntimeCommitRequest? Request { get; set; }
    }

    private sealed class HeldCommit(IProcessRuntimeUnitOfWork inner, CommitGate gate) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            if (request.InitialPlan is not null && gate.Request is null) {
                gate.Request = request;
                gate.Entered.TrySetResult();
                await gate.Release.Task.WaitAsync(cancellationToken);
            }
            return await inner.CommitAsync(request, cancellationToken);
        }
    }
}
