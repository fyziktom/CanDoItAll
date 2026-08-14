using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
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

        var resolution = ProcessProductRootResolver.ResolveInspectableProductRoot(variables);

        Assert.Equal(ProcessProductRootResolutionKind.Invalid, resolution.Kind);
        Assert.Equal(string.Empty, resolution.ProductRoot);
    }

    [Fact]
    public void Required_product_path_preserves_versioned_alias_for_owner_resolution()
    {
        const string root = "external-target/v1/0123456789abcdef01234567";
        const string alias = $"{root}/child";

        Assert.True(ProcessProductRootResolver.TryResolveRequiredProductPath(
            root,
            alias,
            out var resolved,
            out var invalidReason),
            invalidReason);
        Assert.Equal(alias, resolved);
    }

    [Fact]
    public void Required_product_path_preserves_host_native_absolute_path_for_authority_scoped_inspection()
    {
        const string productRootAlias = "external-target/v1/0123456789abcdef01234567";
        var nativePath = Path.Combine(
            TestFileSystem.CreateTemporaryRoot("process-product-native-candidate"),
            "product.txt");
        try
        {
            Assert.True(ProcessProductRootResolver.TryResolveRequiredProductPath(
                productRootAlias,
                nativePath,
                out var resolved,
                out var invalidReason),
                invalidReason);
            Assert.Equal(Path.GetFullPath(nativePath), resolved);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(Path.GetDirectoryName(nativePath)!);
        }
    }

    [Fact]
    public void Required_product_path_preserves_a_unix_backslash_filename()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const string rootId = "0123456789abcdef01234567";
        var rootAlias = ExternalTargetAliasCodec.BuildAliasRoot(rootId);
        const string fileName = @"file\name.txt";

        Assert.True(ProcessProductRootResolver.TryResolveRequiredProductPath(
            rootAlias,
            fileName,
            out var resolved,
            out var invalidReason),
            invalidReason);
        Assert.Equal(ExternalTargetAliasCodec.BuildAlias(rootId, [fileName]), resolved);
    }

    [Fact]
    public void Required_product_path_rejects_segments_that_cannot_be_encoded_safely()
    {
        const string productRootAlias = "external-target/v1/0123456789abcdef01234567";
        var invalidPaths = new[]
        {
            "bad\0name.txt",
            "bad\uD800name.txt"
        };

        foreach (var invalidPath in invalidPaths)
        {
            Assert.False(ProcessProductRootResolver.TryResolveRequiredProductPath(
                productRootAlias,
                invalidPath,
                out var resolved,
                out var invalidReason));
            Assert.Empty(resolved);
            Assert.NotEmpty(invalidReason);
        }
    }
}
