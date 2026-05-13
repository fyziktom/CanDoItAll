using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureLeaseService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<ProjectStructureLeaseSnapshot> AcquireAsync(
        ProjectStructureLeaseAcquireRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(request.ScopeKind, request.ScopeKey);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var now = clock.GetUtcNow();
        var scopeKey = NormalizeScopeKey(request.ScopeKind, request.ScopeKey);
        var durationMinutes = Math.Clamp(request.DurationMinutes, 1, 120);
        var activeLease = await FindActiveLeaseAsync(dbContext, request.ScopeKind, scopeKey, now, cancellationToken);

        if (activeLease is not null)
        {
            if (!IsSameOwner(activeLease, agent))
            {
                throw new ProjectStructureLeaseConflictException(MapConflict(activeLease));
            }

            activeLease.Reason = NormalizeReason(request.Reason);
            activeLease.RenewedAtUtc = now;
            activeLease.ExpiresAtUtc = now.AddMinutes(durationMinutes);
            await dbContext.SaveChangesAsync(cancellationToken);
            return MapSnapshot(activeLease, now);
        }

        var lease = new ProjectStructureLeaseRecord
        {
            ScopeKind = request.ScopeKind,
            ScopeKey = scopeKey,
            LeaseToken = Guid.NewGuid().ToString("N"),
            AgentId = NormalizeAgentValue(agent.AgentId, "anonymous-agent"),
            AgentName = NormalizeAgentValue(agent.AgentName, "Anonymous agent"),
            MachineName = NormalizeAgentValue(agent.MachineName, "unknown-machine"),
            RepositoryRoot = NormalizeAgentValue(agent.RepositoryRoot, string.Empty),
            BranchName = NormalizeAgentValue(agent.BranchName, string.Empty),
            Reason = NormalizeReason(request.Reason),
            AcquiredAtUtc = now,
            RenewedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(durationMinutes)
        };

        await dbContext.Set<ProjectStructureLeaseRecord>().AddAsync(lease, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSnapshot(lease, now);
    }

    public async Task<ProjectStructureLeaseSnapshot?> GetActiveLeaseAsync(
        ProjectStructureLeaseScopeKind scopeKind,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(scopeKind, scopeKey);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var now = clock.GetUtcNow();
        var lease = await FindActiveLeaseAsync(dbContext, scopeKind, NormalizeScopeKey(scopeKind, scopeKey), now, cancellationToken);
        return lease is null ? null : MapSnapshot(lease, now);
    }

    public async Task<ProjectStructureLeaseSnapshot> RenewAsync(
        ProjectStructureLeaseRenewRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(request.ScopeKind, request.ScopeKey);
        if (string.IsNullOrWhiteSpace(request.LeaseToken))
        {
            throw new ProjectStructureAgentException(400, "LeaseTokenRequired", "A lease token is required to renew a lease.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var now = clock.GetUtcNow();
        var lease = await dbContext.Set<ProjectStructureLeaseRecord>()
            .FirstOrDefaultAsync(
                item => item.ScopeKind == request.ScopeKind &&
                        item.ScopeKey == NormalizeScopeKey(request.ScopeKind, request.ScopeKey) &&
                        item.LeaseToken == request.LeaseToken.Trim() &&
                        item.ReleasedAtUtc == null,
                cancellationToken);

        if (lease is null || lease.ExpiresAtUtc <= now)
        {
            throw new ProjectStructureAgentException(404, "LeaseNotFound", "The requested lease is not active and cannot be renewed.");
        }

        if (!IsSameOwner(lease, agent))
        {
            throw new ProjectStructureLeaseConflictException(MapConflict(lease));
        }

        lease.RenewedAtUtc = now;
        lease.ExpiresAtUtc = now.AddMinutes(Math.Clamp(request.DurationMinutes, 1, 120));
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSnapshot(lease, now);
    }

    public async Task<ProjectStructureLeaseSnapshot?> ReleaseAsync(
        ProjectStructureLeaseReleaseRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(request.ScopeKind, request.ScopeKey);
        if (string.IsNullOrWhiteSpace(request.LeaseToken))
        {
            throw new ProjectStructureAgentException(400, "LeaseTokenRequired", "A lease token is required to release a lease.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var scopeKey = NormalizeScopeKey(request.ScopeKind, request.ScopeKey);
        var now = clock.GetUtcNow();
        var lease = await dbContext.Set<ProjectStructureLeaseRecord>()
            .FirstOrDefaultAsync(
                item => item.ScopeKind == request.ScopeKind &&
                        item.ScopeKey == scopeKey &&
                        item.LeaseToken == request.LeaseToken.Trim() &&
                        item.ReleasedAtUtc == null,
                cancellationToken);

        if (lease is null)
        {
            return null;
        }

        if (!IsSameOwner(lease, agent))
        {
            throw new ProjectStructureLeaseConflictException(MapConflict(lease));
        }

        lease.ReleasedAtUtc = now;
        lease.ExpiresAtUtc = now;
        lease.RenewedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSnapshot(lease, now);
    }

    public async Task<ProjectStructureLeaseSnapshot?> ValidateOwnedLeaseAsync(
        ProjectStructureLeaseScopeKind scopeKind,
        string scopeKey,
        string? leaseToken,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(scopeKind, scopeKey);
        if (string.IsNullOrWhiteSpace(leaseToken))
        {
            throw new ProjectStructureAgentException(400, "LeaseTokenRequired", "A lease token is required for this mutation.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var now = clock.GetUtcNow();
        var normalizedScopeKey = NormalizeScopeKey(scopeKind, scopeKey);
        var activeLease = await FindActiveLeaseAsync(dbContext, scopeKind, normalizedScopeKey, now, cancellationToken);
        if (activeLease is null)
        {
            throw new ProjectStructureAgentException(409, "LeaseMissing", $"Scope '{normalizedScopeKey}' is not currently leased.");
        }

        if (!string.Equals(activeLease.LeaseToken, leaseToken.Trim(), StringComparison.Ordinal) ||
            !IsSameOwner(activeLease, agent))
        {
            throw new ProjectStructureLeaseConflictException(MapConflict(activeLease));
        }

        return MapSnapshot(activeLease, now);
    }

    public async Task<T> RunWithProjectMutationLeaseAsync<T>(
        Guid projectId,
        string? leaseToken,
        ProjectStructureAgentContext agent,
        string reason,
        Func<CancellationToken, Task<T>> callback,
        CancellationToken cancellationToken = default)
    {
        var scopeKey = NormalizeScopeKey(ProjectStructureLeaseScopeKind.Project, projectId.ToString());
        if (!string.IsNullOrWhiteSpace(leaseToken))
        {
            await ValidateOwnedLeaseAsync(ProjectStructureLeaseScopeKind.Project, scopeKey, leaseToken, agent, cancellationToken);
            return await callback(cancellationToken);
        }

        var activeLease = await GetActiveLeaseAsync(ProjectStructureLeaseScopeKind.Project, scopeKey, cancellationToken);
        if (activeLease is not null && IsSameOwner(activeLease, agent))
        {
            return await callback(cancellationToken);
        }

        var acquiredLease = await AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, scopeKey, reason, 5),
            agent,
            cancellationToken);

        try
        {
            return await callback(cancellationToken);
        }
        finally
        {
            await ReleaseAsync(
                new ProjectStructureLeaseReleaseRequest(ProjectStructureLeaseScopeKind.Project, scopeKey, acquiredLease.LeaseToken),
                agent,
                cancellationToken);
        }
    }

    private static void ValidateScope(ProjectStructureLeaseScopeKind scopeKind, string scopeKey)
    {
        if (!Enum.IsDefined(scopeKind))
        {
            throw new ProjectStructureAgentException(400, "InvalidScopeKind", $"Scope kind '{scopeKind}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            throw new ProjectStructureAgentException(400, "ScopeKeyRequired", "A non-empty scope key is required.");
        }
    }

    private static string NormalizeScopeKey(ProjectStructureLeaseScopeKind scopeKind, string scopeKey)
    {
        var trimmed = scopeKey.Trim();
        return scopeKind switch
        {
            ProjectStructureLeaseScopeKind.Project or ProjectStructureLeaseScopeKind.ProjectNode
                => trimmed.ToLowerInvariant(),
            ProjectStructureLeaseScopeKind.RepoBranch
                => trimmed.Replace('\\', '/').ToLowerInvariant(),
            _ => trimmed
        };
    }

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Project structure mutation";
        }

        return reason.Trim();
    }

    private static string NormalizeAgentValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim();
    }

    private static bool IsSameOwner(ProjectStructureLeaseRecord lease, ProjectStructureAgentContext agent)
    {
        return string.Equals(lease.AgentId, NormalizeAgentValue(agent.AgentId, "anonymous-agent"), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(lease.MachineName, NormalizeAgentValue(agent.MachineName, "unknown-machine"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOwner(ProjectStructureLeaseSnapshot lease, ProjectStructureAgentContext agent)
    {
        return string.Equals(lease.AgentId, NormalizeAgentValue(agent.AgentId, "anonymous-agent"), StringComparison.OrdinalIgnoreCase) &&
               string.Equals(lease.MachineName, NormalizeAgentValue(agent.MachineName, "unknown-machine"), StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectStructureLeaseSnapshot MapSnapshot(ProjectStructureLeaseRecord lease, DateTimeOffset now)
    {
        return new ProjectStructureLeaseSnapshot(
            lease.ScopeKind,
            lease.ScopeKey,
            lease.LeaseToken,
            lease.AgentId,
            lease.AgentName,
            lease.MachineName,
            lease.RepositoryRoot,
            lease.BranchName,
            lease.Reason,
            lease.AcquiredAtUtc,
            lease.RenewedAtUtc,
            lease.ExpiresAtUtc,
            lease.ReleasedAtUtc is null && lease.ExpiresAtUtc > now);
    }

    private static ProjectStructureLeaseConflict MapConflict(ProjectStructureLeaseRecord lease)
    {
        return new ProjectStructureLeaseConflict(
            lease.ScopeKind,
            lease.ScopeKey,
            lease.AgentId,
            lease.AgentName,
            lease.MachineName,
            lease.RepositoryRoot,
            lease.BranchName,
            lease.Reason,
            lease.AcquiredAtUtc,
            lease.RenewedAtUtc,
            lease.ExpiresAtUtc);
    }

    private static async Task<ProjectStructureLeaseRecord?> FindActiveLeaseAsync(
        AppDbContext dbContext,
        ProjectStructureLeaseScopeKind scopeKind,
        string scopeKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlite())
        {
            var leases = await dbContext.Set<ProjectStructureLeaseRecord>()
                .Where(item => item.ScopeKind == scopeKind &&
                               item.ScopeKey == scopeKey &&
                               item.ReleasedAtUtc == null)
                .ToListAsync(cancellationToken);

            return leases
                .Where(item => item.ExpiresAtUtc > now)
                .OrderByDescending(item => item.RenewedAtUtc)
                .FirstOrDefault();
        }

        return await dbContext.Set<ProjectStructureLeaseRecord>()
            .Where(item => item.ScopeKind == scopeKind &&
                           item.ScopeKey == scopeKey &&
                           item.ReleasedAtUtc == null &&
                           item.ExpiresAtUtc > now)
            .OrderByDescending(item => item.RenewedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
