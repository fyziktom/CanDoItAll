using System.Runtime.CompilerServices;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeHostCodeFirstGuardTests
{
    [Fact]
    public void Process_runtime_host_codefirst_SB01_INV_001_source_inventory_is_not_bundle_path_coupled()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = new[]
        {
            Path.Combine(root, "src", "CanDoItAll.Processes.Contracts", "Runtime", "ProcessRuntimeHostContractModels.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationHostCapabilityCatalog.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationRuntimeHost.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessVerificationAuditStore.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessManagerReadOnlyVerificationCommandService.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessReadOnlyVerificationJobRunner.cs"),
            Path.Combine(root, "src", "CanDoItAll.Modules.Processes", "Automation", "Dispatch", "ProcessDryRunExecutionHost.cs")
        };
        var missingFiles = sourceFiles
            .Where(path => !File.Exists(path))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        var bundleCoupledFiles = sourceFiles
            .Where(path => File.Exists(path))
            .Where(path => File.ReadAllText(path).Contains("codex/bundles", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(missingFiles);
        Assert.Empty(bundleCoupledFiles);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
