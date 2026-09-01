using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using ModelProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using PersistedProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

namespace CanDoItAll.Tests.Integration;

public sealed class SharedProviderPremergeUpgradeTests {
    private const string DevelopmentMigration = "20260822013043_AddWorkflowNativeCheckpointRequestUniqueness";
    private const string ReviewedMigration = "20260830104752_AddProviderHistoryExternalReference";
    private static readonly DateTimeOffset RecordedAt = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DevelopmentToFinalUpgrade_PreservesExistingCanonicalDataAndBuildsHistory() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var files = CanDoItAllTestEnvironment.Create("premerge-upgrade-files");
        await using var database = PostgresTestDatabaseLease.Create("premerge-development-upgrade");
        var factory = new HistoryPersistenceTestDatabase.TestFactory(database.CreateAppDbContextOptions());
        await using var db = factory.CreateDbContext();
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(DevelopmentMigration);
        Assert.Equal(DevelopmentMigration, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Empty(await db.Database.SqlQueryRaw<string>("""
            SELECT table_name AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND
                (table_name LIKE 'ProviderHistory_%' OR table_name LIKE 'Workspace_SharedProvider%'
                 OR table_name = 'Workspace_ProviderSharePublications')
            """).ToArrayAsync());

        var profile = new PersistedProviderProfile {
            Name = "Preserved local provider", ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
            ConfigSchemaVersion = "1.0", BaseUrl = "https://example.invalid/v1",
            DefaultModel = "preserved-model", IsEnabled = true,
            ExtraSettingsJson = """{"preserved":"provider-configuration"}"""
        };
        await InsertAtExistingSchemaAsync(db, profile);
        var operation = await SeedChatAsync(db, profile.Id);
        var workflow = new WorkflowUsageObservationRecordEntity {
            Id = Guid.NewGuid(), RunId = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), VersionId = Guid.NewGuid(),
            NodeId = "preserved-node", ExecutorId = "fixture", ProducerKind = WorkflowUsageProducerKind.Executor,
            InvocationId = Guid.NewGuid(), Attempt = 1, ProviderProfileId = profile.Id,
            ProviderName = "Preserved workflow provider", ProviderNameKey = "PRESERVED WORKFLOW PROVIDER",
            ProviderKind = ModelProviderKind.OpenAi, TransportKind = ProviderTransportKind.Responses,
            Model = "preserved-model", ModelKey = "PRESERVED-MODEL", SourcePhase = "premerge",
            UsageStatus = WorkflowUsageStatus.Observed, PricingStatus = WorkflowPricingStatus.Unknown,
            InputTokens = 23, OutputTokens = 5, TotalTokens = 28,
            StartedAtUtc = RecordedAt, CompletedAtUtc = RecordedAt.AddSeconds(1), RecordedAtUtc = RecordedAt.AddSeconds(1)
        };
        await InsertAtExistingSchemaAsync(db, new WorkflowRunRecordEntity {
            RunId = workflow.RunId, WorkflowId = workflow.WorkflowId, VersionId = workflow.VersionId,
            Summary = "Preserved canonical workflow output", State = WorkflowRunState.Completed,
            CreatedAtUtc = RecordedAt, UpdatedAtUtc = RecordedAt.AddSeconds(1)
        });
        await InsertAtExistingSchemaAsync(db, workflow);

