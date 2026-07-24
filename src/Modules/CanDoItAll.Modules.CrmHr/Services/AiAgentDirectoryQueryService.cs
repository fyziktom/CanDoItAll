using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public static class AiAgentDirectoryQueryLimits
{
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
}

public sealed record AiAgentDirectoryQuery(
    string SearchText = "",
    AiValidationStatus? ValidationStatus = null,
    int PageIndex = 0,
    int PageSize = AiAgentDirectoryQueryLimits.DefaultPageSize);

public sealed record AiAgentDirectoryGovernanceModel(
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    AiResourceBindingStatus BindingStatus,
    string BindingReason,
    AiExecutionMode? ExecutionMode,
    bool HasProfile,
    AiValidationStatus ValidationStatus,
    string OwnerName,
    DateTimeOffset UpdatedAtUtc);

public sealed record AiAgentDirectoryItemModel(
    Guid PartyId,
    AgentDefinition Agent,
    AiAgentDirectoryGovernanceModel Governance,
    ProviderProfile? Provider,
    DateTimeOffset? ProjectionUpdatedAtUtc)
{
    public string ProviderName => Provider?.Name ?? string.Empty;

    public bool IsPrivateProvider => Provider?.IsPrivateProvider == true;

    public AiValidationStatus ValidationStatus => Governance.ValidationStatus;

    public int CapabilityCount => Agent.Capabilities.Count;
}

public sealed record AiAgentDirectoryPage(
    IReadOnlyList<AiAgentDirectoryItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public interface IAiAgentDirectoryQueryService
{
    Task RefreshProjectionAsync(CancellationToken cancellationToken = default);

    Task<AiAgentDirectoryPage> SearchAsync(
        AiAgentDirectoryQuery query,
        CancellationToken cancellationToken = default);

    Task<AiAgentDirectoryItemModel?> GetByPartyIdAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);
}

public sealed class AiAgentDirectoryQueryService : IAiAgentDirectoryQueryService, IDisposable
{
    private static readonly TimeSpan SnapshotTimeToLive = TimeSpan.FromSeconds(20);
    private static readonly AgentReferenceDataRequest ReferenceDataRequest =
        AgentReferenceDataRequest.AgentsAndProviders(false);

    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IAiTechnicalAgentBridge technicalAgentBridge;
    private readonly IAgentReferenceDataProvider referenceDataProvider;
    private readonly IAgentReferenceDataCacheInvalidator referenceDataCacheInvalidator;
    private readonly object snapshotSync = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private CompositeSnapshotEntry? snapshotEntry;
    private bool disposed;

