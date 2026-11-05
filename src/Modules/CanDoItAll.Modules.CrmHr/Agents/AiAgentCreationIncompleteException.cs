using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public sealed class AiAgentCreationIncompleteException(Guid partyId, IReadOnlyList<Error> errors, Exception? innerException = null)
    : Exception($"AI party '{partyId:D}' was created, but profile registration did not finish. Reconcile this party before trying another creation.", innerException) {
    public Guid PartyId { get; } = partyId;
    public IReadOnlyList<Error> Errors { get; } = errors.ToArray();
}
