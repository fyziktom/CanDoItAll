using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProvidersOwnershipIntegrationTests {
    [Fact]
    public async Task Owner_keeps_complete_schema_columns_keys_and_indexes_without_mapping_security() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<ProvidersDbContext>();
        var complete = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        var completeModel = complete.GetService<IDesignTimeModel>().Model;
        Assert.Equal(6, ownerModel.GetEntityTypes().Count());
        Assert.Null(ownerModel.FindEntityType(typeof(SecretRecord)));
        foreach (var entity in ownerModel.GetEntityTypes()) {
            var existing = completeModel.FindEntityType(entity.ClrType)!;
            Assert.Equal(existing.GetTableName(), entity.GetTableName());
            Assert.Equal(existing.GetSchema(), entity.GetSchema());
            Assert.Equal(existing.FindPrimaryKey()!.Properties.Select(property => property.Name),
                entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
            Assert.Equal(existing.GetProperties().Select(property => property.Name), entity.GetProperties().Select(property => property.Name));
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            foreach (var property in entity.GetProperties()) {
                var legacy = existing.FindProperty(property.Name)!;
                Assert.Equal(legacy.ClrType, property.ClrType);
                Assert.Equal(legacy.GetColumnName(table), property.GetColumnName(table));
                Assert.Equal(legacy.GetColumnType(table), property.GetColumnType(table));
                Assert.Equal(legacy.IsNullable, property.IsNullable);
                Assert.Equal(legacy.IsConcurrencyToken, property.IsConcurrencyToken);
                Assert.Equal(legacy.ValueGenerated, property.ValueGenerated);
                Assert.Equal(legacy.GetMaxLength(), property.GetMaxLength());
                Assert.Equal(legacy.GetPrecision(), property.GetPrecision());
                Assert.Equal(legacy.GetScale(), property.GetScale());
                Assert.Equal(legacy.GetDefaultValue(), property.GetDefaultValue());
                Assert.Equal(legacy.GetValueConverter()?.ProviderClrType, property.GetValueConverter()?.ProviderClrType);
            }
            Assert.Equal(existing.GetIndexes().Select(index => index.ToDebugString(MetadataDebugStringOptions.LongDefault)),
                entity.GetIndexes().Select(index => index.ToDebugString(MetadataDebugStringOptions.LongDefault)));
        }
        Assert.Single(completeModel.FindEntityType(typeof(SharedProviderSource))!.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(SecretRecord));
    }

    [Fact]
    public async Task Legacy_provider_reads_through_the_owner_after_restart_and_stays_profile_bound() {
        await using var environment = CanDoItAllTestEnvironment.Create("providers-owner-restart");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = original };
        var profile = new ProviderProfile {
            Name = "Legacy owner provider",
            ConnectorPluginKey = OpenAiProviderAdministrationConnector.PluginKey,
            BaseUrl = "https://provider.example.invalid",
            DefaultModel = "fixture-model",
            IsEnabled = false
        };
        await using (var before = await TestApplication.CreateAsync(options)) {
            var factory = before.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var complete = await factory.CreateDbContextAsync();
            complete.Add(profile);
            await complete.SaveChangesAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var restored = await restartedScope.ServiceProvider.GetRequiredService<IProviderAdministrationService>()
            .GetProviderAsync(profile.Id);
        Assert.Equal(profile.Id, restored.Id);
        Assert.Equal(profile.Name, restored.Name);
        await using var owner = await restarted.Services.GetRequiredService<IDbContextFactory<ProvidersDbContext>>().CreateDbContextAsync();
        Assert.Equal(profile.ConcurrencyToken, (await owner.Set<ProviderProfile>().SingleAsync(row => row.Id == profile.Id)).ConcurrencyToken);

        await using var otherApplication = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = other
        });
        await using var otherOwner = await otherApplication.Services.GetRequiredService<IDbContextFactory<ProvidersDbContext>>().CreateDbContextAsync();
        Assert.False(await otherOwner.Set<ProviderProfile>().AnyAsync(row => row.Id == profile.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secret_validation_and_deletion_guards_share_the_owners_serializable_transaction(bool rollback) {
        await using var application = await TestApplication.CreateAsync();
        var completeFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var secret = new SecretRecord { Name = "Provider owner transaction", Kind = SecretKind.Token };
        await using (var complete = await completeFactory.CreateDbContextAsync()) {
            complete.Add(secret);
            await complete.SaveChangesAsync();
        }

        var transactions = application.Services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var recorder = new TransactionRecorder();
        var ownerOptions = new DbContextOptionsBuilder<ProvidersDbContext>(application.Services.GetRequiredService<DbContextOptions<ProvidersDbContext>>())
            .AddInterceptors(recorder).Options;
        var securityOptions = new DbContextOptionsBuilder<SecurityDbContext>(application.Services.GetRequiredService<DbContextOptions<SecurityDbContext>>())
            .AddInterceptors(recorder).Options;
        var secrets = new SecretReferenceQuery(new SecurityFactory(securityOptions), securityOptions, transactions);
        var providerGuard = new ProviderSecretDeletionReferencePolicy(ownerOptions, transactions);
        var sourceGuard = new SharedProviderSourceSecretDeletionReferencePolicy(ownerOptions, transactions);
        var profile = new ProviderProfile { Name = "Uncommitted provider", ApiKeySecretId = secret.Id };
        var source = new SharedProviderSource {
            Name = "Uncommitted source", BaseUri = "https://source.example.invalid", ApiTokenSecretId = secret.Id
        };
        await using (var owner = new ProvidersDbContext(ownerOptions)) {
            await using var mutation = await SerializableMutationScope.BeginAsync(owner,
                SecretMutationScopeKeys.ForSecretRecord(secret.Id), default);
            using var coordination = transactions.Enter(owner);
            var rawTransaction = owner.Database.CurrentTransaction!.GetDbTransaction();
            Assert.Equal(IsolationLevel.Serializable, rawTransaction.IsolationLevel);
            Assert.True(await secrets.ExistsForMutationAsync(secret.Id, default));
            owner.AddRange(profile, source);
            await owner.SaveChangesAsync();
            Assert.NotNull(await providerGuard.FindReferenceAsync(secret.Id, default));
            Assert.NotNull(await sourceGuard.FindReferenceAsync(secret.Id, default));
            Assert.NotEmpty(recorder.Commands);
            Assert.All(recorder.Commands, command => Assert.Same(rawTransaction, command.Transaction));
            var secretRead = Assert.Single(recorder.Commands, command => command.Text.Contains("Security_SecretRecords", StringComparison.Ordinal));
            Assert.DoesNotContain(nameof(SecretRecord.EncryptedPayload), secretRead.Text, StringComparison.Ordinal);
            if (!rollback) {
                await mutation.CommitAsync(default);
            }
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => providerGuard.FindReferenceAsync(secret.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sourceGuard.FindReferenceAsync(secret.Id, default));
        await using var verification = await application.Services.GetRequiredService<IDbContextFactory<ProvidersDbContext>>().CreateDbContextAsync();
        Assert.Equal(!rollback, await verification.Set<ProviderProfile>().AnyAsync(row => row.Id == profile.Id));
        Assert.Equal(!rollback, await verification.Set<SharedProviderSource>().AnyAsync(row => row.Id == source.Id));
        Assert.Contains(secret.Id, await secrets.GetExistingIdsAsync([secret.Id], default));
    }

    private sealed class SecurityFactory(DbContextOptions<SecurityDbContext> options) : IDbContextFactory<SecurityDbContext> {
        public SecurityDbContext CreateDbContext() => new(options);
    }

    private sealed class TransactionRecorder : DbCommandInterceptor {
        public List<(DbTransaction? Transaction, string Text)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.Transaction, command.CommandText));
            return ValueTask.FromResult(result);
        }
    }
}
