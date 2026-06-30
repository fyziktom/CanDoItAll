using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Plugins;

public interface IPluginCatalogSource
{
    ValueTask<IReadOnlyList<PluginDescriptor>> ListPluginsAsync(CancellationToken cancellationToken = default);
}

public sealed class BundledPluginCatalogSource(IEnumerable<ICanDoItAllPlugin> plugins) : IPluginCatalogSource
{
    public ValueTask<IReadOnlyList<PluginDescriptor>> ListPluginsAsync(CancellationToken cancellationToken = default)
    {
        var descriptors = plugins
            .Select(plugin => plugin.Descriptor)
            .OrderBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return ValueTask.FromResult<IReadOnlyList<PluginDescriptor>>(descriptors);
    }
}

public sealed class PluginInstallationStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ILogger<PluginInstallationStore> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private long revision;

    public long Revision => Interlocked.Read(ref revision);

    public async Task<IReadOnlyList<PluginInstallationRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PluginInstallationRecord>()
            .AsNoTracking()
            .OrderBy(item => item.DisplayNameSnapshot)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PluginInstallationRecord?> FindAsync(
        PluginId pluginId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PluginInstallationRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.PluginId == pluginId.Value, cancellationToken);
    }

    public PluginInstallationRecord? Find(PluginId pluginId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.Set<PluginInstallationRecord>()
            .AsNoTracking()
            .SingleOrDefault(item => item.PluginId == pluginId.Value);
    }

    public async Task<Result<PluginInstallationRecord>> InstallAsync(
        PluginDescriptor descriptor,
        bool enable,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var validation = PluginManifestValidator.Validate(descriptor);
        if (!validation.Succeeded)
        {
            return Result<PluginInstallationRecord>.Failure(validation.Issues.Select(issue => Error.Validation(issue.Message, issue.Code.ToString())));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var timestamp = clock.GetUtcNow();
        var normalizedActor = NormalizeActor(actor);
        var existing = await dbContext.Set<PluginInstallationRecord>()
            .SingleOrDefaultAsync(item => item.PluginId == descriptor.Id.Value, cancellationToken);

        if (existing is null)
        {
            existing = new PluginInstallationRecord
            {
                PluginId = descriptor.Id.Value,
                InstalledAtUtc = timestamp
            };
            dbContext.Set<PluginInstallationRecord>().Add(existing);
        }

        existing.PackageId = descriptor.Package?.PackageId.Value ?? string.Empty;
        existing.DisplayNameSnapshot = descriptor.DisplayName.Trim();
        existing.Version = descriptor.Version.Trim();
        existing.Vendor = descriptor.Vendor.Trim();
        existing.ManifestSnapshotJson = JsonSerializer.Serialize(descriptor, SerializerOptions);
        existing.IsEnabled = enable;
        existing.InstalledBy = normalizedActor;
        existing.UpdatedAtUtc = timestamp;

        await dbContext.SaveChangesAsync(cancellationToken);
        Interlocked.Increment(ref revision);
        logger.LogInformation(
            "Installed plugin {PluginId} version {Version}. Enabled={IsEnabled}. Actor={Actor}.",
            existing.PluginId,
            existing.Version,
            existing.IsEnabled,
            normalizedActor);
        return Result<PluginInstallationRecord>.Success(existing);
    }

    public async Task<Result<PluginInstallationRecord>> SetEnabledAsync(
        PluginId pluginId,
        bool isEnabled,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var installation = await dbContext.Set<PluginInstallationRecord>()
            .SingleOrDefaultAsync(item => item.PluginId == pluginId.Value, cancellationToken);
        if (installation is null)
        {
            return Result<PluginInstallationRecord>.Failure(Error.Failure($"Plugin '{pluginId}' is not installed.", "plugins.not-installed"));
        }

        installation.IsEnabled = isEnabled;
        installation.InstalledBy = NormalizeActor(actor);
        installation.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        Interlocked.Increment(ref revision);

        logger.LogInformation(
            "Set plugin {PluginId} enabled state to {IsEnabled}. Actor={Actor}.",
            installation.PluginId,
            installation.IsEnabled,
            installation.InstalledBy);
        return Result<PluginInstallationRecord>.Success(installation);
    }

    public static PluginDescriptor? TryReadManifestSnapshot(PluginInstallationRecord installation)
    {
        try
        {
            return JsonSerializer.Deserialize<PluginDescriptor>(installation.ManifestSnapshotJson, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor)
            ? "system"
            : actor.Trim();
}

public sealed class PluginCatalogService(
    IEnumerable<IPluginCatalogSource> sources,
    PluginInstallationStore installationStore,
    PluginLogStore logStore)
{
    public async Task<IReadOnlyList<PluginCatalogItem>> ListCatalogAsync(CancellationToken cancellationToken = default)
    {
        var descriptors = await LoadDescriptorsAsync(cancellationToken);
        var installations = await installationStore.ListAsync(cancellationToken);
        var installationsByPluginId = installations.ToDictionary(item => item.PluginId, StringComparer.OrdinalIgnoreCase);
        var items = descriptors
            .Select(descriptor =>
            {
                installationsByPluginId.Remove(descriptor.Id.Value, out var installation);
                return CreateAvailableItem(descriptor, installation);
            })
            .Concat(installationsByPluginId.Values.Select(CreateUnavailableItem))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return items;
    }

    public async Task<Result<PluginCatalogItem>> InstallAsync(
        PluginId pluginId,
        PluginInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var descriptor = (await LoadDescriptorsAsync(cancellationToken))
            .SingleOrDefault(item => item.Id == pluginId);
        if (descriptor is null)
        {
            return Result<PluginCatalogItem>.Failure(Error.Failure($"Plugin '{pluginId}' is not available in the active plugin catalog.", "plugins.not-found"));
        }

        var installResult = await installationStore.InstallAsync(
            descriptor,
            request.Enable,
            request.Actor,
            cancellationToken);
        if (installResult.IsFailure)
        {
            await logStore.WriteAsync(new PluginLogWriteRequest(
                PluginLogStreamKind.Installation,
                PluginLogOperationKind.PluginInstall,
                PluginLogSeverity.Error,
                "Failed",
                string.Join(" ", installResult.Errors.Select(error => error.Message)),
                PluginLogStore.SerializeDetails(new { pluginId = pluginId.Value, request.Enable }),
                pluginId),
                cancellationToken);
            return Result<PluginCatalogItem>.Failure(installResult.Errors);
        }

        await logStore.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Installation,
            PluginLogOperationKind.PluginInstall,
            PluginLogSeverity.Information,
            "Installed",
            $"Plugin '{descriptor.DisplayName}' was installed from the active catalog.",
            PluginLogStore.SerializeDetails(new { pluginId = pluginId.Value, request.Enable, request.Actor }),
            pluginId,
            descriptor.Package?.PackageId),
            cancellationToken);
        return Result<PluginCatalogItem>.Success(CreateAvailableItem(descriptor, installResult.Value!));
    }

    public async Task<Result<PluginCatalogItem>> SetEnabledAsync(
        PluginId pluginId,
        bool isEnabled,
        PluginInstallationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var updateResult = await installationStore.SetEnabledAsync(pluginId, isEnabled, request.Actor, cancellationToken);
        if (updateResult.IsFailure)
        {
            await logStore.WriteAsync(new PluginLogWriteRequest(
                PluginLogStreamKind.Installation,
                isEnabled ? PluginLogOperationKind.PluginEnable : PluginLogOperationKind.PluginDisable,
                PluginLogSeverity.Error,
                "Failed",
                string.Join(" ", updateResult.Errors.Select(error => error.Message)),
                PluginLogStore.SerializeDetails(new { pluginId = pluginId.Value, isEnabled, request.Actor }),
                pluginId),
                cancellationToken);
            return Result<PluginCatalogItem>.Failure(updateResult.Errors);
        }

        var descriptor = (await LoadDescriptorsAsync(cancellationToken))
            .SingleOrDefault(item => item.Id == pluginId);
        await logStore.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Installation,
            isEnabled ? PluginLogOperationKind.PluginEnable : PluginLogOperationKind.PluginDisable,
            PluginLogSeverity.Information,
            isEnabled ? "Enabled" : "Disabled",
            $"Plugin '{pluginId}' was {(isEnabled ? "enabled" : "disabled")}.",
            PluginLogStore.SerializeDetails(new { pluginId = pluginId.Value, isEnabled, request.Actor }),
            pluginId,
            descriptor?.Package?.PackageId),
            cancellationToken);
        return Result<PluginCatalogItem>.Success(
            descriptor is null
                ? CreateUnavailableItem(updateResult.Value!)
                : CreateAvailableItem(descriptor, updateResult.Value!));
    }

    private async Task<IReadOnlyList<PluginDescriptor>> LoadDescriptorsAsync(CancellationToken cancellationToken)
    {
        var descriptors = new List<PluginDescriptor>();
        foreach (var source in sources)
        {
            descriptors.AddRange(await source.ListPluginsAsync(cancellationToken));
        }

        var validation = PluginManifestValidator.ValidateCatalog(descriptors);
        validation.ThrowIfInvalid();
        return descriptors;
    }

    private static PluginCatalogItem CreateAvailableItem(
        PluginDescriptor descriptor,
        PluginInstallationRecord? installation)
        => new PluginCatalogItem(
            descriptor.Id,
            descriptor.DisplayName,
            descriptor.Description,
            descriptor.Version,
            descriptor.Vendor,
            descriptor.SourceKind,
            descriptor.TrustLevel,
            descriptor.Capabilities,
            descriptor.Package?.PackageId,
            ResolveInstallationState(installation),
            PluginCatalogAvailabilityKind.Available,
            string.Empty,
            installation?.InstalledAtUtc,
            installation?.UpdatedAtUtc,
            descriptor.Icon ?? UiIconDescriptor.Default)
        {
            Descriptor = descriptor
        };

    private static PluginCatalogItem CreateUnavailableItem(PluginInstallationRecord installation)
    {
        var snapshot = PluginInstallationStore.TryReadManifestSnapshot(installation);
        return new PluginCatalogItem(
            snapshot?.Id ?? new PluginId(installation.PluginId),
            string.IsNullOrWhiteSpace(installation.DisplayNameSnapshot)
                ? installation.PluginId
                : installation.DisplayNameSnapshot,
            snapshot?.Description ?? string.Empty,
            string.IsNullOrWhiteSpace(installation.Version)
                ? snapshot?.Version ?? string.Empty
                : installation.Version,
            string.IsNullOrWhiteSpace(installation.Vendor)
                ? snapshot?.Vendor ?? string.Empty
                : installation.Vendor,
            snapshot?.SourceKind ?? PluginSourceKind.LocalPackage,
            snapshot?.TrustLevel ?? PluginTrustLevel.Untrusted,
            snapshot?.Capabilities ?? PluginCapabilityKind.None,
            snapshot?.Package?.PackageId,
            ResolveInstallationState(installation),
            PluginCatalogAvailabilityKind.Unavailable,
            "Plugin is installed, but no active catalog source currently provides its manifest.",
            installation.InstalledAtUtc,
            installation.UpdatedAtUtc,
            snapshot?.Icon ?? UiIconDescriptor.Default)
        {
            Descriptor = snapshot ?? new PluginDescriptor(
                new PluginId(installation.PluginId),
                string.IsNullOrWhiteSpace(installation.DisplayNameSnapshot)
                    ? installation.PluginId
                    : installation.DisplayNameSnapshot,
                string.Empty,
                string.IsNullOrWhiteSpace(installation.Version) ? "0.0.0" : installation.Version,
                string.Empty,
                PluginSourceKind.LocalPackage,
                PluginTrustLevel.Untrusted,
                "1.0.0",
                PluginCapabilityKind.None,
                [],
                PluginSettingsDescriptor.Empty,
                [],
                string.IsNullOrWhiteSpace(installation.PackageId)
                    ? null
                    : new PluginPackageDescriptor(new PluginPackageId(installation.PackageId), string.Empty, "1.0.0", string.Empty, string.Empty),
                Icon: UiIconDescriptor.Default)
        };
    }

    private static PluginInstallationStateKind ResolveInstallationState(PluginInstallationRecord? installation)
        => installation is null
            ? PluginInstallationStateKind.NotInstalled
            : installation.IsEnabled
                ? PluginInstallationStateKind.InstalledEnabled
                : PluginInstallationStateKind.InstalledDisabled;
}
