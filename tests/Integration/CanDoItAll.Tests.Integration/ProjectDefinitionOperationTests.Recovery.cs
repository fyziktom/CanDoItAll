using System.Data.Common;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectDefinitionOperationTests {
    [Fact]
    public async Task Actual_commit_followed_by_lost_acknowledgement_is_recovered_after_provider_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-operation-restart");
        var profile = environment.CreatePostgreSqlProfile("recovery");
        var fault = new CommittedAcknowledgementFailure();
        ProjectDefinitionRequest<CreateProjectDefinitionCommand> request;
        var invocation = Invocation();
        await using (var first = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigureServices = services => services.ConfigureDbContext<ProjectsDbContext>(
                options => options.AddInterceptors(fault, new UnavailableReceiptRecovery(fault)), ServiceLifetime.Singleton)
        })) {
            await using var scope = first.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            request = new(await NewOperationAsync(services, invocation), Version, Creation());
            fault.Armed = true;

            var outcome = await services.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(request, invocation);

            Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed>(outcome);
            Assert.True(fault.Committed);
            Assert.True(fault.RecoveryBlocked);
        }

        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile });
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var recoveredServices = restartedScope.ServiceProvider;
        var found = Assert.IsType<ProjectDefinitionLookup.FoundFull>(await recoveredServices.GetRequiredService<ProjectDefinitionReceiptReader>()
            .LookupAsync(request.Operation, invocation));
        var retry = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await recoveredServices.GetRequiredService<ProjectDefinitionWriter>()
            .CreateAsync(request, invocation));

        Assert.True(retry.Replayed);
        Assert.Equal(Encoded(found.Result), Encoded(retry.Result));
        await using var database = await DatabaseAsync(recoveredServices);
        var receipt = Assert.Single(await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().ToArrayAsync());
        Assert.Equal(Encoded(found.Result), receipt.AcceptedPayload);
        Assert.Single(await database.Set<Project>().AsNoTracking().ToArrayAsync());
        var current = await CurrentAsync(recoveredServices, receipt.ProjectId);
        Assert.Equal(found.Result.Acknowledgement.Definition, current.Expectation);
        Assert.Single(current.Phases);
        Assert.Single(current.Options);
    }

    [Fact]
    public async Task Actual_commit_with_lost_acknowledgement_and_revoked_disclosure_remains_unconfirmed() {
        var access = new ReceiptAccess();
        var fault = new CommittedAcknowledgementFailure(() => access.Allowed = false);
        await using var application = await TestApplication.CreateAsync(WithInterceptors(fault));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var invocation = Invocation(access);
        var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(
            await NewOperationAsync(services, invocation), Version, Creation());
        var writer = services.GetRequiredService<ProjectDefinitionWriter>();
        fault.Armed = true;

        var outcome = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed>(
            await writer.CreateAsync(request, invocation));

        Assert.True(fault.Committed);
        Assert.False(access.Allowed);
        Assert.Equal(request.Operation, outcome.Operation);
        Assert.Equal(ProjectDefinitionRecoveryReason.CommitUnconfirmed, outcome.Reason);
        Guid projectId;
        Guid revision;
        byte[] acceptedBytes;
        await using (var database = await DatabaseAsync(services)) {
            var project = Assert.Single(await database.Set<Project>().AsNoTracking().ToArrayAsync());
            var receipt = Assert.Single(await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().ToArrayAsync());
            Assert.Equal(request.Operation.Id, receipt.OperationId);
            Assert.Equal(project.Id, receipt.ProjectId);
            Assert.Equal(project.ConcurrencyToken, receipt.AcceptedRevision);
            projectId = project.Id;
            revision = project.ConcurrencyToken;
            acceptedBytes = receipt.AcceptedPayload;
        }
        await using (var search = await services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync()) {
            var projection = Assert.Single(await search.Set<SearchDocument>().AsNoTracking()
                .Where(document => document.SourceType == SearchDocument.ProjectSourceType).ToArrayAsync());
            Assert.Equal(projectId, projection.ProjectId);
        }
        var beforeRetry = await StateAsync(services);
        var lookup = Assert.IsType<ProjectDefinitionLookup.DetailUnavailable>(await services.GetRequiredService<ProjectDefinitionReceiptReader>()
            .LookupAsync(request.Operation, invocation));
        Assert.Equal(ProjectDefinitionDetailUnavailableReason.AccessDenied, lookup.Reason);
        var refusedRetry = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected>(
            await writer.CreateAsync(request, invocation));
        Assert.Contains(refusedRetry.Errors, error => error.Code == ProjectErrorCodes.DefinitionReceiptUnavailable);
        Assert.Equal(beforeRetry, await StateAsync(services));

        access.Allowed = true;
        var replay = Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(
            await writer.CreateAsync(request, invocation));
        Assert.True(replay.Replayed);
        Assert.Equal(acceptedBytes, Encoded(replay.Result));
        Assert.Equal(projectId, replay.Result.Acknowledgement.Project.ProjectId);
        Assert.Equal(revision, replay.Result.Acknowledgement.Definition.Revision);
        Assert.Equal(beforeRetry, await StateAsync(services));
        Assert.Equal(ProjectDefinitionRecoveryReason.CommitUnconfirmed, outcome.Reason);
    }

    [Fact]
    public async Task Duplicate_recovery_with_two_connection_pool_releases_owner_before_fresh_receipt_lookup() {
        await using var lease = PostgresTestDatabaseLease.Create("project-operation-small-pool");
        await using var environment = CanDoItAllTestEnvironment.Create("project-operation-small-pool");
        var connectionString = new NpgsqlConnectionStringBuilder(lease.ConnectionString) { MaxPoolSize = 2 }.ConnectionString;
        var profile = environment.CreatePostgreSqlProfile("small-pool", connectionString);
        var rendezvous = new MissingReceiptRendezvous();
        var owner = new ReplayOwnerLifetime();
        try {
            await using var app = await TestApplication.CreateAsync(new TestHarnessOptions {
                TestEnvironment = environment,
                ActiveProfile = profile,
                ConfigureServices = services => services.ConfigureDbContext<ProjectsDbContext>(options =>
                    options.AddInterceptors(rendezvous, owner, new ReplayConnectionLifetime(owner)), ServiceLifetime.Singleton)
            });
            await using var firstScope = app.Services.CreateAsyncScope();
            await using var secondScope = app.Services.CreateAsyncScope();
            var invocation = Invocation();
            var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(
                await NewOperationAsync(firstScope.ServiceProvider, invocation), Version, Creation());
            rendezvous.Arm();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var first = firstScope.ServiceProvider.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(request, invocation, deadline.Token);
            var second = secondScope.ServiceProvider.GetRequiredService<ProjectDefinitionWriter>().CreateAsync(request, invocation, deadline.Token);
            ProjectDefinitionOutcome<ProjectEditorAcknowledgement>[] outcomes;
            try {
                await rendezvous.BothMissing.Task.WaitAsync(deadline.Token);
            } finally {
                rendezvous.Release.TrySetResult();
                outcomes = await Task.WhenAll(first, second);
            }

            var accepted = outcomes.Select(outcome => Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(outcome)).ToArray();
            Assert.Single(accepted, outcome => outcome.Replayed);
            Assert.Equal(Encoded(accepted[0].Result), Encoded(accepted[1].Result));
            Assert.True(owner.LockedReceiptWasObserved);
            Assert.True(owner.FreshLookupAfterRelease);
            Assert.False(owner.NestedLookupAttempted);
        } finally {
            await using var pool = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(pool);
        }
    }

    private sealed class CommittedAcknowledgementFailure(Action? afterCommit = null) : DbTransactionInterceptor {
        public bool Armed { get; set; }
        public bool Committed { get; private set; }
        public bool RecoveryBlocked { get; set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                Committed = true;
                afterCommit?.Invoke();
                throw new TimeoutException("Owned acknowledgement loss after PostgreSQL COMMIT completed.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class UnavailableReceiptRecovery(CommittedAcknowledgementFailure fault) : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (fault.Committed && command.CommandText.Contains("AS \"ActiveNamespaceId\"", StringComparison.Ordinal)) {
                fault.RecoveryBlocked = true;
                throw new TimeoutException("Owned original-provider receipt lookup outage after committed acknowledgement loss.");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ReplayOwnerLifetime : DbCommandInterceptor {
        private DbConnection? replayOwnerConnection;
        public bool LockedReceiptWasObserved { get; private set; }
        public bool FreshLookupAfterRelease { get; private set; }
        public bool NestedLookupAttempted { get; private set; }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (command.Transaction is not null && result.HasRows &&
                command.CommandText.Contains("FROM \"Projects_DefinitionReceipts\"", StringComparison.Ordinal)) {
                replayOwnerConnection = command.Connection;
                LockedReceiptWasObserved = true;
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (LockedReceiptWasObserved && command.CommandText.Contains("AS \"ActiveNamespaceId\"", StringComparison.Ordinal)) {
                NestedLookupAttempted |= replayOwnerConnection is not null;
                FreshLookupAfterRelease |= replayOwnerConnection is null;
            }
            return ValueTask.FromResult(result);
        }

        public void Closed(DbConnection connection) {
            if (ReferenceEquals(connection, replayOwnerConnection)) {
                replayOwnerConnection = null;
            }
        }
    }

    private sealed class ReplayConnectionLifetime(ReplayOwnerLifetime owner) : DbConnectionInterceptor {
        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            owner.Closed(connection);
            return Task.CompletedTask;
        }
        public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData) => owner.Closed(connection);
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            owner.Closed(connection);
            return Task.CompletedTask;
        }
        public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData) => owner.Closed(connection);
    }
}
