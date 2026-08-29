using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderInvocationAuditService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    SharedProviderHistoryProjection history)
{
    public async Task<Guid> BeginAsync(
        SharedProviderInvocationStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        var existing = await dbContext.Set<SharedProviderInvocationRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.RequestId == request.RequestId, cancellationToken);
        if (existing is not null)
        {
            EnsureSameStart(existing, request);
            return existing.Id;
        }

        var publicationOwnerId = await dbContext.Set<ProviderSharePublication>()
            .AsNoTracking()
            .Where(publication => publication.PublicId == request.PublicationId)
            .Select(publication => (Guid?)publication.ProviderProfileId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Shared-provider publication '{request.PublicationId}' was not found.");
        if (publicationOwnerId != request.ProviderProfileId)
        {
            throw new ArgumentException(
                "The invocation provider profile does not own the shared-provider publication.",
                nameof(request));
        }

        var startedAt = HistoryStorageTimestamp.Normalize(clock.GetUtcNow());
        var retainUntil = await history.ResolveRetentionAsync(dbContext, startedAt, request.RetainUntilUtc, cancellationToken);
        var record = SharedProviderInvocationTransitions.Create(
            request.RequestId,
            request.PublicationId,
            request.ProviderProfileId,
            request.AuthenticatedSubject,
            request.AccessContextReference,
            request.TraceId,
            request.CorrelationId,
            request.Operation,
            request.PublicModelId,
            request.UpstreamModelId,
            startedAt,
            retainUntil);
        record.PricingSnapshot = request.PricingSnapshot;
        record.CallerIdentity = request.CallerIdentity;
        var provider = await dbContext.Set<ProviderProfile>().AsNoTracking()
            .Where(item => item.Id == request.ProviderProfileId)
            .Select(item => new { item.Name, item.ProviderKind }).SingleAsync(cancellationToken);
        record.ProviderNameSnapshot = provider.Name;
        record.ProviderKindSnapshot = provider.ProviderKind;
        dbContext.Add(record);
        await history.StageAsync(dbContext, record, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) {
                await transaction.CommitAsync(cancellationToken);
            }
            return record.Id;
        }
        catch (DbUpdateException exception) when (exception is not DbUpdateConcurrencyException)
        {
            await using var verification = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var winner = await verification.Set<SharedProviderInvocationRecord>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.RequestId == request.RequestId, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            EnsureSameStart(winner, request);
            return winner.Id;
        }
    }

    public async Task FinalizeAsync(
        string requestId,
        SharedProviderInvocationCompletion completion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(completion);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        var record = await dbContext.Set<SharedProviderInvocationRecord>()
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Shared-provider invocation '{requestId}' was not found.");
        completion = completion with { CompletedAtUtc = HistoryStorageTimestamp.Normalize(completion.CompletedAtUtc) };
        var previousVersion = record.HistoryVersion;
        SharedProviderInvocationTransitions.Finalize(record, completion);
        if (record.HistoryVersion == previousVersion) {
            return;
        }
        await history.StageAsync(dbContext, record, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await using var verification = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var committed = await verification.Set<SharedProviderInvocationRecord>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
            if (committed is not null && CompletionMatches(committed, completion))
            {
                return;
            }

            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderInvocationRecord),
                record.Id,
                exception);
        }
    }

    private static void EnsureSameStart(
        SharedProviderInvocationRecord existing,
        SharedProviderInvocationStartRequest request)
    {
        if (existing.PublicationId != request.PublicationId ||
            existing.ProviderProfileId != request.ProviderProfileId ||
            !string.Equals(existing.AuthenticatedSubject, request.AuthenticatedSubject, StringComparison.Ordinal) ||
            existing.AccessContextReference != request.AccessContextReference ||
            !string.Equals(existing.TraceId, request.TraceId, StringComparison.Ordinal) ||
            !string.Equals(existing.CorrelationId, request.CorrelationId, StringComparison.Ordinal) ||
            existing.Operation != request.Operation ||
            existing.PublicModelId != request.PublicModelId ||
            !string.Equals(existing.UpstreamModelId, request.UpstreamModelId, StringComparison.Ordinal) ||
            request.RetainUntilUtc is { } requestedExpiry && existing.DeleteAfterUtc != HistoryStorageTimestamp.Normalize(requestedExpiry) ||
            existing.PricingSnapshot != request.PricingSnapshot || existing.CallerIdentity != request.CallerIdentity)
        {
            throw new InvalidOperationException(
                $"Shared-provider invocation '{request.RequestId}' already exists with different metadata.");
        }
    }

    private static bool CompletionMatches(
        SharedProviderInvocationRecord record,
        SharedProviderInvocationCompletion completion)
        => record.Outcome == completion.Outcome &&
            record.CompletedAtUtc == completion.CompletedAtUtc &&
            record.FailureCategory == completion.FailureCategory &&
            record.InputTokenCount == completion.InputTokenCount &&
            record.OutputTokenCount == completion.OutputTokenCount &&
            record.ImageCount == completion.ImageCount &&
            record.UsageCompleteness == completion.UsageCompleteness &&
            record.Price == completion.Price &&
            record.PricingCompleteness == completion.PricingCompleteness &&
            record.PriceEvidence == completion.PriceEvidence &&
            record.CachedInputTokenCount == completion.CachedInputTokenCount &&
            record.CacheWriteTokenCount == completion.CacheWriteTokenCount &&
            record.ReasoningTokenCount == completion.ReasoningTokenCount;
}
