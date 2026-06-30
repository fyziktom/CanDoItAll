using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class TabsComponentTests : TestContext
{
    [Fact]
    public void Renders_root_class_and_new_appearance_modes_without_legacy_zy_classes()
    {
        var cut = RenderTabs(parameters => parameters
            .Add(component => component.Class, "custom-shell")
            .Add(component => component.BorderMode, TabsBorderMode.None)
            .Add(component => component.Tone, TabsTone.Success)
            .Add(component => component.OverflowMode, TabsOverflowMode.Wrap));

        var root = cut.Find(".cad-tabs");

        Assert.Contains("custom-shell", root.ClassList);
        Assert.Contains("cad-tabs--border-none", root.ClassList);
        Assert.Contains("cad-tabs--tone-success", root.ClassList);
        Assert.Contains("cad-tabs--overflow-wrap", root.ClassList);
        Assert.DoesNotContain("zy-tabs", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Merges_tabs_item_class_with_shared_button_class()
    {
        var cut = RenderTabs(
            itemSpecifications:
            [
                new TabSpec("Overview", "Overview content", ClassName: "text-rose-500")
            ]);

        var button = cut.Find("button[role='tab']");

        Assert.Contains("cad-tabs__tab", button.ClassList);
        Assert.Contains("text-rose-500", button.ClassList);
    }

    [Fact]
    public void Falls_back_to_tab_label_when_item_text_is_missing()
    {
        var cut = RenderTabs(
            itemSpecifications:
            [
                new TabSpec(null, "Fallback content"),
                new TabSpec("Details", "Details content")
            ]);

        var labels = cut.FindAll(".cad-tabs__tab-text")
            .Select(element => element.TextContent.Trim())
            .ToArray();

        Assert.Contains("Tab", labels);
        Assert.Contains("Details", labels);
    }

    [Fact]
    public void Clicking_enabled_tab_updates_selected_index_and_change_callbacks()
    {
        var selectedIndex = -1;
        var changedIndex = -1;

        var cut = RenderTabs(
            parameters => parameters
                .Add(component => component.SelectedIndexChanged, EventCallback.Factory.Create<int>(this, value => selectedIndex = value))
                .Add(component => component.Change, EventCallback.Factory.Create<int>(this, value => changedIndex = value)),
            [
                new TabSpec("Overview", "Overview content"),
                new TabSpec("Details", "Details content")
            ]);

        cut.FindAll("button[role='tab']")[1].Click();

        Assert.Equal(1, selectedIndex);
        Assert.Equal(1, changedIndex);
    }

    [Fact]
    public void Disabled_tab_does_not_change_selection()
    {
        var selectedIndex = -1;
        var changedIndex = -1;

        var cut = RenderTabs(
            parameters => parameters
                .Add(component => component.SelectedIndex, 0)
                .Add(component => component.SelectedIndexChanged, EventCallback.Factory.Create<int>(this, value => selectedIndex = value))
                .Add(component => component.Change, EventCallback.Factory.Create<int>(this, value => changedIndex = value)),
            [
                new TabSpec("Overview", "Overview content"),
                new TabSpec("Disabled", "Disabled content", Disabled: true)
            ]);

        cut.FindAll("button[role='tab']")[1].Click();

        Assert.Equal(-1, selectedIndex);
        Assert.Equal(-1, changedIndex);
    }

    private IRenderedComponent<Tabs> RenderTabs(
        Action<ComponentParameterCollectionBuilder<Tabs>>? configure = null,
        IReadOnlyList<TabSpec>? itemSpecifications = null)
    {
        itemSpecifications ??=
        [
            new TabSpec("Overview", "Overview content"),
            new TabSpec("Activity", "Activity content")
        ];

        return RenderComponent<Tabs>(parameters =>
        {
            parameters.Add(component => component.TabItems, BuildTabItems(itemSpecifications));
            configure?.Invoke(parameters);
        });
    }

    private static RenderFragment BuildTabItems(IReadOnlyList<TabSpec> itemSpecifications)
    {
        return builder =>
        {
            foreach (var specification in itemSpecifications)
            {
                builder.OpenComponent<TabsItem>(0);

                if (specification.Text is not null)
                {
                    builder.AddAttribute(1, nameof(TabsItem.Text), specification.Text);
                }

                if (specification.Disabled)
                {
                    builder.AddAttribute(2, nameof(TabsItem.Disabled), true);
                }

                if (!string.IsNullOrWhiteSpace(specification.ClassName))
                {
                    builder.AddAttribute(3, "class", specification.ClassName);
                }

                builder.AddAttribute(4, nameof(TabsItem.ChildContent), (RenderFragment)(contentBuilder =>
                {
                    contentBuilder.AddContent(0, specification.Content);
                }));

                builder.CloseComponent();
            }
        };
    }

    private sealed record TabSpec(string? Text, string Content, bool Disabled = false, string? ClassName = null);
}
