using CanDoItAll.SharedKernel;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessLaunchProducerApiTests {
    [Fact]
    public async Task Concurrent_fresh_API_previews_with_different_server_sessions_return_the_same_winning_preparation() {
        var resolver = new ConcurrentResolver();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services => {
            services.Replace(ServiceDescriptor.Singleton<IProcessLaunchExecutorResolver>(resolver));
            services.Replace(ServiceDescriptor.Singleton<IProcessLaunchDriverCatalogProvider>(new PreviewDrivers()));
        });
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(project, new() { Name = "Concurrent Process preview" })).IsSuccess);
        var node = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project,
            new ProjectObjectCreateRequest(ProjectObjectType.ProjectBlock, "Target", "Preview target", "A retained context snapshot.",
                $"project:{project}", 420, 260, ObjectSubtype: "delivery"));
        var intent = Guid.NewGuid();
        var request = new ProcessLaunchApiRequest(DefinitionKey: "dotnet-runtime-command-writeback", ProjectId: project,
            ProjectNodeId: node.Id, Variables: new() { ["Topic"] = "Concurrent original input" }, RunReadiness: false, CallerIntentId: intent);
        var responses = await Task.WhenAll(host.Client.PostAsJsonAsync("/api/processes/launch/check", request),
            host.Client.PostAsJsonAsync("/api/processes/launch/check", request));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        using var first = await ReadAsync(firstResponse);
        using var second = await ReadAsync(secondResponse);
        Assert.Equal(first.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid(),
            second.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        Assert.Equal(first.RootElement.GetProperty("launchPlanId").GetGuid(), second.RootElement.GetProperty("launchPlanId").GetGuid());
        Assert.Equal(JsonValueKind.Null, first.RootElement.GetProperty("runId").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.RootElement.GetProperty("runId").ValueKind);
        var resolved = resolver.Requests.ToArray();
        Assert.Equal(2, resolved.Length);
        Assert.NotEqual(resolved[0].Variables["SessionId"], resolved[1].Variables["SessionId"]);
        await using var context = new ProcessPersistenceDbContext(Options(services));
        var retained = await context.PreparedLaunches.SingleAsync(row => row.CallerIntentId == intent);
        Assert.Null(retained.AcceptedAtUtc);
        Assert.False(await context.RuntimeStates.AnyAsync(row => row.RunId == retained.RunId));
    }

    [Fact]
    public async Task Retrying_project_preview_restores_saved_input_without_reading_a_now_missing_source_node() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var fixture = await PrepareAsync(host, projectScoped: true);
        using var response = await host.Client.PostAsJsonAsync("/api/processes/launch/check", fixture.Request);
        using var body = await ReadAsync(response);
        Assert.Equal(fixture.Saved.Preparation.Review.PlanId.Value, body.RootElement.GetProperty("launchPlanId").GetGuid());
        Assert.Equal(fixture.Saved.Preparation.AdmissionId.Value, body.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("runId").ValueKind);
        await using var scope = host.App.Services.CreateAsyncScope();
        var saved = Assert.IsType<ProcessPreparedLaunchSnapshot>(await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .GetAsync(fixture.Saved.Preparation.AdmissionId));
        Assert.Equal("original-server-session", saved.Preparation.Request.Variables["SessionId"]);
        Assert.Equal(fixture.Saved.Preparation.Authority!.ProjectAdmission, saved.Preparation.Authority!.ProjectAdmission);
        Assert.Equal(fixture.Saved.PreparationFingerprint, saved.PreparationFingerprint);
        Assert.Null(saved.AcceptedAtUtc);
    }

    [Fact]
    public async Task API_rejects_changed_raw_input_before_it_can_retarget_the_saved_preparation() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var fixture = await PrepareAsync(host, projectScoped: true);
        using var response = await host.Client.PostAsJsonAsync("/api/processes/launch", fixture.Request with {
            Variables = new Dictionary<string, string> { ["Topic"] = "Changed original input" }
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var context = new ProcessPersistenceDbContext(Options(host.App.Services));
        Assert.False(await context.RuntimeStates.AnyAsync(row => row.RunId == fixture.Saved.Preparation.InitialCommit.Mutation.State.RunId.Value));
        var row = await context.PreparedLaunches.SingleAsync(row => row.Id == fixture.Saved.Preparation.AdmissionId.Value);
        Assert.Null(row.AcceptedAtUtc);
        Assert.Equal(fixture.Saved.PreparationFingerprint, row.PreparationFingerprint);
    }

    [Fact]
    public async Task API_reports_accepted_identity_after_actual_commit_ack_loss_and_retry_keeps_one_run() {
        var failure = new ArgumentException("Injected Process API commit acknowledgement loss.");
        var fault = new CommitAckFault(failure);
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services => {
            services.AddScoped(provider => new ProcessPersistenceDbContext(Options(provider, fault)));
            services.Replace(ServiceDescriptor.Singleton<IProcessLaunchArtifactInitializer>(new NoArtifacts()));
        });
        var fixture = await PrepareAsync(host, projectScoped: false);
        fault.RunId = fixture.Saved.Preparation.InitialCommit.Mutation.State.RunId.Value;
        using (var response = await host.Client.PostAsJsonAsync("/api/processes/launch", fixture.Request)) {
            using var body = await ReadAsync(response);
            Assert.Equal(fault.RunId, body.RootElement.GetProperty("runId").GetGuid());
            var observation = body.RootElement.GetProperty("observation");
            Assert.Equal(fixture.Saved.Preparation.AdmissionId.Value, observation.GetProperty("admissionId").GetGuid());
            Assert.Equal(fault.RunId, observation.GetProperty("acceptedRunId").GetGuid());
            Assert.Equal(nameof(ProcessLaunchContinuationState.Accepted), observation.GetProperty("continuationState").GetString());
            Assert.False(observation.TryGetProperty("observationException", out _));
        }
        Assert.Equal(1, fault.Calls);
        Assert.Same(failure, fault.Thrown);
        using (var response = await host.Client.PostAsJsonAsync("/api/processes/launch", fixture.Request)) {
            using var body = await ReadAsync(response);
            Assert.Equal(fault.RunId, body.RootElement.GetProperty("runId").GetGuid());
            Assert.Equal(fixture.Saved.Preparation.Review.PlanId.Value, body.RootElement.GetProperty("launchPlanId").GetGuid());
        }
        await using var context = new ProcessPersistenceDbContext(Options(host.App.Services));
        Assert.Equal(1, await context.RuntimeStates.CountAsync(row => row.RunId == fault.RunId));
        Assert.Equal(1, await context.PreparedLaunches.CountAsync(row => row.CallerIntentId == fixture.Request.CallerIntentId));
        Assert.Equal(1, await context.InstancePlans.CountAsync(row => row.PlanId == fixture.Saved.Preparation.Review.PlanId.Value));
    }

    [Fact]
    public async Task Accepted_API_receipt_survives_host_restart_and_project_retirement_without_writes_on_status_reads() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-producer-restart");
        var profile = environment.CreatePostgreSqlProfile("process-producer-restart");
        Fixture fixture;
        await using (var host = await ApiTestHost.CreateAsync(jwtEnabled: false, sharedTestEnvironment: environment, sharedActiveProfile: profile)) {
            fixture = await PrepareAsync(host, projectScoped: true);
            await using var scope = host.App.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var commit = await services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(ProcessPreparedLaunchFixture.Commit(fixture.Saved));
            Assert.True(commit.Succeeded);
            var projectId = fixture.Saved.Preparation.Authority!.ProjectAdmission!.ProjectId;
            var deletion = await services.GetRequiredService<ProjectsService>().DeleteAsync(projectId);
            Assert.Equal(projectId, deletion.ProjectId);
            Assert.Empty(deletion.Warnings);
        }
        await using var restarted = await ApiTestHost.CreateAsync(jwtEnabled: false, sharedTestEnvironment: environment, sharedActiveProfile: profile);
        await using var before = new ProcessPersistenceDbContext(Options(restarted.App.Services));
        var retainedBefore = await before.PreparedLaunches.AsNoTracking().SingleAsync(row => row.Id == fixture.Saved.Preparation.AdmissionId.Value);
        var eventsBefore = await before.RuntimeEvents.CountAsync(row => row.RunId == fixture.Saved.Preparation.InitialCommit.Mutation.State.RunId.Value);
        for (var index = 0; index < 2; index++) {
            using var response = await restarted.Client.GetAsync($"/api/processes/launch/{fixture.Saved.Preparation.AdmissionId.Value:D}");
            using var body = await ReadAsync(response);
            Assert.Equal(fixture.Saved.Preparation.InitialCommit.Mutation.State.RunId.Value, body.RootElement.GetProperty("acceptedRunId").GetGuid());
            Assert.Equal(fixture.Saved.Preparation.AdmissionId.Value, body.RootElement.GetProperty("admissionId").GetGuid());
        }
        await using var after = new ProcessPersistenceDbContext(Options(restarted.App.Services));
        var retainedAfter = await after.PreparedLaunches.AsNoTracking().SingleAsync(row => row.Id == fixture.Saved.Preparation.AdmissionId.Value);
        Assert.Equal(retainedBefore.PreparationFingerprint, retainedAfter.PreparationFingerprint);
        Assert.Equal(retainedBefore.State, retainedAfter.State);
        Assert.Equal(retainedBefore.AcceptedAtUtc, retainedAfter.AcceptedAtUtc);
        Assert.Equal(retainedBefore.ContinuationGeneration, retainedAfter.ContinuationGeneration);
        Assert.Equal(eventsBefore, await after.RuntimeEvents.CountAsync(row => row.RunId == fixture.Saved.Preparation.InitialCommit.Mutation.State.RunId.Value));
    }

    private static async Task<Fixture> PrepareAsync(ApiTestHost host, bool projectScoped) {
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Guid? projectId = null;
        if (projectScoped) {
            projectId = Guid.NewGuid();
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId.Value, new() { Name = "Process producer project" })).IsSuccess);
        }
        var authority = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.Api);
        var preparation = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()));
        var input = preparation.Request with {
            ProjectNodeId = projectScoped ? "removed-source-node" : null,
            RequestedBy = "display-label-is-not-an-actor",
            Authority = null,
            ProjectAdmission = null
        };
        var enriched = input with {
            Authority = authority,
            ProjectAdmission = authority.ProjectAdmission,
            ProducerInputFingerprint = ProcessLaunchProducerRequests.InputFingerprint(input, authority),
            Variables = new Dictionary<string, string>(input.Variables) { ["SessionId"] = "original-server-session" }
        };
        preparation = preparation with { Request = enriched, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(enriched) };
        var saved = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation);
        Assert.IsType<ProcessLaunchPrincipal.LocalOperator>(saved.Preparation.Authority!.Principal);
        return new(saved, new(input.DefinitionKey, input.ProcessDefinitionId?.Value, input.LiveRunProfileKey, input.ProjectId,
            input.ProjectNodeId, input.RequestedBy, new(input.Variables), input.RunReadiness, input.Execute,
            input.CallerIntentId?.Value));
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body);
    }

    private static DbContextOptions<ProcessPersistenceDbContext> Options(IServiceProvider services, params IInterceptor[] interceptors) {
        var builder = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(builder, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        return builder.AddInterceptors(interceptors).Options;
    }

    private sealed record Fixture(ProcessPreparedLaunchSnapshot Saved, ProcessLaunchApiRequest Request);

    private sealed class CommitAckFault(Exception failure) : DbTransactionInterceptor {
        public Guid RunId { get; set; }
        public int Calls { get; private set; }
        public Exception? Thrown { get; private set; }
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (Calls != 0 || RunId == Guid.Empty || eventData.Context is not ProcessPersistenceDbContext context) {
                return;
            }
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT EXISTS (SELECT 1 FROM process_runtime_states runtime
                    JOIN process_prepared_launches admission ON admission."RunId" = runtime."RunId"
                    WHERE runtime."RunId" = @runId AND admission."State" = @accepted)
                """;
            var runId = command.CreateParameter();
            runId.ParameterName = "runId";
            runId.Value = RunId;
            command.Parameters.Add(runId);
            var accepted = command.CreateParameter();
            accepted.ParameterName = "accepted";
            accepted.Value = nameof(ProcessLaunchContinuationState.Accepted);
            command.Parameters.Add(accepted);
            if (await command.ExecuteScalarAsync(cancellationToken) is not true) {
                return;
            }
            Calls++;
            Thrown = failure;
            throw failure;
        }
    }

    private sealed class NoArtifacts : IProcessLaunchArtifactInitializer {
        public Task InitializeAsync(ProcessLaunchArtifactInitializationRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ConcurrentResolver : IProcessLaunchExecutorResolver {
        private readonly TaskCompletionSource bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        public System.Collections.Concurrent.ConcurrentQueue<ProcessLaunchExecutorResolutionRequest> Requests { get; } = new();
        public async ValueTask<ProcessLaunchExecutorResolution> ResolveAsync(ProcessLaunchExecutorResolutionRequest request,
            CancellationToken cancellationToken = default) {
            Requests.Enqueue(request);
            if (Interlocked.Increment(ref calls) == 2) {
                bothEntered.TrySetResult();
            }
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            var steps = request.Definition.Steps.ToDictionary(step => step.Key, StringComparer.OrdinalIgnoreCase);
            return new(request.Plan.Steps.Where(step => step.IsExecutable).Select(step => new ProcessLaunchExecutorBinding(
                step.StepKey, steps[step.StepKey].RoleAssignments.OrderBy(role => role.FallbackOrder).First().RoleKey,
                ProcessLaunchExecutorKinds.Agent, "preview-test-executor", "Preview test executor", "sha256:preview-readiness", "Preview fixture.")).ToArray(), []);
        }
    }

    private sealed class PreviewDrivers : IProcessLaunchDriverCatalogProvider {
        private static readonly StrategyId Execution = new("strategy.producer-preview.execute");
        private static readonly IReadOnlySet<CapabilityTag> Capabilities = new HashSet<CapabilityTag> { new("capability.producer-preview") };
        public ValueTask<ProcessLaunchDriverCatalog> LoadAsync(CancellationToken cancellationToken = default) {
            var descriptor = new ProcessDriverDescriptor(new("driver.producer-preview"), "Producer preview fixture", "1.0.0",
                "runtime/1.0", "runtime/1.0", ProcessDriverLayer.Framework, Capabilities, [], [], [],
                [new ProcessStrategyDescriptor(Execution, "1.0.0", ProcessStrategyKind.StepExecution, Capabilities)]);
            return ValueTask.FromResult(new ProcessLaunchDriverCatalog(new([new ProcessDriverPackage(descriptor, [], [], [], [], [], [])]), Execution, Capabilities));
        }
    }
}
