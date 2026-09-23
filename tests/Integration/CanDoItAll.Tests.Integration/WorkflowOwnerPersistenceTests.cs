using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowOwnerPersistenceTests {
    [Fact]
    public async Task Owner_model_preserves_all_mappings_and_physical_prompt_constraints_without_foreign_runtime_entities() {
        await using var application = await TestApplication.CreateAsync();
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
        var canonicalModel = canonical.GetService<IDesignTimeModel>().Model;
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        Assert.Equal(17, ownerModel.GetEntityTypes().Count());
        foreach (var entity in ownerModel.GetEntityTypes()) {
            var complete = Assert.IsAssignableFrom<IEntityType>(canonicalModel.FindEntityType(entity.ClrType));
            if (entity.ClrType != typeof(WorkflowComponentRecord)) {
                Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault),
                    entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
                continue;
            }

            Assert.Equal(complete.GetTableName(), entity.GetTableName());
            Assert.Equal(complete.GetSchema(), entity.GetSchema());
            Assert.Equal(complete.GetProperties().Select(property => DescribeProperty(property, complete)),
                entity.GetProperties().Select(property => DescribeProperty(property, entity)));
            Assert.Equal(complete.GetKeys().Select(key => key.ToDebugString(MetadataDebugStringOptions.LongDefault)),
                entity.GetKeys().Select(key => key.ToDebugString(MetadataDebugStringOptions.LongDefault)));
            Assert.Equal(complete.GetIndexes().Select(index => index.ToDebugString(MetadataDebugStringOptions.LongDefault)),
                entity.GetIndexes().Select(index => index.ToDebugString(MetadataDebugStringOptions.LongDefault)));
            Assert.Empty(entity.GetForeignKeys());
            Assert.Equal(new[] { typeof(PromptArtifact), typeof(PromptVersion) }.OrderBy(type => type.Name),
                complete.GetForeignKeys().Select(key => key.PrincipalEntityType.ClrType).OrderBy(type => type.Name));
        }

        Assert.Throws<InvalidOperationException>(() => owner.Set<PromptArtifact>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<PromptVersion>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<AgentHistoryLocator>().ToQueryString());
        var component = canonicalModel.FindEntityType(typeof(WorkflowComponentRecord))!;
        var expectedConstraints = component.GetForeignKeys().Select(key => key.GetConstraintName()).Order().ToArray();
        var physicalConstraints = await owner.Database.SqlQueryRaw<string>("""
            SELECT c.conname AS "Value"
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE t.relname = 'AgentFramework_WorkflowComponents' AND n.nspname = 'public' AND c.contype = 'f'
            """).ToArrayAsync();
        Assert.Equal(expectedConstraints, physicalConstraints.Order());
        foreach (var foreignKey in component.GetForeignKeys()) {
            var invalid = new WorkflowComponentRecord { Id = Guid.NewGuid(), Name = "Invalid owner binding", Model = "fixture" };
            if (foreignKey.PrincipalEntityType.ClrType == typeof(PromptArtifact)) {
                invalid.PromptArtifactId = Guid.NewGuid();
            } else {
                invalid.PromptVersionId = Guid.NewGuid();
            }

            owner.Add(invalid);
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => owner.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(failure.InnerException);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
            Assert.Equal(foreignKey.GetConstraintName(), postgres.ConstraintName);
            owner.Entry(invalid).State = EntityState.Detached;
        }
    }

    [Fact]
    public async Task All_seventeen_canonical_record_payloads_survive_restart_and_factory_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var rows = CreateRecords();
        var expected = new List<StoredRow>();
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var canonical = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            canonical.AddRange(rows);
            await canonical.SaveChangesAsync();
            canonical.ChangeTracker.Clear();
            for (var index = 0; index < rows.Length; index++) {
                var row = rows[index];
                var entry = canonical.Entry(row);
                var keys = entry.Metadata.FindPrimaryKey()!.Properties.Select(property => entry.Property(property.Name).CurrentValue).ToArray();
                var stored = await canonical.FindAsync(row.GetType(), keys);
                Assert.NotNull(stored);
                rows[index] = stored;
                expected.Add(new(row.GetType(), keys, JsonSerializer.Serialize(stored, row.GetType())));
            }
        }

        await using var restarted = await TestApplication.CreateAsync(harness);
        var factory = restarted.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        await using var owner = await factory.CreateDbContextAsync();
        Assert.Equal(17, expected.Select(row => row.EntityType).Distinct().Count());
        foreach (var row in expected) {
            var restored = await owner.FindAsync(row.EntityType, row.Keys);
            Assert.NotNull(restored);
            Assert.Equal(row.Json, JsonSerializer.Serialize(restored, row.EntityType));
        }

        var run = Assert.Single(rows.OfType<WorkflowRunRecordEntity>());
        var store = new PersistentWorkflowRunStore(factory);
        var restoredRun = Assert.IsType<WorkflowRunSnapshot>(await store.GetRunAsync(new(run.RunId)));
        Assert.Equal(run.ToSnapshot(), restoredRun);
        Assert.Equal(run.Summary, restoredRun.Summary);
        await using var scope = restarted.Services.CreateAsyncScope();
        var floating = scope.ServiceProvider.GetRequiredService<IFloatingAgentChatSettingsService>();
        Assert.Equal(new FloatingAgentChatSettings(25, 9, 7, true, 30), await floating.GetSettingsAsync());
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherOwner = await other.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
        foreach (var row in expected) {
            Assert.Null(await otherOwner.FindAsync(row.EntityType, row.Keys));
        }

        await using var originalAgain = await factory.CreateDbContextAsync();
        Assert.Equal(run.Summary, (await originalAgain.Set<WorkflowRunRecordEntity>().SingleAsync(row => row.RunId == run.RunId)).Summary);
    }

    [Theory]
    [InlineData(ConcurrentRecord.RequestBoundary)]
    [InlineData(ConcurrentRecord.ResponseOperation)]
    public async Task Owner_contexts_reject_stale_GUID_updates_and_keep_request_cascades(ConcurrentRecord record) {
        await using var application = await TestApplication.CreateAsync();
        var rows = CreateRecords();
        var run = Assert.Single(rows.OfType<WorkflowRunRecordEntity>());
        var request = Assert.Single(rows.OfType<WorkflowExternalRequestRecordEntity>());
        var boundary = Assert.Single(rows.OfType<WorkflowExternalRequestBoundaryEntity>());
        var operation = Assert.Single(rows.OfType<WorkflowExternalResponseOperationEntity>());
        await using (var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            canonical.AddRange(run, request, boundary, operation);
            await canonical.SaveChangesAsync();
        }

        var factory = application.Services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        await using var first = await factory.CreateDbContextAsync();
        await using var stale = await factory.CreateDbContextAsync();
        if (record == ConcurrentRecord.RequestBoundary) {
            var current = await first.Set<WorkflowExternalRequestBoundaryEntity>().SingleAsync(row => row.RequestId == request.Id);
            var previous = await stale.Set<WorkflowExternalRequestBoundaryEntity>().SingleAsync(row => row.RequestId == request.Id);
            current.RequestVersion++;
            previous.RequestVersion += 2;
        } else {
            var current = await first.Set<WorkflowExternalResponseOperationEntity>().SingleAsync(row => row.Id == operation.Id);
            var previous = await stale.Set<WorkflowExternalResponseOperationEntity>().SingleAsync(row => row.Id == operation.Id);
            current.OperationVersion++;
            previous.OperationVersion += 2;
        }

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using var verify = await factory.CreateDbContextAsync();
        var persistedBoundary = await verify.Set<WorkflowExternalRequestBoundaryEntity>().SingleAsync(row => row.RequestId == request.Id);
        var persistedOperation = await verify.Set<WorkflowExternalResponseOperationEntity>().SingleAsync(row => row.Id == operation.Id);
        Assert.Equal(boundary.RequestVersion + (record == ConcurrentRecord.RequestBoundary ? 1 : 0), persistedBoundary.RequestVersion);
        Assert.Equal(operation.OperationVersion + (record == ConcurrentRecord.ResponseOperation ? 1 : 0), persistedOperation.OperationVersion);
        Assert.Equal(operation.LeaseEpoch, persistedOperation.LeaseEpoch);
        verify.Remove(await verify.Set<WorkflowExternalRequestRecordEntity>().SingleAsync(row => row.Id == request.Id));
        await verify.SaveChangesAsync();
        Assert.False(await verify.Set<WorkflowExternalRequestBoundaryEntity>().AnyAsync(row => row.RequestId == request.Id));
        Assert.False(await verify.Set<WorkflowExternalResponseOperationEntity>().AnyAsync(row => row.Id == operation.Id));
        Assert.True(await verify.Set<WorkflowRunRecordEntity>().AnyAsync(row => row.RunId == run.RunId));
    }

    private static object[] CreateRecords() {
        var now = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);
        var workflow = Guid.NewGuid();
        var version = Guid.NewGuid();
        var run = Guid.NewGuid();
        var request = Guid.NewGuid();
        var session = $"legacy-owner-session-{Guid.NewGuid():N}";
        var checkpoint = $"legacy-owner-checkpoint-{Guid.NewGuid():N}";
        const string payload = """{"legacy":{"preserve":"opaque payload","array":[1,null,true]}}""";
        var hash = new string('A', 64);
        return [
            new WorkflowDefinitionRecord { WorkflowId = workflow, VersionId = version, Revision = 11, Name = "Legacy owner definition",
                Description = "Original description", DefinitionJson = payload, InstructionSnapshotSchemaVersion = 3,
                CreatedAtUtc = now, UpdatedAtUtc = now },
            new WorkflowDefinitionHeadRecord { WorkflowId = workflow, VersionId = version, ExternalNamespace = "owner-fixture", ExternalKey = workflow.ToString("N") },
            new WorkflowComponentRecord { Id = Guid.NewGuid(), Name = "Legacy owner component", Model = "fixture", ComponentJson = payload,
                PromptGalleryBindingSchemaVersion = 1, CreatedAtUtc = now, UpdatedAtUtc = now },
            new WorkflowSettingsRecord { Id = "floating-agent-chats.v1", SettingsJson = JsonSerializer.Serialize(new FloatingAgentChatSettings(25, 9, 7, true, 30), new JsonSerializerOptions(JsonSerializerDefaults.Web)), UpdatedAtUtc = now },
            new WorkflowRunRecordEntity { RunId = run, WorkflowId = workflow, VersionId = version, State = WorkflowRunState.Completed,
                Backend = WorkflowRuntimeBackendKind.InProcess, BackendRunId = session, Summary = "Original stored result",
                CreatedAtUtc = now, UpdatedAtUtc = now, TerminalAtUtc = now },
            new WorkflowEventRecordEntity { Id = Guid.NewGuid(), RunId = run, NodeId = "legacy-node", Message = "Original event", PayloadJson = payload, CreatedAtUtc = now },
            new WorkflowExternalRequestRecordEntity { Id = request, RunId = run, NodeId = "legacy-node", EventName = "legacy-request",
                RequestJson = payload, ResponseJson = payload, CreatedAtUtc = now, RespondedAtUtc = now },
            new WorkflowCheckpointRecordEntity { Id = Guid.NewGuid(), RunId = run, WorkflowId = workflow, VersionId = version,
                NodeId = "legacy-node", BackendCheckpointId = checkpoint, ExternalRequestId = request, PayloadReference = session,
                PayloadHash = hash, Summary = "Original checkpoint", CreatedAtUtc = now },
            new WorkflowArtifactRecordEntity { Id = Guid.NewGuid(), RunId = run, NodeId = "legacy-node", Name = "Original artifact",
                ContentType = "text/plain", StoragePath = "legacy-relative-path", Summary = payload, CreatedAtUtc = now },
            new WorkflowLaunchIdempotencyRecordEntity { Id = Guid.NewGuid(), CallerKey = $"owner-{Guid.NewGuid():N}", WorkflowId = workflow,
                RequestedVersionId = version, OriginScopeKey = hash, Fingerprint = hash, CanonicalInputHash = hash, ClaimToken = Guid.NewGuid(),
                ReservedRunId = run, ClaimedAtUtc = now, LeaseExpiresAtUtc = now.AddMinutes(1), CompletionJson = payload,
                CompletedAtUtc = now, ReplayCount = 3, LastReplayedAtUtc = now },
            new WorkflowUsageObservationRecordEntity { Id = Guid.NewGuid(), RunId = run, WorkflowId = workflow, VersionId = version,
                NodeId = "legacy-node", InvocationId = Guid.NewGuid(), Attempt = 2, ProviderName = "Fixture", ProviderNameKey = "FIXTURE",
                Model = "fixture", ModelKey = "FIXTURE", SourcePhase = "legacy", InputTokens = 13, OutputTokens = 5, TotalTokens = 18,
                CostUsd = 0.012345m, PricingProfileHash = hash, PricingVersion = "v1", RecordedAtUtc = now, HistoryEvidenceJson = payload },
            new WorkflowExecutorInvocationRecordEntity { Id = Guid.NewGuid(), ScopeKey = hash, InvocationKey = hash, IdempotencyKey = hash,
                RunId = run, WorkflowVersionId = version, NodeId = "legacy-node", ExecutorId = "legacy-executor", ExecutorContractVersion = "v1",
                CausationRequestId = request, CausationRequestVersion = 7, CausationOperationId = Guid.NewGuid(), LogicalGeneration = 9, InputHash = hash,
                Attempt = 2, ConcurrencyVersion = 13, LeaseOwnerId = "legacy-worker", LeaseEpoch = 17, LeaseAcquiredAtUtc = now,
                LeaseExpiresAtUtc = now.AddMinutes(1), ProtectedStoredResult = payload, StoredResultHash = hash, SafeMessage = "Original claim", CreatedAtUtc = now, UpdatedAtUtc = now },
            new WorkflowBackendCheckpointSessionEntity { Id = session, RunId = run, WorkflowId = workflow, WorkflowVersionId = version,
                Format = "legacy-format", FormatVersion = 2, CompilerContractVersion = 3, TopologyFingerprint = hash, NextCommitOrdinal = 5 },
            new WorkflowBackendCheckpointPayloadEntity { Id = checkpoint, SessionId = session, CommitOrdinal = 4, ProtectedPayload = payload,
                PayloadHash = hash, ExternalRequestId = request, BackendRequestId = "legacy-request", BackendRequestPortId = "legacy-port", CreatedAtUtc = now },
            new WorkflowExternalRequestBoundaryEntity { RequestId = request, RequestVersion = 7, ResponseContractJson = payload,
                ContinuationJson = payload, RequestPayloadHash = hash, AuthorizationPolicyJson = payload, CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid() },
            new WorkflowExternalResponseOperationEntity { Id = Guid.NewGuid(), RequestId = request, RunId = run, ExpectedRequestVersion = 7,
                IdempotencyKeyHash = hash, ResponsePayloadHash = hash, ActorScopeFingerprint = hash, ProtectedResponsePayload = payload,
                ActorSubjectId = "legacy-actor", CorrelationId = "legacy-correlation", Attempt = 2, OperationVersion = 13, LeaseEpoch = 17,
                LeaseOwnerId = "legacy-worker", AcceptedAtUtc = now, LeaseAcquiredAtUtc = now, LeaseExpiresAtUtc = now.AddMinutes(1),
                SafeMessage = "Original response", FinalResultJson = payload, ReplayCount = 3, LastReplayedAtUtc = now, ConcurrencyToken = Guid.NewGuid() },
            new WorkflowStructureOutputRecord { RunId = run, OccurrencePath = hash, Slot = 2, PlanJson = payload,
                ReceiptJson = payload, NextInspectionAtUtc = now, AssetDispatchStarted = true, StoragePlacementIntentId = Guid.NewGuid(), IsComplete = true }
        ];
    }

    private static object DescribeProperty(IProperty property, IEntityType entity) {
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        return new {
            property.Name, property.ClrType, property.IsNullable, property.IsConcurrencyToken, property.ValueGenerated,
            Column = property.GetColumnName(table), StoreType = property.GetColumnType(), MaximumLength = property.GetMaxLength(),
            Precision = property.GetPrecision(), Scale = property.GetScale(), Unicode = property.IsUnicode(),
            DefaultValue = property.GetDefaultValue(), DefaultSql = property.GetDefaultValueSql(),
            ComputedSql = property.GetComputedColumnSql(), Stored = property.GetIsStored(), Collation = property.GetCollation(),
            BeforeSave = property.GetBeforeSaveBehavior(), AfterSave = property.GetAfterSaveBehavior(),
            ConverterType = property.GetValueConverter()?.GetType()
        };
    }

    private sealed record StoredRow(Type EntityType, object?[] Keys, string Json);

    public enum ConcurrentRecord {
        RequestBoundary,
        ResponseOperation
    }
}
