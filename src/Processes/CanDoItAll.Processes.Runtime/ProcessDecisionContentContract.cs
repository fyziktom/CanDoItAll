using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionContentContract([property: JsonRequired] Guid SchemaId, [property: JsonRequired] int Version) {
    public void Validate() {
        if (SchemaId == Guid.Empty || Version < 1) {
            throw new InvalidDataException("Process material requires its original business content schema and version.");
        }
    }
}
