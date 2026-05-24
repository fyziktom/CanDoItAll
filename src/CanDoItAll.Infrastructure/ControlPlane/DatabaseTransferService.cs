using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatabaseTransferService(
    IDatabaseProfileService profileService,
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ISwitchableAppDbContextFactory dbContextFactory,
    IEnumerable<IDatabaseTransferHandler> handlers) : IDatabaseTransferService
{
    private readonly IReadOnlyList<IDatabaseTransferHandler> _handlers = handlers
        .OrderBy(handler => handler.Descriptor.SortOrder)
        .ThenBy(handler => handler.Descriptor.Label, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public async Task<IReadOnlyList<DatabaseTransferSourceSummary>> ListSourcesAsync(
        Guid targetProfileId,
        CancellationToken cancellationToken = default)
    {
        var runtimeProfile = profileAccessor.ResolveCurrentProfile();
        var profiles = await profileService.ListAsync(cancellationToken);
        return profiles
            .Where(profile => profile.Id != targetProfileId)
            .Where(profile => profile.ProviderKind == DatabaseProviderKind.PostgreSql &&
                profile.SourceKind == DatabaseProfileSourceKind.PostgresConnection)
            .OrderByDescending(profile => !runtimeProfile.Profile.Runtime.LockedByRuntimeOverride &&
                profile.Id == runtimeProfile.Profile.Id)
            .ThenBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new DatabaseTransferSourceSummary(
                profile.Id,
                profile.DisplayName,
                profile.ProviderKind,
                profile.SourceKind,
                profile.Descriptor,
                !runtimeProfile.Profile.Runtime.LockedByRuntimeOverride &&
                    profile.Id == runtimeProfile.Profile.Id,
                profile.IsRuntimeLocked))
            .ToList();
    }

    public async Task<IReadOnlyList<DatabaseTransferItemPreview>> PreviewAsync(
        Guid sourceProfileId,
        Guid targetProfileId,
        CancellationToken cancellationToken = default)
    {
        if (sourceProfileId == targetProfileId)
        {
            return _handlers
                .Select(handler => new DatabaseTransferItemPreview(
                    handler.Descriptor,
                    false,
                    "Choose a different source database.",
                    "The source and target database profiles are the same.",
                    0,
                    0))
                .ToList();
        }

        var sourceProfile = profileAccessor.ResolveProfile(sourceProfileId);
        var targetProfile = profileAccessor.ResolveProfile(targetProfileId);

        await using var sourceDbContext = await dbContextFactory.CreateDbContextForProfileAsync(sourceProfile, cancellationToken);
        await using var targetDbContext = await dbContextFactory.CreateDbContextForProfileAsync(targetProfile, cancellationToken);
        await EnsureCanOpenAsync(sourceDbContext, cancellationToken);
        await EnsureCanOpenAsync(targetDbContext, cancellationToken);

        var context = new DatabaseTransferContext(
            sourceProfile,
            targetProfile,
            sourceDbContext,
            targetDbContext,
            ReplaceExisting: true);

        var previews = new List<DatabaseTransferItemPreview>();
        foreach (var handler in _handlers)
        {
            previews.Add(await PreviewHandlerAsync(handler, context, cancellationToken));
        }

        return previews;
    }

    public async Task<DatabaseTransferResult> TransferAsync(
        DatabaseTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceProfileId == request.TargetProfileId)
        {
            return new DatabaseTransferResult(
                request.SourceProfileId,
                request.TargetProfileId,
                [new DatabaseTransferItemResult("database-profile", "Database profile", false, "Choose a different source database.", 0)]);
        }

        var selectedKeys = request.ItemKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedKeys.Count == 0)
        {
            return new DatabaseTransferResult(
                request.SourceProfileId,
                request.TargetProfileId,
                [new DatabaseTransferItemResult("selection", "Selection", false, "Select at least one settings group to transfer.", 0)]);
        }

        var selectedHandlers = _handlers
            .Where(handler => selectedKeys.Contains(handler.Descriptor.Key))
            .ToList();

        var missingKeys = selectedKeys
            .Except(selectedHandlers.Select(handler => handler.Descriptor.Key), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = missingKeys
            .Select(key => new DatabaseTransferItemResult(key, key, false, $"No transfer handler is registered for '{key}'.", 0))
            .ToList();

        var sourceProfile = profileAccessor.ResolveProfile(request.SourceProfileId);
        var targetProfile = profileAccessor.ResolveProfile(request.TargetProfileId);

        await using var sourceDbContext = await dbContextFactory.CreateDbContextForProfileAsync(sourceProfile, cancellationToken);
        await using var targetDbContext = await dbContextFactory.CreateDbContextForProfileAsync(targetProfile, cancellationToken);
        await EnsureCanOpenAsync(sourceDbContext, cancellationToken);
        await EnsureCanOpenAsync(targetDbContext, cancellationToken);

        var context = new DatabaseTransferContext(
            sourceProfile,
            targetProfile,
            sourceDbContext,
            targetDbContext,
            request.ReplaceExisting);

        foreach (var handler in selectedHandlers)
        {
            results.Add(await TransferHandlerAsync(handler, context, cancellationToken));
        }

        return new DatabaseTransferResult(request.SourceProfileId, request.TargetProfileId, results);
    }

    private static async Task<DatabaseTransferItemPreview> PreviewHandlerAsync(
        IDatabaseTransferHandler handler,
        DatabaseTransferContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await handler.PreviewAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DatabaseTransferItemPreview(
                handler.Descriptor,
                false,
                "Preview failed.",
                ex.Message,
                0,
                0);
        }
    }

    private static async Task<DatabaseTransferItemResult> TransferHandlerAsync(
        IDatabaseTransferHandler handler,
        DatabaseTransferContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await handler.TransferAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DatabaseTransferItemResult(
                handler.Descriptor.Key,
                handler.Descriptor.Label,
                false,
                ex.Message,
                0);
        }
    }

    private static async Task EnsureCanOpenAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("The selected database profile cannot be opened.");
        }
    }
}
