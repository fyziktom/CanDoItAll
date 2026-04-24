using Bunit;
using CanDoItAll.Components.OverlayLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class OverlayComponentToolboxTests
{
    [Fact]
    public void Shared_toolbox_renders_grouped_items_with_compatibility_classes()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<OverlayComponentToolbox>(
            parameters => parameters
                .Add(component => component.TestId, "shared-toolbox")
                .Add(component => component.Eyebrow, "Library")
                .Add(component => component.Title, "Components")
                .Add(component => component.Sections, BuildSections()));

        Assert.Contains("cda-component-toolbox", cut.Markup);
        Assert.Contains("cw-context-toolbox", cut.Markup);
        Assert.Contains("2 items", cut.Markup);
        Assert.Contains("Review role", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='toolbox-role-reviewer']"));
    }

    [Fact]
    public void Shared_toolbox_publishes_search_and_item_callbacks()
    {
        using var context = new TestContext();

        string? searchText = null;
        string? selectedActionId = null;
        var cut = context.RenderComponent<OverlayComponentToolbox>(
            parameters => parameters
                .Add(component => component.TestId, "shared-toolbox")
                .Add(component => component.Sections, BuildSections())
                .Add(component => component.SearchTextChanged, EventCallback.Factory.Create<string?>(this, value => searchText = value))
                .Add(component => component.ItemSelected, EventCallback.Factory.Create<string>(this, value => selectedActionId = value)));

        cut.Find("input[type='search']").Input("review");
        cut.Find("[data-testid='toolbox-role-reviewer']").Click();

        Assert.Equal("review", searchText);
        Assert.Equal("role.reviewer", selectedActionId);
    }

    [Fact]
    public void Shared_toolbox_renders_empty_state_when_sections_are_empty()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<OverlayComponentToolbox>(
            parameters => parameters
                .Add(component => component.TestId, "shared-toolbox")
                .Add(component => component.Sections, Array.Empty<OverlayToolboxSection>()));

        Assert.Contains("No components match the current search.", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='shared-toolbox-empty']"));
    }

    private static IReadOnlyList<OverlayToolboxSection> BuildSections()
        =>
        [
            new OverlayToolboxSection(
                "roles",
                "Roles",
                "People and responsibility templates.",
                [
                    new OverlayToolboxGroup(
                        "role-templates",
                        "Role templates",
                        "Reusable process staffing contracts.",
                        [
                            new OverlayToolboxItem(
                                "role.reviewer",
                                "Review role",
                                "Add a reviewer.",
                                Icon: "person",
                                DataTestId: "toolbox-role-reviewer"),
                            new OverlayToolboxItem(
                                "role.architect",
                                "Architect role",
                                "Add an architect.",
                                Icon: "architecture",
                                DataTestId: "toolbox-role-architect")
                        ],
                        DataTestId: "toolbox-group-roles",
                        BodyDataTestId: "toolbox-group-body-roles")
                ])
        ];
}
