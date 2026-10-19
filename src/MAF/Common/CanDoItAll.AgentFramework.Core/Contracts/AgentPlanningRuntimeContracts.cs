using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentPlanningParticipant {
    AgentPlanningOwner Owner { get; }
    AgentPlanningPromptContract Prompt { get; }
    Task<AgentPlanningRunAdmission> PrepareRunAsync(AgentPlanningRunPreparation preparation, CancellationToken cancellationToken);
    Task<AgentToolProtocolEnvelope> CaptureBasisAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken);
    Task RequireCurrentReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken);
    AgentPlanningResponsePreparation PreparePublication(AgentPlanningDecisionRecord decision, AgentPlanningBasisRecord basis, string responseText);
    AgentPlanningPublicationAssessment ClassifyPublication(AgentToolSessionObservation original,
        AgentToolPreparedPayload payload, AgentSavedEffectDispatchResult result)
        => throw new NotSupportedException("This planning participant does not interpret saved publication outcomes.");
}

public enum AgentPlanningPublicationOutcome { Committed, BasisChanged, Refused }

public sealed record AgentPlanningPublicationAssessment(AgentPlanningPublicationOutcome Outcome, string Message);

public interface IAgentPlanningStepRuntime {
    AgentToolSemanticDigest CaptureRuntimeContract(AgentDefinition agent, ProviderProfile provider, IAgentPlanningParticipant participant);
    Task<AgentPlanningStepResult> RunAsync(AgentPlanningStepRequest request, CancellationToken cancellationToken = default);
}

public sealed record AgentPlanningStepRequest(AgentDefinition Agent, ProviderProfile Provider,
    AgentPlanningDecisionOwner DecisionOwner, AgentPlanningLiveWork Work, IAgentPlanningParticipant Participant,
    AgentToolSemanticDigest ExpectedRuntimeContract);

public sealed record AgentPlanningStepResult(AgentPlanningResponseAdmission Admission,
    AgentRuntimeResponse RuntimeResponse, AgentPlanningOutputRefusal? OutputRefusal);

public sealed record AgentPlanningStepObservation(ExecutionRunRecord Run, AgentPlanningBasisRecord Basis, TimeSpan RemainingWork);

public enum AgentPlanningOutputRefusal { InvalidShape, LimitExceeded, UnsupportedContent, PreparationFailed }

public abstract record AgentPlanningResponsePreparation {
    private AgentPlanningResponsePreparation() { }
    public sealed record Publication(AgentToolPreparedCall Call) : AgentPlanningResponsePreparation;
    public sealed record Continue : AgentPlanningResponsePreparation;
    public sealed record Refused(AgentPlanningOutputRefusal Reason) : AgentPlanningResponsePreparation;
}

public sealed record AgentPlanningPromptContract {
    public AgentPlanningPromptContract(string instructions, string schemaName, string schemaJson) {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
        if (Encoding.UTF8.GetByteCount(instructions) > 65_536 || Encoding.UTF8.GetByteCount(schemaJson) > 65_536 ||
            schemaName.Length > 64 || schemaName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-'))) {
            throw new ArgumentException("The planning instructions and response schema exceed their supported shape or bounds.");
        }
        using var schema = JsonDocument.Parse(schemaJson, new() { MaxDepth = 32 });
        if (schema.RootElement.ValueKind != JsonValueKind.Object) {
            throw new ArgumentException("A planning response requires an object JSON schema.", nameof(schemaJson));
        }
        Instructions = instructions;
        SchemaName = schemaName;
        SchemaJson = schemaJson;
    }

    public string Instructions { get; }
    public string SchemaName { get; }
    public string SchemaJson { get; }
}
