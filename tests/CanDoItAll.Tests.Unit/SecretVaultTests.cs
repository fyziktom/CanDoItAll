using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class SecretVaultTests
{
    [Fact]
    public async Task InMemorySecretVault_round_trips_and_deletes_secret()
    {
        var vault = new InMemorySecretVault();

        await vault.SetAsync("providers/openai", "test-value");
        var resolved = await vault.GetAsync("providers/openai");

        Assert.Equal("test-value", resolved);

        await vault.DeleteAsync("providers/openai");

        Assert.Null(await vault.GetAsync("providers/openai"));
    }

    [Fact]
    public async Task DataProtectionFileVault_round_trips_and_deletes_secret()
    {
        var root = CreateTempVaultPath();
        try
        {
            var vault = new DataProtectionFileVault(new SecretVaultOptions
            {
                Provider = SecretVaultProviderKind.DataProtectionFile,
                VaultPath = root,
                ApplicationName = "CanDoItAll.Tests"
            });

            await vault.SetAsync("workflow/http/api-key", "file-secret");

            Assert.Equal("file-secret", await vault.GetAsync("workflow/http/api-key"));

            await vault.DeleteAsync("workflow/http/api-key");

            Assert.Null(await vault.GetAsync("workflow/http/api-key"));
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Fact]
    public async Task DpapiSecretVault_round_trips_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTempVaultPath();
        try
        {
            var vault = new DpapiSecretVault(new SecretVaultOptions
            {
                Provider = SecretVaultProviderKind.Dpapi,
                VaultPath = root,
                ApplicationName = "CanDoItAll.Tests"
            });

            await vault.SetAsync("local/provider", "dpapi-secret");

            Assert.Equal("dpapi-secret", await vault.GetAsync("local/provider"));
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [Fact]
    public void SecretVaultFactory_auto_uses_dpapi_on_windows()
    {
        var vault = SecretVaultFactory.CreateDefault(new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.Auto,
            VaultPath = CreateTempVaultPath()
        });

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<DpapiSecretVault>(vault);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.IsType<MacOsKeychainSecretVault>(vault);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.IsType<LinuxSecretServiceVault>(vault);
        }
        else
        {
            Assert.IsType<DataProtectionFileVault>(vault);
        }
    }

    [Fact]
    public async Task UnsupportedSecretVault_fails_explicitly()
    {
        var vault = SecretVaultFactory.CreateDefault(new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.MauiSecureStorage
        });

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            vault.SetAsync("mobile/key", "secret"));

        Assert.Contains("MauiSecureStorage", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecretService_saves_new_secret_as_vault_reference()
    {
        var vault = new InMemorySecretVault();
        var factory = CreateDbContextFactory();
        var sut = CreateSecretService(factory, vault);

        var result = await sut.SaveAsync(new SecretEditorModel
        {
            Name = "OpenAI",
            Kind = SecretKind.ApiKey,
            SecretValue = "sk-test-secret"
        });

        Assert.True(result.IsSuccess);
        var secretId = result.Value;
        await using var dbContext = await factory.CreateDbContextAsync();
        var record = await dbContext.Set<SecretRecord>().SingleAsync(item => item.Id == secretId);

        Assert.DoesNotContain("sk-test-secret", record.EncryptedPayload, StringComparison.Ordinal);
        Assert.True(SecretVaultRecordReference.TryParse(record.EncryptedPayload, out var vaultKey));
        Assert.Equal("sk-test-secret", await vault.GetAsync(vaultKey));

        var editor = await sut.GetAsync(secretId);

        Assert.NotNull(editor);
        Assert.Equal("sk-test-secret", editor.SecretValue);
    }

    [Fact]
    public async Task SecretService_update_replaces_old_vault_payload()
    {
        var vault = new InMemorySecretVault();
        var factory = CreateDbContextFactory();
        var sut = CreateSecretService(factory, vault);
        var result = await sut.SaveAsync(new SecretEditorModel
        {
            Name = "Provider",
            SecretValue = "old-value"
        });
        var secretId = result.Value;
        await using (var dbContext = await factory.CreateDbContextAsync())
        {
            var oldRecord = await dbContext.Set<SecretRecord>().SingleAsync(item => item.Id == secretId);
            Assert.True(SecretVaultRecordReference.TryParse(oldRecord.EncryptedPayload, out var oldVaultKey));

            var updateResult = await sut.SaveAsync(new SecretEditorModel
            {
                Id = secretId,
                Name = "Provider",
                SecretValue = "new-value"
            });

            Assert.True(updateResult.IsSuccess);
            Assert.Null(await vault.GetAsync(oldVaultKey));
        }

        await using var assertContext = await factory.CreateDbContextAsync();
        var updatedRecord = await assertContext.Set<SecretRecord>().SingleAsync(item => item.Id == secretId);
        Assert.True(SecretVaultRecordReference.TryParse(updatedRecord.EncryptedPayload, out var updatedVaultKey));
        Assert.Equal("new-value", await vault.GetAsync(updatedVaultKey));
    }

    [Fact]
    public async Task SecretRuntimeResolver_reads_legacy_data_protection_payloads()
    {
        var factory = CreateDbContextFactory();
        var protector = new TestSecretProtector();
        var secret = new SecretRecord
        {
            Name = "Legacy",
            EncryptedPayload = protector.Protect("legacy-secret"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await using (var dbContext = await factory.CreateDbContextAsync())
        {
            await dbContext.Set<SecretRecord>().AddAsync(secret);
            await dbContext.SaveChangesAsync();
        }

        var resolver = new SecretRuntimeResolver(factory, new InMemorySecretVault(), protector);

        var resolved = await resolver.ResolveValueAsync(new SecretRuntimeRequest(
            secret.Id,
            SecretRuntimePurposes.AgentProviderApiKey));

        Assert.Equal("legacy-secret", resolved);
    }

    [Fact]
    public async Task SecretRuntimeResolver_rejects_secret_outside_allowed_set()
    {
        var factory = CreateDbContextFactory();
        var resolver = new SecretRuntimeResolver(
            factory,
            new InMemorySecretVault(),
            new TestSecretProtector());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveValueAsync(new SecretRuntimeRequest(
                Guid.NewGuid(),
                "workflow-http-header",
                [Guid.NewGuid()])));

        Assert.Contains("not allowed", exception.Message, StringComparison.Ordinal);
    }

    private static string CreateTempVaultPath()
        => Path.Combine(Path.GetTempPath(), "candoitall-secret-vault-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static TestDbContextFactory CreateDbContextFactory()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(SecretRecord).Assembly]);
        var internalServiceProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"secret-vault-{Guid.NewGuid():N}")
            .UseInternalServiceProvider(internalServiceProvider)
            .Options;

        return new TestDbContextFactory(options);
    }

    private static SecretService CreateSecretService(
        IDbContextFactory<AppDbContext> factory,
        ISecretVault vault)
        => new(
            factory,
            vault,
            new TestSecretProtector(),
            new TestClock(new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero)),
            new NullActivityStream());

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(options));
    }

    private sealed class TestSecretProtector : ISecretProtector
    {
        private const string Prefix = "legacy:";

        public string Protect(string plainText) => $"{Prefix}{plainText}";

        public string Unprotect(string protectedValue)
            => protectedValue.StartsWith(Prefix, StringComparison.Ordinal)
                ? protectedValue[Prefix.Length..]
                : throw new InvalidOperationException("Unsupported legacy test payload.");
    }

    private sealed class TestClock(DateTimeOffset currentUtc) : IClock
    {
        public DateTimeOffset GetUtcNow() => currentUtc;
    }
}
