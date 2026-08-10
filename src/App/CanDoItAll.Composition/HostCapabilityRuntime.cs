using CanDoItAll.FileTools.Desktop;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Composition;

public sealed class HostCapabilitySnapshotService : IHostCapabilitySnapshotProvider
{
    private readonly ResolvedRuntimeHostProfile profile;
    private readonly IPathFoundationReadinessProbe pathFoundationReadinessProbe;
    private readonly ISecretVaultCapabilityState secretVaultCapabilityState;
    private readonly IDesktopFileLauncher desktopFileLauncher;
    private readonly TimeProvider timeProvider;
    private readonly string? infrastructureImplementationVersion;
    private readonly string? securityImplementationVersion;
    private readonly string? fileToolsImplementationVersion;

    public HostCapabilitySnapshotService(
        ResolvedRuntimeHostProfile profile,
        IPathFoundationReadinessProbe pathFoundationReadinessProbe,
        ISecretVaultCapabilityState secretVaultCapabilityState,
        IDesktopFileLauncher desktopFileLauncher,
        TimeProvider timeProvider)
    {
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.pathFoundationReadinessProbe = pathFoundationReadinessProbe ??
            throw new ArgumentNullException(nameof(pathFoundationReadinessProbe));
        this.secretVaultCapabilityState = secretVaultCapabilityState ??
            throw new ArgumentNullException(nameof(secretVaultCapabilityState));
        this.desktopFileLauncher = desktopFileLauncher ??
            throw new ArgumentNullException(nameof(desktopFileLauncher));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        infrastructureImplementationVersion = typeof(IPathFoundationReadinessProbe).Assembly.GetName().Version?.ToString();
        securityImplementationVersion = typeof(ISecretVault).Assembly.GetName().Version?.ToString();
        fileToolsImplementationVersion = typeof(IDesktopFileLauncher).Assembly.GetName().Version?.ToString();
    }

    public HostCapabilitySnapshot GetSnapshot()
        => HostCapabilitySnapshotProjector.Create(
            profile,
            pathFoundationReadinessProbe.Probe(),
            secretVaultCapabilityState.Current,
            infrastructureImplementationVersion,
            securityImplementationVersion,
            desktopFileLauncher.IsAvailable,
            fileToolsImplementationVersion,
            timeProvider.GetUtcNow());
}

public sealed class HostCapabilityUnavailableException(HostCapabilitySnapshot snapshot)
    : InvalidOperationException(BuildMessage(snapshot))
{
    public HostCapabilitySnapshot Snapshot { get; } = snapshot;

    private static string BuildMessage(HostCapabilitySnapshot snapshot)
    {
        string failures = string.Join(
            "; ",
            snapshot.Capabilities
                .Where(capability =>
                    capability.Criticality == HostCapabilityCriticality.Mandatory &&
                    capability.Availability != HostCapabilityAvailability.Available)
                .Select(capability =>
                    $"{capability.Id} ({capability.ReasonCode}): {capability.Remediation}"));
        return $"Runtime host profile '{snapshot.Profile}' is not ready. {failures}".Trim();
    }
}

public sealed class HostCapabilityStartupValidator(
    IHostCapabilitySnapshotProvider snapshotProvider,
    ILogger<HostCapabilityStartupValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostCapabilitySnapshot snapshot = snapshotProvider.GetSnapshot();
        if (!snapshot.IsReady)
        {
            throw new HostCapabilityUnavailableException(snapshot);
        }

        HostCapabilityId[] unavailableOptionalCapabilities = snapshot.Capabilities
            .Where(capability =>
                capability.Criticality == HostCapabilityCriticality.Optional &&
                capability.Availability != HostCapabilityAvailability.Available)
            .Select(capability => capability.Id)
            .ToArray();
        logger.LogInformation(
            "Runtime host profile {Profile} is ready. OptionalUnavailable={OptionalUnavailable}.",
            snapshot.Profile,
            unavailableOptionalCapabilities);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class HostCapabilityHealthCheck(
    IHostCapabilitySnapshotProvider snapshotProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        HostCapabilitySnapshot snapshot = snapshotProvider.GetSnapshot();
        return Task.FromResult(snapshot.IsReady
            ? HealthCheckResult.Healthy($"Runtime host profile '{snapshot.Profile}' is ready.")
            : HealthCheckResult.Unhealthy($"Runtime host profile '{snapshot.Profile}' has unavailable mandatory capabilities."));
    }
}
