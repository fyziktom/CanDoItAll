using System.Reflection;
using System.Text.RegularExpressions;
using CanDoItAll.CrmHr.UI;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components.CrmHr;

// Guards the dependency direction of the whole CRM / HR rendering library: routed hosts and effect adapters in the
// module, renderers and view contracts in the library, lightweight contracts below both. The guards name dependency
// categories and roles; they do not count files or interfaces.
public sealed class CrmHrUiModuleBoundaryTests
{
    // Narrow read ports a renderer may inject: paged catalog and picker sources declared by the contract assemblies.
    private static readonly Type[] ReadPorts =
    [
        typeof(IPartyRecordQueryService),
        typeof(IWorkforceRecordQueryService),
        typeof(IOpportunityPipelineQueryService),
        typeof(IPartyOrganizationAffiliationReader),
        typeof(IProjectRecordQueryService)
    ];

    public static TheoryData<Type, Type, Type> Workspaces => new()
    {
        { typeof(CrmHrDirectoryWorkspaceSurface), typeof(ICrmHrDirectoryWorkspaceView), typeof(CrmHrDirectoryPage) },
        { typeof(CrmHrCrmWorkspaceSurface), typeof(ICrmHrCrmWorkspaceView), typeof(CrmHrCrmPage) },
        { typeof(CrmHrWorkforceWorkspaceSurface), typeof(ICrmHrWorkforceWorkspaceView), typeof(CrmHrWorkforcePage) },
        { typeof(CrmHrRecruitingWorkspaceSurface), typeof(ICrmHrRecruitingWorkspaceView), typeof(CrmHrRecruitingPage) },
        { typeof(CrmHrAgentsWorkspaceSurface), typeof(ICrmHrAgentsWorkspaceView), typeof(CrmHrAgentsPage) },
        { typeof(CrmHrAssignmentsWorkspaceSurface), typeof(ICrmHrAssignmentsWorkspaceView), typeof(CrmHrAssignmentsPage) },
        { typeof(AgentRecruitingEvidenceSurface), typeof(IAgentRecruitingEvidenceView), typeof(AgentRecruitingEvidencePanel) }
    };

    [Fact]
    public void Transitive_reference_closure_contains_no_persistence_or_implementation_assembly()
    {
        var closure = CrmHrUiBoundary.TransitiveReferenceNames();

        Assert.Contains("CanDoItAll.Modules.CrmHr.Contracts", closure);
        Assert.All(closure, name => Assert.False(CrmHrUiBoundary.IsForbidden(name), $"The rendering graph reaches {name}"));
    }

    [Fact]
    public void Renderers_inject_only_read_ports_and_logging()
    {
        var components = CrmHrUiBoundary.RenderingLibrary.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();
        Assert.NotEmpty(components);

        var injected = components
            .SelectMany(component => component
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)
                .Select(property => (Component: component.Name, property.PropertyType)))
            .ToArray();

