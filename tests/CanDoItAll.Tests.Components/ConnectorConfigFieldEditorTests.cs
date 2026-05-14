using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Components;

public sealed class ConnectorConfigFieldEditorTests
{
    [Fact]
    public void ConnectorConfigFieldEditor_updates_text_state_from_canonical_field_descriptor()
    {
        using var context = new TestContext();
        var state = new ConnectorConfigState();
        var field = new ConfigurationFieldDescriptor(
            "endpointUrl",
            "Endpoint",
            ConfigurationFieldType.Url,
            IsRequired: true,
            "Endpoint URL");

        var cut = context.RenderComponent<ConnectorConfigFieldEditor>(parameters => parameters
            .Add(component => component.Field, field)
            .Add(component => component.State, state)
            .Add(component => component.TestId, "connector-config-endpoint"));

        cut.Find("[data-testid='connector-config-endpoint']").Change("https://example.test/hooks");

        Assert.Equal("https://example.test/hooks", state.GetText("endpointUrl"));
    }

    [Fact]
    public void ConnectorConfigFieldEditor_updates_boolean_state_from_canonical_field_descriptor()
    {
        using var context = new TestContext();
        var state = new ConnectorConfigState();
        var field = new ConfigurationFieldDescriptor(
            "enabled",
            "Enabled",
            ConfigurationFieldType.Boolean,
            IsRequired: false,
            "Enable connector");

        var cut = context.RenderComponent<ConnectorConfigFieldEditor>(parameters => parameters
            .Add(component => component.Field, field)
            .Add(component => component.State, state)
            .Add(component => component.TestId, "connector-config-enabled"));

        cut.Find("[data-testid='connector-config-enabled']").Change(true);

        Assert.True(state.GetBoolean("enabled"));
    }
}
