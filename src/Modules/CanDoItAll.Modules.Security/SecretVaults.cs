using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Security;

public interface ISecretVault
{
    Task SetAsync(string key, string value, CancellationToken ct = default);

    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}

public interface ISecretVaultCapability
{
    SecretVaultProviderKind Provider { get; }

    ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}

public enum SecretVaultAvailability
{
    Available,
    UnsupportedPlatform,
    DependencyMissing,
    SessionUnavailable,
    Locked,
    InvalidConfiguration,
    InsecureConfiguration,
    Unavailable
}

public sealed record SecretVaultProbeResult(
    SecretVaultProviderKind Provider,
    SecretVaultAvailability Availability,
    string Remediation)
{
    public bool IsAvailable => Availability == SecretVaultAvailability.Available;

    public static SecretVaultProbeResult Available(SecretVaultProviderKind provider)
        => new(provider, SecretVaultAvailability.Available, string.Empty);
}

public sealed class SecretVaultUnavailableException(SecretVaultProbeResult result)
    : InvalidOperationException(
        $"Secret vault provider '{result.Provider}' is not available ({result.Availability}). {result.Remediation}".Trim())
{
    public SecretVaultProbeResult Result { get; } = result;
}

public enum SecretVaultProviderKind
{
    Auto,
    Dpapi,
    MauiSecureStorage,
    MacOsKeychain,
    LinuxSecretService,
    ExternalWrappingKeyFile,
    DataProtectionFile,
    AzureKeyVault,
    HashiCorp,
    InMemory
}

public enum SecretVaultUsageProfile
{
    Interactive,
    Headless
}

public sealed class SecretVaultOptions
{
    public const string SectionName = "SecretVault";

    public SecretVaultProviderKind Provider { get; set; } = SecretVaultProviderKind.Auto;

    public SecretVaultUsageProfile UsageProfile { get; set; } = SecretVaultUsageProfile.Interactive;

    public string? ApplicationName { get; set; }

    public string? VaultPath { get; set; }

    public string? WrappingKeyId { get; set; }

    public string? WrappingKeyEnvironmentVariable { get; set; }

    public Dictionary<string, string> PreviousWrappingKeyEnvironmentVariables { get; set; } = new(StringComparer.Ordinal);

    public string? LinuxSecretToolPath { get; set; }

    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public bool AllowInsecureDevelopmentProviders { get; set; }
}

public static class SecretVaultFactory
{
    public static ISecretVault CreateDefault(
        SecretVaultOptions options,
        DurableFileWriter durableFileWriter,
        bool isDevelopment = false,
        Func<string, string?>? environmentVariableResolver = null)
        => Create(
            options,
            durableFileWriter,
            isDevelopment,
            allowLegacyFileMigrationSource: false,
            environmentVariableResolver);

    public static ISecretVault CreateMigrationSource(
        SecretVaultOptions options,
        DurableFileWriter durableFileWriter,
        Func<string, string?>? environmentVariableResolver = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(durableFileWriter);
        if (options.Provider is SecretVaultProviderKind.Auto or SecretVaultProviderKind.InMemory)
        {
            throw new SecretVaultConfigurationException(
                "Secret migration requires an explicit persisted source provider.");
        }

        return new MigrationSourceSecretVault(Create(
            options,
            durableFileWriter,
            isDevelopment: false,
            allowLegacyFileMigrationSource: true,
            environmentVariableResolver));
    }

