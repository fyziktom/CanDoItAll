using CanDoItAll.Mcp.Components.Catalog;
using CanDoItAll.Mcp.Components.Configuration;
using CanDoItAll.Mcp.Components.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Components.Tests;

public sealed class ComponentsToolsTests
{
    [Fact]
    public async Task Tools_Return_Structured_Search_And_Component_Details()
    {
        var tools = CreateTools();

        var search = await tools.ComponentsSearchAsync("TextBox");
        var component = await tools.ComponentGetAsync("TextBox");

        Assert.True(search.Ok);
        Assert.NotNull(search.Data);
        Assert.Contains(search.Data!.Components, hit => string.Equals(hit.Name, "TextBox", StringComparison.OrdinalIgnoreCase));

        Assert.True(component.Ok);
        Assert.NotNull(component.Data);
        Assert.Equal("TextBox", component.Data!.Name);
    }

    [Fact]
    public async Task Tools_Return_Examples_Groups_Css_And_Canvas_Contracts()
    {
        var tools = CreateTools();

        var examples = await tools.ComponentExamplesAsync("CanvasWorkbench");
        var groups = await tools.ComponentGroupsListAsync();
        var css = await tools.ComponentCssTokensGetAsync("CanvasWorkbench");
        var contracts = await tools.CanvasContractGetAsync("CanvasCalendarSurface");

        Assert.True(examples.Ok);
        Assert.Contains(examples.Data!.Examples, example => string.Equals(example.GroupKey, "canvas", StringComparison.OrdinalIgnoreCase));

        Assert.True(groups.Ok);
        Assert.Equal(9, groups.Data!.Count);

        Assert.True(css.Ok);
        Assert.Contains(css.Data!.Stylesheets, stylesheet => stylesheet.Contains("canvas-workbench.css", StringComparison.OrdinalIgnoreCase));

        Assert.True(contracts.Ok);
        Assert.Contains(contracts.Data!.Contracts, contract => string.Equals(contract.Name, "CanvasCalendarSurface", StringComparison.OrdinalIgnoreCase));
    }

    private static ComponentsTools CreateTools()
    {
        var service = new ComponentCatalogService(CreateOptions());
        return new ComponentsTools(service, NullLogger<ComponentsTools>.Instance);
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

        throw new InvalidOperationException("Could not locate the CanDoItAll workspace root for MCP component tests.");
    }
}
