using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessProductRootResolverPortabilityTests
{
    [Fact]
    public void Inspectable_product_root_rejects_foreign_host_absolute_syntax()
    {
        var foreignRoot = OperatingSystem.IsWindows()
            ? "/tmp/process-product-root"
            : @"C:\process-product-root";
        var variables = new Dictionary<string, string>
        {
            ["ProductRoot"] = foreignRoot
        };

        Assert.False(ProcessProductRootResolver.TryResolveInspectableProductRoot(variables, out var resolved));
        Assert.Equal(string.Empty, resolved);
    }

    [Fact]
    public void Product_root_containment_uses_host_case_semantics()
    {
        var root = TestFileSystem.CreateTemporaryRoot("process-product-case");
        try
        {
            var differentlyCasedCandidate = Path.Combine(root.ToUpperInvariant(), "child");

            Assert.Equal(
                OperatingSystem.IsWindows(),
                ProcessProductRootResolver.IsSameOrChildPath(root, differentlyCasedCandidate));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void Required_product_path_rejects_unresolved_versioned_alias_explicitly()
    {
        var root = TestFileSystem.CreateTemporaryRoot("process-product-alias");
        try
        {
            var alias = "external-target/v1/0123456789abcdef01234567/child";

            Assert.False(ProcessProductRootResolver.TryResolveRequiredProductPath(
                root,
                alias,
                out var resolved,
                out var invalidReason));
            Assert.Equal(string.Empty, resolved);
            Assert.Contains("external-target alias", invalidReason, StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Fact]
    public void Required_product_path_preserves_a_unix_backslash_filename()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = TestFileSystem.CreateTemporaryRoot("process-product-backslash");
        try
        {
            const string fileName = @"file\name.txt";

            Assert.True(ProcessProductRootResolver.TryResolveRequiredProductPath(
                root,
                fileName,
                out var resolved,
                out var invalidReason),
                invalidReason);
            Assert.Equal(Path.Combine(root, fileName), resolved);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }
}
