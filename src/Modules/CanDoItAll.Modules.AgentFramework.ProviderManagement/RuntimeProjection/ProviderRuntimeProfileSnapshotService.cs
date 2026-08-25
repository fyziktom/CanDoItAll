using System.Collections.Immutable;
using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using AgentFrameworkProviderProfile =
    CanDoItAll.AgentFramework.Models.ProviderProfile;

public interface IProviderRuntimeProfileSnapshotLoader
{
    Task<IReadOnlyList<CanonicalProviderRuntimeProfile>> LoadAllAsync(
        CancellationToken cancellationToken = default);

    Task<CanonicalProviderRuntimeProfile?> LoadAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<ProviderConfigurationRevision?> LoadRevisionAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ProviderConfigurationRevision>>
        LoadRevisionsAsync(CancellationToken cancellationToken = default);
}

internal interface IProviderRuntimeProfileSnapshotUpdater
{
    void Upsert(CanonicalProviderRuntimeProfile provider);

    void Remove(Guid providerId);

    void Invalidate(Guid providerId, Exception cause);
}

public sealed record CanonicalProviderRuntimeProfile(
    AgentFrameworkProviderProfile Profile,
    ProviderConfigurationRevision? ConfigurationRevision);

internal sealed class CanonicalProviderRuntimeProfileSnapshotService :
    IProviderRuntimeProfileSource,
    IProviderRuntimeProfileSnapshotSource,
    IProviderRuntimeProfileSnapshotInitializer,
    IProviderRuntimeProfileSnapshotUpdater,
    IDisposable
{
    private const int MaximumRefreshPublicationAttempts = 3;

    private readonly object stateGate = new();
    private readonly SemaphoreSlim rebuildGate = new(1, 1);
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly IDatabaseSwitchNotificationService switchNotifications;
    private readonly ILogger<CanonicalProviderRuntimeProfileSnapshotService>
        logger;
    private SnapshotState state = SnapshotState.NotReady(
        databaseProfileId: null,
        databaseFingerprint: null,
        databaseProfileGeneration: 0,
        publicationGeneration: 0);
    private long publicationGeneration;
    private bool disposed;

    public CanonicalProviderRuntimeProfileSnapshotService(
        IServiceScopeFactory serviceScopeFactory,
        IDatabaseRuntimeState runtimeState,
        IDatabaseSwitchNotificationService switchNotifications,
        ILogger<CanonicalProviderRuntimeProfileSnapshotService> logger)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.runtimeState = runtimeState;
        this.switchNotifications = switchNotifications;
        this.logger = logger;
        switchNotifications.Changed += HandleDatabaseProfileChanged;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await rebuildGate.WaitAsync(cancellationToken);
        try
        {
            var runtime = RequireRuntimeIdentity(runtimeState.GetSnapshot());
            long refreshGeneration;
            lock (stateGate)
            {
                refreshGeneration = ++publicationGeneration;
                Volatile.Write(
                    ref state,
                    SnapshotState.NotReady(
                        runtime.ActiveProfileId,
                        runtime.ActiveFingerprint,
                        runtime.Generation,
                        refreshGeneration));
            }

            IReadOnlyList<CanonicalProviderRuntimeProfile> loadedProviders;
            try
            {
                await using var scope =
                    serviceScopeFactory.CreateAsyncScope();
                var loader = scope.ServiceProvider.GetRequiredService<
                    IProviderRuntimeProfileSnapshotLoader>();
                loadedProviders = await loader
                    .LoadAllAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                PublishFault(
                    runtime,
                    refreshGeneration,
                    exception);
                throw;
            }

            var leases = loadedProviders
                .Select(CreateLease)
                .ToImmutableDictionary(item => item.Profile.Id);
            lock (stateGate)
            {
                var currentRuntime = runtimeState.GetSnapshot();
                if (refreshGeneration != publicationGeneration ||
                    !HasSameIdentity(runtime, currentRuntime))
                {
                    throw new InvalidOperationException(
                        "The canonical provider snapshot refresh was superseded by a newer database profile generation.");
                }

                Volatile.Write(
                    ref state,
                    SnapshotState.Ready(
                        runtime.ActiveProfileId!.Value,
                        runtime.ActiveFingerprint!,
                        runtime.Generation,
                        refreshGeneration,
                        leases));
            }

            logger.LogInformation(
                "Prepared canonical provider runtime snapshot for database profile {ProfileId} at generation {Generation}. ProviderCount={ProviderCount}.",
                runtime.ActiveProfileId,
                runtime.Generation,
                leases.Count);
        }
        finally
        {
            rebuildGate.Release();
        }
    }

    public async Task<IReadOnlyList<AgentFrameworkProviderProfile>>
        ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureProviderSetCurrentAsync(cancellationToken)
            .ConfigureAwait(false);
        return ReadReadyState().OrderedProfiles;
    }

    public async Task<AgentFrameworkProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var lease = await EnsureProviderCurrentAsync(
                providerId,
                cancellationToken)
            .ConfigureAwait(false);
        return lease?.Profile;
    }

    public async Task<ProviderRuntimeProfileSnapshotLease?>
        AcquireProviderAsync(
        Guid providerId,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogSnapshot);
        return await EnsureProviderCurrentAsync(
                providerId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public ProviderRuntimeProfileSnapshotLease? CaptureProvider(
        Guid providerId,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot)
    {
        ArgumentNullException.ThrowIfNull(catalogSnapshot);
        var ready = ReadReadyState();
        return ready.Providers.GetValueOrDefault(providerId);
    }

    public void Upsert(CanonicalProviderRuntimeProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var lease = CreateLease(provider);

        lock (stateGate)
        {
            var ready = EnsureReadyForMutation();
            var nextGeneration = ++publicationGeneration;
            var providers = ready.Providers.SetItem(
                provider.Profile.Id,
                lease);
            Volatile.Write(
                ref state,
                SnapshotState.Ready(
                    ready.DatabaseProfileId!.Value,
                    ready.DatabaseFingerprint!,
                    ready.DatabaseProfileGeneration,
                    nextGeneration,
                    providers));
        }
    }

    public void Remove(Guid providerId)
    {
        lock (stateGate)
        {
            var current = Volatile.Read(ref state);
            if (!IsCurrentRuntime(current))
            {
                PublishNotReadyForCurrentRuntime();
                return;
            }

            if (current.Status != ProviderRuntimeProfileSnapshotStatus.Ready)
            {
                return;
            }

            var nextGeneration = ++publicationGeneration;
            var providers = current.Providers.Remove(providerId);
            Volatile.Write(
                ref state,
                SnapshotState.Ready(
                    current.DatabaseProfileId!.Value,
                    current.DatabaseFingerprint!,
                    current.DatabaseProfileGeneration,
                    nextGeneration,
                    providers));
        }
    }

    public void Invalidate(Guid providerId, Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        lock (stateGate)
        {
            var current = Volatile.Read(ref state);
            if (!IsCurrentRuntime(current))
            {
                PublishNotReadyForCurrentRuntime();
            }
            else
            {
                var nextGeneration = ++publicationGeneration;
                Volatile.Write(
                    ref state,
                    SnapshotState.Faulted(
                        current.DatabaseProfileId!.Value,
                        current.DatabaseFingerprint!,
                        current.DatabaseProfileGeneration,
                        nextGeneration,
                        cause));
            }
        }

        logger.LogError(
            cause,
            "Faulted the canonical runtime snapshot after committed provider {ProviderId} could not be projected.",
            providerId);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        switchNotifications.Changed -= HandleDatabaseProfileChanged;
        rebuildGate.Dispose();
    }

    private async Task<ProviderRuntimeProfileSnapshotLease?>
        EnsureProviderCurrentAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var current = ReadReadyState();
        if (current.Providers.TryGetValue(providerId, out var syntheticLease) &&
            syntheticLease.ConfigurationRevision is null)
        {
            return syntheticLease;
        }

        var startedTimestamp = Stopwatch.GetTimestamp();
        ProviderConfigurationRevision? canonicalRevision;
        try
        {
            await using var scope =
                serviceScopeFactory.CreateAsyncScope();
            var loader = scope.ServiceProvider.GetRequiredService<
                IProviderRuntimeProfileSnapshotLoader>();
            canonicalRevision = await loader.LoadRevisionAsync(
                    providerId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentFrameworkTelemetry.RecordProviderSnapshotRevisionProbe(
                ProviderSnapshotRevisionProbeKind.Provider,
                ProviderSnapshotRevisionProbeOutcome.Failed,
                Stopwatch.GetElapsedTime(startedTimestamp));
            throw FailClosedAfterRevisionVerificationFailure(
                providerId,
                exception);
        }

        current = ReadReadyState();
        var revisionIsCurrent = HasProviderRevision(
            current,
            providerId,
            canonicalRevision);
        AgentFrameworkTelemetry.RecordProviderSnapshotRevisionProbe(
            ProviderSnapshotRevisionProbeKind.Provider,
            revisionIsCurrent
                ? ProviderSnapshotRevisionProbeOutcome.Current
                : ProviderSnapshotRevisionProbeOutcome.Changed,
            Stopwatch.GetElapsedTime(startedTimestamp));
        if (revisionIsCurrent)
        {
            return current.Providers.GetValueOrDefault(providerId);
        }

        try
        {
            return await RefreshProviderAsync(
                    providerId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderRuntimeProfileSnapshotUnavailableException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw FailClosedAfterRevisionVerificationFailure(
                providerId,
                exception);
        }
    }

    private async Task EnsureProviderSetCurrentAsync(
        CancellationToken cancellationToken)
    {
        ReadReadyState();
        var startedTimestamp = Stopwatch.GetTimestamp();
        IReadOnlyDictionary<Guid, ProviderConfigurationRevision>
            canonicalRevisions;
        try
        {
            await using var scope =
                serviceScopeFactory.CreateAsyncScope();
            var loader = scope.ServiceProvider.GetRequiredService<
                IProviderRuntimeProfileSnapshotLoader>();
            canonicalRevisions = await loader.LoadRevisionsAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentFrameworkTelemetry.RecordProviderSnapshotRevisionProbe(
                ProviderSnapshotRevisionProbeKind.ProviderSet,
                ProviderSnapshotRevisionProbeOutcome.Failed,
                Stopwatch.GetElapsedTime(startedTimestamp));
            throw FailClosedAfterRevisionVerificationFailure(
                providerId: null,
                exception);
        }

        var current = ReadReadyState();
        var revisionsAreCurrent = HaveSameProviderRevisions(
            current.PersistentProviderRevisions,
            canonicalRevisions);
        AgentFrameworkTelemetry.RecordProviderSnapshotRevisionProbe(
            ProviderSnapshotRevisionProbeKind.ProviderSet,
            revisionsAreCurrent
                ? ProviderSnapshotRevisionProbeOutcome.Current
                : ProviderSnapshotRevisionProbeOutcome.Changed,
            Stopwatch.GetElapsedTime(startedTimestamp));
        if (revisionsAreCurrent)
        {
            return;
        }

        try
        {
            await InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CreateUnavailableException(exception);
        }
    }

    private async Task<ProviderRuntimeProfileSnapshotLease?>
        RefreshProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        await rebuildGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            for (var attempt = 1;
                 attempt <= MaximumRefreshPublicationAttempts;
                 attempt++)
            {
                await using var scope =
                    serviceScopeFactory.CreateAsyncScope();
                var loader = scope.ServiceProvider.GetRequiredService<
                    IProviderRuntimeProfileSnapshotLoader>();
                var probeStartedTimestamp = Stopwatch.GetTimestamp();
                ProviderConfigurationRevision? canonicalRevision;
                try
                {
                    canonicalRevision = await loader.LoadRevisionAsync(
                            providerId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    AgentFrameworkTelemetry
                        .RecordProviderSnapshotRevisionProbe(
                            ProviderSnapshotRevisionProbeKind.Provider,
                            ProviderSnapshotRevisionProbeOutcome.Failed,
                            Stopwatch.GetElapsedTime(
                                probeStartedTimestamp));
                    throw;
                }

                var current = ReadReadyState();
                var revisionIsCurrent = HasProviderRevision(
                    current,
                    providerId,
                    canonicalRevision);
                AgentFrameworkTelemetry.RecordProviderSnapshotRevisionProbe(
                    ProviderSnapshotRevisionProbeKind.Provider,
                    revisionIsCurrent
                        ? ProviderSnapshotRevisionProbeOutcome.Current
                        : ProviderSnapshotRevisionProbeOutcome.Changed,
                    Stopwatch.GetElapsedTime(probeStartedTimestamp));
                if (revisionIsCurrent)
                {
                    return current.Providers.GetValueOrDefault(providerId);
                }

                var expectedPublicationGeneration =
                    current.PublicationGeneration;
                var loadedProvider = await loader.LoadAsync(
                        providerId,
                        cancellationToken)
                    .ConfigureAwait(false);
                var loadedLease = loadedProvider is null
                    ? null
                    : CreateLease(loadedProvider);
                lock (stateGate)
                {
                    current = EnsureReadyForMutation();
                    if (current.PublicationGeneration !=
                        expectedPublicationGeneration)
                    {
                        continue;
                    }

                    var nextGeneration = ++publicationGeneration;
                    var providers = loadedLease is null
                        ? current.Providers.Remove(providerId)
                        : current.Providers.SetItem(
                            providerId,
                            loadedLease);
                    Volatile.Write(
                        ref state,
                        SnapshotState.Ready(
                            current.DatabaseProfileId!.Value,
                            current.DatabaseFingerprint!,
                            current.DatabaseProfileGeneration,
                            nextGeneration,
                            providers));
                    return loadedLease;
                }
            }

            throw new InvalidOperationException(
                $"The canonical provider '{providerId:D}' changed during all {MaximumRefreshPublicationAttempts} refresh publication attempts.");
        }
        finally
        {
            rebuildGate.Release();
        }
    }

    private ProviderRuntimeProfileSnapshotUnavailableException
        FailClosedAfterRevisionVerificationFailure(
        Guid? providerId,
        Exception cause)
    {
        PublishFaultForCurrentRuntime(cause);
        logger.LogError(
            cause,
            "Canonical provider revision verification failed closed. ProviderId={ProviderId}.",
            providerId);
        return CreateUnavailableException(cause);
    }

    private void PublishFaultForCurrentRuntime(Exception cause)
    {
        lock (stateGate)
        {
            var current = Volatile.Read(ref state);
            if (!IsCurrentRuntime(current))
            {
                PublishNotReadyForCurrentRuntime();
                return;
            }

            var nextGeneration = ++publicationGeneration;
            Volatile.Write(
                ref state,
                SnapshotState.Faulted(
                    current.DatabaseProfileId!.Value,
                    current.DatabaseFingerprint!,
                    current.DatabaseProfileGeneration,
                    nextGeneration,
                    cause));
        }
    }

    private ProviderRuntimeProfileSnapshotUnavailableException
        CreateUnavailableException(Exception? cause = null)
    {
        var current = Volatile.Read(ref state);
        var runtime = runtimeState.GetSnapshot();
        return new ProviderRuntimeProfileSnapshotUnavailableException(
            current.Status,
            runtime.ActiveProfileId,
            runtime.Generation,
            cause ?? current.Fault);
    }

    private static bool HasProviderRevision(
        SnapshotState snapshot,
        Guid providerId,
        ProviderConfigurationRevision? canonicalRevision)
    {
        if (canonicalRevision is null)
        {
            return !snapshot.Providers.ContainsKey(providerId);
        }

        return snapshot.Providers.TryGetValue(providerId, out var lease) &&
               lease.ConfigurationRevision == canonicalRevision;
    }

    private static bool HaveSameProviderRevisions(
        IReadOnlyDictionary<Guid, ProviderConfigurationRevision> expected,
        IReadOnlyDictionary<Guid, ProviderConfigurationRevision> actual)
    {
        return expected.Count == actual.Count &&
               expected.All(item =>
                   actual.TryGetValue(item.Key, out var revision) &&
                   revision == item.Value);
    }

    private SnapshotState ReadReadyState()
    {
        var current = Volatile.Read(ref state);
        if (current.Status == ProviderRuntimeProfileSnapshotStatus.Ready &&
            IsCurrentRuntime(current))
        {
            return current;
        }

        var runtime = runtimeState.GetSnapshot();
        throw new ProviderRuntimeProfileSnapshotUnavailableException(
            current.Status,
            runtime.ActiveProfileId,
            runtime.Generation,
            current.Fault);
    }

    private SnapshotState EnsureReadyForMutation()
    {
        var current = Volatile.Read(ref state);
        if (current.Status == ProviderRuntimeProfileSnapshotStatus.Ready &&
            IsCurrentRuntime(current))
        {
            return current;
        }

        var runtime = runtimeState.GetSnapshot();
        throw new ProviderRuntimeProfileSnapshotUnavailableException(
            current.Status,
            runtime.ActiveProfileId,
            runtime.Generation,
            current.Fault);
    }

    private bool IsCurrentRuntime(SnapshotState snapshot)
    {
        return HasSameIdentity(snapshot, runtimeState.GetSnapshot());
    }

    private void PublishFault(
        DatabaseRuntimeSnapshot runtime,
        long refreshGeneration,
        Exception exception)
    {
        lock (stateGate)
        {
            if (refreshGeneration != publicationGeneration ||
                !HasSameIdentity(runtime, runtimeState.GetSnapshot()))
            {
                return;
            }

            Volatile.Write(
                ref state,
                SnapshotState.Faulted(
                    runtime.ActiveProfileId!.Value,
                    runtime.ActiveFingerprint!,
                    runtime.Generation,
                    refreshGeneration,
                    exception));
        }
    }

    private void HandleDatabaseProfileChanged(
        object? sender,
        DatabaseProfileChangedNotification notification)
    {
        lock (stateGate)
        {
            var nextGeneration = ++publicationGeneration;
            Volatile.Write(
                ref state,
                SnapshotState.NotReady(
                    notification.CurrentProfileId,
                    notification.CurrentFingerprint,
                    notification.Generation,
                    nextGeneration));
        }
    }

    private SnapshotState PublishNotReadyForCurrentRuntime()
    {
        var runtime = RequireRuntimeIdentity(runtimeState.GetSnapshot());
        var nextGeneration = ++publicationGeneration;
        var notReady = SnapshotState.NotReady(
            runtime.ActiveProfileId,
            runtime.ActiveFingerprint,
            runtime.Generation,
            nextGeneration);
        Volatile.Write(ref state, notReady);
        return notReady;
    }

    private static DatabaseRuntimeSnapshot RequireRuntimeIdentity(
        DatabaseRuntimeSnapshot runtime)
    {
        if (runtime.ActiveProfileId is null ||
            string.IsNullOrWhiteSpace(runtime.ActiveFingerprint))
        {
            throw new ProviderRuntimeProfileSnapshotUnavailableException(
                ProviderRuntimeProfileSnapshotStatus.NotReady,
                runtime.ActiveProfileId,
                runtime.Generation);
        }

        return runtime;
    }

    private static bool HasSameIdentity(
        DatabaseRuntimeSnapshot expected,
        DatabaseRuntimeSnapshot actual)
    {
        return expected.ActiveProfileId == actual.ActiveProfileId &&
               expected.Generation == actual.Generation &&
               string.Equals(
                   expected.ActiveFingerprint,
                   actual.ActiveFingerprint,
                   StringComparison.Ordinal);
    }

    private static bool HasSameIdentity(
        SnapshotState expected,
        DatabaseRuntimeSnapshot actual)
    {
        return expected.DatabaseProfileId == actual.ActiveProfileId &&
               expected.DatabaseProfileGeneration == actual.Generation &&
               string.Equals(
                   expected.DatabaseFingerprint,
                   actual.ActiveFingerprint,
                   StringComparison.Ordinal);
    }

    private static ProviderRuntimeProfileSnapshotLease CreateLease(
        CanonicalProviderRuntimeProfile provider)
    {
        var immutableProvider = provider.Profile with
        {
            SuggestedModels =
                provider.Profile.SuggestedModels.ToImmutableArray(),
            ModelPrices =
                provider.Profile.ModelPrices.ToImmutableArray(),
            Tags = provider.Profile.Tags.ToImmutableArray()
        };
        return new ProviderRuntimeProfileSnapshotLease(
            immutableProvider,
            ProviderConfigurationFingerprintFactory.Create(
                immutableProvider),
            provider.ConfigurationRevision);
    }

    private sealed record SnapshotState(
        ProviderRuntimeProfileSnapshotStatus Status,
        Guid? DatabaseProfileId,
        string? DatabaseFingerprint,
        long DatabaseProfileGeneration,
        long PublicationGeneration,
        ImmutableDictionary<Guid, ProviderRuntimeProfileSnapshotLease>
            Providers,
        ImmutableDictionary<Guid, ProviderConfigurationRevision>
            PersistentProviderRevisions,
        ImmutableArray<AgentFrameworkProviderProfile> OrderedProfiles,
        Exception? Fault)
    {
        public static SnapshotState NotReady(
            Guid? databaseProfileId,
            string? databaseFingerprint,
            long databaseProfileGeneration,
            long publicationGeneration)
        {
            return new SnapshotState(
                ProviderRuntimeProfileSnapshotStatus.NotReady,
                databaseProfileId,
                databaseFingerprint,
                databaseProfileGeneration,
                publicationGeneration,
                ImmutableDictionary<
                    Guid,
                    ProviderRuntimeProfileSnapshotLease>.Empty,
                ImmutableDictionary<
                    Guid,
                    ProviderConfigurationRevision>.Empty,
                [],
                null);
        }

        public static SnapshotState Ready(
            Guid databaseProfileId,
            string databaseFingerprint,
            long databaseProfileGeneration,
            long publicationGeneration,
            ImmutableDictionary<
                Guid,
                ProviderRuntimeProfileSnapshotLease> providers)
        {
            return new SnapshotState(
                ProviderRuntimeProfileSnapshotStatus.Ready,
                databaseProfileId,
                databaseFingerprint,
                databaseProfileGeneration,
                publicationGeneration,
                providers,
                providers
                    .Where(item =>
                        item.Value.ConfigurationRevision.HasValue)
                    .ToImmutableDictionary(
                        item => item.Key,
                        item => item.Value.ConfigurationRevision
                            .GetValueOrDefault()),
                providers.Values
                    .Select(item => item.Profile)
                    .OrderBy(
                        item => item.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToImmutableArray(),
                null);
        }

        public static SnapshotState Faulted(
            Guid databaseProfileId,
            string databaseFingerprint,
            long databaseProfileGeneration,
            long publicationGeneration,
            Exception fault)
        {
            return new SnapshotState(
                ProviderRuntimeProfileSnapshotStatus.Faulted,
                databaseProfileId,
                databaseFingerprint,
                databaseProfileGeneration,
                publicationGeneration,
                ImmutableDictionary<
                    Guid,
                    ProviderRuntimeProfileSnapshotLease>.Empty,
                ImmutableDictionary<
                    Guid,
                    ProviderConfigurationRevision>.Empty,
                [],
                fault);
        }
    }
}

internal sealed class AgentFrameworkProviderRuntimeSnapshotCommitObserver(
    IProviderRuntimeProfileSnapshotLoader loader,
    IProviderRuntimeProfileSnapshotUpdater updater,
    ILogger<AgentFrameworkProviderRuntimeSnapshotCommitObserver> logger) :
    IProviderProfileCommitObserver
{
    public async Task ProviderSavedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await loader.LoadAsync(
                providerId,
                cancellationToken);
            if (provider is null)
            {
                updater.Remove(providerId);
                return;
            }

            updater.Upsert(provider);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            updater.Invalidate(providerId, exception);
            logger.LogWarning(
                "The canonical runtime provider snapshot was faulted because committed provider {ProviderId} could not be projected.",
                providerId);
        }
    }

    public Task ProviderDeletedAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        updater.Remove(providerId);
        return Task.CompletedTask;
    }
}