    private static ISecretVault Create(
        SecretVaultOptions options,
        DurableFileWriter durableFileWriter,
        bool isDevelopment,
        bool allowLegacyFileMigrationSource,
        Func<string, string?>? environmentVariableResolver)
    {
        var provider = options.Provider == SecretVaultProviderKind.Auto
            ? ResolveAutoProvider(options.UsageProfile)
            : options.Provider;

        bool legacyFileMigrationSource =
            allowLegacyFileMigrationSource && provider == SecretVaultProviderKind.DataProtectionFile;
        if (provider is (SecretVaultProviderKind.DataProtectionFile or SecretVaultProviderKind.InMemory) &&
            !legacyFileMigrationSource &&
            (!isDevelopment || !options.AllowInsecureDevelopmentProviders))
        {
            throw new SecretVaultUnavailableException(new SecretVaultProbeResult(
                provider,
                SecretVaultAvailability.InsecureConfiguration,
                "Select an operating-system vault or ExternalWrappingKeyFile. Development-only providers require an explicit development opt-in."));
        }

        return provider switch
        {
            SecretVaultProviderKind.Dpapi => new DpapiSecretVault(options, durableFileWriter),
            SecretVaultProviderKind.MauiSecureStorage => new MauiSecureStorageVault(options),
            SecretVaultProviderKind.MacOsKeychain => new MacOsKeychainSecretVault(options),
            SecretVaultProviderKind.LinuxSecretService => new LinuxSecretServiceVault(options),
            SecretVaultProviderKind.ExternalWrappingKeyFile => new ExternalWrappingKeySecretVault(
                options,
                durableFileWriter,
                environmentVariableResolver ?? Environment.GetEnvironmentVariable),
            SecretVaultProviderKind.DataProtectionFile => new DataProtectionFileVault(options, durableFileWriter),
            SecretVaultProviderKind.AzureKeyVault => new AzureKeyVaultSecretVault(options),
            SecretVaultProviderKind.HashiCorp => new HashiCorpSecretVault(options),
            SecretVaultProviderKind.InMemory => new InMemorySecretVault(),
            _ => throw new NotSupportedException($"Secret vault provider '{provider}' is not supported.")
        };
    }

    private static SecretVaultProviderKind ResolveAutoProvider(SecretVaultUsageProfile usageProfile)
    {
        if (usageProfile == SecretVaultUsageProfile.Headless && !OperatingSystem.IsWindows())
        {
            throw new SecretVaultUnavailableException(new SecretVaultProbeResult(
                SecretVaultProviderKind.Auto,
                SecretVaultAvailability.InvalidConfiguration,
                "Configure ExternalWrappingKeyFile or a supported remote vault for a Unix headless profile."));
        }

        if (OperatingSystem.IsWindows())
        {
            return SecretVaultProviderKind.Dpapi;
        }

        if (OperatingSystem.IsMacOS())
        {
            return SecretVaultProviderKind.MacOsKeychain;
        }

        if (OperatingSystem.IsLinux())
        {
            return SecretVaultProviderKind.LinuxSecretService;
        }

        throw new PlatformNotSupportedException("No automatic secret vault provider is defined for this operating system.");
    }

    private sealed class MigrationSourceSecretVault(ISecretVault inner) : ISecretVault
    {
        public Task SetAsync(string key, string value, CancellationToken ct = default)
            => throw new InvalidOperationException("A migration source vault is read/delete-only.");

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => inner.GetAsync(key, ct);

        public Task DeleteAsync(string key, CancellationToken ct = default)
            => inner.DeleteAsync(key, ct);
    }
}

public sealed class SecretVaultOptionsBackedFactory(
    IOptions<SecretVaultOptions> options,
    DurableFileWriter durableFileWriter,
    Microsoft.Extensions.Hosting.IHostEnvironment hostEnvironment)
{
    public ISecretVault Create() => SecretVaultFactory.CreateDefault(
        options.Value,
        durableFileWriter,
        hostEnvironment.IsDevelopment());
}

public sealed class DpapiSecretVault : FileBackedSecretVault, ISecretVaultCapability
{
    private readonly string applicationName;

    public DpapiSecretVault(SecretVaultOptions options, DurableFileWriter durableFileWriter)
        : base(options, "dpapi", durableFileWriter)
    {
        applicationName = ResolveApplicationName(options);
    }

    public SecretVaultProviderKind Provider => SecretVaultProviderKind.Dpapi;

    public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(OperatingSystem.IsWindows()
            ? SecretVaultProbeResult.Available(Provider)
            : new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.UnsupportedPlatform,
                "Select DPAPI only on Windows."));
    }

    protected override byte[] Protect(string key, byte[] plainBytes)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The DPAPI secret vault is supported only on Windows.");
        }

        return ProtectedData.Protect(
            plainBytes,
            BuildEntropy(applicationName, key),
            DataProtectionScope.CurrentUser);
    }

    protected override byte[] Unprotect(string key, byte[] protectedBytes)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The DPAPI secret vault is supported only on Windows.");
        }

        return ProtectedData.Unprotect(
            protectedBytes,
            BuildEntropy(applicationName, key),
            DataProtectionScope.CurrentUser);
    }

    private static byte[] BuildEntropy(string applicationName, string key)
        => SHA256.HashData(Encoding.UTF8.GetBytes($"{applicationName}\0{key}"));
}