        var scope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
        var layout = new FileSandboxWorkspaceStorageLayout(files.RootPath, scope);
        var agentUsage = new ProviderUsageObservation(
            Guid.NewGuid(), RecordedAt, "Preserved agent", ModelProviderKind.OpenAi, "preserved-model",
            ProviderTransportKind.Responses, ProviderUsageSourcePhases.AgentRuntime,
            ProviderUsageObservationStatus.Observed, 31, 0, 7, 0, 38, 0) {
            ExecutionRunId = Guid.NewGuid(), AgentId = Guid.NewGuid(),
            RawUsageJson = """{"canonical":"must-remain-at-owner"}"""
        };
        var usagePath = Path.Combine(layout.RunUsageRoot(agentUsage.ExecutionRunId!.Value), agentUsage.Id.ToString("N") + ".json");
        await new FileSandboxWorkspaceJsonStore().WriteJsonAtomicallyAsync(usagePath, agentUsage, default);
        var originalBytes = await File.ReadAllBytesAsync(usagePath);

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(ReviewedMigration, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var preservedProfile = await db.Set<PersistedProviderProfile>().SingleAsync();
        Assert.Equal(profile.Id, preservedProfile.Id);
        Assert.Equal(profile.ExtraSettingsJson, preservedProfile.ExtraSettingsJson);
        Assert.Equal(profile.BaseUrl, preservedProfile.BaseUrl);
        Assert.True(preservedProfile.IsEnabled);
        var preservedChat = await db.Set<LlmChatInvocationRecordRow>().SingleAsync();
        Assert.Equal(operation, preservedChat.OperationId);
        Assert.Equal(17, preservedChat.InputTokens);
        Assert.Equal("[]", preservedChat.HistoryAttemptsJson);
        Assert.Null((await db.Set<LlmChatOperationRow>().SingleAsync()).HistoryCaller);
        Assert.Equal("Preserved canonical chat answer", (await db.Set<LlmChatMessageRow>().SingleAsync()).Text);
        var preservedWorkflow = await db.Set<WorkflowUsageObservationRecordEntity>().SingleAsync();
        Assert.Equal(workflow.Id, preservedWorkflow.Id);
        Assert.Equal(23, preservedWorkflow.InputTokens);
        Assert.Equal("null", preservedWorkflow.HistoryEvidenceJson);
        Assert.Equal("Preserved canonical workflow output", (await db.Set<WorkflowRunRecordEntity>().SingleAsync()).Summary);
        Assert.Equal(SHA256.HashData(originalBytes), SHA256.HashData(await File.ReadAllBytesAsync(usagePath)));

        var partition = await new HistoryPartitionStore(factory).GetAsync(default);
        var runtime = new HistoryPersistenceTestDatabase.TestRuntime();
        var maintenance = new HistoryMaintenanceContext(partition, runtime.GetSnapshot(), runtime);
        var outbox = new HistoryOutboxWriter(TimeProvider.System);
        IHistorySourceMaintenance[] sources = [new LlmChatHistorySource(factory, outbox), new WorkflowHistorySource(factory, outbox)];
        foreach (var source in sources) {
            var progress = await source.ProcessAsync(maintenance, null, 10, default);
            Assert.True(progress.BackfillComplete);
        }
        var processor = new HistoryOutboxProcessor(factory, TimeProvider.System, NullLogger<HistoryOutboxProcessor>.Instance);
        Assert.Equal(2, await processor.ProcessAsync(partition, 10, default));
        using var backfill = new FileHistoryBackfill(files.RootPath, scope, partition);
        var complete = false;
        for (var pass = 0; pass < 10 && !complete; pass++) {
            complete = (await backfill.ProcessAsync(10)).AllSourceIntentsStaged;
        }
        Assert.True(complete);
        var journal = new FileProviderHistoryJournal(files.RootPath, scope);
        var batch = await journal.ReadBatchAsync(partition, 10);
        Assert.Single(batch);
        await new AgentHistoryPublicationStore(factory).PublishAsync(partition, scope, batch, default);
        await journal.AcknowledgeAsync(batch[0]);
        Assert.Empty(await journal.ReadBatchAsync(partition, 10));
        db.ChangeTracker.Clear();
        var entries = await db.Set<HistoryEntryRow>().OrderBy(row => row.InputTokens).ToArrayAsync();
        Assert.Equal(new long?[] { 17, 23, 31 }, entries.Select(row => row.InputTokens));
        Assert.All(entries, row => {
            Assert.Equal(HistoryGranularity.LegacyAggregate, row.Granularity);
            Assert.Equal(HistoryRetentionAuthority.CanonicalOwner, row.RetentionAuthority);
            Assert.Equal(HistoryMetadataAuthority.CanonicalProjection, row.MetadataAuthority);
            Assert.Null(row.InputDetailId);
        });
        Assert.Equal(3, await db.Set<HistoryOwnerRow>().CountAsync());
        Assert.Single(await db.Set<AgentHistoryLocator>().ToArrayAsync());
        Assert.Empty(await db.Set<HistoryDetailRow>().ToArrayAsync());
        Assert.Equal(SHA256.HashData(originalBytes), SHA256.HashData(await File.ReadAllBytesAsync(usagePath)));
    }

