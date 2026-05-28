using CanDoItAll.Mcp.Components.Catalog;
using CanDoItAll.Mcp.Components.Configuration;
using CanDoItAll.Mcp.Core.Contracts;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Components.Tests;

public sealed class ComponentCatalogServiceTests
{
    [Fact]
    public void Validator_Accepts_Repository_Component_Roots()
    {
        var options = CreateOptions();
        var validator = new McpServerOptionsValidator();

        var result = validator.Validate(name: null, options.Value);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Failures ?? []));
    }

    [Fact]
    public void Search_Finds_Button_Component_By_Name()
    {
        var service = CreateService();

        var result = service.Search("button");

        Assert.Contains(result.Components, component => string.Equals(component.Name, "Button", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_Finds_Dense_Sandbox_Examples_By_Scenario()
    {
        var service = CreateService();

        var result = service.Search("dense");

        Assert.Contains(result.Examples, example => string.Equals(example.Scenario, "Dense", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_Finds_Tabs_When_Querying_Long_Scroll_Relief()
    {
        var service = CreateService();

        var result = service.Search("long scroll");

        Assert.Contains(result.Components, component => string.Equals(component.Name, "Tabs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Component_Get_Returns_Workbench_Surface_Parameter_And_Examples()
    {
        var service = CreateService();

        var component = service.GetComponent("CanvasWorkbench");
        var examples = service.GetExamples("CanvasWorkbench");

        Assert.Equal("CanvasLib", component.Library);
        Assert.Contains(component.Parameters, parameter => string.Equals(parameter.Name, "Surface", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(examples.Examples, example => string.Equals(example.GroupKey, "canvas", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Css_Tokens_Return_Canvas_Stylesheet_Notes()
    {
        var service = CreateService();

        var result = service.GetCssTokens("CanvasWorkbench");

        Assert.Contains(result.Stylesheets, stylesheet => stylesheet.Contains("css/workbench/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.SourceFiles, file => file.EndsWith(Path.Combine("wwwroot", "css", "workbench", "shell", "01-layout-and-shell.css"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Notes, note => note.Contains("CanvasThemeTokenPack", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Canvas_Contracts_Return_Workbench_Surface_With_Nodes_Property()
    {
        var service = CreateService();

        var result = service.GetCanvasContracts("CanvasWorkbenchSurface");

        var contract = Assert.Single(result.Contracts, item => string.Equals(item.Name, "CanvasWorkbenchSurface", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(contract.Properties, property => string.Equals(property.Name, "Nodes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Groups_List_Returns_All_Required_Sandbox_Groups()
    {
        var service = CreateService();

        var groups = service.GetGroups();

        Assert.Equal(11, groups.Count);
        Assert.Contains(groups, group => string.Equals(group.Key, "charts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(groups, group => string.Equals(group.Key, "mermaid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(groups, group => string.Equals(group.Key, "canvas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(groups, group => string.Equals(group.Key, "foundations", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Component_Get_Returns_Mermaid_Metadata_Examples_And_Css()
    {
        var service = CreateService();

        var component = service.GetComponent("MermaidDiagram");
        var examples = service.GetExamples("MermaidDiagram");
        var css = service.GetCssTokens("MermaidDiagram");

        Assert.Equal("Mermaid", component.Library);
        Assert.Equal("CanDoItAll.Components.Mermaid", component.Namespace);
        Assert.EndsWith(Path.Combine("Components", "MermaidDiagram.razor"), component.SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(component.Tags, tag => string.Equals(tag, "mermaid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.GroupKeys, key => string.Equals(key, "mermaid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.Guidance.CompositionRules, rule => rule.Contains("AddCanDoItAllMermaid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.Parameters, parameter => string.Equals(parameter.Name, "Source", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.Events, @event => string.Equals(@event.Name, "NodeClicked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(examples.Examples, example => string.Equals(example.GroupKey, "mermaid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(css.Stylesheets, stylesheet => stylesheet.Contains("CanDoItAll.Components.Mermaid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Component_Get_Returns_Charts_Metadata_Examples_And_Css()
    {
        var service = CreateService();

        var component = service.GetComponent("CdaChart");
        var examples = service.GetExamples("CdaChart");
        var css = service.GetCssTokens("CdaChart");

        Assert.Equal("Charts", component.Library);
        Assert.Equal("CanDoItAll.Components.Charts", component.Namespace);
        Assert.EndsWith(Path.Combine("Components", "CdaChart.razor"), component.SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(component.Tags, tag => string.Equals(tag, "chart-wrapper", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.GroupKeys, key => string.Equals(key, "charts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.CssNotes, note => note.Contains("raw Apex component markup", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.Guidance.CompositionRules, rule => rule.Contains("AddCanDoItAllCharts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.Guidance.CompositionRules, rule => rule.Contains("browser assertions", StringComparison.OrdinalIgnoreCase));

        var seriesParameter = Assert.Single(component.Parameters, parameter => string.Equals(parameter.Name, "Series", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("CdaChartSeries", seriesParameter.Summary ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(examples.Examples, example => string.Equals(example.GroupKey, "charts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(css.Stylesheets, stylesheet => stylesheet.Contains("Blazor-ApexCharts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Component_Get_Returns_Richer_Tabs_Metadata_And_Sandbox_Examples()
    {
        var service = CreateService();

        var component = service.GetComponent("Tabs");
        var usageExamples = service.GetUsageExamples("Tabs", limit: 20);
        var css = service.GetCssTokens("Tabs");

        Assert.Equal("BaseLib", component.Library);
        Assert.EndsWith(Path.Combine("Components", "Navigation", "Tabs.razor"), component.SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(component.DependencyNames, dependency => string.Equals(dependency, "TabsItem", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.CssNotes, note => note.Contains("Tailwind-owned", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.Guidance.CompositionRules, rule => rule.Contains("SectionCard", StringComparison.OrdinalIgnoreCase));

        var toneParameter = Assert.Single(component.Parameters, parameter => string.Equals(parameter.Name, "Tone", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Primary", toneParameter.DefaultValue);
        Assert.Contains("Info", toneParameter.AllowedValues);
        Assert.Contains("accent", toneParameter.Summary ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var classParameter = Assert.Single(component.Parameters, parameter => string.Equals(parameter.Name, "Class", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Tailwind", classParameter.Summary ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(css.SourceFiles, file => file.EndsWith(Path.Combine("Tailwind", "navigation", "tabs.css"), StringComparison.OrdinalIgnoreCase));
        Assert.True(usageExamples.TotalMatches >= 2);
        Assert.Contains(usageExamples.UsageExamples, example => example.FilePath.EndsWith("NavigationTabs.razor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Component_Get_Returns_Editable_Metadata_And_Sandbox_Example()
    {
        var service = CreateService();

        var component = service.GetComponent("Editable");
        var examples = service.GetExamples("Editable");

        Assert.Equal("BaseLib", component.Library);
        Assert.EndsWith(Path.Combine("Components", "Forms", "Editable.razor"), component.SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(component.Parameters, parameter => string.Equals(parameter.Name, "ParameterName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(component.Events, @event => string.Equals(@event.Name, "ItemChanged", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(examples.Examples, example => string.Equals(example.Id, "inputs-editable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Component_Get_Returns_Overlay_Position_Guidance_For_Tooltip_And_Notification()
    {
        var service = CreateService();

        var notification = service.GetComponent("Notification");
        var tooltip = service.GetComponent("Tooltip");
        var tooltipTarget = service.GetComponent("TooltipTarget");

        Assert.Contains("per-message viewport positioning", notification.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(notification.Tags, tag => string.Equals(tag, "notification-position", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(notification.CssNotes, note => note.Contains("NotificationMessage.Position", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(notification.Guidance.CompositionRules, rule => rule.Contains("TopRight", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(notification.Guidance.CompositionRules, rule => rule.Contains("BottomCenter", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(notification.Guidance.CompositionRules, rule => rule.Contains("DialogService", StringComparison.OrdinalIgnoreCase));

        var notificationPosition = Assert.Single(notification.Parameters, parameter => string.Equals(parameter.Name, "Position", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("BottomCenter", notificationPosition.AllowedValues);
        Assert.Contains("per-message", notificationPosition.Summary ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("side, corner, and edge-aligned placement", tooltip.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(tooltip.CssNotes, note => note.Contains("TooltipOptions.Position", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tooltip.Guidance.CompositionRules, rule => rule.Contains("TopLeft", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tooltip.Guidance.CompositionRules, rule => rule.Contains("Playwright", StringComparison.OrdinalIgnoreCase));

        var tooltipTargetPosition = Assert.Single(tooltipTarget.Parameters, parameter => string.Equals(parameter.Name, "Position", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("RightBottom", tooltipTargetPosition.AllowedValues);
        Assert.Contains("viewport", tooltipTargetPosition.Summary ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void All_Discovered_Components_Resolve_To_Existing_Source_Paths()
    {
        var service = CreateService();
        var components = service.GetIndex().Components;

        Assert.All(components, component => Assert.True(File.Exists(component.SourcePath), $"Missing source path for {component.Name}: {component.SourcePath}"));
    }

    [Fact]
    public void Component_Summaries_Avoid_Generic_And_Sandbox_Derived_Fallbacks()
    {
        var service = CreateService();
        var components = service.GetIndex().Components;

        Assert.DoesNotContain(components, component => component.Summary.Contains("extracted component libraries", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(components, component => component.Summary.Contains("used in the sandbox", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Feedback_Component_Summaries_Describe_Their_Actual_Role()
    {
        var service = CreateService();

        var component = service.GetComponent("EmptyState");

        Assert.Contains("zero-data", component.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Actions group", component.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static ComponentCatalogService CreateService()
    {
        return new ComponentCatalogService(CreateOptions());
    }

    private static IOptions<McpServerOptions> CreateOptions()
    {
        var workspaceRoot = FindWorkspaceRoot();
        return Options.Create(new McpServerOptions
        {
            Server = new ServerOptions
            {
                WorkspaceRoot = workspaceRoot
            },
            Catalog = new CatalogOptions
            {
                BaseLibRoot = Path.Combine("..", "CanDoItAll.Components", "src", "CanDoItAll.Components.BaseLib"),
                CanvasLibRoot = Path.Combine("..", "CanDoItAll.Components", "src", "CanDoItAll.Components.CanvasLib"),
                ChartsRoot = Path.Combine("..", "CanDoItAll.Components", "src", "CanDoItAll.Components.Charts"),
                MermaidRoot = Path.Combine("..", "CanDoItAll.Components", "src", "CanDoItAll.Components.Mermaid"),
                SandboxRoot = Path.Combine("..", "CanDoItAll.Components", "src", "CanDoItAll.Components.Sandbox")
            }
        });
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new ToolInvocationException("WorkspaceNotFound", "Could not locate the CanDoItAll workspace root for MCP component tests.");
    }
}
