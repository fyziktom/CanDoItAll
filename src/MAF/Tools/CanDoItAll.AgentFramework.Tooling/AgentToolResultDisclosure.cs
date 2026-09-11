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
    AgentToolProtocolEnvelope? Evidence = null);
