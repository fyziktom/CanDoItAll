using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UI.Agents;
using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.CrmHr.UI.Workforce;
using CanDoItAll.CrmHr.UiSandbox;
using CanDoItAll.CrmHr.UiSandbox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrWorkspaceSandboxTests : IDisposable
{
    private readonly BunitContext context = new();

    public CrmHrWorkspaceSandboxTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddCanDoItAllCharts();
        context.Services.AddCrmHrSandboxReadPorts();
    }

    public void Dispose() => context.Dispose();

    private IRenderedComponent<Routes> Render(string route, string scenario)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"{route}?scenario={scenario}&layout=matched");
        return context.Render<Routes>();
    }

    // --- Directory ---------------------------------------------------------------------------

    [Theory]
    [InlineData("catalog", "data-testid=\"crmhr-directory-catalog\"")]
    [InlineData("selected-existing", "Jonas Keller")]
    [InlineData("new-draft", "New party record")]
    [InlineData("non-default-section", "crmhr-merge-open-")]
    [InlineData("loading", "Loading relationships and duplicate candidates")]
    [InlineData("failed", "Relationship data could not be loaded.")]
    [InlineData("unavailable-references", "Unknown party")]
    [InlineData("empty", "No duplicate candidates are currently detected for this party.")]
    public void Directory_scenarios_restore_with_the_real_surface_and_a_characteristic_marker(string scenario, string marker)
    {
        var cut = Render("/crm-hr/workspaces/directory", scenario);

        cut.WaitForAssertion(() => Assert.Contains(marker, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrDirectoryWorkspaceSurface>());
    }

    [Fact]
    public void Directory_a_new_draft_can_be_saved_and_the_intent_is_recorded()
    {
        var cut = Render("/crm-hr/workspaces/directory", "catalog");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-directory-new-button']")));

        cut.Find("[data-testid='crmhr-directory-new-button']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-party-display-name']")));

        cut.Find("[data-testid='crmhr-party-display-name']").Change("Riverton Textiles");
        cut.Find("#crmhr-directory-record-form").Submit();

        cut.WaitForAssertion(() => Assert.StartsWith("Save party: created", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal));
    }

    // --- Crm -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("catalog", "data-testid=\"crmhr-crm-catalog\"")]
    [InlineData("selected-existing", "Northwind Logistics")]
    [InlineData("new-draft", "data-testid=\"crmhr-opportunity-create-dialog\"")]
    [InlineData("non-default-section", "Fulfillment expansion")]
    [InlineData("loading", "data-testid=\"crmhr-account-activity-counts-unavailable\"")]
    [InlineData("failed", "data-testid=\"crmhr-account-activity-error\"")]
    [InlineData("unavailable-references", "Unavailable directory record")]
    [InlineData("empty", "Beacon Hill Advisory")]
    [InlineData("busy", "data-testid=\"crmhr-opportunity-edit-dialog\"")]
    public void Crm_scenarios_restore_with_the_real_surface_and_a_characteristic_marker(string scenario, string marker)
    {
        var cut = Render("/crm-hr/workspaces/crm", scenario);

        cut.WaitForAssertion(() => Assert.Contains(marker, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrCrmWorkspaceSurface>());
    }

    [Fact]
    public void Crm_saving_the_account_profile_updates_local_state_and_records_the_intent()
    {
        var cut = Render("/crm-hr/workspaces/crm", "selected-existing");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-account-commercial-notes']")));

        cut.Find("[data-testid='crmhr-account-commercial-notes']").Change("Renewal confirmed for next quarter.");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.StartsWith("Save CRM profile:", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal));
    }

    // --- Workforce -------------------------------------------------------------------------------

    [Theory]
    [InlineData("catalog", "data-testid=\"crmhr-workforce-catalog\"")]
    [InlineData("selected-existing", "Elena Ward")]
    [InlineData("new-draft", "id=\"crmhr-workforce-profile-form\"")]
    [InlineData("non-default-section", "data-testid=\"crmhr-workforce-allocation-gantt\"")]
    [InlineData("loading", "data-testid=\"crmhr-workforce-allocations-loading\"")]
    [InlineData("failed", "data-testid=\"crmhr-workforce-allocations-error\"")]
    [InlineData("unavailable-references", "Unavailable project")]
    [InlineData("empty", "No project allocations are currently pushing capacity from the project module.")]
    [InlineData("busy", "data-testid=\"crmhr-workforce-delivery-unit-dialog\"")]
    public void Workforce_scenarios_restore_with_the_real_surface_and_a_characteristic_marker(string scenario, string marker)
    {
        var cut = Render("/crm-hr/workspaces/workforce", scenario);

        cut.WaitForAssertion(() => Assert.Contains(marker, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrWorkforceWorkspaceSurface>());
    }

    [Fact]
    public void Workforce_saving_the_profile_tab_updates_local_state_and_records_the_intent()
    {
        var cut = Render("/crm-hr/workspaces/workforce", "selected-existing");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-tab-profile']")));

        cut.Find("[data-testid='crmhr-workforce-tab-profile']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-job-title']")));

        cut.Find("[data-testid='crmhr-workforce-job-title']").Change("Senior Delivery Lead");
        cut.Find("#crmhr-workforce-profile-form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Save workforce profile", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent));
    }

    // --- Recruiting -------------------------------------------------------------------------------

    [Theory]
    [InlineData("catalog", "data-testid=\"crmhr-recruiting-catalog\"")]
    [InlineData("selected-existing", "Lucas Meyer")]
    [InlineData("new-draft", "Recruiting workspace ready")]
    [InlineData("non-default-section", "data-testid=\"crmhr-recruiting-interview-item\"")]
    [InlineData("unavailable-references", "Unavailable recruiter")]
    [InlineData("empty", "Ava Thompson")]
    public void Recruiting_scenarios_restore_with_the_real_surface_and_a_characteristic_marker(string scenario, string marker)
    {
        var cut = Render("/crm-hr/workspaces/recruiting", scenario);

        cut.WaitForAssertion(() => Assert.Contains(marker, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrRecruitingWorkspaceSurface>());
    }

    [Fact]
    public void Recruiting_creating_a_new_application_updates_local_state_and_records_the_intent()
    {
        var cut = Render("/crm-hr/workspaces/recruiting", "catalog");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-new-button']")));

        cut.Find("[data-testid='crmhr-recruiting-new-button']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Create new application", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent));
        Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-record-dialog']"));
        Assert.Contains("Recruiting workspace ready", cut.Markup, StringComparison.Ordinal);
    }

    // --- Agents -------------------------------------------------------------------------------

    [Theory]
    [InlineData("catalog", "data-testid=\"crmhr-agent-catalog\"")]
    [InlineData("selected-existing", "Atlas Ops Agent")]
    [InlineData("loading", "Loading CRM-HR agent details")]
    [InlineData("unavailable-references", "Scheduler Agent")]
    [InlineData("empty", "No projected AI agents")]
    public void Agents_scenarios_restore_with_the_real_surface_and_a_characteristic_marker(string scenario, string marker)
    {
        var cut = Render("/crm-hr/workspaces/agents", scenario);

        cut.WaitForAssertion(() => Assert.Contains(marker, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrAgentsWorkspaceSurface>());
    }

    [Fact]
    public void Agents_opening_the_technical_catalog_records_the_intent()
    {
        var cut = Render("/crm-hr/workspaces/agents", "catalog");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-agent-open-catalog']")));

        cut.Find("[data-testid='crmhr-agent-open-catalog']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Open technical catalog", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent));
    }

    // --- Assignments -------------------------------------------------------------------------------

    [Theory]
    [InlineData("catalog", "Choose a project to load its resource schedule and assignment workflows.")]
    [InlineData("selected-existing", "Jonas Keller")]
    [InlineData("new-draft", "Northwind Warehouse Automation")]
    [InlineData("non-default-section", "Atlas Ops Agent")]
    [InlineData("loading", "Loading the bounded schedule window")]
    [InlineData("unavailable-references", "Unavailable party")]
    [InlineData("empty", "Beacon Hill Advisory Engagement")]
    public void Assignments_scenarios_restore_with_the_real_surface_and_a_characteristic_marker(string scenario, string marker)
    {
        var cut = Render("/crm-hr/workspaces/assignments", scenario);

        cut.WaitForAssertion(() => Assert.Contains(marker, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrAssignmentsWorkspaceSurface>());
    }

    // A fixed production defect once passed the literal parameter names ("relationshipAssignmentSearchText",
    // "staffingRequestSearchText", "allocationAssignmentSearchText", "candidateSearchText") as search text instead of
    // the view's real properties; these four inputs must always render the view's text.
    [Fact]
    public void Assignments_search_inputs_never_render_the_literal_property_names()
    {
        var cut = Render("/crm-hr/workspaces/assignments", "selected-existing");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-assignment-search']")));
        AssertSearchValueIsNotTheLiteralPropertyName(cut, "crmhr-assignment-search", "relationshipAssignmentSearchText");

        cut.Find("[data-testid='crmhr-assignment-search']").Input("Jonas");
        cut.WaitForAssertion(() => Assert.Equal("Jonas", cut.Find("[data-testid='crmhr-assignment-search']").GetAttribute("value")));
        Assert.StartsWith("Change relationship search text:", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-assignments-tab-staffing']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-staffing-request-search']")));
        AssertSearchValueIsNotTheLiteralPropertyName(cut, "crmhr-staffing-request-search", "staffingRequestSearchText");

        cut.Find("[data-testid='crmhr-assignments-tab-allocations']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-allocation-filter-search']")));
        AssertSearchValueIsNotTheLiteralPropertyName(cut, "crmhr-allocation-filter-search", "allocationAssignmentSearchText");

        // The candidate search box only exists inside the allocation create dialog.
        cut.Find("[data-testid='crmhr-allocation-create-button']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-allocation-candidate-search']")));
        AssertSearchValueIsNotTheLiteralPropertyName(cut, "crmhr-allocation-candidate-search", "candidateSearchText");
    }

    private static void AssertSearchValueIsNotTheLiteralPropertyName(IRenderedComponent<Routes> cut, string testId, string forbiddenLiteral)
    {
        var value = cut.Find($"[data-testid='{testId}']").GetAttribute("value") ?? string.Empty;
        Assert.NotEqual(forbiddenLiteral, value);
    }

    // --- Agent recruiting evidence -------------------------------------------------------------------------------

    [Theory]
    [InlineData("no-candidate", "No bound AgentFramework candidate")]
    [InlineData("loading", "Loading candidate readiness")]
    [InlineData("failed", "The candidate readiness and assessment evidence could not be loaded.")]
    [InlineData("ready", "Recruiting Scout Agent")]
    [InlineData("create-dialog-open", "data-testid=\"crmhr-assessment-create-dialog\"")]
    [InlineData("attach-dialog-open", "data-testid=\"crmhr-assessment-attach-dialog\"")]
    public void Agent_evidence_scenarios_restore_with_the_real_surface_and_a_characteristic_marker(string scenario, string marker)
    {
        var cut = Render("/crm-hr/workspaces/agent-evidence", scenario);

        cut.WaitForAssertion(() => Assert.Contains(marker, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<AgentRecruitingEvidenceSurface>());
    }

    [Fact]
    public void Agent_evidence_creating_an_assessment_updates_local_state_and_records_the_intent()
    {
        var cut = Render("/crm-hr/workspaces/agent-evidence", "create-dialog-open");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-assessment-purpose']")));

        cut.Find("[data-testid='crmhr-assessment-purpose']").Change("Escalation judgment recheck");
        cut.Find("[data-testid='crmhr-assessment-create-save']").Click();

        cut.WaitForAssertion(() => Assert.Equal("Create assessment: Escalation judgment recheck", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-assessment-create-dialog']"));
    }

    // --- Module boundary -------------------------------------------------------------------------------

    [Fact]
    public void Sandbox_assembly_has_no_module_infrastructure_or_ef_core_reference()
    {
        var referencedAssemblyNames = typeof(CrmHrDirectoryWorkspaceSpecimen).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain(referencedAssemblyNames, name => string.Equals(name, "CanDoItAll.Modules.CrmHr", StringComparison.Ordinal));
        Assert.DoesNotContain(referencedAssemblyNames, name => string.Equals(name, "CanDoItAll.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(referencedAssemblyNames, name => name is not null && name.Contains("EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Each_specimen_composes_the_same_real_surface_type_the_module_pages_compose()
    {
        Assert.Same(typeof(CrmHrDirectoryWorkspaceSurface), Render("/crm-hr/workspaces/directory", "catalog").FindComponent<CrmHrDirectoryWorkspaceSurface>().Instance.GetType());
        Assert.Same(typeof(CrmHrCrmWorkspaceSurface), Render("/crm-hr/workspaces/crm", "catalog").FindComponent<CrmHrCrmWorkspaceSurface>().Instance.GetType());
        Assert.Same(typeof(CrmHrWorkforceWorkspaceSurface), Render("/crm-hr/workspaces/workforce", "catalog").FindComponent<CrmHrWorkforceWorkspaceSurface>().Instance.GetType());
        Assert.Same(typeof(CrmHrRecruitingWorkspaceSurface), Render("/crm-hr/workspaces/recruiting", "catalog").FindComponent<CrmHrRecruitingWorkspaceSurface>().Instance.GetType());
        Assert.Same(typeof(CrmHrAgentsWorkspaceSurface), Render("/crm-hr/workspaces/agents", "catalog").FindComponent<CrmHrAgentsWorkspaceSurface>().Instance.GetType());
        Assert.Same(typeof(CrmHrAssignmentsWorkspaceSurface), Render("/crm-hr/workspaces/assignments", "catalog").FindComponent<CrmHrAssignmentsWorkspaceSurface>().Instance.GetType());
        Assert.Same(typeof(AgentRecruitingEvidenceSurface), Render("/crm-hr/workspaces/agent-evidence", "ready").FindComponent<AgentRecruitingEvidenceSurface>().Instance.GetType());
    }
}
