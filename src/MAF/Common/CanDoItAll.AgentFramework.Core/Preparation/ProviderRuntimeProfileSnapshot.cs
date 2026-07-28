using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public readonly record struct ProviderConfigurationRevision(Guid Value);

public sealed record ProviderRuntimeProfileSnapshotLease(
    ProviderProfile Profile,
    ProviderConfigurationFingerprint ConfigurationFingerprint,
    ProviderConfigurationRevision? ConfigurationRevision = null);

public interface IProviderRuntimeProfileSnapshotSource
{
    Task<ProviderRuntimeProfileSnapshotLease?> AcquireProviderAsync(
        Guid providerId,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CaptureProvider(providerId, catalogSnapshot));
    }

    ProviderRuntimeProfileSnapshotLease? CaptureProvider(
        Guid providerId,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot);
}

public interface IProviderRuntimeProfileSnapshotInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public enum ProviderRuntimeProfileSnapshotStatus
{
    NotReady,
    Ready,
    Faulted
}

public sealed class ProviderRuntimeProfileSnapshotUnavailableException :
    InvalidOperationException
{
    public ProviderRuntimeProfileSnapshotUnavailableException(
        ProviderRuntimeProfileSnapshotStatus status,
        Guid? databaseProfileId,
        long databaseProfileGeneration,
        Exception? innerException = null)
        : base(
            $"The canonical provider runtime snapshot is '{status}' for database profile '{databaseProfileId?.ToString("D") ?? "unknown"}' at generation {databaseProfileGeneration}.",
            innerException)
    {
        Status = status;
        DatabaseProfileId = databaseProfileId;
        DatabaseProfileGeneration = databaseProfileGeneration;
    }

    public ProviderRuntimeProfileSnapshotStatus Status { get; }

    public Guid? DatabaseProfileId { get; }

    public long DatabaseProfileGeneration { get; }
}
