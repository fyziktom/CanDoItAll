using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentFrameworkWorkspaceSeedIntegrationTests
{
    [Fact]
    public async Task Organization_workspace_seeds_playwright_mcp_for_ui_delivery_agents()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var capabilities = await workspaceService.ListCapabilitiesAsync();
        var playwrightCapability = Assert.Single(
            capabilities,
            item => item.Kind == CapabilityKind.McpServer &&
                    string.Equals(item.Key, "playwright-local-mcp", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("npx", playwrightCapability.EndpointOrPath);

        using var configuration = JsonDocument.Parse(playwrightCapability.ConfigurationJson);
        var root = configuration.RootElement;
        Assert.Equal("stdio", root.GetProperty("transport").GetString());
        Assert.Equal("npx", root.GetProperty("command").GetString());
        Assert.Equal(".", root.GetProperty("workingDirectory").GetString());
        Assert.Equal("NeverRequire", root.GetProperty("approvalMode").GetString());
        Assert.Contains(
            root.GetProperty("arguments").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "@playwright/mcp@latest", StringComparison.Ordinal));
        var allowedTools = root.GetProperty("allowedTools")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
        Assert.Contains("browser_navigate", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_take_screenshot", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_snapshot", allowedTools, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser_console_messages", allowedTools, StringComparer.OrdinalIgnoreCase);

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var qaAgent = Assert.Single(agents, item => string.Equals(item.Name, "Delivery QA Observer", StringComparison.Ordinal));
        var programmingAgent = Assert.Single(agents, item => string.Equals(item.Name, "Programming Workspace Analyst", StringComparison.Ordinal));

        Assert.Contains(qaAgent.Capabilities, item => item.CapabilityId == playwrightCapability.Id);
        Assert.Contains(programmingAgent.Capabilities, item => item.CapabilityId == playwrightCapability.Id);
    }
}
