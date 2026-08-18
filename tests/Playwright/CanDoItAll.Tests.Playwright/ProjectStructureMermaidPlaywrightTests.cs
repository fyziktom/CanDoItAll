using System.Net.Http.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Playwright.Flows;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Visual;

[Collection(PlaywrightCollection.Name)]
public sealed class ProjectStructureMermaidPlaywrightTests(PlaywrightAppFixture fixture)
{
    private const string ArchitectureSource = """
        architecture-beta
            group browser(cloud)[Browser]
            group app(cloud)[Blazor host]
            group package(server)[Mermaid package] in app
            group docs(database)[Syntax guidance]

            service user(internet)[User] in browser
            service sandbox(server)[Components sandbox] in app
            service wrapper(server)[MermaidDiagram] in package
            service mermaidjs(server)["Official mermaid.js"] in package
            service rules(database)[MCP rules] in docs
            junction renderPath in package

            user:R --> L:sandbox
            sandbox:R --> L:wrapper
            wrapper:R --> L:renderPath
            renderPath:R --> L:mermaidjs
            wrapper:B --> T:rules
        """;

    [Fact]
    public async Task Project_structure_mermaid_node_opens_rendered_diagram_modal()
    {
        var project = await CreateProjectAsync("Mermaid browser proof");
        string mermaidNodeId;
        await using (var provider = await BuildSeedProviderAsync())
        {
            await using var scope = provider.CreateAsyncScope();
            var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            var metadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                File = new ProjectFileMetadata
                {
                    FileSubtype = ProjectFileSubtype.Mermaid,
                    MermaidDiagramKind = MermaidDiagramKind.ArchitectureBeta
                }
            });

