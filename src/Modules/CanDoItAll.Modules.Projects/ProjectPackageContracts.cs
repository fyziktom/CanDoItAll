using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectPackageManifest
{
    public const string CurrentFormat = "candoitall.projects.v2";

    public Guid PackageId { get; set; }

    public string Format { get; set; } = CurrentFormat;

    public Guid SourceProfileId { get; set; }

    public string SourceProfileName { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public int TotalRecordCount { get; set; }

    public List<ProjectPackageTableManifest> Tables { get; set; } = [];

    public List<ProjectPackageStorageFileManifest> StorageFiles { get; set; } = [];

    public List<ProjectPackageImmutableStorageReferenceManifest> ImmutableStorageReferences { get; set; } = [];

    public List<string> Warnings { get; set; } = [];
}

public sealed class ProjectPackageTableManifest
{
    public string Name { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int RowCount { get; set; }

    public long Length { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class ProjectPackageStorageFileManifest
{
    public Guid? SourceStorageId { get; set; }

    public StorageProviderKind ProviderKind { get; set; }

    public StorageLocatorKind LocatorKind { get; set; }

    public string Locator { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string PackagePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public long Length { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class ProjectPackageImmutableStorageReferenceManifest
{
    public Guid? SourceStorageId { get; set; }

    public StorageProviderKind ProviderKind { get; set; }

    public StorageLocatorKind LocatorKind { get; set; }

    public string Locator { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public long Length { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class ProjectPackageExportRequest
{
    public Guid? SourceProfileId { get; set; }

    public string? PackagePath { get; set; }
}

public sealed class ProjectPackageImportRequest
{
    public string PackagePath { get; set; } = string.Empty;

    public Guid? TargetProfileId { get; set; }

    public bool ReplaceExisting { get; set; } = true;
}

public sealed record ProjectPackageExportResult(
    ProjectPackageManifest Manifest,
    string PackagePath);

public sealed record ProjectPackageImportResult(
    ProjectPackageManifest Manifest,
    int RecordsImported,
    int StorageFilesImported);

public interface IProjectPackageService
{
    Task<Result<ProjectPackageExportResult>> ExportAllAsync(
        ProjectPackageExportRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPackageImportResult>> ImportAllAsync(
        ProjectPackageImportRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPackageManifest>> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}
