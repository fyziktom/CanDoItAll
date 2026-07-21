using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentTeamCatalogIntegrationTests
{
    private const string ManagedSeedVersionPropertyName = "managedSeedVersion";
    private const string ExpectedAgentTemplateSeedVersion = "2026-07-agent-template-teams-v63";

    private static readonly IReadOnlySet<string> LunaTemplateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "programming-workspace-analyst",
        "delivery-qa-observer",
        "code-review-lead",
        "ui-review-lead",
        "security-reviewer",
        "dotnet-solution-architect",
        "dotnet-application-developer",
        "blazor-application-developer",
        "dotnet-qa-review-lead",
        "runtime-failure-analyst",
        "javascript-solution-architect",
        "javascript-application-developer",
        "javascript-qa-review-lead"
    };

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
    public async Task Project_access_grant_and_revoke_are_durable_and_idempotent_without_rewriting_the_agent()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agentId = await CreateAgentAsync(workspaceService, "Restricted project creator");
        var projectId = Guid.NewGuid();

        await workspaceService.GrantAgentProjectStructureAccessAsync(agentId, projectId);
        var firstGrant = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => item.Id == agentId);

        await workspaceService.GrantAgentProjectStructureAccessAsync(agentId, projectId);
        var repeatedGrant = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => item.Id == agentId);
        var grantedAccess = AgentProjectStructureAccessMetadata.Read(repeatedGrant.ConfigurationJson);

        Assert.Equal("Restricted project creator", repeatedGrant.Name);
        Assert.Equal([projectId], grantedAccess.AllowedProjectIds);
        Assert.Equal(firstGrant.UpdatedAtUtc, repeatedGrant.UpdatedAtUtc);

        await workspaceService.RevokeAgentProjectStructureAccessAsync(agentId, projectId);
        var firstRevoke = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => item.Id == agentId);

        await workspaceService.RevokeAgentProjectStructureAccessAsync(agentId, projectId);
        var repeatedRevoke = Assert.Single(
            await workspaceService.ListAgentsAsync(includeTemplates: false),
            item => item.Id == agentId);
        var revokedAccess = AgentProjectStructureAccessMetadata.Read(repeatedRevoke.ConfigurationJson);

        Assert.Equal("Restricted project creator", repeatedRevoke.Name);
        Assert.DoesNotContain(projectId, revokedAccess.AllowedProjectIds);
        Assert.Equal(firstRevoke.UpdatedAtUtc, repeatedRevoke.UpdatedAtUtc);
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

        Assert.Equal(ExpectedAgentTemplateSeedVersion, pack.Manifest.Version);
        Assert.Equal(ExpectedAgentTemplateSeedVersion, pack.Manifest.SeedVersion);
        Assert.Equal(5, pack.Teams.Count);
        var members = pack.Teams.SelectMany(item => item.MemberTemplates).ToArray();
        Assert.All(
            members,
            member =>
            {
                Assert.False(string.IsNullOrWhiteSpace(member.Instructions));
                Assert.False(string.IsNullOrWhiteSpace(member.Settings.ProviderProfileKey));
                Assert.True(AgentAvatarImageCatalog.IsBundledAvatarUrl(member.Settings.AvatarImageUrl));
                Assert.Equal(
                    LunaTemplateKeys.Contains(member.Key)
                        ? OpenAiModelIds.Gpt56Luna
                        : ManagedSeedProviderFallbacks.OpenAiDefaultModel,
                    member.Settings.Model);
                Assert.NotNull(member.Settings.Access.ProjectStructure);
                Assert.NotNull(member.Settings.Access.Processes);
                Assert.NotEmpty(member.Skills.CapabilityKeys);
            });
        Assert.Equal(
            LunaTemplateKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
            members
                .Where(member => string.Equals(member.Settings.Model, OpenAiModelIds.Gpt56Luna, StringComparison.OrdinalIgnoreCase))
                .Select(member => member.Key)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.All(
            members.Where(member =>
                !string.Equals(
                    member.Key,
                    HrAgentIdentity.TemplateKey,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    member.Key,
                    PromptsCuratorAgentIdentity.TemplateKey,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    member.Key,
                    WorkflowCuratorAgentIdentity.TemplateKey,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    member.Key,
                    CapabilityCuratorAgentIdentity.TemplateKey,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    member.Key,
                    SchedulerAgentIdentity.TemplateKey,
                    StringComparison.Ordinal)),
            member =>
            {
                Assert.True(member.Settings.Access.ProjectStructure!.CanRead);
                Assert.True(member.Settings.Access.ProjectStructure.AllowAllProjects);
                Assert.True(member.Settings.Access.Processes!.CanRead);
                Assert.True(member.Settings.Access.Processes.AllowAllDefinitions);
            });

        var curatorTemplate = Assert.Single(
            members,
            member => string.Equals(
                member.Key,
                PromptsCuratorAgentIdentity.TemplateKey,
                StringComparison.Ordinal));
        Assert.False(curatorTemplate.Settings.Access.ProjectStructure!.CanRead);
        Assert.False(curatorTemplate.Settings.Access.ProjectStructure.CanWrite);
        Assert.False(curatorTemplate.Settings.Access.ProjectStructure.AllowAllProjects);
        Assert.False(curatorTemplate.Settings.Access.Processes!.CanRead);
        Assert.False(curatorTemplate.Settings.Access.Processes.CanWrite);
        Assert.False(curatorTemplate.Settings.Access.Processes.AllowAllDefinitions);
        Assert.False(curatorTemplate.Settings.Access.WorkspaceTools!.CanReadFiles);
        Assert.False(curatorTemplate.Settings.Access.WorkspaceTools.CanWriteFiles);
        Assert.False(curatorTemplate.Settings.Access.ImageGeneration!.CanGenerateImages);

        var workflowCuratorTemplate = Assert.Single(
            members,
            member => string.Equals(
                member.Key,
                WorkflowCuratorAgentIdentity.TemplateKey,
                StringComparison.Ordinal));
        Assert.False(workflowCuratorTemplate.Settings.Access.ProjectStructure!.CanRead);
        Assert.False(workflowCuratorTemplate.Settings.Access.ProjectStructure.CanWrite);
        Assert.False(workflowCuratorTemplate.Settings.Access.ProjectStructure.AllowAllProjects);
        Assert.False(workflowCuratorTemplate.Settings.Access.Processes!.CanRead);
        Assert.False(workflowCuratorTemplate.Settings.Access.Processes.CanWrite);
        Assert.False(workflowCuratorTemplate.Settings.Access.Processes.AllowAllDefinitions);
        Assert.False(workflowCuratorTemplate.Settings.Access.WorkspaceTools!.CanReadFiles);
        Assert.False(workflowCuratorTemplate.Settings.Access.WorkspaceTools.CanWriteFiles);
        Assert.False(workflowCuratorTemplate.Settings.Access.ImageGeneration!.CanGenerateImages);

        var capabilityCuratorTemplate = Assert.Single(
            members,
            member => string.Equals(
                member.Key,
                CapabilityCuratorAgentIdentity.TemplateKey,
                StringComparison.Ordinal));
        Assert.False(capabilityCuratorTemplate.Settings.Access.ProjectStructure!.CanRead);
        Assert.False(capabilityCuratorTemplate.Settings.Access.ProjectStructure.CanWrite);
        Assert.False(capabilityCuratorTemplate.Settings.Access.ProjectStructure.AllowAllProjects);
        Assert.False(capabilityCuratorTemplate.Settings.Access.Processes!.CanRead);
        Assert.False(capabilityCuratorTemplate.Settings.Access.Processes.CanWrite);
        Assert.False(capabilityCuratorTemplate.Settings.Access.Processes.AllowAllDefinitions);
        Assert.False(capabilityCuratorTemplate.Settings.Access.WorkspaceTools!.CanReadFiles);
        Assert.False(capabilityCuratorTemplate.Settings.Access.WorkspaceTools.CanWriteFiles);
        Assert.False(capabilityCuratorTemplate.Settings.Access.ImageGeneration!.CanGenerateImages);

        var schedulerTemplate = Assert.Single(
            members,
            member => string.Equals(
                member.Key,
                SchedulerAgentIdentity.TemplateKey,
                StringComparison.Ordinal));
        Assert.False(schedulerTemplate.Settings.Access.ProjectStructure!.CanRead);
        Assert.False(schedulerTemplate.Settings.Access.ProjectStructure.CanWrite);
        Assert.False(schedulerTemplate.Settings.Access.ProjectStructure.AllowAllProjects);
        Assert.False(schedulerTemplate.Settings.Access.Processes!.CanRead);
        Assert.False(schedulerTemplate.Settings.Access.Processes.CanWrite);
        Assert.False(schedulerTemplate.Settings.Access.Processes.AllowAllDefinitions);
        Assert.False(schedulerTemplate.Settings.Access.WorkspaceTools!.CanReadFiles);
        Assert.False(schedulerTemplate.Settings.Access.WorkspaceTools.CanWriteFiles);
        Assert.False(schedulerTemplate.Settings.Access.ImageGeneration!.CanGenerateImages);

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
        Assert.Equal(
            LunaTemplateKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
            agentsByTemplateKey.Values
                .Where(agent => string.Equals(agent.Model, OpenAiModelIds.Gpt56Luna, StringComparison.OrdinalIgnoreCase))
                .Select(agent => agent.TemplateKey)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase));

        var deliveryTeam = Assert.Single(teams, item => string.Equals(item.Name, "Delivery Platform Team", StringComparison.Ordinal));
        Assert.Equal("rocket_launch", deliveryTeam.Icon);
        Assert.Contains(agentsByTemplateKey["portfolio-architect"].Id, deliveryTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["programming-workspace-analyst"].Id, deliveryTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["delivery-qa-observer"].Id, deliveryTeam.AgentIds);
        Assert.Contains(PromptsCuratorAgentIdentity.AgentId, deliveryTeam.AgentIds);
        Assert.Contains(WorkflowCuratorAgentIdentity.AgentId, deliveryTeam.AgentIds);
        Assert.Contains(CapabilityCuratorAgentIdentity.AgentId, deliveryTeam.AgentIds);
        Assert.Contains(SchedulerAgentIdentity.AgentId, deliveryTeam.AgentIds);

        var curator = agentsByTemplateKey[PromptsCuratorAgentIdentity.TemplateKey];
        Assert.Equal(PromptsCuratorAgentIdentity.AgentId, curator.Id);
        Assert.Equal(AgentLifecycleStatus.Active, curator.Status);
        Assert.False(curator.IsTemplate);
        Assert.Equal(AgentWorkloadKind.Management, curator.Workload);
        Assert.True(curator.Permissions.CanUseTools);
        Assert.False(curator.Permissions.CanAskOtherAgents);
        Assert.False(curator.Permissions.CanObserveOtherAgents);
        Assert.Equal(
            PromptsCuratorAgentCapabilityKeys.PrivilegedKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
            curator.Capabilities.Select(item => item.CapabilityKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        var workflowCurator = agentsByTemplateKey[WorkflowCuratorAgentIdentity.TemplateKey];
        Assert.Equal(WorkflowCuratorAgentIdentity.AgentId, workflowCurator.Id);
        Assert.Equal(AgentLifecycleStatus.Active, workflowCurator.Status);
        Assert.False(workflowCurator.IsTemplate);
        Assert.Equal(AgentWorkloadKind.Management, workflowCurator.Workload);
        Assert.True(workflowCurator.Permissions.CanUseTools);
        Assert.False(workflowCurator.Permissions.CanAskOtherAgents);
        Assert.False(workflowCurator.Permissions.CanObserveOtherAgents);
        Assert.Equal(
            WorkflowCuratorAgentCapabilityKeys.PrivilegedKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
            workflowCurator.Capabilities.Select(item => item.CapabilityKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        var capabilityCurator = agentsByTemplateKey[CapabilityCuratorAgentIdentity.TemplateKey];
        Assert.Equal(CapabilityCuratorAgentIdentity.AgentId, capabilityCurator.Id);
        Assert.Equal(AgentLifecycleStatus.Active, capabilityCurator.Status);
        Assert.False(capabilityCurator.IsTemplate);
        Assert.Equal(AgentWorkloadKind.Management, capabilityCurator.Workload);
        Assert.True(capabilityCurator.Permissions.CanUseTools);
        Assert.False(capabilityCurator.Permissions.CanAskOtherAgents);
        Assert.False(capabilityCurator.Permissions.CanObserveOtherAgents);
        Assert.Equal(
            CapabilityCuratorAgentCapabilityKeys.PrivilegedKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
            capabilityCurator.Capabilities.Select(item => item.CapabilityKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        var schedulerAgent = agentsByTemplateKey[SchedulerAgentIdentity.TemplateKey];
        Assert.Equal(SchedulerAgentIdentity.AgentId, schedulerAgent.Id);
        Assert.Equal(AgentLifecycleStatus.Active, schedulerAgent.Status);
        Assert.False(schedulerAgent.IsTemplate);
        Assert.Equal(AgentWorkloadKind.Management, schedulerAgent.Workload);
        Assert.True(schedulerAgent.Permissions.CanUseTools);
        Assert.False(schedulerAgent.Permissions.CanAskOtherAgents);
        Assert.False(schedulerAgent.Permissions.CanObserveOtherAgents);
        Assert.Equal(
            SchedulerAgentIdentity.PrivilegedCapabilityKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
            schedulerAgent.Capabilities.Select(item => item.CapabilityKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        var visualTemplateTeam = Assert.Single(teams, item => string.Equals(item.Name, "Visual Automation Template Team", StringComparison.Ordinal));
        Assert.Contains(agentsByTemplateKey["app-screenshot-capture-agent"].Id, visualTemplateTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["screenshot-review-storage-agent"].Id, visualTemplateTeam.AgentIds);
        Assert.Contains(agentsByTemplateKey["layout-image-generation-agent"].Id, visualTemplateTeam.AgentIds);
    }

    [Fact]
    public void Default_agent_template_pack_exposes_financial_planning_and_document_conversion_access()
    {
        var pack = new AgentTemplatePackLoader().Load();
        var members = pack.Teams
            .SelectMany(team => team.MemberTemplates)
            .ToDictionary(member => member.Key, StringComparer.OrdinalIgnoreCase);

        var financial = members["financial-strategist"];
        Assert.True(financial.Settings.Permissions.AutoApproveExternalCallsByDefault);
        Assert.True(financial.Settings.Access.ProjectStructure is
        {
            CanRead: true,
            CanWrite: false,
            CanWriteTasks: false
        });
        Assert.True(financial.Settings.Access.ImageGeneration is { CanGenerateImages: true });
        Assert.True(financial.Settings.Access.ImageGeneration is { CanStoreImagesAsProjectAssets: true });
        Assert.Contains("project-plan-analysis-inline-skill", financial.Skills.CapabilityKeys);
        Assert.Contains("project-plan-summary-get", financial.Skills.CapabilityKeys);
        Assert.Contains("workspace-convert-document", financial.Skills.CapabilityKeys);
        Assert.Contains("workspace-inspect-image", financial.Skills.CapabilityKeys);
        Assert.Contains("workspace-analyze-image", financial.Skills.CapabilityKeys);
        Assert.Contains("workspace-analyze-images", financial.Skills.CapabilityKeys);
        Assert.Contains("workspace-write-spreadsheet", financial.Skills.CapabilityKeys);
        Assert.Contains("workspace-spreadsheet-function-catalog", financial.Skills.CapabilityKeys);
        Assert.DoesNotContain("provider-native-code-interpreter", financial.Skills.CapabilityKeys);

        var spreadsheetAnalyst = members["spreadsheet-analyst"];
        Assert.Contains("workspace-write-spreadsheet", spreadsheetAnalyst.Skills.CapabilityKeys);
        Assert.Contains("workspace-read-spreadsheet-range", spreadsheetAnalyst.Skills.CapabilityKeys);
        Assert.Contains("workspace-spreadsheet-function-catalog", spreadsheetAnalyst.Skills.CapabilityKeys);

        var research = members["research-deep-dive-analyst"];
        Assert.Equal("ReadOnly", research.Settings.Access.WorkspaceTools?.Profile);
        Assert.True(research.Settings.Access.WorkspaceTools is { CanTransformArtifacts: true });
        Assert.Contains("workspace-convert-document", research.Skills.CapabilityKeys);

        var deliveryManager = members["delivery-manager"];
        Assert.Contains("workspace-convert-document", deliveryManager.Skills.CapabilityKeys);

    }

    [Fact]
    public void Portfolio_architect_template_allows_projects_and_subprojects_to_be_created_independently()
    {
        var pack = new AgentTemplatePackLoader().Load();
        var portfolioArchitect = pack.Teams
            .SelectMany(team => team.MemberTemplates)
            .Single(member => string.Equals(member.Key, "portfolio-architect", StringComparison.Ordinal));

        Assert.True(portfolioArchitect.Settings.Access.ProjectStructure is
        {
            CanCreateProjects: true,
            CanCreateSubprojects: true
        });
    }

    [Fact]
    public void Managed_agent_normalization_backfills_missing_seed_avatar_and_preserves_custom_avatar()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var deliveryManager = seed.Agents.Single(item => string.Equals(item.TemplateKey, "delivery-manager", StringComparison.OrdinalIgnoreCase));
        var architect = seed.Agents.Single(item => string.Equals(item.TemplateKey, "portfolio-architect", StringComparison.OrdinalIgnoreCase));
        var currentManagedSeedVersion = ReadManagedSeedVersion(deliveryManager.ConfigurationJson);
        const string customAvatar = "data:image/png;base64,AQID";

        var oldAgents = seed.Agents
            .Select(agent =>
            {
                var oldAgent = agent with
                {
                    ConfigurationJson = agent.ConfigurationJson.Replace(currentManagedSeedVersion, "2026-06-agent-template-teams-v19", StringComparison.Ordinal)
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
    public void Managed_agent_refresh_preserves_the_user_favorite_tag()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var curator = Assert.Single(seed.Agents, PromptsCuratorAgentIdentity.Matches);
        var currentManagedSeedVersion = ReadManagedSeedVersion(curator.ConfigurationJson);
        var staleCurator = curator with
        {
            ConfigurationJson = curator.ConfigurationJson.Replace(
                currentManagedSeedVersion,
                "2026-07-agent-template-teams-v58",
                StringComparison.Ordinal),
            Tags = curator.Tags
                .Append("user-only-tag")
                .Append(AgentSpecialTags.Favorite)
                .ToList()
        };
        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == curator.Id ? staleCurator : agent)
                .ToList()
        });
        var refreshed = Assert.Single(normalized.Agents, PromptsCuratorAgentIdentity.Matches);

        Assert.Contains(refreshed.Tags, AgentSpecialTags.IsFavorite);
        Assert.DoesNotContain("user-only-tag", refreshed.Tags, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("prompts", refreshed.Tags, StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadManagedSeedVersion(string configurationJson)
    {
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement.GetProperty(ManagedSeedVersionPropertyName).GetString()
            ?? throw new InvalidOperationException("Managed seed version is required for agent template refresh tests.");
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
