using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class DatabaseTransferCommitOutcomeTests {
    private const long EntryGate = 724091824023;

    public enum FailurePoint { SourceCommit, TargetBeforeCommit, TargetAfterServerCommit, ConnectionClose }

    [Fact]
    public async Task Preflight_disposal_failure_occurs_before_any_handler_effect() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var disposal = new PreflightDisposalFailure();
        var handler = new WritingHandler(target);
        var service = new DatabaseTransferService(new UnusedProfiles(), new ProfileAccessor(source.Profile, target.Profile),
            new PreflightContexts(target.Profile.Profile.Id, disposal), [handler], NullLogger<DatabaseTransferService>.Instance);
        await Assert.ThrowsAsync<InjectedFailure>(() => service.TransferAsync(new() {
            SourceProfileId = source.Profile.Profile.Id, TargetProfileId = target.Profile.Profile.Id, ItemKeys = [handler.Descriptor.Key]
        }));
        Assert.True(disposal.Reached);
        Assert.Equal(0, handler.Calls);
        await using var verify = target.Factory.CreateDbContext();
        Assert.False(await verify.Set<Project>().AnyAsync());
    }

    [Theory]
    [InlineData(FailurePoint.SourceCommit)]
    [InlineData(FailurePoint.TargetBeforeCommit)]
    [InlineData(FailurePoint.TargetAfterServerCommit)]
    [InlineData(FailurePoint.ConnectionClose)]
    public async Task Acknowledged_target_commit_survives_cleanup_but_a_throwing_target_commit_is_never_claimed(FailurePoint point) {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var activation = await target.ActivateDatasetAsync();
        var observation = new Observation();
        var sourceFactory = source.Factory.WithInterceptor(new CommitFailure(point == FailurePoint.SourceCommit, false, observation, false));
        var targetFactory = target.Factory
            .WithInterceptor(new CommitFailure(point == FailurePoint.TargetBeforeCommit, point == FailurePoint.TargetAfterServerCommit, observation, true))
            .WithInterceptor(new CloseFailure(point == FailurePoint.ConnectionClose, observation))
            .WithInterceptor(new GateOrderProbe(observation));
        await using var sourceContext = sourceFactory.CreateDbContext();
        await using var targetContext = targetFactory.CreateDbContext();
        var transfer = new DatabaseTransferOperation(source.Profile, target.Profile, true);
        var owners = new DatabaseTransferOwnerSessionRunner();
        var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, owners, activation: activation);
        var project = new Project { Name = "Transfer commit outcome" };
        var expected = new DatabaseTransferItemResult("fixture", "Fixture", true, "Copied one project.", 1);
        var failure = await Record.ExceptionAsync(() => operations.RunTransferAsync(transfer,
            new(new ProjectTransferReferenceQuery(owners).TargetEntityTypes, [EntryGate]), async (request, token) => {
                Assert.Equal(1, observation.SessionUnlocks);
                observation.EffectStarted = true;
                await using var owner = await owners.CreateTargetAsync<ProjectsDbContext>(request, static options => new(options), token);
                owner.Add(project);
                await owner.SaveChangesAsync(token);
                await using var observer = target.Factory.CreateDbContext();
                await using var observerTransaction = await observer.Database.BeginTransactionAsync(token);
                Assert.False(await observer.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({EntryGate}) AS \"Value\"").SingleAsync(token));
                return expected;
            }));
        Assert.NotNull(failure);
        Assert.Equal(1, observation.SessionUnlocks);
        Assert.Equal(0, observation.SessionUnlocksAfterEffect);
        await using var verify = target.Factory.CreateDbContext();
        Assert.Equal(point != FailurePoint.TargetBeforeCommit, await verify.Set<Project>().AnyAsync(row => row.Id == project.Id));
        await using var gateCheck = await verify.Database.BeginTransactionAsync();
        Assert.True(await verify.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({EntryGate}) AS \"Value\"").SingleAsync());
        if (point is FailurePoint.TargetBeforeCommit or FailurePoint.TargetAfterServerCommit) {
            Assert.IsType<InjectedFailure>(failure);
            return;
        }
        var committed = Assert.IsType<DatabaseTransferCommittedException>(failure);
        Assert.Same(expected, committed.CommittedResult);
        Assert.Equal(source.Profile.Profile.Id, committed.SourceProfileId);
        Assert.Equal(target.Profile.Profile.Id, committed.TargetProfileId);
        var service = new DatabaseTransferService(new UnusedProfiles(), new ProfileAccessor(source.Profile, target.Profile),
            new ProfileAppDbContextFactory(), [new FailedCompletionHandler(committed)], NullLogger<DatabaseTransferService>.Instance);
        var reported = await service.TransferAsync(new() {
            SourceProfileId = source.Profile.Profile.Id, TargetProfileId = target.Profile.Profile.Id, ItemKeys = [expected.Key]
        });
        Assert.True(reported.IsSuccess);
        Assert.True(reported.HasWarnings);
        Assert.Equal(1, reported.RecordsCopied);
        var item = Assert.Single(reported.Items);
        Assert.Equal(DatabaseTransferWarning.PostCommitCleanupIncomplete, item.Warning);
        Assert.Contains("committed", item.Message);
        Assert.Contains("do not repeat", item.Message);
        Assert.DoesNotContain(InjectedFailure.PrivateDiagnostic, item.Message);
    }

    private sealed class Observation {
        public bool EffectStarted { get; set; }
        public bool TargetCommitted { get; set; }
        public int SessionUnlocks { get; set; }
        public int SessionUnlocksAfterEffect { get; set; }
    }

    private sealed class InjectedFailure() : InvalidOperationException(PrivateDiagnostic) {
        public const string PrivateDiagnostic = "Injected private cleanup diagnostic.";
    }

    private sealed class CommitFailure(bool before, bool after, Observation observation, bool target) : DbTransactionInterceptor {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (before) {
                throw new InjectedFailure();
            }
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (target) {
                observation.TargetCommitted = true;
            }
            if (after) {
                throw new InjectedFailure();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CloseFailure(bool enabled, Observation observation) : DbConnectionInterceptor {
        private bool thrown;
        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (enabled && observation.TargetCommitted && !thrown) {
                thrown = true;
                throw new InjectedFailure();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class GateOrderProbe(Observation observation) : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("pg_advisory_unlock(", StringComparison.Ordinal)) {
                observation.SessionUnlocks++;
                if (observation.EffectStarted) {
                    observation.SessionUnlocksAfterEffect++;
                }
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailedCompletionHandler(DatabaseTransferCommittedException exception) : IDatabaseTransferHandler {
        public DatabaseTransferItemDescriptor Descriptor { get; } = new(exception.CommittedResult.Key, exception.CommittedResult.Label, "Fixture.");
        public Task<DatabaseTransferItemPreview> PreviewAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<DatabaseTransferItemResult> TransferAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default)
            => throw exception;
    }

    private sealed class WritingHandler(HistoryPersistenceTestDatabase target) : IDatabaseTransferHandler {
        public DatabaseTransferItemDescriptor Descriptor { get; } = new("fixture", "Fixture", "Fixture.");
        public int Calls { get; private set; }
        public Task<DatabaseTransferItemPreview> PreviewAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public async Task<DatabaseTransferItemResult> TransferAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default) {
            Calls++;
            await using var db = target.Factory.CreateDbContext();
            db.Add(new Project { Name = "Unexpected committed preflight effect" });
            await db.SaveChangesAsync(cancellationToken);
            return new(Descriptor.Key, Descriptor.Label, true, "Committed.", 1);
        }
    }

    private sealed class PreflightContexts(Guid targetId, PreflightDisposalFailure failure) : IProfileAppDbContextFactory {
        public Task<AppDbContext> CreateDbContextForProfileAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default) {
            var options = new DbContextOptionsBuilder<AppDbContext>(AppDbContextOptionsConfigurator.CreateOptions(profile));
            if (profile.Profile.Id == targetId) {
                options.AddInterceptors(failure);
            }
            return Task.FromResult(new AppDbContext(options.Options));
        }
    }

    private sealed class PreflightDisposalFailure : DbConnectionInterceptor {
        public bool Reached { get; private set; }
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            Reached = true;
            throw new InjectedFailure();
        }
    }

    private sealed class ProfileAccessor(ResolvedDatabaseProfile source, ResolvedDatabaseProfile target) : IDatabaseProfileRuntimeAccessor {
        public ResolvedDatabaseProfile ResolveCurrentProfile() => source;
        public ResolvedDatabaseProfile ResolveProfile(Guid profileId) => profileId == source.Profile.Id ? source
            : profileId == target.Profile.Id ? target : throw new InvalidOperationException("Unexpected fixture profile.");
    }

    private sealed class UnusedProfiles : IDatabaseProfileService {
        public Task<IReadOnlyList<DatabaseProfileSummary>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DatabaseProfileEditorModel> GetEditorAsync(Guid? id = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Result Validate(DatabaseProfileEditorModel model) => throw new NotSupportedException();
        public Task<Result<Guid>> SaveAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Guid>> SaveEditorAsync(DatabaseProfileEditorModel model, ResolvedDatabaseProfile runtimeProfile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> DeleteEditorAsync(Guid id, ResolvedDatabaseProfile runtimeProfile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RebindWorkspaceAsync(Guid id, string workspaceRoot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> RollbackWorkspacePathMigrationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
