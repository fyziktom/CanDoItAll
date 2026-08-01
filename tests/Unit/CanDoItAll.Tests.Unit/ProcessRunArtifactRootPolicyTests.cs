using CanDoItAll.Processes.Application;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRunArtifactRootPolicyTests
{
    private static readonly Guid RunId = Guid.Parse("5b843f5c-a602-42dd-9407-7118f4a8a136");

    [Fact]
    public void Resolve_projects_managed_artifact_and_product_roots_for_the_current_run()
    {
        ProcessRunArtifactRootResolution artifact = ProcessRunArtifactRootPolicy.Resolve(
            $"artifacts/scopes/organization/org-1/process-runs/{RunId:D}/steps/result.md",
            RunId);
        ProcessRunArtifactRootResolution product = ProcessRunArtifactRootPolicy.Resolve(
            $"output/scopes/organization/org-1/process-runs/{RunId:N}/calculator/bin/app.dll",
            RunId);

        Assert.True(artifact.ShouldProject);
        Assert.Equal(
            $"artifacts/scopes/organization/org-1/process-runs/{RunId:D}",
            artifact.DirectoryPath);
        Assert.Equal(ProcessRunArtifactRootKind.ManagedArtifactRunRoot, artifact.Kind);
        Assert.True(product.ShouldProject);
        Assert.Equal(
            $"output/scopes/organization/org-1/process-runs/{RunId:N}/calculator",
            product.DirectoryPath);
        Assert.Equal(ProcessRunArtifactRootKind.ManagedProductOutputRoot, product.Kind);
    }

    [Fact]
    public void ResolveCurrentRunRoots_deduplicates_current_managed_artifact_and_product_roots()
    {
        IReadOnlyList<ProcessRunArtifactRootResolution> roots = ProcessRunArtifactRootPolicy.ResolveCurrentRunRoots(
            RunId,
            [
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ManagedArtifactRoot"] = $"artifacts/process-runs/{RunId:D}",
                    ["ProductRoot"] = $"output/process-runs/{RunId:D}/calculator",
                    ["OutputRoot"] = $"output/process-runs/{RunId:D}/calculator/bin/app.dll",
                    ["ExternalTargetRoot"] = @"C:\products\calculator"
                }
            ]);

        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, root => root.Kind == ProcessRunArtifactRootKind.ManagedArtifactRunRoot);
        Assert.Contains(roots, root =>
            root.Kind == ProcessRunArtifactRootKind.ManagedProductOutputRoot &&
            root.DirectoryPath.EndsWith("/calculator", StringComparison.Ordinal));
        Assert.DoesNotContain(roots, root => Path.IsPathRooted(root.DirectoryPath));
    }

    [Fact]
    public void ResolveCurrentRunRoots_rejects_more_than_the_bounded_root_count()
    {
        var variables = Enumerable.Range(0, ProcessRunArtifactRootPolicy.MaximumRootCount)
            .ToDictionary(
                index => $"Root{index}",
                index => $"output/process-runs/{RunId:D}/product-{index}",
                StringComparer.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessRunArtifactRootPolicy.ResolveCurrentRunRoots(RunId, [variables]));

        Assert.Contains(ProcessRunArtifactRootPolicy.MaximumRootCount.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../artifacts/process-runs/5b843f5c-a602-42dd-9407-7118f4a8a136")]
    [InlineData("artifacts/process-runs/../5b843f5c-a602-42dd-9407-7118f4a8a136")]
    [InlineData("C:\\managed-files\\artifacts\\process-runs\\5b843f5c-a602-42dd-9407-7118f4a8a136")]
    [InlineData("artifacts/process-runs/bf0b75ba-76fa-46eb-a04e-4d272092f235")]
    [InlineData("artifacts/process-runs/5b843f5c-a602-42dd-9407-7118f4a8a136/../../secret")]
    [InlineData("secret/process-runs/5b843f5c-a602-42dd-9407-7118f4a8a136")]
    [InlineData("launch/5b843f5c-a602-42dd-9407-7118f4a8a136")]
    public void Resolve_rejects_escaped_absolute_and_wrong_run_roots(string path)
    {
        ProcessRunArtifactRootResolution resolution = ProcessRunArtifactRootPolicy.Resolve(path, RunId);

        Assert.False(resolution.ShouldProject);
        Assert.Equal(ProcessRunArtifactRootKind.Ignored, resolution.Kind);
        Assert.NotEmpty(resolution.IgnoreReason);
    }

    [Fact]
    public void Workbench_consumes_process_owned_policy_without_defining_process_root_semantics()
    {
        string repositoryRoot = FindRepositoryRoot();
        string contributor = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureProcessProjectionContributor.cs"));
        string workbenchPolicyPath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureProcessRunFolderProjectionPolicy.cs");

        Assert.Contains("ProcessRunArtifactRootPolicy.Resolve", contributor, StringComparison.Ordinal);
        Assert.False(File.Exists(workbenchPolicyPath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate the CanDoItAll repository root.");
    }
}
