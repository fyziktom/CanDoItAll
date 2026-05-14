using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;

namespace CanDoItAll.Tests.Unit;

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

        var resolved = registry.FindRenderer("BUILTIN.TEST");

        Assert.Same(descriptor, resolved);
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
