using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistoryOwnerPersistenceTests {
    [Fact]
    public async Task Owner_model_contains_exactly_history_records_with_complete_schema_parity() {
        await using var application = await TestApplication.CreateAsync();
        var ownerFactory = application.Services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>();
        var schemaFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var owner = await ownerFactory.CreateDbContextAsync();
        await using var schema = await schemaFactory.CreateDbContextAsync();
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        var schemaModel = schema.GetService<IDesignTimeModel>().Model;
        Type[] ownedTypes = [typeof(HistoryPartitionRow), typeof(HistoryStorageIdentity), typeof(HistoryPolicyRow),
            typeof(HistoryPolicyAuditRow), typeof(HistoryCheckpointRow), typeof(HistoryEntryRow),
            typeof(HistoryDetailRow), typeof(HistoryHostLeaseRow), typeof(HistorySourceRow),
            typeof(HistoryOwnerRow), typeof(HistoryOutboxRow)];
        Assert.Equal(ownedTypes.OrderBy(type => type.Name), ownerModel.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        foreach (var entity in ownerModel.GetEntityTypes()) {
            var completeEntity = Assert.IsAssignableFrom<IEntityType>(schemaModel.FindEntityType(entity.ClrType));
            Assert.Equal(completeEntity.ToDebugString(MetadataDebugStringOptions.LongDefault),
                entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
    }

    [Theory]
    [InlineData(SaveMethod.Synchronous)]
    [InlineData(SaveMethod.SynchronousExplicitAccept)]
    [InlineData(SaveMethod.Asynchronous)]
    [InlineData(SaveMethod.AsynchronousExplicitAccept)]
    public async Task Owner_save_overloads_preserve_existing_guid_stamping(SaveMethod method) {
        await using var application = await TestApplication.CreateAsync();
        var factory = application.Services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>();
        await using var owner = await factory.CreateDbContextAsync();
        var partition = new HistoryPartitionRow();
        var policy = new HistoryPolicyRow { PartitionId = partition.Id };
        var checkpoint = new HistoryCheckpointRow { PartitionId = partition.Id, SourceKind = HistorySourceKind.SimpleChat };
        var entry = new HistoryEntryRow {
            Id = Guid.NewGuid(),
            PartitionId = partition.Id,
            Granularity = HistoryGranularity.LegacyAggregate,
            SortAtUtc = DateTimeOffset.UtcNow
        };
        var source = new HistorySourceRow {
            PartitionId = partition.Id,
            Kind = HistorySourceKind.SimpleChat,
            OwnerId = "stamping-owner",
            EvidenceId = "stamping-evidence"
        };
        var retainedToken = Guid.NewGuid();
        var retainedEntry = new HistoryEntryRow {
            Id = Guid.NewGuid(),
            PartitionId = partition.Id,
            Granularity = HistoryGranularity.LegacyAggregate,
            SortAtUtc = entry.SortAtUtc,
            ConcurrencyToken = retainedToken
        };
        owner.AddRange(partition, policy, checkpoint, entry, source, retainedEntry);
        await SaveAsync(owner, method);
        IHasConcurrencyToken[] stamped = [policy, checkpoint, entry, source];
        Assert.All(stamped, row => Assert.NotEqual(Guid.Empty, row.ConcurrencyToken));
        Assert.Equal(retainedToken, retainedEntry.ConcurrencyToken);
        var originalTokens = stamped.Select(row => row.ConcurrencyToken).ToArray();

        policy.MetadataRetentionDays = 45;
        checkpoint.Cursor = "next-page";
        entry.ProviderName = "Updated provider snapshot";
        source.Version = 1;
        await SaveAsync(owner, method);
        for (var index = 0; index < stamped.Length; index++) {
            Assert.NotEqual(originalTokens[index], stamped[index].ConcurrencyToken);
        }
        Assert.Equal(retainedToken, retainedEntry.ConcurrencyToken);
        await using var readback = await factory.CreateDbContextAsync();
        Assert.Equal(policy.ConcurrencyToken, (await readback.Set<HistoryPolicyRow>().SingleAsync()).ConcurrencyToken);
        Assert.Equal(checkpoint.ConcurrencyToken, (await readback.Set<HistoryCheckpointRow>().SingleAsync()).ConcurrencyToken);
        Assert.Equal(entry.ConcurrencyToken, (await readback.Set<HistoryEntryRow>().SingleAsync(row => row.Id == entry.Id)).ConcurrencyToken);
        Assert.Equal(source.ConcurrencyToken, (await readback.Set<HistorySourceRow>().SingleAsync()).ConcurrencyToken);
    }

    [Fact]
    public async Task Complete_schema_records_and_protected_detail_survive_owner_restart_and_edit() {
        await using var environment = CanDoItAllTestEnvironment.Create("history-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var recordedAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var protection = new EphemeralDataProtectionProvider();
        var options = new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigureServices = services => {
                services.AddSingleton<IDataProtectionProvider>(protection);
                services.AddSingleton<IProviderHistorySecrets, NoHistorySecrets>();
                services.AddSingleton<TimeProvider>(new FixedClock(recordedAt.AddDays(1)));
            }
        };
        var partition = new HistoryPartition(Guid.NewGuid(), Guid.NewGuid(), HistoryStorageIdentity.DefaultSecurityPartition);
        var entryId = HistoryEntryId.New();
        var hostId = Guid.NewGuid();
        var auditId = Guid.NewGuid();
        var originalToken = Guid.NewGuid();
        Guid detailId;
        string protectedText;
        var policy = new HistoryPolicy {
            CaptureMode = HistoryCaptureMode.Detailed,
            MetadataRetentionDays = 90,
            DetailRetentionDays = 90
        };
        var start = new HistoryAttemptStart(entryId, partition, new(0, 0), ProviderRequestId.New(), ProviderAttemptId.New(),
            recordedAt, new(new(Guid.NewGuid()), "Historical provider", "OpenAI", new("historical-model"), new("historical-model")),
            HistoryOperation.CompleteChat, HistoryWorkload.Direct, new(HistoryAuthenticationKind.TrustedLocalOperator), new(policy, 0));
        await using (var beforeRestart = await TestApplication.CreateAsync(options)) {
            var protector = beforeRestart.Services.GetRequiredService<HistoryTextProtector>();
            var detail = await protector.CaptureAsync(start, "Retained historical input", HistoryDetailPart.Input,
                0, recordedAt, policy.MaximumTextBytes, default);
            detailId = detail.Id;
            protectedText = detail.ProtectedText;
            var factory = beforeRestart.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var schema = await factory.CreateDbContextAsync();
            schema.AddRange(
                new HistoryPartitionRow {
                    Id = partition.StorageLineageId,
                    OriginInstanceId = partition.OriginInstanceId,
                    SecurityPartition = partition.SecurityPartition
                },
                new HistoryStorageIdentity { PartitionId = partition.StorageLineageId },
                new HistoryPolicyRow {
                    PartitionId = partition.StorageLineageId,
                    CaptureMode = policy.CaptureMode,
                    MetadataRetentionDays = policy.MetadataRetentionDays,
                    DetailRetentionDays = policy.DetailRetentionDays,
                    UsedDetailBytes = detail.StoredBytes,
                    ConcurrencyToken = originalToken
                },
                new HistoryPolicyAuditRow {
                    Id = auditId,
                    PartitionId = partition.StorageLineageId,
                    ChangedAtUtc = recordedAt,
                    Policy = policy,
                    Caller = start.Caller
                },
                new HistoryCheckpointRow {
                    PartitionId = partition.StorageLineageId,
                    SourceKind = HistorySourceKind.SimpleChat,
                    Coverage = HistoryCoverageState.Partial,
                    Cursor = "historical-cursor",
                    ConcurrencyToken = originalToken
                },
                new HistoryHostLeaseRow {
                    Id = hostId,
                    PartitionId = partition.StorageLineageId,
                    ExpiresAtUtc = recordedAt.AddDays(90)
                },
                detail,
                new HistoryEntryRow {
                    Id = entryId.Value,
                    PartitionId = partition.StorageLineageId,
                    RequestId = start.RequestId.Value,
                    AttemptId = start.AttemptId.Value,
                    CaptureHostId = hostId,
                    Granularity = HistoryGranularity.ProviderCallAttempt,
                    SortAtUtc = recordedAt,
                    TimeBasis = HistoryTimeBasis.AttemptStarted,
                    StartedAtUtc = recordedAt,
                    FinishedAtUtc = recordedAt.AddSeconds(1),
                    ProviderId = start.Provider.Id!.Value.Value,
                    ProviderName = start.Provider.Name,
                    ProviderKind = start.Provider.Kind,
                    RequestedModel = start.Provider.RequestedModel!.Value.Value,
                    ResolvedModel = start.Provider.ResolvedModel!.Value.Value,
                    Outcome = HistoryOutcome.Succeeded,
                    UsageState = HistoryUsageState.Complete,
                    InputTokens = 11,
                    OutputTokens = 7,
                    PriceState = HistoryPriceState.CalculatedAtExecution,
                    Amount = 0.25m,
                    Currency = "USD",
                    PriceHash = "historical-price-hash",
                    PriceVersion = "historical-price-version",
                    MetadataAuthority = HistoryMetadataAuthority.Standalone,
                    RetentionAuthority = HistoryRetentionAuthority.HistoryPolicy,
                    DetailState = HistoryDetailState.Captured,
                    InputDetailId = detailId,
                    ExpiresAtUtc = recordedAt.AddDays(90),
                    ConcurrencyToken = originalToken
                });
            await schema.SaveChangesAsync();
        }

        await using var afterRestart = await TestApplication.CreateAsync(options);
        var partitions = afterRestart.Services.GetRequiredService<HistoryPartitionStore>();
        Assert.Equal(partition, await partitions.GetAsync(default));
        var details = afterRestart.Services.GetRequiredService<HistoryDetailStore>();
        var historyDetail = await details.ReadAsync(partition, entryId, default);
        Assert.Equal(HistoryDetailState.Captured, historyDetail.State);
        Assert.Equal("Retained historical input", historyDetail.Input!.Text);
        var ownerFactory = afterRestart.Services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>();
        await using var owner = await ownerFactory.CreateDbContextAsync();
        var savedEntry = await owner.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(entryId.Value, savedEntry.Id);
        Assert.Equal(start.RequestId.Value, savedEntry.RequestId);
        Assert.Equal(start.AttemptId.Value, savedEntry.AttemptId);
        Assert.Equal(hostId, savedEntry.CaptureHostId);
        Assert.Equal(detailId, savedEntry.InputDetailId);
        Assert.Equal(recordedAt, savedEntry.SortAtUtc);
        Assert.Equal(11, savedEntry.InputTokens);
        Assert.Equal(7, savedEntry.OutputTokens);
        Assert.Equal(0.25m, savedEntry.Amount);
        Assert.Equal("historical-price-hash", savedEntry.PriceHash);
        Assert.Equal("historical-price-version", savedEntry.PriceVersion);
        Assert.Equal(originalToken, savedEntry.ConcurrencyToken);
        var savedDetail = await owner.Set<HistoryDetailRow>().SingleAsync();
        Assert.Equal(detailId, savedDetail.Id);
        Assert.Equal(protectedText, savedDetail.ProtectedText);
        Assert.Equal(auditId, (await owner.Set<HistoryPolicyAuditRow>().SingleAsync()).Id);
        Assert.Equal("historical-cursor", (await owner.Set<HistoryCheckpointRow>().SingleAsync()).Cursor);
        var savedPolicy = await owner.Set<HistoryPolicyRow>().SingleAsync();
        Assert.Equal(savedDetail.StoredBytes, savedPolicy.UsedDetailBytes);
        Assert.Equal(originalToken, savedPolicy.ConcurrencyToken);

        savedEntry.CorrelationId = "owner-edit";
        savedPolicy.MetadataRetentionDays = 120;
        await owner.SaveChangesAsync();
        await using var readback = await ownerFactory.CreateDbContextAsync();
        var editedEntry = await readback.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(entryId.Value, editedEntry.Id);
        Assert.Equal("owner-edit", editedEntry.CorrelationId);
        Assert.NotEqual(originalToken, editedEntry.ConcurrencyToken);
        Assert.Equal(protectedText, (await readback.Set<HistoryDetailRow>().SingleAsync()).ProtectedText);
        Assert.Equal(partition.StorageLineageId, (await readback.Set<HistoryStorageIdentity>().SingleAsync()).PartitionId);
        Assert.Equal(120, (await readback.Set<HistoryPolicyRow>().SingleAsync()).MetadataRetentionDays);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enlisted_writers_follow_the_foreign_owners_final_commit(bool commit) {
        await using var application = await TestApplication.CreateAsync();
        var transactions = application.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var ownerFactory = application.Services.GetRequiredService<IDbContextFactory<CollaborationDbContext>>();
        var historyFactory = application.Services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>();
        var partitions = application.Services.GetRequiredService<HistoryPartitionStore>();
        var outbox = application.Services.GetRequiredService<HistoryOutboxWriter>();
        var projection = application.Services.GetRequiredService<HistoryProjectionWriter>();
        var thread = new CollaborationThreadRecord {
            Subject = "History transaction owner",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastActivityAtUtc = DateTimeOffset.UtcNow
        };
        await using (var owner = await ownerFactory.CreateDbContextAsync()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var participation = transactions.Enter(owner);
            var partition = await partitions.GetForWriteAsync(default);
            var mutation = Mutation(partition);
            await outbox.StageAsync(mutation, default);
            await projection.StageAsync(mutation, default);
            await partitions.RequireForWriteAsync(partition, default);
            owner.Add(thread);
            await owner.SaveChangesAsync();
            if (commit) {
                await transaction.CommitAsync();
            }
        }
        await using var history = await historyFactory.CreateDbContextAsync();
        var expected = commit ? 1 : 0;
        Assert.Equal(expected, await history.Set<HistoryPartitionRow>().CountAsync());
        Assert.Equal(expected, await history.Set<HistoryOutboxRow>().CountAsync());
        Assert.Equal(expected, await history.Set<HistoryEntryRow>().CountAsync());
        Assert.Equal(expected, await history.Set<HistorySourceRow>().CountAsync());
        Assert.Equal(expected, await history.Set<HistoryOwnerRow>().CountAsync());
        await using var readback = await ownerFactory.CreateDbContextAsync();
        Assert.Equal(expected, await readback.Set<CollaborationThreadRecord>().CountAsync(row => row.Id == thread.Id));
    }

    [Fact]
    public async Task Explicit_target_session_writes_only_the_transfer_target_profile() {
        await using var environment = CanDoItAllTestEnvironment.Create("history-owner-target");
        var originalProfile = environment.CreatePostgreSqlProfile("original");
        var targetProfile = environment.CreatePostgreSqlProfile("target");
        await using var original = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = originalProfile
        });
        var originalPartition = await original.Services.GetRequiredService<HistoryPartitionStore>().GetAsync(default);
        await using var target = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = targetProfile
        });
        var targetDatabase = target.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var session = new HistoryTargetWriteSession(targetDatabase.Profile, TimeProvider.System);
        var ownerFactory = target.Services.GetRequiredService<IDbContextFactory<CollaborationDbContext>>();
        HistoryPartition targetPartition;
        await using (var owner = await ownerFactory.CreateDbContextAsync()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var participation = session.Transactions.Enter(owner);
            targetPartition = await session.Partitions.GetForWriteAsync(default);
            var mutation = Mutation(targetPartition);
            Assert.False(await session.HasRetainedSourceOrPendingMutationsAsync(HistorySourceKind.SimpleChat, default));
            await session.Projection.StageAsync(mutation, default);
            Assert.True(await session.HasRetainedSourceOrPendingMutationsAsync(HistorySourceKind.SimpleChat, default));
            Assert.False(await session.HasRetainedSourceOrPendingMutationsAsync(HistorySourceKind.Workflow, default));
            await session.Outbox.StageAsync(mutation, default);
            Assert.True(await session.HasRetainedSourceOrPendingMutationsAsync(HistorySourceKind.Workflow, default));
            var runtimeTransactions = original.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
            Assert.Throws<InvalidOperationException>(() => runtimeTransactions.Enter(owner));
            await transaction.CommitAsync();
        }
        Assert.NotEqual(originalPartition, targetPartition);
        Assert.Equal(targetPartition, await session.Partitions.GetAsync(default));
        var originalFactory = original.Services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>();
        await using var originalReadback = await originalFactory.CreateDbContextAsync();
        Assert.Empty(await originalReadback.Set<HistoryOutboxRow>().ToArrayAsync());
        Assert.Empty(await originalReadback.Set<HistorySourceRow>().ToArrayAsync());
        Assert.Equal(originalPartition.StorageLineageId, (await originalReadback.Set<HistoryPartitionRow>().SingleAsync()).Id);
        var targetFactory = target.Services.GetRequiredService<IDbContextFactory<ProviderHistoryDbContext>>();
        await using var targetReadback = await targetFactory.CreateDbContextAsync();
        Assert.Equal(targetPartition.StorageLineageId, (await targetReadback.Set<HistoryOutboxRow>().SingleAsync()).PartitionId);
    }

    private static HistorySourceMutation Mutation(HistoryPartition partition) {
        var entry = new HistoryEntry(HistoryEntryId.New(), partition, null, null,
            HistoryGranularity.LegacyAggregate, DateTimeOffset.UtcNow, HistoryTimeBasis.CanonicalRecorded, null, null,
            new(null, "Owner fixture", "OpenAI", new("fixture-model"), new("fixture-model")),
            HistoryOperation.CompleteChat, HistoryWorkload.SimpleChat, HistoryOutcome.Succeeded,
            new(HistoryAuthenticationKind.TrustedLocalOperator), new(HistoryUsageState.Unavailable), new(HistoryPriceState.Unpriced),
            HistoryMetadataAuthority.CanonicalProjection, HistoryRetentionAuthority.CanonicalOwner, HistoryDetailState.Canonical) {
            Version = 1
        };
        return new(new(partition, HistorySourceKind.SimpleChat, new("owner-operation"), new("owner-evidence")),
            new(1), HistorySourceMutationKind.Upsert, entry, []);
    }

    private static async Task SaveAsync(ProviderHistoryDbContext context, SaveMethod method) {
        switch (method) {
            case SaveMethod.Synchronous:
                context.SaveChanges();
                break;
            case SaveMethod.SynchronousExplicitAccept:
                context.SaveChanges(acceptAllChangesOnSuccess: true);
                break;
            case SaveMethod.Asynchronous:
                await context.SaveChangesAsync();
                break;
            case SaveMethod.AsynchronousExplicitAccept:
                await context.SaveChangesAsync(acceptAllChangesOnSuccess: true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }
    }

    public enum SaveMethod { Synchronous, SynchronousExplicitAccept, Asynchronous, AsynchronousExplicitAccept }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NoHistorySecrets : IProviderHistorySecrets {
        public Task<IReadOnlyList<string>> GetKnownSecretsAsync(ProviderIdentity provider, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
