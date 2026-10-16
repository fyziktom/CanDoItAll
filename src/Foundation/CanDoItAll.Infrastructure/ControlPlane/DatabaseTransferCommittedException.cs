namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatabaseTransferCommittedException(Guid sourceProfileId, Guid targetProfileId,
    DatabaseTransferItemResult committedResult, Exception innerException)
    : Exception("The target transfer committed, but connection or transaction cleanup did not finish. Do not repeat the transfer.", innerException) {
    public Guid SourceProfileId { get; } = sourceProfileId;
    public Guid TargetProfileId { get; } = targetProfileId;
    public DatabaseTransferItemResult CommittedResult { get; } = committedResult;
}
