using CanDoItAll.Mcp.Components.Catalog;
using CanDoItAll.Mcp.Components.Configuration;
using CanDoItAll.Mcp.Components.Tools;
using CanDoItAll.Mcp.Core.Hosting;
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
        Assert.Equal(11, groups.Data!.Count);
        Assert.Contains(groups.Data, group => string.Equals(group.Key, "charts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(groups.Data, group => string.Equals(group.Key, "mermaid", StringComparison.OrdinalIgnoreCase));

        Assert.True(css.Ok);
        Assert.Contains(css.Data!.Stylesheets, stylesheet => stylesheet.Contains("css/workbench/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(css.Data!.SourceFiles, file => file.EndsWith(Path.Combine("wwwroot", "css", "workbench", "shell", "01-layout-and-shell.css"), StringComparison.OrdinalIgnoreCase));

        Assert.True(contracts.Ok);
        Assert.Contains(contracts.Data!.Contracts, contract => string.Equals(contract.Name, "CanvasCalendarSurface", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Tool_Execution_Refreshes_Idle_Activity()
    {
        var timeProvider = new ManualTimeProvider();
        var activityTracker = new McpIdleActivityTracker(timeProvider);
        var tools = CreateTools(activityTracker);

        timeProvider.Advance(TimeSpan.FromSeconds(10));

        var search = await tools.ComponentsSearchAsync("TextBox");
        var snapshot = activityTracker.GetSnapshot();

        Assert.True(search.Ok);
        Assert.Equal(timeProvider.GetUtcNow(), snapshot.LastActivityUtc);
        Assert.Equal(0, snapshot.ActiveOperationCount);
    }

    private static ComponentsTools CreateTools(IMcpIdleActivityTracker? activityTracker = null)
    {
        var service = new ComponentCatalogService(CreateOptions());
        return new ComponentsTools(
            service,
            activityTracker ?? new McpIdleActivityTracker(TimeProvider.System),
            NullLogger<ComponentsTools>.Instance);
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

        throw new InvalidOperationException("Could not locate the CanDoItAll workspace root for MCP component tests.");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }
}
