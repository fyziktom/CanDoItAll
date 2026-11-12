using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Playwright;

internal enum FileProposalRefusal { None, UnexpectedTool, InvalidSchema, WrongTarget, ChangedContent, DuplicateEffect }

internal sealed record FileApprovalIntent(Guid ProjectId, string ParentId, string Title, string Content,
    string FileName, string ContentType, string? WorkspacePath = null) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal FileProposalRefusal Validate(AgentToolPreparedPayload payload, IReadOnlySet<string> acceptedTools) {
        if (acceptedTools.Contains(payload.ToolName)) {
            return FileProposalRefusal.DuplicateEffect;
        }
        try {
            using var document = JsonDocument.Parse(payload.ArgumentsJson);
            RequireUniqueMembers(document.RootElement);
            if (payload.ToolName == ToolContractCatalog.WorkspaceWriteFile && WorkspacePath is not null) {
                var write = document.RootElement.Deserialize<WorkspaceWrite>(Json);
                if (write is null || write.Path != WorkspacePath || write.Overwrite != false) {
                    return FileProposalRefusal.WrongTarget;
                }
                return write.Content == Content ? FileProposalRefusal.None : FileProposalRefusal.ChangedContent;
            }
            if (payload.ToolName != ProjectStructureToolPolicy.ProjectStructureAssetCreate) {
                return FileProposalRefusal.UnexpectedTool;
            }
            var create = document.RootElement.Deserialize<ProjectProcessAssetCreateProposal>(Json);
            if (create is null || create.EstimatedMinutes is not null || create.ProjectId != ProjectId || create.Request is not { } request ||
                request.ObjectType != ProjectObjectType.File || request.ParentNodeKey != ParentId || request.Title != Title ||
                !string.IsNullOrEmpty(request.Subtitle) || !string.IsNullOrEmpty(request.Notes) ||
                !string.IsNullOrEmpty(request.ObjectSubtype) || !string.IsNullOrEmpty(request.LeaseToken) || request.SourceUrl is not null) {
                return FileProposalRefusal.WrongTarget;
            }
            if (WorkspacePath is not null) {
                return request.Media is null && request.SourceWorkspacePath == WorkspacePath &&
                    request.SourceFileName == FileName && request.SourceContentType == ContentType
                    ? FileProposalRefusal.None : FileProposalRefusal.WrongTarget;
            }
            if (request.SourceWorkspacePath is not null || request.SourceFileName is not null ||
                request.SourceContentType is not null || request.Media is not { } media ||
                media.FileName != FileName || media.ContentType != ContentType) {
                return FileProposalRefusal.WrongTarget;
            }
            return Convert.FromBase64String(media.Base64Data).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Content))
                ? FileProposalRefusal.None : FileProposalRefusal.ChangedContent;
        } catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException) {
            return FileProposalRefusal.InvalidSchema;
        }
    }

    private static void RequireUniqueMembers(JsonElement value) {
        if (value.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("Duplicate proposal member.");
                }
                RequireUniqueMembers(property.Value);
            }
        } else if (value.ValueKind == JsonValueKind.Array) {
            foreach (var item in value.EnumerateArray()) {
                RequireUniqueMembers(item);
            }
        }
    }

    private sealed record WorkspaceWrite(string Path, string Content, bool? Overwrite);
}
