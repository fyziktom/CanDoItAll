using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed partial class WorkspaceToolResultDisclosure {
    private static string NormalizeCollectionRequest(string toolName, AIFunctionArguments arguments, string path,
        WorkspaceRuntimeFileAccessGuard guard, ref WorkspaceToolResultEvidenceState state) {
        if (toolName != ToolContractCatalog.WorkspaceListFiles) {
            return path;
        }
        arguments.TryGetValue("searchPattern", out var patternValue);
        string? pattern;
        switch (patternValue) {
            case null:
            case JsonElement { ValueKind: JsonValueKind.Null }:
                pattern = null;
                break;
            case string text:
                pattern = text;
                break;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                pattern = element.GetString();
                break;
            default:
                state = WorkspaceToolResultEvidenceState.UnsupportedPath;
                return path;
        }
        try {
            return WorkspaceFileListRequest.Normalize(guard.PrepareFileReadPath(path), pattern).RelativePath ?? ".";
        } catch (Exception exception) when (exception is WorkspacePathResolutionException or WorkspaceToolAccessDeniedException) {
            state = WorkspaceToolResultEvidenceState.UnsupportedPath;
            return path;
        }
    }
}
