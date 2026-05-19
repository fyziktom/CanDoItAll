using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryDistributedComputeCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryDistributedComputeCoordinator
{
    public async ValueTask<CognitiveMemoryDistributedWorkerRecord> RegisterWorkerAsync(
        string workerId,
        string machineName,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        CancellationToken cancellationToken = default)
    {
        if (capabilities.Count == 0)
        {
            throw new ArgumentException("Distributed workers must declare at least one capability.", nameof(capabilities));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedWorkerId = CognitiveMemoryGuard.EnsureText(workerId, nameof(workerId));
        var now = clock.GetUtcNow();
        var worker = await dbContext.Set<CognitiveMemoryDistributedWorkerRecord>()
            .SingleOrDefaultAsync(item => item.WorkerId == normalizedWorkerId, cancellationToken);
        if (worker is null)
        {
            worker = new CognitiveMemoryDistributedWorkerRecord
            {
                WorkerId = normalizedWorkerId
            };
            worker.CreatedOrUpdated(machineName, capabilities, now);
            dbContext.Add(worker);
        }
        else
        {
            worker.CreatedOrUpdated(machineName, capabilities, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return worker;
    }

    public async ValueTask<CognitiveMemoryDistributedJobRecord> EnqueueAsync(
        CognitiveMemoryDistributedJobEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inputHash = CognitiveMemoryHash.FromUtf8(request.InputPayloadJson);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == request.ProjectId &&
                        item.JobKind == request.JobKind &&
                        item.InputHash == inputHash.Value,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = clock.GetUtcNow();
        var job = new CognitiveMemoryDistributedJobRecord
        {
            ProjectId = request.ProjectId,
            JobKind = request.JobKind,
            State = CognitiveMemoryDistributedJobState.Queued,
            SourceScopeKey = CognitiveMemoryGuard.EnsureText(request.SourceScopeKey, nameof(request.SourceScopeKey)),
            InputPayloadJson = CognitiveMemoryGuard.EnsureText(request.InputPayloadJson, nameof(request.InputPayloadJson)),
            InputHashAlgorithm = inputHash.Algorithm,
            InputHash = inputHash.Value,
            ExpectedOutputSchema = CognitiveMemoryGuard.EnsureText(request.ExpectedOutputSchema, nameof(request.ExpectedOutputSchema)),
            AlgorithmVersion = CognitiveMemoryGuard.EnsureText(request.AlgorithmVersion, nameof(request.AlgorithmVersion)),
            PolicyProfileId = CognitiveMemoryGuard.EnsureText(request.PolicyProfileId, nameof(request.PolicyProfileId)),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async ValueTask<CognitiveMemoryDistributedLeaseClaim?> ClaimAsync(
        string workerId,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedWorkerId = CognitiveMemoryGuard.EnsureText(workerId, nameof(workerId));
        var now = clock.GetUtcNow();
        var job = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .Where(item => capabilities.Contains(item.JobKind) &&
                           (item.State == CognitiveMemoryDistributedJobState.Queued ||
                            item.State == CognitiveMemoryDistributedJobState.Leased && item.LeaseExpiresAtUtc < now))
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return null;
        }

        job.State = CognitiveMemoryDistributedJobState.Leased;
        job.LeasedWorkerId = normalizedWorkerId;
        job.LeaseToken = Guid.NewGuid().ToString("N");
        job.LeaseExpiresAtUtc = now.Add(leaseDuration);
        job.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryDistributedLeaseClaim(
            job.Id,
            job.LeaseToken,
            job.LeaseExpiresAtUtc.Value,
            job.InputPayloadJson,
            job.InputHash);
    }

    public async ValueTask<CognitiveMemoryDistributedWorkerResultRecord> SubmitResultAsync(
        Guid jobId,
        string workerId,
        string leaseToken,
        string inputHash,
        string outputPayloadJson,
        string algorithmVersion,
        string outputSchema,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var job = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Distributed job '{jobId:D}' was not found.");
        var now = clock.GetUtcNow();
        var outputHash = CognitiveMemoryHash.FromUtf8(outputPayloadJson);
        var result = new CognitiveMemoryDistributedWorkerResultRecord
        {
            DistributedJobId = job.Id,
            ProjectId = job.ProjectId,
            WorkerId = CognitiveMemoryGuard.EnsureText(workerId, nameof(workerId)),
            InputHash = inputHash.Trim().ToLowerInvariant(),
            OutputHash = outputHash.Value,
            AlgorithmVersion = algorithmVersion.Trim(),
            OutputSchema = outputSchema.Trim(),
            OutputPayloadJson = outputPayloadJson.Trim(),
            SubmittedAtUtc = now
        };
        var rejection = ValidateDistributedResult(job, result, leaseToken, now);
        if (rejection is null)
        {
            result.Status = CognitiveMemoryDistributedResultStatus.Accepted;
            result.AcceptedAtUtc = now;
            job.State = CognitiveMemoryDistributedJobState.Completed;
            job.UpdatedAtUtc = now;
        }
        else
        {
            result.Status = CognitiveMemoryDistributedResultStatus.Rejected;
            result.RejectionReason = rejection;
            job.State = CognitiveMemoryDistributedJobState.Rejected;
            job.UpdatedAtUtc = now;
        }

        dbContext.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static string? ValidateDistributedResult(
        CognitiveMemoryDistributedJobRecord job,
        CognitiveMemoryDistributedWorkerResultRecord result,
        string leaseToken,
        DateTimeOffset now)
    {
        if (job.State != CognitiveMemoryDistributedJobState.Leased)
        {
            return "Job is not leased.";
        }

        if (job.LeaseExpiresAtUtc is null || job.LeaseExpiresAtUtc <= now)
        {
            return "Lease expired.";
        }

        if (!string.Equals(job.LeaseToken, leaseToken, StringComparison.Ordinal))
        {
            return "Lease token mismatch.";
        }

        if (!string.Equals(job.LeasedWorkerId, result.WorkerId, StringComparison.Ordinal))
        {
            return "Worker id mismatch.";
        }

        if (!string.Equals(job.InputHash, result.InputHash, StringComparison.OrdinalIgnoreCase))
        {
            return "Input hash mismatch.";
        }

        if (!string.Equals(job.AlgorithmVersion, result.AlgorithmVersion, StringComparison.Ordinal))
        {
            return "Algorithm version mismatch.";
        }

        return string.Equals(job.ExpectedOutputSchema, result.OutputSchema, StringComparison.Ordinal)
            ? null
            : "Output schema mismatch.";
    }
}

