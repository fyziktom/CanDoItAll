using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentCapabilityListTests
{
    [Fact]
    public void List_renders_attached_and_unattached_tools_skills_and_mcp_entries()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var tool = CreateCapability(CapabilityKind.Tool, "Repository tool", "repo.exe", CapabilityProofStatus.NotRun);
        var skill = CreateCapability(CapabilityKind.Skill, "Review skill", "inline://review", CapabilityProofStatus.Verified);
        var mcp = CreateCapability(CapabilityKind.McpServer, "Browser MCP", "npx", CapabilityProofStatus.PendingReview);

        var cut = context.Render<AgentCapabilityList>(parameters => parameters
            .Add(component => component.Items, [tool, skill, mcp])
            .Add(component => component.AttachedCapabilityIds, new[] { skill.Id })
            .Add(component => component.AssignmentRequested, _ => { })
            .Add(component => component.VerificationRequested, _ => { })
            .Add(component => component.TestIdPrefix, "test-capability")
            .Add(component => component.ListTestId, "test-capability-grid"));

        Assert.NotNull(cut.Find("[data-testid='test-capability-grid']"));
        Assert.Equal(3, cut.FindAll("[data-testid='test-capability-card']").Count);
        var toolCard = cut.Find("[data-capability-kind='Tool']");
        Assert.Contains("Repository tool", toolCard.TextContent, StringComparison.Ordinal);
        Assert.Contains("Available", toolCard.TextContent, StringComparison.Ordinal);
        Assert.Contains("repo.exe", toolCard.TextContent, StringComparison.Ordinal);
        Assert.True(toolCard.QuerySelector("[data-testid='test-capability-verify']")!.HasAttribute("disabled"));
        var endpoint = toolCard.QuerySelector(".agent-capability-list__endpoint")!;
        Assert.Equal("0", endpoint.GetAttribute("tabindex"));
        Assert.Equal("Path or endpoint: repo.exe", endpoint.GetAttribute("aria-label"));
        Assert.Equal("repo.exe", endpoint.GetAttribute("title"));

        var skillCard = cut.Find("[data-capability-kind='Skill']");
        Assert.Contains("Attached", skillCard.TextContent, StringComparison.Ordinal);
        Assert.Contains("Verified", skillCard.TextContent, StringComparison.Ordinal);
        Assert.False(skillCard.QuerySelector("[data-testid='test-capability-verify']")!.HasAttribute("disabled"));

        var mcpCard = cut.Find("[data-capability-kind='McpServer']");
        Assert.Contains("MCP server", mcpCard.TextContent, StringComparison.Ordinal);
        Assert.Contains("Pending review", mcpCard.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='test-capability-details']"));
    }

    [Fact]
    public void List_routes_typed_callbacks_and_honors_verification_availability()
    {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var capability = CreateCapability(CapabilityKind.Tool, "Attached tool", "tool.exe", CapabilityProofStatus.NotRun);
        Guid? assignedId = null;
        Guid? verifiedId = null;
        Guid? detailsId = null;

        var cut = context.Render<AgentCapabilityList>(parameters => parameters
            .Add(component => component.Items, [capability])
            .Add(component => component.AttachedCapabilityIds, new[] { capability.Id })
            .Add(component => component.AssignmentRequested, id => assignedId = id)
            .Add(component => component.VerificationRequested, id => verifiedId = id)
            .Add(component => component.DetailsRequested, id => detailsId = id)
            .Add(component => component.TestIdPrefix, "callback-capability"));

        cut.Find("[data-testid='callback-capability-toggle']").Click();
        cut.Find("[data-testid='callback-capability-verify']").Click();
        cut.Find("[data-testid='callback-capability-details']").Click();

        Assert.Equal(capability.Id, assignedId);
        Assert.Equal(capability.Id, verifiedId);
        Assert.Equal(capability.Id, detailsId);

        cut.Render(parameters => parameters
            .Add(component => component.CanVerify, false));

        Assert.True(cut.Find("[data-testid='callback-capability-verify']").HasAttribute("disabled"));
    }

    private static CapabilityCatalogItem CreateCapability(
        CapabilityKind kind,
        string name,
        string endpoint,
        CapabilityProofStatus proofStatus)
    {
        return new CapabilityCatalogItem(
            Guid.NewGuid(),
            kind,
            name.ToLowerInvariant().Replace(' ', '-'),
            name,
            $"{name} description that is intentionally long enough to exercise the compact two-line presentation.",
            endpoint,
            "{}",
            proofStatus,
            string.Empty,
            null,
            IsBuiltIn: false)
        {
            Tags = ["test", kind.ToString().ToLowerInvariant()]
        };
    }
}
