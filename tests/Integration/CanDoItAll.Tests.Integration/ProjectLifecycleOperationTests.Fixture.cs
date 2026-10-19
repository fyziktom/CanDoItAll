using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    private static async Task WithFixtureAsync(Func<LifecycleFixture, Task> action, OwnerFaults? faults = null, bool nativeParticipants = false) {
        LifecycleFixture? fixture = null;
        Exception? primary = null;
        try {
            fixture = await LifecycleFixture.CreateAsync(faults ?? new(), nativeParticipants: nativeParticipants);
            await action(fixture);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            await DisposeAsync(primary, fixture);
        }
    }

    private static ValueTask DisposeAsync(Exception? primary, params IAsyncDisposable?[] owners) => primary is null
        ? TestFixtureCleanup.DisposeAsync(owners) : TestFixtureCleanup.DisposeAfterFailureAsync(primary, owners);

    private static Task RequireActualWaitAsync(LifecycleFixture fixture, int waiter, int owner, CancellationToken cancellationToken)
        => fixture.ReadAsync(async context => {
            while (!await context.Database.SqlQuery<bool>($"SELECT {owner} = ANY(pg_blocking_pids({waiter})) AS \"Value\"")
                    .SingleAsync(cancellationToken)) {
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            }
            return true;
        });

    private static async Task DrainOwnerTasksAsync(List<Task> tasks, CancellationTokenSource cancellation, Exception? primary) {
        if (primary is not null) {
            await cancellation.CancelAsync();
        }
        var all = Task.WhenAll(tasks);
        try {
            await all;
        } catch (Exception failure) {
            Exception observed = all.Exception ?? failure;
            if (primary is not null) {
                throw new AggregateException("Lifecycle proof and terminal owner drain both failed.", primary, observed);
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(observed).Throw();
        }
    }

    private sealed class LifecycleFixture : IAsyncDisposable {
        private readonly TestApplication application;
        private readonly AsyncServiceScope scope;
        private LifecycleFixture(TestApplication application, AsyncServiceScope scope, CompletionProbe participant, OwnerFaults faults) {
            this.application = application;
            this.scope = scope;
            Participant = participant;
            Faults = faults;
            participant.Services = scope.ServiceProvider;
        }
        internal IServiceProvider Services => scope.ServiceProvider;
        internal TestApplication Application => application;
        internal CompletionProbe Participant { get; }
        internal OwnerFaults Faults { get; }
        internal ProjectLifecycleWriter Writer => Services.GetRequiredService<ProjectLifecycleWriter>();
        internal ProjectLifecycleReceiptReader Reader => Services.GetRequiredService<ProjectLifecycleReceiptReader>();
        internal CurrentWrite Write { get; } = new();
        internal ProjectLifecycleInvocation Invocation => new(ProjectMutationCallerScope.LocalWorkspace, new ReceiptAccess(), Write);
        internal static async Task<LifecycleFixture> CreateAsync(OwnerFaults faults,
            CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null, bool nativeParticipants = false) {
            TestApplication? application = null;
            IAsyncDisposable? scopeOwner = null;
            try {
                var participant = new CompletionProbe(faults);
                application = await TestApplication.CreateAsync(new TestHarnessOptions {
                    TestEnvironment = environment,
                    ActiveProfile = profile,
                    ConfigureServices = services => {
                        if (!nativeParticipants) {
                            services.RemoveAll<IProjectDeletionParticipant>();
                            services.AddSingleton<IProjectDeletionParticipant>(participant);
                        }
                        services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new OwnerFactory(
                            new DbContextOptionsBuilder<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
                                .AddInterceptors(new OwnerCommands(faults), new OwnerTransactions(faults), new OwnerConnections(faults)).Options));
                    }
                });
                var scope = application.Services.CreateAsyncScope();
                scopeOwner = scope;
                return new(application, scope, participant, faults);
            } catch (Exception failure) {
                await TestFixtureCleanup.DisposeAfterFailureAsync(failure, scopeOwner, application);
                throw;
            }
        }
        internal async Task<ProjectDefinitionExpectation> SeedAsync(string name) {
            var result = await Services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
                Name = name, Objective = "Original lifecycle proof", CurrentPhase = "Validation"
            });
            Assert.True(result.IsSuccess);
            var profile = Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id;
            return await ReadAsync(async context => {
                var project = await context.Set<Project>().AsNoTracking().SingleAsync(row => row.Id == result.Value);
                context.Add(new ProjectPhase { ProjectId = project.Id, Name = "Retained phase", Goal = "Atomic deletion" });
                context.Add(new ProjectOptionSelection { ProjectId = project.Id, OptionName = "Retained option", Notes = "Atomic deletion" });
                await context.SaveChangesAsync();
                return new ProjectDefinitionExpectation(new(profile, project.Id, project.LifetimeId), project.ConcurrencyToken);
            });
        }
        internal async Task<ProjectLifecycleSubmission> SubmitIntentAsync(ProjectLifecycleCommand command) {
            var current = await Services.GetRequiredService<ProjectLifecycleOperationProtocol>()
                .GetWriteContextAsync(Invocation.Caller);
            return new(new(current.DatabaseProfileId, current.Namespace, current.Epoch, current.CallerBinding, Guid.NewGuid()),
                ProjectLifecycleInterpretation.VersionOne, command);
        }
        internal async Task<T> ReadAsync<T>(Func<ProjectsDbContext, Task<T>> action) {
            ProjectsDbContext? context = null;
            Exception? primary = null;
            try {
                context = await Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
                return await action(context);
            } catch (Exception failure) {
                primary = failure;
                throw;
            } finally {
                await ProjectLifecycleOperationTests.DisposeAsync(primary, context);
            }
        }
        internal Task MutateAsync(Func<ProjectsDbContext, Task> action) => ReadAsync(async context => {
            await action(context);
            return true;
        });
        internal Task<ProjectLifecycleLookup> LookupAsync(ProjectLifecycleSubmission original)
            => Reader.LookupAsync(original.Operation, new(Invocation.Caller, Invocation.ReceiptAccess));
        internal Task<State> StateAsync() => ReadAsync(async context => new State(
            await context.Set<Project>().AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync(),
            await context.Set<ProjectHierarchyLink>().AsNoTracking().OrderBy(row => row.Id)
                .Select(row => new Edge(row.Id, row.ParentProjectId, row.ChildProjectId)).ToArrayAsync(),
            await context.Set<ProjectLifecycleReceiptRecord>().AsNoTracking().OrderBy(row => row.OperationId)
                .Select(row => row.AcceptedPayload).ToArrayAsync(),
            await context.Set<ProjectRetirementRecord>().AsNoTracking().OrderBy(row => row.ProjectId)
                .Select(row => row.ProjectId).ToArrayAsync(),
            await context.Set<ProjectDeletionCompletionRecord>().CountAsync(),
            await context.Set<Project>().AsNoTracking().OrderBy(row => row.Id).Select(row => new Root(row.Id, row.ConcurrencyToken, row.Name)).ToArrayAsync(),
            await context.Set<ProjectPhase>().AsNoTracking().OrderBy(row => row.Id).Select(row => new Child(row.Id, row.ProjectId, row.Name, row.Goal)).ToArrayAsync(),
            await context.Set<ProjectOptionSelection>().AsNoTracking().OrderBy(row => row.Id).Select(row => new Child(row.Id, row.ProjectId, row.OptionName, row.Notes)).ToArrayAsync()));
        public ValueTask DisposeAsync() => TestFixtureCleanup.DisposeAsync(scope, application);
    }

    private sealed record Edge(Guid Id, Guid Parent, Guid Child);
    private sealed record Root(Guid Id, Guid Revision, string Name);
    private sealed record Child(Guid Id, Guid ProjectId, string Name, string Details);
    private sealed record State(Guid[] Projects, Edge[] Edges, byte[][] Receipts, Guid[] Retired, int Completions,
        Root[] Roots, Child[] Phases, Child[] Options) {
        internal void RequireEqual(State actual) {
            Assert.Equal(Projects, actual.Projects);
            Assert.Equal(Edges, actual.Edges);
            Assert.Equal(Receipts.Length, actual.Receipts.Length);
            for (var index = 0; index < Receipts.Length; index++) {
                Assert.Equal(Receipts[index], actual.Receipts[index]);
            }
            Assert.Equal(Retired, actual.Retired);
            Assert.Equal(Completions, actual.Completions);
            Assert.Equal(Roots, actual.Roots);
            Assert.Equal(Phases, actual.Phases);
            Assert.Equal(Options, actual.Options);
        }
    }

    private sealed class ReceiptAccess : IProjectLifecycleReceiptAccess {
        public Task<bool> CanReadAsync(ProjectLifecycleSubmission original, CancellationToken cancellationToken) => Task.FromResult(true);
    }
    private sealed class CurrentWrite : IProjectLifecycleCurrentWriteAccess {
        internal int Observations;
        internal int DenyAt = int.MaxValue;
        public ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Interlocked.Increment(ref Observations) >= DenyAt
                ? ProjectLifecycleWriteAccessDecision.Denied : ProjectLifecycleWriteAccessDecision.Allowed);
        }
    }
    private sealed class CompletionProbe(OwnerFaults faults) : IProjectDeletionParticipant {
        internal IServiceProvider Services = null!;
        internal ProjectDeletionPreparationContext? Original;
        internal int Prepared;
        internal int Completed;
        internal Guid RecoveryId { get; } = Guid.NewGuid();
        public ProjectDeletionParticipantId Id { get; } = new("lifecycle-owner-proof");
        public int RecoveryProtocolVersion => 1;
        public IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys
            => faults.ReturnNoPreparation ? [new("lifecycle-proof:empty-preparation")] : [];
        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(Guid projectId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The lifecycle owner must call typed preparation.");
        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(ProjectReservationCancellationPreparationContext original,
            CancellationToken cancellationToken = default)
            => PrepareAsync(original.Completion, cancellationToken);
        public Task<ProjectDeletionParticipantPreparation?> PrepareAsync(ProjectDeletionPreparationContext original,
            CancellationToken cancellationToken = default) {
            Original = original;
            Interlocked.Increment(ref Prepared);
            return Task.FromResult<ProjectDeletionParticipantPreparation?>(faults.ReturnNoPreparation
                ? null : new(original.Project.ProjectId, RecoveryId));
        }
        public async Task<ProjectDeletionParticipantCompletion> CompleteAsync(ProjectDeletionParticipantPreparation preparation,
            CancellationToken cancellationToken = default) {
            Assert.NotNull(Original);
            Assert.Equal(Original.Project.ProjectId, preparation.ProjectId);
            Assert.Equal(RecoveryId, preparation.RecoveryId);
            Assert.True(faults.OwnerDisposed);
            Assert.IsType<ProjectDeletionCompletionObservation.Released>(await Services.GetRequiredService<IProjectDeletionCompletionQuery>()
                .ObserveAsync(Original, new(Id, RecoveryId), cancellationToken));
            Interlocked.Increment(ref Completed);
            return ProjectDeletionParticipantCompletion.Empty(RecoveryId);
        }
        public Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectDeletionParticipantRecovery>>([]);
        public Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>>([]);
    }

    private enum FailurePoint { None, ReceiptInsert, CommitAcknowledgement, OwnerDisposal }
    private sealed class OwnerFaults {
        internal bool ReturnNoPreparation;
        internal FailurePoint Failure;
        internal bool Armed;
        internal bool Triggered;
        internal bool Committed;
        internal bool OwnerDisposed;
        internal bool BlockRecovery;
        internal bool RecoveryBlocked;
        internal bool PauseReceipt;
        internal DbContext? OwnerContext;
        internal int OwnerPid;
        internal readonly TaskCompletionSource ReceiptReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseReceipt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<int> WaiterPid = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal async ValueTask BeforeAsync(DbCommand command, DbContext? context, CancellationToken cancellationToken) {
            if (Committed && BlockRecovery && command.Transaction is null &&
                    command.CommandText.Contains("JOIN \"Projects_LifecycleReceipts\" AS r", StringComparison.Ordinal)) {
                RecoveryBlocked = true;
                throw new IOException("Owned original-provider receipt read failure after acknowledged database COMMIT.");
            }
            if (!Armed) {
                return;
            }
            if (ReceiptReached.Task.IsCompleted && !ReferenceEquals(OwnerContext, context) &&
                    command.CommandText.Contains("pg_advisory_lock(", StringComparison.Ordinal)) {
                WaiterPid.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
            }
            if (!command.CommandText.Contains("INSERT INTO \"Projects_LifecycleReceipts\"", StringComparison.Ordinal)) {
                return;
            }
            OwnerContext = context;
            OwnerPid = ((NpgsqlConnection)command.Connection!).ProcessID;
            ReceiptReached.TrySetResult();
            if (Failure == FailurePoint.ReceiptInsert) {
                Triggered = true;
                throw new InvalidOperationException("Owned lifecycle receipt insertion refusal after native effect staging.");
            }
            if (PauseReceipt) {
                await ReleaseReceipt.Task.WaitAsync(cancellationToken);
            }
        }
    }
    private sealed class OwnerFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
        public Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
    private sealed class OwnerCommands(OwnerFaults faults) : DbCommandInterceptor {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            await faults.BeforeAsync(command, eventData.Context, cancellationToken);
            return result;
        }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            await faults.BeforeAsync(command, eventData.Context, cancellationToken);
            return result;
        }
    }
    private sealed class OwnerTransactions(OwnerFaults faults) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (ReferenceEquals(eventData.Context, faults.OwnerContext)) {
                faults.Committed = true;
                if (faults.Failure == FailurePoint.CommitAcknowledgement) {
                    faults.Triggered = true;
                    throw new TimeoutException("Owned loss of the lifecycle commit acknowledgement after real COMMIT.");
                }
            }
            return Task.CompletedTask;
        }
    }
    private sealed class OwnerConnections(OwnerFaults faults) : DbConnectionInterceptor {
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (ReferenceEquals(eventData.Context, faults.OwnerContext)) {
                faults.OwnerDisposed = true;
                if (faults.Committed && faults.Failure == FailurePoint.OwnerDisposal) {
                    faults.Triggered = true;
                    throw new IOException("Owned deletion context disposal acknowledgement failure after commit.");
                }
            }
            return Task.CompletedTask;
        }
    }
}
