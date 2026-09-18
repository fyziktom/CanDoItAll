using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UiSandbox;
using CanDoItAll.CrmHr.UiSandbox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// A record dialog's footer Save sits outside the form element. It validates the same form the submit path validates,
// parsing errors included, before the host sees the action: an unparsable date or number never becomes a save with the
// previous value. The production surfaces render against the sandbox views, whose intent record shows what reached the
// host.
public sealed class CrmHrFooterSaveValidationTests : IDisposable
{
    private readonly BunitContext context = new();

    public CrmHrFooterSaveValidationTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddCanDoItAllCharts();
        context.Services.AddCrmHrSandboxReadPorts();
    }

    public void Dispose() => context.Dispose();

    [Fact]
    public void Workforce_footer_save_is_refused_while_the_profile_form_has_a_parsing_error_and_saves_once_it_is_valid()
    {
        var cut = Render("/crm-hr/workspaces/workforce", "selected-existing");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-tab-profile']")));
        cut.Find("[data-testid='crmhr-workforce-tab-profile']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-capacity']")));
        var intentBefore = Intent(cut);

        cut.Find("[data-testid='crmhr-workforce-capacity']").Change("forty");
        cut.Find("[data-testid='crmhr-workforce-save-button']").Click();

        Assert.Equal(intentBefore, Intent(cut));
        Assert.Contains("invalid", cut.Find("[data-testid='crmhr-workforce-capacity']").ClassList);

        cut.Find("[data-testid='crmhr-workforce-capacity']").Change("32");
        cut.Find("[data-testid='crmhr-workforce-save-button']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Save workforce profile", Intent(cut)));
    }

    [Fact]
    public void Directory_footer_save_is_refused_while_a_role_date_cannot_be_parsed_and_saves_once_it_is_valid()
    {
        var cut = Render("/crm-hr/workspaces/directory", "new-draft");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-party-display-name']")));
        cut.Find("[data-testid='crmhr-party-display-name']").Change("Footer validation party");
        cut.Find("[data-testid='crmhr-party-role-add']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-party-extra-role-start-0']")));
        var intentBefore = Intent(cut);

        cut.Find("[data-testid='crmhr-party-extra-role-start-0']").Change("not a date");
        cut.Find("[data-testid='crmhr-party-save-button']").Click();

        Assert.Equal(intentBefore, Intent(cut));
        Assert.Contains("invalid", cut.Find("[data-testid='crmhr-party-extra-role-start-0']").ClassList);

        cut.Find("[data-testid='crmhr-party-extra-role-start-0']").Change("2026-09-01");
        cut.Find("[data-testid='crmhr-party-save-button']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Save party: created Footer validation party", Intent(cut)));
    }

    private IRenderedComponent<Routes> Render(string route, string scenario)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"{route}?scenario={scenario}&layout=matched");
        return context.Render<Routes>();
    }

    private static string Intent(IRenderedComponent<Routes> cut)
        => cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent.Trim();
}
