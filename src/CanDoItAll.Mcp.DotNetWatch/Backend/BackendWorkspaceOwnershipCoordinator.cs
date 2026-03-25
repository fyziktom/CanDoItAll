using CanDoItAll.Mcp.DotNetWatch.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class BackendWorkspaceOwnershipCoordinator(
    RuntimeConfiguration configuration,
    BackendIdentityProvider identityProvider,
    BackendRegistrationStore registrationStore,
    GlobalBackendCatalogStore globalCatalogStore,
    ILogger<BackendWorkspaceOwnershipCoordinator> logger)
{
    public async Task<BackendOwnershipDecision> AcquireAsync(CancellationToken cancellationToken)
    {
        var ownershipLockPath = GetOwnershipLockPath();
        var ownershipLockDirectory = Path.GetDirectoryName(ownershipLockPath);
        if (!string.IsNullOrWhiteSpace(ownershipLockDirectory))
        {
            Directory.CreateDirectory(ownershipLockDirectory);
        }

        FileStream? ownershipLock = null;
        try
        {
            ownershipLock = new FileStream(
                ownershipLockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException)
        {
            var existingOwner = await FindPreferredExistingOwnerAsync(cancellationToken);
            await SyncWorkspaceRegistrationAsync(existingOwner, cancellationToken);
            logger.LogInformation(
                "Backend ownership denied because another process already holds the workspace lease. ExistingOwner={BackendId}",
                existingOwner?.BackendId ?? "<pending>");
            return BackendOwnershipDecision.Deny(existingOwner);
        }

        var conflictingOwner = await FindPreferredExistingOwnerAsync(cancellationToken);
        if (conflictingOwner is not null)
        {
            await SyncWorkspaceRegistrationAsync(conflictingOwner, cancellationToken);
            await ownershipLock.DisposeAsync();
            logger.LogWarning(
                "Backend ownership denied because a matching backend is already live. ExistingOwner={BackendId}, ExistingPid={Pid}, CurrentPid={CurrentPid}",
                conflictingOwner.BackendId,
                conflictingOwner.ProcessId,
                Environment.ProcessId);
            return BackendOwnershipDecision.Deny(conflictingOwner);
        }

        return BackendOwnershipDecision.Grant(new BackendWorkspaceOwnershipLease(ownershipLock));
    }

    private async Task<BackendRegistrationRecord?> FindPreferredExistingOwnerAsync(CancellationToken cancellationToken)
    {
        var matchingOwners = await ReadMatchingOwnersAsync(cancellationToken);
        return matchingOwners
            .OrderByDescending(record => identityProvider.MatchesConfiguration(record.Identity))
            .ThenByDescending(static record => record.RegisteredUtc)
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<BackendRegistrationRecord>> ReadMatchingOwnersAsync(CancellationToken cancellationToken)
    {
        var matchingOwners = new Dictionary<string, BackendRegistrationRecord>(StringComparer.OrdinalIgnoreCase);
        var workspaceRegistration = await registrationStore.ReadAsync(cancellationToken);
        if (workspaceRegistration is not null && identityProvider.MatchesOwnerScope(workspaceRegistration.Identity))
        {
            if (registrationStore.IsLiveProcess(workspaceRegistration))
            {
                matchingOwners[workspaceRegistration.BackendId] = workspaceRegistration;
            }
            else
            {
                registrationStore.Delete();
            }
        }

        var staleCatalogIds = new List<string>();
        foreach (var record in await globalCatalogStore.ReadAllAsync(cancellationToken))
        {
            if (!identityProvider.MatchesOwnerScope(record.Identity))
            {
                continue;
            }

            if (!globalCatalogStore.IsLiveProcess(record))
            {
                staleCatalogIds.Add(record.BackendId);
                continue;
            }

            matchingOwners[record.BackendId] = record;
        }

        if (staleCatalogIds.Count > 0)
        {
            await globalCatalogStore.DeleteManyAsync(staleCatalogIds, cancellationToken);
        }

        return matchingOwners.Values
            .Where(record => record.ProcessId != Environment.ProcessId)
            .ToArray();
    }

    private async Task SyncWorkspaceRegistrationAsync(BackendRegistrationRecord? existingOwner, CancellationToken cancellationToken)
    {
        if (existingOwner is null)
        {
            return;
        }

        await registrationStore.WriteAsync(existingOwner, cancellationToken);
    }

    private string GetOwnershipLockPath()
    {
        var identity = identityProvider.Current;
        var key = string.Join(
            "|",
            identity.ServerName,
            identity.WorkspaceRoot,
            identity.SettingsPath);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Path.Combine(configuration.MachineStateRoot, "workspace-locks", $"{Convert.ToHexString(hash)}.lock");
    }
}

internal sealed class BackendWorkspaceOwnershipLease(FileStream stream) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => stream.DisposeAsync();
}

internal sealed record BackendOwnershipDecision(
    BackendWorkspaceOwnershipLease? Lease,
    BackendRegistrationRecord? ExistingOwner)
{
    public bool Acquired => Lease is not null;

    public static BackendOwnershipDecision Grant(BackendWorkspaceOwnershipLease lease)
        => new(lease, ExistingOwner: null);

    public static BackendOwnershipDecision Deny(BackendRegistrationRecord? existingOwner)
        => new(Lease: null, existingOwner);
}
