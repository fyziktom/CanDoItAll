namespace CanDoItAll.AgentFramework.Models;

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
    public string ExternalNamespace { get; init; } = AgentExternalIdentityNormalizer.PackageImportNamespace;
}

public sealed record AgentPackageImportOperationRecord(
    string IdempotencyKey,
    string RequestFingerprint,
    AgentPackageImportReceipt Receipt,
    DateTimeOffset CompletedAtUtc);
