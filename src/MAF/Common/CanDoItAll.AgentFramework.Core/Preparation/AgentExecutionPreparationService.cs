using System.Diagnostics;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentExecutionProfileGenerationSource
{
    DatabaseProfileGeneration GetGeneration();
}

public sealed class FixedAgentExecutionProfileGenerationSource(
    DatabaseProfileGeneration generation) :
    IAgentExecutionProfileGenerationSource
{
    public DatabaseProfileGeneration GetGeneration()
        => generation;
}

public enum AgentExecutionPreparationSource
{
    Reused,
    Refreshed
}

public enum AgentExecutionPreparationUseValidation
{
    Current,
    CatalogRevisionChanged,
    DatabaseProfileGenerationChanged,
    AgentProviderChanged,
    ProviderConfigurationChanged
}

public sealed record AgentExecutionPreparationSnapshot(
    AgentExecutionPreparationBlueprint Blueprint,
    SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
    AgentExecutionPreparationSource Source,
    TimeSpan Elapsed);

public sealed class AgentExecutionPreparationCapacityException(
    AgentExecutionPreparationKey key,
    int capacity)
    : InvalidOperationException(
        $"Agent execution preparation capacity {capacity} is exhausted for agent '{key.AgentId}'.")
{
    public AgentExecutionPreparationKey Key { get; } = key;

    public int Capacity { get; } = capacity;
}

public sealed class AgentExecutionPreparationChurnException(
    AgentExecutionPreparationKey key,
    int attempts)
    : InvalidOperationException(
        $"Agent execution preparation for '{key.AgentId}' changed during all {attempts} validation attempts.")
{
    public AgentExecutionPreparationKey Key { get; } = key;

    public int Attempts { get; } = attempts;
}

public sealed class AgentExecutionPreparationStaleException(
    AgentExecutionPreparationKey key,
    AgentExecutionPreparationUseValidation validation)
    : InvalidOperationException(
        $"Agent execution preparation for '{key.AgentId}' is stale at use time because validation returned '{validation}'.")
{
    public AgentExecutionPreparationKey Key { get; } = key;

    public AgentExecutionPreparationUseValidation Validation { get; } =
        validation;
}

public interface IAgentExecutionPreparationService
{
    Task<AgentExecutionPreparationSnapshot> AcquireAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<AgentExecutionPreparationSnapshot> AcquireForAtomicConsumerAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    AgentExecutionPreparationUseValidation ValidateForUse(
        AgentExecutionPreparationBlueprint blueprint,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot);
}

public sealed class ProviderRuntimeProfileUnavailableException(
    Guid providerId) : InvalidOperationException(
    $"Provider runtime profile '{providerId:D}' is unavailable.")
{
    public Guid ProviderId { get; } = providerId;
}

