using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class SettingsRendererTests
{
    [Fact]
    public void ConfigurationField_fallback_renderer_updates_canonical_state()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<ISettingsRendererRegistry>(new SettingsRendererRegistry(Array.Empty<ISettingsRendererSource>()));
        var schema = new ConfigurationSchema("1.0",
        [
            new ConfigurationFieldDescriptor("endpointUrl", "Endpoint", ConfigurationFieldType.Url, IsRequired: true, "Endpoint URL"),
            new ConfigurationFieldDescriptor("operation", "Operation", ConfigurationFieldType.Select, IsRequired: true, "Operation")
            {
                Options =
                [
                    new ConfigurationFieldOption("ReadText", "Read text"),
                    new ConfigurationFieldOption("WriteText", "Write text")
                ]
            },
            new ConfigurationFieldDescriptor("enabled", "Enabled", ConfigurationFieldType.Boolean, IsRequired: false, "Enabled")
        ]);
        var state = new ConfigurationState();

        var cut = context.RenderComponent<SettingsRendererHost>(parameters => parameters
            .Add(component => component.Schema, schema)
            .Add(component => component.State, state)
            .Add(component => component.TestIdPrefix, "settings-renderer"));

        Assert.Contains("settings-renderer", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='settings-renderer-endpointUrl']").Change("https://example.test/hooks");
        cut.Find("[data-testid='settings-renderer-operation']").Change("WriteText");
        cut.Find("[data-testid='settings-renderer-enabled']").Change(true);

        Assert.Equal("https://example.test/hooks", state.GetText("endpointUrl"));
        Assert.Equal("WriteText", state.GetText("operation"));
        Assert.True(state.GetBoolean("enabled"));
    }

    [Fact]
    public void SettingsRendererHost_uses_registered_trusted_renderer()
    {
        using var context = new TestContext();
        var descriptor = new SettingsRendererDescriptor(
            "trusted.test",
            typeof(TrustedSettingsRenderer),
            SettingsRendererTrustLevel.Application,
            "test",
            "1.0");
        context.Services.AddSingleton<ISettingsRendererRegistry>(new SettingsRendererRegistry(
        [
            new StaticSettingsRendererSource([descriptor])
        ]));
        var schema = new ConfigurationSchema("1.0",
        [
            new ConfigurationFieldDescriptor("name", "Name", ConfigurationFieldType.Text, IsRequired: true, "Name")
        ]);

        var cut = context.RenderComponent<SettingsRendererHost>(parameters => parameters
            .Add(component => component.RendererKey, "trusted.test")
            .Add(component => component.Schema, schema)
            .Add(component => component.State, new ConfigurationState())
            .Add(component => component.TestIdPrefix, "settings-renderer"));

        Assert.Contains("trusted-renderer", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("1.0", cut.Markup, StringComparison.Ordinal);
    }

    public sealed class TrustedSettingsRenderer : ComponentBase
    {
        [Parameter]
        public required ConfigurationSchema Schema { get; set; }

        [Parameter]
        public ConfigurationState? State { get; set; }

        [Parameter]
        public ConfigurationValidationResult? Validation { get; set; }

        [Parameter]
        public IReadOnlyList<CanDoItAll.Modules.Security.SecretListItem> Secrets { get; set; } = [];

        [Parameter]
        public EventCallback<ConfigurationState> StateChanged { get; set; }

        [Parameter]
        public string? TestIdPrefix { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "trusted-renderer");
            builder.AddContent(2, Schema.Version);
            builder.CloseElement();
        }
    }
}
