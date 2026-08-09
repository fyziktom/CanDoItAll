using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.Modules.Security;

public sealed class ExternalWrappingKeySecretVault : FileBackedSecretVault, ISecretVaultCapability
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly Regex IdentifierPattern = new(
        "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ExternalWrappingKeySource keySource;

    public ExternalWrappingKeySecretVault(
        SecretVaultOptions options,
        DurableFileWriter durableFileWriter,
        Func<string, string?> environmentVariableResolver)
        : base(options, "external-wrapping-key", durableFileWriter)
    {
        keySource = new ExternalWrappingKeySource(options, environmentVariableResolver);
    }

    public SecretVaultProviderKind Provider => SecretVaultProviderKind.ExternalWrappingKeyFile;

    public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            byte[] key = keySource.ResolveCurrentKey();
            CryptographicOperations.ZeroMemory(key);
            return ValueTask.FromResult(SecretVaultProbeResult.Available(Provider));
        }
        catch (SecretVaultConfigurationException exception)
        {
            return ValueTask.FromResult(new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.InvalidConfiguration,
                exception.Remediation));
        }
    }

    protected override byte[] Protect(string key, byte[] plainBytes)
    {
        byte[] wrappingKey = keySource.ResolveCurrentKey();
        try
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] cipherText = new byte[plainBytes.Length];
            byte[] tag = new byte[TagSize];
            using var aes = new AesGcm(wrappingKey, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherText, tag, BuildAssociatedData(key, keySource.CurrentKeyId));
            try
            {
                return JsonSerializer.SerializeToUtf8Bytes(new ExternalWrappingKeyEnvelope(
                    Version: 1,
                    KeyId: keySource.CurrentKeyId,
                    Nonce: Convert.ToBase64String(nonce),
                    Tag: Convert.ToBase64String(tag),
                    CipherText: Convert.ToBase64String(cipherText)), SerializerOptions);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(cipherText);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }
    }

    protected override byte[] Unprotect(string key, byte[] protectedBytes)
    {
        ExternalWrappingKeyEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ExternalWrappingKeyEnvelope>(protectedBytes, SerializerOptions)
                ?? throw new CryptographicException("The protected secret envelope is empty.");
        }
        catch (JsonException exception)
        {
            throw new CryptographicException("The protected secret envelope is invalid.", exception);
        }

        if (envelope.Version != 1 ||
            !IdentifierPattern.IsMatch(envelope.KeyId) ||
            string.IsNullOrWhiteSpace(envelope.Nonce) ||
            string.IsNullOrWhiteSpace(envelope.Tag) ||
            string.IsNullOrWhiteSpace(envelope.CipherText))
        {
            throw new CryptographicException("The protected secret envelope is invalid.");
        }

        byte[] wrappingKey = keySource.ResolveKey(envelope.KeyId);
        byte[] nonce = Decode(envelope.Nonce, NonceSize, "nonce");
        byte[] tag = Decode(envelope.Tag, TagSize, "authentication tag");
        byte[] cipherText = Decode(envelope.CipherText, expectedLength: null, "ciphertext");
        byte[] plainBytes = new byte[cipherText.Length];
        try
        {
            using var aes = new AesGcm(wrappingKey, TagSize);
            aes.Decrypt(
                nonce,
                cipherText,
                tag,
                plainBytes,
                BuildAssociatedData(key, envelope.KeyId));
            return plainBytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
            CryptographicOperations.ZeroMemory(cipherText);
        }
    }

    private static byte[] Decode(string value, int? expectedLength, string field)
    {
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException($"The protected secret {field} is invalid.", exception);
        }

        if (expectedLength.HasValue && decoded.Length != expectedLength.Value)
        {
            CryptographicOperations.ZeroMemory(decoded);
            throw new CryptographicException($"The protected secret {field} has an invalid length.");
        }

        return decoded;
    }

    private static byte[] BuildAssociatedData(string key, string keyId)
        => SHA256.HashData(Encoding.UTF8.GetBytes($"CanDoItAll.SecretVault.v1\0{keyId}\0{key}"));

    private sealed record ExternalWrappingKeyEnvelope(
        int Version,
        string KeyId,
        string Nonce,
        string Tag,
        string CipherText);

    private sealed class ExternalWrappingKeySource
    {
        private readonly Func<string, string?> environmentVariableResolver;
        private readonly IReadOnlyDictionary<string, string> environmentVariablesByKeyId;

        public ExternalWrappingKeySource(
            SecretVaultOptions options,
            Func<string, string?> environmentVariableResolver)
        {
            ArgumentNullException.ThrowIfNull(options);
            this.environmentVariableResolver = environmentVariableResolver
                ?? throw new ArgumentNullException(nameof(environmentVariableResolver));
            CurrentKeyId = NormalizeIdentifier(options.WrappingKeyId, "wrapping key id");
            string currentEnvironmentVariable = NormalizeIdentifier(
                options.WrappingKeyEnvironmentVariable,
                "wrapping key environment variable");
            var variables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CurrentKeyId] = currentEnvironmentVariable
            };
            foreach ((string keyId, string environmentVariable) in options.PreviousWrappingKeyEnvironmentVariables)
            {
                string normalizedKeyId = NormalizeIdentifier(keyId, "previous wrapping key id");
                if (!variables.TryAdd(
                        normalizedKeyId,
                        NormalizeIdentifier(environmentVariable, "previous wrapping key environment variable")))
                {
                    throw new SecretVaultConfigurationException(
                        "Wrapping key identifiers must be unique.");
                }
            }

            environmentVariablesByKeyId = variables;
        }

        public string CurrentKeyId { get; }

        public byte[] ResolveCurrentKey() => ResolveKey(CurrentKeyId);

        public byte[] ResolveKey(string keyId)
        {
            if (!environmentVariablesByKeyId.TryGetValue(keyId, out string? environmentVariable))
            {
                throw new SecretVaultConfigurationException(
                    "The wrapping key generation required by an existing payload is not configured.");
            }

            string? encodedKey = environmentVariableResolver(environmentVariable);
            if (string.IsNullOrWhiteSpace(encodedKey))
            {
                throw new SecretVaultConfigurationException(
                    "Supply the configured wrapping key through the protected startup environment or secret mount.");
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(encodedKey);
            }
            catch (FormatException exception)
            {
                throw new SecretVaultConfigurationException(
                    "The configured wrapping key must be Base64-encoded 256-bit key material.",
                    exception);
            }

            if (key.Length == KeySize)
            {
                return key;
            }

            CryptographicOperations.ZeroMemory(key);
            throw new SecretVaultConfigurationException(
                "The configured wrapping key must contain exactly 256 bits of key material.");
        }

        private static string NormalizeIdentifier(string? value, string description)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (!IdentifierPattern.IsMatch(normalized))
            {
                throw new SecretVaultConfigurationException(
                    $"Configure a valid {description} using letters, digits, dot, underscore, or hyphen.");
            }

            return normalized;
        }
    }
}

public sealed class SecretVaultConfigurationException : InvalidOperationException
{
    public SecretVaultConfigurationException(string remediation)
        : base("Secret vault configuration is invalid.")
    {
        Remediation = remediation;
    }

    public SecretVaultConfigurationException(string remediation, Exception innerException)
        : base("Secret vault configuration is invalid.", innerException)
    {
        Remediation = remediation;
    }

    public string Remediation { get; }
}
