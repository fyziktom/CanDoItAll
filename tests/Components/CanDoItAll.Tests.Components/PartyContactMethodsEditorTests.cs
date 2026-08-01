using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Components;

public sealed class PartyContactMethodsEditorTests {
    [Fact]
    public async Task Wizard_keeps_an_isolated_draft_across_back_and_discards_it_on_cancel() {
        using var context = CreateContext();
        var contactPoints = new List<PartyContactPointEditorModel>();
        var cut = context.Render<PartyContactMethodsEditor>(parameters => parameters
            .Add(component => component.ContactPoints, contactPoints));

        cut.Find("[data-testid='crmhr-contact-add']").Click();

        Assert.Empty(contactPoints);
        Assert.Equal(
            6,
            cut.FindAll("[data-testid^='crmhr-contact-wizard-type-']").Count);
        var emailTypeCard = cut.Find("[data-testid='crmhr-contact-wizard-type-email']");
        Assert.Contains("aspect-square", emailTypeCard.ClassList);
        Assert.Contains("items-center", emailTypeCard.ClassList);
        Assert.Contains("justify-center", emailTypeCard.ClassList);

        emailTypeCard.Click();
        cut.Find("[data-testid='crmhr-contact-wizard-next']").Click();
        cut.Find("[data-testid='crmhr-contact-wizard-value']")
            .Change("sales@example.test");
        cut.Find("[data-testid='crmhr-contact-wizard-label']")
            .Change("Sales desk");

        await cut.InvokeAsync(() => cut.FindComponent<TagEditor>()
            .Instance.ValueChanged.InvokeAsync(["sales", "preferred"]));
        await cut.InvokeAsync(() => cut.FindComponent<TextArea>()
            .Instance.ValueChanged.InvokeAsync("Available during business hours."));

        cut.Find("[data-testid='crmhr-contact-wizard-back']").Click();

        Assert.Empty(contactPoints);
        Assert.Equal(
            "true",
            cut.Find("[data-testid='crmhr-contact-wizard-type-email']")
                .GetAttribute("aria-pressed"));

        cut.Find("[data-testid='crmhr-contact-wizard-next']").Click();

        Assert.Equal(
            "sales@example.test",
            cut.Find("[data-testid='crmhr-contact-wizard-value']")
                .GetAttribute("value"));
        Assert.Equal(
            ["sales", "preferred"],
            cut.FindComponent<TagEditor>().Instance.Value);
        Assert.Equal(
            "Available during business hours.",
            cut.FindComponent<TextArea>().Instance.Value);

        cut.Find("[data-testid='crmhr-contact-wizard-cancel']").Click();

        Assert.Empty(contactPoints);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-contact-wizard']"));
    }

    [Fact]
    public async Task Invalid_finish_stays_open_and_valid_finish_adds_exactly_one_contact() {
        using var context = CreateContext();
        var contactPoints = new List<PartyContactPointEditorModel>();
        var cut = context.Render<PartyContactMethodsEditor>(parameters => parameters
            .Add(component => component.ContactPoints, contactPoints));

        cut.Find("[data-testid='crmhr-contact-add']").Click();
        cut.Find("[data-testid='crmhr-contact-wizard-type-phone']").Click();
        cut.Find("[data-testid='crmhr-contact-wizard-next']").Click();
        cut.Find("[data-testid='crmhr-contact-wizard-finish']").Click();

        Assert.Empty(contactPoints);
        Assert.Contains("Enter a contact value.", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("[data-testid='crmhr-contact-wizard']"));

        cut.Find("[data-testid='crmhr-contact-wizard-value']")
            .Change("+1 555 0100");
        cut.Find("[data-testid='crmhr-contact-wizard-label']")
            .Change("After-hours");
        cut.Find("[data-testid='crmhr-contact-wizard-public']")
            .Change(false);
        await cut.InvokeAsync(() => cut.FindComponent<TagEditor>()
            .Instance.ValueChanged.InvokeAsync(["urgent", " urgent ", "support"]));
        await cut.InvokeAsync(() => cut.FindComponent<TextArea>()
            .Instance.ValueChanged.InvokeAsync("Escalation line."));

        cut.Find("[data-testid='crmhr-contact-wizard-finish']").Click();

        var contactPoint = Assert.Single(contactPoints);
        Assert.Equal(PartyContactType.Phone, contactPoint.ContactType);
        Assert.Equal("After-hours", contactPoint.Label);
        Assert.Equal("+1 555 0100", contactPoint.Value);
        Assert.False(contactPoint.IsPrimary);
        Assert.False(contactPoint.IsPublic);
        Assert.Equal(["urgent", "support"], contactPoint.Tags);
        Assert.Equal("Escalation line.", contactPoint.Notes);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-contact-wizard']"));
    }

    [Fact]
    public void Remove_callback_targets_the_rendered_contact_after_reorder() {
        using var context = CreateContext();
        var first = new PartyContactPointEditorModel {
            ContactType = PartyContactType.Email,
            Value = "first@example.test"
        };
        var second = new PartyContactPointEditorModel {
            ContactType = PartyContactType.Phone,
            Value = "+15550101"
        };
        var contactPoints = new List<PartyContactPointEditorModel> { first, second };
        var cut = context.Render<PartyContactMethodsEditor>(parameters => parameters
            .Add(component => component.ContactPoints, contactPoints));

        contactPoints.Reverse();
        cut.Render(parameters => parameters
            .Add(component => component.ContactPoints, contactPoints));
        cut.Find("[data-testid='crmhr-contact-remove-0']").Click();

        Assert.Single(contactPoints);
        Assert.Same(first, contactPoints[0]);
    }

    [Fact]
    public void One_row_remove_does_not_capture_the_terminal_loop_index() {
        using var context = CreateContext();
        var contactPoint = new PartyContactPointEditorModel {
            ContactType = PartyContactType.Email,
            Value = "only@example.test"
        };
        var contactPoints = new List<PartyContactPointEditorModel> { contactPoint };
        var cut = context.Render<PartyContactMethodsEditor>(parameters => parameters
            .Add(component => component.ContactPoints, contactPoints));

        cut.Find("[data-testid='crmhr-contact-remove-0']").Click();

        Assert.Empty(contactPoints);
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