            var createdNode = await workbenchService.CreateObjectAsync(
                project.ProjectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.File,
                    "Architecture beta proof",
                    "Mermaid file",
                    ArchitectureSource,
                    $"project:{project.ProjectId:D}",
                    520,
                    260,
                    null,
                    null,
                    "mermaid",
                    null,
                    metadata));
            mermaidNodeId = createdNode.Id;
        }

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

        var page = await context.NewPageAsync();
        var browserDiagnostics = new List<string>();
        page.Console += (_, message) => browserDiagnostics.Add($"{message.Type}: {message.Text}");
        page.PageError += (_, message) => browserDiagnostics.Add($"pageerror: {message}");
        await page.GotoAsync($"{fixture.BaseUrl}{project.Route}");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("project-structure-selection-window").WaitForAsync();
        await HideAgentWindowIfPresentAsync(page);

        await WaitForCanvasNodeAsync(page, mermaidNodeId);
        await OpenCanvasNodeByDoubleClickAsync(page, mermaidNodeId);

        var diagram = page.GetByTestId("project-structure-mermaid-diagram");
        await diagram.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await WaitForMermaidRenderAsync(page, diagram, browserDiagnostics);

        var clickableNodes = await diagram.Locator("[data-cda-mermaid-node='true']").CountAsync();
        Assert.True(clickableNodes > 0);

        var evidenceRoot = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        var mermaidDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Architecture beta proof Mermaid viewer" });
        Assert.True(
            await mermaidDialog.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).CountAsync() > 0,
            "Expected the Mermaid modal opened by double-click to expose an Edit action.");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceRoot, "project-structure-mermaid-modal-doubleclick.png"),
            FullPage = true
        });

        await mermaidDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await diagram.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10_000 });

        await page.GetByTestId("project-structure-node-actions")
            .GetByRole(AriaRole.Button, new() { Name = "View Mermaid", Exact = true })
            .ClickAsync();
        await diagram.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await WaitForMermaidRenderAsync(page, diagram, browserDiagnostics);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceRoot, "project-structure-mermaid-modal-button.png"),
            FullPage = true
        });
    }

    private static async Task WaitForMermaidRenderAsync(IPage page, ILocator diagram, IReadOnlyList<string> browserDiagnostics)
    {
        try
        {
            await page.WaitForFunctionAsync(
                @"() => {
                    const diagram = document.querySelector('[data-testid=""project-structure-mermaid-diagram""]');
                    return !!diagram?.querySelector('svg') || !!diagram?.querySelector('[data-testid=""mermaid-error""]');
                }",
                null,
                new() { Timeout = 20_000 });
        }
        catch (TimeoutException ex)
        {
            var evidenceRoot = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "evidence");
            Directory.CreateDirectory(evidenceRoot);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(evidenceRoot, "project-structure-mermaid-modal-timeout.png"),
                FullPage = true
            });

            var diagramDiagnostics = await page.EvaluateAsync<string>(
                @"() => {
                    const diagram = document.querySelector('[data-testid=""project-structure-mermaid-diagram""]');
                    const viewport = diagram?.querySelector('[data-testid=""mermaid-diagram-viewport""]');
                    return JSON.stringify({
                        text: (diagram?.textContent || '').trim().slice(0, 1200),
                        html: (viewport?.innerHTML || '').slice(0, 1200),
                        hasViewport: !!viewport,
                        hasSvg: !!diagram?.querySelector('svg'),
                        hasError: !!diagram?.querySelector('[data-testid=""mermaid-error""]')
                    });
                }");
            throw new TimeoutException(
                $"Mermaid did not report an SVG or an error. Diagram: {diagramDiagnostics}. Browser: {string.Join(" | ", browserDiagnostics.TakeLast(20))}",
                ex);
        }

        if (await diagram.GetByTestId("mermaid-error").CountAsync() > 0)
        {
            var evidenceRoot = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "evidence");
            Directory.CreateDirectory(evidenceRoot);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(evidenceRoot, "project-structure-mermaid-modal-error.png"),
                FullPage = true
            });

            var errorText = await diagram.GetByTestId("mermaid-error").TextContentAsync();
            throw new InvalidOperationException($"Mermaid failed to render the architecture-beta proof: {errorText}");
        }

        await Assertions.Expect(diagram.Locator("svg[data-cda-mermaid-svg]")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
        {
            Timeout = 5_000
        });
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page)
    {
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Database profiles", Exact = true });
        try
        {
            await dialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = 2_500
            });
        }
        catch (TimeoutException)
        {
            return;
        }
        catch (PlaywrightException)
        {
            return;
        }

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });
    }

    private static async Task HideAgentWindowIfPresentAsync(IPage page)
    {
        var agentsWindow = page.GetByTestId("project-structure-agents-window");
        try
        {
            await agentsWindow.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = 2_500
            });
        }
        catch (TimeoutException)
        {
            return;
        }
        catch (PlaywrightException)
        {
            return;
        }

        await agentsWindow.GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
        await agentsWindow.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 10_000
        });
    }

    private static async Task WaitForCanvasNodeAsync(IPage page, string nodeId)
    {
        await page.WaitForFunctionAsync(
            @"expected => {
                const byId = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.lookups?.byId;
                return byId instanceof Map && byId.has(expected.nodeId);
            }",
            new { nodeId },
            new() { Timeout = 20_000 });
    }

    private static async Task OpenCanvasNodeByDoubleClickAsync(IPage page, string nodeId)
    {
        var center = await ReadCanvasHotZoneCenterAsync(page, nodeId);
        await page.Mouse.DblClickAsync((float)center.X, (float)center.Y);
    }

    private static async Task<CanvasHotZoneCenter> ReadCanvasHotZoneCenterAsync(IPage page, string nodeId)
    {
        await page.WaitForFunctionAsync(
            @"() => !!document.querySelector('.cw-canvas-host') && !!window.CanDoItAll?.canvasWorkbench?.getHotZoneCenter",
            null,
            new() { Timeout = 20_000 });

        var center = await page.EvaluateAsync<CanvasHotZoneCenter?>(
            @"nodeId => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const host = document.querySelector('.cw-canvas-host');
                const hostRect = host?.getBoundingClientRect?.();
                if (!host || !hostRect || !workbench?.getHotZoneCenter) {
                    return null;
                }

                const hotZone = workbench.getHotZoneCenter(host, { zone: 'node-body', nodeId });
                if (hotZone) {
                    return {
                        x: hostRect.left + hotZone.x,
                        y: hostRect.top + hotZone.y
                    };
                }

                const snapshot = workbench.getSceneSnapshot?.(host);
                const node = snapshot?.nodes?.find(candidate => candidate?.id === nodeId);
                if (!node) {
                    return null;
                }

                return {
                    x: hostRect.left + node.left + (node.width / 2),
                    y: hostRect.top + node.top + (node.height / 2)
                };
            }",
            nodeId);

        return center ?? throw new InvalidOperationException($"Could not resolve canvas geometry for Mermaid node '{nodeId}'.");
    }

    private async Task<DevProjectRoute> CreateProjectAsync(string name)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(fixture.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        using var response = await client.PostAsync(
            $"/_dev/projects?name={Uri.EscapeDataString(name)}&phase=Execution",
            content: null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DevProjectRoute>()
            ?? throw new InvalidOperationException("The development project endpoint returned no payload.");
    }

    private async Task<ServiceProvider> BuildSeedProviderAsync()
    {
        var connectionString = fixture.DatabaseConnectionString
            ?? throw new InvalidOperationException("The Playwright fixture did not expose a database connection string.");
        var workspaceRoot = fixture.StorageWorkspaceRoot
            ?? throw new InvalidOperationException("The Playwright fixture did not expose a storage workspace root.");
        var profileRoot = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright-mermaid-seed");
        Directory.CreateDirectory(profileRoot);

        var profile = new TestDatabaseProfile(
            "playwright-mermaid",
            PlaywrightTestHostPaths.RepositoryRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            connectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));

        return await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.MermaidPlaywrightSeed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            },
            services => services.AddScoped<NavigationManager, SeedNavigationManager>());
    }

    private sealed class SeedNavigationManager : NavigationManager
    {
        public SeedNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }

    private sealed class CanvasHotZoneCenter
    {
        public double X { get; set; }

        public double Y { get; set; }
    }
}
