using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using CanDoItAll.Conversations.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Conversations;

public sealed class ConversationDefinitionSettingsComponentsTests
{
    [Fact]
    public void Identity_fields_bind_configurable_labels_slots_and_avatar_actions()
    {
        using var context = CreateContext();
        string? name = "Initial name";
        string? role = "Initial role";
        string? summary = "Initial summary";
        string? instructions = "Initial prompt";

        var cut = context.Render<ConversationIdentityFields>(parameters => parameters
            .Add(component => component.Name, name)
            .Add(component => component.NameChanged, EventCallback.Factory.Create<string?>(this, value => name = value))
            .Add(component => component.Role, role)
            .Add(component => component.RoleChanged, EventCallback.Factory.Create<string?>(this, value => role = value))
            .Add(component => component.Summary, summary)
            .Add(component => component.SummaryChanged, EventCallback.Factory.Create<string?>(this, value => summary = value))
            .Add(component => component.Instructions, instructions)
            .Add(component => component.InstructionsChanged, EventCallback.Factory.Create<string?>(this, value => instructions = value))
            .Add(component => component.RoleLabel, "Subtitle")
            .Add(component => component.InstructionsLabel, "System prompt")
            .Add(component => component.InstructionsDescription, "Applied before each request.")
            .Add(component => component.Avatar, new ConversationAvatarPresentation(
                "Definition avatar",
                "/avatar.svg",
                "DA",
                "Definition avatar"))
            .Add(component => component.AvatarStatus, "Custom avatar")
            .Add(component => component.NameTestId, "definition-name")
            .Add(component => component.RoleTestId, "definition-role")
            .Add(component => component.AvatarTestId, "definition-avatar")
            .Add(component => component.SummaryTestId, "definition-summary")
            .Add(component => component.InstructionsTestId, "definition-instructions")
            .Add(component => component.AvatarActions, builder =>
                builder.AddMarkupContent(0, "<button data-testid='avatar-action'>Choose</button>"))
            .Add(component => component.AdditionalFields, builder =>
                builder.AddMarkupContent(0, "<div data-testid='additional-field'>Additional</div>"))
            .Add(component => component.NameValidation, builder =>
                builder.AddMarkupContent(0, "<span data-testid='name-validation'>Required</span>")));

        Assert.Contains("Subtitle", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("System prompt", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Applied before each request.", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Definition avatar", cut.Find("[data-testid='definition-avatar'] img").GetAttribute("alt"));
        Assert.NotNull(cut.Find("[data-testid='avatar-action']"));
        Assert.NotNull(cut.Find("[data-testid='additional-field']"));
        Assert.NotNull(cut.Find("[data-testid='name-validation']"));

        cut.Find("[data-testid='definition-name']").Change("Renamed");
        cut.Find("[data-testid='definition-role']").Change("Operator");
        cut.Find("[data-testid='definition-summary']").Change("Updated summary");
        cut.Find("[data-testid='definition-instructions']").Change("Updated prompt");

        Assert.Equal("Renamed", name);
        Assert.Equal("Operator", role);
        Assert.Equal("Updated summary", summary);
        Assert.Equal("Updated prompt", instructions);
    }

    [Fact]
    public void Identity_fields_hide_optional_role_and_disable_owned_inputs()
    {
        using var context = CreateContext();

        var cut = context.Render<ConversationIdentityFields>(parameters => parameters
            .Add(component => component.RoleLabel, null)
            .Add(component => component.Disabled, true)
            .Add(component => component.NameTestId, "definition-name")
            .Add(component => component.SummaryTestId, "definition-summary")
            .Add(component => component.InstructionsTestId, "definition-instructions"));

        Assert.DoesNotContain("Role", cut.Markup, StringComparison.Ordinal);
        Assert.All(
            cut.FindAll("[data-testid='definition-name'], [data-testid='definition-summary'], [data-testid='definition-instructions']"),
            element => Assert.True(element.HasAttribute("disabled")));
    }

    [Fact]
    public void Provider_selector_uses_opaque_keys_and_presents_availability_and_errors()
    {
        using var context = CreateContext();
        var firstKey = new ConversationPresentationKey("provider-one");
        var secondKey = new ConversationPresentationKey("provider-two");
        ConversationPresentationKey? selected = firstKey;
        IReadOnlyList<ConversationProviderOption> options =
        [
            new(firstKey, "Provider one", true, "model-one", ["model-one"]),
            new(secondKey, "Provider two", false, "model-two", ["model-two"])
        ];

        var cut = context.Render<ConversationProviderSelector>(parameters => parameters
            .Add(component => component.Options, options)
            .Add(component => component.Value, selected)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<ConversationPresentationKey?>(this, value => selected = value))
            .Add(component => component.ShowDisabledStatus, true)
            .Add(component => component.InputTestId, "provider-selector"));

        var optionText = cut.Find("[data-testid='provider-selector']")
            .QuerySelectorAll("option")
            .Select(option => option.TextContent)
            .ToList();
        Assert.Equal(["None", "Provider one", "Provider two (disabled)"], optionText);

        cut.Find("[data-testid='provider-selector']").Change("1");
        Assert.Equal(secondKey, selected);

        var errorCut = context.Render<ConversationProviderSelector>(parameters => parameters
            .Add(component => component.Options, options)
            .Add(component => component.ErrorMessage, "Provider catalog unavailable.")
            .Add(component => component.InputTestId, "provider-selector"));
        Assert.Contains("Provider catalog unavailable.", errorCut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Model_selector_handles_provider_default_suggestions_and_custom_override()
    {
        using var context = CreateContext();
        string? selected = "model-two";
        var provider = new ConversationProviderOption(
            new ConversationPresentationKey("provider"),
            "Provider",
            true,
            "model-one",
            ["model-one", "model-two"]);

        var cut = context.Render<ConversationProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, selected)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<string?>(this, value => selected = value))
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override")
            .Add(component => component.CustomModelTestId, "custom-model"));

