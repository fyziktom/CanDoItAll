using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PersistedProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class LlmChatDefinitionCreateReceiptIntegrationTests {
    [Fact]
    public async Task Replay_survives_persisted_provider_disablement_through_the_canonical_resolver() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-provider-change");
        await using var application = await database.OpenAsync(null);
        var admission = CreateCommand();
        admission = admission with { Definition = admission.Definition with {
            Model = "gpt-5", Settings = admission.Definition.Settings with { ThinkingEffort = null }
        } };
        var completeFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var seed = await completeFactory.CreateDbContextAsync()) {
            seed.Add(new PersistedProviderProfile {
                Id = admission.Definition.ProviderProfileId, Name = "Receipt provider",
                ConnectorPluginKey = CanDoItAll.Modules.AgentFramework.ProviderManagement.OpenAiProviderAdministrationConnector.PluginKey,
                BaseUrl = "https://provider.example.invalid", DefaultModel = admission.Definition.Model,
                IsEnabled = true, SupportsStructuredOutput = true
            });
            await seed.SaveChangesAsync();
        }

        await RefreshProvidersAsync(application);
        LlmChatDefinitionCreateReceipt original;
        await using (var createScope = application.Services.CreateAsyncScope()) {
            var created = await createScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>()
                .CreateOnceAsync(admission);
            Assert.True(created.IsSuccess);
            original = created.Value!.Receipt;
        }

        await using (var configuration = await completeFactory.CreateDbContextAsync()) {
            Assert.Equal(1, await configuration.Set<PersistedProviderProfile>()
                .Where(row => row.Id == admission.Definition.ProviderProfileId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.IsEnabled, false)));
        }

        await RefreshProvidersAsync(application);
        await using var replayScope = application.Services.CreateAsyncScope();
        var current = await replayScope.ServiceProvider.GetRequiredService<CanonicalLlmChatProviderResolver>()
            .ResolveAsync(admission.Definition.ProviderProfileId, admission.Definition.Model, null);
        Assert.Equal(LlmChatErrorCodes.ProviderUnavailable, Assert.Single(current.Errors).Code);
        var replay = await replayScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>()
            .CreateOnceAsync(admission);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value!.WasReplay);
        Assert.Equal(original, replay.Value.Receipt);
        await AssertCountsAsync(application, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Independent_instances_competing_for_one_intent_commit_one_effect(bool changedPayload) {
        await using var database = ReceiptDatabase.Create("simple-chat-create-race");
        var admission = CreateCommand();
        var firstResolver = new Resolver { Pause = true };
        var secondResolver = new Resolver { Reject = true };
        var secondCommands = new ClaimCommands();
        await using var first = await database.OpenAsync(firstResolver);
        await using var second = await database.OpenAsync(secondResolver, secondCommands);
        Assert.NotSame(first.Services.GetRequiredService<CoordinatedDatabaseTransaction>(),
            second.Services.GetRequiredService<CoordinatedDatabaseTransaction>());
        await using var firstScope = first.Services.CreateAsyncScope();
        await using var secondScope = second.Services.CreateAsyncScope();
        var firstTask = firstScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>()
            .CreateOnceAsync(admission);
        Task<Result<LlmChatDefinitionCreateResponse>>? secondTask = null;
        try {
            await firstResolver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var secondCommand = changedPayload
                ? admission with { Definition = admission.Definition with { SystemPrompt = "Competing payload" } }
                : admission;
            secondTask = secondScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>()
                .CreateOnceAsync(secondCommand);
            await secondCommands.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(firstTask.IsCompleted);
            Assert.False(secondTask.IsCompleted);
        } finally {
            firstResolver.Release.TrySetResult();
        }

        var created = await firstTask.WaitAsync(TimeSpan.FromSeconds(10));
        var competed = await secondTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(created.IsSuccess);
        Assert.False(created.Value!.WasReplay);
        if (changedPayload) {
            Assert.Equal(LlmChatErrorCodes.DefinitionCreateIntentConflict, Assert.Single(competed.Errors).Code);
        } else {
            Assert.True(competed.IsSuccess);
            Assert.True(competed.Value!.WasReplay);
            Assert.Equal(created.Value.Receipt, competed.Value.Receipt);
        }

        Assert.Equal(1, firstResolver.Calls);
        Assert.Equal(0, secondResolver.Calls);
        await AssertCountsAsync(first, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_acknowledgement_replays_after_restart_without_resolving_current_provider(bool argumentFailure) {
        await using var database = ReceiptDatabase.Create("simple-chat-create-lost-ack");
        var admission = CreateCommand();
        Exception failure = argumentFailure
            ? new ArgumentException("The acknowledgement failed after commit.")
            : new AcknowledgementLostException();
        LlmChatDefinitionCreateReceipt original;
        await using (var before = await database.OpenAsync(new Resolver(), acknowledgementFailure: failure)) {
            await using var scope = before.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
            var thrown = await Record.ExceptionAsync(() => service.CreateOnceAsync(admission));
            Assert.Same(failure, thrown);
            original = (await service.FindReceiptAsync(admission.Key)).Value!;
            Assert.NotNull(original);
            await AssertCountsAsync(before, 1, 1);
        }

        var unavailable = new Resolver { Reject = true };
        await using var restarted = await database.OpenAsync(unavailable);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var receipts = restartedScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
        var recovered = await receipts.FindReceiptAsync(admission.Key);
        var replay = await receipts.CreateOnceAsync(admission);
        Assert.Equal(original, recovered.Value);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value!.WasReplay);
        Assert.Equal(original, replay.Value.Receipt);
        Assert.Equal(0, unavailable.Calls);
        await AssertCountsAsync(restarted, 1, 1);
    }

    [Fact]
    public async Task Replay_preserves_human_revision_and_archive_when_provider_configuration_changes() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-human-edit");
        var resolver = new Resolver();
        await using var application = await database.OpenAsync(resolver);
        var admission = CreateCommand();
        LlmChatDefinitionCreateReceipt original;
        await using (var createScope = application.Services.CreateAsyncScope()) {
            original = (await createScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>()
                .CreateOnceAsync(admission)).Value!.Receipt;
        }

        await using (var humanScope = application.Services.CreateAsyncScope()) {
            var definitions = humanScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionApplicationService>();
            var command = admission.Definition;
            var edited = await definitions.UpdateAsync(new(original.DefinitionId, "Human name", command.Summary,
                command.AvatarImageUrl, "Human prompt", command.ProviderProfileId, command.Model, command.Settings,
                command.Timeout, command.ResponseFormat, "Human revision", 0, ["human"]));
            Assert.True(edited.IsSuccess);
            var archived = await definitions.ChangeStatusAsync(new(original.DefinitionId, LlmChatDefinitionStatus.Archived, 1));
            Assert.True(archived.IsSuccess);
        }

        resolver.Reject = true;
        var callsBeforeReplay = resolver.Calls;
        await using var replayScope = application.Services.CreateAsyncScope();
        var replay = await replayScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>()
            .CreateOnceAsync(admission);
        var current = (await replayScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionApplicationService>()
            .GetAsync(original.DefinitionId)).Value!;
        Assert.Equal(original, replay.Value!.Receipt);
        Assert.True(replay.Value.WasReplay);
        Assert.Equal(1, replay.Value.Receipt.DefinitionRevision.Value);
        Assert.Equal(0, replay.Value.Receipt.OriginalConcurrencyToken);
        Assert.Equal(LlmChatDefinitionStatus.Archived, current.Definition.Status);
        Assert.Equal(2, current.Definition.CurrentRevision.Value);
        Assert.Equal(2, current.Definition.ConcurrencyToken);
        Assert.Equal("Human name", current.Definition.Name);
        Assert.Equal("Human prompt", current.Revision.SystemPrompt);
        Assert.Equal(["human"], current.NormalizedTags);
        Assert.Equal(callsBeforeReplay, resolver.Calls);
        await AssertCountsAsync(application, 1, 1, revisions: 2);
    }

    [Fact]
    public async Task Every_create_field_participates_in_the_semantic_conflict_check() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-payload");
        var resolver = new Resolver();
        await using var application = await database.OpenAsync(resolver);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
        var admission = CreateCommand();
        var original = (await service.CreateOnceAsync(admission)).Value!.Receipt;
        var command = admission.Definition;
        CreateLlmChatDefinitionCommand[] changed = [
            command with { Name = "Other" },
            command with { Summary = "Other summary" },
            command with { AvatarImageUrl = "https://example.invalid/other.png" },
            command with { SystemPrompt = "Other prompt" },
            command with { ProviderProfileId = Guid.NewGuid() },
            command with { Model = "other-model" },
            command with { Settings = command.Settings with { Temperature = 0.7 } },
            command with { Settings = command.Settings with { ThinkingEffort = AgentReasoningEffortLevel.High } },
            command with { Settings = command.Settings with { ModelParameterConfigurationJson = "{\"a\":2}" } },
            command with { Timeout = command.Timeout!.Value + TimeSpan.FromTicks(1) },
            command with { ResponseFormat = null },
            command with { ResponseFormat = command.ResponseFormat! with { RequireJson = false } },
            command with { ResponseFormat = command.ResponseFormat! with { SchemaJson = "{\"type\":\"array\"}" } },
            command with { ResponseFormat = command.ResponseFormat! with { SchemaName = "other-schema" } },
            command with { ResponseFormat = command.ResponseFormat! with { SchemaDescription = "Other description" } },
            command with { RevisionReason = "Other reason" },
            command with { Tags = ["other"] }
        ];
        resolver.Reject = true;
        foreach (var payload in changed) {
            var result = await service.CreateOnceAsync(admission with { Definition = payload });
            Assert.Equal(LlmChatErrorCodes.DefinitionCreateIntentConflict, Assert.Single(result.Errors).Code);
        }

        Assert.Equal(original, (await service.FindReceiptAsync(admission.Key)).Value);
        Assert.Equal(1, resolver.Calls);
        await AssertCountsAsync(application, 1, 1);
    }

    [Fact]
    public async Task Canonical_field_normalization_and_json_property_order_replay_the_original_receipt() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-canonical");
        var resolver = new Resolver();
        await using var application = await database.OpenAsync(resolver);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
        var admission = CreateCommand();
        var original = (await service.CreateOnceAsync(admission)).Value!.Receipt;
        var command = admission.Definition;
        var reordered = command with {
            Name = "  " + command.Name + "  ",
            Summary = command.Summary + "  ",
            AvatarImageUrl = command.AvatarImageUrl + "  ",
            Model = " " + command.Model,
            RevisionReason = " " + command.RevisionReason,
            Tags = [" BETA ", "ALPHA"],
            Settings = command.Settings with { ModelParameterConfigurationJson = "{\"b\":{\"y\":2,\"x\":1},\"a\":1}" },
            ResponseFormat = command.ResponseFormat! with {
                SchemaName = " " + command.ResponseFormat.SchemaName,
                SchemaDescription = command.ResponseFormat.SchemaDescription + " ",
                SchemaJson = "{\"properties\":{\"answer\":{\"type\":\"string\"}},\"type\":\"object\"}"
            }
        };
        var replay = await service.CreateOnceAsync(admission with { Definition = reordered });
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value!.WasReplay);
        Assert.Equal(original, replay.Value.Receipt);
        var duplicate = await service.CreateOnceAsync(new(NewKey(), command with { Tags = ["alpha", " ALPHA "] }));
        Assert.Equal(LlmChatErrorCodes.InvalidRequest, Assert.Single(duplicate.Errors).Code);
        Assert.Equal(1, resolver.Calls);
        await AssertCountsAsync(application, 1, 1);
    }

    [Fact]
    public async Task Intent_is_scoped_by_producer_actor_and_history_namespace_without_name_deduplication() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-scope");
        await using var application = await database.OpenAsync(new Resolver());
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
        var admission = CreateCommand();
        LlmChatDefinitionCreateScope[] scopes = [admission.Key.Scope,
            new("other-producer", "operator", "history"),
            new("producer", "other-actor", "history"),
            new("producer", "operator", "other-history")];
        var ids = new HashSet<LlmChatDefinitionId>();
        foreach (var item in scopes) {
            var result = await service.CreateOnceAsync(admission with { Key = new(item, admission.Key.IntentId) });
            Assert.True(result.IsSuccess);
            Assert.False(result.Value!.WasReplay);
            Assert.True(ids.Add(result.Value.Receipt.DefinitionId));
        }

        Assert.Null((await service.FindReceiptAsync(NewKey())).Value);
        await AssertCountsAsync(application, 4, 4);
        await using var otherDatabase = ReceiptDatabase.Create("simple-chat-create-other-profile");
        await using var otherApplication = await otherDatabase.OpenAsync(new Resolver());
        await using var otherScope = otherApplication.Services.CreateAsyncScope();
        Assert.Null((await otherScope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>()
            .FindReceiptAsync(admission.Key)).Value);
        await AssertCountsAsync(otherApplication, 0, 0);
    }

    [Fact]
    public async Task Deferred_constraint_prevents_orphan_receipts_and_preserves_the_original_revision() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-receipt-fk");
        await using var application = await database.OpenAsync(new Resolver());
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
        var admission = CreateCommand();
        var original = (await service.CreateOnceAsync(admission)).Value!.Receipt;
        var owner = scope.ServiceProvider.GetRequiredService<SimpleChatsDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptRepository>();
        var orphanKey = NewKey();
        await using (var transaction = await owner.Database.BeginTransactionAsync()) {
            var orphan = new LlmChatDefinitionCreateClaim(new(orphanKey, LlmChatDefinitionId.New(), new(1), 0,
                DateTimeOffset.UtcNow), new(new string('a', 64)));
            Assert.True(await repository.TryClaimAsync(orphan));
            var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
            Assert.Equal("FK_LlmChats_CreateReceipt_OriginalRevision", failure.ConstraintName);
        }

        Assert.Null((await service.FindReceiptAsync(orphanKey)).Value);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => owner.Set<LlmChatDefinitionRevisionRow>()
            .Where(row => row.DefinitionId == original.DefinitionId.Value && row.Revision == 1).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deletion.SqlState);
        Assert.Equal("FK_LlmChats_CreateReceipt_OriginalRevision", deletion.ConstraintName);
        Assert.Equal(original, (await service.FindReceiptAsync(admission.Key)).Value);
        await AssertCountsAsync(application, 1, 1);
    }

    [Fact]
    public async Task Fault_after_effect_flush_rolls_back_the_effect_and_raw_receipt_in_the_same_transaction() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-rollback");
        var commands = new ClaimCommands();
        var failure = new FlushFailure(commands, afterFlush: true);
        await using var application = await database.OpenAsync(new Resolver(), commands, failure);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
        var admission = CreateCommand();
        var thrown = await Assert.ThrowsAsync<InjectedCreateFailure>(() => service.CreateOnceAsync(admission));
        Assert.Same(failure.Failure, thrown);
        Assert.True(failure.SawDefinition);
        Assert.True(failure.SawSameTransaction);
        Assert.Null((await service.FindReceiptAsync(admission.Key)).Value);
        await AssertCountsAsync(application, 0, 0);
        var retried = await service.CreateOnceAsync(admission);
        Assert.True(retried.IsSuccess);
        Assert.False(retried.Value!.WasReplay);
        await AssertCountsAsync(application, 1, 1);
    }

    [Fact]
    public async Task Failed_attempt_cleanup_preserves_unrelated_tracking_and_cannot_flush_an_abandoned_definition() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-cleanup");
        var commands = new ClaimCommands();
        var failure = new FlushFailure(commands, afterFlush: false) { Armed = false };
        await using var application = await database.OpenAsync(new Resolver(), commands, failure);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var definitions = services.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var receipts = services.GetRequiredService<ILlmChatDefinitionCreateReceiptService>();
        var command = CreateCommand();
        var unrelated = (await definitions.CreateAsync(command.Definition with { Name = "Unrelated" })).Value!;
        var owner = services.GetRequiredService<SimpleChatsDbContext>();
        var tracked = await owner.Set<LlmChatDefinitionRow>().SingleAsync(row => row.Id == unrelated.Definition.Id.Value);
        tracked.Name = "Unrelated pending edit";
        failure.Armed = true;
        var thrown = await Assert.ThrowsAsync<InjectedCreateFailure>(() => receipts.CreateOnceAsync(command));
        Assert.Same(failure.Failure, thrown);
        Assert.Equal(EntityState.Modified, owner.Entry(tracked).State);
        Assert.DoesNotContain(owner.ChangeTracker.Entries<LlmChatDefinitionRow>(), entry => entry.Entity.Id == failure.AttemptedId);
        Assert.DoesNotContain(owner.ChangeTracker.Entries<LlmChatDefinitionRevisionRow>(), entry => entry.Entity.DefinitionId == failure.AttemptedId);
        Assert.DoesNotContain(owner.ChangeTracker.Entries<LlmChatDefinitionTagRow>(), entry => entry.Entity.DefinitionId == failure.AttemptedId);
        var ordinary = await definitions.CreateAsync(command.Definition with { Name = "After rejection" });
        Assert.True(ordinary.IsSuccess);
        Assert.Null((await receipts.FindReceiptAsync(command.Key)).Value);
        await AssertCountsAsync(application, 2, 0);
        await using var observer = await application.Services.GetRequiredService<IDbContextFactory<SimpleChatsDbContext>>().CreateDbContextAsync();
        Assert.False(await observer.Set<LlmChatDefinitionRow>().AnyAsync(row => row.Id == failure.AttemptedId));
        Assert.Equal("Unrelated pending edit", (await observer.Set<LlmChatDefinitionRow>().SingleAsync(row => row.Id == tracked.Id)).Name);
    }

    [Fact]
    public async Task Rejected_nested_create_cannot_commit_its_reservation_when_the_failure_result_is_swallowed() {
        await using var database = ReceiptDatabase.Create("simple-chat-create-nested-rejection");
        var resolver = new Resolver { Reject = true };
        await using var application = await database.OpenAsync(resolver);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var execution = PushExecution(services);
        var unitOfWork = services.GetRequiredService<ILlmChatUnitOfWork>();
        var writer = services.GetRequiredService<LlmChatDefinitionApplicationService>();
        var callback = false;
        var admission = CreateCommand();
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync(async token => {
            var rejected = await writer.CreateOnceAsync(admission, token);
            Assert.Equal(LlmChatErrorCodes.ProviderNotFound, Assert.Single(rejected.Errors).Code);
            unitOfWork.RegisterPostCommit(() => callback = true);
            return true;
        }));
        Assert.Contains("nested work failed", thrown.Message, StringComparison.Ordinal);
        Assert.NotNull(thrown.InnerException);
        Assert.Contains("reserving its intent", thrown.InnerException.Message, StringComparison.Ordinal);
        Assert.False(callback);
        Assert.Null((await writer.FindReceiptAsync(admission.Key)).Value);
        await AssertCountsAsync(application, 0, 0);
        resolver.Reject = false;
        var retried = await writer.CreateOnceAsync(admission);
        Assert.True(retried.IsSuccess);
        Assert.False(retried.Value!.WasReplay);
        await AssertCountsAsync(application, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nested_exception_preserves_original_cause_and_rolls_back_flushed_work(bool swallow) {
        await using var database = ReceiptDatabase.Create("simple-chat-create-nested-fault");
        await using var application = await database.OpenAsync(new Resolver());
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var execution = PushExecution(services);
        var unitOfWork = services.GetRequiredService<ILlmChatUnitOfWork>();
        var writer = services.GetRequiredService<LlmChatDefinitionApplicationService>();
        var cause = new InjectedCreateFailure();
        var admission = CreateCommand();
        var callback = false;
        var thrown = await Record.ExceptionAsync(() => unitOfWork.ExecuteAsync(async token => {
            try {
                await unitOfWork.ExecuteAsync<bool>(async nestedToken => {
                    var created = await writer.CreateOnceAsync(admission, nestedToken);
                    Assert.True(created.IsSuccess);
                    unitOfWork.RegisterPostCommit(() => callback = true);
                    throw cause;
                }, token);
            } catch (InjectedCreateFailure) when (swallow) {
            }

            return true;
        }));
        if (swallow) {
            Assert.Same(cause, Assert.IsType<InvalidOperationException>(thrown).InnerException);
        } else {
            Assert.Same(cause, thrown);
        }

        Assert.False(callback);
        await AssertCountsAsync(application, 0, 0);
        var retried = await writer.CreateOnceAsync(admission);
        Assert.True(retried.IsSuccess);
        await AssertCountsAsync(application, 1, 1);
    }

    private static LlmChatDefinitionCreateKey NewKey()
        => new(new("producer", "operator", "history"), new(Guid.NewGuid()));

    private static async Task RefreshProvidersAsync(TestApplication application) {
        foreach (var initializer in application.Services.GetServices<IProviderRuntimeProfileSnapshotInitializer>()) {
            await initializer.InitializeAsync();
        }
    }

    private static CreateLlmChatDefinitionOnceCommand CreateCommand()
        => new(NewKey(), new("Owner-created chat", "Original summary", "https://example.invalid/avatar.png",
            "Original prompt", Guid.NewGuid(), "fixture-model",
            new LlmModelSettings(0.3, "{\"a\":1,\"b\":{\"x\":1,\"y\":2}}") { ThinkingEffort = AgentReasoningEffortLevel.Medium },
            TimeSpan.FromSeconds(15),
            new(true, "{\"type\":\"object\",\"properties\":{\"answer\":{\"type\":\"string\"}}}", "answer", "Answer schema"),
            "Initial reason", ["alpha", "beta"]));

    private static IDisposable PushExecution(IServiceProvider services) {
        var runtime = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        return services.GetRequiredService<ILlmChatOperationScopeAccessor>().Push(new(
            LlmChatOperationId.New(), new(runtime.ActiveProfileId!.Value, runtime.ActiveFingerprint!, runtime.Generation)));
    }

    private static async Task AssertCountsAsync(TestApplication application, int definitions, int receipts, int? revisions = null) {
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<SimpleChatsDbContext>>().CreateDbContextAsync();
        Assert.Equal(definitions, await owner.Set<LlmChatDefinitionRow>().CountAsync());
        Assert.Equal(revisions ?? definitions, await owner.Set<LlmChatDefinitionRevisionRow>().CountAsync());
        Assert.Equal(receipts, await owner.Set<LlmChatDefinitionCreateReceiptRow>().CountAsync());
        Assert.Empty(await owner.Set<LlmChatConversationRow>().ToArrayAsync());
        Assert.Empty(await owner.Set<LlmChatTranscriptRow>().ToArrayAsync());
        Assert.Empty(await owner.Set<LlmChatOperationRow>().ToArrayAsync());
    }

    private sealed class ReceiptDatabase : IAsyncDisposable {
        private readonly CanDoItAllTestEnvironment environment;
        private readonly TestDatabaseProfile profile;

        private ReceiptDatabase(string key) {
            environment = CanDoItAllTestEnvironment.Create(key);
            profile = environment.CreatePostgreSqlProfile("owner");
        }

        public static ReceiptDatabase Create(string key) => new(key);

        public Task<TestApplication> OpenAsync(Resolver? resolver, ClaimCommands? commands = null,
            FlushFailure? failure = null, Exception? acknowledgementFailure = null)
            => TestApplication.CreateAsync(new TestHarnessOptions {
                TestEnvironment = environment,
                ActiveProfile = profile,
                ConfigureServices = services => {
                    if (resolver is not null) {
                        services.AddSingleton<ILlmChatProviderResolver>(resolver);
                    }
                    if (commands is not null || failure is not null) {
                        services.AddScoped(provider => {
                            var options = new DbContextOptionsBuilder<SimpleChatsDbContext>(
                                provider.GetRequiredService<DbContextOptions<SimpleChatsDbContext>>());
                            if (commands is not null) {
                                options.AddInterceptors(commands);
                            }

                            if (failure is not null) {
                                options.AddInterceptors(failure);
                            }

                            return new SimpleChatsDbContext(options.Options);
                        });
                    }

                    if (acknowledgementFailure is not null) {
                        services.AddScoped<ILlmChatCommitFence>(provider => new LostAcknowledgementFence(
                            new DatabaseProfileLlmChatCommitFence(provider.GetRequiredService<IDatabaseRuntimeWriteFence>(),
                                provider.GetRequiredService<ILlmChatOperationScopeAccessor>()), acknowledgementFailure));
                    }
                }
            });

        public ValueTask DisposeAsync() => environment.DisposeAsync();
    }

    private sealed class Resolver : ILlmChatProviderResolver {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public bool Reject { get; set; }
        public bool Pause { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Result<LlmChatResolvedProvider>> ResolveAsync(Guid providerProfileId, string model,
            AgentReasoningEffortLevel? thinkingEffort, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref calls);
            Entered.TrySetResult();
            if (Pause) {
                await Release.Task.WaitAsync(cancellationToken);
            }

            if (Reject) {
                return Result<LlmChatResolvedProvider>.Failure(new Error(LlmChatErrorCodes.ProviderNotFound,
                    "The current provider configuration is unavailable."));
            }

            var capability = new ProviderModelThinkingEffortCapability(model,
                AgentThinkingEffortSupportStatus.Supported, AgentThinkingEffortCapabilitySource.Defined,
                Enum.GetValues<AgentReasoningEffortLevel>());
            return Result<LlmChatResolvedProvider>.Success(new(providerProfileId, "Local fixture provider",
                ProviderKind.OpenAi, model, capability, AgentReasoningEffortLevel.Medium));
        }

        public Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<LlmChatProviderOption>>.Success([]));
    }

    private sealed class ClaimCommands : DbCommandInterceptor {
        public TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DbTransaction? Transaction { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("INSERT INTO \"LlmChats_DefinitionCreateReceipts\"", StringComparison.Ordinal)) {
                Transaction = command.Transaction;
                Attempted.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class FlushFailure(ClaimCommands commands, bool afterFlush) : SaveChangesInterceptor {
        public bool Armed { get; set; } = true;
        public bool SawDefinition { get; private set; }
        public bool SawSameTransaction { get; private set; }
        public Guid AttemptedId { get; private set; }
        public InjectedCreateFailure Failure { get; } = new();

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (!afterFlush) {
                Fail(eventData.Context);
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (afterFlush) {
                Fail(eventData.Context);
            }

            return ValueTask.FromResult(result);
        }

        private void Fail(DbContext? context) {
            if (!Armed || context is not SimpleChatsDbContext owner || commands.Transaction is null) {
                return;
            }

            var created = owner.ChangeTracker.Entries<LlmChatDefinitionRow>()
                .Single(entry => entry.Entity.Name == "Owner-created chat");
            AttemptedId = created.Entity.Id;
            SawDefinition = true;
            SawSameTransaction = ReferenceEquals(commands.Transaction, owner.Database.CurrentTransaction!.GetDbTransaction());
            Armed = false;
            throw Failure;
        }
    }

    private sealed class LostAcknowledgementFence(ILlmChatCommitFence inner, Exception failure) : ILlmChatCommitFence {
        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) {
            await inner.ExecuteAsync(operation, cancellationToken);
            throw failure;
        }
    }

    private sealed class InjectedCreateFailure : Exception;
    private sealed class AcknowledgementLostException : Exception;
}
