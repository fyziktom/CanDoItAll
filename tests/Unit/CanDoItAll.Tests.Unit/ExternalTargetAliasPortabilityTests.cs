using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ExternalTargetAliasPortabilityTests : IDisposable
{
    private readonly string rootPath = TestFileSystem.CreateTemporaryRoot("external-target-alias-portability");
    private readonly ExternalTargetPathRegistryFactory externalTargetFactory = new();
    private readonly IExternalTargetPathRegistry externalTargets;

    public ExternalTargetAliasPortabilityTests()
    {
        externalTargets = externalTargetFactory.Create([]);
    }

    [Fact]
    public void Versioned_alias_round_trips_a_native_root_and_child_without_exposing_the_root()
    {
        var externalRoot = CreateDirectory("first-root");
        var childPath = Path.Combine(externalRoot, "src", "Application.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(childPath)!);
        File.WriteAllText(childPath, "content");
        Assert.True(externalTargets.TryCreateAlias(externalRoot, out var rootAlias));
        Assert.True(externalTargets.TryCreateAlias(childPath, out var childAlias));
        var policy = TestWorkspaceServices.CreatePathPolicy(
            CreateDirectory("workspace"),
            externalTargetRegistry: externalTargets);

        var succeeded = policy.TryResolveWorkspacePath(
            childAlias,
            allowWorkspaceRoot: false,
            out var resolution,
            out var validationMessage);

        Assert.True(succeeded, validationMessage);
        Assert.StartsWith("external-target/v1/", rootAlias, StringComparison.Ordinal);
        Assert.StartsWith(rootAlias + "/", childAlias, StringComparison.Ordinal);
        Assert.DoesNotContain(externalRoot, rootAlias, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath(childPath), resolution.FullPath);
        Assert.Equal(childAlias, resolution.RelativePath);
    }

    [Fact]
    public void Versioned_aliases_distinguish_multiple_allowed_roots()
    {
        var firstRoot = CreateDirectory("first-root");
        var secondRoot = CreateDirectory("second-root");

        Assert.True(externalTargets.TryCreateAlias(firstRoot, out var firstAlias));
        Assert.True(externalTargets.TryCreateAlias(secondRoot, out var secondAlias));

        Assert.NotEqual(firstAlias, secondAlias);
        Assert.Matches("^external-target/v1/[0-9a-f]{24}$", firstAlias);
        Assert.Matches("^external-target/v1/[0-9a-f]{24}$", secondAlias);
    }

    [Fact]
    public void Versioned_alias_rejects_dot_segments_before_filesystem_resolution()
    {
        var externalRoot = CreateDirectory("dot-root");
        Assert.True(externalTargets.TryCreateAlias(externalRoot, out var rootAlias));
        var policy = TestWorkspaceServices.CreatePathPolicy(
            CreateDirectory("workspace"),
            externalTargetRegistry: externalTargets);

        var succeeded = policy.TryResolveWorkspacePath(
            rootAlias + "/src/../secret.txt",
            allowWorkspaceRoot: false,
            out _,
            out var validationMessage);

        Assert.False(succeeded);
        Assert.Contains("traversal segments", validationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Access_settings_writer_persists_only_versioned_aliases_with_a_trusted_binding_record()
    {
        var externalRoot = CreateDirectory("settings-root");
        Assert.True(externalTargets.TryCreateAlias(externalRoot, out var alias));
        var settings = new AgentWorkspaceToolAccessSettings
        {
            AllowedExternalTargetAliases = [alias],
            ExternalTargetRootBindings = externalTargets.ExportBindings([alias]).ToList()
        };

        var json = AgentWorkspaceToolAccessMetadata.Write(null, settings, externalTargets);
        var roundTripped = AgentWorkspaceToolAccessMetadata.Read(json);

        Assert.Contains(alias, roundTripped.AllowedExternalTargetAliases);
        Assert.Contains("externalTargetRootBindings", json, StringComparison.Ordinal);
        Assert.Contains("protectedRootToken", json, StringComparison.Ordinal);
        Assert.DoesNotContain(externalRoot, json, StringComparison.Ordinal);
        Assert.DoesNotContain(externalRoot, alias, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_windows_alias_writer_migrates_on_windows_and_fails_closed_on_foreign_hosts()
    {
        const string legacyAlias = "external-target/C/repositories/demo";
        var settings = new AgentWorkspaceToolAccessSettings
        {
            AllowedExternalTargetAliases = [legacyAlias]
        };

        if (!OperatingSystem.IsWindows())
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                AgentWorkspaceToolAccessMetadata.Write(null, settings, externalTargets));

            Assert.Contains("cannot be written on this host", exception.Message, StringComparison.OrdinalIgnoreCase);
            return;
        }

        var json = AgentWorkspaceToolAccessMetadata.Write(null, settings, externalTargets);
        var roundTripped = AgentWorkspaceToolAccessMetadata.Read(json);
        var alias = Assert.Single(roundTripped.AllowedExternalTargetAliases);

        Assert.StartsWith("external-target/v1/", alias, StringComparison.Ordinal);
        Assert.DoesNotContain(legacyAlias, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Versioned_alias_round_trips_valid_unix_backslash_colon_and_unicode_segments()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var externalRoot = CreateDirectory("encoded-root");
        var childPath = Path.Combine(externalRoot, "a\\b:žluťoučký.txt");
        File.WriteAllText(childPath, "content");
        Assert.True(externalTargets.TryCreateAlias(externalRoot, out var rootAlias));
        Assert.True(externalTargets.TryCreateAlias(childPath, out var childAlias));
        var policy = TestWorkspaceServices.CreatePathPolicy(
            CreateDirectory("encoded-workspace"),
            externalTargetRegistry: externalTargets);

        var succeeded = policy.TryResolveWorkspacePath(
            childAlias,
            allowWorkspaceRoot: false,
            out var resolution,
            out var validationMessage);

        Assert.True(succeeded, validationMessage);
        Assert.StartsWith(rootAlias + "/", childAlias, StringComparison.Ordinal);
        Assert.Contains("%5C", childAlias, StringComparison.Ordinal);
        Assert.Contains("%3A", childAlias, StringComparison.Ordinal);
        Assert.Equal(childPath, resolution.FullPath);
    }

    [Fact]
    public void Versioned_alias_authorization_keeps_case_distinct_unix_children_separate()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var externalRoot = CreateDirectory("case-sensitive-root");
        var upperChild = Path.Combine(externalRoot, "Foo");
        var lowerChild = Path.Combine(externalRoot, "foo");
        Directory.CreateDirectory(upperChild);
        Directory.CreateDirectory(lowerChild);
        Assert.True(externalTargets.TryCreateAlias(externalRoot, out _));
        Assert.True(externalTargets.TryCreateAlias(upperChild, out var upperAlias));
        Assert.True(externalTargets.TryCreateAlias(lowerChild, out var lowerAlias));

        Assert.NotEqual(upperAlias, lowerAlias);
        Assert.True(AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            upperAlias,
            [upperAlias]));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            lowerAlias,
            [upperAlias]));
    }

    [Theory]
    [InlineData(@"\\server\share\file.txt")]
    [InlineData("//server/share/file.txt")]
    public void Foreign_unc_spellings_are_rejected_on_unix(string foreignPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.False(externalTargets.TryCreateAlias(foreignPath, out _));
        Assert.Null(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(foreignPath, externalTargets));
    }

    [Fact]
    public void Persisted_binding_resolves_only_inside_the_registry_instance_that_imported_it()
    {
        var externalRoot = CreateDirectory("scoped-root");
        Assert.True(externalTargets.TryCreateAlias(externalRoot, out var alias));
        var bindings = externalTargets.ExportBindings([alias]);
        var unboundRegistry = externalTargetFactory.Create([]);
        var reboundRegistry = externalTargetFactory.Create(bindings);

        Assert.Equal(
            ExternalTargetAliasResolutionKind.Unbound,
            unboundRegistry.TryResolve(alias, out _, out _));
        Assert.Equal(
            ExternalTargetAliasResolutionKind.Resolved,
            reboundRegistry.TryResolve(alias, out var resolvedPath, out _));
        Assert.Equal(externalRoot, resolvedPath);
    }

    public void Dispose()
    {
        TestFileSystem.DeleteDirectoryWithRetry(rootPath);
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(rootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
