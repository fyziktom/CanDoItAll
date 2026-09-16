using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafContextToolSourcePreparation {
    AgentToolProtocolEnvelope Prepare(string toolName, JsonElement arguments);
    ValueTask ValidateAsync(AgentToolProtocolEnvelope source, CancellationToken cancellationToken);
}

internal static class MafContextToolSourceContract {
    internal const int SemanticVersion = 2;

    internal static AgentToolPreparedPayload Bind(AgentToolPreparedPayload original, AgentToolProtocolEnvelope? source) {
        if (source is null) {
            return original;
        }
        using var arguments = JsonDocument.Parse(original.ArgumentsJson);
        var digest = MafToolProtocolCodec.Digest(new {
            SemanticVersion, original.ToolName, Arguments = arguments.RootElement, SourcePreparation = source,
            original.Effect, original.Recovery
        });
        return new(original.ToolName, SemanticVersion, digest, original.ArgumentsJson, original.Effect, original.Recovery, source);
    }

    internal static AgentToolAdmissionException MissingSource()
        => new("tool-admission.source-preparation-unavailable",
            "The saved context tool proposal has no supported original source preparation. It cannot be rebound or executed automatically.");
}
