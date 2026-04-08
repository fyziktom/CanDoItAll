namespace CanDoItAll.Infrastructure.Storage;

public sealed class DefaultStorageRoutingService(IStorageCatalogService catalogService) : IStorageRoutingService
{
    public async Task<StorageRecommendation> RecommendAsync(StorageSelectionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var storages = (await catalogService.ListAsync(cancellationToken))
            .Where(item => item.IsEnabled)
            .ToList();
        var rules = (await catalogService.ListRulesAsync(cancellationToken))
            .Where(item => item.IsEnabled)
            .OrderBy(item => GetScopeRank(item.ScopeKind))
            .ThenBy(item => item.Priority)
            .ToList();

        var warnings = new List<string>();
        var requiredCapabilities = ResolveRequiredCapabilities(context);

        var matchingRule = rules.FirstOrDefault(rule =>
            ScopeMatches(rule, context) &&
            UsageMatches(rule, context) &&
            ContentMatches(rule, context) &&
            IntentMatches(rule, context) &&
            MimeMatches(rule, context) &&
            SizeMatches(rule, context));

        if (matchingRule is not null)
        {
            var ruleRequiredCapabilities = requiredCapabilities | matchingRule.RequiredCapabilities;
            var ruleCandidates = RankByRule(storages, matchingRule, ruleRequiredCapabilities)
                .ToList();
            var primaryCandidate = ruleCandidates.FirstOrDefault();
            if (primaryCandidate is not null)
            {
                var ruleReason = primaryCandidate.StorageId == matchingRule.PreferredStorageId
                    ? matchingRule.Reason
                    : $"Fallback applied for rule '{matchingRule.Name}' because the preferred storage is unavailable or incompatible.";

                if (primaryCandidate.StorageId != matchingRule.PreferredStorageId)
                {
                    warnings.Add("The configured default storage is unavailable or does not satisfy the required capabilities.");
                }

                return new StorageRecommendation(
                    primaryCandidate,
                    ruleCandidates.Skip(1).ToList(),
                    ruleReason,
                    warnings);
            }
        }

        if (matchingRule is not null)
        {
            warnings.Add("The configured default storage is unavailable or does not satisfy the required capabilities.");
        }

        var rankedCandidates = RankByHeuristic(storages, context, requiredCapabilities)
            .ToList();
        var primary = rankedCandidates.FirstOrDefault();
        var reason = primary is null
            ? "No enabled storage currently satisfies the requested capability set."
            : BuildHeuristicReason(primary.ProviderKind, context);

        return new StorageRecommendation(
            primary,
            rankedCandidates.Skip(primary is null ? 0 : 1).ToList(),
            reason,
            warnings);
    }

    private static IEnumerable<StorageRecommendationCandidate> RankByHeuristic(
        IReadOnlyList<StorageCatalogRecord> storages,
        StorageSelectionContext context,
        StorageCapability requiredCapabilities)
    {
        foreach (var storage in storages
                     .Where(item => SupportsCapabilities(item, requiredCapabilities))
                     .OrderBy(item => GetProviderPreference(item.ProviderKind, context))
                     .ThenBy(item => item.DisplayOrder)
                     .ThenBy(item => item.Name))
        {
            yield return ToCandidate(storage, BuildHeuristicReason(storage.ProviderKind, context));
        }
    }

    private static StorageRecommendationCandidate ToCandidate(StorageCatalogRecord storage, string reason)
    {
        return new StorageRecommendationCandidate(
            storage.Id,
            storage.Name,
            storage.ProviderKind,
            storage.CapabilityMask,
            storage.HealthStatus,
            storage.IsReadOnly,
            reason);
    }

    private static StorageCapability ResolveRequiredCapabilities(StorageSelectionContext context)
    {
        return context.RequiredCapabilities |
               StorageCapability.Write |
               (context.PreviewRequired ? StorageCapability.InlinePreview : StorageCapability.None);
    }

