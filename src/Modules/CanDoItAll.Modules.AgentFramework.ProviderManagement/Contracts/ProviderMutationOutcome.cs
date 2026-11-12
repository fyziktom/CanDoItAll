namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed record ProviderMutationCommit(
    Guid ProviderId,
    ProviderCatalogProjectionOperationKind OperationKind,
    Guid? ConcurrencyToken = null,
    CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel? Accepted = null);

public class ProviderMutationCommittedException(
    ProviderMutationCommit commit,
    string warning,
    Exception innerException) : InvalidOperationException(warning, innerException), CanDoItAll.AgentFramework.Models.IProviderConfigurationFailure {
    public ProviderMutationCommit Commit { get; } = commit;
    public Guid ProviderId => Commit.ProviderId;
    public ProviderCatalogProjectionOperationKind OperationKind => Commit.OperationKind;
    public bool CanonicalCommitSucceeded => true;
}

public sealed class ProviderMutationUnconfirmedException(ProviderMutationAttempt attempt, Exception innerException)
    : Exception("The provider write could not be confirmed. Verify this exact attempt before another write.", innerException), CanDoItAll.AgentFramework.Models.IProviderConfigurationFailure {
    public ProviderMutationAttempt Attempt { get; } = attempt;
    public bool CanonicalCommitSucceeded => false;
}

public sealed class ProviderProfileConcurrencyException(Guid providerId, Exception? innerException = null)
    : CanDoItAll.AgentFramework.Models.ProviderConfigurationConflictException(providerId, innerException);

public interface IProviderCatalogReconciliation {
    Task ReconcileAsync(Guid providerId, CancellationToken cancellationToken = default);
}

public sealed class ProviderHealthDiagnosticException(Exception innerException)
    : Exception("The provider health diagnostic did not complete. No provider health update was written.", innerException);
