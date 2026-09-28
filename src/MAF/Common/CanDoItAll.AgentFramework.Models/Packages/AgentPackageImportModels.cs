namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// How an agent package import applies the packaged agent, as a JSON integer in the import receipt: 0 Create (added
/// under the packaged identifier with its history), 1 ReplaceExactVersion (replaced the existing agent with that
/// identifier and its history), 2 Clone (added only the definition under a new identifier). The import request
/// itself names the mode with the text values <c>create</c>, <c>replace-exact-version</c> and <c>clone</c>.
/// </summary>
public enum AgentPackageImportMode
{
    Create,
    ReplaceExactVersion,
    Clone
}

public sealed record AgentPackageReadOptions
{
    public const long DefaultMaximumPackageBytes = 32L * 1024 * 1024;
    public const long DefaultMaximumExpandedBytes = 128L * 1024 * 1024;
    public const int DefaultMaximumEntryCount = 64;
    public const long DefaultMaximumManifestBytes = 8L * 1024 * 1024;

    public long MaximumPackageBytes { get; init; } = DefaultMaximumPackageBytes;
    public long MaximumExpandedBytes { get; init; } = DefaultMaximumExpandedBytes;
    public int MaximumEntryCount { get; init; } = DefaultMaximumEntryCount;
    public long MaximumManifestBytes { get; init; } = DefaultMaximumManifestBytes;
    public string? ExpectedPackageSha256 { get; init; }
}

public sealed record AgentPackageImportCommand(
    AgentPackageImportMode Mode,
    string IdempotencyKey,
    string ExternalKey,
    string? ExpectedPackageSha256 = null,
    DateTimeOffset? ExpectedAgentVersion = null,
    string ExternalNamespace = AgentExternalIdentityNormalizer.PackageImportNamespace);

/// <summary>
/// Receipt of an agent package import, returned by <c>POST /api/agents/import-package</c>: the resulting agent, the
/// external identity bound to it, the imported package and what could not be resolved. A replayed request returns
/// the receipt stored by the original import.
/// </summary>
/// <param name="AgentId">
/// Identifier of the resulting agent: the packaged identifier for <c>create</c> and <c>replace-exact-version</c>, a
/// new identifier for <c>clone</c>.
/// </param>
/// <param name="Mode">
/// Mode that was applied, as a JSON integer: 0 Create, 1 ReplaceExactVersion, 2 Clone.
/// </param>
/// <param name="ExternalKey">
/// Key of the external identity now bound to the agent, normalized to lowercase; use it with
/// <c>externalNamespace</c> in <c>GET /api/agents/by-external-key/{externalNamespace}/{key}</c>.
/// </param>
/// <param name="PackageSha256">SHA-256 of the uploaded package file as 64 uppercase hexadecimal digits.</param>
/// <param name="PackageSchemaVersion">Schema version declared by the package manifest; currently always 1.0.</param>
/// <param name="ImportedVersion">
/// Revision of the resulting agent as an ISO 8601 instant with seven fractional digits. Send it unchanged as
/// <c>expectedAgentVersion</c> to replace this agent later with <c>replace-exact-version</c>.
/// </param>
/// <param name="ConfigurationSha256">
/// Configuration version of the resulting agent, 64 uppercase hexadecimal digits; it is also recorded on the external
/// identity binding as its <c>configurationVersion</c>.
/// </param>
/// <param name="UnresolvedPrerequisites">
/// Package references that could not be matched in this workspace and were dropped, sorted: <c>provider:</c>
/// followed by the packaged provider profile identifier, or <c>capability:</c> followed by the capability kind name
/// and key (for example <c>capability:Tool:crm-lookup</c>). Empty when everything was resolved.
/// </param>
/// <param name="Warnings">
/// Human-readable notes about the import, for example that secret references were removed or that a clone copies no
/// history. Their wording can change.
/// </param>
/// <param name="Replayed">
/// True when this receipt was returned for a repeated request with the same <c>Idempotency-Key</c>; nothing was
/// imported again.
/// </param>
public sealed record AgentPackageImportReceipt(
    Guid AgentId,
    AgentPackageImportMode Mode,
    string ExternalKey,
    string PackageSha256,
    string PackageSchemaVersion,
    string ImportedVersion,
    string ConfigurationSha256,
    IReadOnlyList<string> UnresolvedPrerequisites,
    IReadOnlyList<string> Warnings,
    bool Replayed)
{
    /// <summary>
    /// Namespace of the external identity now bound to the agent: the <c>externalNamespace</c> sent with the import,
    /// normalized to lowercase, or <c>package-import</c> when none was sent.
    /// </summary>
    public string ExternalNamespace { get; init; } = AgentExternalIdentityNormalizer.PackageImportNamespace;
}

public sealed record AgentPackageImportOperationRecord(
    string IdempotencyKey,
    string RequestFingerprint,
    AgentPackageImportReceipt Receipt,
    DateTimeOffset CompletedAtUtc);
