using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Configuration.UI;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class ConnectorConfigFieldEditorTests
{
    [Fact]
    public void Workspace_fallback_keeps_raw_number_json_validation_and_reference_only_secret_selection() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var secret = Guid.NewGuid();
        var state = new ConfigurationState();
        var schema = new ConfigurationSchema("1.0", [
            new("count", "Count", ConfigurationFieldType.Number, false, ""),
            new("payload", "Payload", ConfigurationFieldType.Json, false, ""),
            new("secret", "Secret", ConfigurationFieldType.SecretReference, false, "")
        ]);
        ConfigurationState? changed = null;
        var cut = context.Render<CanDoItAll.Modules.Workspace.Pages.Components.ConfigurationSchemaFallbackRenderer>(p => p
            .Add(c => c.Schema, schema).Add(c => c.State, state)
            .Add(c => c.Secrets, [new CanDoItAll.Modules.Security.SecretListItem(secret, "Reference metadata", CanDoItAll.Modules.Security.SecretKind.ApiKey, "fixture", DateTimeOffset.UnixEpoch)])
            .Add(c => c.StateChanged, value => changed = value));
        cut.Find("[data-testid='configuration-field-count']").Input(" 1e- ");
        cut.Find("[data-testid='configuration-field-payload']").Input("{ unfinished");
        cut.Find("[data-testid='configuration-field-secret']").Change(secret.ToString());
        var validation = new ConfigurationSchemaValidator().Validate(schema, state);
        cut.Render(p => p.Add(c => c.Validation, validation));
        Assert.Same(state, changed);
        Assert.Equal(" 1e- ", cut.Find("[data-testid='configuration-field-count']").GetAttribute("value"));
        Assert.Equal("{ unfinished", state.GetText("payload"));
        Assert.Equal(secret.ToString(), state.GetText("secret"));
        Assert.Equal(2, cut.FindAll(".workflow-canvas-error").Count);
        Assert.Contains("Reference metadata", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_secret_field_keeps_unavailable_reference_until_an_explicit_selection() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var state = new ConfigurationState();
        var missing = Guid.NewGuid().ToString();
        state.SetText("secret", missing);
        var cut = context.Render<ConnectorConfigFieldEditor>(p => p.Add(c => c.State, state)
            .Add(c => c.Field, new ConfigurationFieldDescriptor("secret", "Secret", ConfigurationFieldType.SecretReference, false, "")));
        Assert.Contains($"Unavailable secret ({missing})", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(missing, state.GetText("secret"));
        cut.Find("select").Change(string.Empty);
        Assert.Equal(string.Empty, state.GetText("secret"));
    }

    [Fact]
    public void ConnectorConfigFieldEditor_updates_text_state_from_canonical_field_descriptor()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
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

        cut.Find("[data-testid='connector-config-endpoint']").Input("https://example.test/hooks");

        Assert.Equal("https://example.test/hooks", state.GetText("endpointUrl"));
    }

    [Fact]
    public void ConnectorConfigFieldEditor_updates_boolean_state_from_canonical_field_descriptor()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
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
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
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

        cut.Find("[data-testid='connector-config-max-bytes']").Input("1099511627776");

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
