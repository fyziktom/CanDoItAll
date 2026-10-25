using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionExecutionContract(
    [property: JsonRequired] Guid ContractId,
    [property: JsonRequired] int Version,
    [property: JsonRequired] ProcessDecisionChildOwner Executor) {
    public void Validate() {
        if (ContractId == Guid.Empty || Version < 1 || !Enum.IsDefined(Executor)) {
            throw new InvalidDataException("A Process decision execution contract requires its identity, positive version and supported executor owner.");
        }
    }
}
