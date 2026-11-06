using System.ComponentModel;

namespace CanDoItAll.AgentFramework.Models;

public sealed record ProviderProfileObservation(AgentExecutionActivityWorkspaceIdentity Workspace,
    ProviderProfile Profile, Guid ExpectedConcurrencyToken);

public interface IProviderConfigurationFailure {
    bool CanonicalCommitSucceeded { get; }
}

public sealed class ProviderConfigurationCommittedException(Guid providerId, ProviderProfileEditorModel? accepted, Exception innerException)
    : Exception("The provider change is saved, but a secondary effect requires reconciliation.", innerException), IProviderConfigurationFailure {
    public Guid ProviderId { get; } = providerId;
    public ProviderProfileEditorModel? Accepted { get; } = accepted;
    public bool CanonicalCommitSucceeded => true;
}

public class ProviderConfigurationConflictException(Guid providerId, Exception? innerException = null)
    : Exception("The provider changed after it was read. Review its current configuration before another write.", innerException) {
    public Guid ProviderId { get; } = providerId;
}

[Description("Persistence outcome for the local provider configuration after remote model maintenance completed.")]
public enum ProviderModelMaintenancePersistenceState { Rejected, Conflict, Unconfirmed, CommittedWithWarning }

public sealed class ProviderModelMaintenancePersistenceException(Guid providerId, ProviderModelMaintenanceEditorResult result,
    ProviderModelMaintenancePersistenceState state, Exception innerException)
    : Exception("Remote model maintenance completed. Provider configuration requires review or reconciliation; do not repeat the remote operation.", innerException) {
    public Guid ProviderId { get; } = providerId;
    public ProviderModelMaintenanceEditorResult Result { get; } = result;
    public ProviderModelMaintenancePersistenceState State { get; } = state;
}
