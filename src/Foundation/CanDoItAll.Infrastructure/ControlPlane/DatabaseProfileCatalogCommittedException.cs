namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class DatabaseProfileCatalogCommittedException(Guid profileId, Exception innerException)
    : Exception("The profile catalog was saved, but active-selection processing did not finish.", innerException) {
    public Guid ProfileId { get; } = profileId;
}
