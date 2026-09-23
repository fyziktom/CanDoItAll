using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class MaterialIconPickerDialogTests {
    [Fact]
    public void Picker_renders_every_catalog_icon_without_fallback_and_preserves_accessibility() {
        using var context = CreateContext();
        var cut = context.Render<MaterialIconPickerDialog>();

        var cards = cut.FindAll("[data-testid='material-icon-picker-option']");
        Assert.Equal(AgentTeamIconCatalog.Options.Count, cards.Count);
        Assert.Empty(cut.FindAll(".rz-icon-fallback"));
        for (var index = 0; index < cards.Count; index++) {
            var icon = cards[index].QuerySelector(".cda-material-icon");
            Assert.NotNull(icon);
            Assert.Equal(AgentTeamIconCatalog.Options[index].Icon, icon.TextContent.Trim());
            Assert.Equal("true", icon.GetAttribute("aria-hidden"));
            Assert.Contains("text-2xl", icon.ClassList);
            Assert.Equal(AgentTeamIconCatalog.Options[index].Label, cards[index].GetAttribute("title"));
        }
    }

    [Fact]
    public void Picker_filters_and_preserves_selection_when_search_is_cleared() {
        using var context = CreateContext();
        var cut = context.Render<MaterialIconPickerDialog>(parameters => parameters
            .Add(component => component.SelectedIcon, AgentTeamIconCatalog.DefaultIcon));
        var selectedOption = AgentTeamIconCatalog.Options.Single(option => option.Icon == "engineering");

        cut.Find("[data-testid='material-icon-picker-search']").Input(selectedOption.Label);
        var card = Assert.Single(cut.FindAll("[data-testid='material-icon-picker-option']"));
        Assert.Equal(selectedOption.Label, card.GetAttribute("title"));
        card.Click();
        cut.Find("[data-testid='material-icon-picker-search']").Input(string.Empty);

        var selectedCard = Assert.Single(cut.FindAll("[data-testid='material-icon-picker-option'].border-sky-500"));
        Assert.Equal(selectedOption.Label, selectedCard.GetAttribute("title"));
        Assert.Equal(AgentTeamIconCatalog.Options.Count, cut.FindAll("[data-testid='material-icon-picker-option']").Count);
        Assert.Empty(cut.FindAll(".rz-icon-fallback"));
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
