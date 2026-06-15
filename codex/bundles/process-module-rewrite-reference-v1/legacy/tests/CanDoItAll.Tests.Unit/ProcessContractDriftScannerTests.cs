using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

public sealed partial class ProcessContractDriftScannerTests
{
    [Fact]
    public void Process_operation_contract_names_match_runtime_enums()
    {
        Assert.Equal(Enum.GetNames<ProcessStepOperation>(), ProcessOperationContractNames.AllOperations);
        Assert.Equal(Enum.GetNames<ProcessStepTargetScope>(), ProcessOperationContractNames.AllTargetScopes);

        Assert.Empty(ProcessContractCatalog.FindUnknownOperationNames(ProcessOperationContractNames.AllOperations));
    }

    [Fact]
    public void Tool_contract_catalog_contains_process_runtime_tool_surfaces()
    {
        foreach (var toolName in new[]
                 {
                     ToolContractCatalog.WorkspaceDotNetRun,
                     ToolContractCatalog.WorkspaceDotNetNew,
                     ToolContractCatalog.BrowserTakeScreenshot,
                     ToolContractCatalog.BrowserSnapshot,
                     ToolContractCatalog.BrowserConsoleMessages,
                     AgentToolInvocationPolicyMetadata.ProcessesRunStart,
                     AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate
                 })
        {
            Assert.True(ToolContractCatalog.IsKnownToolName(toolName), $"Tool '{toolName}' is missing from the canonical tool catalog.");
        }
    }

    [Fact]
    public void Scanner_rejects_unowned_internal_tool_id()
    {
        const string source = """
            internal static class BadPolicy {
                private const string ToolName = "workspace_destroy_everything";
            }
            """;

        var findings = ContractDriftScanner.ScanText(
            "src/CanDoItAll.Modules.Processes/FakePolicy.cs",
            source);

        var finding = Assert.Single(findings);
        Assert.Equal("workspace_destroy_everything", finding.Value);
        Assert.Equal(ContractLiteralClassification.InternalCanonical, finding.Classification);
    }

    [Fact]
    public void Scanner_accepts_classified_external_template_and_test_literals()
    {
        const string templateJson = """
            {
              "ExecutorId": "office365.messages-by-category",
              "Category": "CanDoItAllSummaryTest",
              "MessagePath": "$.inputPayload.runContext.office365Processing.messageIds[0]",
              "AllowedOperations": [ "ReadProcessContext", "CaptureRuntimeProof" ],
              "OperationTargetScope": "ExternalProductTargetReadOnly"
            }
            """;

        var templateFindings = ContractDriftScanner.ScanText(
            "Templates/Processes/processes/example/definition.json",
            templateJson);

        Assert.Empty(templateFindings);

        const string testFixture = """
            Assert.Equal("workspace_fixture_only", actual.ToolName);
            Assert.Equal("missing.executor", actual.ExecutorId);
            """;

        var fixtureFindings = ContractDriftScanner.ScanText(
            "tests/CanDoItAll.Tests.Unit/FakeFixtureTests.cs",
            testFixture);

        Assert.Empty(fixtureFindings);
    }

    [Fact]
    public void Scoped_repository_contract_drift_scan_has_no_unowned_internal_ids()
    {
        var root = FindRepositoryRoot();
        var files = ResolveScopedScanFiles(root);

        var findings = files
            .SelectMany(file => ContractDriftScanner.ScanFile(root, file))
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Process_template_operation_ids_are_known()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, "Templates", "Processes", "processes", "software-delivery", "definition.json");
        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));

        var unknownOperations = document.RootElement
            .GetProperty("Steps")
            .EnumerateArray()
            .SelectMany(step => step.TryGetProperty("AllowedOperations", out var operations)
                ? operations.EnumerateArray().Select(operation => operation.GetString() ?? string.Empty)
                : [])
            .Where(operation => !ProcessContractCatalog.IsKnownOperationName(operation))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unknownOperations);
    }

    private static IReadOnlyList<string> ResolveScopedScanFiles(string root)
    {
        var dispatchDirectory = Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch");
        var files = Directory
            .EnumerateFiles(dispatchDirectory, "ProcessRunAutomationDispatchService*.cs")
            .Concat(
            [
                Path.Combine(root, "src", "CanDoItAll.AgentFramework.Core", "ToolPolicy", "AgentToolInvocationPolicy.cs"),
                Path.Combine(root, "src", "CanDoItAll.AgentFramework.Core", "ToolPolicy", "ToolContractCatalog.cs"),
                Path.Combine(root, "src", "CanDoItAll.Modules.AgentFramework", "Pages", "Components", "WorkflowCanvasEditor.razor.cs"),
                Path.Combine(root, "Templates", "Processes", "processes", "software-delivery", "definition.json"),
                Path.Combine(root, "Templates", "Processes", "seed-catalog", "baseline-scenarios.json"),
                Path.Combine(root, "codex", "skills", "candoitall-api-processes", "SKILL.md"),
                Path.Combine(root, "codex", "skills", "candoitall-api-workflows", "SKILL.md")
            ])
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(files);
        return files;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[]
                 {
                     AppContext.BaseDirectory,
                     Directory.GetCurrentDirectory(),
                     Path.GetDirectoryName(sourceFilePath) ?? string.Empty
                 })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}

internal enum ContractLiteralClassification
{
    InternalCanonical,
    ExternalBoundary,
    TemplateContent,
    TestFixture
}

internal sealed record ContractDriftFinding(
    string Path,
    string Value,
    ContractLiteralClassification Classification,
    string Reason);

internal static partial class ContractDriftScanner
{
    private static readonly Regex StringLiteralRegex = CreateStringLiteralRegex();
    private static readonly Regex InternalToolIdRegex = CreateInternalToolIdRegex();
    private static readonly Regex JsonPathRegex = CreateJsonPathRegex();

