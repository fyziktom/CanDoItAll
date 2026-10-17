using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class CanonicalAgentHistoricalReceiptReadObserver {
    private readonly AgentHistoricalCatalogBasis basis;
    private readonly IReadOnlyDictionary<string, IAgentHistoricalReceiptSourceAuthorityProvider> sources;

    public CanonicalAgentHistoricalReceiptReadObserver(AgentHistoricalCatalogBasis basis,
        IEnumerable<IAgentHistoricalReceiptSourceAuthorityProvider> sources) {
        this.basis = basis;
        this.sources = sources.ToDictionary(source => source.SourceKind, StringComparer.Ordinal);
    }

    internal ValueTask<AgentHistoricalReceiptReadPolicy> ObserveAsync(AgentToolSessionObservation original,
        AgentHistoricalReceiptReadFence currentFence, CancellationToken cancellationToken) {
        basis.RequireCurrent(cancellationToken);
        if (basis.Fence != currentFence) {
            return ValueTask.FromResult(AgentHistoricalReceiptReadPolicy.ContextChanged);
        }
        if (!MatchesOriginalProfile(original, currentFence)) {
            return ValueTask.FromResult(AgentHistoricalReceiptReadPolicy.Denied);
        }
        return ObserveSelectedAsync(original, currentFence, basis.RequireExecutor(original.Session.AgentId, cancellationToken),
            () => {
                basis.RequireCurrent(cancellationToken);
                return true;
            }, kind => sources.TryGetValue(kind, out var source) ? source.ResolveHistoricalReadAsync : null, cancellationToken);
    }

    internal static async ValueTask<AgentHistoricalReceiptReadPolicy> ObserveAsync(
        AgentToolSessionObservation original, AgentHistoricalReceiptReadFence currentFence,
        Func<bool> matchesCurrentFence, IAgentHistoricalCatalogReadStore? historicalCatalog,
        Func<string, Func<AgentExecutionSourceAuthorityRequest, CancellationToken, ValueTask<AgentExecutionSourceAuthorityDecision>>?> findSource,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(currentFence);
        if (!matchesCurrentFence()) {
            return AgentHistoricalReceiptReadPolicy.ContextChanged;
        }
        if (!MatchesOriginalProfile(original, currentFence)) {
            return AgentHistoricalReceiptReadPolicy.Denied;
        }
        if (historicalCatalog is null) {
            return AgentHistoricalReceiptReadPolicy.Unsupported;
        }
        var catalog = await historicalCatalog.ReadHistoricalAgentAsync(original.Session.AgentId, cancellationToken);
        if (catalog.Status == AgentHistoricalStorageReadStatus.Missing) {
            return AgentHistoricalReceiptReadPolicy.Denied;
        }
        if (catalog.Status != AgentHistoricalStorageReadStatus.Ready) {
            throw new IOException("Current historical Agent read is unavailable.");
        }
        if (catalog.Scope != currentFence.Scope) {
            return AgentHistoricalReceiptReadPolicy.Denied;
        }
        var decision = await ObserveSelectedAsync(original, currentFence, catalog.Agent, matchesCurrentFence, findSource, cancellationToken);
        if (decision != AgentHistoricalReceiptReadPolicy.Allowed || original.Session.Reference.BackgroundSource is not null) {
            return decision;
        }
        var final = await historicalCatalog.ReadHistoricalAgentAsync(original.Session.AgentId, cancellationToken);
        if (!matchesCurrentFence()) {
            return AgentHistoricalReceiptReadPolicy.ContextChanged;
        }
        if (final.Status == AgentHistoricalStorageReadStatus.Missing) {
            return AgentHistoricalReceiptReadPolicy.Denied;
        }
        if (final.Status != AgentHistoricalStorageReadStatus.Ready || final.Revision != catalog.Revision) {
            throw new IOException("Current Agent authority changed during historical receipt observation.");
        }
        return decision;
    }

    private static async ValueTask<AgentHistoricalReceiptReadPolicy> ObserveSelectedAsync(
        AgentToolSessionObservation original, AgentHistoricalReceiptReadFence currentFence, AgentDefinition? agent,
        Func<bool> matchesCurrentFence,
        Func<string, Func<AgentExecutionSourceAuthorityRequest, CancellationToken, ValueTask<AgentExecutionSourceAuthorityDecision>>?> findSource,
        CancellationToken cancellationToken) {
        var session = original.Session;
        if (!MatchesOriginalProfile(original, currentFence) || agent is null || agent.Id != session.AgentId ||
            agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active) {
            return AgentHistoricalReceiptReadPolicy.Denied;
        }
        if (!matchesCurrentFence()) {
            return AgentHistoricalReceiptReadPolicy.ContextChanged;
        }
        if (session.Reference.BackgroundSource is not null) {
            return session.Purpose == AgentRuntimeContextPurpose.GovernedProcessAutomation &&
                original.Governance is null && original.TurnContext is null
                ? AgentHistoricalReceiptReadPolicy.Allowed : AgentHistoricalReceiptReadPolicy.Denied;
        }
        if (original.Governance is not { } governance || original.TurnContext is not { } source ||
            session.Purpose != AgentRuntimeContextPurpose.InteractiveChat || !governance.ReadAllowed ||
            governance.AuthorityId != session.Reference.AuthorityId || governance.AgentId != session.AgentId ||
            governance.DatabaseProfileId != session.Profile.ProfileId || governance.DatabaseProfileGeneration != session.Profile.Generation) {
            return AgentHistoricalReceiptReadPolicy.Denied;
        }
        if (source.SourceKind.Value != AgentChatTrustedSourceKinds.ProjectStructure ||
            governance.EffectiveSchemaVersion != AgentExecutionAuthorityRecord.CurrentSchemaVersion || governance.SourceProjectLifetime is null ||
            findSource(source.SourceKind.Value) is not { } resolveSource) {
            return AgentHistoricalReceiptReadPolicy.Unsupported;
        }
        AgentExecutionSourceAuthorityDecision decision;
        try {
            decision = await resolveSource(new(agent, source.SourceKind, source.SourceId,
                governance.WorkspaceScope, currentFence.Profile.ProfileId) {
                Revalidation = new(source, governance), ObservedProjectLifetime = governance.SourceProjectLifetime
            }, cancellationToken);
        } catch (Exception exception) when (exception is AgentExecutionAuthorityMismatchException or AgentChatContextAccessDeniedException) {
            return AgentHistoricalReceiptReadPolicy.Denied;
        }
        if (!matchesCurrentFence()) {
            return AgentHistoricalReceiptReadPolicy.ContextChanged;
        }
        return decision.ReadAllowed && decision.WorkspaceScope == governance.WorkspaceScope && decision.SourceProjectLifetime == governance.SourceProjectLifetime
            ? AgentHistoricalReceiptReadPolicy.Allowed : AgentHistoricalReceiptReadPolicy.Denied;
    }

    private static bool MatchesOriginalProfile(AgentToolSessionObservation original, AgentHistoricalReceiptReadFence currentFence)
        => original.Session.Profile.ProfileId == currentFence.Profile.ProfileId &&
           original.Session.Profile.Fingerprint == currentFence.Profile.Fingerprint;
}
