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
            "tests/CanDoItAll.Tests.Integration/AgentRecoveryModelsTests.cs"
        };

        var missingPaths = requiredPaths
            .Where(path => !File.Exists(Path.Combine(root, Normalize(path))))
            .ToList();

        Assert.True(
            missingPaths.Count == 0,
            "Required round 4 implementation artifact(s) are missing: " + string.Join(", ", missingPaths));
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
}