    private static async Task<Guid> SeedChatAsync(AppDbContext db, Guid profileId) {
        var definition = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        var operation = Guid.NewGuid();
        await InsertAtExistingSchemaAsync(db, new LlmChatDefinitionRow {
            Id = definition, Name = "Preserved chat", CurrentRevision = 1, Status = LlmChatDefinitionStatus.Active,
            CreatedAtUtc = RecordedAt, UpdatedAtUtc = RecordedAt
        });
        await InsertAtExistingSchemaAsync(db, new LlmChatDefinitionRevisionRow {
            DefinitionId = definition, Revision = 1, Name = "Preserved chat",
            SystemPrompt = "Preserved canonical instructions", ProviderProfileId = profileId,
            ProviderKind = ModelProviderKind.OpenAi, ProviderName = "Preserved provider", Model = "preserved-model",
            ModelParameterConfigurationJson = "{}", SettingsFingerprint = new string('a', 64), CreatedAtUtc = RecordedAt
        });
        await InsertAtExistingSchemaAsync(db, new LlmChatTranscriptRow {
            ConversationId = conversation, ProviderId = profileId, ProviderKind = ModelProviderKind.OpenAi,
            ProviderName = "Preserved provider", Model = "preserved-model", TranscriptRevision = 1, EntryCount = 1
        });
        await InsertAtExistingSchemaAsync(db, new LlmChatConversationRow {
            Id = conversation, DefinitionId = definition, DefinitionRevision = 1, Title = "Preserved conversation",
            Status = LlmChatConversationStatus.Active, Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = RecordedAt, UpdatedAtUtc = RecordedAt
        });
        await InsertAtExistingSchemaAsync(db, new LlmChatOperationRow {
            Id = operation, ConversationId = conversation, Kind = LlmChatOperationKind.SendTurn,
            Status = LlmChatOperationStatus.Succeeded, RequestFingerprint = new string('b', 64),
            StartedAtUtc = RecordedAt, CompletedAtUtc = RecordedAt.AddSeconds(1)
        });
        await InsertAtExistingSchemaAsync(db, new LlmChatMessageRow {
            EntryId = Guid.NewGuid(), ConversationId = conversation, TurnId = operation, Sequence = 1,
            Role = LlmMessageRole.Assistant, Text = "Preserved canonical chat answer", CreatedAtUtc = RecordedAt
        });
        await InsertAtExistingSchemaAsync(db, new LlmChatInvocationRecordRow {
            OperationId = operation, Ordinal = 1, ProviderProfileId = profileId, ProviderKind = ModelProviderKind.OpenAi,
            ProviderName = "Preserved provider", Model = "preserved-model", InputTokens = 17, OutputTokens = 4,
            UsageStatus = LlmChatInvocationUsageEvidenceStatus.Observed, Outcome = LlmChatInvocationOutcome.Succeeded,
            StartedAtUtc = RecordedAt, CompletedAtUtc = RecordedAt.AddSeconds(1), CorrelationId = "preserved-correlation"
        });
        return operation;
    }

    private static async Task InsertAtExistingSchemaAsync<T>(AppDbContext db, T record) where T : class {
        var entry = db.Entry(record);
        var table = entry.Metadata.GetTableName()!;
        var columns = (await db.Database.SqlQuery<string>($"""
            SELECT column_name AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = {table}
            """).ToArrayAsync()).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(columns);
        var store = StoreObjectIdentifier.Table(table, entry.Metadata.GetSchema());
        var properties = entry.Properties.Where(property => columns.Contains(property.Metadata.GetColumnName(store)!)).ToArray();
        var names = string.Join(", ", properties.Select(property => "\"" + property.Metadata.GetColumnName(store) + "\""));
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        var values = new List<string>();
        for (var index = 0; index < properties.Length; index++) {
            var property = properties[index];
            var parameterName = "p" + index;
            values.Add("@" + parameterName);
            command.Parameters.Add(property.Metadata.GetRelationalTypeMapping()
                .CreateParameter(command, parameterName, property.CurrentValue));
        }
        command.CommandText = $"INSERT INTO \"{table}\" ({names}) VALUES ({string.Join(", ", values)})";
        await command.ExecuteNonQueryAsync();
        entry.State = EntityState.Detached;
    }
}