    private static bool ScopeMatches(StorageRoutingRule rule, StorageSelectionContext context)
    {
        return rule.ScopeKind switch
        {
            StorageRoutingScopeKind.Workspace => true,
            StorageRoutingScopeKind.Project => context.ProjectId.HasValue && rule.ProjectId == context.ProjectId,
            StorageRoutingScopeKind.Node => context.ProjectId.HasValue &&
                                            rule.ProjectId == context.ProjectId &&
                                            !string.IsNullOrWhiteSpace(context.NodeKey) &&
                                            string.Equals(rule.NodeKey, context.NodeKey, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool UsageMatches(StorageRoutingRule rule, StorageSelectionContext context)
    {
        return rule.UsagePurpose == StorageUsagePurpose.Unknown || rule.UsagePurpose == context.UsagePurpose;
    }

    private static bool ContentMatches(StorageRoutingRule rule, StorageSelectionContext context)
    {
        return rule.ContentKind == StorageContentKind.Unknown || rule.ContentKind == context.ContentKind;
    }

    private static bool IntentMatches(StorageRoutingRule rule, StorageSelectionContext context)
    {
        if (rule.EditIntent && !context.EditIntent)
        {
            return false;
        }

        if (rule.PreviewRequired && !context.PreviewRequired)
        {
            return false;
        }

        if (rule.PublishIntent && !context.PublishIntent)
        {
            return false;
        }

        return true;
    }

    private static bool MimeMatches(StorageRoutingRule rule, StorageSelectionContext context)
    {
        if (string.IsNullOrWhiteSpace(rule.MimePattern))
        {
            return true;
        }

        if (rule.MimePattern.EndsWith("/*", StringComparison.Ordinal))
        {
            var prefix = rule.MimePattern[..^1];
            return context.ContentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(rule.MimePattern, context.ContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SizeMatches(StorageRoutingRule rule, StorageSelectionContext context)
    {
        if (!context.ContentLength.HasValue)
        {
            return !rule.MinimumContentLength.HasValue;
        }

        if (rule.MinimumContentLength.HasValue && context.ContentLength.Value < rule.MinimumContentLength.Value)
        {
            return false;
        }

        if (rule.MaximumContentLength.HasValue && context.ContentLength.Value > rule.MaximumContentLength.Value)
        {
            return false;
        }

        return true;
    }

    private static bool SupportsCapabilities(StorageCatalogRecord storage, StorageCapability requiredCapabilities)
    {
        if (storage.IsReadOnly && requiredCapabilities.HasFlag(StorageCapability.Write))
        {
            return false;
        }

        if (storage.HealthStatus == StorageHealthStatus.Unavailable)
        {
            return false;
        }

        return (storage.CapabilityMask & requiredCapabilities) == requiredCapabilities;
    }

    private static IEnumerable<StorageRecommendationCandidate> RankByRule(
        IReadOnlyList<StorageCatalogRecord> storages,
        StorageRoutingRule rule,
        StorageCapability requiredCapabilities)
    {
        var supportedStoragesById = storages
            .Where(item => SupportsCapabilities(item, requiredCapabilities))
            .ToDictionary(item => item.Id);
        var yieldedStorageIds = new HashSet<Guid>();

        if (supportedStoragesById.TryGetValue(rule.PreferredStorageId, out var preferredStorage))
        {
            yieldedStorageIds.Add(preferredStorage.Id);
            yield return ToCandidate(preferredStorage, rule.Reason);
        }

        foreach (var alternativeStorageId in StorageJson.ParseGuidList(rule.AlternativeStorageIdsJson))
        {
            if (!supportedStoragesById.TryGetValue(alternativeStorageId, out var alternativeStorage) ||
                !yieldedStorageIds.Add(alternativeStorage.Id))
            {
                continue;
            }

            yield return ToCandidate(alternativeStorage, $"Alternative from {rule.Name}.");
        }

        foreach (var fallbackStorage in storages
                     .Where(item => yieldedStorageIds.Add(item.Id) && SupportsCapabilities(item, requiredCapabilities))
                     .OrderBy(item => item.DisplayOrder)
                     .ThenBy(item => item.Name))
        {
            yield return ToCandidate(fallbackStorage, $"Alternative from {rule.Name}.");
        }
    }

    private static int GetScopeRank(StorageRoutingScopeKind scopeKind)
    {
        return scopeKind switch
        {
            StorageRoutingScopeKind.Node => 0,
            StorageRoutingScopeKind.Project => 1,
            _ => 2
        };
    }

    private static int GetProviderPreference(StorageProviderKind providerKind, StorageSelectionContext context)
    {
        var ordering = ResolveProviderOrdering(context);
        var index = Array.IndexOf(ordering, providerKind);
        return index < 0 ? int.MaxValue : index;
    }

    private static StorageProviderKind[] ResolveProviderOrdering(StorageSelectionContext context)
    {
        if (context.PublishIntent ||
            context.UsagePurpose is StorageUsagePurpose.DeploymentMirror or StorageUsagePurpose.ReleasePackage)
        {
            return [StorageProviderKind.Ftp, StorageProviderKind.Ipfs, StorageProviderKind.FileSystem];
        }

        if (context.UsagePurpose is StorageUsagePurpose.Evidence or StorageUsagePurpose.RecordingMedia or StorageUsagePurpose.SnapshotPackage ||
            context.ContentKind is StorageContentKind.Pdf or StorageContentKind.Image or StorageContentKind.Screenshot or StorageContentKind.Audio or StorageContentKind.Video)
        {
            return [StorageProviderKind.Ipfs, StorageProviderKind.FileSystem, StorageProviderKind.Ftp];
        }

        return [StorageProviderKind.FileSystem, StorageProviderKind.Ipfs, StorageProviderKind.Ftp];
    }

    private static string BuildHeuristicReason(StorageProviderKind providerKind, StorageSelectionContext context)
    {
        return providerKind switch
        {
            StorageProviderKind.FileSystem when context.PublishIntent =>
                "File system is the fallback because no healthier publish-oriented storage is available.",
            StorageProviderKind.FileSystem =>
                "Editable-first content stays on a mutable local file system by default.",
            StorageProviderKind.Ipfs =>
                "Immutable or preview-oriented content prefers IPFS for shareable access and durable addressing.",
            StorageProviderKind.Ftp =>
                "Publish and deployment flows prefer FTP when the intent is distribution or mirror sync.",
            _ => "Storage recommendation derived from provider health, capability fit, and default routing policy."
        };
    }
}
