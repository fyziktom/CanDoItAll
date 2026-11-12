namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatabaseImportCommitKnowledge { Unconfirmed, Acknowledged }

public sealed class DatabaseImportCommitException(Guid targetProfileId, DatabaseImportCommitKnowledge knowledge, Exception innerException)
    : Exception("The import reached its commit boundary; preserve staged assets and inspect the target before any further import.", innerException) {
    public Guid TargetProfileId { get; } = targetProfileId;
    public DatabaseImportCommitKnowledge Knowledge { get; } = knowledge;
}
