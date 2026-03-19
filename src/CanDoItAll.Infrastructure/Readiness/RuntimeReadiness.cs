using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CanDoItAll.Infrastructure.Readiness;

public sealed record RuntimeReadinessSnapshot(
    bool IsReady,
    string EnvironmentName,
    string? Summary,
    int? WatchIteration,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastChangedAtUtc,
    IReadOnlyList<string> ActiveUrls);

public interface IRuntimeReadinessService
{
    RuntimeReadinessSnapshot GetSnapshot();

    void MarkStarting(string environmentName, IEnumerable<string>? urls = null);

    void MarkReady(string environmentName, int? watchIteration = null, IEnumerable<string>? urls = null);

    void MarkFaulted(string environmentName, string summary, int? watchIteration = null, IEnumerable<string>? urls = null);
}

public sealed class RuntimeReadinessService : IRuntimeReadinessService
{
    private readonly object _gate = new();
    private RuntimeReadinessSnapshot _snapshot = new(
        false,
        "Unknown",
        "Starting",
        null,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        []);

    public RuntimeReadinessSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public void MarkStarting(string environmentName, IEnumerable<string>? urls = null)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                IsReady = false,
                EnvironmentName = environmentName,
                Summary = "Starting",
                LastChangedAtUtc = DateTimeOffset.UtcNow,
                ActiveUrls = urls?.ToArray() ?? []
            };
        }
    }

    public void MarkReady(string environmentName, int? watchIteration = null, IEnumerable<string>? urls = null)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                IsReady = true,
                EnvironmentName = environmentName,
                Summary = "Ready",
                WatchIteration = watchIteration,
                LastChangedAtUtc = DateTimeOffset.UtcNow,
                ActiveUrls = urls?.ToArray() ?? []
            };
        }
    }

    public void MarkFaulted(string environmentName, string summary, int? watchIteration = null, IEnumerable<string>? urls = null)
    {
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                IsReady = false,
                EnvironmentName = environmentName,
                Summary = summary,
                WatchIteration = watchIteration,
                LastChangedAtUtc = DateTimeOffset.UtcNow,
                ActiveUrls = urls?.ToArray() ?? []
            };
        }
    }
}

public sealed class RuntimeReadinessHealthCheck(IRuntimeReadinessService readinessService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = readinessService.GetSnapshot();
        return Task.FromResult(snapshot.IsReady
            ? HealthCheckResult.Healthy(snapshot.Summary)
            : HealthCheckResult.Degraded(snapshot.Summary));
    }
}
