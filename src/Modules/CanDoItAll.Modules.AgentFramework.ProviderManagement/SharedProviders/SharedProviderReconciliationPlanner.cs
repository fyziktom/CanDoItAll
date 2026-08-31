using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal enum SharedProviderReconciliationDecisionKind
{
    Create,
    Refresh,
    MarkMissing,
    Reactivate,
    Retire
}

internal sealed record SharedProviderReconciliationDecision
{
    internal SharedProviderReconciliationDecision(
        SharedProviderReconciliationDecisionKind kind,
        SharedProviderPublicationId publicationId,
        Guid? importId,
        Guid? providerProfileId,
        SharedProviderCatalogPublication? remotePublication)
    {
        Kind = kind;
        PublicationId = publicationId;
        ImportId = importId;
        ProviderProfileId = providerProfileId;
        RemotePublication = remotePublication;
    }

    public SharedProviderReconciliationDecisionKind Kind { get; }

    public SharedProviderPublicationId PublicationId { get; }

    public Guid? ImportId { get; }

    public Guid? ProviderProfileId { get; }

    public SharedProviderCatalogPublication? RemotePublication { get; }
}

internal sealed record SharedProviderReconciliationPlan
{
    internal SharedProviderReconciliationPlan(
        IReadOnlyList<SharedProviderReconciliationDecision> decisions)
    {
        Decisions = Array.AsReadOnly(decisions.ToArray());
    }

    public IReadOnlyList<SharedProviderReconciliationDecision> Decisions { get; }

    public bool IsNoOp => Decisions.Count == 0;
}

internal static class SharedProviderReconciliationPlanner
{
    public static SharedProviderReconciliationPlan Create(
        IReadOnlyCollection<SharedProviderImport> existingImports,
        SharedProviderCatalogDocument catalog,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        SharedProviderSelectionMode selectionMode)
    {
        ArgumentNullException.ThrowIfNull(existingImports);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selectedPublicationIds);
        if (!Enum.IsDefined(selectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(selectionMode));
        }

        SharedProviderProtocolJson.ValidateCatalog(catalog);
        var importsByPublicationId = BuildImportIndex(existingImports);
        var publicationsById = catalog.Providers.ToDictionary(
            publication => publication.PublicationId);
        var staleSelections = selectedPublicationIds
            .Where(publicationId =>
                !publicationsById.ContainsKey(publicationId) &&
                (!importsByPublicationId.TryGetValue(publicationId, out var import) ||
                    import.SelectionState != SharedProviderSelectionState.Selected))
            .OrderBy(publicationId => publicationId.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (staleSelections.Length > 0)
        {
            throw new SharedProviderSelectionConflictException(staleSelections);
        }

        var decisions = new List<SharedProviderReconciliationDecision>();
        foreach (var publication in catalog.Providers)
        {
            if (!importsByPublicationId.TryGetValue(publication.PublicationId, out var import))
            {
                if (selectedPublicationIds.Contains(publication.PublicationId))
                {
                    decisions.Add(CreateDecision(
                        SharedProviderReconciliationDecisionKind.Create,
                        publication,
                        import: null));
                }

                continue;
            }

            if (RequiresRefresh(import, publication))
            {
                decisions.Add(CreateDecision(
                    SharedProviderReconciliationDecisionKind.Refresh,
                    publication,
                    import));
            }

            AddSelectionDecision(
                decisions,
                import,
                publication,
                selectedPublicationIds,
                selectionMode);
        }

        var authoritativePublicationIds = publicationsById.Keys.ToHashSet();
        foreach (var import in existingImports.Where(import =>
                     !authoritativePublicationIds.Contains(import.RemotePublicationId)))
        {
            if (import.SelectionState == SharedProviderSelectionState.Selected &&
                import.AvailabilityState != SharedProviderAvailabilityState.Missing)
            {
                decisions.Add(CreateDecision(
                    SharedProviderReconciliationDecisionKind.MarkMissing,
                    import));
            }

            if (selectionMode == SharedProviderSelectionMode.Replace &&
                !selectedPublicationIds.Contains(import.RemotePublicationId) &&
                import.SelectionState == SharedProviderSelectionState.Selected)
            {
                decisions.Add(CreateDecision(
                    SharedProviderReconciliationDecisionKind.Retire,
                    import));
            }
        }

        var ordered = decisions
            .OrderBy(decision => decision.PublicationId.ToString(), StringComparer.Ordinal)
            .ThenBy(decision => GetExecutionOrder(decision.Kind))
            .ToArray();
        return new SharedProviderReconciliationPlan(ordered);
    }

