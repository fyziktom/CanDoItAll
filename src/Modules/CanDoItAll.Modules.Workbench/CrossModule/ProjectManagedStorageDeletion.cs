using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectManagedStorageDeletionPlan(
    IReadOnlyList<ProjectManagedStorageDeletionCandidate> Candidates,
    IReadOnlyList<ProjectManagedStorageDeletionOutcome> Outcomes)
{
    public IReadOnlyList<StorageObjectReference> References
        => Candidates.Select(candidate => candidate.Reference).ToArray();
}

internal enum ProjectManagedStorageOwnershipBasis
{
    CreationProvenanceV2 = 1,
    AuthoritativeBootstrapNamespace = 2,
    ImmutableContentAddress = 3,
    UnverifiedLegacyPayload = 4
}

internal sealed record ProjectManagedStorageDeletionCandidate(
    StorageObjectReference Reference,
    ProjectManagedStorageOwnershipBasis OwnershipBasis,
    string ExpectedPhysicalObjectFingerprint,
    string MatchedManagedPath);

internal enum ProjectManagedStorageDeletionOutcomeKind
{
    DeletedOrAlreadyAbsent = 1,
    RetainedByProvider = 2,
    RetainedWithoutOwnershipProof = 3
}

internal sealed record ProjectManagedStorageDeletionOutcome(
    StorageObjectReference Reference,
    ProjectManagedStorageDeletionOutcomeKind Kind,
    string Reason);

internal sealed class ProjectManagedStorageBindingException : IOException
{
    public ProjectManagedStorageBindingException(Guid bindingId, string message)
        : base($"Managed project binding '{bindingId:D}' is invalid: {message}")
    {
        BindingId = bindingId;
    }

    public Guid BindingId { get; }
}

internal sealed class ProjectManagedStorageObjectKey : IEquatable<ProjectManagedStorageObjectKey>
{
    private static readonly StringComparer WindowsPathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> WindowsReservedSegmentNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    private ProjectManagedStorageObjectKey(
        Guid? storageId,
        StorageProviderKind providerKind,
        StorageLocatorKind locatorKind,
        string locator)
    {
        StorageId = storageId;
        ProviderKind = providerKind;
        LocatorKind = locatorKind;
        Locator = locator;
    }

    public Guid? StorageId { get; }

    public StorageProviderKind ProviderKind { get; }

    public StorageLocatorKind LocatorKind { get; }

    public string Locator { get; }

    public static ProjectManagedStorageObjectKey FromReference(StorageObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (string.IsNullOrWhiteSpace(reference.Locator))
        {
            throw new InvalidDataException("A managed storage reference requires a locator.");
        }

        ValidateProviderLocatorCompatibility(reference);
        var locator = NormalizeLocator(
            reference.ProviderKind,
            reference.LocatorKind,
            reference.Locator);

        return new(
            reference.StorageId,
            reference.ProviderKind,
            reference.LocatorKind,
            locator);
    }

    public bool Equals(ProjectManagedStorageObjectKey? other)
    {
        if (other is null ||
            StorageId != other.StorageId ||
            ProviderKind != other.ProviderKind ||
            LocatorKind != other.LocatorKind)
        {
            return false;
        }

        return ResolveLocatorComparer(ProviderKind).Equals(Locator, other.Locator);
    }

    public override bool Equals(object? obj)
        => obj is ProjectManagedStorageObjectKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StorageId);
        hash.Add(ProviderKind);
        hash.Add(LocatorKind);
        hash.Add(Locator, ResolveLocatorComparer(ProviderKind));
        return hash.ToHashCode();
    }

    internal static bool LocatorEquals(
        StorageProviderKind providerKind,
        string left,
        string right)
        => ResolveLocatorComparer(providerKind).Equals(left, right);

    private static StringComparer ResolveLocatorComparer(StorageProviderKind providerKind)
        => providerKind == StorageProviderKind.Ftp ||
           providerKind == StorageProviderKind.FileSystem && OperatingSystem.IsWindows()
            ? WindowsPathComparer
            : StringComparer.Ordinal;

    private static string NormalizeLocator(
        StorageProviderKind providerKind,
        StorageLocatorKind locatorKind,
        string locator)
    {
        var trimmed = locator.Trim();
        if (!string.Equals(locator, trimmed, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A managed storage locator must not contain surrounding whitespace.");
        }

        if (locatorKind is not StorageLocatorKind.RelativePath and not StorageLocatorKind.RemotePath)
        {
            if (trimmed.Contains('/') || trimmed.Contains('\\'))
            {
                throw new InvalidDataException("A managed content-address locator must be a single canonical value.");
            }

            return trimmed;
        }

        if (trimmed.StartsWith('/') ||
            trimmed.StartsWith('\\') ||
            trimmed.Contains('\\'))
        {
            throw new InvalidDataException("A managed storage path must be a canonical relative path using forward slashes.");
        }

        var segments = trimmed.Split('/', StringSplitOptions.None);
        if (segments.Any(segment =>
                string.IsNullOrEmpty(segment) ||
                segment is "." or ".."))
        {
            throw new InvalidDataException("A managed storage path must not contain empty or dot segments.");
        }

        if (providerKind == StorageProviderKind.FileSystem &&
            OperatingSystem.IsWindows() &&
            segments.Any(IsNonCanonicalWindowsSegment))
        {
            throw new InvalidDataException(
                "A managed filesystem path contains a noncanonical Windows path segment.");
        }

        return string.Join('/', segments);
    }

    private static bool IsNonCanonicalWindowsSegment(string segment)
    {
        if (segment.EndsWith(' ') ||
            segment.EndsWith('.') ||
            segment.Contains(':'))
        {
            return true;
        }

        var deviceName = segment.Split('.', 2)[0];
        return WindowsReservedSegmentNames.Contains(deviceName);
    }

    private static void ValidateProviderLocatorCompatibility(StorageObjectReference reference)
    {
        var compatible = reference.ProviderKind switch
        {
            StorageProviderKind.FileSystem => reference.LocatorKind == StorageLocatorKind.RelativePath,
            StorageProviderKind.Ftp => reference.LocatorKind == StorageLocatorKind.RemotePath,
            StorageProviderKind.Ipfs => reference.LocatorKind == StorageLocatorKind.ContentAddress,
            _ => false
        };
        if (!compatible)
        {
            throw new InvalidDataException(
                $"Storage provider '{reference.ProviderKind}' cannot use locator kind '{reference.LocatorKind}' for managed project media.");
        }
    }
}

