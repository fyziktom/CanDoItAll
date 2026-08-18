using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class SelectedReferenceTableTests : BunitContext
{
    public SelectedReferenceTableTests()
    {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Renders_readable_value_identifier_detail_and_status_in_a_semantic_table()
    {
        var items = new SelectedReferenceItem<string>[]
        {
            new("external-target/v1/9d6f", @"C:\work\customer-app", "external-target/v1/9d6f")
            {
                DetailText = "Available on this computer",
                StatusText = "Resolved",
                StatusTone = SelectedReferenceStatusTone.Success,
                TestId = "external-root-customer-app"
            }
        };

        var cut = Render<SelectedReferenceTable<string>>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.PrimaryColumnLabel, "Folder path")
            .Add(component => component.IdentifierColumnLabel, "Internal alias")
            .Add(component => component.DataTestId, "external-roots"));

        var table = cut.Find("[data-testid='external-roots-table']");
        Assert.Equal("table", table.TagName, ignoreCase: true);
        Assert.Collection(
            table.QuerySelectorAll("thead th"),
            heading => Assert.Equal("Folder path", heading.TextContent.Trim()),
            heading => Assert.Equal("Internal alias", heading.TextContent.Trim()));

        Assert.Equal(
            @"C:\work\customer-app",
            cut.Find("[data-testid='external-root-customer-app-primary'] strong").TextContent);
        Assert.Equal(
            "external-target/v1/9d6f",
            cut.Find("[data-testid='external-root-customer-app-identifier'] code").TextContent);
        Assert.Equal(
            "Available on this computer",
            cut.Find("[data-testid='external-root-customer-app-detail']").TextContent.Trim());
        Assert.Equal(
            "Resolved",
            cut.Find("[data-testid='external-root-customer-app-status']").TextContent.Trim());
        Assert.Equal("success", cut.FindComponent<StatusBadge>().Instance.Tone);
    }

    [Fact]
    public void Remove_requested_returns_the_row_key_and_respects_disabled_states()
    {
        var removedKeys = new List<Guid>();
        var removableId = Guid.Parse("d3bc3b89-6019-45ac-8e3d-9ac326033fc2");
        var lockedId = Guid.Parse("65be1afe-67fd-4ca1-954d-8aa50959d608");
        var items = new SelectedReferenceItem<Guid>[]
        {
            new(removableId, "Documents", removableId.ToString())
            {
                TestId = "storage-documents"
            },
            new(lockedId, "System storage", lockedId.ToString())
            {
                CanRemove = false,
                TestId = "storage-system"
            }
        };

        var cut = Render<SelectedReferenceTable<Guid>>(parameters => parameters
            .Add(component => component.Items, items)
            .Add(component => component.RemoveRequested, key => removedKeys.Add(key))
            .Add(component => component.DataTestId, "storage-catalogs"));

        cut.Find("[data-testid='storage-documents-remove']").Click();

        Assert.Equal(removableId, Assert.Single(removedKeys));
        Assert.Equal(
            "Remove Documents",
            cut.Find("[data-testid='storage-documents-remove']").GetAttribute("aria-label"));
        Assert.True(cut.Find("[data-testid='storage-system-remove']").HasAttribute("disabled"));

        cut.Render(parameters => parameters
            .Add(component => component.Disabled, true));

        Assert.Equal("true", cut.Find("[data-testid='storage-catalogs']").GetAttribute("aria-disabled"));
        Assert.All(cut.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
    }

    [Fact]
    public void Empty_and_view_only_states_avoid_unused_table_actions()
    {
        var cut = Render<SelectedReferenceTable<string>>(parameters => parameters
            .Add(component => component.Items, Array.Empty<SelectedReferenceItem<string>>())
            .Add(component => component.EmptyTitle, "No folders added")
            .Add(component => component.EmptyDescription, "Add a folder to make it available to this agent.")
            .Add(component => component.DataTestId, "external-roots"));

        Assert.Equal("No folders added", cut.Find("[data-testid='external-roots-empty-title']").TextContent);
        Assert.Equal(
            "Add a folder to make it available to this agent.",
            cut.Find("[data-testid='external-roots-empty-description']").TextContent);
        Assert.Empty(cut.FindAll("table"));

        cut.Render(parameters => parameters
            .Add(component => component.Items,
            [
                new SelectedReferenceItem<string>("id", "Readable name", "stable-id")
            ]));

        Assert.Equal(
            "tr",
            cut.Find("[data-testid='external-roots-row-stable-id']").TagName,
            ignoreCase: true);
        Assert.Equal(2, cut.FindAll("thead th").Count);
        Assert.Empty(cut.FindAll("button"));
    }
}
