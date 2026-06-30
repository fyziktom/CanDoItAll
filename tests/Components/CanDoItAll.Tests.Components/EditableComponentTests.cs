using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class EditableComponentTests : TestContext
{
    [Fact]
    public void Saves_text_property_and_returns_to_display_mode()
    {
        var item = new EditableTestItem { Title = "Draft review" };
        EditableTestItem? savedItem = null;

        var cut = RenderEditable(item, nameof(EditableTestItem.Title), value => savedItem = value);

        Assert.Contains("Draft review", cut.Markup);

        cut.Find("button[aria-label='Edit Title']").Click();
        cut.Find("input[aria-label='Editing Title']").Change("Ready review");
        cut.Find("button[aria-label='Save Title']").Click();

        Assert.Equal("Ready review", item.Title);
        Assert.Same(item, savedItem);
        Assert.Contains("Ready review", cut.Markup);
        Assert.Empty(cut.FindAll("input[aria-label='Editing Title']"));
    }

    [Fact]
    public void Saves_integer_property_with_display_extension()
    {
        var item = new EditableTestItem { Confidence = 70 };
        EditableTestItem? savedItem = null;

        var cut = RenderEditable(
            item,
            nameof(EditableTestItem.Confidence),
            value => savedItem = value,
            parameters => parameters.Add(component => component.Extension, "%"));

        Assert.Contains("70%", cut.Markup);

        cut.Find("button[aria-label='Edit Confidence']").Click();
        cut.Find("input[aria-label='Editing Confidence']").Change("85");
        cut.Find("button[aria-label='Save Confidence']").Click();

        Assert.Equal(85, item.Confidence);
        Assert.Same(item, savedItem);
        Assert.Contains("85%", cut.Markup);
    }

    [Fact]
    public void Saves_boolean_property_with_shared_checkbox()
    {
        var item = new EditableTestItem { RequiresApproval = false };
        EditableTestItem? savedItem = null;

        var cut = RenderEditable(item, nameof(EditableTestItem.RequiresApproval), value => savedItem = value);

        Assert.Contains("False", cut.Markup);

        cut.Find("button[aria-label='Edit RequiresApproval']").Click();
        cut.Find("input[aria-label='Editing RequiresApproval']").Change(true);
        cut.Find("button[aria-label='Save RequiresApproval']").Click();

        Assert.True(item.RequiresApproval);
        Assert.Same(item, savedItem);
        Assert.Contains("True", cut.Markup);
    }

    [Fact]
    public void Renders_compact_class_by_default()
    {
        var item = new EditableTestItem { Title = "Compact" };

        var cut = RenderEditable(item, nameof(EditableTestItem.Title), _ => { });

        var root = cut.Find(".cda-editable");
        Assert.Contains("cda-editable--compact", root.ClassList);
        Assert.Empty(cut.FindAll(".cda-editable--xs"));
    }

    [Fact]
    public void Renders_extra_small_class_for_dense_icon_layouts()
    {
        var item = new EditableTestItem { Title = "Tiny" };

        var cut = RenderEditable(
            item,
            nameof(EditableTestItem.Title),
            _ => { },
            parameters => parameters.Add(component => component.Size, EditableSize.ExtraSmall));

        var root = cut.Find(".cda-editable");
        var action = cut.Find("button[aria-label='Edit Title']");

        Assert.Contains("cda-editable--xs", root.ClassList);
        Assert.Contains("cda-editable__action", action.ClassList);
    }

    private IRenderedComponent<Editable<EditableTestItem>> RenderEditable(
        EditableTestItem item,
        string parameterName,
        Action<EditableTestItem> itemChanged,
        Action<ComponentParameterCollectionBuilder<Editable<EditableTestItem>>>? configure = null)
    {
        return RenderComponent<Editable<EditableTestItem>>(parameters =>
        {
            parameters.Add(component => component.Item, item);
            parameters.Add(component => component.ParameterName, parameterName);
            parameters.Add(component => component.ItemChanged, EventCallback.Factory.Create(this, itemChanged));
            configure?.Invoke(parameters);
        });
    }

    private sealed class EditableTestItem
    {
        public string Title { get; set; } = string.Empty;

        public int Confidence { get; set; }

        public bool RequiresApproval { get; set; }
    }
}
