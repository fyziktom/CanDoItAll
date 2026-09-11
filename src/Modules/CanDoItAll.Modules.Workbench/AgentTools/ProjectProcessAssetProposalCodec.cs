using CanDoItAll.SharedKernel;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessAssetProposalCodec : IAgentToolProposalPreparer {
    public const string RevisionToolName = "project_structure_asset_create_revision";
    public const int SemanticVersion = 1;
    internal static JsonSerializerOptions Json => ProjectStructureProcessProposalCodec.SerializerOptions;

    public bool Supports(string toolName)
        => toolName is ProjectStructureToolPolicy.ProjectStructureAssetCreate or RevisionToolName;

    public AgentToolPreparedPayload Prepare(string toolName, JsonElement arguments) {
        RequireUniqueProperties(arguments);
        object input = toolName switch {
            ProjectStructureToolPolicy.ProjectStructureAssetCreate => arguments.Deserialize<ProjectProcessAssetCreateProposal>(Json)
                ?? throw new JsonException("A typed asset creation proposal is required."),
            RevisionToolName => arguments.Deserialize<ProjectProcessAssetRevisionProposal>(Json)
                ?? throw new JsonException("A typed asset revision proposal is required."),
            _ => throw new ArgumentException("This codec only admits asset creation and revision.", nameof(toolName))
        };
        var canonical = JsonSerializer.Serialize(input, input.GetType(), Json);
        var digest = AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            Version = SemanticVersion, ToolName = toolName, Input = JsonSerializer.Deserialize<JsonElement>(canonical)
        }, Json));
        var payload = new AgentToolPreparedPayload(toolName, SemanticVersion, digest, canonical,
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
        ReadInput(payload);
        return payload;
    }

    internal ProjectProcessAssetProposal Read(AgentToolPreparedPayload payload) {
        using var document = JsonDocument.Parse(payload.ArgumentsJson);
        if (Prepare(payload.ToolName, document.RootElement) != payload) {
            throw new InvalidOperationException("The saved asset proposal differs from its original approved payload.");
        }
        return ReadInput(payload);
    }

    private static ProjectProcessAssetProposal ReadInput(AgentToolPreparedPayload payload) {
        var result = payload.ToolName switch {
            ProjectStructureToolPolicy.ProjectStructureAssetCreate => FromCreate(JsonSerializer.Deserialize<ProjectProcessAssetCreateProposal>(payload.ArgumentsJson, Json)!),
            RevisionToolName => FromRevision(JsonSerializer.Deserialize<ProjectProcessAssetRevisionProposal>(payload.ArgumentsJson, Json)!),
            _ => throw new InvalidOperationException("The saved asset proposal has an unsupported tool.")
        };
        ArgumentOutOfRangeException.ThrowIfEqual(result.ProjectId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.ParentNodeKey);
        if (result.Create is { } create && create.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset)) {
            throw new ArgumentException("A managed asset proposal requires a File, ImageAsset or VideoAsset.");
        }
        if (result.Revision is { Media: null }) {
            throw new ArgumentException("An asset revision requires supplied media.");
        }
        return result;
    }

    private static ProjectProcessAssetProposal FromCreate(ProjectProcessAssetCreateProposal value) {
        ArgumentNullException.ThrowIfNull(value.Request);
        return new(value.ProjectId, value.Request.ParentNodeKey!, value.Request, null);
    }

    private static ProjectProcessAssetProposal FromRevision(ProjectProcessAssetRevisionProposal value) {
        ArgumentNullException.ThrowIfNull(value.Request);
        return new(value.ProjectId, value.NodeId, null, value.Request);
    }

    private static void RequireUniqueProperties(JsonElement element) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("Duplicate asset proposal properties are not accepted.");
                }
                RequireUniqueProperties(property.Value);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (var item in element.EnumerateArray()) {
                RequireUniqueProperties(item);
            }
        }
    }
}

public sealed record ProjectProcessAssetCreateProposal(Guid ProjectId, ProjectStructureAgentAssetCreateInput Request,
    int? EstimatedMinutes = null);

public sealed record ProjectProcessAssetRevisionProposal(Guid ProjectId, string NodeId, ProjectStructureAgentAssetRevisionRequest Request,
    int? EstimatedMinutes = null);

internal sealed record ProjectProcessAssetProposal(Guid ProjectId, string ParentNodeKey,
    ProjectStructureAgentAssetCreateInput? Create, ProjectStructureAgentAssetRevisionRequest? Revision);