        var optionText = cut.Find("[data-testid='model-choice']")
            .QuerySelectorAll("option")
            .Select(option => option.TextContent)
            .ToList();
        Assert.Equal(["Provider default (model-one)", "model-two"], optionText);

        cut.Find("[data-testid='model-choice']").Change("0");
        Assert.Equal(string.Empty, selected);

        cut.Find("[data-testid='model-override']").Change(true);
        cut.Find("[data-testid='custom-model']").Input(" custom-model ");
        Assert.Equal("custom-model", selected);
    }

    [Fact]
    public void Editor_shell_and_temperature_field_render_optional_owner_slots()
    {
        using var context = CreateContext();
        double? temperature = 0.2d;

        var shell = context.Render<ConversationDefinitionEditorShell>(parameters => parameters
            .Add(component => component.Header, builder => builder.AddMarkupContent(0, "<div data-testid='header-slot'>Header</div>"))
            .Add(component => component.Validation, builder => builder.AddMarkupContent(0, "<div data-testid='validation-slot'>Validation</div>"))
            .Add(component => component.Sections, builder => builder.AddMarkupContent(0, "<div data-testid='sections-slot'>Sections</div>"))
            .Add(component => component.AdvancedSettings, builder => builder.AddMarkupContent(0, "<div data-testid='advanced-slot'>Advanced</div>"))
            .Add(component => component.Actions, builder => builder.AddMarkupContent(0, "<div data-testid='actions-slot'>Actions</div>")));

        Assert.NotNull(shell.Find("[data-testid='header-slot']"));
        Assert.NotNull(shell.Find("[data-testid='validation-slot']"));
        Assert.NotNull(shell.Find("[data-testid='sections-slot']"));
        Assert.NotNull(shell.Find("[data-testid='advanced-slot']"));
        Assert.NotNull(shell.Find("[data-testid='actions-slot']"));

        var field = context.Render<ConversationTemperatureField>(parameters => parameters
            .Add(component => component.Value, temperature)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<double?>(this, value => temperature = value))
            .Add(component => component.Description, "Higher values increase variation.")
            .Add(component => component.InputTestId, "temperature-input")
            .Add(component => component.Validation, builder =>
                builder.AddMarkupContent(0, "<span data-testid='temperature-validation'>Valid</span>")));

        field.Find("[data-testid='temperature-input']").Change("1.4");
        Assert.Equal(1.4d, temperature);
        Assert.Contains("Higher values increase variation.", field.Markup, StringComparison.Ordinal);
        Assert.NotNull(field.Find("[data-testid='temperature-validation']"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
