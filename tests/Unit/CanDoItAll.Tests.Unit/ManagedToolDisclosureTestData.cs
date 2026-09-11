using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal static class ManagedToolDisclosureTestData {
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter() }
    };

    internal static AgentToolResultDisclosure Create(AgentRuntimeToolMetadata metadata, object? request = null,
        object? result = null, AgentToolEffectState effectState = AgentToolEffectState.Unknown) {
        var arguments = JsonSerializer.Serialize(new { request }, Options);
        var read = metadata.OperationKind == AgentRuntimeToolOperationKind.Read;
        return new(new(Guid.NewGuid()), new(metadata.ToolName, 1, AgentToolProtocolEnvelope.ComputeDigest(arguments),
            arguments, read ? AgentToolProposalEffect.Read : AgentToolProposalEffect.Mutation,
            read ? AgentToolProposalRecovery.RevalidateAndRead : AgentToolProposalRecovery.ReconcileBeforeRetry),
            effectState, JsonSerializer.SerializeToElement(result ?? new { acknowledged = true }, Options));
    }
}
