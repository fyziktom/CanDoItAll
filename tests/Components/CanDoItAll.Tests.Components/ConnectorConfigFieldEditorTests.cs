using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class ConnectorConfigFieldEditorTests
{
    [Fact]
    public void ConnectorConfigFieldEditor_updates_text_state_from_canonical_field_descriptor()
    {
        using var context = new BunitContext();
        var state = new ConnectorConfigState();
        var field = new ConfigurationFieldDescriptor(
            "endpointUrl",
            "Endpoint",
            ConfigurationFieldType.Url,
            IsRequired: true,
            "Endpoint URL");

        var cut = context.Render<ConnectorConfigFieldEditor>(parameters => parameters
            .Add(component => component.Field, field)
            .Add(component => component.State, state)
            .Add(component => component.TestId, "connector-config-endpoint"));

        cut.Find("[data-testid='connector-config-endpoint']").Change("https://example.test/hooks");

        Assert.Equal("https://example.test/hooks", state.GetText("endpointUrl"));
    }

    [Fact]
    public void ConnectorConfigFieldEditor_updates_boolean_state_from_canonical_field_descriptor()
    {
        using var context = new BunitContext();
        var state = new ConnectorConfigState();
        var field = new ConfigurationFieldDescriptor(
            "enabled",
            "Enabled",
            ConfigurationFieldType.Boolean,
            IsRequired: false,
            "Enable connector");

        var cut = context.Render<ConnectorConfigFieldEditor>(parameters => parameters
            .Add(component => component.Field, field)
            .Add(component => component.State, state)
            .Add(component => component.TestId, "connector-config-enabled"));

        cut.Find("[data-testid='connector-config-enabled']").Change(true);

        Assert.True(state.GetBoolean("enabled"));
    }

    [Fact]
    public void ConnectorConfigFieldEditor_preserves_int64_numeric_state()
    {
        using var context = new BunitContext();
        var state = new ConnectorConfigState();
        var field = new ConfigurationFieldDescriptor(
            "maxBytes",
            "Max bytes",
            ConfigurationFieldType.Number,
            IsRequired: false,
            "Maximum bytes")
        {
            NumberKind = ConfigurationNumberKind.Int64
        };

        var cut = context.Render<ConnectorConfigFieldEditor>(parameters => parameters
            .Add(component => component.Field, field)
            .Add(component => component.State, state)
            .Add(component => component.TestId, "connector-config-max-bytes"));

        cut.Find("[data-testid='connector-config-max-bytes']").Change("1099511627776");

        Assert.Equal("1099511627776", state.GetText("maxBytes"));
    }

    [Fact]
    public void ConfigurationSchemaValidator_rejects_fractional_integer_and_empty_guid()
    {
        var schema = new ConfigurationSchema(
            "1.0",
            [
                new ConfigurationFieldDescriptor("count", "Count", ConfigurationFieldType.Number, false, string.Empty),
                new ConfigurationFieldDescriptor("providerId", "Provider", ConfigurationFieldType.Guid, true, string.Empty)
            ]);
        var state = new ConfigurationState(new Dictionary<string, string>
        {
            ["count"] = "2.5",
            ["providerId"] = Guid.Empty.ToString("D")
        });

        var result = new ConfigurationSchemaValidator().Validate(schema, state);

        Assert.Contains(result.Issues, issue => issue.FieldKey == "count" && issue.Message.Contains("Int32", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.FieldKey == "providerId" && issue.Message.Contains("GUID", StringComparison.Ordinal));
    }
}
