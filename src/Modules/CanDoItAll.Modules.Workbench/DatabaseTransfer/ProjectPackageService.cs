using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectPackageService(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IAppDatabaseBootstrapper bootstrapper,
    IProfileAppDbContextFactory dbContextFactory,
    IControlPlanePathResolver controlPlanePathResolver,
    IClock clock,
    ILogger<ProjectPackageService> logger) : IProjectPackageService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

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

            await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(sourceProfile, cancellationToken);
            var dataSet = await ProjectTransferDataSet.LoadAsync(dbContext, cancellationToken);
            var counts = dataSet.Counts;
            if (counts.Projects == 0)
            {
                return Result<ProjectPackageExportResult>.Failure(
                    Error.Validation("The source database has no projects to export."));
            }

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
                await CopyReferencedStorageFilesAsync(sourceProfile, workingRoot, dataSet, manifest, cancellationToken);
                await WriteJsonFileAsync(Path.Combine(workingRoot, "manifest.json"), manifest, cancellationToken);

                if (File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                }

                ZipFile.CreateFromDirectory(workingRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);

                logger.LogInformation(
                    "Exported projects package {PackageId} from profile {ProfileId} to {PackagePath}. Records={RecordCount}. StorageFiles={StorageFileCount}.",
                    packageId,
                    sourceProfile.Profile.Id,
                    packagePath,
                    manifest.TotalRecordCount,
                    manifest.StorageFiles.Count);

                return Result<ProjectPackageExportResult>.Success(new ProjectPackageExportResult(manifest, packagePath));
            }
            finally
            {
                DeleteDirectoryIfExists(workingRoot);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exporting projects package failed.");
            return Result<ProjectPackageExportResult>.Failure(
                Error.Failure($"Exporting projects failed: {ex.Message}"));
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

        try
        {
            var packagePath = Path.GetFullPath(request.PackagePath);
            var extractionRoot = ExtractPackage(packagePath);

            try
            {
                var manifest = await ReadExtractedManifestAsync(extractionRoot, cancellationToken);
                ValidateManifest(manifest);
                var dataSet = await ReadPayloadsAsync(extractionRoot, manifest, cancellationToken);

                var targetProfile = request.TargetProfileId.HasValue
                    ? profileAccessor.ResolveProfile(request.TargetProfileId.Value)
                    : profileAccessor.ResolveCurrentProfile();
                await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);

                await using var targetDbContext = await dbContextFactory.CreateDbContextForProfileAsync(targetProfile, cancellationToken);
                if (!request.ReplaceExisting)
                {
                    var targetCounts = await ProjectTransferDataSet.CountAsync(targetDbContext, cancellationToken);
                    if (targetCounts.Total > 0)
                    {
                        return Result<ProjectPackageImportResult>.Failure(
                            Error.Validation("The target database already has project data. Enable replacement before importing projects."));
                    }
                }

                await ProjectTransferDataSet.ClearAsync(targetDbContext, cancellationToken);
                await ProjectTransferDataSet.SaveAsync(targetDbContext, dataSet, cancellationToken);
                var storageFilesImported = await RestoreStorageFilesAsync(extractionRoot, targetProfile, manifest, cancellationToken);

                logger.LogInformation(
                    "Imported projects package {PackageId} into profile {ProfileId}. Records={RecordCount}. StorageFiles={StorageFileCount}.",
                    manifest.PackageId,
                    targetProfile.Profile.Id,
                    dataSet.Counts.Total,
                    storageFilesImported);

                return Result<ProjectPackageImportResult>.Success(new ProjectPackageImportResult(
                    manifest,
                    dataSet.Counts.Total,
                    storageFilesImported));
            }
            finally
            {
                DeleteDirectoryIfExists(extractionRoot);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Importing projects package failed.");
            return Result<ProjectPackageImportResult>.Failure(
                Error.Failure($"Importing projects failed: {ex.Message}"));
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
            var extractionRoot = ExtractPackage(Path.GetFullPath(packagePath));
            try
            {
                var manifest = await ReadExtractedManifestAsync(extractionRoot, cancellationToken);
                ValidateManifest(manifest);
                return Result<ProjectPackageManifest>.Success(manifest);
            }
            finally
            {
                DeleteDirectoryIfExists(extractionRoot);
            }
        }
        catch (Exception ex)
        {
            return Result<ProjectPackageManifest>.Failure(
                Error.Failure($"Reading the project package manifest failed: {ex.Message}"));
        }
    }

    private string ResolveExportPackagePath(
        string? requestedPath,
        Guid packageId,
        DateTimeOffset createdUtc)
    {
        var fileName = $"projects-{createdUtc:yyyyMMdd-HHmmss}-{packageId:N}.cda-projects.zip";
        string packagePath;
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            var packageRoot = Path.Combine(controlPlanePathResolver.ResolveRootPath(), "project-packages");
            Directory.CreateDirectory(packageRoot);
            packagePath = Path.Combine(packageRoot, fileName);
        }
        else
        {
            var fullPath = Path.GetFullPath(requestedPath);
            packagePath = string.IsNullOrWhiteSpace(Path.GetExtension(fullPath)) || Directory.Exists(fullPath)
                ? Path.Combine(fullPath, fileName)
                : fullPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
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
        manifest.Tables.Add(new ProjectPackageTableManifest
        {
            Name = table.Name,
            FilePath = table.FilePath,
            RowCount = rows.Count
        });
    }

    private static async Task<List<T>> ReadTableAsync<T>(
        string extractionRoot,
        ProjectPackageManifest manifest,
        ProjectPackageTable table,
        CancellationToken cancellationToken)
        where T : class
    {
        var tableManifest = manifest.Tables.FirstOrDefault(item => string.Equals(item.Name, table.Name, StringComparison.Ordinal));
        if (tableManifest is null)
        {
            throw new InvalidOperationException($"The project package is missing the '{table.Name}' table manifest.");
        }

        var filePath = ResolvePackageFilePath(extractionRoot, tableManifest.FilePath);
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"The project package is missing the '{tableManifest.FilePath}' table payload.");
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var rows = JsonSerializer.Deserialize<List<T>>(json, SerializerOptions)
            ?? throw new InvalidOperationException($"The project package table '{tableManifest.Name}' is invalid.");
        if (rows.Count != tableManifest.RowCount)
        {
            throw new InvalidOperationException(
                $"The project package table '{tableManifest.Name}' row count does not match its manifest.");
        }

        return rows;
    }

    private static async Task CopyReferencedStorageFilesAsync(
        ResolvedDatabaseProfile sourceProfile,
        string workingRoot,
        ProjectTransferDataSet dataSet,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        var workspaceRoot = sourceProfile.Profile.Storage.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            manifest.Warnings.Add("The source profile does not have a workspace root; project media files were not packaged.");
            return;
        }

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var references = dataSet.NodeBindings
            .Select(item => new
            {
                RelativePath = NormalizeWorkspaceRelativePath(item.MediaRelativePath),
                item.MediaContentType,
                item.MediaOriginalFileName
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.RelativePath))
            .GroupBy(item => item.RelativePath!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var reference in references)
        {
            var sourcePath = ResolveWorkspaceFilePath(normalizedWorkspaceRoot, reference.RelativePath!);
            if (!File.Exists(sourcePath))
            {
                manifest.Warnings.Add($"Referenced media file '{reference.RelativePath}' was not found and was not packaged.");
                continue;
            }

            var packageRelativePath = $"storage/{reference.RelativePath}";
            var packagePath = ResolvePackageFilePath(workingRoot, packageRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
            await using (var sourceStream = File.OpenRead(sourcePath))
            await using (var targetStream = File.Create(packagePath))
            {
                await sourceStream.CopyToAsync(targetStream, cancellationToken);
            }

            manifest.StorageFiles.Add(new ProjectPackageStorageFileManifest
            {
                RelativePath = reference.RelativePath!,
                PackagePath = packageRelativePath,
                ContentType = reference.MediaContentType,
                OriginalFileName = reference.MediaOriginalFileName,
                Length = new FileInfo(sourcePath).Length
            });
        }
    }

    private static async Task<int> RestoreStorageFilesAsync(
        string extractionRoot,
        ResolvedDatabaseProfile targetProfile,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        if (manifest.StorageFiles.Count == 0)
        {
            return 0;
        }

        var workspaceRoot = targetProfile.Profile.Storage.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new InvalidOperationException("The target profile does not have a workspace root for project media files.");
        }

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var restored = 0;
        foreach (var file in manifest.StorageFiles)
        {
            var sourcePath = ResolvePackageFilePath(extractionRoot, file.PackagePath);
            if (!File.Exists(sourcePath))
            {
                throw new InvalidOperationException($"The packaged storage file '{file.PackagePath}' is missing.");
            }

            var targetPath = ResolveWorkspaceFilePath(normalizedWorkspaceRoot, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using (var sourceStream = File.OpenRead(sourcePath))
            await using (var targetStream = File.Create(targetPath))
            {
                await sourceStream.CopyToAsync(targetStream, cancellationToken);
            }

            restored++;
        }

        return restored;
    }

    private static string ExtractPackage(string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            throw new InvalidOperationException($"Project package '{packagePath}' was not found.");
        }

        var extractionRoot = Path.Combine(
            Path.GetDirectoryName(packagePath)!,
            $"{Path.GetFileNameWithoutExtension(packagePath)}.{Guid.NewGuid():N}.extract");
        Directory.CreateDirectory(extractionRoot);

        using var archive = ZipFile.OpenRead(packagePath);
        var normalizedRoot = extractionRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        try
        {
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.GetFullPath(Path.Combine(
                    extractionRoot,
                    entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!destinationPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(destinationPath, extractionRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The project package contains an unsafe file path.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }
        catch
        {
            DeleteDirectoryIfExists(extractionRoot);
            throw;
        }

        return extractionRoot;
    }

    private static async Task<ProjectPackageManifest> ReadExtractedManifestAsync(
        string extractionRoot,
        CancellationToken cancellationToken)
    {
        var manifestPath = ResolvePackageFilePath(extractionRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("The project package is missing manifest.json.");
        }

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        return JsonSerializer.Deserialize<ProjectPackageManifest>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The project package manifest is invalid.");
    }

    private static void ValidateManifest(ProjectPackageManifest manifest)
    {
        if (!string.Equals(manifest.Format, ProjectPackageManifest.CurrentFormat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported project package format '{manifest.Format}'.");
        }

        if (manifest.PackageId == Guid.Empty)
        {
            throw new InvalidOperationException("The project package manifest is missing its package id.");
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

    private static string ResolvePackageFilePath(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The project package contains an unsafe relative path.");
        }

        return fullPath;
    }

    private static string ResolveWorkspaceFilePath(string workspaceRoot, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A project media file path points outside the workspace root.");
        }

        return fullPath;
    }

    private static string? NormalizeWorkspaceRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
        {
            return null;
        }

        var normalized = value.Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Equals("..", StringComparison.Ordinal) ||
            normalized.Contains("../", StringComparison.Ordinal) ||
            normalized.Contains("/..", StringComparison.Ordinal))
        {
            return null;
        }

        return normalized;
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

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record ProjectPackageTable(string Name, string FilePath);

    private static class ProjectPackageTables
    {
        public static ProjectPackageTable Projects { get; } = new("Projects_Projects", "tables/projects.json");

        public static ProjectPackageTable Phases { get; } = new("Projects_ProjectPhases", "tables/project-phases.json");

        public static ProjectPackageTable Options { get; } = new("Projects_ProjectOptionSelections", "tables/project-option-selections.json");

        public static ProjectPackageTable HierarchyLinks { get; } = new("Projects_ProjectHierarchyLinks", "tables/project-hierarchy-links.json");

        public static ProjectPackageTable Objects { get; } = new("Workbench_ProjectObjects", "tables/workbench-project-objects.json");

        public static ProjectPackageTable ObjectLinks { get; } = new("Workbench_ProjectObjectLinks", "tables/workbench-project-object-links.json");

        public static ProjectPackageTable ProjectionLayouts { get; } = new("Workbench_ProjectProjectionLayouts", "tables/workbench-project-projection-layouts.json");

        public static ProjectPackageTable NodeBindings { get; } = new("Workbench_ProjectNodeBindings", "tables/workbench-project-node-bindings.json");

        public static ProjectPackageTable NodeReferences { get; } = new("Workbench_ProjectNodeReferences", "tables/workbench-project-node-references.json");

        public static ProjectPackageTable NodeLifecycleEvents { get; } = new("Workbench_ProjectNodeLifecycleEvents", "tables/workbench-project-node-lifecycle-events.json");

        public static ProjectPackageTable CrossModuleMutations { get; } = new("Workbench_ProjectCrossModuleMutations", "tables/workbench-project-cross-module-mutations.json");

        public static ProjectPackageTable ViewStates { get; } = new("Workbench_ViewStates", "tables/workbench-view-states.json");
    }
}
