using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class DatabaseProviderMutationVerification(IDbContextFactory<AppDbContext> factory) : IProviderMutationVerification {
    public async Task<ProviderMutationVerification> VerifyAsync(
        ProviderMutationAttempt attempt, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(attempt);
        if (attempt.ProviderId == Guid.Empty || attempt.AttemptId == Guid.Empty || !Enum.IsDefined(attempt.Kind)) {
            throw new ArgumentException("The mutation receipt is invalid.", nameof(attempt));
        }
        try {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var row = await db.Set<ProviderProfile>().AsNoTracking()
                .Where(provider => provider.Id == attempt.ProviderId)
                .Select(provider => new { provider.ConcurrencyToken })
                .SingleOrDefaultAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = attempt.Kind switch {
                ProviderMutationKind.Create => row is null
                    ? ProviderVerificationDisposition.DefinitelyNotCommitted : ProviderVerificationDisposition.Committed,
                ProviderMutationKind.Delete when row is null => ProviderVerificationDisposition.Committed,
                _ when row is null => ProviderVerificationDisposition.StillUnconfirmed,
                _ when attempt.IntendedConcurrencyToken is { } intended &&
                    intended != attempt.ExpectedConcurrencyToken && row.ConcurrencyToken == intended =>
                    ProviderVerificationDisposition.Committed,
                _ when attempt.ExpectedConcurrencyToken is { } expected && row.ConcurrencyToken == expected =>
                    ProviderVerificationDisposition.DefinitelyNotCommitted,
                _ => ProviderVerificationDisposition.StillUnconfirmed
            };
            return new(outcome, attempt.ProviderId, row?.ConcurrencyToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception) {
            return new(ProviderVerificationDisposition.StillUnconfirmed, attempt.ProviderId);
        }
    }
}
