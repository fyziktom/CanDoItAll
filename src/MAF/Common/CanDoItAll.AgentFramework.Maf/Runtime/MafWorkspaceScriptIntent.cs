using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Maf;

internal enum MafWorkspaceScriptIntent {
    Unspecified,
    InvalidManifest,
    Validation
}

internal static class MafWorkspaceScriptIntentResolver {
    internal const string ManifestGuidance = "Use sideEffectManifest {\"mode\":\"NoMutation\"} for a validation script with no writes. For scripts that write, declare the applicable write mode and exact declaredWritePaths.";

    public static MafWorkspaceScriptIntent Resolve(ToolInvocationPolicyContext context) {
        if (context.DeclaredCapability?.OperationRequirementKind != ToolCapabilityOperationRequirementKind.WorkspaceScript ||
            string.IsNullOrWhiteSpace(context.ScriptSideEffectManifestJson)) {
            return MafWorkspaceScriptIntent.Unspecified;
        }
        if (!GovernedScriptSideEffectManifest.TryParse(context.ScriptSideEffectManifestJson, out var manifest, out _)) {
            return MafWorkspaceScriptIntent.InvalidManifest;
        }
        if (manifest.Mode != GovernedScriptSideEffectMode.NoMutation || manifest.DeclaredWritePaths.Length != 0 ||
            manifest.DeclaredChildScripts.Length != 0 || manifest.AllowEncodedCommands || manifest.AllowShellDelegation ||
            !context.PathArguments.IsComplete || context.PathArguments.Values.Any(path => path.Name.Equals("outputPaths", StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(context.ScriptInspectionFailure) || string.IsNullOrWhiteSpace(context.InspectedScriptContent)) {
            return MafWorkspaceScriptIntent.Unspecified;
        }

        return WorkspaceScriptSideEffectAnalyzer.Analyze(context.ToolName, context.InspectedScriptContent).Findings.Count == 0
            ? MafWorkspaceScriptIntent.Validation : MafWorkspaceScriptIntent.Unspecified;
    }
}
