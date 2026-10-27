using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Unit.Storage;

[Trait("Category", "UnixPortabilityCore")]
public sealed class PhysicalFileSystemPathPolicyTests
{
    [Theory]
    [InlineData(PhysicalFileSystemCaseSensitivity.Sensitive)]
    [InlineData(PhysicalFileSystemCaseSensitivity.Insensitive)]
    public void Configured_comparison_is_explicit_even_before_the_root_exists(PhysicalFileSystemCaseSensitivity comparisonMode) {
        using var directory = new TemporaryDirectory();
        var root = Path.Combine(directory.Path, "missing-root");
        var factory = new PhysicalFileSystemPathPolicyFactory(comparisonMode);
        var policy = factory.Create(root);

        Assert.Equal(comparisonMode, policy.CaseSensitivity);
        Assert.False(Directory.Exists(root));
        Assert.Equal(comparisonMode == PhysicalFileSystemCaseSensitivity.Insensitive, policy.IsWithinRoot(ChangeCaseOfLeaf(root)));
        Assert.Equal(Path.Combine(root, "record.json"), policy.ResolveContainedPath("record.json"));
        Assert.Throws<PhysicalPathValidationException>(() => policy.ResolveContainedPath(Path.Combine("..", "outside.json")));
        Assert.Equal(PhysicalFileSystemCaseSensitivity.Sensitive,
            factory.Create(root, PhysicalFileSystemCaseSensitivity.Sensitive).CaseSensitivity);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Theory]
    [InlineData(PhysicalFileSystemCaseSensitivity.Unknown)]
    [InlineData((PhysicalFileSystemCaseSensitivity)int.MaxValue)]
    public void Configured_factory_rejects_unknown_or_invalid_comparison(PhysicalFileSystemCaseSensitivity comparisonMode) {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicalFileSystemPathPolicyFactory(comparisonMode));
    }

    [Fact]
    public void Exact_factory_revalidates_replaced_parent_instead_of_caching_path_safety() {
        using var managed = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var parent = Path.Combine(managed.Path, "parent");
        Directory.CreateDirectory(parent);
        var factory = new PhysicalFileSystemPathPolicyFactory(PhysicalFileSystemCaseSensitivity.Sensitive);
        var original = factory.Create(managed.Path);
        var target = original.ResolveContainedPath(Path.Combine(parent, "record.json"));
        Directory.Delete(parent);
        Directory.CreateSymbolicLink(parent, outside.Path);

        var existing = Assert.Throws<PhysicalPathValidationException>(() => original.RevalidateMutationTarget(target));
        var fresh = Assert.Throws<PhysicalPathValidationException>(() => factory.Create(managed.Path).RevalidateMutationTarget(target));
        Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal, existing.ErrorCode);
        Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal, fresh.ErrorCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside.Path));
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mutation_revalidation_rejects_existing_and_dangling_leaf_links(bool dangling) {
        using var managed = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var outsidePath = Path.Combine(outside.Path, "record.json");
        if (!dangling) {
            File.WriteAllText(outsidePath, "outside");
        }
        var target = Path.Combine(managed.Path, "record.json");
        File.CreateSymbolicLink(target, outsidePath);
        var policy = new PhysicalFileSystemPathPolicyFactory(PhysicalFileSystemCaseSensitivity.Sensitive).Create(managed.Path);

        var failure = Assert.Throws<PhysicalPathValidationException>(() => policy.RevalidateMutationTarget(target));

        Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal, failure.ErrorCode);
        Assert.Equal(!dangling, File.Exists(outsidePath));
        if (!dangling) {
            Assert.Equal("outside", File.ReadAllText(outsidePath));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mutation_revalidation_requires_an_existing_directory_parent(bool parentIsFile) {
        using var managed = new TemporaryDirectory();
        var parent = Path.Combine(managed.Path, "parent");
        if (parentIsFile) {
            File.WriteAllText(parent, "parent is a file");
        }
        var policy = new PhysicalFileSystemPathPolicyFactory(PhysicalFileSystemCaseSensitivity.Sensitive).Create(managed.Path);
        var target = Path.Combine(parent, "record.json");

        var failure = Assert.Throws<PhysicalPathValidationException>(() => policy.RevalidateMutationTarget(target));

        Assert.Equal(PhysicalPathValidationErrorCode.InvalidPath, failure.ErrorCode);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void Mutation_revalidation_rejects_a_replaced_ancestor_on_the_same_policy() {
        using var managed = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var ancestor = Path.Combine(managed.Path, "first");
        var parent = Path.Combine(ancestor, "second");
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(Path.Combine(outside.Path, "second"));
        var target = Path.Combine(parent, "record.json");
        var policy = new PhysicalFileSystemPathPolicyFactory(PhysicalFileSystemCaseSensitivity.Sensitive).Create(managed.Path);
        policy.RevalidateMutationTarget(target);
        Directory.Move(ancestor, Path.Combine(managed.Path, "retired"));
        Directory.CreateSymbolicLink(ancestor, outside.Path);

        var failure = Assert.Throws<PhysicalPathValidationException>(() => policy.RevalidateMutationTarget(target));

        Assert.Equal(PhysicalPathValidationErrorCode.LinkTraversal, failure.ErrorCode);
        Assert.False(File.Exists(Path.Combine(outside.Path, "second", "record.json")));
    }

    [Fact]
    public void Mutation_revalidation_does_not_authorize_the_managed_root_parent() {
        using var managed = new TemporaryDirectory();
        var policy = new PhysicalFileSystemPathPolicyFactory(PhysicalFileSystemCaseSensitivity.Sensitive).Create(managed.Path);

        var failure = Assert.Throws<PhysicalPathValidationException>(() => policy.RevalidateMutationTarget(managed.Path));

        Assert.Equal(PhysicalPathValidationErrorCode.OutsideRoot, failure.ErrorCode);
        Assert.True(Directory.Exists(managed.Path));
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