    private static readonly string[] KnownToolPrefixes =
    [
        "workspace_dotnet_"
    ];

    private static readonly string[] KnownExternalExecutorIds =
    [
        "office365.messages-by-category",
        "office365.message-by-address-unprocessed",
        "office365.mark-message-processed",
        "gmail.messages-by-label",
        "gmail.mark-message-processed"
    ];

    private static readonly string[] TestOnlyLiterals =
    [
        "CanDoItAllSummaryTest",
        "CanDoItAllSummaryTestProcessed"
    ];

    public static IReadOnlyList<ContractDriftFinding> ScanFile(string root, string path)
    {
        var text = File.ReadAllText(path);
        var relativePath = Path.GetRelativePath(root, path);
        return ScanText(relativePath, text);
    }

    public static IReadOnlyList<ContractDriftFinding> ScanText(string path, string text)
    {
        var classification = ClassifyPath(path);
        if (classification == ContractLiteralClassification.TestFixture)
        {
            return [];
        }

        var findings = new List<ContractDriftFinding>();
        foreach (var value in ExtractStringLiteralValues(text))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsClassifiedExternalOrTemplateLiteral(value, classification))
            {
                continue;
            }

            if (InternalToolIdRegex.IsMatch(value) &&
                !ToolContractCatalog.IsKnownToolName(value) &&
                !KnownToolPrefixes.Contains(value, StringComparer.Ordinal))
            {
                findings.Add(new ContractDriftFinding(
                    path,
                    value,
                    ContractLiteralClassification.InternalCanonical,
                    "Internal tool id is not present in ToolContractCatalog."));
                continue;
            }

            if (classification == ContractLiteralClassification.InternalCanonical &&
                LooksLikeProcessOperationOrScope(value) &&
                !ProcessOperationContractNames.IsOperationName(value) &&
                !ProcessOperationContractNames.IsTargetScopeName(value))
            {
                findings.Add(new ContractDriftFinding(
                    path,
                    value,
                    ContractLiteralClassification.InternalCanonical,
                    "Process operation or target scope literal is not present in ProcessOperationContractNames."));
                continue;
            }

            if (JsonPathRegex.IsMatch(value) &&
                !WorkflowJsonPathContractNames.IsKnownPath(value) &&
                classification == ContractLiteralClassification.InternalCanonical)
            {
                findings.Add(new ContractDriftFinding(
                    path,
                    value,
                    ContractLiteralClassification.InternalCanonical,
                    "Internal JSON path literal is not present in WorkflowJsonPathContractNames."));
            }
        }

        return findings;
    }

    private static bool IsClassifiedExternalOrTemplateLiteral(
        string value,
        ContractLiteralClassification classification)
    {
        if (KnownExternalExecutorIds.Contains(value, StringComparer.Ordinal))
        {
            return true;
        }

        if (TestOnlyLiterals.Contains(value, StringComparer.Ordinal))
        {
            return true;
        }

        if (classification is ContractLiteralClassification.TemplateContent or ContractLiteralClassification.ExternalBoundary &&
            value.StartsWith("$.", StringComparison.Ordinal))
        {
            return WorkflowJsonPathContractNames.IsKnownPath(value) ||
                   value.StartsWith("$.inputPayload.", StringComparison.Ordinal) ||
                   value.StartsWith("$.tasks", StringComparison.Ordinal);
        }

        return false;
    }

    private static bool LooksLikeProcessOperationOrScope(string value)
    {
        return value is
            "ReadProcessContext" or
            "ReadProjectStructure" or
            "ReadUpstreamArtifacts" or
            "WriteManagedProcessArtifacts" or
            "WriteExternalArtifactDestination" or
            "MutateProductTarget" or
            "RunValidation" or
            "LaunchRuntime" or
            "CaptureRuntimeProof" or
            "ExecuteExternalAction" or
            "RecoverArtifactsOnly" or
            "EscalateOrDecide" or
            "ManagedProcessArtifactsOnly" or
            "ManagedOutputProduct" or
            "ExternalArtifactDestination" or
            "ExternalProductTargetReadOnly" or
            "ExternalProductTargetMutable" or
            "ExternalActionControlled" ||
            Regex.IsMatch(value, @"^(?:[A-Z][a-z]+)+(?:Operation|Target|Scope)$", RegexOptions.CultureInvariant);
    }

    private static ContractLiteralClassification ClassifyPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
        {
            return ContractLiteralClassification.TestFixture;
        }

        if (normalized.StartsWith("Templates/", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return ContractLiteralClassification.TemplateContent;
        }

        if (normalized.StartsWith("codex/skills/", StringComparison.OrdinalIgnoreCase))
        {
            return ContractLiteralClassification.ExternalBoundary;
        }

        return ContractLiteralClassification.InternalCanonical;
    }

    private static IEnumerable<string> ExtractStringLiteralValues(string text)
    {
        foreach (Match match in StringLiteralRegex.Matches(text))
        {
            var value = match.Groups["verbatim"].Success
                ? match.Groups["verbatim"].Value.Replace("\"\"", "\"", StringComparison.Ordinal)
                : DecodeRegularStringLiteral(match.Groups["regular"].Value);
            yield return value;
        }
    }

    private static string DecodeRegularStringLiteral(string value)
    {
        return value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
    }

    [GeneratedRegex("@\"(?<verbatim>(?:\"\"|[^\"])*?)\"|\"(?<regular>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex CreateStringLiteralRegex();

    [GeneratedRegex("^(?:workspace|browser|project_structure|image_generation|processes)_[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateInternalToolIdRegex();

    [GeneratedRegex("^\\$\\.[A-Za-z0-9_\\[\\].{}-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateJsonPathRegex();
}
