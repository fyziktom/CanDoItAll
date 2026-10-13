using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed record BrowserMcpToolResult(AgentToolInvocationOutcome Outcome, string Message,
    IReadOnlyList<string> Artifacts, string FailureCode) : IAgentToolInvocationResultEvidence {
    public bool Succeeded => Outcome == AgentToolInvocationOutcome.Succeeded;
    public AgentToolEffectState EffectState => AgentToolEffectState.Unknown;
    public bool CanRetryWithCorrectedInput => false;
    public string SafeMessage => Succeeded ? string.Empty : Message;
    [JsonIgnore]
    public JsonElement CanonicalResult { get; init; }
    public override string ToString() => Message;
}

internal static class BrowserMcpResultProjector {
    internal static JsonElement Capture(object? result) {
        if (result is JsonElement element) {
            if (element.ValueKind != JsonValueKind.String) {
                return element.Clone();
            }
            result = element.GetString();
        }
        if (result is string text) {
            try {
                using var document = JsonDocument.Parse(text);
                return document.RootElement.Clone();
            } catch (JsonException) {
                return JsonSerializer.SerializeToElement(text);
            }
        }
        return result is CallToolResult native
            ? JsonSerializer.SerializeToElement(native, McpJsonUtilities.DefaultOptions)
            : JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
    }

    internal static AgentToolInvocationOutcome Outcome(JsonElement canonical) {
        if (canonical.ValueKind != JsonValueKind.Object ||
            !canonical.TryGetProperty("content", out var contents) || contents.ValueKind != JsonValueKind.Array) {
            return AgentToolInvocationOutcome.Unknown;
        }
        if (!canonical.TryGetProperty("isError", out var error)) {
            return AgentToolInvocationOutcome.Succeeded;
        }
        return error.ValueKind switch {
            JsonValueKind.True => AgentToolInvocationOutcome.Failed,
            JsonValueKind.False => AgentToolInvocationOutcome.Succeeded,
            _ => AgentToolInvocationOutcome.Unknown
        };
    }

    internal static BrowserMcpToolResult Project(string toolName, string? requestedFile, JsonElement canonical,
        BrowserMcpArtifactImportResult artifact) {
        var outcome = Outcome(canonical);
        var screenshot = toolName.Equals(ToolContractCatalog.BrowserTakeScreenshot, StringComparison.OrdinalIgnoreCase);
        var maximum = screenshot ? 2000 : 12000;
        var missingArtifact = !string.IsNullOrWhiteSpace(requestedFile) && !artifact.Imported;
        var failureCode = outcome switch {
            AgentToolInvocationOutcome.Failed => "BrowserMcpError",
            AgentToolInvocationOutcome.Unknown => "BrowserMcpUnverifiedResult",
            _ => string.Empty
        };
        if (outcome == AgentToolInvocationOutcome.Succeeded && missingArtifact) {
            outcome = AgentToolInvocationOutcome.Failed;
            failureCode = "BrowserArtifactUnverified";
        }
        var message = new StringBuilder($"Browser MCP tool {toolName}: {outcome}.");
        if (artifact.Imported && outcome == AgentToolInvocationOutcome.Succeeded) {
            message.Append(" Verified fresh artifact: ").AppendJoin(", ", artifact.ToolReferences).Append('.');
        } else if (missingArtifact) {
            message.Append(' ').Append(artifact.Failure ?? "No fresh artifact was verified for this invocation. Use a unique in-scope filename for a new capture.");
        }
        if (screenshot) {
            message.Append(" Image bytes are retained in execution evidence and omitted from model context.");
        }
        if (canonical.ValueKind == JsonValueKind.Object && canonical.TryGetProperty("content", out var contents) &&
            contents.ValueKind == JsonValueKind.Array) {
            foreach (var content in contents.EnumerateArray()) {
                if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("type", out var type) ||
                    type.ValueKind != JsonValueKind.String || type.GetString() != "text" ||
                    !content.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String) {
                    continue;
                }
                var remaining = maximum - message.Length;
                if (remaining <= 20) {
                    break;
                }
                var value = text.GetString()!;
                message.AppendLine();
                message.Append(value.AsSpan(0, Math.Min(value.Length, remaining - 20)));
                if (value.Length > remaining - 20) {
                    message.Append("... [truncated]");
                    break;
                }
            }
        }
        return new(outcome, message.ToString(), outcome == AgentToolInvocationOutcome.Succeeded ? artifact.ToolReferences : [], failureCode) {
            CanonicalResult = canonical
        };
    }
}
