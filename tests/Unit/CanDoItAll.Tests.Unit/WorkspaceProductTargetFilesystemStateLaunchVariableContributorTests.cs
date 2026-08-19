using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceProductTargetFilesystemStateLaunchVariableContributorTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"CanDoItAll.ProductTargetState.{Guid.NewGuid():N}");

    [Fact]
    public void Enrich_reports_missing_target_from_product_root()
    {
        var targetRoot = Path.Combine(root, "products", "missing");

        var state = Enrich(targetRoot);

        Assert.Equal("missing", state);
    }

    [Fact]
    public void Enrich_reports_empty_directory_target()
    {
        var targetRoot = Path.Combine(root, "products", "empty");
        Directory.CreateDirectory(targetRoot);

        var state = Enrich(targetRoot);

        Assert.Equal("empty", state);
    }

    [Fact]
    public void Enrich_reports_populated_directory_target()
    {
        var targetRoot = Path.Combine(root, "products", "populated");
        Directory.CreateDirectory(targetRoot);
        File.WriteAllText(Path.Combine(targetRoot, "baseline.txt"), "baseline");

        var state = Enrich(targetRoot);

        Assert.Equal("populated", state);
    }

    [Fact]
    public void Enrich_reports_file_target_as_not_directory()
    {
        var targetRoot = Path.Combine(root, "products", "file-target");
        Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
        File.WriteAllText(targetRoot, "not a directory");

        var state = Enrich(targetRoot);

        Assert.Equal("not-directory", state);
    }

    [Fact]
    public void Enrich_reports_unavailable_for_an_invalid_external_target_alias()
    {
        Directory.CreateDirectory(root);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRootAlias"] = "external-target/invalid"
        };
        var contributor = new WorkspaceProductTargetFilesystemStateLaunchVariableContributor(
            TestWorkspaceServices.CreateFileService(root),
            TestExternalTargetPathRegistry.Create());

        contributor.Enrich(CreateContext(), variables);

        Assert.Equal("unavailable", variables[WorkspaceProductTargetFilesystemStateLaunchVariableContributor.VariableName]);
    }

    [Fact]
    public void Enrich_does_not_emit_a_state_without_a_grounded_target()
    {
        Directory.CreateDirectory(root);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contributor = new WorkspaceProductTargetFilesystemStateLaunchVariableContributor(
            TestWorkspaceServices.CreateFileService(root),
            TestExternalTargetPathRegistry.Create());

        contributor.Enrich(CreateContext(), variables);

        Assert.DoesNotContain(
            WorkspaceProductTargetFilesystemStateLaunchVariableContributor.VariableName,
            variables.Keys,
            StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string Enrich(string targetRoot)
    {
        Directory.CreateDirectory(root);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductRoot"] = targetRoot
        };
        var externalTargets = TestExternalTargetPathRegistry.Create();
        var contributor = new WorkspaceProductTargetFilesystemStateLaunchVariableContributor(
            TestWorkspaceServices.CreateFileService(
                Path.Combine(root, "workspace"),
                externalTargetRegistry: externalTargets),
            externalTargets);

        contributor.Enrich(CreateContext(), variables);

        return variables[WorkspaceProductTargetFilesystemStateLaunchVariableContributor.VariableName];
    }

    private static ProcessLaunchPreparationContext CreateContext()
    {
        var source = new ProcessLaunchSourceItem(
            "test",
            "Test source",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            ProcessLaunchSourceItemKind.Other,
            IsIncludedInProcessContext: true);
        return new ProcessLaunchPreparationContext(
            "generic-process",
            IsSubprocess: false,
            new ProcessLaunchSourceSnapshot(Guid.NewGuid(), "Test", source, [source], string.Empty));
    }
}