public sealed class AgentExecutionPreparationService(
    ISandboxWorkspaceCatalogStore store,
    IProviderRuntimeProfileSnapshotSource providerSnapshotSource,
    IAgentExecutionPreparationCache cache,
    IAgentExecutionProfileGenerationSource profileGenerationSource,
    AgentExecutionActivityWorkspaceIdentity workspaceIdentity) :
    IAgentExecutionPreparationService
{
    private const int MaximumValidationAttempts = 3;

    public async Task<AgentExecutionPreparationSnapshot> AcquireAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await AcquireCoreAsync(
                agentId,
                revalidateRefreshedSnapshot: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AgentExecutionPreparationSnapshot>
        AcquireForAtomicConsumerAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
    {
        return await AcquireCoreAsync(
                agentId,
                revalidateRefreshedSnapshot: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public AgentExecutionPreparationUseValidation ValidateForUse(
        AgentExecutionPreparationBlueprint blueprint,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        EnsureCoherentRevision(catalogSnapshot);

        var version = blueprint.Request.Version;
        if (catalogSnapshot.Revision != version.CatalogRevision)
        {
            return AgentExecutionPreparationUseValidation
                .CatalogRevisionChanged;
        }

        if (profileGenerationSource.GetGeneration() !=
            version.DatabaseProfileGeneration)
        {
            return AgentExecutionPreparationUseValidation
                .DatabaseProfileGenerationChanged;
        }

        var agent = EnsureAgentExists(
            catalogSnapshot.Catalog,
            blueprint.Request.Key.AgentId);
        if (agent.ProviderProfileId != blueprint.Provider.Id)
        {
            return AgentExecutionPreparationUseValidation.AgentProviderChanged;
        }

        var currentProvider = CaptureCurrentProvider(
            agent,
            catalogSnapshot);
        if (currentProvider is null ||
            currentProvider.Value.ConfigurationFingerprint !=
            version.ProviderFingerprint)
        {
            return AgentExecutionPreparationUseValidation
                .ProviderConfigurationChanged;
        }

        return AgentExecutionPreparationUseValidation.Current;
    }

    private async Task<AgentExecutionPreparationSnapshot> AcquireCoreAsync(
        Guid agentId,
        bool revalidateRefreshedSnapshot,
        CancellationToken cancellationToken)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException(
                "An agent identifier is required.",
                nameof(agentId));
        }

        var startedTimestamp = Stopwatch.GetTimestamp();
        var key = new AgentExecutionPreparationKey(
            workspaceIdentity.DatabaseProfileId,
            workspaceIdentity.WorkspaceScope,
            agentId);

        for (var attempt = 1; attempt <= MaximumValidationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var catalogSnapshot = await store
                .LoadCatalogSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            EnsureCoherentRevision(catalogSnapshot);

            var agent = EnsureAgentExists(catalogSnapshot.Catalog, agentId);
            var resolvedProvider = await ResolveCurrentProviderAsync(
                agent,
                catalogSnapshot,
                cancellationToken)
                .ConfigureAwait(false);
            var provider = resolvedProvider.Profile;
            var request = new AgentExecutionPreparationRequest(
                key,
                new AgentExecutionPreparationVersion(
                    catalogSnapshot.Revision,
                    profileGenerationSource.GetGeneration(),
                    resolvedProvider.ConfigurationFingerprint));

            AgentExecutionPreparationAcquireResult acquisition;
            try
            {
                acquisition = await cache.AcquireAsync(
                        request,
                        sharedCancellationToken => CreateBlueprintAsync(
                        request,
                        catalogSnapshot.Catalog,
                        agent,
                        provider,
                        sharedCancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (AgentExecutionPreparationInvalidatedException)
                when (attempt < MaximumValidationAttempts)
            {
                continue;
            }

            if (acquisition is AgentExecutionPreparationRejected rejected)
            {
                throw new AgentExecutionPreparationCapacityException(
                    rejected.Key,
                    rejected.Capacity);
            }

            var acquired = (AgentExecutionPreparationAcquired)acquisition;
            if (acquired.Disposition ==
                AgentExecutionPreparationCacheDisposition.Reused)
            {
                return new AgentExecutionPreparationSnapshot(
                    acquired.Blueprint,
                    catalogSnapshot,
                    AgentExecutionPreparationSource.Reused,
                    Stopwatch.GetElapsedTime(startedTimestamp));
            }

            if (!revalidateRefreshedSnapshot)
            {
                return new AgentExecutionPreparationSnapshot(
                    acquired.Blueprint,
                    catalogSnapshot,
                    AgentExecutionPreparationSource.Refreshed,
                    Stopwatch.GetElapsedTime(startedTimestamp));
            }

            var revalidatedSnapshot = await store
                .LoadCatalogSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            EnsureCoherentRevision(revalidatedSnapshot);
            if (IsStillCurrent(request, revalidatedSnapshot))
            {
                return new AgentExecutionPreparationSnapshot(
                    acquired.Blueprint,
                    revalidatedSnapshot,
                    AgentExecutionPreparationSource.Refreshed,
                    Stopwatch.GetElapsedTime(startedTimestamp));
            }

            cache.Invalidate(key);
        }

        throw new AgentExecutionPreparationChurnException(
            key,
            MaximumValidationAttempts);
    }

    private static Task<AgentExecutionPreparationBlueprint> CreateBlueprintAsync(
        AgentExecutionPreparationRequest request,
        SandboxWorkspaceCatalog catalog,
        AgentDefinition agent,
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            AgentExecutionPreparationBlueprint.Create(
                request,
                agent,
                provider,
                ResolveAttachedCapabilities(catalog, agent),
                ResolveAgentMemory(catalog, agent.Id)));
    }

    private bool IsStillCurrent(
        AgentExecutionPreparationRequest request,
        SandboxWorkspaceCatalogSnapshot snapshot)
    {
        if (snapshot.Revision != request.Version.CatalogRevision ||
            profileGenerationSource.GetGeneration() !=
            request.Version.DatabaseProfileGeneration)
        {
            return false;
        }

        var agent = EnsureAgentExists(
            snapshot.Catalog,
            request.Key.AgentId);
        var provider = CaptureCurrentProvider(agent, snapshot);
        return provider is not null &&
               provider.Value.ConfigurationFingerprint ==
               request.Version.ProviderFingerprint;
    }

    private async Task<ResolvedProviderSnapshot> ResolveCurrentProviderAsync(
        AgentDefinition agent,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot,
        CancellationToken cancellationToken)
    {
        if (agent.ProviderProfileId is not Guid providerId)
        {
            throw new InvalidOperationException(
                "The selected agent does not have a provider profile.");
        }

        var lease = await providerSnapshotSource.AcquireProviderAsync(
                providerId,
                catalogSnapshot,
                cancellationToken)
            .ConfigureAwait(false);
        return lease is null
            ? throw new InvalidOperationException(
                "The selected agent does not have a provider profile.")
            : ResolveProviderSnapshot(agent, lease);
    }

    private ResolvedProviderSnapshot? CaptureCurrentProvider(
        AgentDefinition agent,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot)
    {
        if (agent.ProviderProfileId is not Guid providerId)
        {
            return null;
        }

        var lease = providerSnapshotSource.CaptureProvider(
            providerId,
            catalogSnapshot);
        if (lease is null)
        {
            return null;
        }

        return ResolveProviderSnapshot(agent, lease);
    }

    private static ResolvedProviderSnapshot ResolveProviderSnapshot(
        AgentDefinition agent,
        ProviderRuntimeProfileSnapshotLease lease)
    {
        var provider = ManagedSeedProviderFallbacks.Apply(
            agent,
            lease.Profile);
        if (!provider.IsEnabled)
        {
            throw new ProviderRuntimeProfileUnavailableException(provider.Id);
        }

        var fingerprint = ReferenceEquals(provider, lease.Profile)
            ? lease.ConfigurationFingerprint
            : ProviderConfigurationFingerprintFactory.Create(provider);
        return new ResolvedProviderSnapshot(provider, fingerprint);
    }

    private static AgentDefinition EnsureAgentExists(
        SandboxWorkspaceCatalog catalog,
        Guid agentId)
    {
        return catalog.Agents.FirstOrDefault(item => item.Id == agentId)
            ?? throw new InvalidOperationException(
                $"Agent '{agentId:N}' was not found.");
    }

    private static IReadOnlyList<CapabilityCatalogItem>
        ResolveAttachedCapabilities(
            SandboxWorkspaceCatalog catalog,
            AgentDefinition agent)
    {
        var attachedCapabilityIds = agent.Capabilities
            .Select(item => item.CapabilityId)
            .ToHashSet();
        return catalog.Capabilities
            .Where(item => attachedCapabilityIds.Contains(item.Id))
            .Where(item =>
                !AgentCapabilityRequirementEvaluator.IsRetiredCapability(item))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AgentMemoryRecord> ResolveAgentMemory(
        SandboxWorkspaceCatalog catalog,
        Guid agentId)
    {
        return catalog.Memory
            .Where(item => item.AgentId == agentId)
            .OrderByDescending(item => item.Importance)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static void EnsureCoherentRevision(
        SandboxWorkspaceCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.Revision.IsAssigned ||
            snapshot.Catalog.CatalogDataRevision != snapshot.Revision)
        {
            throw new InvalidOperationException(
                "The workspace catalog snapshot has an invalid or incoherent data revision.");
        }
    }

    private readonly record struct ResolvedProviderSnapshot(
        ProviderProfile Profile,
        ProviderConfigurationFingerprint ConfigurationFingerprint);
}
