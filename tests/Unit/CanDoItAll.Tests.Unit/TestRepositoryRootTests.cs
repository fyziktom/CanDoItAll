using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class TestRepositoryRootTests : IDisposable
{
    private readonly string sandbox = Path.Combine(
        Path.GetTempPath(),
        "candoitall repository root tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Current_process_resolves_this_checkout()
    {
        string root = TestRepositoryRoot.Find();

        Assert.True(TestRepositoryRoot.IsRepositoryRoot(root));
        Assert.True(Directory.Exists(Path.Combine(root, "tests", "Unit", "CanDoItAll.Tests.Unit")));
    }

    [Fact]
    public void Explicit_root_with_spaces_and_forward_slashes_is_used_as_given()
    {
        string root = CreateCheckout("explicit root with spaces");
        string unrelatedStart = Directory.CreateDirectory(Path.Combine(sandbox, "unrelated", "bin")).FullName;

        string resolved = TestRepositoryRoot.Resolve(root.Replace('\\', '/') + "/", unrelatedStart);

        Assert.Equal(Path.GetFullPath(root), resolved);
    }

    [Fact]
    public void Explicit_root_that_is_not_a_complete_checkout_fails_instead_of_walking_elsewhere()
    {
        string validAncestor = CreateCheckout("valid");
        string decoy = Directory.CreateDirectory(Path.Combine(validAncestor, "decoy")).FullName;
        File.WriteAllText(Path.Combine(decoy, "CanDoItAll.slnx"), "<Solution />");
        string start = Directory.CreateDirectory(Path.Combine(decoy, "bin")).FullName;

        Assert.Throws<InvalidOperationException>(() => TestRepositoryRoot.Resolve(decoy, start));
    }

    [Fact]
    public void Isolated_output_directory_resolves_to_its_owning_checkout()
    {
        string root = CreateCheckout("checkout");
        string output = Directory.CreateDirectory(Path.Combine(
            root, "artifacts", "mdo storage", "bin", "CanDoItAll.Tests.Unit", "release")).FullName;

        Assert.Equal(Path.GetFullPath(root), TestRepositoryRoot.Resolve(null, output));
    }

    [Fact]
    public void Nested_worktree_wins_over_an_enclosing_checkout()
    {
        string outer = CreateCheckout("outer");
        string worktree = CreateCheckout(Path.Combine("outer", ".worktrees", "feature"));
        string output = Directory.CreateDirectory(Path.Combine(worktree, "tests", "Unit", "bin", "Release")).FullName;

        Assert.Equal(Path.GetFullPath(worktree), TestRepositoryRoot.Resolve(" ", output));
        Assert.NotEqual(Path.GetFullPath(outer), TestRepositoryRoot.Resolve(null, output));
    }

    [Fact]
    public void Directory_with_only_a_solution_file_is_skipped_and_a_missing_checkout_fails()
    {
        string decoy = Directory.CreateDirectory(Path.Combine(sandbox, "solution only")).FullName;
        File.WriteAllText(Path.Combine(decoy, "CanDoItAll.slnx"), "<Solution />");
        string start = Directory.CreateDirectory(Path.Combine(decoy, "bin", "Release")).FullName;

        Assert.False(TestRepositoryRoot.IsRepositoryRoot(decoy));
        if (!AncestorsContainCheckout(start))
        {
            Assert.Throws<DirectoryNotFoundException>(() => TestRepositoryRoot.Resolve(null, start));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(sandbox))
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    private string CreateCheckout(string relativeRoot)
    {
        string root = Directory.CreateDirectory(Path.Combine(sandbox, relativeRoot)).FullName;
        File.WriteAllText(Path.Combine(root, "CanDoItAll.slnx"), "<Solution />");
        string webProject = Path.Combine(root, "src", "App", "CanDoItAll.Web", "CanDoItAll.Web.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(webProject)!);
        File.WriteAllText(webProject, "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        return root;
    }

    private static bool AncestorsContainCheckout(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (TestRepositoryRoot.IsRepositoryRoot(directory.FullName))
            {
                return true;
            }
        }

        return false;
    }
}
