namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessProjectAdmission {
    public ProcessProjectAdmission(Guid databaseProfileId, Guid projectId, Guid lifetimeId) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty || lifetimeId == Guid.Empty) {
            throw new ArgumentException("A process project admission requires nonempty database profile, project and lifetime identifiers.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        LifetimeId = lifetimeId;
    }

    public Guid DatabaseProfileId { get; }
    public Guid ProjectId { get; }
    public Guid LifetimeId { get; }
}

public interface IProcessProjectAdmissionPolicy {
    Task RequireForMutationAsync(ProcessProjectAdmission admission, CancellationToken cancellationToken = default);
}
