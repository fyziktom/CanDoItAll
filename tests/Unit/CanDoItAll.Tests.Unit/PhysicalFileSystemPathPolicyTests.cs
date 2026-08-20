using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Unit.Storage;

[Trait("Category", "UnixPortabilityCore")]
public sealed class PhysicalFileSystemPathPolicyTests
{
    [Fact]
    public void Sensitive_root_does_not_authorize_case_distinct_sibling()
    {
        using var directory = new TemporaryDirectory();
        var policy = new PhysicalFileSystemPathPolicy(
            directory.Path,
            PhysicalFileSystemCaseSensitivity.Sensitive);
        string caseDistinctPath = ChangeCaseOfLeaf(directory.Path);

        Assert.False(policy.IsWithinRoot(caseDistinctPath));
        Assert.Equal(StringComparer.Ordinal, policy.PathComparer);
    }

    [Fact]
    public void Insensitive_root_authorizes_case_variant_of_same_path()
    {
        using var directory = new TemporaryDirectory();
        var policy = new PhysicalFileSystemPathPolicy(
            directory.Path,
            PhysicalFileSystemCaseSensitivity.Insensitive);
        string caseVariantPath = ChangeCaseOfLeaf(directory.Path);

        Assert.True(policy.IsWithinRoot(caseVariantPath));
        Assert.Equal(StringComparer.OrdinalIgnoreCase, policy.PathComparer);
    }

    [Fact]
    public void Unknown_root_uses_conservative_ordinal_comparison()
    {
        using var directory = new TemporaryDirectory();
        var policy = new PhysicalFileSystemPathPolicy(
            directory.Path,
            PhysicalFileSystemCaseSensitivity.Unknown);

        Assert.Equal(StringComparer.Ordinal, policy.PathComparer);
        Assert.False(policy.IsWithinRoot(ChangeCaseOfLeaf(directory.Path)));
    }

    [Fact]
    public void Factory_probes_writable_root_and_cleans_probe_file()
    {
        using var directory = new TemporaryDirectory();
        var factory = new PhysicalFileSystemPathPolicyFactory();

        IPhysicalFileSystemPathPolicy policy = factory.Create(directory.Path);

        Assert.NotEqual(PhysicalFileSystemCaseSensitivity.Unknown, policy.CaseSensitivity);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, ".candoitall-case-probe-*"));
    }

    [Fact]
    public void ResolveContainedPath_rejects_parent_escape()
    {
        using var directory = new TemporaryDirectory();
        var policy = new PhysicalFileSystemPathPolicy(
            directory.Path,
            PhysicalFileSystemCaseSensitivity.Sensitive);

        var exception = Assert.Throws<PhysicalPathValidationException>(() =>
            policy.ResolveContainedPath(Path.Combine("..", "outside.txt")));

        Assert.Equal(PhysicalPathValidationErrorCode.OutsideRoot, exception.ErrorCode);
    }

    [Fact]
    public void ResolveContainedPath_allows_missing_leaf_below_verified_parent()
    {
        using var directory = new TemporaryDirectory();
        var policy = new PhysicalFileSystemPathPolicy(
            directory.Path,
            PhysicalFileSystemCaseSensitivity.Sensitive);

        string resolved = policy.ResolveContainedPath(Path.Combine("missing", "leaf.txt"));

        Assert.Equal(Path.Combine(directory.Path, "missing", "leaf.txt"), resolved);
    }

    [Fact]
    public void Constructor_allows_the_macos_system_var_alias()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string managedRoot = Path.Combine("/var", $"candoitall-policy-{Guid.NewGuid():N}");

        var policy = new PhysicalFileSystemPathPolicy(managedRoot);

        Assert.Equal(managedRoot, policy.RootPath);
    }

    [Fact]
    public void Policy_rejects_symbolic_link_ancestor()
    {
        using var managed = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        string linkPath = Path.Combine(managed.Path, "linked");
        if (!TryCreateDirectoryLink(linkPath, outside.Path))
        {
            return;
        }

        var policy = new PhysicalFileSystemPathPolicy(
            managed.Path,
            PhysicalFileSystemCaseSensitivity.Sensitive);

        var exception = Assert.Throws<PhysicalPathValidationException>(() =>
            policy.ResolveContainedPath(Path.Combine("linked", "secret.txt")));

        Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal, exception.ErrorCode);
    }

    [Fact]
    public void Factory_rejects_managed_root_that_is_a_symbolic_link()
    {
        using var parent = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        string linkPath = Path.Combine(parent.Path, "managed-link");
        if (!TryCreateDirectoryLink(linkPath, outside.Path))
        {
            return;
        }

        var exception = Assert.Throws<PhysicalPathValidationException>(() =>
            new PhysicalFileSystemPathPolicyFactory().Create(linkPath));

        Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal, exception.ErrorCode);
    }

    [Fact]
    public void Mutation_revalidation_rejects_parent_replaced_by_symbolic_link()
    {
        using var managed = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        string parentPath = Path.Combine(managed.Path, "target");
        Directory.CreateDirectory(parentPath);
        var policy = new PhysicalFileSystemPathPolicy(
            managed.Path,
            PhysicalFileSystemCaseSensitivity.Sensitive);
        string targetPath = policy.ResolveContainedPath(Path.Combine("target", "file.txt"));
        Directory.Delete(parentPath);
        if (!TryCreateDirectoryLink(parentPath, outside.Path))
        {
            return;
        }

        var exception = Assert.Throws<PhysicalPathValidationException>(() =>
            policy.RevalidateMutationTarget(targetPath));

        Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal, exception.ErrorCode);
    }

    private static string ChangeCaseOfLeaf(string path)
    {
        string leaf = Path.GetFileName(path);
        string changedLeaf = leaf.Any(char.IsLower)
            ? leaf.ToUpperInvariant()
            : leaf.ToLowerInvariant();
        return Path.Combine(Path.GetDirectoryName(path)!, changedLeaf);
    }

    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"candoitall-physical-policy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
