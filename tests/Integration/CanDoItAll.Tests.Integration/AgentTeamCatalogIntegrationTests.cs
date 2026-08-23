using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentTeamCatalogIntegrationTests
{
    private const string ManagedSeedVersionPropertyName = "managedSeedVersion";
    private const string ExpectedAgentTemplateSeedVersion = "2026-08-agent-template-teams-v72";
    private const string PreviousAgentTemplateSeedVersion = "2026-07-agent-template-teams-v70";

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
                Assert.Equal(AgentReasoningEffortLevel.Medium, member.Settings.ReasoningEffort);
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

        var hrTemplate = Assert.Single(
            members,
            member => string.Equals(
                member.Key,
                HrAgentIdentity.TemplateKey,
                StringComparison.Ordinal));
        var hrTemplateCurationKeys = hrTemplate.Skills.CapabilityKeys
            .Where(IsCapabilityCurationKey)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            HrAgentIdentity.CapabilityCurationCapabilityKeys.OrderBy(item => item, StringComparer.Ordinal),
            hrTemplateCurationKeys);
        Assert.DoesNotContain(
            CapabilityCuratorAgentIdentity.AssignmentEditorGetCapabilityKey,
            hrTemplate.Skills.CapabilityKeys,
            StringComparer.Ordinal);
        Assert.DoesNotContain(
            CapabilityCuratorAgentIdentity.AssignmentUpdateCapabilityKey,
            hrTemplate.Skills.CapabilityKeys,
            StringComparer.Ordinal);
        Assert.DoesNotContain(
            CapabilityCuratorAgentIdentity.CuratorSkillCapabilityKey,
            hrTemplate.Skills.CapabilityKeys,
            StringComparer.Ordinal);

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
        Assert.True(schedulerTemplate.Settings.Permissions.CanScheduleWork);

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
            agent =>
            {
                Assert.True(AgentAvatarImageCatalog.IsBundledAvatarUrl(agent.AvatarImageUrl));
                AssertCanonicalReasoningEffort(
                    agent.ConfigurationJson,
                    AgentReasoningEffortLevel.Medium);
            });
        Assert.Equal(
            LunaTemplateKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
            agentsByTemplateKey.Values
                .Where(agent => string.Equals(agent.Model, OpenAiModelIds.Gpt56Luna, StringComparison.OrdinalIgnoreCase))
                .Select(agent => agent.TemplateKey)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase));

        var hrAgent = agentsByTemplateKey[HrAgentIdentity.TemplateKey];
        Assert.Equal(HrAgentIdentity.AgentId, hrAgent.Id);
        Assert.True(HrAgentIdentity.Matches(hrAgent));
        Assert.Equal(
            HrAgentIdentity.CapabilityCurationCapabilityKeys.OrderBy(item => item, StringComparer.Ordinal),
            hrAgent.Capabilities
                .Select(item => item.CapabilityKey)
                .Where(IsCapabilityCurationKey)
                .OrderBy(item => item, StringComparer.Ordinal));
        var hrCapabilityCurationSkill = Assert.Single(
            seed.Capabilities,
            capability => string.Equals(
                capability.Key,
                HrAgentIdentity.CapabilityCurationSkillCapabilityKey,
                StringComparison.Ordinal));
        Assert.Equal(CapabilityKind.Skill, hrCapabilityCurationSkill.Kind);
        Assert.True(hrCapabilityCurationSkill.IsBuiltIn);
        var hrCapabilityCurationInstructions = ReadInlineSkillInstructions(
            hrCapabilityCurationSkill.ConfigurationJson);
        Assert.Contains(
            "Complete at most one approval-gated stage per user turn.",
            hrCapabilityCurationInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "pass that exact key as the entire `text` value",
            hrCapabilityCurationInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "`capability_curator_verify` is not a standalone definition check",
            hrCapabilityCurationInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "ask the user to continue with assignment verification",
            hrCapabilityCurationInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "draft the smallest typed save candidate from the `capability_curator_save` schema",
            hrCapabilityCurationInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            hrAgent.Capabilities,
            assignment =>
                assignment.CapabilityId == hrCapabilityCurationSkill.Id &&
                string.Equals(
                    assignment.CapabilityKey,
                    HrAgentIdentity.CapabilityCurationSkillCapabilityKey,
                    StringComparison.Ordinal));

        var deliveryManager = agentsByTemplateKey["delivery-manager"];
        Assert.Equal(DeliveryManagerAgentIdentity.AgentId, deliveryManager.Id);
        Assert.True(DeliveryManagerAgentIdentity.Matches(deliveryManager));
        Assert.Equal(AgentLifecycleStatus.Active, deliveryManager.Status);
        Assert.False(deliveryManager.IsTemplate);
        Assert.NotNull(deliveryManager.ProviderProfileId);
        Assert.True(deliveryManager.Permissions.CanObserveOtherAgents);
        Assert.Contains("process-manager", deliveryManager.Tags, StringComparer.Ordinal);

        Assert.Contains(
            "Pre-implementation setup exception",
            agentsByTemplateKey["delivery-qa-observer"].Instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Generated starter/demo UI or content and a passing placeholder template test are expected",
            agentsByTemplateKey["dotnet-qa-review-lead"].Instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "only entries with `kind=ProductAcceptance` and `required=true` are mandatory acceptance criteria",
            agentsByTemplateKey["delivery-qa-observer"].Instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "never select no-go, block, escalation, or human reconfirmation solely because one lacks product proof",
            agentsByTemplateKey["dotnet-qa-review-lead"].Instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not reopen that fact or create a human-decision acceptance gate",
            agentsByTemplateKey["business-strategist"].Instructions,
            StringComparison.Ordinal);

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
            WorkflowCuratorAgentCapabilityKeys.PrivilegedKeys
                .Concat(WorkflowRuntimeCapabilityKeys.Keys)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
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
        Assert.Contains(
            "assign the capability to the exact target agent before calling `capability_curator_verify`",
            capabilityCurator.Instructions,
            StringComparison.Ordinal);
        var capabilityCuratorSkill = Assert.Single(
            seed.Capabilities,
            capability => string.Equals(
                capability.Key,
                CapabilityCuratorAgentIdentity.CuratorSkillCapabilityKey,
                StringComparison.Ordinal));
        Assert.Contains(
            "Assign a saved capability to the exact target agent before verification",
            ReadInlineSkillInstructions(capabilityCuratorSkill.ConfigurationJson),
            StringComparison.Ordinal);

        var schedulerAgent = agentsByTemplateKey[SchedulerAgentIdentity.TemplateKey];
        Assert.Equal(SchedulerAgentIdentity.AgentId, schedulerAgent.Id);
        Assert.Equal(AgentLifecycleStatus.Active, schedulerAgent.Status);
        Assert.False(schedulerAgent.IsTemplate);
        Assert.Equal(AgentWorkloadKind.Management, schedulerAgent.Workload);
        Assert.True(schedulerAgent.Permissions.CanUseTools);
        Assert.True(schedulerAgent.Permissions.CanScheduleWork);
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
    public void Agent_template_without_reasoning_effort_inherits_provider_default()
    {
        using var templatePack = new TemporaryAgentTemplatePack();
        templatePack.SetTargetSetting("reasoningEffort", valueJson: null);

        var seed = SandboxWorkspaceSeedBuilder.Build(agentTemplatePackRoot: templatePack.RootPath);
        var architect = Assert.Single(
            seed.Agents,
            agent => string.Equals(agent.TemplateKey, "portfolio-architect", StringComparison.Ordinal));

        AssertCanonicalReasoningEffort(architect.ConfigurationJson, expected: null);
    }

    [Theory]
    [InlineData("\"turbo\"")]
    [InlineData("2")]
    public void Agent_template_pack_rejects_invalid_reasoning_effort_with_settings_path(string invalidValueJson)
    {
        using var templatePack = new TemporaryAgentTemplatePack();
        templatePack.SetTargetSetting("reasoningEffort", invalidValueJson);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AgentTemplatePackLoader(templatePack.RootPath).Load());

        Assert.Contains(templatePack.TargetSettingsPath, exception.Message, StringComparison.Ordinal);
        Assert.Contains("could not be loaded", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agent_template_pack_rejects_legacy_reasoning_effort_switch_with_settings_path()
    {
        using var templatePack = new TemporaryAgentTemplatePack();
        templatePack.SetTargetSetting("reasoningEffort", valueJson: null);
        templatePack.SetTargetSetting("applyDefaultReasoningEffort", "true");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AgentTemplatePackLoader(templatePack.RootPath).Load());

        Assert.Contains(templatePack.TargetSettingsPath, exception.Message, StringComparison.Ordinal);
        Assert.Contains("applyDefaultReasoningEffort", exception.Message, StringComparison.Ordinal);
        Assert.Contains("no longer supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Agent_template_materialization_rejects_reasoning_effort_for_unsupported_model()
    {
        using var templatePack = new TemporaryAgentTemplatePack();
        templatePack.SetTargetSetting("model", "\"gpt-4.1\"");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SandboxWorkspaceSeedBuilder.Build(agentTemplatePackRoot: templatePack.RootPath));

        Assert.Contains("portfolio-architect", exception.Message, StringComparison.Ordinal);
        Assert.Contains("gpt-4.1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be applied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCapabilityCurationKey(string capabilityKey)
    {
        return string.Equals(
                   capabilityKey,
                   HrAgentIdentity.CapabilityCurationSkillCapabilityKey,
                   StringComparison.Ordinal) ||
               string.Equals(
                   capabilityKey,
                   CapabilityCuratorAgentIdentity.CuratorSkillCapabilityKey,
                   StringComparison.Ordinal) ||
               CapabilityCuratorAgentIdentity.ToolCapabilityKeys.Contains(capabilityKey);
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
        Assert.Contains("process-manager", deliveryManager.Settings.Tags, StringComparer.Ordinal);

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

    [Fact]
    public void Current_managed_agent_reasoning_effort_drift_refreshes_canonical_policy()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var driftedHrAgent = seededHrAgent with
        {
            ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(
                seededHrAgent.ConfigurationJson,
                AgentReasoningEffortLevel.High)
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == seededHrAgent.Id ? driftedHrAgent : agent)
                .ToList()
        });
        var refreshedHrAgent = Assert.Single(normalized.Agents, HrAgentIdentity.Matches);

        Assert.Equal(seededHrAgent.ConfigurationJson, refreshedHrAgent.ConfigurationJson);
        AssertCanonicalReasoningEffort(
            refreshedHrAgent.ConfigurationJson,
            AgentReasoningEffortLevel.Medium);
    }

    [Fact]
    public void Managed_hr_agent_v70_refreshes_current_workspace_access_guidance_and_settings()
    {
        const string legacyInstructions = "Legacy HR instructions without typed workspace access guidance.";

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var staleHrAgent = seededHrAgent with
        {
            Instructions = legacyInstructions,
            Permissions = seededHrAgent.Permissions with { CanAskOtherAgents = false },
            ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(
                seededHrAgent.ConfigurationJson.Replace(
                    ExpectedAgentTemplateSeedVersion,
                    PreviousAgentTemplateSeedVersion,
                    StringComparison.Ordinal),
                AgentReasoningEffortLevel.High)
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == seededHrAgent.Id ? staleHrAgent : agent)
                .ToList()
        });
        var refreshedHrAgent = Assert.Single(normalized.Agents, HrAgentIdentity.Matches);

        Assert.Equal(seededHrAgent.Instructions, refreshedHrAgent.Instructions);
        Assert.Contains("typed `WorkspaceToolAccess` field", refreshedHrAgent.Instructions, StringComparison.Ordinal);
        Assert.Equal(seededHrAgent.Permissions, refreshedHrAgent.Permissions);
        Assert.Equal(seededHrAgent.ConfigurationJson, refreshedHrAgent.ConfigurationJson);
        Assert.Equal(ExpectedAgentTemplateSeedVersion, ReadManagedSeedVersion(refreshedHrAgent.ConfigurationJson));
        Assert.Equal(
            AgentReasoningEffortLevel.Medium,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(refreshedHrAgent.ConfigurationJson, "agent"));
    }

    [Fact]
    public void Customized_managed_hr_agent_v70_preserves_customer_owned_guidance_and_settings()
    {
        const string customizedInstructions = "Customer-owned HR governance instructions.";

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededHrAgent = Assert.Single(seed.Agents, HrAgentIdentity.Matches);
        var staleConfiguration = AgentThinkingEffortPolicy.WriteAgentOverride(
            seededHrAgent.ConfigurationJson.Replace(
                ExpectedAgentTemplateSeedVersion,
                PreviousAgentTemplateSeedVersion,
                StringComparison.Ordinal),
            AgentReasoningEffortLevel.High);
        var customizedHrAgent = seededHrAgent with
        {
            Instructions = customizedInstructions,
            Permissions = seededHrAgent.Permissions with { CanAskOtherAgents = false },
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(staleConfiguration)
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == seededHrAgent.Id ? customizedHrAgent : agent)
                .ToList()
        });
        var preservedHrAgent = Assert.Single(normalized.Agents, HrAgentIdentity.Matches);

        Assert.Equal(customizedInstructions, preservedHrAgent.Instructions);
        Assert.Equal(customizedHrAgent.Permissions, preservedHrAgent.Permissions);
        Assert.Equal(customizedHrAgent.ConfigurationJson, preservedHrAgent.ConfigurationJson);
        Assert.Equal(PreviousAgentTemplateSeedVersion, ReadManagedSeedVersion(preservedHrAgent.ConfigurationJson));
        Assert.True(AgentManagedSeedCustomizationMetadata.HasCurrentCustomization(preservedHrAgent.ConfigurationJson));
        Assert.Equal(
            AgentReasoningEffortLevel.High,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(preservedHrAgent.ConfigurationJson, "agent"));
    }

    [Fact]
    public void Managed_delivery_manager_refresh_restores_process_run_narrator_eligibility()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var deliveryManager = seed.Agents.Single(item =>
            string.Equals(item.TemplateKey, "delivery-manager", StringComparison.OrdinalIgnoreCase));
        var currentManagedSeedVersion = ReadManagedSeedVersion(deliveryManager.ConfigurationJson);
        var staleDeliveryManager = deliveryManager with
        {
            ProviderProfileId = null,
            Permissions = deliveryManager.Permissions with { CanObserveOtherAgents = false },
            ConfigurationJson = deliveryManager.ConfigurationJson.Replace(
                currentManagedSeedVersion,
                "2026-07-agent-template-teams-v63",
                StringComparison.Ordinal),
            Tags = deliveryManager.Tags
                .Where(tag => !string.Equals(tag, "process-manager", StringComparison.Ordinal))
                .ToList()
        };

        var normalized = SandboxWorkspaceSeedFactory.NormalizeCatalog(seed.ToCatalog() with
        {
            Agents = seed.Agents
                .Select(agent => agent.Id == deliveryManager.Id ? staleDeliveryManager : agent)
                .ToList()
        });
        var refreshed = normalized.Agents.Single(item => item.Id == deliveryManager.Id);

        Assert.Equal(AgentLifecycleStatus.Active, refreshed.Status);
        Assert.False(refreshed.IsTemplate);
        Assert.NotNull(refreshed.ProviderProfileId);
        Assert.True(refreshed.Permissions.CanObserveOtherAgents);
        Assert.Contains("process-manager", refreshed.Tags, StringComparer.Ordinal);
        Assert.Equal(ExpectedAgentTemplateSeedVersion, ReadManagedSeedVersion(refreshed.ConfigurationJson));
    }

    private static string ReadManagedSeedVersion(string configurationJson)
    {
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement.GetProperty(ManagedSeedVersionPropertyName).GetString()
            ?? throw new InvalidOperationException("Managed seed version is required for agent template refresh tests.");
    }

    private static void AssertCanonicalReasoningEffort(
        string configurationJson,
        AgentReasoningEffortLevel? expected)
    {
        Assert.Equal(
            expected,
            AgentThinkingEffortPolicy.ReadConfiguredEffort(configurationJson, "agent"));

        using var document = JsonDocument.Parse(configurationJson);
        var root = document.RootElement;
        Assert.DoesNotContain(
            root.EnumerateObject(),
            property => string.Equals(property.Name, "reasoningEffort", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            root.EnumerateObject(),
            property => string.Equals(property.Name, "think", StringComparison.OrdinalIgnoreCase));

        if (!root.TryGetProperty(
                AgentThinkingEffortPolicy.ModelParametersConfigurationPropertyName,
                out var modelParameters))
        {
            Assert.Null(expected);
            return;
        }

        Assert.DoesNotContain(
            modelParameters.EnumerateObject(),
            property => string.Equals(property.Name, "think", StringComparison.OrdinalIgnoreCase));
        if (expected is null)
        {
            Assert.DoesNotContain(
                modelParameters.EnumerateObject(),
                property => string.Equals(property.Name, "reasoningEffort", StringComparison.OrdinalIgnoreCase));
            return;
        }

        Assert.Equal(
            AgentThinkingEffortPolicy.FormatEffort(expected.Value),
            modelParameters
                .GetProperty(AgentThinkingEffortPolicy.ReasoningEffortConfigurationPropertyName)
                .GetString());
    }

    private static string ReadInlineSkillInstructions(string configurationJson)
    {
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement
                   .GetProperty("inlineSkill")
                   .GetProperty("instructions")
                   .GetString()
               ?? throw new InvalidOperationException("Inline skill instructions are required.");
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

    private sealed class TemporaryAgentTemplatePack : IDisposable
    {
        private static readonly string TargetSettingsRelativePath = Path.Combine(
            "teams",
            "delivery-platform",
            "members",
            "portfolio-architect",
            "settings.json");

        public TemporaryAgentTemplatePack()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"agent-template-pack-{Guid.NewGuid():N}");
            CopyDirectory(AgentTemplatePackLoader.FindPackRoot(), RootPath);
            TargetSettingsPath = Path.Combine(RootPath, TargetSettingsRelativePath);
        }

        public string RootPath { get; }

        public string TargetSettingsPath { get; }

        public void SetTargetSetting(string propertyName, string? valueJson)
        {
            var settings = JsonNode.Parse(File.ReadAllText(TargetSettingsPath))?.AsObject()
                ?? throw new InvalidOperationException(
                    $"Agent template settings '{TargetSettingsPath}' must contain a JSON object.");
            if (valueJson is null)
            {
                settings.Remove(propertyName);
            }
            else
            {
                settings[propertyName] = JsonNode.Parse(valueJson)
                    ?? throw new InvalidOperationException(
                        $"Agent template test value for '{propertyName}' must not be JSON null.");
            }

            File.WriteAllText(
                TargetSettingsPath,
                settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }

        private static void CopyDirectory(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);
            foreach (var filePath in Directory.EnumerateFiles(sourcePath))
            {
                File.Copy(
                    filePath,
                    Path.Combine(destinationPath, Path.GetFileName(filePath)));
            }

            foreach (var directoryPath in Directory.EnumerateDirectories(sourcePath))
            {
                CopyDirectory(
                    directoryPath,
                    Path.Combine(destinationPath, Path.GetFileName(directoryPath)));
            }
        }
    }
}
