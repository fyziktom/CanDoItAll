using System.Globalization;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Editing;
using CanDoItAll.Editing.UI;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class AssignmentPendingComparisonTests {
    [Fact]
    public void A_pending_number_shows_saved_values_without_claiming_the_last_parse_as_the_draft() {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var field = new MergeField<decimal>(101.25m, 999m, 234.50m);
        var cut = context.Render<MergeFieldView<decimal>>(parameters => parameters
            .Add(component => component.Label, "Allocation %")
            .Add(component => component.Field, field)
            .Add(component => component.Format, value => value.ToString("0.00", CultureInfo.InvariantCulture))
            .Add(component => component.HasPendingInput, true)
            .Add(component => component.ShowUnchanged, false));

        Assert.Contains("101.25", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("234.50", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("999.00", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Your unfinished entry stays in the editor.", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("button"));
        Assert.Equal(MergeChoice.Unresolved, field.Choice);
    }
}
