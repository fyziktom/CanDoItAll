using System.Security.Cryptography;
using CanDoItAll.Infrastructure;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Tests.Unit;

public sealed class SecretPortabilityTests
{
    private const string Sentinel = "secret-sentinel-a04-7ff954";

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Factory_rejects_insecure_file_provider_in_production()
    {
        var options = new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.DataProtectionFile,
            VaultPath = CreateTempPath(),
            AllowInsecureDevelopmentProviders = true
        };

        SecretVaultUnavailableException exception = Assert.Throws<SecretVaultUnavailableException>(() =>
            SecretVaultFactory.CreateDefault(options, CreateWriter(), isDevelopment: false));

        Assert.Equal(SecretVaultAvailability.InsecureConfiguration, exception.Result.Availability);
        Assert.DoesNotContain(Sentinel, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void Factory_requires_explicit_headless_provider_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var options = new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.Auto,
            UsageProfile = SecretVaultUsageProfile.Headless
        };

        SecretVaultUnavailableException exception = Assert.Throws<SecretVaultUnavailableException>(() =>
            SecretVaultFactory.CreateDefault(options, CreateWriter()));

        Assert.Equal(SecretVaultAvailability.InvalidConfiguration, exception.Result.Availability);
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task External_wrapping_key_vault_round_trips_restarts_and_rotates()
    {
        string root = CreateTempPath();
        byte[] oldKey = RandomNumberGenerator.GetBytes(32);
        byte[] newKey = RandomNumberGenerator.GetBytes(32);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A04_OLD_KEY"] = Convert.ToBase64String(oldKey),
            ["A04_NEW_KEY"] = Convert.ToBase64String(newKey)
        };
        try
        {
            var oldVault = new ExternalWrappingKeySecretVault(
                CreateExternalOptions(root, "generation-1", "A04_OLD_KEY"),
                CreateWriter(),
                name => environment.GetValueOrDefault(name));
            await oldVault.SetAsync("provider/api-key", Sentinel);

            SecretVaultOptions rotatedOptions = CreateExternalOptions(root, "generation-2", "A04_NEW_KEY");
            rotatedOptions.PreviousWrappingKeyEnvironmentVariables["generation-1"] = "A04_OLD_KEY";
            var restartedVault = new ExternalWrappingKeySecretVault(
                rotatedOptions,
                CreateWriter(),
                name => environment.GetValueOrDefault(name));

            Assert.Equal(Sentinel, await restartedVault.GetAsync("provider/api-key"));
            await restartedVault.SetAsync("provider/api-key", "rotated-value");
            Assert.Equal("rotated-value", await restartedVault.GetAsync("provider/api-key"));

            string persisted = string.Join(
                '\n',
                Directory.EnumerateFiles(root, "*.secret", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));
            Assert.DoesNotContain(Sentinel, persisted, StringComparison.Ordinal);
            Assert.DoesNotContain(environment["A04_OLD_KEY"], persisted, StringComparison.Ordinal);
            Assert.DoesNotContain(environment["A04_NEW_KEY"], persisted, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(root, "vault.key", SearchOption.AllDirectories));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(oldKey);
            CryptographicOperations.ZeroMemory(newKey);
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task External_wrapping_key_probe_fails_closed_without_key_material()
    {
        var vault = new ExternalWrappingKeySecretVault(
            CreateExternalOptions(CreateTempPath(), "generation-1", "A04_MISSING_KEY"),
            CreateWriter(),
            _ => null);

        SecretVaultProbeResult result = await vault.ProbeAsync();

        Assert.Equal(SecretVaultAvailability.InvalidConfiguration, result.Availability);
        Assert.DoesNotContain("A04_MISSING_KEY", result.Remediation, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Legacy_file_vault_is_available_only_as_an_explicit_read_delete_migration_source()
    {
        string root = CreateTempPath();
        var options = new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.DataProtectionFile,
            VaultPath = root
        };
        try
        {
            var legacyVault = new DataProtectionFileVault(options, CreateWriter());
            await legacyVault.SetAsync("legacy/provider", Sentinel);

            Assert.Throws<SecretVaultUnavailableException>(() =>
                SecretVaultFactory.CreateDefault(options, CreateWriter(), isDevelopment: false));

            ISecretVault migrationSource = SecretVaultFactory.CreateMigrationSource(options, CreateWriter());
            Assert.Equal(Sentinel, await migrationSource.GetAsync("legacy/provider"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                migrationSource.SetAsync("legacy/provider", "replacement"));
            await migrationSource.DeleteAsync("legacy/provider");
            Assert.Null(await migrationSource.GetAsync("legacy/provider"));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task MacOs_vault_preserves_native_probe_and_crud_contract()
    {
        var client = new FakeMacOsKeychainClient(
            new SecretVaultProbeResult(
                SecretVaultProviderKind.MacOsKeychain,
                SecretVaultAvailability.Locked,
                "Unlock the login Keychain."));
        var vault = new MacOsKeychainSecretVault(
            new SecretVaultOptions
            {
                Provider = SecretVaultProviderKind.MacOsKeychain,
                UsageProfile = SecretVaultUsageProfile.Interactive,
                ApplicationName = "CanDoItAll.Tests"
            },
            client);

        SecretVaultProbeResult probe = await vault.ProbeAsync();
        Assert.Equal(SecretVaultAvailability.Locked, probe.Availability);

        await vault.SetAsync("provider/api-key", Sentinel);
        Assert.Equal(Sentinel, await vault.GetAsync("provider/api-key"));
        await vault.DeleteAsync("provider/api-key");
        Assert.Null(await vault.GetAsync("provider/api-key"));
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public void MacOs_vault_rejects_headless_profile_without_fallback()
    {
        var options = new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.MacOsKeychain,
            UsageProfile = SecretVaultUsageProfile.Headless
        };

        SecretVaultConfigurationException exception = Assert.Throws<SecretVaultConfigurationException>(() =>
            new MacOsKeychainSecretVault(options));

        Assert.Contains("headless", exception.Remediation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task Linux_secret_service_distinguishes_headless_and_locked_sessions()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var noSessionVault = new LinuxSecretServiceVault(
            CreateLinuxOptions(),
            new FakeLinuxCommandRunner(new LinuxSecretServiceCommandResult(1, string.Empty, string.Empty)),
            _ => null);
        Assert.Equal(
            SecretVaultAvailability.SessionUnavailable,
            (await noSessionVault.ProbeAsync()).Availability);

        var lockedVault = new LinuxSecretServiceVault(
            CreateLinuxOptions(),
            new FakeLinuxCommandRunner(new LinuxSecretServiceCommandResult(1, string.Empty, "keyring is locked")),
            _ => "unix:path=/run/user/1000/bus");
        Assert.Equal(
            SecretVaultAvailability.Locked,
            (await lockedVault.ProbeAsync()).Availability);
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task Linux_secret_service_reports_probe_timeout_without_leaking_provider_output()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var vault = new LinuxSecretServiceVault(
            CreateLinuxOptions(),
            new TimeoutLinuxCommandRunner(),
            _ => "unix:path=/run/user/1000/bus");

        SecretVaultProbeResult probe = await vault.ProbeAsync();

        Assert.Equal(SecretVaultAvailability.Unavailable, probe.Availability);
        Assert.DoesNotContain(Sentinel, probe.Remediation, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task Startup_validator_fails_before_serving_when_probe_is_unavailable()
    {
        var state = new SecretVaultCapabilityState();
        var validator = new SecretVaultStartupValidator(
            new UnavailableVault(),
            state);

        SecretVaultUnavailableException exception = await Assert.ThrowsAsync<SecretVaultUnavailableException>(() =>
            validator.StartAsync(CancellationToken.None));

        Assert.Equal(SecretVaultAvailability.DependencyMissing, exception.Result.Availability);
        Assert.Equal(exception.Result, state.Current);
        Assert.DoesNotContain(Sentinel, exception.ToString(), StringComparison.Ordinal);
    }

    private static SecretVaultOptions CreateExternalOptions(string root, string keyId, string environmentVariable)
        => new()
        {
            Provider = SecretVaultProviderKind.ExternalWrappingKeyFile,
            UsageProfile = SecretVaultUsageProfile.Headless,
            VaultPath = root,
            WrappingKeyId = keyId,
            WrappingKeyEnvironmentVariable = environmentVariable
        };

    private static SecretVaultOptions CreateLinuxOptions()
        => new()
        {
            Provider = SecretVaultProviderKind.LinuxSecretService,
            UsageProfile = SecretVaultUsageProfile.Interactive,
            ProbeTimeout = TimeSpan.FromSeconds(1)
        };

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "candoitall-a04-secret-tests", Guid.NewGuid().ToString("N"));

    private static DurableFileWriter CreateWriter()
        => new(TestWorkspaceServices.PhysicalPathPolicyFactory);

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class FakeMacOsKeychainClient(SecretVaultProbeResult probe) : IMacOsKeychainClient
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);

        public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(probe);

        public Task SetAsync(string service, string account, string value, CancellationToken cancellationToken)
        {
            values[$"{service}\0{account}"] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string service, string account, CancellationToken cancellationToken)
            => Task.FromResult(values.GetValueOrDefault($"{service}\0{account}"));

        public Task DeleteAsync(string service, string account, CancellationToken cancellationToken)
        {
            values.Remove($"{service}\0{account}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLinuxCommandRunner(LinuxSecretServiceCommandResult result)
        : ILinuxSecretServiceCommandRunner
    {
        public Task<LinuxSecretServiceCommandResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            string? standardInput,
            TimeSpan timeout,
            CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class TimeoutLinuxCommandRunner : ILinuxSecretServiceCommandRunner
    {
        public Task<LinuxSecretServiceCommandResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            string? standardInput,
            TimeSpan timeout,
            CancellationToken cancellationToken)
            => throw new TimeoutException("provider output " + Sentinel);
    }

    private sealed class UnavailableVault : ISecretVault, ISecretVaultCapability
    {
        public SecretVaultProviderKind Provider => SecretVaultProviderKind.LinuxSecretService;

        public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.DependencyMissing,
                "Install the required native provider."));

        public Task SetAsync(string key, string value, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string key, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
