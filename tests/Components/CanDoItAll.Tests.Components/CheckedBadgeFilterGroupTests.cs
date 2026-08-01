using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CheckedBadgeFilterGroupTests : BunitContext
{
    public CheckedBadgeFilterGroupTests()
    {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Toggle_returns_a_new_strongly_typed_set_and_exposes_pressed_state()
    {
        IReadOnlySet<FilterKind>? changedValue = null;
        var cut = Render<CheckedBadgeFilterGroup<FilterKind>>(parameters => parameters
            .Add(component => component.Options, CreateOptions())
            .Add(component => component.Value, new HashSet<FilterKind>
            {
                FilterKind.People,
                FilterKind.Agents
            })
            .Add(component => component.ValueChanged, value => changedValue = value)
            .Add(component => component.DataTestId, "resource-kind-filter"));

        Assert.Equal("true", cut.Find("[data-testid='filter-people']").GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find("[data-testid='filter-workflows']").GetAttribute("aria-pressed"));

        cut.Find("[data-testid='filter-people']").Click();

        Assert.NotNull(changedValue);
        Assert.DoesNotContain(FilterKind.People, changedValue);
        Assert.Contains(FilterKind.Agents, changedValue);
    }

    [Fact]
    public void Non_empty_mode_disables_the_last_checked_filter()
    {
        var callbackCount = 0;
        var cut = Render<CheckedBadgeFilterGroup<FilterKind>>(parameters => parameters
            .Add(component => component.Options, CreateOptions())
            .Add(component => component.Value, new HashSet<FilterKind>
            {
                FilterKind.Workflows
            })
            .Add(component => component.ValueChanged, _ => callbackCount++)
            .Add(component => component.AllowEmpty, false));

        var lastCheckedFilter = cut.Find("[data-testid='filter-workflows']");

        Assert.True(lastCheckedFilter.HasAttribute("disabled"));
        lastCheckedFilter.Click();
        Assert.Equal(0, callbackCount);
    }

    private static IReadOnlyList<CheckedBadgeFilterOption<FilterKind>> CreateOptions()
    {
        return
        [
            new(FilterKind.People, "People", "filter-people"),
            new(FilterKind.Agents, "Agents", "filter-agents"),
            new(FilterKind.Workflows, "Workflows", "filter-workflows")
        ];
    }

    private enum FilterKind
    {
        People,
        Agents,
        Workflows
    }
}
