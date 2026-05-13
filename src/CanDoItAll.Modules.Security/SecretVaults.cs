using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Security;

public interface ISecretVault
{
    Task SetAsync(string key, string value, CancellationToken ct = default);

    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}

public enum SecretVaultProviderKind
{
    Auto,
    Dpapi,
    MauiSecureStorage,
    MacOsKeychain,
    LinuxSecretService,
    DataProtectionFile,
    AzureKeyVault,
    HashiCorp,
    InMemory
}

public sealed class SecretVaultOptions
{
    public const string SectionName = "SecretVault";

    public SecretVaultProviderKind Provider { get; set; } = SecretVaultProviderKind.Auto;

    public string? ApplicationName { get; set; }

    public string? VaultPath { get; set; }
}

public static class SecretVaultFactory
{
    public static ISecretVault CreateDefault(SecretVaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var provider = options.Provider == SecretVaultProviderKind.Auto
            ? ResolveAutoProvider()
            : options.Provider;

        return provider switch
        {
            SecretVaultProviderKind.Dpapi => new DpapiSecretVault(options),
            SecretVaultProviderKind.MauiSecureStorage => new MauiSecureStorageVault(options),
            SecretVaultProviderKind.MacOsKeychain => new MacOsKeychainSecretVault(options),
            SecretVaultProviderKind.LinuxSecretService => new LinuxSecretServiceVault(options),
            SecretVaultProviderKind.DataProtectionFile => new DataProtectionFileVault(options),
            SecretVaultProviderKind.AzureKeyVault => new AzureKeyVaultSecretVault(options),
            SecretVaultProviderKind.HashiCorp => new HashiCorpSecretVault(options),
            SecretVaultProviderKind.InMemory => new InMemorySecretVault(),
            _ => throw new NotSupportedException($"Secret vault provider '{provider}' is not supported.")
        };
    }

    private static SecretVaultProviderKind ResolveAutoProvider()
    {
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

        return SecretVaultProviderKind.DataProtectionFile;
    }
}

public sealed class SecretVaultOptionsBackedFactory(IOptions<SecretVaultOptions> options)
{
    public ISecretVault Create() => SecretVaultFactory.CreateDefault(options.Value);
}

public sealed class DpapiSecretVault : FileBackedSecretVault
{
    private readonly string applicationName;

    public DpapiSecretVault(SecretVaultOptions options)
        : base(options, "dpapi")
    {
        applicationName = ResolveApplicationName(options);
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

public sealed class DataProtectionFileVault : FileBackedSecretVault
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly string keyFilePath;

    public DataProtectionFileVault(SecretVaultOptions options)
        : base(options, "file")
    {
        keyFilePath = Path.Combine(VaultRoot, "vault.key");
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
        Directory.CreateDirectory(VaultRoot);
        if (File.Exists(keyFilePath))
        {
            return Convert.FromBase64String(File.ReadAllText(keyFilePath));
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllText(keyFilePath, Convert.ToBase64String(key));
        return key;
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

public sealed class MacOsKeychainSecretVault : UnsupportedSecretVault
{
    public MacOsKeychainSecretVault(SecretVaultOptions options)
        : base(SecretVaultProviderKind.MacOsKeychain, options)
    {
    }
}

public sealed class LinuxSecretServiceVault : UnsupportedSecretVault
{
    public LinuxSecretServiceVault(SecretVaultOptions options)
        : base(SecretVaultProviderKind.LinuxSecretService, options)
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

public sealed class InMemorySecretVault : ISecretVault
{
    private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);

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

public abstract class UnsupportedSecretVault : ISecretVault
{
    private readonly SecretVaultProviderKind provider;

    protected UnsupportedSecretVault(SecretVaultProviderKind provider, SecretVaultOptions options)
    {
        this.provider = provider;
        _ = options;
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
    protected FileBackedSecretVault(SecretVaultOptions options, string providerFolder)
    {
        VaultRoot = ResolveVaultRoot(options, providerFolder);
    }

    protected string VaultRoot { get; }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ct.ThrowIfCancellationRequested();
        var normalizedKey = NormalizeKey(key);
        var plainBytes = Encoding.UTF8.GetBytes(value);
        try
        {
            var protectedBytes = Protect(normalizedKey, plainBytes);
            Directory.CreateDirectory(VaultRoot);
            await File.WriteAllTextAsync(
                ResolvePayloadPath(normalizedKey),
                Convert.ToBase64String(protectedBytes),
                ct);
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
        finally
        {
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

        var protectedBytes = Convert.FromBase64String(await File.ReadAllTextAsync(payloadPath, ct));
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

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedKey = NormalizeKey(key);
        var payloadPath = ResolvePayloadPath(normalizedKey);
        if (File.Exists(payloadPath))
        {
            File.Delete(payloadPath);
        }

        return Task.CompletedTask;
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
            return Path.GetFullPath(options.VaultPath);
        }

        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = AppContext.BaseDirectory;
        }

        return Path.Combine(basePath, ResolveApplicationName(options), "secrets", providerFolder);
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
