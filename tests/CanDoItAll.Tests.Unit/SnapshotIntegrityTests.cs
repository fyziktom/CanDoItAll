using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class SnapshotIntegrityTests
{
    [Fact]
    public void Round4_required_artifacts_exist()
    {
        var root = FindRepositoryRoot();
        var requiredPaths = new[]
        {
            "docs/agent-recovery-stabilization.md",
            "docs/secure-configuration.md",
            "docs/testing.md",
            "src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs",
            "src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs",
            "src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs",
            "src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs",
            "tests/CanDoItAll.Tests.Unit/SecretScanningTests.cs",
            "tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs",
            "src/CanDoItAll.Modules.Processes/Runtime/ProcessOperatorControlPlane.cs",
            "src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsOperatorConsoleSection.razor",
            "tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs",
            "tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs",
            "tests/CanDoItAll.Tests.Integration/AgentRecoveryModelsTests.cs"
        };

        var missingPaths = requiredPaths
            .Where(path => !File.Exists(Path.Combine(root, Normalize(path))))
            .ToList();

        Assert.True(
            missingPaths.Count == 0,
            "Required round 4 implementation artifact(s) are missing: " + string.Join(", ", missingPaths));
    }

    [Fact]
    public void Current_execution_report_references_existing_files_and_tests()
    {
        var root = FindRepositoryRoot();
        var reportPath = Path.Combine(root, "01-execution-report.md");

        Assert.True(File.Exists(reportPath), "Current execution report 01-execution-report.md is missing.");

        var report = File.ReadAllText(reportPath);
        var providerKeyPattern = new Regex("sk-(proj-)?[A-Za-z0-9_-]{20,}", RegexOptions.Compiled);

        Assert.DoesNotMatch(providerKeyPattern, report);
        Assert.Contains("No tracked provider key pattern remains", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No raw secret value", report, StringComparison.OrdinalIgnoreCase);

        AssertReferencedPathsExist(root, report, "Files Changed");
        AssertReferencedPathsExist(root, report, "Tests Added Or Updated");
        Assert.Contains("dotnet test", report, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string Normalize(string relativePath)
    {
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static void AssertReferencedPathsExist(
        string root,
        string report,
        string sectionTitle)
    {
        var section = ExtractSection(report, sectionTitle);
        var paths = Regex.Matches(section, "`([^`]+)`")
            .Select(match => match.Groups[1].Value)
            .Where(value => value.Contains('/') || value.Contains('\\'))
            .Where(value => !value.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
            .Where(value => !value.StartsWith("git ", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(paths.Count > 0, $"Report section '{sectionTitle}' must list concrete file paths.");

        var missing = paths
            .Where(path => !File.Exists(Path.Combine(root, Normalize(path))))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Report section '{sectionTitle}' references missing file(s): {string.Join(", ", missing)}");
    }

    private static string ExtractSection(string report,
        string sectionTitle)
    {
        var pattern = $@"(?ms)^##\s+{Regex.Escape(sectionTitle)}\s*$([\s\S]*?)(?=^##\s+|\z)";
        var match = Regex.Match(report, pattern);

        Assert.True(match.Success, $"Report section '{sectionTitle}' is missing.");
        return match.Groups[1].Value;
    }
}
