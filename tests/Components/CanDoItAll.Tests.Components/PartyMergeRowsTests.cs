using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class PartyMergeRowsTests {
    [Fact]
    public async Task Oversized_comparison_previews_expand_in_batches_and_the_choice_keeps_the_complete_collection() {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var original = new PartyEditorModel {
            DisplayName = "Large collection",
            Addresses = Enumerable.Range(0, 1001).Select(index => new PartyAddressEditorModel { Id = Guid.NewGuid(), Line1 = $"Address {index}" }).ToList()
        };
        var local = original.Snapshot();
        var current = original.Snapshot();
        local.Addresses[0].Line1 = "Local address";
        current.Addresses[1000].Line1 = "Current final address";
        var review = new PartyMergeReview(original, local, current);
        var cut = context.Render<PartyMergeRows<PartyAddressEditorModel>>(parameters => parameters
            .Add(component => component.Title, "Addresses")
            .Add(component => component.Rows, review.Addresses)
            .Add(component => component.WholeCollection, review.LargeAddresses)
            .Add(component => component.Format, row => row.Line1));

        Assert.Contains("Choose one complete collection", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, cut.FindAll("[data-testid='crmhr-party-collection-snapshot']").Count);
        Assert.Equal(150, cut.FindAll("[data-testid='crmhr-party-collection-row']").Count);
        Assert.DoesNotContain("Current final address", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent == "Show more snapshot rows").ClickAsync());
        Assert.Equal(300, cut.FindAll("[data-testid='crmhr-party-collection-row']").Count);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent == "Use current").ClickAsync());

        Assert.True(review.CanApply);
        Assert.Equal(1001, review.Result().Addresses.Count);
        Assert.Equal("Current final address", review.Result().Addresses[1000].Line1);
    }
}