    private static Dictionary<SharedProviderPublicationId, SharedProviderImport> BuildImportIndex(
        IReadOnlyCollection<SharedProviderImport> existingImports)
    {
        var result = new Dictionary<SharedProviderPublicationId, SharedProviderImport>();
        foreach (var import in existingImports)
        {
            ArgumentNullException.ThrowIfNull(import);
            if (!result.TryAdd(import.RemotePublicationId, import))
            {
                throw new InvalidOperationException(
                    "Shared-provider reconciliation received duplicate persisted import identities.");
            }
        }

        return result;
    }

    private static bool RequiresRefresh(
        SharedProviderImport import,
        SharedProviderCatalogPublication publication)
    {
        var remoteState = SharedProviderRemotePublicationState.Create(publication);
        return import.RemoteRevision != remoteState.Revision ||
            import.RemoteDisplayName != remoteState.DisplayName ||
            import.RemotePurpose != remoteState.Purpose ||
            import.RemoteTransport != remoteState.Transport ||
            import.RemoteDefaultModelId != remoteState.DefaultModelId ||
            import.RemoteCatalogSnapshotJson != remoteState.CatalogSnapshotJson ||
            import.AvailabilityState != SharedProviderAvailabilityState.Available;
    }

    private static void AddSelectionDecision(
        ICollection<SharedProviderReconciliationDecision> decisions,
        SharedProviderImport import,
        SharedProviderCatalogPublication publication,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        SharedProviderSelectionMode selectionMode)
    {
        if (selectedPublicationIds.Contains(publication.PublicationId))
        {
            if (import.SelectionState == SharedProviderSelectionState.Retired)
            {
                decisions.Add(CreateDecision(
                    SharedProviderReconciliationDecisionKind.Reactivate,
                    publication,
                    import));
            }

            return;
        }

        if (selectionMode == SharedProviderSelectionMode.Replace &&
            import.SelectionState == SharedProviderSelectionState.Selected)
        {
            decisions.Add(CreateDecision(
                SharedProviderReconciliationDecisionKind.Retire,
                publication,
                import));
        }
    }

    private static SharedProviderReconciliationDecision CreateDecision(
        SharedProviderReconciliationDecisionKind kind,
        SharedProviderCatalogPublication publication,
        SharedProviderImport? import)
        => new(
            kind,
            publication.PublicationId,
            import?.Id,
            import?.ProviderProfileId,
            publication);

    private static SharedProviderReconciliationDecision CreateDecision(
        SharedProviderReconciliationDecisionKind kind,
        SharedProviderImport import)
        => new(
            kind,
            import.RemotePublicationId,
            import.Id,
            import.ProviderProfileId,
            remotePublication: null);

    private static int GetExecutionOrder(SharedProviderReconciliationDecisionKind kind)
        => kind switch
        {
            SharedProviderReconciliationDecisionKind.Create => 0,
            SharedProviderReconciliationDecisionKind.Refresh => 1,
            SharedProviderReconciliationDecisionKind.MarkMissing => 2,
            SharedProviderReconciliationDecisionKind.Reactivate => 3,
            SharedProviderReconciliationDecisionKind.Retire => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
