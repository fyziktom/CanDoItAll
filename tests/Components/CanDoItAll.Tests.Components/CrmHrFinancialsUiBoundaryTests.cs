using System.Reflection;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UI.Financials;
using CanDoItAll.CrmHr.UiSandbox.Components;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Guards the boundary of the Financials surface: its inputs stay in the light graph plus the chart library, it
// injects nothing, the rendering library's direct references gained exactly the chart library, and the production
// host and the sandbox compose the same surface with the real chart component in their rendered trees.
public sealed class CrmHrFinancialsUiBoundaryTests
{
    private static readonly string[] AllowedReferencePrefixes =
    [
        "System",
        "netstandard",
        "Microsoft.AspNetCore.Components",
        "Microsoft.Extensions",
        "CanDoItAll.Components.BaseLib",
        "CanDoItAll.Components.Charts",
        "CanDoItAll.Components.Common"
    ];

    [Fact]
    public void Surface_parameters_inject_nothing_and_expose_only_light_graph_and_chart_types()
    {
        var surface = typeof(CrmHrFinancialsSurface);
        var properties = surface.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.Empty(properties.Where(property => property.GetCustomAttribute<InjectAttribute>() is not null));

        var parameterAssemblies = properties
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(property => property.PropertyType)
            .SelectMany(Expand)
            .Select(type => type.Assembly.GetName().Name ?? string.Empty)
            .Distinct()
            .ToArray();
        Assert.NotEmpty(parameterAssemblies);
        Assert.All(parameterAssemblies, name =>
            Assert.True(
                AllowedReferencePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) || name == surface.Assembly.GetName().Name,
                $"{surface.Name} exposes assembly {name} in a parameter"));
    }

    [Fact]
    public void Rendering_library_references_the_chart_library_directly_and_still_nothing_from_the_module_graph()
    {
        var references = typeof(CrmHrFinancialsSurface).Assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();

        Assert.Contains("CanDoItAll.Components.Charts", references);
        Assert.All(references, reference =>
            Assert.True(
                AllowedReferencePrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)),
                $"Unexpected reference from the rendering library: {reference}"));
        // The chart implementation's own dependency is reached through the chart library, never referenced directly.
        Assert.DoesNotContain(references, reference => reference.StartsWith("Blazor-ApexCharts", StringComparison.Ordinal));
        Assert.Same(typeof(CrmHrFinancialsSurface).Assembly, typeof(CrmHrFinancialsPresentation).Assembly);
        Assert.Same(typeof(CrmHrFinancialsSurface).Assembly, typeof(CrmHrFinancialsChart).Assembly);
    }

    [Fact]
    public void Production_host_keeps_its_name_and_account_entry_and_composes_the_surface_with_the_real_chart()
    {
        var accountEntry = typeof(CrmFinancialsPanel).GetProperty(nameof(CrmFinancialsPanel.AccountPartyId));
        Assert.NotNull(accountEntry);
        Assert.Equal(typeof(Guid), accountEntry!.PropertyType);
        Assert.NotNull(accountEntry.GetCustomAttribute<ParameterAttribute>());
        Assert.Contains(typeof(CrmHrCrmPage).Assembly.GetReferencedAssemblies(), reference => reference.Name == typeof(CrmHrFinancialsSurface).Assembly.GetName().Name);

        using var context = CreateContext();
        var accountId = Guid.NewGuid();
        context.Services.AddSingleton<ICrmFinancialSnapshotQueryService>(new StaticFinancialQuery(Snapshot(accountId)));

        var panel = context.Render<CrmFinancialsPanel>(parameters => parameters.Add(component => component.AccountPartyId, accountId));

        panel.WaitForAssertion(() => Assert.Equal("ready", panel.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-phase")));
        Assert.Single(panel.FindComponents<CrmHrFinancialsSurface>());
        Assert.Single(panel.FindComponents<CdaChart>());
    }

    [Fact]
    public void Sandbox_specimen_composes_the_surface_with_the_real_chart_without_the_module()
    {
        using var context = CreateContext();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/crm-hr/financials?scenario=populated");

        var specimen = context.Render<Routes>();

        specimen.WaitForAssertion(() => Assert.Single(specimen.FindComponents<CrmHrFinancialsSurface>()));
        Assert.Single(specimen.FindComponents<CdaChart>());
        Assert.DoesNotContain(
            typeof(CrmHrFinancialsSpecimen).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == typeof(CrmHrCrmPage).Assembly.GetName().Name);
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Expand))
            {
                yield return argument;
            }
        }
    }

    private static CrmAccountFinancialSnapshot Snapshot(Guid accountId)
        => new(
            accountId,
            FinancialDataAvailability.Available,
            [new CrmCurrencyAmount("EUR", 1m)],
            [new CrmFinancialPeriodAmount(new DateOnly(2026, 1, 1), "EUR", 1m)],
            [new CrmFinancialPeriodAmount(new DateOnly(2026, 1, 1), "EUR", 1m)],
            0,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable);

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private sealed class StaticFinancialQuery(CrmAccountFinancialSnapshot snapshot) : ICrmFinancialSnapshotQueryService
    {
        public Task<CrmAccountFinancialSnapshot> GetAsync(Guid accountPartyId, CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }
}
