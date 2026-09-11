using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Modules.Workbench.ProjectPackageArchive;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectPackageService(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IAppDatabaseBootstrapper bootstrapper,
    DatabaseTransferOperationRunner operations,
    ProjectsProfileTransferStore projects,
    StorageProfileTransferService storageTransfers,
    IControlPlanePathResolver controlPlanePathResolver,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IClock clock,
    ProjectTransferTargetStateGuard targetStateGuard,
    ILogger<ProjectPackageService> logger) : IProjectPackageService {
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly ProjectTransferStore data = new(operations, projects);
    private readonly ProjectPackageStorageExporter _storageExporter = new(
        storageTransfers,
        physicalPathPolicyFactory);
    private readonly ProjectPackageStorageImporter _storageImporter = new(
        storageTransfers,
        physicalPathPolicyFactory,
        logger);

    public async Task<Result<ProjectPackageExportResult>> ExportAllAsync(
        ProjectPackageExportRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            var sourceProfile = request.SourceProfileId.HasValue
                ? profileAccessor.ResolveProfile(request.SourceProfileId.Value)
                : profileAccessor.ResolveCurrentProfile();
            await bootstrapper.EnsureProfileReadyAsync(sourceProfile, cancellationToken);

            string? workingRoot = null;
            try {
                var snapshot = await operations.RunSerializableAsync<ProjectPackageExportSnapshot?>(sourceProfile,
                    [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], async (database, token) => {
                    var dataSet = await data.LoadAsync(database, token);
                    dataSet.PrepareForPackageExport();
                    dataSet.ValidateForImport();
                    if (dataSet.Counts.Total == 0) {
                        return null;
                    }
                    var packageId = Guid.NewGuid();
                    var createdUtc = clock.GetUtcNow();
                    var packagePath = ResolveExportPackagePath(request.PackagePath, packageId, createdUtc);
                    workingRoot = Path.Combine(Path.GetTempPath(), $"cda-project-package-{packageId:N}");
                    Directory.CreateDirectory(workingRoot);
                    var manifest = new ProjectPackageManifest {
                        PackageId = packageId,
                        SourceProfileId = sourceProfile.Profile.Id,
                        SourceProfileName = sourceProfile.Profile.DisplayName,
                        CreatedUtc = createdUtc,
                        HistoryDisposition = ProjectPackageHistoryDisposition.PreserveAsHistory,
                        WorkAssignmentHistoryVersion = ProjectPackageManifest.CurrentWorkAssignmentHistoryVersion,
                        ProcessAssetHistoryVersion = ProjectPackageManifest.CurrentProcessAssetHistoryVersion,
                        TotalRecordCount = dataSet.Counts.Total
                    };
                    await WritePayloadsAsync(workingRoot, dataSet, manifest, token);
                    await storageTransfers.WithSourceAsync(database, sourceProfile, async (storageSession, storageToken) => {
                        await _storageExporter.CopyReferencedStorageAsync(sourceProfile, workingRoot, dataSet, storageSession, manifest, storageToken);
                        return true;
                    }, token);
                    return new ProjectPackageExportSnapshot(manifest, packagePath);
                }, cancellationToken);
                if (snapshot is null) {
                    return Result<ProjectPackageExportResult>.Failure(Error.Validation("The source database has no projects or retained project history to export."));
                }
                var manifest = snapshot.Manifest;
                await WriteJsonFileAsync(Path.Combine(workingRoot!, "manifest.json"), manifest, cancellationToken);
                await CreatePackageArchiveAsync(workingRoot!, snapshot.PackagePath, manifest.CreatedUtc, physicalPathPolicyFactory, cancellationToken);
                logger.LogInformation(
                    "Exported project package {PackageId} from profile {ProfileId}. Records={RecordCount}. MutableStorageFiles={StorageFileCount}. ImmutableReferences={ImmutableReferenceCount}.",
                    manifest.PackageId, sourceProfile.Profile.Id, manifest.TotalRecordCount, manifest.StorageFiles.Count, manifest.ImmutableStorageReferences.Count);
                return Result<ProjectPackageExportResult>.Success(new ProjectPackageExportResult(manifest, snapshot.PackagePath));
            } finally {
                if (workingRoot is not null) {
                    DeleteDirectoryIfExists(workingRoot);
                }
            }
        } catch (InvalidDataException exception) {
            logger.LogWarning(
                "Project package export validation failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return Result<ProjectPackageExportResult>.Failure(
                Error.Validation(exception.Message));
        } catch (Exception exception) when (exception is not OperationCanceledException) {
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
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.PackagePath)) {
            return Result<ProjectPackageImportResult>.Failure(
                Error.Validation("Choose a project package to import."));
        }

        if (!request.TargetProfileId.HasValue) {
            return Result<ProjectPackageImportResult>.Failure(
                Error.Validation(
                    "Select an inactive target database profile. Project package import cannot replace the running profile."));
        }

        try {
            var targetProfile = profileAccessor.ResolveProfile(request.TargetProfileId.Value);
            if (IsCurrentProfile(targetProfile)) {
                return ActiveTargetFailure();
            }

            var packagePath = Path.GetFullPath(request.PackagePath);
            var extractionRoot = await ExtractPackageAsync(
                packagePath,
                physicalPathPolicyFactory,
                cancellationToken);
            try {
                var manifest = await ReadExtractedManifestAsync(extractionRoot, cancellationToken);
                ValidateManifest(manifest);
                var dataSet = await ReadPayloadsAsync(
                    extractionRoot,
                    manifest,
                    cancellationToken);
                dataSet.ValidatePackageImportSafety();
                dataSet.ValidateForImport();
                if (dataSet.Counts.Total != manifest.TotalRecordCount) {
                    throw new InvalidDataException(
                        "The project package total record count does not match its payloads.");
                }

                dataSet.PrepareForTargetImport(manifest.SourceProfileId, manifest.PackageId);

                var storagePreflight = await _storageImporter.PreflightImportAsync(
                    extractionRoot,
                    manifest,
                    dataSet,
                    cancellationToken);

                await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);
                if (IsCurrentProfile(targetProfile)) {
                    return ActiveTargetFailure();
                }

                var initial = await operations.RunIndependentAsync(targetProfile, async (database, token) =>
                    (Counts: await data.CountAsync(database, token), Residues: await targetStateGuard.FindPreflightResiduesAsync(database, token)), cancellationToken);
                var initialTargetCounts = initial.Counts;
                var initialTargetResidues = initial.Residues;
                if (initialTargetCounts.Total > 0 || initialTargetResidues.Count > 0) {
                    var residueDetails = initialTargetResidues.Count == 0
                        ? string.Empty
                        : $" Related state found: {ProjectTransferTargetStateGuard.Describe(initialTargetResidues)}.";
                    return Result<ProjectPackageImportResult>.Failure(
                        Error.Validation(
                            "Project package import requires an inactive target with no project or project-related state. " +
                            $"Existing target data and storage were left unchanged.{residueDetails} Choose or create an empty target profile."));
                }

                return await storageTransfers.WithTargetAsync(targetProfile, async (storagePlan, storageToken) => {
                    var stagedWrites = _storageImporter.CreateStagingJournal();
                    var committed = false;
                    try {
                        var storageFilesImported = await _storageImporter.RewriteStorageBindingsAsync(extractionRoot, manifest, dataSet,
                            storagePreflight, storagePlan, stagedWrites, storageToken);
                        if (IsCurrentProfile(targetProfile)) {
                            throw new InvalidOperationException("The selected target profile became active while the package was being prepared. Import was stopped before project data changed.");
                        }
                        await targetStateGuard.RunLockedImportAsync(targetProfile, async (database, token) => {
                                if (IsCurrentProfile(targetProfile)) {
                                    throw new InvalidOperationException("The selected target profile became active while the package was being prepared. Import was stopped before project data changed.");
                                }
                                var currentTargetCounts = await data.CountAsync(database, token);
                                var currentTargetResidues = await targetStateGuard.FindLockedResiduesAsync(database, token);
                                if (currentTargetCounts.Total > 0 || currentTargetResidues.Count > 0) {
                                    var residueDetails = currentTargetResidues.Count == 0 ? string.Empty
                                        : $" Related state found: {ProjectTransferTargetStateGuard.Describe(currentTargetResidues)}.";
                                    throw new InvalidDataException("The inactive target acquired project or project-related data while the package was being prepared; import was stopped without replacing it." + residueDetails);
                                }
                                await storageTransfers.ValidateAndPersistTargetAsync(database, storagePlan, token);
                                await data.ClearAsync(database, token);
                                await data.SaveAsync(database, dataSet, token);
                                return true;
                            }, storageToken);
                        committed = true;
                        logger.LogInformation(
                            "Imported project package {PackageId} into inactive profile {ProfileId}. Records={RecordCount}. MutableStorageFiles={StorageFileCount}.",
                            manifest.PackageId, targetProfile.Profile.Id, dataSet.Counts.Total, storageFilesImported);
                        return Result<ProjectPackageImportResult>.Success(new ProjectPackageImportResult(manifest, dataSet.Counts.Total, storageFilesImported));
                    } finally {
                        if (!committed) {
                            await _storageImporter.CleanupStagedWritesAsync(stagedWrites);
                        }
                    }
                }, cancellationToken);
            } finally {
                DeleteDirectoryIfExists(extractionRoot);
            }
        } catch (InvalidDataException exception) {
            logger.LogWarning(
                "Project package import validation failed. FailureType={FailureType}.",
                exception.GetType().Name);
            return Result<ProjectPackageImportResult>.Failure(
                Error.Validation(exception.Message));
        } catch (ProjectPackageCompensationException exception) {
            logger.LogError(
                "Project package import compensation is incomplete. FailureCount={FailureCount}.",
                exception.Failures.Count);
            return Result<ProjectPackageImportResult>.Failure(
                Error.Failure(exception.Message));
        } catch (Exception exception) when (exception is not OperationCanceledException) {
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
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(packagePath)) {
            return Result<ProjectPackageManifest>.Failure(
                Error.Validation("Choose a project package."));
        }

        try {
            var extractionRoot = await ExtractPackageAsync(
                Path.GetFullPath(packagePath),
                physicalPathPolicyFactory,
                cancellationToken);
            try {
                var manifest = await ReadExtractedManifestAsync(
                    extractionRoot,
                    cancellationToken);
                ValidateManifest(manifest);
                return Result<ProjectPackageManifest>.Success(manifest);
            } finally {
                DeleteDirectoryIfExists(extractionRoot);
            }
        } catch (InvalidDataException exception) {
            return Result<ProjectPackageManifest>.Failure(
                Error.Validation(exception.Message));
        } catch (Exception exception) when (exception is not OperationCanceledException) {
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
        DateTimeOffset createdUtc) {
        var fileName = $"projects-{createdUtc:yyyyMMdd-HHmmss}-{packageId:N}.cda-projects.zip";
        string packagePath;
        if (string.IsNullOrWhiteSpace(requestedPath)) {
            var packageRoot = Path.Combine(
                controlPlanePathResolver.ResolveRootPath(),
                "project-packages");
            Directory.CreateDirectory(packageRoot);
            packagePath = Path.Combine(packageRoot, fileName);
        } else {
            var fullPath = Path.GetFullPath(requestedPath);
            packagePath = string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)) ||
                          Directory.Exists(fullPath)
                ? Path.Combine(fullPath, fileName)
                : fullPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        if (File.Exists(packagePath)) {
            throw new IOException(
                $"Project package '{packagePath}' already exists. Choose a new path; exports never overwrite an existing package.");
        }

        return packagePath;
    }

    private async Task WritePayloadsAsync(
        string workingRoot,
        ProjectTransferDataSet dataSet,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken) {
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
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.Retirements, dataSet.Retirements, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.CreationReservations, dataSet.CreationReservations, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.WorkflowContributions, dataSet.WorkflowContributions, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.WorkflowAdmissions, dataSet.WorkflowAdmissions, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.WorkAssignmentHistory, dataSet.WorkAssignmentHistory, cancellationToken);
        await WriteTableAsync(workingRoot, manifest, ProjectPackageTables.ProcessAssetContributions, dataSet.ProcessAssetContributions, cancellationToken);
    }

    private async Task<ProjectTransferDataSet> ReadPayloadsAsync(
        string extractionRoot,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken) {
        var dataSet = new ProjectTransferDataSet {
            Projects = await ReadTableAsync<ProjectTransferProject>(extractionRoot, manifest, ProjectPackageTables.Projects, cancellationToken),
            Phases = await ReadTableAsync<ProjectTransferPhase>(extractionRoot, manifest, ProjectPackageTables.Phases, cancellationToken),
            Options = await ReadTableAsync<ProjectTransferOption>(extractionRoot, manifest, ProjectPackageTables.Options, cancellationToken),
            HierarchyLinks = await ReadTableAsync<ProjectTransferHierarchy>(extractionRoot, manifest, ProjectPackageTables.HierarchyLinks, cancellationToken),
            Objects = await ReadTableAsync<ProjectObjectRecord>(extractionRoot, manifest, ProjectPackageTables.Objects, cancellationToken),
            ObjectLinks = await ReadTableAsync<ProjectObjectLinkRecord>(extractionRoot, manifest, ProjectPackageTables.ObjectLinks, cancellationToken),
            ProjectionLayouts = await ReadTableAsync<ProjectStructureProjectionLayoutRecord>(extractionRoot, manifest, ProjectPackageTables.ProjectionLayouts, cancellationToken),
            NodeBindings = await ReadTableAsync<ProjectNodeBindingRecord>(extractionRoot, manifest, ProjectPackageTables.NodeBindings, cancellationToken),
            NodeReferences = await ReadTableAsync<ProjectNodeReferenceRecord>(extractionRoot, manifest, ProjectPackageTables.NodeReferences, cancellationToken),
            NodeLifecycleEvents = await ReadTableAsync<ProjectNodeLifecycleEventRecord>(extractionRoot, manifest, ProjectPackageTables.NodeLifecycleEvents, cancellationToken),
            CrossModuleMutations = await ReadTableAsync<ProjectCrossModuleMutationRecord>(extractionRoot, manifest, ProjectPackageTables.CrossModuleMutations, cancellationToken),
            ViewStates = await ReadTableAsync<ProjectWorkbenchViewStateRecord>(extractionRoot, manifest, ProjectPackageTables.ViewStates, cancellationToken)
        };
        if (manifest.Format == ProjectPackageManifest.CurrentFormat) {
            dataSet.Retirements = await ReadTableAsync<ProjectTransferRetirement>(extractionRoot, manifest, ProjectPackageTables.Retirements, cancellationToken);
            dataSet.CreationReservations = await ReadTableAsync<ProjectTransferReservation>(extractionRoot, manifest, ProjectPackageTables.CreationReservations, cancellationToken);
            dataSet.WorkflowContributions = await ReadTableAsync<ProjectWorkflowContributionRecord>(extractionRoot, manifest, ProjectPackageTables.WorkflowContributions, cancellationToken);
            dataSet.WorkflowAdmissions = await ReadTableAsync<ProjectWorkflowAdmissionRecord>(extractionRoot, manifest, ProjectPackageTables.WorkflowAdmissions, cancellationToken);
        }
        if (manifest.WorkAssignmentHistoryVersion == ProjectPackageManifest.CurrentWorkAssignmentHistoryVersion) {
            dataSet.WorkAssignmentHistory = await ReadTableAsync<ProjectWorkAssignmentTransferItem>(extractionRoot, manifest, ProjectPackageTables.WorkAssignmentHistory, cancellationToken);
        }
        if (manifest.ProcessAssetHistoryVersion == ProjectPackageManifest.CurrentProcessAssetHistoryVersion) {
            dataSet.ProcessAssetContributions = await ReadTableAsync<ProjectProcessAssetContributionRecord>(extractionRoot, manifest, ProjectPackageTables.ProcessAssetContributions, cancellationToken);
        }
        return dataSet;
    }

    private async Task WriteTableAsync<T>(
        string workingRoot,
        ProjectPackageManifest manifest,
        ProjectPackageTable table,
        IReadOnlyCollection<T> rows,
        CancellationToken cancellationToken)
        where T : class {
        var filePath = ResolvePackageFilePath(
            workingRoot,
            table.FilePath,
            physicalPathPolicyFactory);
        await WriteJsonFileAsync(filePath, rows, cancellationToken);
        var integrity = await ComputeFileIntegrityAsync(
            filePath,
            MaximumArchiveEntryBytes,
            cancellationToken);
        manifest.Tables.Add(new ProjectPackageTableManifest {
            Name = table.Name,
            FilePath = table.FilePath,
            RowCount = rows.Count,
            Length = integrity.Length,
            Sha256 = integrity.Sha256
        });
    }

    private async Task<List<T>> ReadTableAsync<T>(
        string extractionRoot,
        ProjectPackageManifest manifest,
        ProjectPackageTable table,
        CancellationToken cancellationToken)
        where T : class {
        var tableManifest = manifest.Tables.SingleOrDefault(
            item => string.Equals(item.Name, table.Name, StringComparison.Ordinal));
        if (tableManifest is null) {
            throw new InvalidDataException(
                $"The project package is missing the '{table.Name}' table manifest.");
        }

        var filePath = ResolvePackageFilePath(
            extractionRoot,
            tableManifest.FilePath,
            physicalPathPolicyFactory);
        if (!File.Exists(filePath)) {
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
        if (rows.Count != tableManifest.RowCount) {
            throw new InvalidDataException(
                $"The project package table '{tableManifest.Name}' row count does not match its manifest.");
        }

        return rows;
    }

    private async Task<ProjectPackageManifest> ReadExtractedManifestAsync(
        string extractionRoot,
        CancellationToken cancellationToken) {
        var manifestPath = ResolvePackageFilePath(
            extractionRoot,
            "manifest.json",
            physicalPathPolicyFactory);
        if (!File.Exists(manifestPath)) {
            throw new InvalidDataException(
                "The project package is missing manifest.json.");
        }

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        return JsonSerializer.Deserialize<ProjectPackageManifest>(json, SerializerOptions)
            ?? throw new InvalidDataException(
                "The project package manifest is invalid.");
    }

    internal static void ValidateManifest(ProjectPackageManifest manifest) {
        if (manifest.Format is not (ProjectPackageManifest.CurrentFormat or ProjectPackageManifest.LegacyFormat)) {
            throw new InvalidDataException(
                $"Unsupported project package format '{manifest.Format}'. Only '{ProjectPackageManifest.LegacyFormat}' or '{ProjectPackageManifest.CurrentFormat}' packages with integrity metadata can be imported.");
        }
        if (manifest.Format == ProjectPackageManifest.CurrentFormat &&
            manifest.HistoryDisposition != ProjectPackageHistoryDisposition.PreserveAsHistory) {
            throw new InvalidDataException("Project package v3 requires explicit preservation as history; imported source authority cannot be activated.");
        }

        if (manifest.PackageId == Guid.Empty ||
            manifest.SourceProfileId == Guid.Empty ||
            manifest.TotalRecordCount < 0 ||
            manifest.Tables is null ||
            manifest.StorageFiles is null ||
            manifest.ImmutableStorageReferences is null ||
            manifest.Warnings is null) {
            throw new InvalidDataException(
                "The project package manifest is missing required values.");
        }

        if (manifest.WorkAssignmentHistoryVersion is { } workVersion &&
            (manifest.Format != ProjectPackageManifest.CurrentFormat || workVersion != ProjectPackageManifest.CurrentWorkAssignmentHistoryVersion)) {
            throw new InvalidDataException("The project package has an unsupported Work assignment history section.");
        }
        if (manifest.ProcessAssetHistoryVersion is { } assetVersion &&
            (manifest.Format != ProjectPackageManifest.CurrentFormat || assetVersion != ProjectPackageManifest.CurrentProcessAssetHistoryVersion)) {
            throw new InvalidDataException("The project package has an unsupported Process asset history section.");
        }
        var baseTables = manifest.Format == ProjectPackageManifest.LegacyFormat ? ProjectPackageTables.Legacy : ProjectPackageTables.All;
        List<ProjectPackageTable> requiredTables = [.. baseTables];
        if (manifest.WorkAssignmentHistoryVersion.HasValue) {
            requiredTables.Add(ProjectPackageTables.WorkAssignmentHistory);
        }
        if (manifest.ProcessAssetHistoryVersion.HasValue) {
            requiredTables.Add(ProjectPackageTables.ProcessAssetContributions);
        }
        if (manifest.Tables.Count != requiredTables.Count) {
            throw new InvalidDataException(
                "The project package table manifest count is invalid.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in manifest.Tables) {
            if (!names.Add(table.Name) ||
                !paths.Add(NormalizePackageRelativePath(
                    table.FilePath,
                    isDirectory: false)) ||
                table.RowCount < 0 ||
                table.Length < 0 ||
                table.Length > MaximumArchiveEntryBytes ||
                !IsSha256(table.Sha256)) {
                throw new InvalidDataException(
                    "The project package contains an invalid or duplicate table manifest entry.");
            }
        }

        if (requiredTables.Any(table => !names.Contains(table.Name))) {
            throw new InvalidDataException(
                "The project package does not contain the exact required project tables.");
        }
    }

    private static async Task WriteJsonFileAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static ProjectManagedStorageObjectKey CreateStorageKey(
        Guid? storageId,
        StorageProviderKind providerKind,
        StorageLocatorKind locatorKind,
        string locator) {
        if (storageId == Guid.Empty ||
            !Enum.IsDefined(providerKind) ||
            !Enum.IsDefined(locatorKind)) {
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

    private static JsonSerializerOptions CreateSerializerOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ProjectPackageTable(string Name, string FilePath);

    private static class ProjectPackageTables {
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

        public static ProjectPackageTable Retirements { get; } = new(
            "Projects_ProjectRetirements", "tables/project-retirements.json");

        public static ProjectPackageTable CreationReservations { get; } = new(
            "Projects_ProjectCreationReservations", "tables/project-creation-reservations.json");

        public static ProjectPackageTable WorkflowContributions { get; } = new(
            "Workbench_WorkflowContributionReceipts", "tables/workbench-workflow-contributions.json");

        public static ProjectPackageTable WorkflowAdmissions { get; } = new(
            "Workbench_WorkflowAdmissions", "tables/workbench-workflow-admissions.json");

        public static IReadOnlyList<ProjectPackageTable> Legacy { get; } =
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

        public static ProjectPackageTable WorkAssignmentHistory { get; } = new(
            "Workbench_WorkAssignmentHistory", "tables/work-assignment-history.json");

        public static ProjectPackageTable ProcessAssetContributions { get; } = new(
            "Workbench_ProcessAssetContributions", "tables/process-asset-contributions.json");

        public static IReadOnlyList<ProjectPackageTable> All { get; } =
            [.. Legacy, Retirements, CreationReservations, WorkflowContributions, WorkflowAdmissions];
    }
    private sealed record ProjectPackageExportSnapshot(ProjectPackageManifest Manifest, string PackagePath);
}
