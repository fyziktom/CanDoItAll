namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed record ProviderMutationCommit(
    Guid ProviderId,
    ProviderCatalogProjectionOperationKind OperationKind,
    Guid? ConcurrencyToken = null);

public class ProviderMutationCommittedException(
    ProviderMutationCommit commit,
    string warning,
    Exception innerException) : InvalidOperationException(warning, innerException) {
    public ProviderMutationCommit Commit { get; } = commit;
    public Guid ProviderId => Commit.ProviderId;
    public ProviderCatalogProjectionOperationKind OperationKind => Commit.OperationKind;
    public bool CanonicalCommitSucceeded => true;
}

public sealed class ProviderMutationUnconfirmedException(Exception innerException)
    : Exception("The provider write could not be confirmed. Refresh and verify the canonical state before another write.", innerException);

public sealed class ProviderProfileConcurrencyException(Guid providerId, Exception? innerException = null)
    : Exception("The provider changed after it was read. Reload it before saving again.", innerException) {
    public Guid ProviderId { get; } = providerId;
}

public interface IProviderCatalogReconciliation {
    Task ReconcileAsync(Guid providerId, CancellationToken cancellationToken = default);
}

public sealed class ProviderHealthDiagnosticException(Exception innerException)
    : Exception("The provider health diagnostic did not complete. No provider health update was written.", innerException);