public sealed class DataProtectionFileVault : FileBackedSecretVault, ISecretVaultCapability
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly string keyFilePath;

    public DataProtectionFileVault(SecretVaultOptions options, DurableFileWriter durableFileWriter)
        : base(options, "file", durableFileWriter)
    {
        keyFilePath = Path.Combine(VaultRoot, "vault.key");
    }

    public SecretVaultProviderKind Provider => SecretVaultProviderKind.DataProtectionFile;

    public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new SecretVaultProbeResult(
            Provider,
            SecretVaultAvailability.InsecureConfiguration,
            "This legacy provider is supported only for explicit development or migration reads."));
    }

    protected override byte[] Protect(string key, byte[] plainBytes)
    {
        var vaultKey = GetOrCreateKey();
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipherText = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(vaultKey, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherText, tag, BuildAssociatedData(key));

            var output = new byte[NonceSize + TagSize + cipherText.Length];
            Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
            Buffer.BlockCopy(cipherText, 0, output, NonceSize + TagSize, cipherText.Length);
            CryptographicOperations.ZeroMemory(cipherText);
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultKey);
        }
    }

    protected override byte[] Unprotect(string key, byte[] protectedBytes)
    {
        if (protectedBytes.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("The protected secret payload is invalid.");
        }

        var vaultKey = GetOrCreateKey();
        try
        {
            var nonce = protectedBytes.AsSpan(0, NonceSize);
            var tag = protectedBytes.AsSpan(NonceSize, TagSize);
            var cipherText = protectedBytes.AsSpan(NonceSize + TagSize);
            var plainBytes = new byte[cipherText.Length];

            using var aes = new AesGcm(vaultKey, TagSize);
            aes.Decrypt(nonce, cipherText, tag, plainBytes, BuildAssociatedData(key));
            return plainBytes;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultKey);
        }
    }

    private byte[] GetOrCreateKey()
    {
        string coordinationPath = keyFilePath + ".generation.lock";
        using IDisposable coordination = DurableFileWriter.AcquireCoordination(
            VaultRoot,
            coordinationPath,
            DurableFileWriteOptions.Private.LockTimeout,
            requirePrivateUnixMode: true);
        if (File.Exists(keyFilePath))
        {
            DurableFileWriter.HardenPrivateFile(VaultRoot, keyFilePath);
            return Convert.FromBase64String(File.ReadAllText(keyFilePath));
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            DurableFileWriter.WriteText(
                VaultRoot,
                keyFilePath,
                Convert.ToBase64String(key),
                DurableFileWriteOptions.Private);
            return key;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    private static byte[] BuildAssociatedData(string key)
        => SHA256.HashData(Encoding.UTF8.GetBytes(key));
}

public sealed class MauiSecureStorageVault : UnsupportedSecretVault
{
    public MauiSecureStorageVault(SecretVaultOptions options)
        : base(SecretVaultProviderKind.MauiSecureStorage, options)
    {
    }
}

public sealed class AzureKeyVaultSecretVault : UnsupportedSecretVault
{
    public AzureKeyVaultSecretVault(SecretVaultOptions options)
        : base(SecretVaultProviderKind.AzureKeyVault, options)
    {
    }
}

public sealed class HashiCorpSecretVault : UnsupportedSecretVault
{
    public HashiCorpSecretVault(SecretVaultOptions options)
        : base(SecretVaultProviderKind.HashiCorp, options)
    {
    }
}

public sealed class InMemorySecretVault : ISecretVault, ISecretVaultCapability
{
    private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);

    public SecretVaultProviderKind Provider => SecretVaultProviderKind.InMemory;

    public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SecretVaultProbeResult.Available(Provider));
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        values[NormalizeKey(key)] = value ?? throw new ArgumentNullException(nameof(value));
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        values.TryGetValue(NormalizeKey(key), out var value);
        return Task.FromResult(value);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        values.TryRemove(NormalizeKey(key), out _);
        return Task.CompletedTask;
    }

    private static string NormalizeKey(string key)
        => string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Secret key cannot be empty.", nameof(key))
            : key.Trim();
}