        Assert.All(injected, item =>
            Assert.True(
                ReadPorts.Contains(item.PropertyType) ||
                (item.PropertyType.IsGenericType && item.PropertyType.GetGenericTypeDefinition() == typeof(ILogger<>)),
                $"{item.Component} injects {item.PropertyType.FullName}"));
    }

    [Fact]
    public void Read_ports_are_contract_interfaces_without_commands()
    {
        var readName = new Regex("^(Search|Get|List|Count|Find|Resolve)", RegexOptions.CultureInvariant);
        Assert.All(ReadPorts, port =>
        {
            Assert.True(port.IsInterface, $"{port.Name} is not an interface");
            Assert.EndsWith(".Contracts", port.Assembly.GetName().Name, StringComparison.Ordinal);
            Assert.All(port.GetMethods(), method =>
                Assert.True(readName.IsMatch(method.Name), $"{port.Name}.{method.Name} does not look like a read"));
        });
    }

    [Theory]
    [MemberData(nameof(Workspaces))]
    public void Workspace_surface_binds_to_a_view_contract_its_host_implements(Type surface, Type view, Type host)
    {
        Assert.Same(CrmHrUiBoundary.RenderingLibrary, surface.Assembly);
        Assert.Same(CrmHrUiBoundary.RenderingLibrary, view.Assembly);
        Assert.True(view.IsInterface);
        Assert.True(typeof(ICrmHrWorkspaceView).IsAssignableFrom(view));
        Assert.True(typeof(CrmHrWorkspaceSurface<>).MakeGenericType(view).IsAssignableFrom(surface), $"{surface.Name} does not render {view.Name}");

        Assert.Equal("CanDoItAll.Modules.CrmHr", host.Assembly.GetName().Name);
        Assert.True(view.IsAssignableFrom(host), $"{host.Name} does not implement {view.Name}");

        Assert.Empty(surface
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null));

        // Everything the contract exposes stays in the light graph.
        var exposed = view.GetProperties().Select(property => property.PropertyType)
            .Concat(view.GetMethods().SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
            .SelectMany(CrmHrUiBoundary.Expand)
            .Where(type => !type.IsGenericParameter)
            .Select(type => type.Assembly.GetName().Name ?? string.Empty)
            .Distinct()
            .ToArray();
        Assert.All(exposed, name => Assert.True(CrmHrUiBoundary.IsLightGraphAssembly(name), $"{view.Name} exposes assembly {name}"));
    }

    [Fact]
    public void Contract_assemblies_stay_lightweight()
    {
        var crm = typeof(PartyEditorModel).Assembly;
        var projects = typeof(ProjectWriteAdmission).Assembly;
        Assert.Equal("CanDoItAll.Modules.CrmHr.Contracts", crm.GetName().Name);
        Assert.Equal("CanDoItAll.Modules.Projects.Contracts", projects.GetName().Name);

        string[] crmAllowed = ["CanDoItAll.SharedKernel", "CanDoItAll.Modules.Projects.Contracts", "CanDoItAll.AgentFramework.Models"];
        Assert.All(References(crm), name => Assert.True(name.StartsWith("System", StringComparison.Ordinal) || crmAllowed.Contains(name), $"CRM / HR contracts reference {name}"));
        Assert.All(References(projects), name => Assert.True(name.StartsWith("System", StringComparison.Ordinal) || name == "CanDoItAll.SharedKernel", $"Projects contracts reference {name}"));

        // The implementation modules consume their contracts, never the other way around.
        Assert.Contains("CanDoItAll.Modules.CrmHr.Contracts", References(typeof(CrmHrHomePage).Assembly));
        Assert.Contains("CanDoItAll.Modules.Projects.Contracts", References(typeof(ProjectWriteAdmissionService).Assembly));
    }

    [Fact]
    public void Module_components_are_routed_hosts_or_named_effect_adapters()
    {
        // Every Razor component that stays in the module has a host or adapter role. A new component here needs the
        // same decision: render it from the library, or name its effect role in this list and in the completion record.
        Type[] adapters =
        [
            typeof(AccountSummaryPanel),            // maps the workspace model and fences the rendered action origin
            typeof(InteractionTimeline),            // adapts the history session's presentation and paging admission
            typeof(CrmFinancialsPanel),             // owns the Financials read session for one account
            typeof(AgentRecruitingEvidencePanel),   // owns the technical owner's evidence reads and commands
            typeof(CrmAgentChatContextProvider),    // publishes the CRM agent-chat context; renders nothing
            typeof(CrmHrSecondaryTabs)              // performs area navigation for the library's tab renderer
        ];

        var components = typeof(CrmHrHomePage).Assembly.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract && !type.IsNested)
            .ToArray();
        var unexplained = components
            .Where(type => type.GetCustomAttribute<RouteAttribute>() is null && !adapters.Contains(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(unexplained);
        Assert.Equal(7, components.Count(type => type.GetCustomAttribute<RouteAttribute>() is not null));
    }

    private static string[] References(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();
}
