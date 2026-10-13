using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class BrowserMcpFunction(AIFunction inner, string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope, IReadOnlyList<IToolInvocationPolicyContextContributor> contributors)
    : DelegatingAIFunction(inner) {
    public override JsonElement JsonSchema => MafToolInputSchema.Apply(base.JsonSchema,
        contributors.SelectMany(contributor => contributor.ContributeToolInputConstraints(Name,
            WorkspaceExecutionAuditContext.Current)).ToArray());

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) {
        var fileName = arguments.TryGetValue("filename", out var value) ? value switch {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null
        } : null;
        var scope = BrowserMcpArtifactPathService.ResolveArtifactScope(workspaceScope);
        BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(workspaceRoot, scope, fileName);
        var before = BrowserMcpArtifactPathService.ObserveBeforeInvocation(workspaceRoot, scope, fileName);
        var raw = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
        var canonical = BrowserMcpResultProjector.Capture(raw);
        var artifact = new BrowserMcpArtifactImportResult([]);
        if (BrowserMcpResultProjector.Outcome(canonical) == AgentToolInvocationOutcome.Succeeded) {
            try {
                artifact = BrowserMcpArtifactPathService.TryImportAfterInvocation(workspaceRoot, scope, fileName,
                    WorkspaceExecutionAuditContext.Current?.ProcessRunId, before);
            } catch (Exception failure) when (failure is IOException or UnauthorizedAccessException) {
                artifact = new([], $"Artifact import failed ({failure.GetType().Name}). The MCP reply is retained; no saved artifact is confirmed.");
            }
        }
        var result = BrowserMcpResultProjector.Project(Name, fileName, canonical, artifact);
        if (result.Succeeded && WorkspaceExecutionAuditContext.Current is { } audit) {
            foreach (var path in artifact.ImportedRelativePaths) {
                WorkspaceExecutionAuditTrailWriter.PersistArtifact(workspaceRoot, scope, new(
                    Guid.NewGuid(), audit.ExecutionRunId, "browser-evidence", Path.GetFileName(path), path,
                    Path.GetExtension(path).ToLowerInvariant() switch {
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        _ => "application/octet-stream"
                    }, Name, "Fresh artifact verified and retained from this browser invocation.", DateTimeOffset.UtcNow));
            }
        }
        return result;
    }
}