public sealed class ProjectManagedStoragePhysicalIdentityPolicy(
    FileSystemStoragePathPolicy fileSystemStoragePathPolicy)
{
    internal string ResolveWorkspaceRootPath()
        => fileSystemStoragePathPolicy.ResolveWorkspaceRootPath();

    internal string ResolveObjectFingerprint(
        StorageObjectReference reference,
        StorageCatalogRecord? storage,
        Guid? authoritativeBootstrapStorageId = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var key = ProjectManagedStorageObjectKey.FromReference(reference);
        string canonicalIdentity = reference.ProviderKind switch
        {
            StorageProviderKind.FileSystem => ResolveFileSystemIdentity(
                storage,
                key.Locator,
                authoritativeBootstrapStorageId),
            StorageProviderKind.Ftp => ResolveFtpIdentity(storage, key.Locator),
            StorageProviderKind.Ipfs => $"ipfs|{key.Locator}",
            _ => throw new InvalidOperationException(
                $"Storage provider '{reference.ProviderKind}' is not supported for managed project media.")
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIdentity)));
    }

    internal string ResolveConservativeLivenessKey(
        StorageObjectReference reference,
        StorageCatalogRecord? storage,
        Guid? authoritativeBootstrapStorageId = null)
    {
        var key = ProjectManagedStorageObjectKey.FromReference(reference);
        if (reference.ProviderKind == StorageProviderKind.FileSystem &&
            OperatingSystem.IsWindows())
        {
            var fullPath = ResolveFileSystemFullPath(
                storage,
                key.Locator,
                authoritativeBootstrapStorageId);
            var physicalIdentity =
                WindowsFileSystemObjectIdentity.ResolveConservativePhysicalIdentity(fullPath)
                    .ToUpperInvariant();
            return Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes($"filesystem|{physicalIdentity}")));
        }

        if (reference.ProviderKind != StorageProviderKind.Ftp)
        {
            return ResolveObjectFingerprint(
                reference,
                storage,
                authoritativeBootstrapStorageId);
        }

        var conservativeIdentity = ResolveFtpIdentity(storage, key.Locator).ToUpperInvariant();
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(conservativeIdentity)));
    }

    private string ResolveFileSystemIdentity(
        StorageCatalogRecord? storage,
        string locator,
        Guid? authoritativeBootstrapStorageId)
    {
        var fullPath = ResolveFileSystemFullPath(
            storage,
            locator,
            authoritativeBootstrapStorageId);
        var canonicalPath = OperatingSystem.IsWindows()
            ? WindowsFileSystemObjectIdentity.ResolveCanonicalPath(fullPath)
            : fullPath;
        return OperatingSystem.IsWindows()
            ? $"filesystem|{canonicalPath.ToUpperInvariant()}"
            : $"filesystem|{canonicalPath}";
    }

    private string ResolveFileSystemFullPath(
        StorageCatalogRecord? storage,
        string locator,
        Guid? authoritativeBootstrapStorageId)
    {
        var effectiveStorage = storage ?? new StorageCatalogRecord
        {
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = string.Empty
        };
        var isAuthoritativeBootstrap = authoritativeBootstrapStorageId.HasValue &&
            effectiveStorage.Id == authoritativeBootstrapStorageId.Value;
        if (isAuthoritativeBootstrap)
        {
            effectiveStorage = new StorageCatalogRecord
            {
                ProviderKind = StorageProviderKind.FileSystem,
                IsSystemDefault = true,
                EndpointOrRoot = string.Empty
            };
        }

        var fullPath = FileSystemStoragePathPolicy.ResolveReparseSafeFullPath(
            fileSystemStoragePathPolicy.ResolveFullPath(effectiveStorage, locator));
        return fullPath;
    }

    private static string ResolveFtpIdentity(
        StorageCatalogRecord? storage,
        string locator)
    {
        if (storage is null)
        {
            throw new InvalidOperationException(
                "Managed FTP media requires a current storage catalog entry.");
        }

        var address = FtpStorageAddressPolicy.ResolveObjectUri(storage, locator);
        var authority = address
            .GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            .ToLowerInvariant();
        var path = address.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        return $"ftp|{authority}|{path}";
    }
}

