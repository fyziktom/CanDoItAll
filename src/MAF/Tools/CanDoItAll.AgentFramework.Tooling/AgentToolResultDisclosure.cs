using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Tooling;

public sealed record AgentToolResultDisclosure(
    AgentToolBusinessIntentId IntentId,
    AgentToolPreparedPayload Payload,
    AgentToolEffectState EffectState,
    JsonElement Result,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AgentToolProtocolEnvelope? Evidence = null) {
    [JsonIgnore]
    public bool IsTypedFailure { get; init; }

    /// <summary>
    /// The saved result is the owner's typed rejection with proven no effect. It carries only that safe rejection text,
    /// so an owner re-disclosing it checks its current authority instead of the shape of a successful result.
    /// </summary>
    [JsonIgnore]
    public bool IsNoEffectTypedFailure =>
        IsTypedFailure && EffectState is AgentToolEffectState.None or AgentToolEffectState.NotCommitted;
}
