using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentExternalIdentity(string Namespace, string Key);

public static partial class AgentExternalIdentityNormalizer
{
    public const string PackageImportNamespace = "package-import";
    private const int MaximumPartLength = 100;

    public static AgentExternalIdentity Normalize(string? externalNamespace, string? key)
    {
        return new AgentExternalIdentity(
            NormalizePart(externalNamespace, "namespace"),
            NormalizePart(key, "key"));
    }

    public static string ToCanonicalString(AgentExternalIdentity identity)
        => $"{identity.Namespace}/{identity.Key}";

    private static string NormalizePart(string? value, string label)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < 1 or > MaximumPartLength ||
            !ExternalIdentityPartPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                $"External identity {label} must be 1-{MaximumPartLength} lowercase letters, digits, '.', '_', or '-' and must start and end with a letter or digit.",
                label);
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9._-]{0,98}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex ExternalIdentityPartPattern();
}

public sealed record AgentExternalBindingRecord(
    string Namespace,
    string Key,
    Guid AgentId,
    string ConfigurationVersion,
    bool IsArchived,
    DateTimeOffset UpdatedAtUtc);

public sealed record AgentExternalProvisioningCommand(
    string Namespace,
    string Key,
    string IdempotencyKey,
    string? ExpectedConfigurationVersion,
    AgentEditorModel Agent);

public sealed record AgentExternalArchiveCommand(
    string Namespace,
    string Key,
    string IdempotencyKey,
    string? ExpectedConfigurationVersion);

public sealed record AgentExternalProvisioningReceipt(
    string Namespace,
    string Key,
    Guid AgentId,
    string ConfigurationVersion,
    bool Created,
    bool Replayed,
    bool Archived,
    IReadOnlyList<string> Warnings);

public sealed record AgentExternalProvisioningResource(
    string Namespace,
    string Key,
    Guid AgentId,
    string ConfigurationVersion,
    bool IsArchived,
    DateTimeOffset UpdatedAtUtc);

public sealed record AgentExternalProvisioningOperationRecord(
    string IdempotencyKey,
    string RequestFingerprint,
    AgentExternalProvisioningReceipt Receipt,
    DateTimeOffset CompletedAtUtc);
