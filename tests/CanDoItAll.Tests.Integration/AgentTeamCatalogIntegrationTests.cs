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
            Icon = "rocket_launch",
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
        Assert.Equal("rocket_launch", deliveryTeam.Icon);
        Assert.Equal(
            new[] { builderId, reviewerId }.OrderBy(item => item).ToList(),
            deliveryTeam.AgentIds.OrderBy(item => item).ToList());
        Assert.Equal([reviewerId], reviewTeam.AgentIds);
        Assert.Equal(AgentTeamIconCatalog.DefaultIcon, reviewTeam.Icon);

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
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Icon: "not_real_icon");
        var catalog = seed.ToCatalog() with
        {
            AgentTeams = [team]
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var normalizedTeam = Assert.Single(normalized.AgentTeams, item => item.Id == team.Id);

        Assert.Equal("Normalized Team", normalizedTeam.Name);
        Assert.Equal("Keeps real agents only.", normalizedTeam.Description);
        Assert.Equal(AgentTeamIconCatalog.DefaultIcon, normalizedTeam.Icon);
        Assert.Equal([agent.Id], normalizedTeam.AgentIds);
    }

    [Fact]
    public void Default_agent_template_pack_seeds_team_memberships()
    {
        var pack = new AgentTemplatePackLoader().Load();
        Assert.Equal(5, pack.Teams.Count);
        Assert.All(
            pack.Teams.SelectMany(item => item.MemberTemplates),
            member =>
            {
                Assert.False(string.IsNullOrWhiteSpace(member.Instructions));
                Assert.False(string.IsNullOrWhiteSpace(member.Settings.ProviderProfileKey));
                Assert.True(AgentAvatarImageCatalog.IsBundledAvatarUrl(member.Settings.AvatarImageUrl));
                Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, member.Settings.Model);
                Assert.NotNull(member.Settings.Access.ProjectStructure);
                Assert.True(member.Settings.Access.ProjectStructure.CanRead);
                Assert.True(member.Settings.Access.ProjectStructure.AllowAllProjects);
                Assert.NotNull(member.Settings.Access.Processes);
                Assert.True(member.Settings.Access.Processes.CanRead);
                Assert.True(member.Settings.Access.Processes.AllowAllDefinitions);
                Assert.NotEmpty(member.Skills.CapabilityKeys);
            });

        var seed = SandboxWorkspaceSeedFactory.Create();
        var teams = seed.AgentTeams;

        Assert.Contains(teams, item => string.Equals(item.Name, "Delivery Platform Team", StringComparison.Ordinal));
        Assert.Contains(teams, item => string.Equals(item.Name, ".NET Delivery Team", StringComparison.Ordinal));
        Assert.Contains(teams, item => string.Equals(item.Name, "JavaScript Delivery Team", StringComparison.Ordinal));
        Assert.Contains(teams, item => string.Equals(item.Name, "Business And Research Team", StringComparison.Ordinal));
        Assert.Contains(teams, item => string.Equals(item.Name, "Visual Automation Template Team", StringComparison.Ordinal));

        var agentsByTemplateKey = seed.Agents.ToDictionary(item => item.TemplateKey, StringComparer.OrdinalIgnoreCase);
        Assert.All(
            agentsByTemplateKey.Values,
            agent => Assert.True(AgentAvatarImageCatalog.IsBundledAvatarUrl(agent.AvatarImageUrl)));

        var deliveryTeam = Assert.Single(teams, item => string.Equals(item.Name, "Delivery Platform Team", StringComparison.Ordinal));
        Assert.Equal("rocket_launch", deliveryTeam.Icon);
        Assert.Contains(agentsByTemplateKey["portfolio-architect"].Id, deliveryTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["programming-workspace-analyst"].Id, deliveryTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["delivery-qa-observer"].Id, deliveryTeam.AgentIds);

        var visualTemplateTeam = Assert.Single(teams, item => string.Equals(item.Name, "Visual Automation Template Team", StringComparison.Ordinal));
        Assert.Contains(agentsByTemplateKey["app-screenshot-capture-agent"].Id, visualTemplateTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["screenshot-review-storage-agent"].Id, visualTemplateTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["layout-image-generation-agent"].Id, visualTemplateTeam.AgentIds);
    }

    [Fact]
    public void Managed_agent_normalization_backfills_missing_seed_avatar_and_preserves_custom_avatar()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var deliveryManager = seed.Agents.Single(item => string.Equals(item.TemplateKey, "delivery-manager", StringComparison.OrdinalIgnoreCase));
        var architect = seed.Agents.Single(item => string.Equals(item.TemplateKey, "portfolio-architect", StringComparison.OrdinalIgnoreCase));
        const string customAvatar = "data:image/png;base64,AQID";

        var oldAgents = seed.Agents
            .Select(agent =>
            {
                var oldAgent = agent with
                {
                    ConfigurationJson = agent.ConfigurationJson.Replace("2026-06-agent-template-teams-v23", "2026-06-agent-template-teams-v19", StringComparison.Ordinal)
                };

                if (oldAgent.Id == deliveryManager.Id)
                {
                    return oldAgent with { AvatarImageUrl = null };
                }

                if (oldAgent.Id == architect.Id)
                {
                    return oldAgent with { AvatarImageUrl = customAvatar };
                }

                return oldAgent;
            })
            .ToList();

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = oldAgents
        });

        var normalizedDeliveryManager = normalized.Agents.Single(item => item.Id == deliveryManager.Id);
        var normalizedArchitect = normalized.Agents.Single(item => item.Id == architect.Id);

        Assert.Equal(deliveryManager.AvatarImageUrl, normalizedDeliveryManager.AvatarImageUrl);
        Assert.Equal(customAvatar, normalizedArchitect.AvatarImageUrl);

        var clearedAfterUpgrade = SandboxWorkspaceSeedFactory.NormalizeCatalog(normalized with
        {
            Agents = normalized.Agents
                .Select(agent => agent.Id == deliveryManager.Id
                    ? agent with { AvatarImageUrl = null }
                    : agent)
                .ToList()
        });

        Assert.Null(clearedAfterUpgrade.Agents.Single(item => item.Id == deliveryManager.Id).AvatarImageUrl);
    }

    [Fact]
    public async Task Agent_catalog_persists_custom_avatar_image_url()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        const string customAvatar = "data:image/png;base64,AQID";

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Custom Avatar Agent";
        editor.RoleTitle = "Avatar persistence specialist";
        editor.Summary = "Keeps a custom avatar image.";
        editor.Instructions = "Use the configured custom avatar.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.AvatarImageUrl = customAvatar;

        var agentId = await workspaceService.SaveAgentAsync(editor);

        var storedAgent = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            agent => agent.Id == agentId);
        var storedEditor = await workspaceService.GetAgentEditorAsync(agentId);

        Assert.Equal(customAvatar, storedAgent.AvatarImageUrl);
        Assert.Equal(customAvatar, storedEditor.AvatarImageUrl);
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
