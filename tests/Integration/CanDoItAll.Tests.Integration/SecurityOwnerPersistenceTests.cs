using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class SecurityOwnerPersistenceTests {
    private const string PluginId = "security-owner-fixture";
    private const string ConnectionId = "primary";

    [Fact]
    public async Task Owner_model_matches_complete_schema_without_inventing_concurrency_tokens() {
        await using var application = await TestApplication.CreateAsync();
        var factory = application.Services.GetRequiredService<IDbContextFactory<SecurityDbContext>>();
        var schemaFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var owner = await factory.CreateDbContextAsync();
        await using var schema = await schemaFactory.CreateDbContextAsync();
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        var schemaModel = schema.GetService<IDesignTimeModel>().Model;
        Type[] ownedTypes = [typeof(SecretRecord), typeof(SecretReference)];
        Assert.Equal(ownedTypes.OrderBy(type => type.Name), ownerModel.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        foreach (var entity in ownerModel.GetEntityTypes()) {
            var complete = Assert.IsAssignableFrom<IEntityType>(schemaModel.FindEntityType(entity.ClrType));
            Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault),
                entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
            Assert.False(typeof(IHasConcurrencyToken).IsAssignableFrom(entity.ClrType));
            Assert.DoesNotContain(entity.GetProperties(), property => property.IsConcurrencyToken);
        }
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
    }

    [Fact]
    public async Task Legacy_secret_and_binding_survive_restart_and_owner_edit_with_same_identities() {
        await using var environment = CanDoItAllTestEnvironment.Create("security-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var protection = new EphemeralDataProtectionProvider();
        var vault = new InMemorySecretVault();
        var options = new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigureServices = services => {
                services.AddSingleton<IDataProtectionProvider>(protection);
                services.AddSingleton<ISecretVault>(vault);
            }
        };
        var secretId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var payload = new DataProtectionSecretProtector(protection).Protect("legacy-owner-fixture-value");
        var consumer = SecretRuntimeConsumerIds.PluginConnection(PluginId, ConnectionId);
        await using (var beforeRestart = await TestApplication.CreateAsync(options)) {
            var factory = beforeRestart.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var schema = await factory.CreateDbContextAsync();
            schema.AddRange(new SecretRecord {
                Id = secretId,
                Name = "Legacy binding",
                Kind = SecretKind.ApiKey,
                EncryptedPayload = payload,
                Scope = "plugin",
                MetadataJson = "{\"fixtureRevision\":1}",
                RotationNote = "Retained rotation note",
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            }, new SecretReference {
                Id = referenceId,
                SecretRecordId = secretId,
                ContextType = SecretRuntimeConsumerTypes.PluginConnection,
                ContextId = consumer,
                Purpose = SecretRuntimePurposes.PluginConnectionSecret
            });
            await schema.SaveChangesAsync();
        }

        await using var afterRestart = await TestApplication.CreateAsync(options);
        var ownerFactory = afterRestart.Services.GetRequiredService<IDbContextFactory<SecurityDbContext>>();
        await using (var readback = await ownerFactory.CreateDbContextAsync()) {
            var legacy = await readback.Set<SecretRecord>().SingleAsync(item => item.Id == secretId);
            Assert.Equal(secretId, legacy.Id);
            Assert.Equal(payload, legacy.EncryptedPayload);
            Assert.Equal(createdAt, legacy.CreatedAtUtc);
            Assert.Equal("{\"fixtureRevision\":1}", legacy.MetadataJson);
            Assert.Equal("Retained rotation note", legacy.RotationNote);
            Assert.Equal(referenceId, (await readback.Set<SecretReference>().SingleAsync(item => item.Id == referenceId)).Id);
        }
        var resolver = afterRestart.Services.GetRequiredService<ISecretRuntimeResolver>();
        var request = new SecretRuntimeRequest(secretId, SecretRuntimePurposes.PluginConnectionSecret,
            ConsumerType: SecretRuntimeConsumerTypes.PluginConnection, ConsumerId: consumer);
        Assert.Equal("legacy-owner-fixture-value", await resolver.ResolveValueAsync(request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveValueAsync(request with {
            ConsumerId = SecretRuntimeConsumerIds.PluginConnection(PluginId, "unbound")
        }));
        await using var scope = afterRestart.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<SecretService>();
        var editor = Assert.IsType<SecretEditorModel>(await service.GetAsync(secretId));
        editor.Name = "Edited legacy binding";
        var saved = await service.SaveAsync(editor);
        Assert.True(saved.IsSuccess);
        Assert.Equal(secretId, saved.Value);
        Assert.Equal("legacy-owner-fixture-value", await resolver.ResolveValueAsync(request));
        await using var final = await ownerFactory.CreateDbContextAsync();
        var record = await final.Set<SecretRecord>().SingleAsync(item => item.Id == secretId);
        Assert.Equal(secretId, record.Id);
        Assert.Equal("Edited legacy binding", record.Name);
        Assert.Equal(createdAt, record.CreatedAtUtc);
        Assert.True(SecretVaultRecordReference.TryParse(record.EncryptedPayload, out _));
        var binding = await final.Set<SecretReference>().SingleAsync(item => item.Id == referenceId);
        Assert.Equal(referenceId, binding.Id);
        Assert.Equal(secretId, binding.SecretRecordId);
        Assert.Equal(consumer, binding.ContextId);
        Assert.Equal(SecretRuntimePurposes.PluginConnectionSecret, binding.Purpose);
    }

    [Fact]
    public async Task Canonical_factories_and_reference_queries_remain_in_their_profile() {
        await using var environment = CanDoItAllTestEnvironment.Create("security-owner-profiles");
        var originalProfile = environment.CreatePostgreSqlProfile("original");
        var targetProfile = environment.CreatePostgreSqlProfile("target");
        await using var original = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = originalProfile
        });
        var originalFactory = original.Services.GetRequiredService<IDbContextFactory<SecurityDbContext>>();
        var originalRecord = new SecretRecord { Name = "Original profile fixture" };
        await using (var owner = await originalFactory.CreateDbContextAsync()) {
            owner.Add(originalRecord);
            await owner.SaveChangesAsync();
        }
        await using var target = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = targetProfile
        });
        var targetFactory = target.Services.GetRequiredService<IDbContextFactory<SecurityDbContext>>();
        var targetRecord = new SecretRecord { Name = "Target profile fixture" };
        await using (var owner = await targetFactory.CreateDbContextAsync()) {
            owner.Add(targetRecord);
            await owner.SaveChangesAsync();
        }
        Guid[] candidates = [originalRecord.Id, targetRecord.Id, Guid.NewGuid(), originalRecord.Id];
        var originalQuery = original.Services.GetRequiredService<SecretReferenceQuery>();
        var targetQuery = target.Services.GetRequiredService<SecretReferenceQuery>();
        Assert.Equal(originalRecord.Id, Assert.Single(await originalQuery.GetExistingIdsAsync(candidates, default)));
        Assert.Equal(targetRecord.Id, Assert.Single(await targetQuery.GetExistingIdsAsync(candidates, default)));
        await using var originalReadback = await originalFactory.CreateDbContextAsync();
        Assert.Equal("Original profile fixture", (await originalReadback.Set<SecretRecord>().SingleAsync(item => item.Id == originalRecord.Id)).Name);
        await using var targetReadback = await targetFactory.CreateDbContextAsync();
        Assert.Equal("Target profile fixture", (await targetReadback.Set<SecretRecord>().SingleAsync(item => item.Id == targetRecord.Id)).Name);
    }

    [Fact]
    public async Task Mutation_reference_read_joins_transaction_while_standalone_read_remains_independent() {
        await using var application = await TestApplication.CreateAsync();
        var transactions = application.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var ownerFactory = application.Services.GetRequiredService<IDbContextFactory<CollaborationDbContext>>();
        var options = application.Services.GetRequiredService<DbContextOptions<SecurityDbContext>>();
        var query = application.Services.GetRequiredService<SecretReferenceQuery>();
        var secret = new SecretRecord { Name = "Uncommitted reference fixture" };
        await using (var owner = await ownerFactory.CreateDbContextAsync()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var coordination = transactions.Enter(owner);
            await using (var security = await transactions.CreateEnlistedAsync(options,
                static value => new SecurityDbContext(value))) {
                security.Add(secret);
                await security.SaveChangesAsync();
            }
            Assert.True(await query.ExistsForMutationAsync(secret.Id, default));
            Assert.Empty(await query.GetExistingIdsAsync([secret.Id], default));
            await transaction.RollbackAsync();
        }
        Assert.Empty(await query.GetExistingIdsAsync([secret.Id], default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.ExistsForMutationAsync(secret.Id, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_keeps_serializable_policy_read_and_runs_vault_callback_after_commit(bool blocked) {
        await using var application = await TestApplication.CreateAsync();
        var factory = application.Services.GetRequiredService<IDbContextFactory<SecurityDbContext>>();
        var options = application.Services.GetRequiredService<DbContextOptions<SecurityDbContext>>();
        var transactions = application.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var policy = new ReferencePolicyProbe(options, transactions, blocked);
        var vault = new ObservingVault();
        var service = new SecretService(factory, vault,
            new DataProtectionSecretProtector(new EphemeralDataProtectionProvider()),
            new SystemClock(), new NullActivityStream(), [policy], transactions);
        var saved = await service.SaveAsync(new SecretEditorModel {
            Name = "Deletion fixture",
            SecretValue = "delete-owner-fixture-value"
        });
        Assert.True(saved.IsSuccess);
        var secretId = saved.Value;
        var query = application.Services.GetRequiredService<SecretReferenceQuery>();
        var callbackEntered = false;
        vault.OnDelete = async cancellationToken => {
            var callbackFactory = application.Services.GetRequiredService<IDbContextFactory<CollaborationDbContext>>();
            await using var callbackOwner = await callbackFactory.CreateDbContextAsync(cancellationToken);
            await using var callbackTransaction = await callbackOwner.Database.BeginTransactionAsync(cancellationToken);
            using var callbackCoordination = transactions.Enter(callbackOwner);
            Assert.False(await query.ExistsForMutationAsync(secretId, cancellationToken));
            await callbackTransaction.CommitAsync(cancellationToken);
            callbackEntered = true;
        };
        if (blocked) {
            var failure = await Assert.ThrowsAsync<SecretDeletionBlockedException>(() => service.DeleteAsync(secretId));
            Assert.Equal(secretId, failure.SecretRecordId);
            Assert.Single(failure.References);
        } else {
            await service.DeleteAsync(secretId);
        }
        Assert.Equal(secretId, policy.ObservedId);
        Assert.Equal(IsolationLevel.Serializable, policy.Isolation);
        Assert.Equal(!blocked, callbackEntered);
        Assert.Equal(blocked ? 0 : 1, vault.DeleteCalls);
        Assert.Equal(blocked ? 1 : 0, (await query.GetExistingIdsAsync([secretId], default)).Count);
    }

    private sealed class ReferencePolicyProbe(DbContextOptions<SecurityDbContext> options,
        CoordinatedDatabaseTransaction transactions, bool blocked) : ISecretDeletionReferencePolicy {
        public Guid? ObservedId { get; private set; }
        public IsolationLevel? Isolation { get; private set; }

        public async Task<SecretDeletionReference?> FindReferenceAsync(Guid secretRecordId, CancellationToken cancellationToken) {
            await using var db = await transactions.CreateEnlistedAsync(options,
                static value => new SecurityDbContext(value), cancellationToken);
            ObservedId = secretRecordId;
            Isolation = db.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel;
            Assert.True(await db.Set<SecretRecord>().AnyAsync(secret => secret.Id == secretRecordId, cancellationToken));
            return blocked ? new("Retained fixture reference.") : null;
        }
    }

    private sealed class ObservingVault : ISecretVault {
        private readonly InMemorySecretVault inner = new();
        public Func<CancellationToken, Task>? OnDelete { get; set; }
        public int DeleteCalls { get; private set; }

        public Task SetAsync(string key, string value, CancellationToken ct = default) => inner.SetAsync(key, value, ct);
        public Task<string?> GetAsync(string key, CancellationToken ct = default) => inner.GetAsync(key, ct);

        public async Task DeleteAsync(string key, CancellationToken ct = default) {
            if (OnDelete is not null) {
                await OnDelete(ct);
            }
            await inner.DeleteAsync(key, ct);
            DeleteCalls++;
        }
    }
}
