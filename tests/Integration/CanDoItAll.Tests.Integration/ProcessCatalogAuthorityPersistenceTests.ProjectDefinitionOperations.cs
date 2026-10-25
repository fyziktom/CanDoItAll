using System.Data;
using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Serializable_owner_attempts_with_an_explicit_source_lease_seam_recover_the_receipt_after_a_snapshot_miss() {
        var clock = new NativeClock();
        var rendezvous = new DefinitionOperationLockRendezvous();
        await using var app = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                ProcessProjectsHarness(clock).ConfigureServices!(services);
                services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(rendezvous), ServiceLifetime.Singleton);
            }
        });
        await using var firstScope = app.Services.CreateAsyncScope();
        await using var secondScope = app.Services.CreateAsyncScope();
        var services = firstScope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var processes = services.GetRequiredService<ProjectProcessExecutionMutationService>();
        var admission = (await processes.ObserveAsync(run.Id))!;
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        var firstInvocation = WithIndependentSourceLease(DefinitionInvocation(services, admission, original.Expectation));
        var secondInvocation = WithIndependentSourceLease(DefinitionInvocation(secondScope.ServiceProvider, admission, original.Expectation));
        var context = await services.GetRequiredService<ProjectDefinitionOperationProtocol>().GetWriteContextAsync(firstInvocation.Caller);
        var request = new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(
            new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding), ProjectDefinitionRequestVersion.VersionOne,
            new(original.Expectation, original.Scalars with { Name = "Single governed accepted effect" },
                original.Phases.Select(row => new ProjectDefinitionPhaseInput(row.Id, row.Name, row.Goal, row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
                original.Options.Select(row => new ProjectDefinitionOptionInput(row.Id, row.Category, row.OptionName, row.Notes)).ToArray(), null));
        rendezvous.Armed = true;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var first = services.GetRequiredService<ProjectDefinitionWriter>().ReplaceAsync(request, firstInvocation, deadline.Token);
        var second = secondScope.ServiceProvider.GetRequiredService<ProjectDefinitionWriter>().ReplaceAsync(request, secondInvocation, deadline.Token);
        ProjectDefinitionOutcome<ProjectEditorAcknowledgement>[] outcomes;
        try {
            await rendezvous.BothTransactions.Task.WaitAsync(deadline.Token);
        } finally {
            rendezvous.Release.TrySetResult();
            outcomes = await Task.WhenAll(first, second);
        }
        var accepted = outcomes.Select(outcome =>
            Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(outcome)).ToArray();

        Assert.Single(accepted, outcome => !outcome.Replayed);
        Assert.Single(accepted, outcome => outcome.Replayed);
        Assert.Equal(2, rendezvous.SerializableContexts.Distinct().Count());
        Assert.Equal(2, rendezvous.MissingLockedReceipts);
        Assert.Equal(ProjectDefinitionOperationCodec.EncodeAccepted(accepted[0].Result, ProjectDefinitionLimits.VersionOneMaximum).ToArray(),
            ProjectDefinitionOperationCodec.EncodeAccepted(accepted[1].Result, ProjectDefinitionLimits.VersionOneMaximum).ToArray());
        var current = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        Assert.Equal(accepted[0].Result.Acknowledgement.Definition, current.Expectation);
        Assert.Equal("Single governed accepted effect", current.Scalars.Name);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync());

        static ProjectDefinitionInvocation WithIndependentSourceLease(ProjectDefinitionInvocation original)
            => new(original.Caller, original.ReceiptAccess,
                new(new IndependentDefinitionSource(original.Authorization!.ExpectedProjects), original.Authorization.ExpectedProjects),
                original.EffectBinding);
    }

    [Fact]
    public async Task Expired_process_claim_can_recover_its_accepted_definition_without_reacquiring_mutation_authority() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var processes = services.GetRequiredService<ProjectProcessExecutionMutationService>();
        var admission = (await processes.ObserveAsync(run.Id))!;
        var original = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        var governed = DefinitionInvocation(services, admission, original.Expectation);
        var countedSource = new CountedDefinitionSource(governed.Authorization!.SourceAuthority);
        var invocation = new ProjectDefinitionInvocation(governed.Caller, governed.ReceiptAccess,
            new(countedSource, governed.Authorization.ExpectedProjects), governed.EffectBinding);
        var context = await services.GetRequiredService<ProjectDefinitionOperationProtocol>().GetWriteContextAsync(invocation.Caller);
        var command = new ReplaceProjectDefinitionCommand(original.Expectation, original.Scalars with { Name = "Accepted before source expiry" },
            original.Phases.Select(row => new ProjectDefinitionPhaseInput(row.Id, row.Name, row.Goal, row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            original.Options.Select(row => new ProjectDefinitionOptionInput(row.Id, row.Category, row.OptionName, row.Notes)).ToArray(), null);
        var request = new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(
            new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding), ProjectDefinitionRequestVersion.VersionOne, command);
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        var accepted = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.ReplaceAsync(request, invocation));
        Assert.Equal(1, countedSource.Acquisitions);
        clock.Now = clock.Now.AddHours(2);

        var found = Assert.IsType<ProjectDefinitionLookup.FoundFull>(await services.GetRequiredService<ProjectDefinitionReceiptReader>()
            .LookupAsync(request.Operation, invocation));
        var replay = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await writer.ReplaceAsync(request, invocation));

        Assert.True(replay.Replayed);
        Assert.Equal(1, countedSource.Acquisitions);
        Assert.Equal(Encode(accepted.Result), Encode(found.Result));
        Assert.Equal(Encode(accepted.Result), Encode(replay.Result));
        var current = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        var fresh = new ProjectDefinitionRequest<ReplaceProjectDefinitionCommand>(
            new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding), ProjectDefinitionRequestVersion.VersionOne,
            new(current.Expectation, current.Scalars with { Name = "Must require current source authority" }, command.Phases, command.Options, null));

        Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(await writer.ReplaceAsync(fresh, invocation));
        Assert.Equal(2, countedSource.Acquisitions);
        var unchanged = (await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(fixture.Project.ProjectId)).Value!;
        Assert.Equal(accepted.Result.Acknowledgement.Definition, unchanged.Expectation);
        Assert.Equal("Accepted before source expiry", unchanged.Scalars.Name);
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await database.Set<ProjectDefinitionReceiptRecord>().CountAsync());

        static byte[] Encode(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> result)
            => ProjectDefinitionOperationCodec.EncodeAccepted(result, ProjectDefinitionLimits.VersionOneMaximum).ToArray();
    }

    private static ProjectDefinitionInvocation DefinitionInvocation(IServiceProvider services, ProjectProcessMutationAdmission admission,
        ProjectDefinitionExpectation expected) {
        var factory = new ProjectDefinitionGovernedInvocationFactory(services.GetRequiredService<ProjectAgentSourceMutationAuthority>(),
            services.GetRequiredService<ProjectProcessExecutionMutationService>(), services.GetRequiredService<IAgentCatalogReadLeaseStore>(),
            services.GetRequiredService<ProjectWriteAdmissionService>());
        return factory.ForProcess(admission, ProjectProcessProjectOperation.Update, [expected.Project]);
    }

    private sealed class DefinitionOperationLockRendezvous : DbCommandInterceptor {
        private int arrivals;
        private int missingLockedReceipts;
        public bool Armed { get; set; }
        public int MissingLockedReceipts => Volatile.Read(ref missingLockedReceipts);
        public List<Guid> SerializableContexts { get; } = [];
        public TaskCompletionSource BothTransactions { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) &&
                command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string key &&
                    key.StartsWith("projects:definition-operation:", StringComparison.Ordinal))) {
                Assert.Equal(IsolationLevel.Serializable, command.Transaction!.IsolationLevel);
                lock (SerializableContexts) {
                    SerializableContexts.Add(eventData.Context!.ContextId.InstanceId);
                }
                if (Interlocked.Increment(ref arrivals) == 2) {
                    BothTransactions.TrySetResult();
                }
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Armed && command.Transaction is not null && !result.HasRows &&
                command.CommandText.Contains("FROM \"Projects_DefinitionReceipts\"", StringComparison.Ordinal)) {
                Interlocked.Increment(ref missingLockedReceipts);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CountedDefinitionSource(IProjectMutationSourceAuthority source) : IProjectMutationSourceAuthority {
        public int Acquisitions { get; private set; }
        public Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default) {
            Acquisitions++;
            return source.AcquireAsync(target, cancellationToken);
        }
    }

    private sealed class IndependentDefinitionSource(IReadOnlyCollection<ProjectWriteAdmission> projects) : IProjectMutationSourceAuthority {
        public Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default) {
            Assert.Equal(ProjectMutationPurpose.ExistingWrite, target.Purpose);
            Assert.Equal(projects.Single(), target.ExistingProjects.Single());
            return Task.FromResult<IProjectMutationSourceLease>(new IndependentDefinitionLease(projects));
        }
    }

    private sealed class IndependentDefinitionLease(IReadOnlyCollection<ProjectWriteAdmission> projects) : IProjectMutationSourceLease {
        public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects { get; } = projects;
        public Task RequireForMutationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
