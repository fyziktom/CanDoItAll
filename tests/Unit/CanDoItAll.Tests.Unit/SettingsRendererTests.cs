using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class SettingsRendererTests
{
    [Fact]
    public void SettingsRendererRegistry_resolves_keys_case_insensitively()
    {
        var descriptor = new SettingsRendererDescriptor(
            "builtin.test",
            typeof(SettingsRendererHost),
            SettingsRendererTrustLevel.Application,
            "test",
            "1.0");
        var registry = new SettingsRendererRegistry(
        [
            new StaticSettingsRendererSource([descriptor])
        ]);

        var resolved = registry.ResolveRenderer(new SettingsRendererResolutionRequest(
            "BUILTIN.TEST",
            SettingsRendererTrustLevel.Application,
            "test",
            "1.0"));

        Assert.Equal(SettingsRendererResolutionStatus.Resolved, resolved.Status);
        Assert.Same(descriptor, resolved.Descriptor);
    }

    [Theory]
    [InlineData(SettingsRendererTrustLevel.BundledPlugin, "test", "1.0", SettingsRendererResolutionStatus.TrustMismatch)]
    [InlineData(SettingsRendererTrustLevel.Application, "another-owner", "1.0", SettingsRendererResolutionStatus.OwnerMismatch)]
    [InlineData(SettingsRendererTrustLevel.Application, "test", "2.0", SettingsRendererResolutionStatus.SchemaVersionMismatch)]
    public void SettingsRendererRegistry_rejects_untrusted_or_incompatible_requests(
        SettingsRendererTrustLevel trustLevel,
        string ownerId,
        string schemaVersion,
        SettingsRendererResolutionStatus expectedStatus)
    {
        var descriptor = new SettingsRendererDescriptor(
            "builtin.test",
            typeof(SettingsRendererHost),
            SettingsRendererTrustLevel.Application,
            "test",
            "1.0");
        var registry = new SettingsRendererRegistry(
        [
            new StaticSettingsRendererSource([descriptor])
        ]);

        var resolution = registry.ResolveRenderer(new SettingsRendererResolutionRequest(
            descriptor.RendererKey,
            trustLevel,
            ownerId,
            schemaVersion));

        Assert.Equal(expectedStatus, resolution.Status);
        Assert.Null(resolution.Descriptor);
    }

    [Fact]
    public void SettingsRendererRegistry_rejects_duplicate_renderer_keys()
    {
        var first = new SettingsRendererDescriptor(
            "duplicate.renderer",
            typeof(SettingsRendererHost),
            SettingsRendererTrustLevel.Application,
            "first",
            "1.0");
        var second = new SettingsRendererDescriptor(
            "DUPLICATE.RENDERER",
            typeof(SettingsRendererHost),
            SettingsRendererTrustLevel.BundledPlugin,
            "second",
            "1.0");

        var exception = Assert.Throws<InvalidOperationException>(() => new SettingsRendererRegistry(
        [
            new StaticSettingsRendererSource([first, second])
        ]));

        Assert.Contains("duplicate.renderer", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsRendererDescriptor_rejects_non_component_type()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SettingsRendererDescriptor(
            "bad.renderer",
            typeof(string),
            SettingsRendererTrustLevel.Application,
            "test",
            "1.0"));

        Assert.Contains("IComponent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