    public AiAgentDirectoryQueryService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IAiTechnicalAgentBridge technicalAgentBridge,
        IAgentReferenceDataProvider referenceDataProvider,
        IAgentReferenceDataCacheInvalidator referenceDataCacheInvalidator)
    {
        this.dbContextFactory = dbContextFactory;
        this.technicalAgentBridge = technicalAgentBridge;
        this.referenceDataProvider = referenceDataProvider;
        this.referenceDataCacheInvalidator = referenceDataCacheInvalidator;
        referenceDataCacheInvalidator.Invalidated += HandleReferenceDataInvalidated;
    }

    public async Task RefreshProjectionAsync(CancellationToken cancellationToken = default)
    {
        await technicalAgentBridge
            .SynchronizeDirectoryProjectionAsync(cancellationToken)
            .ConfigureAwait(false);

        InvalidateSnapshot();
        referenceDataCacheInvalidator.Invalidate();
    }

    public async Task<AiAgentDirectoryPage> SearchAsync(
        AiAgentDirectoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = Normalize(query);
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var pageStart = normalized.PageIndex * normalized.PageSize;
        var items = new List<AiAgentDirectoryItemModel>(normalized.PageSize);
        var totalCount = 0;

        foreach (var item in snapshot.Items)
        {
            if (normalized.ValidationStatus is AiValidationStatus validationStatus &&
                item.Governance.ValidationStatus != validationStatus)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(normalized.SearchText) &&
                !MatchesSearch(item, normalized.SearchText))
            {
                continue;
            }

            if (totalCount >= pageStart &&
                items.Count < normalized.PageSize)
            {
                items.Add(item);
            }

            totalCount++;
        }

        return new AiAgentDirectoryPage(
            items.ToArray(),
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    public async Task<AiAgentDirectoryItemModel?> GetByPartyIdAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException(
                "AI agent directory party identifier cannot be empty.",
                nameof(partyId));
        }

        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.ItemByPartyId.GetValueOrDefault(partyId);
    }

    public void Dispose()
    {
        lock (snapshotSync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            snapshotEntry = null;
        }

        referenceDataCacheInvalidator.Invalidated -= HandleReferenceDataInvalidated;
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
    }

    private async Task<CompositeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        Lazy<Task<CompositeSnapshot>> snapshot;
        var now = DateTimeOffset.UtcNow;

        lock (snapshotSync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (snapshotEntry is not null &&
                snapshotEntry.ExpiresAtUtc > now)
            {
                snapshot = snapshotEntry.Value;
            }
            else
            {
                snapshot = new Lazy<Task<CompositeSnapshot>>(
                    () => LoadSnapshotAsync(lifetimeCancellation.Token),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                snapshotEntry = new CompositeSnapshotEntry(
                    snapshot,
                    now.Add(SnapshotTimeToLive));
            }
        }

        var snapshotTask = snapshot.Value;
        try
        {
            return await snapshotTask
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch when (snapshotTask.IsFaulted || snapshotTask.IsCanceled)
        {
            RemoveFailedSnapshot(snapshot);
            throw;
        }
    }

    private async Task<CompositeSnapshot> LoadSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var referenceData = await referenceDataProvider
            .GetAsync(ReferenceDataRequest, cancellationToken)
            .ConfigureAwait(false);
        var agentsById = referenceData.Agents.ToDictionary(agent => agent.Id);

        await using var dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var rows = await (
                from binding in dbContext.Set<AiResourceBinding>().AsNoTracking()
                join party in dbContext.Set<Party>().AsNoTracking()
                    on binding.PartyId equals party.Id
                join profile in dbContext.Set<AiAgentProfile>().AsNoTracking()
                    on party.Id equals profile.PartyId into profiles
                from profile in profiles.DefaultIfEmpty()
                join owner in dbContext.Set<Party>().AsNoTracking()
                    on profile.OwnerPartyId equals (Guid?)owner.Id into owners
                from owner in owners.DefaultIfEmpty()
                where party.PartyType == PartyType.AiAgent
                where binding.TechnicalAgentId.HasValue
                where binding.BindingStatus == AiResourceBindingStatus.Bound
                select new
                {
                    PartyId = party.Id,
                    party.LifecycleStatus,
                    party.IsSensitive,
                    PartyUpdatedAtUtc = party.UpdatedAtUtc,
                    TechnicalAgentId = binding.TechnicalAgentId!.Value,
                    binding.BindingStatus,
                    binding.BindingReason,
                    ExecutionMode = binding.ProjectedExecutionMode,
                    binding.ProjectionUpdatedAtUtc,
                    HasProfile = profile != null,
                    ValidationStatus = profile == null
                        ? AiValidationStatus.Draft
                        : profile.ValidationStatus,
                    OwnerName = owner == null || owner.IsSensitive
                        ? string.Empty
                        : owner.DisplayName
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var duplicateTechnicalAgentId = rows
            .GroupBy(row => row.TechnicalAgentId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTechnicalAgentId is not null)
        {
            var partyIds = string.Join(
                ", ",
                duplicateTechnicalAgentId
                    .Select(row => row.PartyId.ToString("D"))
                    .Order(StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"Technical agent '{duplicateTechnicalAgentId.Key:D}' is bound to multiple CRM-HR parties: {partyIds}.");
        }

        var projectedItems = new List<AiAgentDirectoryItemModel>(rows.Count);
        foreach (var row in rows)
        {
            if (!agentsById.TryGetValue(row.TechnicalAgentId, out var agent))
            {
                continue;
            }

            ProviderProfile? provider = null;
            if (agent.ProviderProfileId is Guid providerProfileId)
            {
                referenceData.ProviderById.TryGetValue(providerProfileId, out provider);
            }

            var governance = new AiAgentDirectoryGovernanceModel(
                row.LifecycleStatus,
                row.IsSensitive,
                row.BindingStatus,
                row.BindingReason,
                row.ExecutionMode,
                row.HasProfile,
                row.ValidationStatus,
                row.OwnerName,
                row.PartyUpdatedAtUtc);
            projectedItems.Add(new AiAgentDirectoryItemModel(
                row.PartyId,
                agent,
                governance,
                provider,
                row.ProjectionUpdatedAtUtc));
        }

        var items = projectedItems
            .OrderBy(item => item.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Agent.Id)
            .ToImmutableArray();

        return new CompositeSnapshot(
            items,
            items.ToImmutableDictionary(item => item.PartyId));
    }

    private static bool MatchesSearch(
        AiAgentDirectoryItemModel item,
        string searchText)
    {
        return item.Agent.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               item.Agent.RoleTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               item.Agent.Summary.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               item.Agent.Model.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               ContainsTag(item.Agent.Tags, searchText) ||
               item.Governance.OwnerName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsTag(
        IReadOnlyList<string> tags,
        string searchText)
    {
        foreach (var tag in tags)
        {
            if (tag.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleReferenceDataInvalidated(object? sender, EventArgs eventArgs)
    {
        InvalidateSnapshot();
    }

    private void InvalidateSnapshot()
    {
        lock (snapshotSync)
        {
            snapshotEntry = null;
        }
    }

    private void RemoveFailedSnapshot(Lazy<Task<CompositeSnapshot>> failedSnapshot)
    {
        lock (snapshotSync)
        {
            if (snapshotEntry is not null &&
                ReferenceEquals(snapshotEntry.Value, failedSnapshot))
            {
                snapshotEntry = null;
            }
        }
    }

    private static AiAgentDirectoryQuery Normalize(AiAgentDirectoryQuery query)
    {
        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "AI agent directory page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > AiAgentDirectoryQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"AI agent directory page size must be between 1 and {AiAgentDirectoryQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "AI agent directory page offset is too large.");
        }

        var searchText = query.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > AiAgentDirectoryQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"AI agent directory search cannot exceed {AiAgentDirectoryQueryLimits.MaximumSearchLength} characters.",
                nameof(query));
        }

        if (query.ValidationStatus.HasValue &&
            !Enum.IsDefined(query.ValidationStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.ValidationStatus,
                "AI agent validation status must be supported.");
        }

        return query with
        {
            SearchText = searchText
        };
    }

    private sealed record CompositeSnapshot(
        ImmutableArray<AiAgentDirectoryItemModel> Items,
        ImmutableDictionary<Guid, AiAgentDirectoryItemModel> ItemByPartyId);

    private sealed record CompositeSnapshotEntry(
        Lazy<Task<CompositeSnapshot>> Value,
        DateTimeOffset ExpiresAtUtc);
}
