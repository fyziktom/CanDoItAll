using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessProposalCodec : IAgentToolProposalPreparer {
    public const int SemanticVersion = 1;
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    internal static JsonSerializerOptions ResultSerializerOptions { get; } = new(SerializerOptions) {
        Converters = { new JsonStringEnumConverter() }
    };

    public bool Supports(string toolName) => toolName == ProjectStructureToolPolicy.ProjectStructureNodeProcessStart;

    public AgentToolPreparedPayload Prepare(string toolName, JsonElement arguments) {
        if (!Supports(toolName)) {
            throw new ArgumentException("This proposal codec only admits a Structure Process node start.", nameof(toolName));
        }
        RequireUniqueProperties(arguments);
        var input = arguments.Deserialize<ProjectStructureProcessStartProposal>(SerializerOptions)
            ?? throw new JsonException("A typed Structure Process start proposal is required.");
        return Prepare(input);
    }

    public AgentToolPreparedPayload Prepare(ProjectStructureProcessStartProposal input) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Request);
        ArgumentOutOfRangeException.ThrowIfEqual(input.ProjectId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.NodeId);
        var json = JsonSerializer.Serialize(input, SerializerOptions);
        var digest = JsonSerializer.Serialize(new {
            Version = SemanticVersion,
            ToolName = ProjectStructureToolPolicy.ProjectStructureNodeProcessStart,
            Input = input
        }, SerializerOptions);
        return new(ProjectStructureToolPolicy.ProjectStructureNodeProcessStart, SemanticVersion,
            new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digest))).ToLowerInvariant()),
            json, AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
    }

    public ProjectStructureProcessStartProposal Read(AgentToolPreparedPayload payload) {
        using var document = JsonDocument.Parse(payload.ArgumentsJson);
        if (Prepare(payload.ToolName, document.RootElement) != payload) {
            throw new InvalidOperationException("The saved Structure Process proposal differs from its exact semantic payload.");
        }
        return document.RootElement.Deserialize<ProjectStructureProcessStartProposal>(SerializerOptions)!;
    }

    private static void RequireUniqueProperties(JsonElement element) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("Duplicate proposal properties are not accepted.");
                }
                RequireUniqueProperties(property.Value);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (var value in element.EnumerateArray()) {
                RequireUniqueProperties(value);
            }
        }
    }
}

public sealed record ProjectStructureProcessStartProposal(
    Guid ProjectId,
    string NodeId,
    ProjectStructureProcessNodeStartInput Request,
    int? EstimatedMinutes = null);
