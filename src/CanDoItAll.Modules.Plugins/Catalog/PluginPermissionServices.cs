using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginGrantStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ILogger<PluginGrantStore> logger)
{
    public async Task<IReadOnlyList<PluginCapabilityGrantItem>> ListAsync(
        PluginId pluginId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PluginCapabilityGrantRecord>()
            .AsNoTracking()
            .Where(item => item.PluginId == pluginId.Value)
            .OrderBy(item => item.Capability)
            .ThenBy(item => item.RecipeId)
            .ThenBy(item => item.ScopeKind)
            .ThenBy(item => item.ScopeKey)
            .Select(item => ToItem(item))
            .ToArrayAsync(cancellationToken);
    }

    public IReadOnlyList<PluginCapabilityGrantItem> List(PluginId pluginId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.Set<PluginCapabilityGrantRecord>()
            .AsNoTracking()
            .Where(item => item.PluginId == pluginId.Value)
            .OrderBy(item => item.Capability)
            .ThenBy(item => item.RecipeId)
            .ThenBy(item => item.ScopeKind)
            .ThenBy(item => item.ScopeKey)
            .Select(item => ToItem(item))
            .ToArray();
    }

    public async Task<Result<PluginCapabilityGrantItem>> SetAsync(
        PluginId pluginId,
        PluginGrantUpdateRequest request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var recipeId = NormalizeRecipeId(request.RecipeId);
        var scopeKey = NormalizeScopeKey(request.ScopeKey);
        var state = request.State;
        if (state == PluginGrantState.Unavailable || state == PluginGrantState.Requested)
        {
            return Result<PluginCapabilityGrantItem>.Failure(Error.Validation(
                $"Grant state '{state}' cannot be persisted from the API.",
                "plugins.grant-state-invalid"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginCapabilityGrantRecord>()
            .SingleOrDefaultAsync(item =>
                item.PluginId == pluginId.Value &&
                item.Capability == (int)request.Capability &&
                item.RecipeId == recipeId &&
                item.ScopeKind == request.ScopeKind.ToString() &&
                item.ScopeKey == scopeKey,
                cancellationToken);
        var timestamp = clock.GetUtcNow();
        if (record is null)
        {
            record = new PluginCapabilityGrantRecord
            {
                PluginId = pluginId.Value,
                Capability = (int)request.Capability,
                RecipeId = recipeId,
                ScopeKind = request.ScopeKind.ToString(),
                ScopeKey = scopeKey,
                CreatedAtUtc = timestamp
            };
            dbContext.Set<PluginCapabilityGrantRecord>().Add(record);
        }

        record.State = state.ToString();
        record.RiskKind = request.RiskKind.ToString();
        record.Reason = request.Reason?.Trim() ?? string.Empty;
        record.UpdatedBy = NormalizeActor(actor);
        record.UpdatedAtUtc = timestamp;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Set plugin grant {PluginId} capability {Capability} recipe {RecipeId} state {State}. Actor={Actor}.",
            pluginId.Value,
            request.Capability,
            recipeId,
            record.State,
            record.UpdatedBy);
        return Result<PluginCapabilityGrantItem>.Success(ToItem(record));
    }

    internal static string NormalizeRecipeId(string? recipeId)
        => string.IsNullOrWhiteSpace(recipeId)
            ? string.Empty
            : new PluginHostToolRecipeId(recipeId).Value;

    internal static string NormalizeScopeKey(string? scopeKey)
        => string.IsNullOrWhiteSpace(scopeKey) ? string.Empty : scopeKey.Trim();

    internal static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();

    private static PluginCapabilityGrantItem ToItem(PluginCapabilityGrantRecord record)
        => new(
            new PluginId(record.PluginId),
            (PluginCapabilityKind)record.Capability,
            string.IsNullOrWhiteSpace(record.RecipeId) ? null : new PluginHostToolRecipeId(record.RecipeId),
            Enum.TryParse<PluginGrantScopeKind>(record.ScopeKind, out var scopeKind) ? scopeKind : PluginGrantScopeKind.Plugin,
            record.ScopeKey,
            Enum.TryParse<PluginGrantState>(record.State, out var state) ? state : PluginGrantState.Unavailable,
            Enum.TryParse<PluginGrantRiskKind>(record.RiskKind, out var riskKind) ? riskKind : PluginGrantRiskKind.Medium,
            record.Reason,
            record.UpdatedBy,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            record.ConcurrencyToken);
}

public sealed class PluginConnectionStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<PluginConnectionItem?> FindAsync(
        PluginId pluginId,
        PluginConnectionId connectionId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<PluginConnectionRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == connectionId.Value && item.PluginId == pluginId.Value, cancellationToken);

        return record is null ? null : ToItem(record);
    }

    public async Task<PluginConnectionItem?> FindFirstByKeyAsync(
        PluginId pluginId,
        PluginConnectionKey connectionKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<PluginConnectionRecord>()
            .AsNoTracking()
            .Where(item => item.PluginId == pluginId.Value && item.ConnectionKey == connectionKey.Value)
            .ToArrayAsync(cancellationToken);
        var record = records
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();

        return record is null ? null : ToItem(record);
    }

    public async Task<IReadOnlyList<PluginConnectionItem>> ListAsync(
        PluginId pluginId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PluginConnectionRecord>()
            .AsNoTracking()
            .Where(item => item.PluginId == pluginId.Value)
            .OrderBy(item => item.ConnectionKey)
            .ThenBy(item => item.DisplayName)
            .Select(item => ToItem(item))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Result<PluginConnectionItem>> SaveAsync(
        PluginId pluginId,
        PluginConnectionSaveRequest request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result<PluginConnectionItem>.Failure(Error.Validation(
                "Connection display name is required.",
                "plugins.connection-display-name-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        PluginConnectionRecord? record = null;
        if (request.Id is { } connectionId)
        {
            record = await dbContext.Set<PluginConnectionRecord>()
                .SingleOrDefaultAsync(item => item.Id == connectionId.Value && item.PluginId == pluginId.Value, cancellationToken);
        }

        var timestamp = clock.GetUtcNow();
        if (record is null)
        {
            record = new PluginConnectionRecord
            {
                Id = request.Id?.Value ?? Guid.NewGuid(),
                PluginId = pluginId.Value,
                ConnectionKey = request.ConnectionKey.Value,
                CreatedAtUtc = timestamp
            };
            dbContext.Set<PluginConnectionRecord>().Add(record);
        }

        record.DisplayName = request.DisplayName.Trim();
        record.SettingsJson = string.IsNullOrWhiteSpace(request.SettingsJson) ? "{}" : request.SettingsJson.Trim();
        record.IsEnabled = request.IsEnabled;
        record.UpdatedBy = PluginGrantStore.NormalizeActor(actor);
        record.UpdatedAtUtc = timestamp;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<PluginConnectionItem>.Success(ToItem(record));
    }

    private static PluginConnectionItem ToItem(PluginConnectionRecord record)
        => new(
            new PluginConnectionId(record.Id),
            new PluginId(record.PluginId),
            new PluginConnectionKey(record.ConnectionKey),
            record.DisplayName,
            record.SettingsJson,
            record.IsEnabled,
            record.HealthStatus,
            record.UpdatedBy,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            record.ConcurrencyToken);
}

public sealed class PluginGrantEvaluator(
    PluginInstallationStore installationStore,
    PluginGrantStore grantStore)
{
    public PluginGrantDecision Evaluate(
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId = null)
    {
        var installation = installationStore.Find(pluginId);
        if (installation is null)
        {
            return PluginGrantDecision.Deny(
                pluginId,
                capability,
                PluginGrantDecisionKind.PluginNotInstalled,
                $"Plugin '{pluginId}' is not installed.",
                recipeId);
        }

        if (!installation.IsEnabled)
        {
            return PluginGrantDecision.Deny(
                pluginId,
                capability,
                PluginGrantDecisionKind.PluginDisabled,
                $"Plugin '{pluginId}' is disabled.",
                recipeId);
        }

        var descriptor = PluginInstallationStore.TryReadManifestSnapshot(installation);
        if (descriptor is null || !descriptor.Capabilities.HasFlag(capability))
        {
            return PluginGrantDecision.Deny(
                pluginId,
                capability,
                PluginGrantDecisionKind.CapabilityNotDeclared,
                $"Plugin '{pluginId}' does not declare capability '{capability}'.",
                recipeId);
        }

        var grants = grantStore.List(pluginId);
        var capabilityDecision = EvaluateGrant(pluginId, capability, recipeId: null, grants);
        if (!capabilityDecision.Allowed)
        {
            return capabilityDecision;
        }

        if (recipeId is null)
        {
            return PluginGrantDecision.Allow(pluginId, capability);
        }

        var recipeDecision = EvaluateGrant(pluginId, capability, recipeId, grants);
        return recipeDecision.Allowed
            ? PluginGrantDecision.Allow(pluginId, capability, recipeId)
            : recipeDecision;
    }

    public Task<PluginGrantDecision> EvaluateAsync(
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate(pluginId, capability, recipeId));
    }

    private static PluginGrantDecision EvaluateGrant(
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId,
        IReadOnlyList<PluginCapabilityGrantItem> grants)
    {
        var grant = grants.SingleOrDefault(item =>
            item.Capability == capability &&
            item.ScopeKind == PluginGrantScopeKind.Plugin &&
            string.IsNullOrWhiteSpace(item.ScopeKey) &&
            item.RecipeId == recipeId);
        if (grant is null)
        {
            return PluginGrantDecision.Deny(
                pluginId,
                capability,
                recipeId is null ? PluginGrantDecisionKind.GrantMissing : PluginGrantDecisionKind.RecipeGrantMissing,
                recipeId is null
                    ? $"Plugin '{pluginId}' has no grant for capability '{capability}'."
                    : $"Plugin '{pluginId}' has no grant for host-tool recipe '{recipeId}'.",
                recipeId);
        }

        return grant.State switch
        {
            PluginGrantState.Granted => PluginGrantDecision.Allow(pluginId, capability, recipeId),
            PluginGrantState.Revoked => PluginGrantDecision.Deny(
                pluginId,
                capability,
                recipeId is null ? PluginGrantDecisionKind.GrantRevoked : PluginGrantDecisionKind.RecipeGrantRevoked,
                recipeId is null
                    ? $"Plugin '{pluginId}' grant for capability '{capability}' was revoked."
                    : $"Plugin '{pluginId}' grant for host-tool recipe '{recipeId}' was revoked.",
                recipeId),
            _ => PluginGrantDecision.Deny(
                pluginId,
                capability,
                recipeId is null ? PluginGrantDecisionKind.GrantDenied : PluginGrantDecisionKind.RecipeGrantDenied,
                recipeId is null
                    ? $"Plugin '{pluginId}' grant for capability '{capability}' is '{grant.State}'."
                    : $"Plugin '{pluginId}' grant for host-tool recipe '{recipeId}' is '{grant.State}'.",
                recipeId)
        };
    }
}

public sealed class PluginSettingsService(
    PluginCatalogService catalogService,
    PluginGrantStore grantStore,
    PluginConnectionStore connectionStore,
    PluginHostToolRecipeCatalogService hostToolRecipeCatalog)
{
    public async Task<PluginSettingsDetail?> GetSettingsAsync(
        PluginId pluginId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await catalogService.ListCatalogAsync(cancellationToken);
        var catalogItem = catalog.SingleOrDefault(item => item.PluginId == pluginId);
        if (catalogItem is null)
        {
            return null;
        }

        return new PluginSettingsDetail(
            catalogItem,
            await ListEffectiveGrantsAsync(catalogItem, cancellationToken),
            await connectionStore.ListAsync(pluginId, cancellationToken),
            hostToolRecipeCatalog.ListForPlugin(catalogItem),
            catalogItem.Descriptor.Connections,
            catalogItem.Descriptor.OAuth2);
    }

    public async Task<IReadOnlyList<PluginCapabilityGrantItem>> ListEffectiveGrantsAsync(
        PluginId pluginId,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetSettingsAsync(pluginId, cancellationToken);
        return detail?.Grants ?? [];
    }

    public async Task<IReadOnlyList<PluginCapabilityGrantItem>> ListEffectiveGrantsAsync(
        PluginCatalogItem catalogItem,
        CancellationToken cancellationToken = default)
    {
        var persisted = await grantStore.ListAsync(catalogItem.PluginId, cancellationToken);
        var result = new List<PluginCapabilityGrantItem>();
        foreach (var capability in PluginCapabilityCatalog.ListDeclaredCapabilities(catalogItem.Capabilities))
        {
            result.Add(ResolveEffectiveGrant(catalogItem.PluginId, capability, recipeId: null, persisted));
        }

        foreach (var recipe in hostToolRecipeCatalog.ListForPlugin(catalogItem))
        {
            result.Add(ResolveEffectiveGrant(catalogItem.PluginId, PluginCapabilityKind.HostCommand, recipe.RecipeId, persisted));
        }

        result.AddRange(persisted.Where(item => !result.Any(existing =>
            existing.Capability == item.Capability &&
            existing.RecipeId == item.RecipeId &&
            existing.ScopeKind == item.ScopeKind &&
            string.Equals(existing.ScopeKey, item.ScopeKey, StringComparison.OrdinalIgnoreCase))));
        return result
            .OrderBy(item => item.Capability)
            .ThenBy(item => item.RecipeId?.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<Result<PluginCapabilityGrantItem>> UpdateGrantAsync(
        PluginId pluginId,
        PluginGrantUpdateRequest request,
        string actor,
        CancellationToken cancellationToken = default)
        => grantStore.SetAsync(pluginId, request, actor, cancellationToken);

    public Task<IReadOnlyList<PluginConnectionItem>> ListConnectionsAsync(
        PluginId pluginId,
        CancellationToken cancellationToken = default)
        => connectionStore.ListAsync(pluginId, cancellationToken);

    public Task<Result<PluginConnectionItem>> SaveConnectionAsync(
        PluginId pluginId,
        PluginConnectionSaveRequest request,
        string actor,
        CancellationToken cancellationToken = default)
        => connectionStore.SaveAsync(pluginId, request, actor, cancellationToken);

    private static PluginCapabilityGrantItem ResolveEffectiveGrant(
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId,
        IReadOnlyList<PluginCapabilityGrantItem> persisted)
    {
        var match = persisted.SingleOrDefault(item =>
            item.Capability == capability &&
            item.RecipeId == recipeId &&
            item.ScopeKind == PluginGrantScopeKind.Plugin &&
            string.IsNullOrWhiteSpace(item.ScopeKey));
        if (match is not null)
        {
            return match;
        }

        return new PluginCapabilityGrantItem(
            pluginId,
            capability,
            recipeId,
            PluginGrantScopeKind.Plugin,
            string.Empty,
            PluginGrantState.Requested,
            ResolveRiskKind(capability, recipeId),
            "Grant has not been decided.",
            string.Empty,
            null,
            null,
            null);
    }

    private static PluginGrantRiskKind ResolveRiskKind(PluginCapabilityKind capability, PluginHostToolRecipeId? recipeId)
    {
        if (capability == PluginCapabilityKind.HostCommand || recipeId is not null)
        {
            return PluginGrantRiskKind.High;
        }

        return capability is PluginCapabilityKind.WorkspaceFiles or PluginCapabilityKind.Storage or PluginCapabilityKind.SecretReference
            ? PluginGrantRiskKind.Medium
            : PluginGrantRiskKind.Low;
    }
}

public static class PluginCapabilityCatalog
{
    public static IReadOnlyList<PluginCapabilityKind> ListDeclaredCapabilities(PluginCapabilityKind capabilities)
        => Enum.GetValues<PluginCapabilityKind>()
            .Where(capability => capability != PluginCapabilityKind.None && capabilities.HasFlag(capability))
            .OrderBy(capability => capability.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public interface IPluginHostToolRecipeCatalogSource
{
    IReadOnlyList<PluginHostToolRecipeDescriptor> ListForPlugin(PluginCatalogItem catalogItem);
}

public sealed class PluginHostToolRecipeCatalogService(IEnumerable<IPluginHostToolRecipeCatalogSource> sources)
{
    public IReadOnlyList<PluginHostToolRecipeDescriptor> ListForPlugin(PluginCatalogItem catalogItem)
        => sources
            .SelectMany(source => source.ListForPlugin(catalogItem))
            .GroupBy(recipe => recipe.RecipeId, RecipeIdComparer.Instance)
            .Select(group => group.First())
            .OrderBy(recipe => recipe.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

internal sealed class RecipeIdComparer : IEqualityComparer<PluginHostToolRecipeId>
{
    public static RecipeIdComparer Instance { get; } = new();

    public bool Equals(PluginHostToolRecipeId x, PluginHostToolRecipeId y)
        => string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(PluginHostToolRecipeId obj)
        => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);
}
