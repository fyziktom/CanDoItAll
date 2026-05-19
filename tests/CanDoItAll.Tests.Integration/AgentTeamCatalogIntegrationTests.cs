using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentTeamCatalogIntegrationTests
{
    [Fact]
    public async Task Agent_team_catalog_persists_many_to_many_memberships_and_prunes_deleted_agents()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var builderId = await CreateAgentAsync(workspaceService, "Team Catalog Builder");
        var reviewerId = await CreateAgentAsync(workspaceService, "Team Catalog Reviewer");

        var deliveryTeamId = await workspaceService.SaveAgentTeamAsync(new AgentTeamEditorModel
        {
            Name = "Delivery Team",
            Description = "Owns implementation and review.",
            AgentIds = [builderId, reviewerId]
        });
        var reviewTeamId = await workspaceService.SaveAgentTeamAsync(new AgentTeamEditorModel
        {
            Name = "Review Team",
            AgentIds = [reviewerId]
        });

        var teams = await workspaceService.ListAgentTeamsAsync();
        var deliveryTeam = Assert.Single(teams, item => item.Id == deliveryTeamId);
        var reviewTeam = Assert.Single(teams, item => item.Id == reviewTeamId);

        Assert.Equal("Delivery Team", deliveryTeam.Name);
        Assert.Equal("Owns implementation and review.", deliveryTeam.Description);
        Assert.Equal(
            new[] { builderId, reviewerId }.OrderBy(item => item).ToList(),
            deliveryTeam.AgentIds.OrderBy(item => item).ToList());
        Assert.Equal([reviewerId], reviewTeam.AgentIds);

        var updatedDeliveryTeam = await workspaceService.UpdateAgentTeamMembersAsync(
            deliveryTeamId,
            [reviewerId, reviewerId, Guid.Empty],
            CancellationToken.None);

        Assert.Equal([reviewerId], updatedDeliveryTeam.AgentIds);

        await workspaceService.DeleteAgentAsync(reviewerId);

        teams = await workspaceService.ListAgentTeamsAsync();
        deliveryTeam = Assert.Single(teams, item => item.Id == deliveryTeamId);
        reviewTeam = Assert.Single(teams, item => item.Id == reviewTeamId);

        Assert.Empty(deliveryTeam.AgentIds);
        Assert.Empty(reviewTeam.AgentIds);
    }

    [Fact]
    public void Agent_team_normalization_prunes_unknown_or_duplicate_memberships()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var agent = seed.Agents.First(item => !item.IsTemplate);
        var team = new AgentTeamDefinition(
            Id: Guid.NewGuid(),
            Name: " Normalized Team ",
            Description: " Keeps real agents only. ",
            AgentIds: [Guid.Empty, agent.Id, agent.Id, Guid.NewGuid()],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        var catalog = seed.ToCatalog() with
        {
            AgentTeams = [team]
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var normalizedTeam = Assert.Single(normalized.AgentTeams);

        Assert.Equal("Normalized Team", normalizedTeam.Name);
        Assert.Equal("Keeps real agents only.", normalizedTeam.Description);
        Assert.Equal([agent.Id], normalizedTeam.AgentIds);
    }

    private static async Task<Guid> CreateAgentAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        string name)
    {
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = name;
        editor.RoleTitle = "Delivery specialist";
        editor.Summary = "Participates in team-scoped delivery tests.";
        editor.Instructions = "Support delivery work and stay within assigned scope.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        return await workspaceService.SaveAgentAsync(editor);
    }
}
