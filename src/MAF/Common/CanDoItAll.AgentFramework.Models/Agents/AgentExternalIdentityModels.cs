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

/// <summary>
/// Receipt of an external-key provisioning write (<c>PUT</c> or <c>DELETE</c> on
/// <c>/api/agents/by-external-key/{externalNamespace}/{key}</c>): the external identity, the agent bound to it and
/// the configuration version to send as <c>If-Match</c> with the next write. A replayed request returns the receipt
/// stored by the original write.
/// </summary>
/// <param name="Namespace">Namespace of the external identity, normalized to lowercase.</param>
/// <param name="Key">Key of the external identity, normalized to lowercase.</param>
/// <param name="AgentId">
/// Identifier of the agent bound to the external identity; use it with the other <c>/api/agents</c> operations.
/// </param>
/// <param name="ConfigurationVersion">
/// Configuration version now recorded for the binding: 64 uppercase hexadecimal digits, a hash of the agent's
/// configuration rather than a counter. The response's <c>ETag</c> header carries the same value in quotes; send it as
/// <c>If-Match</c> with the next write through this identity.
/// </param>
/// <param name="Created">True when this write created the binding and a new agent (HTTP 201).</param>
/// <param name="Replayed">
/// True when this receipt was returned for a repeated request with the same <c>Idempotency-Key</c>; nothing was
/// written again.
/// </param>
/// <param name="Archived">True when the bound agent is archived after this write.</param>
/// <param name="Warnings">
/// Human-readable notes, for example that the requested configuration already matched the stored agent and nothing
/// was changed. Their wording can change.
/// </param>
public sealed record AgentExternalProvisioningReceipt(
    string Namespace,
    string Key,
    Guid AgentId,
    string ConfigurationVersion,
    bool Created,
    bool Replayed,
    bool Archived,
    IReadOnlyList<string> Warnings);

/// <summary>
/// An external identity binding as read by <c>GET /api/agents/by-external-key/{externalNamespace}/{key}</c>: which
/// agent the caller-defined namespace and key point to, and the configuration version recorded by the last write
/// through this identity.
/// </summary>
/// <param name="Namespace">Namespace of the external identity, normalized to lowercase.</param>
/// <param name="Key">Key of the external identity, normalized to lowercase.</param>
/// <param name="AgentId">Identifier of the bound agent; use it with the other <c>/api/agents</c> operations.</param>
/// <param name="ConfigurationVersion">
/// Configuration version recorded for the binding by the last provisioning, archive or package import through this
/// identity: 64 uppercase hexadecimal digits. Changes made to the agent through other operations do not update it.
/// Send it as <c>If-Match</c> to update or archive through this identity; the <c>ETag</c> header carries the same
/// value.
/// </param>
/// <param name="IsArchived">
/// True when the last write through this identity left the agent archived: a <c>DELETE</c>, or a <c>PUT</c> or
/// package import of an archived agent. Archiving the agent through other operations does not change it.
/// </param>
/// <param name="UpdatedAtUtc">
/// Instant of the last write to the binding: the provisioning or archive time, or for a package import the imported
/// agent's revision.
/// </param>
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
