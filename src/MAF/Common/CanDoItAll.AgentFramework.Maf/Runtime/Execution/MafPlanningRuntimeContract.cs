using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafPlanningRuntimeContract {
    private const int Version = 1;

    internal static AgentToolSemanticDigest Capture(AgentDefinition agent, ProviderProfile provider, IAgentPlanningStepParticipant participant) {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(participant);
        var prompt = participant.Prompt;
        var options = Options(agent, provider, prompt);
        return MafToolProtocolCodec.Digest(new {
            Version, agent.Id, participant.Owner,
            Provider = ProviderConfigurationFingerprintFactory.Create(provider),
            Prompt = prompt, Options = MafToolAdmissionChatClient.RequestDigest([], options, [])
        });
    }

    internal static void Require(AgentPlanningStepRequest request) {
        if (Capture(request.Agent, request.Provider, request.Participant) != request.ExpectedRuntimeContract) {
            throw MafPlanningStepContext.Failure("The originally admitted provider configuration, model options or planning contract changed. Fresh computation is refused.");
        }
    }

    internal static ChatOptions Options(AgentDefinition agent, ProviderProfile provider, AgentPlanningPromptContract prompt) {
        var model = MafModelParametersBuilder.ResolveRuntimeModel(agent, provider);
        using var schema = JsonDocument.Parse(prompt.SchemaJson);
        var options = MafModelParametersBuilder.CreateModelCompatibleChatOptions(provider, model,
            (float)agent.Temperature, forceOmitTemperature: false, agent.ConfigurationJson);
        options.ModelId = model;
        options.MaxOutputTokens = AgentPlanningProviderBounds.MaximumOutputTokens;
        options.ResponseFormat = ChatResponseFormat.ForJsonSchema(schema.RootElement.Clone(), prompt.SchemaName);
        return options;
    }
}
