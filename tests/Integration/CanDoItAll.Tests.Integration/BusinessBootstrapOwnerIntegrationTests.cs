using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

namespace CanDoItAll.Tests.Integration.Persistence;

public sealed class BusinessBootstrapOwnerIntegrationTests {
    private static readonly Guid OpenAiId = Guid.Parse("C1C103DB-707E-3F52-8809-8D804FC171D1");
    private static readonly Guid ChatCompletionsId = Guid.Parse("036B360A-E3F4-8350-97CA-F88DE60BA2BB");
    private static readonly Guid ImageId = Guid.Parse("8958FA61-4BD6-1451-8123-4E4E4FEA2E26");
    private static readonly Guid ComfyId = Guid.Parse("509EAF62-4A4E-1C50-856F-8836328A519E");
    private static readonly Guid OllamaId = Guid.Parse("BD2BFFBB-23D5-D152-82F6-E1D37908B169");
    private static readonly Guid SecretId = Guid.Parse("86F781F1-1E76-4B45-9F1A-42B8CF13D8C7");
    private static readonly DateTimeOffset Epoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Composition_bootstraps_explicit_target_and_repeat_preserves_rows_without_retargeting_canonical_factories() {
        using var environmentKey = new EnvironmentKey("synthetic-target-key");
        var vault = new RecordingVault();
        await using var canonical = await EmptyApplicationAsync(vault);
        await using var target = await EmptyApplicationAsync();
        var canonicalProfile = canonical.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var targetProfile = target.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var original = await SnapshotAsync(canonicalProfile);
        canonical.Services.GetRequiredService<IOptions<ProviderInitializationOptions>>().Value.SeedDefaults = true;
        var bootstrapper = canonical.Services.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);
        var first = await SnapshotAsync(targetProfile);
        await using (var providers = Providers(targetProfile)) {
            Assert.Equal(new[] { OpenAiId, ChatCompletionsId, ImageId, ComfyId, OllamaId }.Order(),
                (await providers.Set<ProviderProfile>().Select(item => item.Id).ToListAsync()).Order());
            Assert.All(await providers.Set<ProviderProfile>().Where(item => item.ApiKeySecretId != null).ToListAsync(),
                item => Assert.Equal(SecretId, item.ApiKeySecretId));
        }
        await using (var workspace = Workspace(targetProfile)) {
            Assert.Equal(OpenAiId, (await workspace.Set<WorkspaceSettings>().SingleAsync()).DefaultProviderProfileId);
        }
        Assert.Equal(1, vault.Sets);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);
        Assert.Equal(first, await SnapshotAsync(targetProfile));
        Assert.Equal(original, await SnapshotAsync(canonicalProfile));
        Assert.Equal(1, vault.Sets);
        Assert.Equal(canonicalProfile.Profile.Id, canonical.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id);
        await using var canonicalFactoryContext = await canonical.Services.GetRequiredService<IDbContextFactory<ProvidersDbContext>>().CreateDbContextAsync();
        Assert.Empty(await canonicalFactoryContext.Set<ProviderProfile>().ToListAsync());
    }

    [Theory]
    [InlineData(OpenAiModelIds.GptImage1Mini, OpenAiModelIds.GptImage2)]
    [InlineData("custom-image-model", "custom-image-model")]
    public async Task Owner_seed_repeats_preserve_customization_and_only_apply_exact_managed_upgrades(string model, string expected) {
        await using var application = await EmptyApplicationAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        await SeedOwnersAsync(application.Services, profile, null);
        await using (var providers = Providers(profile)) {
            var image = await providers.Set<ProviderProfile>().SingleAsync(item => item.Id == ImageId);
            image.DefaultModel = model;
            image.BaseUrl = "https://images.example.invalid/custom";
            image.ExtraSettingsJson = "{\"custom\":true}";
            image.IsEnabled = false;
            image.LastHealthStatus = "operator health note";
            image.LastHealthCheckAtUtc = Epoch;
            var ollama = await providers.Set<ProviderProfile>().SingleAsync(item => item.Id == OllamaId);
            ollama.SupportsStructuredOutput = false;
            await providers.SaveChangesAsync();
        }
        await SeedOwnersAsync(application.Services, profile, null);
        await using (var providers = Providers(profile)) {
            var image = await providers.Set<ProviderProfile>().SingleAsync(item => item.Id == ImageId);
            Assert.Equal(expected, image.DefaultModel);
            Assert.Equal("https://images.example.invalid/custom", image.BaseUrl);
            Assert.Equal("{\"custom\":true}", image.ExtraSettingsJson);
            Assert.False(image.IsEnabled);
            Assert.Equal("operator health note", image.LastHealthStatus);
            Assert.Equal(Epoch, image.LastHealthCheckAtUtc);
            Assert.True((await providers.Set<ProviderProfile>().SingleAsync(item => item.Id == OllamaId)).SupportsStructuredOutput);
        }
        var beforeRepeat = await SnapshotAsync(profile);
        await SeedOwnersAsync(application.Services, profile, null);
        Assert.Equal(beforeRepeat, await SnapshotAsync(profile));
    }

    [Theory]
    [InlineData(DefaultSelection.ExistingCustom)]
    [InlineData(DefaultSelection.Missing)]
    [InlineData(DefaultSelection.Unset)]
    [InlineData(DefaultSelection.NewWorkspace)]
    public async Task Owner_default_selection_preserves_named_provider_identity_and_workspace_fields(DefaultSelection selection) {
        await using var application = await EmptyApplicationAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var namedId = Guid.NewGuid();
        var customId = Guid.NewGuid();
        await using (var providers = Providers(profile)) {
            providers.Add(new ProviderProfile { Id = namedId, Name = ManagedSeedProviderFallbacks.OpenAiDefaultProviderName });
            providers.Add(new ProviderProfile { Id = customId, Name = "Retained custom default" });
            await providers.SaveChangesAsync();
        }
        if (selection != DefaultSelection.NewWorkspace) {
            await using var workspace = Workspace(profile);
            workspace.Add(new WorkspaceSettings {
                WorkspaceName = "Retained workspace", Notes = "Retained notes", DefaultPromptOutputFormat = "JSON",
                CurrencyCode = "EUR", CurrencyCultureName = "de-DE", UpdatedAtUtc = Epoch,
                DefaultProviderProfileId = selection switch {
                    DefaultSelection.ExistingCustom => customId,
                    DefaultSelection.Missing => Guid.NewGuid(),
                    _ => null
                }
            });
            await workspace.SaveChangesAsync();
        }
        await SeedOwnersAsync(application.Services, profile, null);
        await using (var workspace = Workspace(profile)) {
            var settings = await workspace.Set<WorkspaceSettings>().SingleAsync();
            var expected = selection switch {
                DefaultSelection.ExistingCustom => customId,
                DefaultSelection.NewWorkspace => OpenAiId,
                _ => namedId
            };
            Assert.Equal(expected, settings.DefaultProviderProfileId);
            if (selection != DefaultSelection.NewWorkspace) {
                Assert.Equal("Retained workspace", settings.WorkspaceName);
                Assert.Equal("Retained notes", settings.Notes);
                Assert.Equal("JSON", settings.DefaultPromptOutputFormat);
                Assert.Equal("EUR", settings.CurrencyCode);
                Assert.Equal("de-DE", settings.CurrencyCultureName);
                Assert.Equal(selection == DefaultSelection.ExistingCustom ? Epoch : Epoch.AddDays(1), settings.UpdatedAtUtc);
            }
        }
        await using var providerRead = Providers(profile);
        Assert.Equal(namedId, (await providerRead.Set<ProviderProfile>().SingleAsync(item => item.Name == ManagedSeedProviderFallbacks.OpenAiDefaultProviderName)).Id);
        Assert.False(await providerRead.Set<ProviderProfile>().AnyAsync(item => item.Id == OpenAiId));
    }

    [Theory]
    [InlineData(BootstrapFault.ProviderFlush)]
    [InlineData(BootstrapFault.WorkspaceFlush)]
    public async Task Provider_and_workspace_failure_roll_back_together_after_separate_secret_commit(BootstrapFault fault) {
        using var environmentKey = new EnvironmentKey("synthetic-rollback-key");
        await using var application = await EmptyApplicationAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var vault = new RecordingVault();
        var secret = await new EnvironmentSecretBootstrapService(vault).EnsureOpenAiEnvironmentSecretAsync(profile);
        await using (var context = Providers(profile)) {
            var sql = fault == BootstrapFault.ProviderFlush
                ? "ALTER TABLE \"Workspace_ProviderProfiles\" ADD CONSTRAINT \"BootstrapWriteFailure\" CHECK (false)"
                : "ALTER TABLE \"Workspace_Settings\" ADD CONSTRAINT \"BootstrapWriteFailure\" CHECK (false)";
            await context.Database.ExecuteSqlRawAsync(sql);
        }
        var observedUncommittedWorkspace = false;
        await Assert.ThrowsAsync<DbUpdateException>(AttemptAsync);
        Assert.Equal(fault == BootstrapFault.ProviderFlush, observedUncommittedWorkspace);
        await using var providers = Providers(profile);
        await using var workspace = Workspace(profile);
        await using var security = Security(profile);
        Assert.Empty(await providers.Set<ProviderProfile>().ToListAsync());
        Assert.Empty(await workspace.Set<WorkspaceSettings>().ToListAsync());
        var retained = await security.Set<SecretRecord>().SingleAsync();
        Assert.Equal(secret, retained.Id);
        Assert.True(SecretVaultRecordReference.TryParse(retained.EncryptedPayload, out var key));
        Assert.Equal("synthetic-rollback-key", await vault.GetAsync(key));
        Assert.Equal(1, vault.Sets);
        Assert.Equal(0, vault.Deletes);

        async Task AttemptAsync() {
            await using var stage = await application.Services.GetRequiredService<ProviderDefaultsBootstrapService>().PrepareAsync(profile, secret);
            using var participation = stage.EnterTransaction();
            var changed = await application.Services.GetRequiredService<WorkspaceDefaultsBootstrapService>().EnsureAsync(profile, stage.Transactions,
                stage.NewWorkspaceDefaultProviderId, stage.MatchedProviderId, stage.ProviderExistsAsync, Epoch.AddDays(1));
            var options = new DbContextOptionsBuilder<WorkspaceSettingsDbContext>();
            AppDbContextOptionsConfigurator.Configure(options, profile);
            await using var enlisted = await stage.Transactions.CreateEnlistedAsync(options.Options,
                static value => new WorkspaceSettingsDbContext(value));
            Assert.Single(await enlisted.Set<WorkspaceSettings>().ToListAsync());
            await using var outside = Workspace(profile);
            Assert.Empty(await outside.Set<WorkspaceSettings>().ToListAsync());
            observedUncommittedWorkspace = true;
            await stage.CommitAsync(changed);
        }
    }

    [Theory]
    [InlineData(VaultFault.None)]
    [InlineData(VaultFault.ReferenceSave)]
    [InlineData(VaultFault.DeleteOld)]
    public async Task Secret_rotation_preserves_existing_identity_commit_order_and_repeat_semantics(VaultFault fault) {
        using var environmentKey = new EnvironmentKey("  synthetic-new-value  ");
        await using var application = await EmptyApplicationAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var existingId = Guid.NewGuid();
        var oldKey = SecretVaultRecordReference.BuildKey(existingId, Guid.NewGuid());
        await using (var security = Security(profile)) {
            security.Add(new SecretRecord { Id = existingId, Name = "OpenAI API key",
                EncryptedPayload = SecretVaultRecordReference.Create(oldKey), CreatedAtUtc = Epoch, UpdatedAtUtc = Epoch });
            await security.SaveChangesAsync();
            if (fault == VaultFault.ReferenceSave) {
                await security.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Security_SecretRecords\" ADD CONSTRAINT \"BootstrapWriteFailure\" CHECK (\"RotationNote\" IS NULL)");
            }
        }
        var vault = new RecordingVault();
        vault.Values.Add(oldKey, "synthetic-old-value");
        var deleteFailure = new InvalidOperationException("synthetic delete failure");
        var sawCommittedReference = false;
        vault.BeforeDelete = async key => {
            Assert.Equal(oldKey, key);
            await using var independent = Security(profile);
            var row = await independent.Set<SecretRecord>().SingleAsync();
            Assert.NotEqual(SecretVaultRecordReference.Create(oldKey), row.EncryptedPayload);
            Assert.True(SecretVaultRecordReference.TryParse(row.EncryptedPayload, out var newKey));
            Assert.Equal("synthetic-new-value", vault.Values[newKey]);
            sawCommittedReference = true;
            if (fault == VaultFault.DeleteOld) {
                throw deleteFailure;
            }
        };
        var service = new EnvironmentSecretBootstrapService(vault);
        var error = await Record.ExceptionAsync(() => service.EnsureOpenAiEnvironmentSecretAsync(profile));
        if (fault == VaultFault.ReferenceSave) {
            Assert.IsType<DbUpdateException>(error);
            Assert.False(sawCommittedReference);
            Assert.Equal(0, vault.Deletes);
            await using var original = Security(profile);
            Assert.Equal(SecretVaultRecordReference.Create(oldKey), (await original.Set<SecretRecord>().SingleAsync()).EncryptedPayload);
            Assert.Contains(oldKey, vault.Values.Keys);
            return;
        }
        if (fault == VaultFault.DeleteOld) {
            Assert.Same(deleteFailure, error);
            Assert.Contains(oldKey, vault.Values.Keys);
        } else {
            Assert.Null(error);
            Assert.DoesNotContain(oldKey, vault.Values.Keys);
        }
        Assert.True(sawCommittedReference);
        await using (var security = Security(profile)) {
            var row = await security.Set<SecretRecord>().SingleAsync();
            Assert.Equal(existingId, row.Id);
            Assert.Equal(Epoch, row.CreatedAtUtc);
            Assert.Equal(SecretKind.ApiKey, row.Kind);
            Assert.Equal("workspace", row.Scope);
            Assert.Equal("Synchronized from OPENAI_API_KEY.", row.RotationNote);
        }
        var before = await SnapshotAsync(profile);
        Assert.Equal(existingId, await service.EnsureOpenAiEnvironmentSecretAsync(profile));
        Assert.Equal(before, await SnapshotAsync(profile));
        Assert.Equal(1, vault.Sets);
        Assert.Equal(1, vault.Deletes);
    }

    private static Task<TestApplication> EmptyApplicationAsync(ISecretVault? vault = null) => TestApplication.CreateAsync(new TestHarnessOptions {
        ConfigurationOverrides = new Dictionary<string, string?> { [$"{ProviderInitializationOptions.SectionName}:{nameof(ProviderInitializationOptions.SeedDefaults)}"] = "false" },
        ConfigureServices = services => {
            if (vault is not null) {
                services.RemoveAll<ISecretVault>();
                services.AddSingleton(vault);
            }
        }
    });

    private static async Task SeedOwnersAsync(IServiceProvider services, ResolvedDatabaseProfile profile, Guid? secretId) {
        await using var stage = await services.GetRequiredService<ProviderDefaultsBootstrapService>().PrepareAsync(profile, secretId);
        using var participation = stage.EnterTransaction();
        var changed = await services.GetRequiredService<WorkspaceDefaultsBootstrapService>().EnsureAsync(profile, stage.Transactions,
            stage.NewWorkspaceDefaultProviderId, stage.MatchedProviderId, stage.ProviderExistsAsync, Epoch.AddDays(1));
        await stage.CommitAsync(changed);
    }

    private static ProvidersDbContext Providers(ResolvedDatabaseProfile profile) {
        var options = new DbContextOptionsBuilder<ProvidersDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        return new(options.Options);
    }

    private static WorkspaceSettingsDbContext Workspace(ResolvedDatabaseProfile profile) {
        var options = new DbContextOptionsBuilder<WorkspaceSettingsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        return new(options.Options);
    }

    private static SecurityDbContext Security(ResolvedDatabaseProfile profile) {
        var options = new DbContextOptionsBuilder<SecurityDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        return new(options.Options);
    }

    private static async Task<string> SnapshotAsync(ResolvedDatabaseProfile profile) {
        await using var providers = Providers(profile);
        await using var workspace = Workspace(profile);
        await using var security = Security(profile);
        Assert.True(providers.Database.IsNpgsql());
        return JsonSerializer.Serialize(new {
            Providers = await providers.Set<ProviderProfile>().AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
            Workspace = await workspace.Set<WorkspaceSettings>().AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
            Secrets = await security.Set<SecretRecord>().AsNoTracking().OrderBy(item => item.Id).ToListAsync()
        });
    }

    public enum DefaultSelection { ExistingCustom, Missing, Unset, NewWorkspace }
    public enum BootstrapFault { ProviderFlush, WorkspaceFlush }
    public enum VaultFault { None, ReferenceSave, DeleteOld }

    private sealed class EnvironmentKey : IDisposable {
        private const string Name = "OPENAI_API_KEY";
        private readonly string? original = Environment.GetEnvironmentVariable(Name);
        public EnvironmentKey(string value) => Environment.SetEnvironmentVariable(Name, value);
        public void Dispose() => Environment.SetEnvironmentVariable(Name, original);
    }

    private sealed class RecordingVault : ISecretVault {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public Func<string, Task>? BeforeDelete { get; set; }
        public int Sets { get; private set; }
        public int Deletes { get; private set; }
        public Task SetAsync(string key, string value, CancellationToken ct = default) {
            Sets++;
            Values[key] = value;
            return Task.CompletedTask;
        }
        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult<string?>(Values.GetValueOrDefault(key));
        public async Task DeleteAsync(string key, CancellationToken ct = default) {
            Deletes++;
            if (BeforeDelete is not null) {
                await BeforeDelete(key);
            }
            Values.Remove(key);
        }
    }
}
