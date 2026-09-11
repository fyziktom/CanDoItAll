using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkspaceScriptSideEffectFindingKind {
    Write,
    EncodedCommand,
    ShellDelegation,
    ChildScript
}

public sealed record WorkspaceScriptSideEffectFinding(
    WorkspaceScriptSideEffectFindingKind Kind,
    string Signal);

public sealed record WorkspaceScriptSideEffectAnalysis(IReadOnlyList<WorkspaceScriptSideEffectFinding> Findings) {
    public bool HasWriteSignal => Findings.Any(finding => finding.Kind == WorkspaceScriptSideEffectFindingKind.Write);

    public IReadOnlyList<string> EncodedCommandSignals => ResolveSignals(WorkspaceScriptSideEffectFindingKind.EncodedCommand);

    public IReadOnlyList<string> ShellDelegationSignals => ResolveSignals(WorkspaceScriptSideEffectFindingKind.ShellDelegation);

    public IReadOnlyList<string> ChildScriptSignals => ResolveSignals(WorkspaceScriptSideEffectFindingKind.ChildScript);

    private IReadOnlyList<string> ResolveSignals(WorkspaceScriptSideEffectFindingKind kind) {
        return Findings
            .Where(finding => finding.Kind == kind)
            .Select(finding => finding.Signal)
            .Where(signal => !string.IsNullOrWhiteSpace(signal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public static class WorkspaceScriptSideEffectAnalyzer {
    private static readonly Regex PowerShellWriteSignalRegex = new(
        @"(?:\b(?:Set-Content|Add-Content|Out-File|New-Item|Remove-Item|Move-Item|Copy-Item|Rename-Item|Clear-Content|Set-ItemProperty|New-ItemProperty)\b|\[(?:System\.)?IO\.File\]::(?:WriteAllText|WriteAllLines|WriteAllBytes|AppendAllText|AppendAllLines|Delete|Move|Copy)\s*\(|(?<![<>=])>{1,2}(?![=>&]))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PythonWriteSignalRegex = new(
        @"(?:\.(?:write_text|write_bytes|unlink|rename|replace|mkdir|rmdir)\s*\(|\.open\s*\(\s*[""'][^""']*[wax+][^""']*[""']|\bshutil\.(?:copy|copyfile|copytree|move|rmtree)\b|\bos\.(?:remove|unlink|rename|replace|makedirs|rmdir)\b|\bopen\s*\([^,\r\n]+,\s*[""'][^""']*[wax+][^""']*[""'])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PowerShellEncodedCommandRegex = new(
        @"(?:\b(?:pwsh|powershell)(?:\.exe)?\b[^\r\n]*-(?:EncodedCommand|enc|e)\b|\bFromBase64String\s*\()",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PowerShellShellDelegationRegex = new(
        @"(?:\bcmd(?:\.exe)?\s+/[dc]\b|\bStart-Process\b|\bStart-Job\b|\bSystem\.Diagnostics\.Process\b|\[Diagnostics\.Process\]|\[System\.Diagnostics\.Process\])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PowerShellChildScriptRegex = new(
        @"(?:^|[\s;&|])(?:&|\.)\s*(?<path>['""]?[^'"">\r\n]+\.ps1['""]?)|\b-File\s+(?<path>['""]?[^'"">\r\n]+\.ps1['""]?)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PythonShellDelegationRegex = new(
        @"\b(?:subprocess\.(?:run|Popen|call|check_call|check_output)|os\.system|os\.popen)\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PythonChildScriptRegex = new(
        @"\b(?:subprocess\.(?:run|Popen|call|check_call|check_output)|runpy\.run_path)\s*\([^\r\n]*(?<path>['""][^'""]+\.py['""])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static WorkspaceScriptSideEffectAnalysis Analyze(string toolName, string scriptContent) {
        if (string.IsNullOrWhiteSpace(scriptContent)) {
            return new WorkspaceScriptSideEffectAnalysis([]);
        }

        var findings = new List<WorkspaceScriptSideEffectFinding>();
        if (string.Equals(toolName, AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase)) {
            AddMatches(findings, PowerShellWriteSignalRegex, scriptContent, WorkspaceScriptSideEffectFindingKind.Write);
            AddMatches(findings, PowerShellEncodedCommandRegex, scriptContent, WorkspaceScriptSideEffectFindingKind.EncodedCommand);
            AddMatches(findings, PowerShellShellDelegationRegex, scriptContent, WorkspaceScriptSideEffectFindingKind.ShellDelegation);
            AddChildScriptMatches(findings, PowerShellChildScriptRegex, scriptContent);
            return new WorkspaceScriptSideEffectAnalysis(findings);
        }

        if (string.Equals(toolName, AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile, StringComparison.OrdinalIgnoreCase)) {
            AddMatches(findings, PythonWriteSignalRegex, scriptContent, WorkspaceScriptSideEffectFindingKind.Write);
            AddMatches(findings, PythonShellDelegationRegex, scriptContent, WorkspaceScriptSideEffectFindingKind.ShellDelegation);
            AddChildScriptMatches(findings, PythonChildScriptRegex, scriptContent);
            return new WorkspaceScriptSideEffectAnalysis(findings);
        }

        return new WorkspaceScriptSideEffectAnalysis([]);
    }

    public static bool IsDeclaredChildScript(string childScript, IReadOnlyList<string> declaredChildScripts) {
        var normalizedChildScript = NormalizeToolPath(childScript);
        return declaredChildScripts
            .Select(NormalizeToolPath)
            .Any(declaredChildScript =>
                string.Equals(normalizedChildScript, declaredChildScript, StringComparison.OrdinalIgnoreCase) ||
                normalizedChildScript.EndsWith("/" + declaredChildScript, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasInspectedChildScriptMarker(string inspectedScriptContent, string childScript) {
        var marker = BuildInspectedChildScriptMarker(childScript);
        return inspectedScriptContent.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildInspectedChildScriptMarker(string childScript) {
        return $"# inspected-child-script: {NormalizeToolPath(childScript)}";
    }

    private static void AddMatches(
        List<WorkspaceScriptSideEffectFinding> findings,
        Regex regex,
        string scriptContent,
        WorkspaceScriptSideEffectFindingKind kind) {
        foreach (Match match in regex.Matches(scriptContent)) {
            var signal = CollapsePolicySignal(match.Value);
            if (!string.IsNullOrWhiteSpace(signal)) {
                findings.Add(new WorkspaceScriptSideEffectFinding(kind, signal));
            }
        }
    }

    private static void AddChildScriptMatches(
        List<WorkspaceScriptSideEffectFinding> findings,
        Regex regex,
        string scriptContent) {
        foreach (Match match in regex.Matches(scriptContent)) {
            var path = match.Groups["path"].Success
                ? match.Groups["path"].Value
                : match.Value;
            var signal = NormalizeToolPath(path);
            if (!string.IsNullOrWhiteSpace(signal)) {
                findings.Add(new WorkspaceScriptSideEffectFinding(WorkspaceScriptSideEffectFindingKind.ChildScript, signal));
            }
        }
    }

    private static string CollapsePolicySignal(string signal) {
        var collapsed = signal.ReplaceLineEndings(" ").Trim();
        return collapsed.Length <= 80 ? collapsed : collapsed[..80];
    }

    private static string NormalizeToolPath(string? value) {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\\', '/').Trim().Trim('`', '"', '\'').Trim('/');
    }
}
