using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Plugins;

public sealed class InstalledPluginPackageCatalogSource(PluginPackageManifestStore manifestStore) : IPluginCatalogSource
{
    public async ValueTask<IReadOnlyList<PluginDescriptor>> ListPluginsAsync(CancellationToken cancellationToken = default)
        => await manifestStore.ListInstalledPluginDescriptorsAsync(cancellationToken);
}

public sealed class PluginPackageService(
    PluginPackageOptions options,
    PluginPackageManifestStore manifestStore,
    PluginInstallationStore installationStore,
    PluginRuntimeRestartService restartService,
    PluginLogStore logStore,
    ILogger<PluginPackageService> logger)
{
    public async Task<IReadOnlyList<PluginPackageCatalogItem>> ListPackagesAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.CatalogRootPath);
        Directory.CreateDirectory(options.InstalledRootPath);

        var installations = await installationStore.ListAsync(cancellationToken);
        var installedPackageIds = installations
            .Where(item => !string.IsNullOrWhiteSpace(item.PackageId))
            .Select(item => item.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var itemsByPackageId = new Dictionary<string, PluginPackageCatalogItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var archivePath in PluginPackageManifestStore.EnumerateCatalogArchives(options.CatalogRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await manifestStore.ReadArchiveManifestAsync(archivePath, cancellationToken);
            var item = CreateCatalogItem(
                manifest,
                PluginPackageCatalogSourceKind.Catalogue,
                installedPackageIds.Contains(manifest.Plugin.Package!.PackageId.Value),
                Path.GetFileName(archivePath));
            itemsByPackageId[item.PackageId.Value] = item;
        }

        foreach (var manifest in await manifestStore.ListInstalledManifestsAsync(cancellationToken))
        {
            var item = CreateCatalogItem(
                manifest,
                PluginPackageCatalogSourceKind.Installed,
                installedPackageIds.Contains(manifest.Plugin.Package!.PackageId.Value),
                "installed");

            if (itemsByPackageId.TryGetValue(item.PackageId.Value, out var catalogueItem))
            {
                itemsByPackageId[item.PackageId.Value] = catalogueItem with { IsInstalled = item.IsInstalled };
                continue;
            }

            itemsByPackageId[item.PackageId.Value] = item;
        }

        return itemsByPackageId.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<Result<PluginPackageInstallResult>> InstallFromCatalogAsync(
        PluginPackageId packageId,
        PluginPackageInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(options.CatalogRootPath);

        foreach (var archivePath in PluginPackageManifestStore.EnumerateCatalogArchives(options.CatalogRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            PluginPackageManifest manifest;
            try
            {
                manifest = await manifestStore.ReadArchiveManifestAsync(archivePath, cancellationToken);
            }
            catch (Exception exception) when (IsPackageException(exception))
            {
                logger.LogWarning(
                    exception,
                    "Skipped invalid plugin package archive {ArchivePath} while looking for package {PackageId}.",
                    archivePath,
                    packageId.Value);
                continue;
            }

            if (manifest.Plugin.Package?.PackageId != packageId)
            {
                continue;
            }

            return await InstallArchiveAsync(
                archivePath,
                PluginPackageInstallSourceKind.Catalogue,
                request,
                cancellationToken);
        }

        return Result<PluginPackageInstallResult>.Failure(Error.Failure(
            $"Plugin package '{packageId}' was not found in the configured catalogue.",
            "plugins.package-not-found"));
    }

    public async Task<Result<PluginPackageInstallResult>> InstallUploadedPackageAsync(
        Stream packageStream,
        string fileName,
        PluginPackageInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageStream);
        ArgumentNullException.ThrowIfNull(request);

        Directory.CreateDirectory(options.RuntimeStateRootPath);
        var uploadRoot = Path.Combine(options.RuntimeStateRootPath, "uploads");
        Directory.CreateDirectory(uploadRoot);
        var temporaryArchivePath = Path.Combine(uploadRoot, $"{Guid.NewGuid():N}.zip");

        try
        {
            await CopyWithLimitAsync(packageStream, temporaryArchivePath, options.MaxPackageBytes, cancellationToken);
            return await InstallArchiveAsync(
                temporaryArchivePath,
                PluginPackageInstallSourceKind.Upload,
                request,
                cancellationToken);
        }
        catch (Exception exception) when (IsPackageException(exception))
        {
            await logStore.WriteAsync(new PluginLogWriteRequest(
                PluginLogStreamKind.Installation,
                PluginLogOperationKind.PackageUpload,
                PluginLogSeverity.Error,
                "Invalid",
                $"Plugin package upload '{fileName}' is invalid: {exception.Message}",
                PluginLogStore.SerializeDetails(new { fileName, exceptionType = exception.GetType().Name }),
                CorrelationId: Path.GetFileNameWithoutExtension(temporaryArchivePath)),
                cancellationToken);
            return Result<PluginPackageInstallResult>.Failure(Error.Validation(
                $"Plugin package upload '{fileName}' is invalid: {exception.Message}",
                "plugins.package-upload-invalid"));
        }
        finally
        {
            TryDeleteFile(temporaryArchivePath);
        }
    }

    private async Task<Result<PluginPackageInstallResult>> InstallArchiveAsync(
        string archivePath,
        PluginPackageInstallSourceKind sourceKind,
        PluginPackageInstallRequest request,
        CancellationToken cancellationToken)
    {
        PluginPackageManifest manifest;
        try
        {
            manifest = await manifestStore.ReadArchiveManifestAsync(archivePath, cancellationToken);
            await logStore.WriteAsync(new PluginLogWriteRequest(
                PluginLogStreamKind.Installation,
                PluginLogOperationKind.PackageValidation,
                PluginLogSeverity.Information,
                "Valid",
                $"Plugin package '{Path.GetFileName(archivePath)}' manifest was validated.",
                PluginLogStore.SerializeDetails(new { archive = Path.GetFileName(archivePath), sourceKind }),
                manifest.Plugin.Id,
                manifest.Plugin.Package?.PackageId),
                cancellationToken);
            await manifestStore.ExtractInstalledPackageAsync(archivePath, manifest, options.MaxPackageBytes, cancellationToken);
        }
        catch (Exception exception) when (IsPackageException(exception))
        {
            await logStore.WriteAsync(new PluginLogWriteRequest(
                PluginLogStreamKind.Installation,
                PluginLogOperationKind.PackageValidation,
                PluginLogSeverity.Error,
                "Invalid",
                $"Plugin package '{Path.GetFileName(archivePath)}' is invalid: {exception.Message}",
                PluginLogStore.SerializeDetails(new { archive = Path.GetFileName(archivePath), sourceKind, exceptionType = exception.GetType().Name })),
                cancellationToken);
            return Result<PluginPackageInstallResult>.Failure(Error.Validation(
                $"Plugin package '{Path.GetFileName(archivePath)}' is invalid: {exception.Message}",
                "plugins.package-invalid"));
        }

        var installResult = await installationStore.InstallAsync(
            manifest.Plugin,
            request.Enable,
            request.Actor,
            cancellationToken);
        if (installResult.IsFailure)
        {
            await logStore.WriteAsync(new PluginLogWriteRequest(
                PluginLogStreamKind.Installation,
                PluginLogOperationKind.PackageInstall,
                PluginLogSeverity.Error,
                "Failed",
                string.Join(" ", installResult.Errors.Select(error => error.Message)),
                PluginLogStore.SerializeDetails(new { sourceKind, actor = request.Actor }),
                manifest.Plugin.Id,
                manifest.Plugin.Package?.PackageId),
                cancellationToken);
            return Result<PluginPackageInstallResult>.Failure(installResult.Errors);
        }

        var restartRequired = manifest.RequiresRestart || PluginPackageManifestStore.HasRuntimeAssemblies(manifest);
        var restartStatus = restartRequired
            ? await restartService.MarkRestartRequiredAsync(
                $"Plugin package '{manifest.Plugin.DisplayName}' was installed and requires application restart before runtime assemblies can be used.",
                request.Actor,
                cancellationToken)
            : await restartService.GetStatusAsync(cancellationToken);

        logger.LogInformation(
            "Installed plugin package {PackageId} for plugin {PluginId}. Source={SourceKind}. RestartRequired={RestartRequired}. Actor={Actor}.",
            manifest.Plugin.Package!.PackageId.Value,
            manifest.Plugin.Id.Value,
            sourceKind,
            restartRequired,
            NormalizeActor(request.Actor));

        await logStore.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Installation,
            PluginLogOperationKind.PackageInstall,
            PluginLogSeverity.Information,
            "Installed",
            $"Plugin package '{manifest.Plugin.DisplayName}' was installed.",
            PluginLogStore.SerializeDetails(new { sourceKind, restartRequired, actor = request.Actor }),
            manifest.Plugin.Id,
            manifest.Plugin.Package.PackageId),
            cancellationToken);
        if (restartRequired)
        {
            await logStore.WriteAsync(new PluginLogWriteRequest(
                PluginLogStreamKind.Installation,
                PluginLogOperationKind.RestartRequired,
                PluginLogSeverity.Warning,
                restartStatus.IsRestartRequested ? "RestartRequested" : "Required",
                restartStatus.Reason,
                PluginLogStore.SerializeDetails(new { restartStatus.ProcessId, restartStatus.RequiredAtUtc, actor = request.Actor }),
                manifest.Plugin.Id,
                manifest.Plugin.Package.PackageId),
                cancellationToken);
        }

        return Result<PluginPackageInstallResult>.Success(new PluginPackageInstallResult(
            manifest.Plugin.Package.PackageId,
            manifest.Plugin.Id,
            manifest.Plugin.DisplayName,
            manifest.Plugin.Version,
            sourceKind,
            restartRequired,
            restartStatus));
    }

    private static PluginPackageCatalogItem CreateCatalogItem(
        PluginPackageManifest manifest,
        PluginPackageCatalogSourceKind sourceKind,
        bool isInstalled,
        string sourceName)
    {
        var descriptor = manifest.Plugin;
        return new PluginPackageCatalogItem(
            descriptor.Package!.PackageId,
            descriptor.Id,
            descriptor.DisplayName,
            descriptor.Description,
            descriptor.Version,
            descriptor.Vendor,
            sourceKind,
            descriptor.SourceKind,
            descriptor.TrustLevel,
            descriptor.Capabilities,
            isInstalled,
            manifest.RequiresRestart || PluginPackageManifestStore.HasRuntimeAssemblies(manifest),
            PluginPackageManifestStore.HasRuntimeAssemblies(manifest),
            manifest.IconPath,
            sourceName);
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        string destinationPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var destination = File.Create(destinationPath);
        var buffer = new byte[81920];
        long totalBytes = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > maxBytes)
            {
                throw new InvalidDataException($"Package exceeds the configured {maxBytes} byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static bool IsPackageException(Exception exception)
        => exception is IOException or InvalidDataException or JsonException or ArgumentException or UnauthorizedAccessException;

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
}

public sealed class PluginPackageManifestStore(
    PluginPackageOptions options,
    ILogger<PluginPackageManifestStore> logger)
{
    public const string ManifestFileName = "plugin.package.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<PluginPackageManifest>> ListInstalledManifestsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.InstalledRootPath);
        var manifests = new List<PluginPackageManifest>();

        foreach (var packageRoot in ListInstalledPackageRoots(options.InstalledRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            manifests.Add(await ReadInstalledManifestAsync(packageRoot.ManifestPath, cancellationToken));
        }

        return manifests
            .OrderBy(item => item.Plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<PluginDescriptor>> ListInstalledPluginDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        var manifests = await ListInstalledManifestsAsync(cancellationToken);
        return manifests
            .Select(item => item.Plugin)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<PluginPackageManifest> ReadArchiveManifestAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var entry = archive.GetEntry(ManifestFileName)
            ?? throw new InvalidDataException($"Package archive must contain '{ManifestFileName}' at the root.");

        await using var manifestStream = entry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<PluginPackageManifest>(
            manifestStream,
            SerializerOptions,
            cancellationToken);

        ValidateManifest(manifest, archive);
        return NormalizePackageManifestIcon(manifest!);
    }

    public async Task ExtractInstalledPackageAsync(
        string archivePath,
        PluginPackageManifest manifest,
        long maxPackageBytes,
        CancellationToken cancellationToken = default)
    {
        var packageId = manifest.Plugin.Package!.PackageId.Value;
        var installedPackagePath = Path.GetFullPath(Path.Combine(options.InstalledRootPath, packageId));
        var temporaryPackagePath = Path.GetFullPath(Path.Combine(options.InstalledRootPath, $".{packageId}.{Guid.NewGuid():N}.tmp"));
        EnsureChildPath(options.InstalledRootPath, installedPackagePath);
        EnsureChildPath(options.InstalledRootPath, temporaryPackagePath);

        if (Directory.Exists(temporaryPackagePath))
        {
            Directory.Delete(temporaryPackagePath, recursive: true);
        }

        Directory.CreateDirectory(temporaryPackagePath);

        try
        {
            await using var stream = File.OpenRead(archivePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            long totalUncompressedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                totalUncompressedBytes += entry.Length;
                if (totalUncompressedBytes > maxPackageBytes)
                {
                    throw new InvalidDataException($"Package uncompressed content exceeds the configured {maxPackageBytes} byte limit.");
                }

                var relativePath = NormalizeArchivePath(entry.FullName);
                var destinationPath = Path.GetFullPath(Path.Combine(
                    temporaryPackagePath,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                EnsureChildPath(temporaryPackagePath, destinationPath);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using var entryStream = entry.Open();
                await using var destination = File.Create(destinationPath);
                await entryStream.CopyToAsync(destination, cancellationToken);
            }

            var normalizedManifestPath = Path.Combine(temporaryPackagePath, ManifestFileName);
            await File.WriteAllTextAsync(
                normalizedManifestPath,
                JsonSerializer.Serialize(manifest, SerializerOptions),
                cancellationToken);

            if (Directory.Exists(installedPackagePath))
            {
                Directory.Delete(installedPackagePath, recursive: true);
            }

            Directory.Move(temporaryPackagePath, installedPackagePath);
            logger.LogInformation(
                "Extracted plugin package {PackageId} into {InstalledPackagePath}.",
                packageId,
                installedPackagePath);
        }
        catch
        {
            if (Directory.Exists(temporaryPackagePath))
            {
                Directory.Delete(temporaryPackagePath, recursive: true);
            }

            throw;
        }
    }

    internal static bool HasRuntimeAssemblies(PluginPackageManifest manifest)
        => !string.IsNullOrWhiteSpace(manifest.EntryAssembly) ||
           manifest.Assemblies.Any(path => !string.IsNullOrWhiteSpace(path));

    internal static IReadOnlyList<InstalledPluginPackageRoot> ListInstalledPackageRoots(string installedRootPath)
    {
        if (!Directory.Exists(installedRootPath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(installedRootPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !IsTemporaryPackageRoot(path))
            .Select(path => new InstalledPluginPackageRoot(Path.GetFullPath(path), Path.Combine(Path.GetFullPath(path), ManifestFileName)))
            .Where(root => File.Exists(root.ManifestPath))
            .OrderBy(root => Path.GetFileName(root.PackageRootPath), StringComparer.Ordinal)
            .ThenBy(root => root.PackageRootPath, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IEnumerable<string> EnumerateCatalogArchives(string catalogRootPath)
        => Directory.EnumerateFiles(catalogRootPath, "*.zip", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal);

    private static bool IsTemporaryPackageRoot(string path)
    {
        var directoryName = Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return directoryName.StartsWith(".", StringComparison.Ordinal) ||
            directoryName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> ResolveAssemblyPaths(
        PluginPackageManifest manifest,
        string packageRootPath)
    {
        var assemblies = manifest.Assemblies
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeArchivePath)
            .ToList();
        if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            var entryAssembly = NormalizeArchivePath(manifest.EntryAssembly);
            if (!assemblies.Contains(entryAssembly, StringComparer.OrdinalIgnoreCase))
            {
                assemblies.Insert(0, entryAssembly);
            }
        }

        return assemblies
            .Select(path =>
            {
                var assemblyPath = Path.GetFullPath(Path.Combine(
                    packageRootPath,
                    path.Replace('/', Path.DirectorySeparatorChar)));
                EnsureChildPath(packageRootPath, assemblyPath);
                return assemblyPath;
            })
            .ToArray();
    }

    internal static string NormalizePackageAssetPath(string path)
        => NormalizeArchivePath(path);

    internal static void EnsurePackageChildPath(
        string rootPath,
        string candidatePath)
        => EnsureChildPath(rootPath, candidatePath);

    private static async Task<PluginPackageManifest> ReadInstalledManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<PluginPackageManifest>(
            stream,
            SerializerOptions,
            cancellationToken);
        ValidateManifest(manifest, packageRootPath: Path.GetDirectoryName(manifestPath));
        return NormalizePackageManifestIcon(manifest!);
    }

    private static void ValidateManifest(
        PluginPackageManifest? manifest,
        ZipArchive archive)
    {
        ValidateManifestCore(manifest);
        AssertEntryExists(archive, manifest!.IconPath, "icon");

        foreach (var assemblyPath in ResolveAssemblyRelativePaths(manifest))
        {
            AssertEntryExists(archive, assemblyPath, "assembly");
        }
    }

    private static void ValidateManifest(
        PluginPackageManifest? manifest,
        string? packageRootPath)
    {
        ValidateManifestCore(manifest);
        if (string.IsNullOrWhiteSpace(packageRootPath))
        {
            throw new InvalidDataException("Installed package root path could not be resolved.");
        }

        AssertFileExists(packageRootPath, manifest!.IconPath, "icon");
        foreach (var assemblyPath in ResolveAssemblyRelativePaths(manifest))
        {
            AssertFileExists(packageRootPath, assemblyPath, "assembly");
        }

        var packageRootName = Path.GetFileName(Path.GetFullPath(packageRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var packageId = manifest.Plugin.Package!.PackageId.Value;
        if (!string.Equals(packageRootName, packageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Installed package root '{packageRootName}' must match package id '{packageId}'.");
        }
    }

    private static void ValidateManifestCore(PluginPackageManifest? manifest)
    {
        if (manifest?.Plugin is null)
        {
            throw new InvalidDataException("Package manifest must define a plugin descriptor.");
        }

        if (manifest.Plugin.Package is null)
        {
            throw new InvalidDataException("Package plugin descriptor must include package metadata.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Plugin.DisplayName))
        {
            throw new InvalidDataException("Package plugin display name is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Plugin.Version))
        {
            throw new InvalidDataException("Package plugin version is required.");
        }

        if (manifest.Plugin.SourceKind == PluginSourceKind.Bundled ||
            manifest.Plugin.TrustLevel is PluginTrustLevel.Application or PluginTrustLevel.Bundled)
        {
            throw new InvalidDataException("Runtime package manifests cannot claim bundled or application trust.");
        }

        if (string.IsNullOrWhiteSpace(manifest.IconPath))
        {
            throw new InvalidDataException("Package manifest must include an icon path.");
        }

        NormalizeArchivePath(manifest.IconPath);
        foreach (var assemblyPath in ResolveAssemblyRelativePaths(manifest))
        {
            if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Package assembly path '{assemblyPath}' must point to a .dll file.");
            }
        }

        if (manifest.Plugin.WorkflowExecutors.Count > 0 && !HasRuntimeAssemblies(manifest))
        {
            throw new InvalidDataException("Packages that declare workflow executors must include a runtime assembly.");
        }

        var validation = PluginManifestValidator.Validate(manifest.Plugin);
        if (!validation.Succeeded)
        {
            throw new InvalidDataException(string.Join(" ", validation.Issues.Select(issue => issue.Message)));
        }
    }

    private static IReadOnlyList<string> ResolveAssemblyRelativePaths(PluginPackageManifest manifest)
    {
        var assemblies = manifest.Assemblies
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeArchivePath)
            .ToList();
        if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            var entryAssembly = NormalizeArchivePath(manifest.EntryAssembly);
            if (!assemblies.Contains(entryAssembly, StringComparer.OrdinalIgnoreCase))
            {
                assemblies.Insert(0, entryAssembly);
            }
        }

        return assemblies;
    }

    private static void AssertEntryExists(
        ZipArchive archive,
        string relativePath,
        string contentKind)
    {
        var normalizedPath = NormalizeArchivePath(relativePath);
        if (archive.GetEntry(normalizedPath) is null)
        {
            throw new InvalidDataException($"Package {contentKind} '{normalizedPath}' was not found in the archive.");
        }
    }

    private static void AssertFileExists(
        string packageRootPath,
        string relativePath,
        string contentKind)
    {
        var normalizedPath = NormalizeArchivePath(relativePath);
        var path = Path.GetFullPath(Path.Combine(packageRootPath, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureChildPath(packageRootPath, path);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Package {contentKind} '{normalizedPath}' was not found in the installed package.");
        }
    }

    private static string NormalizeArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidDataException("Package paths cannot be empty.");
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Package path '{path}' must be relative.");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new InvalidDataException($"Package path '{path}' cannot contain current or parent-directory segments.");
        }

        return string.Join('/', segments);
    }

    private static PluginPackageManifest NormalizePackageManifestIcon(PluginPackageManifest manifest)
    {
        var packageId = manifest.Plugin.Package!.PackageId.Value;
        var icon = manifest.Plugin.Icon ?? UiIconDescriptor.PackageAsset(
            packageId,
            NormalizeArchivePath(manifest.IconPath),
            manifest.Plugin.DisplayName);

        return new PluginPackageManifest
        {
            Plugin = manifest.Plugin with { Icon = icon },
            EntryAssembly = manifest.EntryAssembly,
            Assemblies = manifest.Assemblies,
            IconPath = manifest.IconPath,
            RequiresRestart = manifest.RequiresRestart
        };
    }

    private static void EnsureChildPath(
        string rootPath,
        string candidatePath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        if (!normalizedCandidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Package path '{candidatePath}' escapes the package root.");
        }
    }
}

internal sealed record InstalledPluginPackageRoot(
    string PackageRootPath,
    string ManifestPath);

public sealed class PluginRuntimeRestartService(
    PluginPackageOptions options,
    IClock clock,
    IHostApplicationLifetime applicationLifetime,
    ILogger<PluginRuntimeRestartService> logger)
{
    private const string StateFileName = "plugin-runtime-restart.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<PluginRuntimeRestartStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.RuntimeStateRootPath);
        var status = await ReadStatusFileAsync(cancellationToken);
        return status ?? EmptyStatus();
    }

    public async Task<PluginRuntimeRestartStatus> MarkRestartRequiredAsync(
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var status = new PluginRuntimeRestartStatus(
            IsRestartRequired: true,
            IsRestartRequested: false,
            Reason: string.IsNullOrWhiteSpace(reason) ? "Application restart is required." : reason.Trim(),
            RequiredAtUtc: clock.GetUtcNow(),
            RequestedBy: string.Empty,
            RequestedAtUtc: null,
            ProcessId: Environment.ProcessId);

        await WriteStatusFileAsync(status, cancellationToken);
        logger.LogInformation(
            "Plugin runtime restart marked as required. Actor={Actor}. Reason={Reason}.",
            NormalizeActor(actor),
            status.Reason);
        return status;
    }

    public async Task<Result<PluginRuntimeRestartStatus>> RequestRestartAsync(
        PluginRuntimeRestartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = await GetStatusAsync(cancellationToken);
        if (!current.IsRestartRequired)
        {
            return Result<PluginRuntimeRestartStatus>.Failure(Error.Validation(
                "No plugin runtime restart is currently required.",
                "plugins.restart-not-required"));
        }

        var requested = current with
        {
            IsRestartRequested = true,
            RequestedBy = NormalizeActor(request.Actor),
            RequestedAtUtc = clock.GetUtcNow(),
            ProcessId = Environment.ProcessId
        };
        await WriteStatusFileAsync(requested, cancellationToken);

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750));
            logger.LogWarning(
                "Stopping application for plugin runtime restart. Actor={Actor}. Reason={Reason}.",
                requested.RequestedBy,
                requested.Reason);
            applicationLifetime.StopApplication();
        }, CancellationToken.None);

        return Result<PluginRuntimeRestartStatus>.Success(requested);
    }

    public Task ClearSatisfiedRestartRequirementAsync(CancellationToken cancellationToken = default)
    {
        var stateFilePath = GetStateFilePath();
        if (File.Exists(stateFilePath))
        {
            File.Delete(stateFilePath);
            logger.LogInformation("Cleared plugin runtime restart requirement marker at startup.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private async Task<PluginRuntimeRestartStatus?> ReadStatusFileAsync(CancellationToken cancellationToken)
    {
        var stateFilePath = GetStateFilePath();
        if (!File.Exists(stateFilePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(stateFilePath);
        return await JsonSerializer.DeserializeAsync<PluginRuntimeRestartStatus>(
            stream,
            SerializerOptions,
            cancellationToken);
    }

    private async Task WriteStatusFileAsync(
        PluginRuntimeRestartStatus status,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.RuntimeStateRootPath);
        var stateFilePath = GetStateFilePath();
        await File.WriteAllTextAsync(
            stateFilePath,
            JsonSerializer.Serialize(status, SerializerOptions),
            cancellationToken);
    }

    private string GetStateFilePath()
        => Path.Combine(options.RuntimeStateRootPath, StateFileName);

    private static PluginRuntimeRestartStatus EmptyStatus()
        => new(
            IsRestartRequired: false,
            IsRestartRequested: false,
            Reason: string.Empty,
            RequiredAtUtc: null,
            RequestedBy: string.Empty,
            RequestedAtUtc: null,
            ProcessId: Environment.ProcessId);

    private static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
}

public sealed class PluginPackageAssetService(
    PluginPackageOptions options,
    PluginPackageManifestStore manifestStore)
{
    public async Task<PluginPackageAsset?> ResolveIconAsync(
        PluginPackageId packageId,
        CancellationToken cancellationToken = default)
    {
        var packageRootPath = Path.GetFullPath(Path.Combine(options.InstalledRootPath, packageId.Value));
        var manifestPath = Path.Combine(packageRootPath, PluginPackageManifestStore.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var manifest = (await manifestStore.ListInstalledManifestsAsync(cancellationToken))
            .SingleOrDefault(item => item.Plugin.Package?.PackageId == packageId);
        if (manifest is null)
        {
            return null;
        }

        var relativePath = PluginPackageManifestStore.NormalizePackageAssetPath(manifest.IconPath);
        var filePath = Path.GetFullPath(Path.Combine(packageRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        PluginPackageManifestStore.EnsurePackageChildPath(packageRootPath, filePath);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return new PluginPackageAsset(
            filePath,
            ResolveContentType(filePath),
            new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero));
    }

    private static string ResolveContentType(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
}

public sealed class PluginPackageActivationHostedService(PluginRuntimeRestartService restartService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => restartService.ClearSatisfiedRestartRequirementAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal static class RuntimePluginAssemblyRegistrar
{
    public static void RegisterInstalledPackages(
        IServiceCollection services,
        PluginPackageOptions options)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        if (!Directory.Exists(options.InstalledRootPath))
        {
            return;
        }

        foreach (var packageRoot in PluginPackageManifestStore.ListInstalledPackageRoots(options.InstalledRootPath))
        {
            var manifest = ReadManifestForStartup(packageRoot.ManifestPath, packageRoot.PackageRootPath);
            var assemblies = PluginPackageManifestStore
                .ResolveAssemblyPaths(manifest, packageRoot.PackageRootPath)
                .Select(assemblyPath =>
                {
                    if (!File.Exists(assemblyPath))
                    {
                        throw new InvalidOperationException($"Installed plugin package assembly '{assemblyPath}' was not found.");
                    }

                    return LoadPluginAssembly(assemblyPath);
                })
                .ToArray();
            foreach (var assembly in assemblies)
            {
                InvokeRegistrars(services, assembly);
                RegisterAssignableTypes<IPluginHostToolRecipeCatalogSource>(services, assembly, ServiceLifetime.Singleton);
            }

            PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
                services,
                assemblies.SelectMany(PluginWorkflowExecutorRuntimeRegistration.DiscoverWorkflowExecutorTypes),
                manifest.Plugin);
        }
    }

    private static PluginPackageManifest ReadManifestForStartup(
        string manifestPath,
        string packageRootPath)
    {
        using var stream = File.OpenRead(manifestPath);
        var manifest = JsonSerializer.Deserialize<PluginPackageManifest>(
                stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Installed plugin package manifest '{manifestPath}' could not be read.");
        var packageId = manifest.Plugin.Package?.PackageId.Value
            ?? throw new InvalidOperationException($"Installed plugin package manifest '{manifestPath}' does not contain package metadata.");
        var packageRootName = Path.GetFileName(Path.GetFullPath(packageRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.Equals(packageRootName, packageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Installed plugin package root '{packageRootName}' must match package id '{packageId}'.");
        }

        return manifest;
    }

    private static Assembly LoadPluginAssembly(string assemblyPath)
    {
        var context = new RuntimePluginLoadContext(assemblyPath);
        return context.LoadAssemblyWithoutFileLock(assemblyPath);
    }

    private static void InvokeRegistrars(
        IServiceCollection services,
        Assembly assembly)
    {
        foreach (var implementationType in assembly.DefinedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false } && !type.ContainsGenericParameters)
            .Select(type => type.AsType())
            .Where(typeof(IRuntimePluginServiceRegistrar).IsAssignableFrom))
        {
            if (Activator.CreateInstance(implementationType) is not IRuntimePluginServiceRegistrar registrar)
            {
                throw new InvalidOperationException($"Runtime plugin registrar '{implementationType.FullName}' must have a public parameterless constructor.");
            }

            registrar.ConfigureServices(services);
        }
    }

    private static void RegisterAssignableTypes<TService>(
        IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime)
    {
        var serviceType = typeof(TService);
        foreach (var implementationType in assembly.DefinedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false } && !type.ContainsGenericParameters)
            .Select(type => type.AsType())
            .Where(serviceType.IsAssignableFrom))
        {
            services.TryAddEnumerable(new ServiceDescriptor(serviceType, implementationType, lifetime));
        }
    }

#pragma warning disable CA1416
    private sealed class RuntimePluginLoadContext(string mainAssemblyPath) : AssemblyLoadContext(isCollectible: false)
    {
        private readonly AssemblyDependencyResolver resolver = new(mainAssemblyPath);

        public Assembly LoadAssemblyWithoutFileLock(string assemblyPath)
        {
            using var assemblyStream = File.OpenRead(assemblyPath);
            var symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (!File.Exists(symbolsPath))
            {
                return LoadFromStream(assemblyStream);
            }

            using var symbolsStream = File.OpenRead(symbolsPath);
            return LoadFromStream(assemblyStream, symbolsStream);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is null ||
                assemblyName.Name.StartsWith("CanDoItAll.", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.Name.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase) ||
                assemblyName.Name.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadAssemblyWithoutFileLock(assemblyPath);
        }
    }
#pragma warning restore CA1416
}