public abstract class UnsupportedSecretVault : ISecretVault, ISecretVaultCapability
{
    private readonly SecretVaultProviderKind provider;

    protected UnsupportedSecretVault(SecretVaultProviderKind provider, SecretVaultOptions options)
    {
        this.provider = provider;
        _ = options;
    }

    public SecretVaultProviderKind Provider => provider;

    public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new SecretVaultProbeResult(
            provider,
            SecretVaultAvailability.Unavailable,
            "Configure an implemented operating-system or headless secret provider."));
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
        => throw CreateException();

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => throw CreateException();

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => throw CreateException();

    private NotSupportedException CreateException()
        => new($"Secret vault provider '{provider}' is not implemented in this host.");
}

public abstract class FileBackedSecretVault : ISecretVault
{
    protected FileBackedSecretVault(
        SecretVaultOptions options,
        string providerFolder,
        DurableFileWriter durableFileWriter)
    {
        VaultRoot = ResolveVaultRoot(options, providerFolder);
        DurableFileWriter = durableFileWriter ?? throw new ArgumentNullException(nameof(durableFileWriter));
    }

    protected string VaultRoot { get; }

    protected DurableFileWriter DurableFileWriter { get; }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ct.ThrowIfCancellationRequested();
        var normalizedKey = NormalizeKey(key);
        var plainBytes = Encoding.UTF8.GetBytes(value);
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = Protect(normalizedKey, plainBytes);
            await DurableFileWriter.WriteTextAsync(
                VaultRoot,
                ResolvePayloadPath(normalizedKey),
                Convert.ToBase64String(protectedBytes),
                DurableFileWriteOptions.Private,
                ct).ConfigureAwait(false);
        }
        finally
        {
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedKey = NormalizeKey(key);
        var payloadPath = ResolvePayloadPath(normalizedKey);
        if (!File.Exists(payloadPath))
        {
            return null;
        }

        DurableFileWriter.HardenPrivateFile(VaultRoot, payloadPath);
        var protectedBytes = Convert.FromBase64String(await File.ReadAllTextAsync(payloadPath, ct)
            .ConfigureAwait(false));
        byte[]? plainBytes = null;
        try
        {
            plainBytes = Unprotect(normalizedKey, protectedBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedKey = NormalizeKey(key);
        await DurableFileWriter.DeleteAsync(
            VaultRoot,
            ResolvePayloadPath(normalizedKey),
            DurableFileWriteOptions.Private,
            ct).ConfigureAwait(false);
    }

    protected abstract byte[] Protect(string key, byte[] plainBytes);

    protected abstract byte[] Unprotect(string key, byte[] protectedBytes);

    protected static string ResolveApplicationName(SecretVaultOptions options)
        => string.IsNullOrWhiteSpace(options.ApplicationName)
            ? "CanDoItAll"
            : options.ApplicationName.Trim();

    private static string ResolveVaultRoot(SecretVaultOptions options, string providerFolder)
    {
        if (!string.IsNullOrWhiteSpace(options.VaultPath))
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(options.VaultPath, "secret vault root");
            if (!Path.IsPathRooted(options.VaultPath))
            {
                throw new SecretVaultConfigurationException(
                    "Configure SecretVault:VaultPath as an absolute native-host path.");
            }

            return Path.GetFullPath(options.VaultPath);
        }

        return Path.Combine(
            ApplicationPurposeRootPolicy.ResolveCurrent().StateRoot,
            "secrets",
            ResolveApplicationName(options),
            providerFolder);
    }

    private string ResolvePayloadPath(string key)
    {
        var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(VaultRoot, $"{fileName}.secret");
    }

    private static string NormalizeKey(string key)
        => string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Secret key cannot be empty.", nameof(key))
            : key.Trim();
}
