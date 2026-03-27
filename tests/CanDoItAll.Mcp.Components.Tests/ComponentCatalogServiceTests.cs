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

        Assert.Contains(result.Stylesheets, stylesheet => stylesheet.Contains("canvas-workbench.css", StringComparison.OrdinalIgnoreCase));
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

        Assert.Equal(9, groups.Count);
        Assert.Contains(groups, group => string.Equals(group.Key, "canvas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(groups, group => string.Equals(group.Key, "foundations", StringComparison.OrdinalIgnoreCase));
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
                BaseLibRoot = Path.Combine("src", "CanDoItAll.Components.BaseLib"),
                CanvasLibRoot = Path.Combine("src", "CanDoItAll.Components.CanvasLib"),
                SandboxRoot = Path.Combine("src", "CanDoItAll.Components.Sandbox")
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
