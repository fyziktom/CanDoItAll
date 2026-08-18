using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class PartyAddressesEditorTests {
    [Fact]
    public void Remove_callback_targets_the_rendered_address_after_reorder() {
        using var context = new BunitContext();
        var first = new PartyAddressEditorModel {
            AddressType = "Billing",
            Line1 = "First address"
        };
        var second = new PartyAddressEditorModel {
            AddressType = "Shipping",
            Line1 = "Second address"
        };
        var addresses = new List<PartyAddressEditorModel> { first, second };
        var cut = context.Render<PartyAddressesEditor>(parameters => parameters
            .Add(component => component.Addresses, addresses));

        addresses.Reverse();
        cut.Render(parameters => parameters
            .Add(component => component.Addresses, addresses));
        cut.Find("[data-testid='crmhr-address-remove-0']").Click();

        Assert.Single(addresses);
        Assert.Same(first, addresses[0]);
    }
}
