using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime.Coordination;

public sealed record EndpointLease(string LeaseId, string OwnerId, int HttpPort, DateTimeOffset AcquiredUtc);

public sealed class RuntimeEndpointAllocator(RuntimeConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly Dictionary<string, EndpointLease> _leases = new(StringComparer.OrdinalIgnoreCase);

    public EndpointLease Acquire(string ownerId)
    {
        lock (_gate)
        {
            LoadLeasesIfNeeded();
            var existing = _leases.Values.FirstOrDefault(lease => string.Equals(lease.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return existing;
            }

            var usedPorts = _leases.Values.Select(lease => lease.HttpPort).ToHashSet();
            for (var port = configuration.CandidateHttpPortStart; port <= configuration.CandidateHttpPortEnd; port++)
            {
                if (usedPorts.Contains(port) || !IsPortAvailable(port))
                {
                    continue;
                }

                var lease = new EndpointLease($"lease_{Guid.NewGuid():N}", ownerId, port, DateTimeOffset.UtcNow);
                _leases[lease.LeaseId] = lease;
                Persist();
                return lease;
            }
        }

        throw new ToolInvocationException(
            "ResourceConflict",
            $"No candidate HTTP ports are available in the configured range {configuration.CandidateHttpPortStart}-{configuration.CandidateHttpPortEnd}.",
            new
            {
                configuration.CandidateHttpPortStart,
                configuration.CandidateHttpPortEnd
            });
    }

    public void Release(string? leaseId)
    {
        if (string.IsNullOrWhiteSpace(leaseId))
        {
            return;
        }

        lock (_gate)
        {
            LoadLeasesIfNeeded();
            if (_leases.Remove(leaseId))
            {
                Persist();
            }
        }
    }

    private void LoadLeasesIfNeeded()
    {
        if (_leases.Count > 0 || !File.Exists(configuration.EndpointLeasePath))
        {
            return;
        }

        var payload = File.ReadAllText(configuration.EndpointLeasePath);
        var leases = JsonSerializer.Deserialize<List<EndpointLease>>(payload, JsonOptions) ?? [];
        foreach (var lease in leases)
        {
            _leases[lease.LeaseId] = lease;
        }
    }

    private void Persist()
    {
        var payload = JsonSerializer.Serialize(_leases.Values.OrderBy(static lease => lease.HttpPort).ToArray(), JsonOptions);
        File.WriteAllText(configuration.EndpointLeasePath, payload);
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
