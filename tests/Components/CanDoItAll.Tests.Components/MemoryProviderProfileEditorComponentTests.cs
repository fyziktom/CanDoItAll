using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.Memory.Components;
using CanDoItAll.Modules.Memory.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class MemoryProviderProfileEditorComponentTests
{
    [Fact]
    public void Mcp_editor_offers_only_remote_http_runtime_fields()
    {
        using var context = CreateContext();
        var cut = context.RenderComponent<MemoryProviderMcpTransportEditor>(parameters => parameters
            .Add(component => component.Transport, new MemoryProviderMcpTransportEditorModel()));

        Assert.Contains("remote-http", cut.Markup);
        Assert.Contains("complete header value", cut.Markup);
        Assert.Contains("Bearer &lt;token&gt;", cut.Markup);
        Assert.DoesNotContain("Internal implementation", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ingestion tool", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Source request tool", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Feedback tool", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Http_capability_editor_disables_new_runtime_overclaims()
    {
        using var context = CreateContext();
        var editor = new MemoryProviderProfileEditorModel
        {
            DriverKind = MemoryProviderDriverKind.Http,
            SupportsContextQuerySync = true
        };
        var cut = context.RenderComponent<MemoryProviderCapabilityEditor>(parameters => parameters
            .Add(component => component.Editor, editor));

        Assert.False(cut.Find("[data-testid='memory-ui-editor-sync-query']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='memory-ui-editor-async-query']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='memory-ui-editor-snapshot-ingestion']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='memory-ui-editor-immediate-feedback']").HasAttribute("disabled"));
    }

    [Fact]
    public void Profile_editor_surfaces_legacy_raw_credential_migration_warning()
    {
        using var context = CreateContext();
        var editor = new MemoryProviderProfileEditorModel
        {
            LegacyRawCredentialKeys = ["host.candoitall.memory.http.apiKey"]
        };
        var cut = context.RenderComponent<MemoryProviderProfileEditor>(parameters => parameters
            .Add(component => component.Editor, editor));

        Assert.Contains("legacy raw credential", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("environment-variable reference", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Profile_editor_offers_only_executable_driver_kinds()
    {
        using var context = CreateContext();
        var cut = context.RenderComponent<MemoryProviderProfileEditor>(parameters => parameters
            .Add(component => component.Editor, new MemoryProviderProfileEditorModel()));

        Assert.Contains("Http", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("NativeRemote", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Mcp", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Mock", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("InProcessMigration", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SingleWorkspace", cut.Markup, StringComparison.Ordinal);
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