internal sealed record ProjectManagedStorageProvenance(
    string OwnershipKind,
    int Version,
    Guid AssetId,
    string RequestedManagedPath,
    Guid? StorageId,
    StorageProviderKind ProviderKind,
    StorageLocatorKind LocatorKind,
    string Locator,
    string PhysicalObjectFingerprint,
    string OriginalMetadataJson);

internal static class ProjectManagedStorageProvenancePolicy
{
    private const string OwnershipKind = "project-asset";
    private const int CurrentVersion = 2;
    private const string ManagedProjectMediaPrefix = "managed-files/project-media/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static StorageObjectReference Stamp(
        StorageObjectReference reference,
        string requestedManagedPath,
        StorageCatalogRecord storage,
        ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(physicalIdentityPolicy);
        var key = ProjectManagedStorageObjectKey.FromReference(reference);
        var path = NormalizeManagedPath(requestedManagedPath);
        var provenance = new ProjectManagedStorageProvenance(
            OwnershipKind,
            CurrentVersion,
            Guid.NewGuid(),
            path,
            key.StorageId,
            key.ProviderKind,
            key.LocatorKind,
            key.Locator,
            physicalIdentityPolicy.ResolveObjectFingerprint(reference, storage),
            string.IsNullOrWhiteSpace(reference.MetadataJson) ? "{}" : reference.MetadataJson);
        return reference with
        {
            MetadataJson = JsonSerializer.Serialize(provenance, JsonOptions)
        };
    }

    internal static bool HasManagedMarker(StorageObjectReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.MetadataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(reference.MetadataJson);
            return document.RootElement.TryGetProperty("ownershipKind", out var property) &&
                   string.Equals(property.GetString(), OwnershipKind, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryValidate(
        StorageObjectReference reference,
        string? mediaRelativePath,
        out string error)
    {
        error = string.Empty;
        ProjectManagedStorageProvenance? provenance;
        try
        {
            provenance = JsonSerializer.Deserialize<ProjectManagedStorageProvenance>(
                reference.MetadataJson,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"managed storage provenance cannot be parsed ({exception.GetType().Name}).";
            return false;
        }

        if (provenance is null ||
            !string.Equals(provenance.OwnershipKind, OwnershipKind, StringComparison.Ordinal) ||
            provenance.Version != CurrentVersion ||
            provenance.AssetId == Guid.Empty ||
            string.IsNullOrWhiteSpace(provenance.PhysicalObjectFingerprint))
        {
            error = "managed storage provenance is missing or unsupported.";
            return false;
        }

        string requestedPath;
        ProjectManagedStorageObjectKey key;
        try
        {
            requestedPath = NormalizeManagedPath(provenance.RequestedManagedPath);
            key = ProjectManagedStorageObjectKey.FromReference(reference);
        }
        catch (InvalidDataException exception)
        {
            error = exception.Message;
            return false;
        }

        if (key.StorageId != provenance.StorageId ||
            key.ProviderKind != provenance.ProviderKind ||
            key.LocatorKind != provenance.LocatorKind ||
            !ProjectManagedStorageObjectKey.LocatorEquals(
                key.ProviderKind,
                key.Locator,
                provenance.Locator))
        {
            error = "managed storage provenance does not match the bound storage reference.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(mediaRelativePath))
        {
            try
            {
                var mediaPath = NormalizeManagedPath(mediaRelativePath);
                if (!ProjectManagedStorageObjectKey.LocatorEquals(
                        reference.ProviderKind,
                        mediaPath,
                        requestedPath))
                {
                    error = "the media path does not match managed storage provenance.";
                    return false;
                }
            }
            catch (InvalidDataException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        if (reference.ProviderKind is StorageProviderKind.FileSystem or StorageProviderKind.Ftp &&
            !ProjectManagedStorageObjectKey.LocatorEquals(
                reference.ProviderKind,
                key.Locator,
                requestedPath))
        {
            error = "the mutable storage locator does not match the managed asset path.";
            return false;
        }

        return true;
    }

    internal static bool TryValidateCurrentStorage(
        StorageObjectReference reference,
        StorageCatalogRecord? storage,
        ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
        out string error)
        => TryValidateCurrentStorageCore(
            reference,
            storage,
            physicalIdentityPolicy,
            authoritativeBootstrapStorageId: null,
            out error);

    internal static bool TryValidateCurrentStorageForDeletion(
        StorageObjectReference reference,
        StorageCatalogRecord? storage,
        ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
        Guid authoritativeBootstrapStorageId,
        out string error)
        => TryValidateCurrentStorageCore(
            reference,
            storage,
            physicalIdentityPolicy,
            authoritativeBootstrapStorageId,
            out error);

    private static bool TryValidateCurrentStorageCore(
        StorageObjectReference reference,
        StorageCatalogRecord? storage,
        ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
        Guid? authoritativeBootstrapStorageId,
        out string error)
    {
        error = string.Empty;
        if (!HasManagedMarker(reference))
        {
            return true;
        }

        ProjectManagedStorageProvenance? provenance;
        try
        {
            provenance = JsonSerializer.Deserialize<ProjectManagedStorageProvenance>(
                reference.MetadataJson,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"managed storage provenance cannot be parsed ({exception.GetType().Name}).";
            return false;
        }

        if (provenance is null)
        {
            error = "managed storage provenance is missing.";
            return false;
        }

        string currentFingerprint;
        try
        {
            currentFingerprint = physicalIdentityPolicy.ResolveObjectFingerprint(
                reference,
                storage,
                authoritativeBootstrapStorageId);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                InvalidDataException or
                StorageBrowseException)
        {
            error = $"the current storage namespace cannot be validated ({exception.GetType().Name}).";
            return false;
        }

        if (!string.Equals(
                provenance.PhysicalObjectFingerprint,
                currentFingerprint,
                StringComparison.Ordinal))
        {
            error = "the current storage namespace differs from the asset creation namespace.";
            return false;
        }

        return true;
    }

    internal static bool IsManagedProjectMediaPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            _ = NormalizeManagedPath(path);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    internal static bool LooksLikeManagedProjectMediaNamespace(string? path)
    {
        var normalized = path?
            .Trim()
            .Replace('\\', '/')
            .TrimStart('/') ?? string.Empty;
        return normalized.StartsWith(
            ManagedProjectMediaPrefix,
            StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCanonicalGeneratedManagedProjectMediaPath(
        StorageProviderKind providerKind,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized;
        try
        {
            normalized = NormalizeManagedPath(path);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var comparison = providerKind == StorageProviderKind.FileSystem &&
                         OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(path, normalized, comparison) &&
               normalized.StartsWith(ManagedProjectMediaPrefix, comparison);
    }

    internal static string NormalizeManagedPath(string path)
    {
        var normalizedInput = path
            .Trim()
            .Replace('\\', '/')
            .TrimStart('/');
        var reference = new StorageObjectReference(
            null,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            normalizedInput);
        var normalized = ProjectManagedStorageObjectKey.FromReference(reference).Locator;
        if (!normalized.StartsWith(ManagedProjectMediaPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The managed asset path is outside the project-media namespace.");
        }

        return normalized;
    }
}

public sealed class ProjectManagedStorageDeletionPlanner(
    ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy)
{
    internal async Task<ProjectManagedStorageDeletionPlan> PlanAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> deletedProjectObjectIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(deletedProjectObjectIds);
        if (deletedProjectObjectIds.Count == 0)
        {
            return new([], []);
        }

        var deletedIds = deletedProjectObjectIds.Distinct().ToArray();
        var storages = await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var storageById = storages.ToDictionary(storage => storage.Id);
        var bootstrapFileSystemStorage =
            StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorage(
                storages,
                physicalIdentityPolicy.ResolveWorkspaceRootPath());
        var deletedBindings = await dbContext.Set<ProjectNodeBindingRecord>()
            .Where(binding => deletedIds.Contains(binding.ProjectObjectId))
            .ToListAsync(cancellationToken);
        var candidates = deletedBindings
            .Select(binding => ResolveManagedReference(
                binding,
                storageById,
                bootstrapFileSystemStorage,
                isDeletionCandidate: true))
            .Where(static resolved => resolved is not null)
            .Cast<ResolvedManagedStorageReference>()
            .GroupBy(resolved => resolved.PhysicalObjectFingerprint, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(resolved => resolved.HasSafeDeletionProvenance)
                    .First(),
                StringComparer.Ordinal);
        if (candidates.Count == 0)
        {
            return new([], []);
        }

        var survivingBindings = await dbContext.Set<ProjectNodeBindingRecord>()
            .Where(binding =>
                !deletedIds.Contains(binding.ProjectObjectId) &&
                (binding.StorageObjectReferenceJson != string.Empty ||
                 binding.MediaRelativePath != string.Empty))
            .ToListAsync(cancellationToken);
        var survivingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in survivingBindings)
        {
            var resolved = ResolveManagedReference(
                binding,
                storageById,
                bootstrapFileSystemStorage,
                isDeletionCandidate: false);
            if (resolved is not null)
            {
                survivingKeys.Add(resolved.PhysicalObjectFingerprint);
            }
        }

        var unreferencedCandidates = candidates
            .Where(candidate => !survivingKeys.Contains(candidate.Key))
            .Select(candidate => candidate.Value)
            .ToArray();
        var orderedCandidates = unreferencedCandidates
            .OrderBy(candidate => candidate.Reference.ProviderKind)
            .ThenBy(candidate => candidate.Reference.StorageId)
            .ThenBy(candidate => candidate.Reference.Locator, StringComparer.Ordinal)
            .ToArray();
        var retainedOutcomes = orderedCandidates
            .Where(candidate =>
                !candidate.HasSafeDeletionProvenance &&
                candidate.Reference.ProviderKind is
                    StorageProviderKind.FileSystem or StorageProviderKind.Ftp)
            .Select(candidate => new ProjectManagedStorageDeletionOutcome(
                candidate.Reference,
                ProjectManagedStorageDeletionOutcomeKind.RetainedWithoutOwnershipProof,
                "Legacy mutable media was retained because v2 creation-time physical ownership proof is unavailable; migrate or remove it manually."))
            .ToArray();
        return new(
            orderedCandidates.Select(candidate => candidate.Candidate).ToArray(),
            retainedOutcomes);
    }

    private static StorageObjectReference NormalizeBootstrapReference(
        StorageObjectReference reference,
        StorageCatalogRecord? bootstrapFileSystemStorage)
    {
        return reference.ProviderKind == StorageProviderKind.FileSystem &&
               reference.StorageId is null &&
               bootstrapFileSystemStorage is not null
            ? reference with { StorageId = bootstrapFileSystemStorage.Id }
            : reference;
    }

    private ResolvedManagedStorageReference? ResolveManagedReference(
        ProjectNodeBindingRecord binding,
        IReadOnlyDictionary<Guid, StorageCatalogRecord> storageById,
        StorageCatalogRecord? bootstrapFileSystemStorage,
        bool isDeletionCandidate)
    {
        var reference = ResolveManagedReferenceSyntax(binding);
        if (reference is null)
        {
            return null;
        }

        var normalizedReference = NormalizeBootstrapReference(
            reference,
            bootstrapFileSystemStorage);
        var storage = ResolveStorageForPlanning(
            binding.Id,
            normalizedReference,
            storageById,
            bootstrapFileSystemStorage);
        var hasProvenance = ProjectManagedStorageProvenancePolicy.HasManagedMarker(
            normalizedReference);
        if (hasProvenance)
        {
            var validCurrentStorage = bootstrapFileSystemStorage is null
                ? ProjectManagedStorageProvenancePolicy.TryValidateCurrentStorage(
                    normalizedReference,
                    storage,
                    physicalIdentityPolicy,
                    out var error)
                : ProjectManagedStorageProvenancePolicy.TryValidateCurrentStorageForDeletion(
                    normalizedReference,
                    storage,
                    physicalIdentityPolicy,
                    bootstrapFileSystemStorage.Id,
                    out error);
            if (!validCurrentStorage)
            {
                throw new ProjectManagedStorageBindingException(binding.Id, error);
            }
        }
        string fingerprint;
        try
        {
            fingerprint = physicalIdentityPolicy.ResolveConservativeLivenessKey(
                normalizedReference,
                storage,
                bootstrapFileSystemStorage?.Id ?? Guid.Empty);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                InvalidDataException or
                StorageBrowseException)
        {
            throw new ProjectManagedStorageBindingException(
                binding.Id,
                $"the physical storage identity cannot be resolved ({exception.GetType().Name}).");
        }

        var ownershipBasis = normalizedReference.ProviderKind switch
        {
            StorageProviderKind.Ipfs => ProjectManagedStorageOwnershipBasis.ImmutableContentAddress,
            _ when hasProvenance => ProjectManagedStorageOwnershipBasis.CreationProvenanceV2,
            StorageProviderKind.FileSystem when
                isDeletionCandidate &&
                bootstrapFileSystemStorage is not null &&
                storage?.Id == bootstrapFileSystemStorage.Id &&
                normalizedReference.LocatorKind == StorageLocatorKind.RelativePath =>
                ProjectManagedStorageOwnershipBasis.AuthoritativeBootstrapNamespace,
            _ => ProjectManagedStorageOwnershipBasis.UnverifiedLegacyPayload
        };
        var matchedManagedPath = normalizedReference.ProviderKind == StorageProviderKind.Ipfs
            ? string.Empty
            : ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(
                string.IsNullOrWhiteSpace(binding.MediaRelativePath)
                    ? normalizedReference.Locator
                    : binding.MediaRelativePath);
        var candidate = new ProjectManagedStorageDeletionCandidate(
            normalizedReference,
            ownershipBasis,
            physicalIdentityPolicy.ResolveObjectFingerprint(
                normalizedReference,
                storage,
                bootstrapFileSystemStorage?.Id ?? Guid.Empty),
            matchedManagedPath);
        return new(
            binding.Id,
            candidate,
            fingerprint,
            ownershipBasis != ProjectManagedStorageOwnershipBasis.UnverifiedLegacyPayload);
    }

    private static StorageCatalogRecord? ResolveStorageForPlanning(
        Guid bindingId,
        StorageObjectReference reference,
        IReadOnlyDictionary<Guid, StorageCatalogRecord> storageById,
        StorageCatalogRecord? bootstrapFileSystemStorage)
    {
        StorageCatalogRecord? storage = null;
        if (reference.StorageId.HasValue)
        {
            storageById.TryGetValue(reference.StorageId.Value, out storage);
        }
        else if (reference.ProviderKind == StorageProviderKind.FileSystem)
        {
            storage = bootstrapFileSystemStorage;
        }

        if (storage is null)
        {
            if (reference.ProviderKind == StorageProviderKind.Ipfs ||
                reference.ProviderKind == StorageProviderKind.FileSystem &&
                reference.StorageId is null)
            {
                return null;
            }

            throw new ProjectManagedStorageBindingException(
                bindingId,
                "the referenced mutable storage catalog entry was not found.");
        }

        if (storage.ProviderKind != reference.ProviderKind)
        {
            throw new ProjectManagedStorageBindingException(
                bindingId,
                "the storage catalog provider does not match the bound reference.");
        }

        return storage;
    }

    private static StorageObjectReference? ResolveManagedReferenceSyntax(ProjectNodeBindingRecord binding)
    {
        var mediaPathLooksLikeManagedNamespace =
            ProjectManagedStorageProvenancePolicy.LooksLikeManagedProjectMediaNamespace(
                binding.MediaRelativePath);
        var mediaPathLooksManaged = ProjectManagedStorageProvenancePolicy.IsManagedProjectMediaPath(
            binding.MediaRelativePath);
        if (mediaPathLooksLikeManagedNamespace && !mediaPathLooksManaged)
        {
            throw new ProjectManagedStorageBindingException(
                binding.Id,
                "the managed media path is not canonical.");
        }

        if (string.IsNullOrWhiteSpace(binding.StorageObjectReferenceJson))
        {
            return mediaPathLooksManaged
                ? ResolveLegacyReference(binding)
                : null;
        }

        if (!StorageJson.TryParseReference(binding.StorageObjectReferenceJson, out var reference) ||
            reference is null)
        {
            if (mediaPathLooksManaged)
            {
                throw new ProjectManagedStorageBindingException(
                    binding.Id,
                    "the storage reference JSON cannot be parsed.");
            }

            return null;
        }

        ValidateReference(binding.Id, reference);
        if (ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference))
        {
            if (!ProjectManagedStorageProvenancePolicy.TryValidate(
                    reference,
                    binding.MediaRelativePath,
                    out var error))
            {
                throw new ProjectManagedStorageBindingException(binding.Id, error);
            }

            return reference;
        }

        if (reference.ProviderKind == StorageProviderKind.Ipfs)
        {
            return reference;
        }

        var locatorLooksManaged = ProjectManagedStorageProvenancePolicy.IsManagedProjectMediaPath(
            reference.Locator);
        if (!mediaPathLooksManaged && !locatorLooksManaged)
        {
            return null;
        }

        if (!locatorLooksManaged)
        {
            throw new ProjectManagedStorageBindingException(
                binding.Id,
                "the managed media path points at an unrelated storage locator.");
        }

        if (mediaPathLooksManaged &&
            !ProjectManagedStorageObjectKey.LocatorEquals(
                reference.ProviderKind,
                ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(binding.MediaRelativePath),
                reference.Locator))
        {
            throw new ProjectManagedStorageBindingException(
                binding.Id,
                "the managed media path does not match the storage locator.");
        }

        return reference;
    }

    private static StorageObjectReference ResolveLegacyReference(ProjectNodeBindingRecord binding)
    {
        var legacyReference = StorageJson.CreateLegacyManagedFileReference(
            ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(binding.MediaRelativePath),
            binding.MediaContentType,
            binding.MediaOriginalFileName);
        ValidateReference(binding.Id, legacyReference);
        return legacyReference;
    }

    private static void ValidateReference(Guid bindingId, StorageObjectReference reference)
    {
        try
        {
            _ = ProjectManagedStorageObjectKey.FromReference(reference);
        }
        catch (InvalidDataException exception)
        {
            throw new ProjectManagedStorageBindingException(bindingId, exception.Message);
        }
    }

    private sealed record ResolvedManagedStorageReference(
        Guid BindingId,
        ProjectManagedStorageDeletionCandidate Candidate,
        string PhysicalObjectFingerprint,
        bool HasSafeDeletionProvenance)
    {
        public StorageObjectReference Reference => Candidate.Reference;
    }
}

public sealed class ProjectManagedStorageDeletionService(
    IStorageDriverRegistry storageDriverRegistry,
    ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    internal async Task<IReadOnlyList<ProjectManagedStorageDeletionOutcome>> DeleteAsync(
        IReadOnlyCollection<ProjectManagedStorageDeletionCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var outcomes = new List<ProjectManagedStorageDeletionOutcome>();
        foreach (var candidate in candidates
                     .GroupBy(item => ProjectManagedStorageObjectKey.FromReference(item.Reference))
                     .Select(group => group.First()))
        {
            var reference = candidate.Reference;
            if (reference.ProviderKind == StorageProviderKind.Ipfs)
            {
                outcomes.Add(new ProjectManagedStorageDeletionOutcome(
                    reference,
                    ProjectManagedStorageDeletionOutcomeKind.RetainedByProvider,
                    $"Storage provider '{reference.ProviderKind}' is immutable or does not support deletion."));
                continue;
            }

            if (candidate.OwnershipBasis == ProjectManagedStorageOwnershipBasis.UnverifiedLegacyPayload)
            {
                outcomes.Add(new ProjectManagedStorageDeletionOutcome(
                    reference,
                    ProjectManagedStorageDeletionOutcomeKind.RetainedWithoutOwnershipProof,
                    "Legacy mutable media was retained because v2 creation-time physical ownership proof is unavailable; migrate or remove it manually."));
                continue;
            }

            await using var coordinationDbContext =
                await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var bindingMutationScope = await SerializableMutationScope.BeginAsync(
                coordinationDbContext,
                ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey,
                cancellationToken);

            var storageResolution = await ResolveStorageAsync(
                reference,
                cancellationToken);
            var storage = storageResolution.Storage;
            var authoritativeBootstrapStorage =
                storageResolution.AuthoritativeBootstrapStorage;
            ValidateDeletionCandidate(
                candidate,
                storage,
                authoritativeBootstrapStorage);
            await EnsureNoSurvivingPhysicalReferenceAsync(
                candidate,
                storage,
                authoritativeBootstrapStorage,
                cancellationToken);

            var driver = storageDriverRegistry.Resolve(reference.ProviderKind);

            if (!driver.SupportedCapabilities.HasFlag(StorageCapability.Delete))
            {
                throw new InvalidOperationException(
                    $"Storage provider '{reference.ProviderKind}' unexpectedly does not support managed project media deletion.");
            }

            if (!storage.IsEnabled)
            {
                throw new InvalidOperationException(
                    $"Storage '{storage.Id:D}' is disabled and cannot delete managed project media.");
            }

            if (storage.IsReadOnly || !storage.CapabilityMask.HasFlag(StorageCapability.Delete))
            {
                throw new InvalidOperationException(
                    $"Storage '{storage.Id:D}' does not allow managed project media deletion.");
            }

            await driver.DeleteAsync(storage, reference, cancellationToken);
            await bindingMutationScope.CommitAsync(cancellationToken);
            outcomes.Add(new ProjectManagedStorageDeletionOutcome(
                reference,
                ProjectManagedStorageDeletionOutcomeKind.DeletedOrAlreadyAbsent,
                "The storage driver completed idempotent deletion."));
        }

        return outcomes;
    }

    private void ValidateDeletionCandidate(
        ProjectManagedStorageDeletionCandidate candidate,
        StorageCatalogRecord storage,
        StorageCatalogRecord? authoritativeBootstrapStorage)
    {
        var reference = candidate.Reference;
        switch (candidate.OwnershipBasis)
        {
            case ProjectManagedStorageOwnershipBasis.CreationProvenanceV2:
                if (!ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference))
                {
                    throw new InvalidOperationException(
                        "Managed project media deletion was refused because v2 creation provenance is missing.");
                }

                if (!ProjectManagedStorageProvenancePolicy.TryValidate(
                        reference,
                        mediaRelativePath: null,
                        out var referenceError))
                {
                    throw new InvalidOperationException(
                        $"Managed project media deletion was refused because {referenceError}");
                }

                var validCurrentStorage =
                    ProjectManagedStorageProvenancePolicy.TryValidateCurrentStorageForDeletion(
                        reference,
                        storage,
                        physicalIdentityPolicy,
                        authoritativeBootstrapStorage?.Id ?? Guid.Empty,
                        out var storageError);
                if (!validCurrentStorage)
                {
                    throw new InvalidOperationException(
                        $"Managed project media deletion was refused because {storageError}");
                }

                break;
            case ProjectManagedStorageOwnershipBasis.AuthoritativeBootstrapNamespace:
                ValidateAuthoritativeBootstrapCandidate(
                    candidate,
                    storage,
                    authoritativeBootstrapStorage);
                break;
            case ProjectManagedStorageOwnershipBasis.ImmutableContentAddress:
                throw new InvalidOperationException(
                    "Immutable managed media must be retained before mutable deletion validation.");
            case ProjectManagedStorageOwnershipBasis.UnverifiedLegacyPayload:
                throw new InvalidOperationException(
                    "Unverified legacy media must be retained before mutable deletion validation.");
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(candidate.OwnershipBasis),
                    candidate.OwnershipBasis,
                    null);
        }
    }

    private void ValidateAuthoritativeBootstrapCandidate(
        ProjectManagedStorageDeletionCandidate candidate,
        StorageCatalogRecord storage,
        StorageCatalogRecord? authoritativeBootstrapStorage)
    {
        var reference = candidate.Reference;
        if (reference.ProviderKind != StorageProviderKind.FileSystem ||
            reference.LocatorKind != StorageLocatorKind.RelativePath ||
            authoritativeBootstrapStorage is null ||
            storage.Id != authoritativeBootstrapStorage.Id ||
            reference.StorageId.HasValue &&
            reference.StorageId.Value != authoritativeBootstrapStorage.Id ||
            !ProjectManagedStorageProvenancePolicy.IsCanonicalGeneratedManagedProjectMediaPath(
                reference.ProviderKind,
                candidate.MatchedManagedPath) ||
            !ProjectManagedStorageObjectKey.LocatorEquals(
                reference.ProviderKind,
                reference.Locator,
                candidate.MatchedManagedPath))
        {
            throw new InvalidOperationException(
                "Authoritative bootstrap deletion evidence does not match the current managed project-media namespace.");
        }

        var currentFingerprint = physicalIdentityPolicy.ResolveObjectFingerprint(
            reference,
            storage,
            authoritativeBootstrapStorage.Id);
        if (string.IsNullOrWhiteSpace(candidate.ExpectedPhysicalObjectFingerprint) ||
            !string.Equals(
                candidate.ExpectedPhysicalObjectFingerprint,
                currentFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Authoritative bootstrap deletion evidence does not match the current workspace storage identity.");
        }
    }

    private async Task EnsureNoSurvivingPhysicalReferenceAsync(
        ProjectManagedStorageDeletionCandidate candidate,
        StorageCatalogRecord candidateStorage,
        StorageCatalogRecord? authoritativeBootstrapStorage,
        CancellationToken cancellationToken)
    {
        var authoritativeBootstrapStorageId =
            authoritativeBootstrapStorage?.Id ?? Guid.Empty;
        var candidateKey = physicalIdentityPolicy.ResolveConservativeLivenessKey(
            candidate.Reference,
            candidateStorage,
            authoritativeBootstrapStorageId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storages = await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var storageById = storages.ToDictionary(storage => storage.Id);
        var survivingBindings = await dbContext.Set<ProjectNodeBindingRecord>()
            .AsNoTracking()
            .Where(binding =>
                binding.StorageObjectReferenceJson != string.Empty ||
                binding.MediaRelativePath != string.Empty)
            .ToListAsync(cancellationToken);
        foreach (var binding in survivingBindings)
        {
            var survivingReference = ResolveSurvivingReference(binding);
            if (survivingReference is null)
            {
                continue;
            }

            if (survivingReference.ProviderKind != candidate.Reference.ProviderKind)
            {
                continue;
            }

            if (survivingReference.ProviderKind == StorageProviderKind.FileSystem &&
                survivingReference.StorageId is null &&
                authoritativeBootstrapStorage is not null)
            {
                survivingReference = survivingReference with
                {
                    StorageId = authoritativeBootstrapStorage.Id
                };
            }

            var survivingStorage = survivingReference.StorageId.HasValue &&
                                   storageById.TryGetValue(
                                       survivingReference.StorageId.Value,
                                       out var resolvedStorage)
                ? resolvedStorage
                : survivingReference.ProviderKind == StorageProviderKind.FileSystem &&
                  survivingReference.StorageId is null &&
                  authoritativeBootstrapStorage is not null
                    ? authoritativeBootstrapStorage
                    : null;
            if (survivingStorage is null)
            {
                throw new InvalidOperationException(
                    "Managed storage deletion was refused because a surviving binding has unresolved storage identity.");
            }

            var survivingKey = physicalIdentityPolicy.ResolveConservativeLivenessKey(
                survivingReference,
                survivingStorage,
                authoritativeBootstrapStorageId);
            if (string.Equals(candidateKey, survivingKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Managed storage deletion was refused because the physical object is still referenced by a project node.");
            }
        }
    }

    private static StorageObjectReference? ResolveSurvivingReference(
        ProjectNodeBindingRecord binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.StorageObjectReferenceJson))
        {
            if (StorageJson.TryParseReference(
                    binding.StorageObjectReferenceJson,
                    out var parsedReference) &&
                parsedReference is not null)
            {
                return parsedReference;
            }

            if (ProjectManagedStorageProvenancePolicy.LooksLikeManagedProjectMediaNamespace(
                    binding.MediaRelativePath))
            {
                throw new InvalidOperationException(
                    "Managed storage deletion was refused because a surviving managed binding is malformed.");
            }

            return null;
        }

        if (!ProjectManagedStorageProvenancePolicy.IsManagedProjectMediaPath(
                binding.MediaRelativePath))
        {
            return null;
        }

        return StorageJson.CreateLegacyManagedFileReference(
            ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(
                binding.MediaRelativePath),
            binding.MediaContentType,
            binding.MediaOriginalFileName);
    }

    private async Task<ManagedStorageResolution> ResolveStorageAsync(
        StorageObjectReference reference,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storages = await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var authoritativeBootstrapStorage =
            StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorage(
                storages,
                physicalIdentityPolicy.ResolveWorkspaceRootPath());
        StorageCatalogRecord? storage;
        if (reference.StorageId.HasValue)
        {
            storage = storages.SingleOrDefault(candidate =>
                candidate.Id == reference.StorageId.Value);
            if (storage is null)
            {
                throw new InvalidOperationException(
                    $"Storage '{reference.StorageId.Value:D}' for managed project media was not found.");
            }
        }
        else if (reference.ProviderKind == StorageProviderKind.FileSystem)
        {
            storage = authoritativeBootstrapStorage;
            if (storage is null)
            {
                throw new InvalidOperationException(
                    "Authoritative bootstrap filesystem storage was not found.");
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Managed project media for provider '{reference.ProviderKind}' requires a storage id.");
        }

        if (storage.ProviderKind != reference.ProviderKind)
        {
            throw new InvalidOperationException(
                $"Storage '{storage.Id:D}' uses provider '{storage.ProviderKind}', but the managed media reference requires '{reference.ProviderKind}'.");
        }

        return new ManagedStorageResolution(
            storage,
            authoritativeBootstrapStorage);
    }

    private sealed record ManagedStorageResolution(
        StorageCatalogRecord Storage,
        StorageCatalogRecord? AuthoritativeBootstrapStorage);
}
