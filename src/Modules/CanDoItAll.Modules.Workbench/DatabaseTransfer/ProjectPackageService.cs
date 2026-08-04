using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Modules.Workbench.ProjectPackageArchive;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectPackageService(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IAppDatabaseBootstrapper bootstrapper,
    IProfileAppDbContextFactory dbContextFactory,
    IControlPlanePathResolver controlPlanePathResolver,
    IStorageDriverRegistry storageDrivers,
    ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
    IClock clock,
    ILogger<StoragePlacementService> placementLogger,
    ProjectTransferTargetStateGuard targetStateGuard,
    ILogger<ProjectPackageService> logger) : IProjectPackageService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly ProjectPackageStorageExporter _storageExporter = new(
        storageDrivers,
        physicalIdentityPolicy);
    private readonly ProjectPackageStorageImporter _storageImporter = new(
        storageDrivers,
        physicalIdentityPolicy,
        clock,
        placementLogger,
        logger);

    public async Task<Result<ProjectPackageExportResult>> ExportAllAsync(
        ProjectPackageExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var sourceProfile = request.SourceProfileId.HasValue
                ? profileAccessor.ResolveProfile(request.SourceProfileId.Value)
                : profileAccessor.ResolveCurrentProfile();
            await bootstrapper.EnsureProfileReadyAsync(sourceProfile, cancellationToken);

            await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(
                sourceProfile,
                cancellationToken);
            await ProjectTransferDataSet.EnsureSchemasAsync(dbContext, cancellationToken);
            await using var snapshotScope = await SerializableMutationScope.BeginAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey,
                cancellationToken);
            var dataSet = await ProjectTransferDataSet.LoadAsync(dbContext, cancellationToken);
            dataSet.PrepareForPackageExport();
            dataSet.ValidateForImport();
            var counts = dataSet.Counts;
            if (counts.Projects == 0)
            {
                return Result<ProjectPackageExportResult>.Failure(
                    Error.Validation("The source database has no projects to export."));
            }

            var storages = await dbContext.Set<StorageCatalogRecord>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var packageId = Guid.NewGuid();
            var createdUtc = clock.GetUtcNow();
            var packagePath = ResolveExportPackagePath(request.PackagePath, packageId, createdUtc);
            var workingRoot = Path.Combine(Path.GetTempPath(), $"cda-project-package-{packageId:N}");
            Directory.CreateDirectory(workingRoot);

            try
            {
                var manifest = new ProjectPackageManifest
                {
                    PackageId = packageId,
                    SourceProfileId = sourceProfile.Profile.Id,
                    SourceProfileName = sourceProfile.Profile.DisplayName,
                    CreatedUtc = createdUtc,
                    TotalRecordCount = counts.Total
                };

                await WritePayloadsAsync(workingRoot, dataSet, manifest, cancellationToken);
                await _storageExporter.CopyReferencedStorageAsync(
                    sourceProfile,
                    workingRoot,
                    dataSet,
                    storages,
                    manifest,
                    cancellationToken);
                await snapshotScope.CommitAsync(cancellationToken);
                await WriteJsonFileAsync(
                    Path.Combine(workingRoot, "manifest.json"),
                    manifest,
                    cancellationToken);
                await CreatePackageArchiveAsync(
                    workingRoot,
                    packagePath,
                    createdUtc,
                    cancellationToken);

                logger.LogInformation(
                    "Exported project package {PackageId} from profile {ProfileId}. Records={RecordCount}. MutableStorageFiles={StorageFileCount}. ImmutableReferences={ImmutableReferenceCount}.",
                    packageId,
                    sourceProfile.Profile.Id,
                    manifest.TotalRecordCount,
                    manifest.StorageFiles.Count,
                    manifest.ImmutableStorageReferences.Count);

                return Result<ProjectPackageExportResult>.Success(
                    new ProjectPackageExportResult(manifest, packagePath));
            }
            finally
            {
                DeleteDirectoryIfExists(workingRoot);
            }
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(
                "Project package export validation failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return Result<ProjectPackageExportResult>.Failure(
                Error.Validation(exception.Message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Project package export failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return Result<ProjectPackageExportResult>.Failure(
                Error.Failure(
                    "Project package export failed. Verify the source storage configuration and choose a new package path."));
        }
    }

    public async Task<Result<ProjectPackageImportResult>> ImportAllAsync(
        ProjectPackageImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PackagePath))
        {
            return Result<ProjectPackageImportResult>.Failure(
                Error.Validation("Choose a project package to import."));
        }

        if (!request.TargetProfileId.HasValue)
        {
            return Result<ProjectPackageImportResult>.Failure(
                Error.Validation(
                    "Select an inactive target database profile. Project package import cannot replace the running profile."));
        }

        try
        {
            var targetProfile = profileAccessor.ResolveProfile(request.TargetProfileId.Value);
            if (IsCurrentProfile(targetProfile))
            {
                return ActiveTargetFailure();
            }

            var packagePath = Path.GetFullPath(request.PackagePath);
            var extractionRoot = await ExtractPackageAsync(packagePath, cancellationToken);
            try
            {
                var manifest = await ReadExtractedManifestAsync(extractionRoot, cancellationToken);
                ValidateManifest(manifest);
                var dataSet = await ReadPayloadsAsync(
                    extractionRoot,
                    manifest,
                    cancellationToken);
                dataSet.ValidatePackageImportSafety();
                dataSet.ValidateForImport();
                if (dataSet.Counts.Total != manifest.TotalRecordCount)
                {
                    throw new InvalidDataException(
                        "The project package total record count does not match its payloads.");
                }

                var storagePreflight = await _storageImporter.PreflightImportAsync(
                    extractionRoot,
                    manifest,
                    dataSet,
                    cancellationToken);

                await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);
                if (IsCurrentProfile(targetProfile))
                {
                    return ActiveTargetFailure();
                }

                await using var targetDbContext = await dbContextFactory.CreateDbContextForProfileAsync(
                    targetProfile,
                    cancellationToken);
                var initialTargetCounts = await ProjectTransferDataSet.CountAsync(
                    targetDbContext,
                    cancellationToken);
                var initialTargetResidues = await targetStateGuard
                    .FindResiduesAsync(targetDbContext, cancellationToken);
                if (initialTargetCounts.Total > 0 || initialTargetResidues.Count > 0)
                {
                    var residueDetails = initialTargetResidues.Count == 0
                        ? string.Empty
                        : $" Related state found: {ProjectTransferTargetStateGuard.Describe(initialTargetResidues)}.";
                    return Result<ProjectPackageImportResult>.Failure(
                        Error.Validation(
                            "Project package v2 import requires an inactive target with no project or project-related state. " +
                            $"Existing target data and storage were left unchanged.{residueDetails} Choose or create an empty target profile."));
                }

                var storagePlan = await _storageImporter.BuildTargetStoragePlanAsync(
                    targetDbContext,
                    targetProfile,
                    cancellationToken);
                var stagedWrites = _storageImporter.CreateStagingJournal();
                var committed = false;
                try
                {
                    var storageFilesImported = await _storageImporter.RewriteStorageBindingsAsync(
                        extractionRoot,
                        manifest,
                        dataSet,
                        storagePreflight,
                        storagePlan,
                        stagedWrites,
                        cancellationToken);

                    if (IsCurrentProfile(targetProfile))
                    {
                        throw new InvalidOperationException(
                            "The selected target profile became active while the package was being prepared. Import was stopped before project data changed.");
                    }

                    await using var importScope = await SerializableMutationScope.BeginAsync(
                        targetDbContext,
                        ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey,
                        cancellationToken);
                    await targetStateGuard.AcquireExclusiveImportLocksAsync(
                        targetDbContext,
                        cancellationToken);
                    var currentTargetCounts = await ProjectTransferDataSet.CountAsync(
                        targetDbContext,
                        cancellationToken);
                    var currentTargetResidues = await targetStateGuard
                        .FindResiduesAsync(targetDbContext, cancellationToken);
                    if (currentTargetCounts.Total > 0 || currentTargetResidues.Count > 0)
                    {
                        var residueDetails = currentTargetResidues.Count == 0
                            ? string.Empty
                            : $" Related state found: {ProjectTransferTargetStateGuard.Describe(currentTargetResidues)}.";
                        throw new InvalidDataException(
                            "The inactive target acquired project or project-related data while the package was being prepared; import was stopped without replacing it." +
                            residueDetails);
                    }

                    await ProjectPackageStorageImporter.ValidateTargetStoragePlanStillCurrentAsync(
                        targetDbContext,
                        storagePlan,
                        cancellationToken);
                    await ProjectPackageStorageImporter.PersistPendingStorageCatalogAsync(
                        targetDbContext,
                        storagePlan,
                        cancellationToken);
                    await ProjectTransferDataSet.ClearAsync(targetDbContext, cancellationToken);
                    await ProjectTransferDataSet.SaveAsync(targetDbContext, dataSet, cancellationToken);
                    await importScope.CommitAsync(cancellationToken);
                    committed = true;

                    logger.LogInformation(
                        "Imported project package {PackageId} into inactive profile {ProfileId}. Records={RecordCount}. MutableStorageFiles={StorageFileCount}.",
                        manifest.PackageId,
                        targetProfile.Profile.Id,
                        dataSet.Counts.Total,
                        storageFilesImported);

                    return Result<ProjectPackageImportResult>.Success(
                        new ProjectPackageImportResult(
                            manifest,
                            dataSet.Counts.Total,
                            storageFilesImported));
                }
                finally
                {
                    if (!committed)
                    {
                        await _storageImporter.CleanupStagedWritesAsync(stagedWrites);
                    }
                }
            }
            finally
            {
                DeleteDirectoryIfExists(extractionRoot);
            }
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(
                "Project package import validation failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return Result<ProjectPackageImportResult>.Failure(
                Error.Validation(exception.Message));
        }
        catch (ProjectPackageCompensationException exception)
        {
            logger.LogError(
                "Project package import compensation is incomplete. FailureCount={FailureCount}.",
                exception.Failures.Count);
            return Result<ProjectPackageImportResult>.Failure(
                Error.Failure(exception.Message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Project package import failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return Result<ProjectPackageImportResult>.Failure(
                Error.Failure(
                    "Project package import failed before completion. The inactive target project data was not replaced."));
        }
    }

    public async Task<Result<ProjectPackageManifest>> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return Result<ProjectPackageManifest>.Failure(
                Error.Validation("Choose a project package."));
        }

        try
        {
            var extractionRoot = await ExtractPackageAsync(
                Path.GetFullPath(packagePath),
                cancellationToken);
            try
            {
                var manifest = await ReadExtractedManifestAsync(
                    extractionRoot,
                    cancellationToken);
                ValidateManifest(manifest);
                return Result<ProjectPackageManifest>.Success(manifest);
            }
            finally
            {
                DeleteDirectoryIfExists(extractionRoot);
            }
        }
        catch (InvalidDataException exception)
        {
            return Result<ProjectPackageManifest>.Failure(
                Error.Validation(exception.Message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Project package manifest read failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return Result<ProjectPackageManifest>.Failure(
                Error.Failure("Reading the project package manifest failed."));
        }
    }

    private Result<ProjectPackageImportResult> ActiveTargetFailure()
        => Result<ProjectPackageImportResult>.Failure(
            Error.Validation(
                "The selected target database profile is currently running. Activate a different profile, restart, and import into this profile while it is inactive."));

    private bool IsCurrentProfile(ResolvedDatabaseProfile profile)
        => profileAccessor.ResolveCurrentProfile().Profile.Id == profile.Profile.Id;

    private string ResolveExportPackagePath(
        string? requestedPath,
        Guid packageId,
        DateTimeOffset createdUtc)
    {
        var fileName = $"projects-{createdUtc:yyyyMMdd-HHmmss}-{packageId:N}.cda-projects.zip";
        string packagePath;
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            var packageRoot = Path.Combine(
                controlPlanePathResolver.ResolveRootPath(),
                "project-packages");
            Directory.CreateDirectory(packageRoot);
            packagePath = Path.Combine(packageRoot, fileName);
        }
        else
        {
            var fullPath = Path.GetFullPath(requestedPath);
            packagePath = string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)) ||
                          Directory.Exists(fullPath)
                ? Path.Combine(fullPath, fileName)
                : fullPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        if (File.Exists(packagePath))
        {
            throw new IOException(
                $"Project package '{packagePath}' already exists. Choose a new path; exports never overwrite an existing package.");
        }

        return packagePath;
    }

    private static async Task WritePayloadsAsync(
        string workingRoot,
        ProjectTransferDataSet dataSet,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.Projects, dataSet.Projects, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.Phases, dataSet.Phases, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.Options, dataSet.Options, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.HierarchyLinks, dataSet.HierarchyLinks, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.Objects, dataSet.Objects, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.ObjectLinks, dataSet.ObjectLinks, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.ProjectionLayouts, dataSet.ProjectionLayouts, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.NodeBindings, dataSet.NodeBindings, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.NodeReferences, dataSet.NodeReferences, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.NodeLifecycleEvents, dataSet.NodeLifecycleEvents, cancellationToken);

        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.CrossModuleMutations, dataSet.CrossModuleMutations, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.ViewStates, dataSet.ViewStates, cancellationToken);
    }

    private static async Task<ProjectTransferDataSet> ReadPayloadsAsync(
        string extractionRoot,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        return new ProjectTransferDataSet
        {
            Projects = await ReadTableAsync<Project>(extractionRoot, manifest, ProjectPackageTables.Projects, cancellationToken),
            Phases = await ReadTableAsync<ProjectPhase>(extractionRoot, manifest, ProjectPackageTables.Phases, cancellationToken),
            Options = await ReadTableAsync<ProjectOptionSelection>(extractionRoot, manifest, ProjectPackageTables.Options, cancellationToken),
            HierarchyLinks = await ReadTableAsync<ProjectHierarchyLink>(extractionRoot, manifest, ProjectPackageTables.HierarchyLinks, cancellationToken),
            Objects = await ReadTableAsync<ProjectObjectRecord>(extractionRoot, manifest, ProjectPackageTables.Objects, cancellationToken),
            ObjectLinks = await ReadTableAsync<ProjectObjectLinkRecord>(extractionRoot, manifest, ProjectPackageTables.ObjectLinks, cancellationToken),
            ProjectionLayouts = await ReadTableAsync<ProjectStructureProjectionLayoutRecord>(extractionRoot, manifest, ProjectPackageTables.ProjectionLayouts, cancellationToken),
            NodeBindings = await ReadTableAsync<ProjectNodeBindingRecord>(extractionRoot, manifest, ProjectPackageTables.NodeBindings, cancellationToken),
            NodeReferences = await ReadTableAsync<ProjectNodeReferenceRecord>(extractionRoot, manifest, ProjectPackageTables.NodeReferences, cancellationToken),
            NodeLifecycleEvents = await ReadTableAsync<ProjectNodeLifecycleEventRecord>(extractionRoot, manifest, ProjectPackageTables.NodeLifecycleEvents, cancellationToken),
            CrossModuleMutations = await ReadTableAsync<ProjectCrossModuleMutationRecord>(extractionRoot, manifest, ProjectPackageTables.CrossModuleMutations, cancellationToken),
            ViewStates = await ReadTableAsync<ProjectWorkbenchViewStateRecord>(extractionRoot, manifest, ProjectPackageTables.ViewStates, cancellationToken)
        };
    }

    private static async Task WriteTableAsync<T>(
        string workingRoot,
        ProjectPackageManifest manifest,
        ProjectPackageTable table,
        IReadOnlyCollection<T> rows,
        CancellationToken cancellationToken)
        where T : class
    {
        var filePath = ResolvePackageFilePath(workingRoot, table.FilePath);
        await WriteJsonFileAsync(filePath, rows, cancellationToken);
        var integrity = await ComputeFileIntegrityAsync(
            filePath,
            MaximumArchiveEntryBytes,
            cancellationToken);
        manifest.Tables.Add(new ProjectPackageTableManifest
        {
            Name = table.Name,
            FilePath = table.FilePath,
            RowCount = rows.Count,
            Length = integrity.Length,
            Sha256 = integrity.Sha256
        });
    }

    private static async Task<List<T>> ReadTableAsync<T>(
        string extractionRoot,
        ProjectPackageManifest manifest,
        ProjectPackageTable table,
        CancellationToken cancellationToken)
        where T : class
    {
        var tableManifest = manifest.Tables.SingleOrDefault(
            item => string.Equals(item.Name, table.Name, StringComparison.Ordinal));
        if (tableManifest is null)
        {
            throw new InvalidDataException(
                $"The project package is missing the '{table.Name}' table manifest.");
        }

        var filePath = ResolvePackageFilePath(extractionRoot, tableManifest.FilePath);
        if (!File.Exists(filePath))
        {
            throw new InvalidDataException(
                $"The project package is missing the '{tableManifest.FilePath}' table payload.");
        }

        await VerifyFileIntegrityAsync(
            filePath,
            tableManifest.Length,
            tableManifest.Sha256,
            MaximumArchiveEntryBytes,
            cancellationToken);
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var rows = JsonSerializer.Deserialize<List<T>>(json, SerializerOptions)
            ?? throw new InvalidDataException(
                $"The project package table '{tableManifest.Name}' is invalid.");
        if (rows.Count != tableManifest.RowCount)
        {
            throw new InvalidDataException(
                $"The project package table '{tableManifest.Name}' row count does not match its manifest.");
        }

        return rows;
    }

    private static async Task<ProjectPackageManifest> ReadExtractedManifestAsync(
        string extractionRoot,
        CancellationToken cancellationToken)
    {
        var manifestPath = ResolvePackageFilePath(
            extractionRoot,
            "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException(
                "The project package is missing manifest.json.");
        }

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        return JsonSerializer.Deserialize<ProjectPackageManifest>(json, SerializerOptions)
            ?? throw new InvalidDataException(
                "The project package manifest is invalid.");
    }

    internal static void ValidateManifest(ProjectPackageManifest manifest)
    {
        if (!string.Equals(
                manifest.Format,
                ProjectPackageManifest.CurrentFormat,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported project package format '{manifest.Format}'. Only '{ProjectPackageManifest.CurrentFormat}' packages with integrity metadata can be imported.");
        }

        if (manifest.PackageId == Guid.Empty ||
            manifest.SourceProfileId == Guid.Empty ||
            manifest.TotalRecordCount < 0 ||
            manifest.Tables is null ||
            manifest.StorageFiles is null ||
            manifest.ImmutableStorageReferences is null ||
            manifest.Warnings is null)
        {
            throw new InvalidDataException(
                "The project package manifest is missing required v2 values.");
        }

        if (manifest.Tables.Count != ProjectPackageTables.All.Count)
        {
            throw new InvalidDataException(
                "The project package table manifest count is invalid.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in manifest.Tables)
        {
            if (!names.Add(table.Name) ||
                !paths.Add(NormalizePackageRelativePath(
                    table.FilePath,
                    isDirectory: false)) ||
                table.RowCount < 0 ||
                table.Length < 0 ||
                table.Length > MaximumArchiveEntryBytes ||
                !IsSha256(table.Sha256))
            {
                throw new InvalidDataException(
                    "The project package contains an invalid or duplicate table manifest entry.");
            }
        }

        if (ProjectPackageTables.All.Any(table => !names.Contains(table.Name)))
        {
            throw new InvalidDataException(
                "The project package does not contain the exact required project tables.");
        }
    }

    private static async Task WriteJsonFileAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static ProjectManagedStorageObjectKey CreateStorageKey(
        Guid? storageId,
        StorageProviderKind providerKind,
        StorageLocatorKind locatorKind,
        string locator)
    {
        if (storageId == Guid.Empty ||
            !Enum.IsDefined(providerKind) ||
            !Enum.IsDefined(locatorKind))
        {
            throw new InvalidDataException(
                "The project package contains invalid storage identity values.");
        }

        return ProjectManagedStorageObjectKey.FromReference(
            new StorageObjectReference(
                storageId,
                providerKind,
                locatorKind,
                locator));
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ProjectPackageTable(string Name, string FilePath);

    private static class ProjectPackageTables
    {
        public static ProjectPackageTable Projects { get; } = new(
            "Projects_Projects",
            "tables/projects.json");

        public static ProjectPackageTable Phases { get; } = new(
            "Projects_ProjectPhases",
            "tables/project-phases.json");

        public static ProjectPackageTable Options { get; } = new(
            "Projects_ProjectOptionSelections",
            "tables/project-option-selections.json");

        public static ProjectPackageTable HierarchyLinks { get; } = new(
            "Projects_ProjectHierarchyLinks",
            "tables/project-hierarchy-links.json");

        public static ProjectPackageTable Objects { get; } = new(
            "Workbench_ProjectObjects",
            "tables/workbench-project-objects.json");

        public static ProjectPackageTable ObjectLinks { get; } = new(
            "Workbench_ProjectObjectLinks",
            "tables/workbench-project-object-links.json");

        public static ProjectPackageTable ProjectionLayouts { get; } = new(
            "Workbench_ProjectProjectionLayouts",
            "tables/workbench-project-projection-layouts.json");

        public static ProjectPackageTable NodeBindings { get; } = new(
            "Workbench_ProjectNodeBindings",
            "tables/workbench-project-node-bindings.json");

        public static ProjectPackageTable NodeReferences { get; } = new(
            "Workbench_ProjectNodeReferences",
            "tables/workbench-project-node-references.json");

        public static ProjectPackageTable NodeLifecycleEvents { get; } = new(
            "Workbench_ProjectNodeLifecycleEvents",
            "tables/workbench-project-node-lifecycle-events.json");

        public static ProjectPackageTable CrossModuleMutations { get; } = new(
            "Workbench_ProjectCrossModuleMutations",
            "tables/workbench-project-cross-module-mutations.json");

        public static ProjectPackageTable ViewStates { get; } = new(
            "Workbench_ViewStates",
            "tables/workbench-view-states.json");

        public static IReadOnlyList<ProjectPackageTable> All { get; } =
        [
            Projects,
            Phases,
            Options,
            HierarchyLinks,
            Objects,
            ObjectLinks,
            ProjectionLayouts,
            NodeBindings,
            NodeReferences,
            NodeLifecycleEvents,
            CrossModuleMutations,
            ViewStates
        ];
    }
}
