using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowProcessToolProposalCodec {
    public const int SemanticVersion = 1;
    private const string DigestDomain = "process-workflow-tool-proposal-v1";
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public AgentToolPreparedPayload Prepare(JsonElement arguments) {
        RequireUniqueProperties(arguments, StringComparer.OrdinalIgnoreCase);
        var proposal = arguments.Deserialize<WorkflowProcessToolProposal>(SerializerOptions)
            ?? throw new JsonException("A typed Process Workflow proposal is required.");
        return Prepare(proposal.Request);
    }

    public AgentToolPreparedPayload Prepare(WorkflowAgentStartInput request) {
        ArgumentNullException.ThrowIfNull(request);
        using var input = JsonDocument.Parse(request.InputJson);
        RequireUniqueProperties(input.RootElement, StringComparer.Ordinal);
        var normalized = new WorkflowAgentStartInput(request.WorkflowId, request.SelectionMode, request.VersionId,
            WorkflowLaunchIdempotencyRequestFactory.CanonicalizeInputJson(request.InputJson), request.IdempotencyKey);
        var proposal = new WorkflowProcessToolProposal(normalized);
        var json = JsonSerializer.Serialize(proposal, SerializerOptions);
        var digest = AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            Domain = DigestDomain,
            ToolName = WorkflowToolPolicy.WorkflowsRunStart,
            Proposal = proposal
        }, SerializerOptions));
        return new(WorkflowToolPolicy.WorkflowsRunStart, SemanticVersion, digest, json,
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
    }

    public WorkflowAgentStartInput Read(AgentToolPreparedPayload payload) {
        using var document = JsonDocument.Parse(payload.ArgumentsJson);
        if (payload.ToolName != WorkflowToolPolicy.WorkflowsRunStart || Prepare(document.RootElement) != payload) {
            throw new InvalidOperationException("The saved Process Workflow proposal differs from its approved semantic payload.");
        }
        return document.RootElement.Deserialize<WorkflowProcessToolProposal>(SerializerOptions)!.Request;
    }

    private static void RequireUniqueProperties(JsonElement element, StringComparer comparer) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(comparer);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("Duplicate Workflow proposal properties are not accepted.");
                }
                RequireUniqueProperties(property.Value, comparer);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (var value in element.EnumerateArray()) {
                RequireUniqueProperties(value, comparer);
            }
        }
    }
}

public sealed record WorkflowProcessToolProposal(WorkflowAgentStartInput Request);
