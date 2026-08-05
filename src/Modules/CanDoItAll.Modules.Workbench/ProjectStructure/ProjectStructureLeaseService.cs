using System.Runtime.ExceptionServices;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureLeaseService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    private static readonly TimeSpan MutationLeaseConflictRetryWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MutationLeaseConflictRetryPadding = TimeSpan.FromMilliseconds(250);

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
        var projectId = await ResolveLeaseProjectIdAsync(
            dbContext,
            request.ScopeKind,
            scopeKey,
            cancellationToken);
        await using var mutationScope = projectId.HasValue
            ? await ProjectStructureSerializableMutationScope.BeginAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId.Value),
                cancellationToken)
            : null;
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
            if (mutationScope is not null)
            {
                await mutationScope.CommitAsync(cancellationToken);
            }

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
        if (mutationScope is not null)
        {
            await mutationScope.CommitAsync(cancellationToken);
        }

        return MapSnapshot(lease, now);
    }

    private static async Task<Guid?> ResolveLeaseProjectIdAsync(
        AppDbContext dbContext,
        ProjectStructureLeaseScopeKind scopeKind,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        if (scopeKind == ProjectStructureLeaseScopeKind.RepoBranch)
        {
            return null;
        }

        if (scopeKind == ProjectStructureLeaseScopeKind.Project)
        {
            return Guid.TryParse(scopeKey, out var projectId)
                ? projectId
                : throw new ProjectStructureAgentException(
                    400,
                    "InvalidProjectScope",
                    "A project lease requires a valid project id scope key.");
        }

        var projectIds = await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .Where(record => record.NodeKey == scopeKey)
            .Select(record => record.ProjectId)
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);
        return projectIds.Count switch
        {
            1 => projectIds[0],
            0 => throw new ProjectStructureAgentException(
                404,
                "ProjectNodeNotFound",
                $"Project node '{scopeKey}' does not exist and cannot be leased."),
            _ => throw new ProjectStructureAgentException(
                409,
                "AmbiguousProjectNodeScope",
                $"Project node scope '{scopeKey}' is not unique across projects.")
        };
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

        var scopeKey = NormalizeScopeKey(request.ScopeKind, request.ScopeKey);
        var projectId = await ResolveLeaseProjectIdAsync(
            dbContext,
            request.ScopeKind,
            scopeKey,
            cancellationToken);
        await using var mutationScope = projectId.HasValue
            ? await ProjectStructureSerializableMutationScope.BeginAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId.Value),
                cancellationToken)
            : null;

        var now = clock.GetUtcNow();
        var lease = await dbContext.Set<ProjectStructureLeaseRecord>()
            .FirstOrDefaultAsync(
                item => item.ScopeKind == request.ScopeKind &&
                        item.ScopeKey == scopeKey &&
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
        if (mutationScope is not null)
        {
            await mutationScope.CommitAsync(cancellationToken);
        }

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
        return await RunWithProjectMutationLeasesAsync(
            [new ProjectStructureProjectMutationLeaseRequest(projectId, leaseToken)],
            agent,
            reason,
            callback,
            cancellationToken);
    }

    internal async Task<T> RunWithProjectMutationLeasesAsync<T>(
        IReadOnlyCollection<ProjectStructureProjectMutationLeaseRequest> requests,
        ProjectStructureAgentContext agent,
        string reason,
        Func<CancellationToken, Task<T>> callback,
        CancellationToken cancellationToken = default)
    {
        var orderedRequests = ProjectStructureProjectMutationLeasePlan.Create(requests);
        var acquiredLeases = new List<ProjectStructureLeaseSnapshot>();
        Exception? operationFailure = null;
        T result = default!;

        try
        {
            foreach (var request in orderedRequests)
            {
                var scopeKey = NormalizeScopeKey(
                    ProjectStructureLeaseScopeKind.Project,
                    request.ProjectId.ToString("D"));
                if (!string.IsNullOrWhiteSpace(request.LeaseToken))
                {
                    await ValidateOwnedLeaseAsync(
                        ProjectStructureLeaseScopeKind.Project,
                        scopeKey,
                        request.LeaseToken,
                        agent,
                        cancellationToken);
                    continue;
                }

                var activeLease = await GetActiveLeaseAsync(
                    ProjectStructureLeaseScopeKind.Project,
                    scopeKey,
                    cancellationToken);
                if (activeLease is not null && IsSameOwner(activeLease, agent))
                {
                    continue;
                }

                var acquiredLease = await AcquireMutationLeaseWithShortConflictRetryAsync(
                    scopeKey,
                    agent,
                    reason,
                    cancellationToken);
                acquiredLeases.Add(acquiredLease);
            }

            result = await callback(cancellationToken);
        }
        catch (Exception exception)
        {
            operationFailure = exception;
        }

        await ProjectStructureMutationLeaseCleanup.CompleteAsync(
            acquiredLeases,
            acquiredLease => ReleaseAsync(
                new ProjectStructureLeaseReleaseRequest(
                    ProjectStructureLeaseScopeKind.Project,
                    acquiredLease.ScopeKey,
                    acquiredLease.LeaseToken),
                agent,
                CancellationToken.None),
            operationFailure);

        return result;
    }

    private async Task<ProjectStructureLeaseSnapshot> AcquireMutationLeaseWithShortConflictRetryAsync(
        string scopeKey,
        ProjectStructureAgentContext agent,
        string reason,
        CancellationToken cancellationToken)
    {
        var request = new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, scopeKey, reason, 5);

        try
        {
            return await AcquireAsync(request, agent, cancellationToken);
        }
        catch (ProjectStructureLeaseConflictException exception) when (ShouldRetryShortMutationLeaseConflict(exception.Conflict))
        {
            var delay = exception.Conflict.ExpiresAtUtc - clock.GetUtcNow() + MutationLeaseConflictRetryPadding;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return await AcquireAsync(request, agent, cancellationToken);
        }
    }

    private bool ShouldRetryShortMutationLeaseConflict(ProjectStructureLeaseConflict conflict)
    {
        return conflict.ExpiresAtUtc - clock.GetUtcNow() <= MutationLeaseConflictRetryWindow;
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
        return await dbContext.Set<ProjectStructureLeaseRecord>()
            .Where(item => item.ScopeKind == scopeKind &&
                           item.ScopeKey == scopeKey &&
                           item.ReleasedAtUtc == null &&
                           item.ExpiresAtUtc > now)
            .OrderByDescending(item => item.RenewedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

internal static class ProjectStructureMutationLeaseCleanup
{
    public static async Task CompleteAsync(
        IReadOnlyList<ProjectStructureLeaseSnapshot> acquiredLeases,
        Func<ProjectStructureLeaseSnapshot, Task> releaseAsync,
        Exception? operationFailure)
    {
        ArgumentNullException.ThrowIfNull(acquiredLeases);
        ArgumentNullException.ThrowIfNull(releaseAsync);

        var releaseFailures = new List<Exception>();
        for (var index = acquiredLeases.Count - 1; index >= 0; index--)
        {
            try
            {
                await releaseAsync(acquiredLeases[index]);
            }
            catch (Exception exception)
            {
                releaseFailures.Add(exception);
            }
        }

        if (releaseFailures.Count > 0)
        {
            var failures = operationFailure is null
                ? releaseFailures
                : [operationFailure, .. releaseFailures];
            throw new AggregateException(
                "One or more project mutation leases could not be released.",
                failures);
        }

        if (operationFailure is not null)
        {
            ExceptionDispatchInfo.Capture(operationFailure).Throw();
        }
    }
}

internal sealed record ProjectStructureProjectMutationLeaseRequest(
    Guid ProjectId,
    string? LeaseToken = null);

internal static class ProjectStructureProjectMutationLeasePlan
{
    public static IReadOnlyList<ProjectStructureProjectMutationLeaseRequest> Create(
        IReadOnlyCollection<ProjectStructureProjectMutationLeaseRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            throw new ArgumentException("At least one project mutation lease is required.", nameof(requests));
        }

        if (requests.Any(request => request.ProjectId == Guid.Empty))
        {
            throw new ArgumentException("Project mutation leases require non-empty project ids.", nameof(requests));
        }

        return requests
            .Select(request => request with { LeaseToken = request.LeaseToken?.Trim() })
            .GroupBy(request => request.ProjectId)
            .Select(ResolveSingleRequest)
            .OrderBy(request => request.ProjectId)
            .ToList();
    }

    private static ProjectStructureProjectMutationLeaseRequest ResolveSingleRequest(
        IGrouping<Guid, ProjectStructureProjectMutationLeaseRequest> requests)
    {
        var leaseTokens = requests
            .Select(request => request.LeaseToken)
            .Where(leaseToken => !string.IsNullOrWhiteSpace(leaseToken))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (leaseTokens.Count > 1)
        {
            throw new ArgumentException(
                $"Project '{requests.Key:D}' has conflicting mutation lease tokens.",
                nameof(requests));
        }

        return new ProjectStructureProjectMutationLeaseRequest(
            requests.Key,
            leaseTokens.SingleOrDefault());
    }
}
