namespace CanDoItAll.Modules.Workspace.ApiAccess;

public sealed class ApiDurableAcknowledgementException(Guid candidateId, string failureType)
    : Exception("The API credential store did not acknowledge the write. Do not automatically repeat it.") {
    public Guid CandidateId { get; } = candidateId;
    public string FailureType { get; } = failureType;
}
