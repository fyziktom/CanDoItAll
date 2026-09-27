using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkspaceScriptEnvironmentAccessKind
{
    /// <summary>A literal variable name outside the basic set every process receives.</summary>
    NamedRead,

    /// <summary>Listing, dumping or iterating over all environment variables.</summary>
    Enumeration,

    /// <summary>A variable name computed at run time, which inspection cannot check.</summary>
    DynamicName
}

public sealed record WorkspaceScriptEnvironmentAccessFinding(
    WorkspaceScriptEnvironmentAccessKind Kind,
    string Name,
    string Signal);

/// <summary>
/// Finds environment-variable access in PowerShell and Python scripts before they run, so an agent that
/// is not allowed to read the process environment is stopped with an explanation instead of silently
/// getting empty values. This is a guard rail, not a sandbox: the process environment itself is already
/// reduced to an allowlist, and inspection cannot see through obfuscation.
/// </summary>
public static partial class WorkspaceScriptEnvironmentAccessAnalyzer
{
    private const int MaximumSignalLength = 80;

    public static bool IsInspectedScriptTool(string? toolName)
        => string.Equals(toolName, ToolContractCatalog.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(toolName, ToolContractCatalog.WorkspacePythonRunFile, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the environment access that needs the agent's permission. Reads of names in the basic set
    /// (<paramref name="isBaseName"/>) and writes to variables are allowed and not reported.
    /// </summary>
    public static IReadOnlyList<WorkspaceScriptEnvironmentAccessFinding> Analyze(
        string toolName,
        string scriptContent,
        Func<string, bool> isBaseName)
    {
        ArgumentNullException.ThrowIfNull(isBaseName);
        if (string.IsNullOrWhiteSpace(scriptContent) || !IsInspectedScriptTool(toolName))
        {
            return [];
        }

        var findings = new List<WorkspaceScriptEnvironmentAccessFinding>();
        if (string.Equals(toolName, ToolContractCatalog.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase))
        {
            AnalyzePowerShell(StripPowerShellComments(scriptContent), isBaseName, findings);
        }
        else
        {
            AnalyzePython(StripLineComments(scriptContent), isBaseName, findings);
        }

        return findings
            .DistinctBy(finding => (finding.Kind, finding.Name.ToUpperInvariant(), finding.Signal))
            .ToArray();
    }

    /// <summary>A short, agent-safe description of the findings (names and kinds, no values).</summary>
    public static string Describe(IReadOnlyList<WorkspaceScriptEnvironmentAccessFinding> findings)
    {
        var parts = new List<string>();
        var named = findings.Where(finding => finding.Kind == WorkspaceScriptEnvironmentAccessKind.NamedRead)
            .Select(finding => finding.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (named.Length > 0)
        {
            parts.Add($"reads {JoinAsSentence(named)}");
        }

        if (findings.Any(finding => finding.Kind == WorkspaceScriptEnvironmentAccessKind.Enumeration))
        {
            parts.Add("lists all environment variables");
        }

        if (findings.Any(finding => finding.Kind == WorkspaceScriptEnvironmentAccessKind.DynamicName))
        {
            parts.Add("reads a variable whose name is computed at run time");
        }

        // Continues "The script …", for example "reads A and B, lists all environment variables and …".
        return JoinAsSentence(parts);
    }

    private static string JoinAsSentence(IReadOnlyList<string> items)
        => items.Count <= 1
            ? string.Join(string.Empty, items)
            : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    private static void AnalyzePowerShell(
        string script,
        Func<string, bool> isBaseName,
        List<WorkspaceScriptEnvironmentAccessFinding> findings)
    {
        foreach (Match match in PowerShellEnvironmentVariableRegex().Matches(script))
        {
            AddNamed(match.Groups["name"].Value, match.Value, isBaseName, findings);
        }

        foreach (Match match in PowerShellEnvironmentProviderRegex().Matches(script))
        {
            var name = match.Groups["name"].Value.Trim().Trim('\\', '/');
            if (name.Length == 0 || name == "*")
            {
                findings.Add(new(WorkspaceScriptEnvironmentAccessKind.Enumeration, string.Empty, Collapse(match.Value)));
            }
            else if (name.Contains('$', StringComparison.Ordinal) || name.Contains('*', StringComparison.Ordinal))
            {
                findings.Add(new(WorkspaceScriptEnvironmentAccessKind.DynamicName, string.Empty, Collapse(match.Value)));
            }
            else
            {
                AddNamed(name, match.Value, isBaseName, findings);
            }
        }

        foreach (Match match in DotnetGetEnvironmentVariableRegex().Matches(script))
        {
            if (match.Groups["name"].Success)
            {
                AddNamed(match.Groups["name"].Value, match.Value, isBaseName, findings);
            }
            else
            {
                findings.Add(new(WorkspaceScriptEnvironmentAccessKind.DynamicName, string.Empty, Collapse(match.Value)));
            }
        }

        foreach (Match match in DotnetGetEnvironmentVariablesRegex().Matches(script))
        {
            findings.Add(new(WorkspaceScriptEnvironmentAccessKind.Enumeration, string.Empty, Collapse(match.Value)));
        }
    }

    private static void AnalyzePython(
        string script,
        Func<string, bool> isBaseName,
        List<WorkspaceScriptEnvironmentAccessFinding> findings)
    {
        foreach (Match match in PythonNamedReadRegex().Matches(script))
        {
            if (match.Groups["name"].Success)
            {
                AddNamed(match.Groups["name"].Value, match.Value, isBaseName, findings);
            }
            else
            {
                findings.Add(new(WorkspaceScriptEnvironmentAccessKind.DynamicName, string.Empty, Collapse(match.Value)));
            }
        }

        foreach (Match match in PythonEnumerationRegex().Matches(script))
        {
            findings.Add(new(WorkspaceScriptEnvironmentAccessKind.Enumeration, string.Empty, Collapse(match.Value)));
        }
    }

    private static void AddNamed(
        string name,
        string signal,
        Func<string, bool> isBaseName,
        List<WorkspaceScriptEnvironmentAccessFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(name) || isBaseName(name))
        {
            return;
        }

        findings.Add(new(WorkspaceScriptEnvironmentAccessKind.NamedRead, name, Collapse(signal)));
    }

    private static string StripPowerShellComments(string script)
        => StripLineComments(PowerShellBlockCommentRegex().Replace(script, " "));

    private static string StripLineComments(string script)
        => string.Join(
            '\n',
            script.Split('\n').Select(line => line.TrimStart().StartsWith('#') ? string.Empty : line));

    private static string Collapse(string value)
    {
        var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaximumSignalLength ? collapsed : collapsed[..MaximumSignalLength];
    }

    // $env:NAME and ${env:NAME}, but not a plain assignment ($env:NAME = ...), which only sets a variable.
    [GeneratedRegex(@"\$\{?env:(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\(x86\))?)(?![A-Za-z0-9_(])\}?(?!\s*=(?!=))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PowerShellEnvironmentVariableRegex();

    // Get-ChildItem/Get-Item/Get-Content (and aliases) on the env: drive: no name lists everything.
    [GeneratedRegex(@"\b(?:Get-ChildItem|Get-Item|Get-ItemProperty|Get-Content|gci|gi|gc|dir|ls|cat)\s+(?:-(?:Path|LiteralPath)\s+)?['""]?env:(?<name>[^\s'""|;)]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PowerShellEnvironmentProviderRegex();

    [GeneratedRegex(@"\[(?:System\.)?Environment\]::GetEnvironmentVariable\s*\(\s*(?:(['""])(?<name>[^'""]+)\1|[^)'""\s][^)]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DotnetGetEnvironmentVariableRegex();

    [GeneratedRegex(@"\[(?:System\.)?Environment\]::GetEnvironmentVariables\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DotnetGetEnvironmentVariablesRegex();

    [GeneratedRegex(@"<#[\s\S]*?#>", RegexOptions.CultureInvariant)]
    private static partial Regex PowerShellBlockCommentRegex();

    // os.environ['X'] (not a write), os.environ.get('X'), os.getenv('X'); a non-literal argument is dynamic.
    [GeneratedRegex(@"(?:\b(?:os\.)?environ\s*\[\s*(?:(['""])(?<name>[^'""]+)\1\s*\](?!\s*=(?!=))|[^'""\s\]][^\]]*\](?!\s*=(?!=)))|\b(?:os\.)?environ\.get\s*\(\s*(?:(['""])(?<name>[^'""]+)\2|[^'""\s)][^)]*)|\b(?:os\.)?getenv\s*\(\s*(?:(['""])(?<name>[^'""]+)\3|[^'""\s)][^)]*))", RegexOptions.CultureInvariant)]
    private static partial Regex PythonNamedReadRegex();

    // Dumping or iterating over the whole environment. Passing it on to a child process (env=os.environ,
    // os.environ.copy()) is not a read and stays allowed.
    [GeneratedRegex(@"\b(?:os\.)?environb?\.(?:items|keys|values)\s*\(|\bfor\s+\w+(?:\s*,\s*\w+)?\s+in\s+(?:os\.)?environb?\b(?!\s*[\[.])|\b(?:print|str|repr|list|sorted|set|len|pprint|pformat)\s*\(\s*(?:dict\s*\(\s*)?(?:os\.)?environb?\b(?!\s*[\[.])|\bjson\.dumps?\s*\(\s*(?:dict\s*\(\s*)?(?:os\.)?environb?\b(?!\s*[\[.])|(?<!env\s*=\s*)\bdict\s*\(\s*(?:os\.)?environb?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex PythonEnumerationRegex();
}
