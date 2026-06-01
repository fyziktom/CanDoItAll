using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessLaunchPlanningIntegrationTests
{
    [Fact]
    public async Task CreateLaunchPlanAsync_creates_draft_plan_with_resolved_ai_candidates_and_supports_approval_submission()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Launch Planning Proof {suffix}");
        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Launch Manager {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"manager.{suffix}@example.test");
        var builderId = await CreatePartyAsync(
            partyDirectoryService,
            $"Builder Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"builder.{suffix}@example.test");
        var reviewerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Reviewer Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"reviewer.{suffix}@example.test");

        await SaveAssignmentAsync(projectPartyBridge, projectId, managerId, ProjectPartyAssignmentRole.Manager, "manager", true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, builderId, ProjectPartyAssignmentRole.AiAgent, "builder", true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, reviewerId, ProjectPartyAssignmentRole.AiAgent, "reviewer", false);

        await SaveApprovedAiProfileAsync(
            aiAgentService,
            builderId,
            managerId,
            "Build generated app",
            "Create a simple Blazor app delivery.",
            "Workspace build",
            "Deterministic builder profile for launch planning validation.");
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            reviewerId,
            managerId,
            "Review generated app",
            "Inspect app delivery evidence.",
            "Readonly workspace",
            "Deterministic reviewer profile for launch planning validation.");

        var definition = BuildLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition.Editor);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Launch plan {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test launch plan validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var plans = await processesService.ListLaunchPlansAsync(saveResult.Value, projectId);
        var plan = Assert.Single(plans, item => item.Id == launchResult.Value);
        Assert.Equal(ProcessLaunchPlanStatus.Draft, plan.Status);
        Assert.Equal(2, plan.ResolvedRoleCount);
        Assert.Equal(2, plan.TotalRoleCount);

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        Assert.Equal("Launch plan " + suffix, details!.Name);
        Assert.Equal(ProcessLaunchPlanStatus.Draft, details.Status);
        Assert.Contains("Project assignments first", details.RecommendationStrategy, StringComparison.Ordinal);
        Assert.Contains("Human substitute approval", details.FallbackStrategy, StringComparison.Ordinal);
        Assert.Equal(2, details.Roles.Count);
        Assert.All(details.Roles, role => Assert.NotEmpty(role.Candidates));
        Assert.Contains(details.Roles, role =>
            string.Equals(role.DisplayName, definition.BuilderRoleName, StringComparison.Ordinal) &&
            role.SelectedCandidateId.HasValue &&
            role.IsResolved);
        Assert.Contains(details.Roles, role =>
            string.Equals(role.DisplayName, definition.ReviewerRoleName, StringComparison.Ordinal) &&
            role.SelectedCandidateId.HasValue &&
            role.IsResolved);

        var submitResult = await processesService.SubmitLaunchPlanForApprovalAsync(launchResult.Value, "integration-tests");
        Assert.True(submitResult.IsSuccess, string.Join(" | ", submitResult.Errors.Select(error => error.Message)));

        details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        Assert.Equal(ProcessLaunchPlanStatus.PendingApproval, details!.Status);
        var approval = Assert.Single(details.Approvals);
        Assert.Equal($"Launch Manager {suffix}", approval.ApproverDisplayName);
        Assert.Equal(ProcessLaunchApprovalStatus.Pending, approval.Status);
        Assert.True(approval.CollaborationThreadId.HasValue);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_completes_with_seeded_internal_agent_projection_without_hanging()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Seeded launch planning proof {suffix}");
        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Seeded Launch Manager {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"seeded.manager.{suffix}@example.test");
        await SaveAssignmentAsync(projectPartyBridge, projectId, managerId, ProjectPartyAssignmentRole.Manager, "manager", true);

        var roster = await aiAgentService.ListAgentDirectoryAsync();
        var programmingAgent = Assert.Single(
            roster,
            item => string.Equals(item.DisplayName, "Programming Workspace Analyst", StringComparison.Ordinal));
        var qaAgent = Assert.Single(
            roster,
            item => string.Equals(item.DisplayName, "Delivery QA Observer", StringComparison.Ordinal));

        await SaveAssignmentAsync(projectPartyBridge, projectId, programmingAgent.PartyId, ProjectPartyAssignmentRole.AiAgent, "builder", true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, qaAgent.PartyId, ProjectPartyAssignmentRole.AiAgent, "reviewer", false);

        var definition = BuildLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition.Editor);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var launchResult = await processesService.CreateLaunchPlanAsync(
            new ProcessLaunchCreateRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                LaunchName = $"Seeded launch {suffix}",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration test seeded launch planning validation.",
                RequestedBy = "integration-tests"
            },
            timeout.Token);
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        Assert.Equal(ProcessLaunchPlanStatus.Draft, details!.Status);
        Assert.Equal(2, details.Roles.Count);
        Assert.All(details.Roles, role => Assert.True(role.SelectedCandidateId.HasValue));
        Assert.Contains(details.Roles, role =>
            string.Equals(role.DisplayName, definition.BuilderRoleName, StringComparison.Ordinal) &&
            role.IsResolved);
        Assert.Contains(details.Roles, role =>
            string.Equals(role.DisplayName, definition.ReviewerRoleName, StringComparison.Ordinal) &&
            role.IsResolved);
    }

    [Fact]
    public async Task SubmitLaunchPlanForApprovalAsync_uses_human_substitute_when_manager_assignment_is_missing()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Launch Substitute Proof {suffix}");
        var substituteId = await CreatePartyAsync(
            partyDirectoryService,
            $"Launch Substitute {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"substitute.{suffix}@example.test");
        var builderId = await CreatePartyAsync(
            partyDirectoryService,
            $"Builder Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"builder.substitute.{suffix}@example.test");
        var reviewerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Reviewer Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"reviewer.substitute.{suffix}@example.test");

        await SaveAssignmentAsync(projectPartyBridge, projectId, substituteId, ProjectPartyAssignmentRole.Reviewer, "reviewer-human", true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, builderId, ProjectPartyAssignmentRole.AiAgent, "builder", true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, reviewerId, ProjectPartyAssignmentRole.AiAgent, "reviewer-ai", false);

        await SaveApprovedAiProfileAsync(
            aiAgentService,
            builderId,
            substituteId,
            "Build generated app",
            "Create a simple Blazor app delivery.",
            "Workspace build",
            "Deterministic builder profile for substitute validation.");
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            reviewerId,
            substituteId,
            "Review generated app",
            "Inspect app delivery evidence.",
            "Readonly workspace",
            "Deterministic reviewer profile for substitute validation.");

        var definition = BuildLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition.Editor);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Launch substitute {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test substitute approval validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var submitResult = await processesService.SubmitLaunchPlanForApprovalAsync(launchResult.Value, "integration-tests");
        Assert.True(submitResult.IsSuccess, string.Join(" | ", submitResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        var approval = Assert.Single(details!.Approvals);
        Assert.Equal($"Launch Substitute {suffix}", approval.ApproverDisplayName);
        Assert.Equal("Human substitute", approval.ApproverKind);
        Assert.Equal(substituteId, approval.HumanSubstitutePartyId);
        Assert.True(approval.CollaborationThreadId.HasValue);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_prefers_ai_candidates_that_match_required_skills()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Skill Guided Launch {suffix}");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Skill Guided Owner {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"owner.skill.{suffix}@example.test");
        var genericAgentId = await CreatePartyAsync(
            partyDirectoryService,
            $"Aardvark Generic Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"generic.skill.{suffix}@example.test");
        var skilledAgentId = await CreatePartyAsync(
            partyDirectoryService,
            $"Skilled Delivery Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"skilled.skill.{suffix}@example.test");

        await SaveApprovedAiProfileAsync(
            aiAgentService,
            genericAgentId,
            ownerId,
            "General AI work",
            "Provides a generic AI resource profile.",
            "Workspace build",
            "Generic AI resource for skill-sensitive launch planning.");
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            skilledAgentId,
            ownerId,
            "Skill-guided implementation",
            "Provides a skill-matched AI resource profile.",
            "Workspace build",
            "Skilled AI resource for skill-sensitive launch planning.");

        var skillDefinition = await hrService.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel
        {
            Name = $"Blazor SSR delivery {suffix}",
            Category = "Engineering",
            Description = "Serious Blazor SSR delivery capability."
        });
        Assert.True(skillDefinition.IsSuccess, string.Join(" | ", skillDefinition.Errors.Select(error => error.Message)));

        var skillAssignment = await hrService.SavePartySkillAsync(new PartySkillEditorModel
        {
            PartyId = skilledAgentId,
            SkillId = skillDefinition.Value,
            Proficiency = SkillProficiencyLevel.Expert,
            YearsExperience = 6,
            CertificationStatus = "Validated",
            Notes = "Explicitly skilled for Blazor SSR delivery."
        });
        Assert.True(skillAssignment.IsSuccess, string.Join(" | ", skillAssignment.Errors.Select(error => error.Message)));

        var definition = BuildSkillGuidedLaunchPlanningDefinition(projectId, skillDefinition.Value);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Skill guided launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test skill-guided launch validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var role = Assert.Single(details!.Roles);
        Assert.True(role.SelectedCandidateId.HasValue);
        var selectedCandidate = Assert.Single(role.Candidates, item => item.Id == role.SelectedCandidateId.Value);

        Assert.Equal(skilledAgentId, selectedCandidate.PartyId);
        Assert.Equal(ProcessLaunchCandidateKind.AiResource, selectedCandidate.CandidateKind);
        Assert.Contains("1 of 1 required skill", selectedCandidate.RecommendationSummary, StringComparison.Ordinal);

        var genericCandidate = Assert.Single(role.Candidates, item => item.PartyId == genericAgentId);
        Assert.Contains("does not currently match", genericCandidate.RecommendationSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_prefers_role_matching_ai_candidate_when_skills_are_not_recorded()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Role Fit Launch {suffix}");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Role Fit Owner {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"owner.rolefit.{suffix}@example.test");
        var genericAgentId = await CreatePartyAsync(
            partyDirectoryService,
            $"Aardvark Generic Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"generic.rolefit.{suffix}@example.test");
        var roleMatchedAgentId = await CreatePartyAsync(
            partyDirectoryService,
            $"Scenario Ledger Navigator {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"rolematched.rolefit.{suffix}@example.test");

        await SaveApprovedAiProfileAsync(
            aiAgentService,
            genericAgentId,
            ownerId,
            "General AI work",
            "Provides a generic AI resource profile.",
            "Workspace build",
            "Generic AI resource for role-fit launch planning.");
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            roleMatchedAgentId,
            ownerId,
            "Scenario ledger navigation",
            "Owns scenario-ledger navigation, concrete traceability, and execution flow control.",
            "Workspace build",
            "Role-matched AI resource for role-fit launch planning.");

        var definition = BuildRoleFitLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Role fit launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test role-fit launch validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var role = Assert.Single(details!.Roles);
        Assert.True(role.SelectedCandidateId.HasValue);
        var selectedCandidate = Assert.Single(role.Candidates, item => item.Id == role.SelectedCandidateId.Value);
        var genericCandidate = Assert.Single(role.Candidates, item => item.PartyId == genericAgentId);

        Assert.Equal(roleMatchedAgentId, selectedCandidate.PartyId);
        Assert.True(selectedCandidate.Score > genericCandidate.Score);
    }

    [Fact]
    public async Task MatchLaunchPlanWithHrManagerAsync_marks_required_agents_outside_selected_delivery_team()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var agentWorkspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Team Scoped HR Match {suffix}");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Team Scoped Owner {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"owner.teamscope.{suffix}@example.test");
        var teamAgentPartyId = await CreatePartyAsync(
            partyDirectoryService,
            $"General Team Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"team.agent.{suffix}@example.test");
        var outsideAgentPartyId = await CreatePartyAsync(
            partyDirectoryService,
            $"Specialist Outside Team Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"outside.agent.{suffix}@example.test");

        await SaveApprovedAiProfileAsync(
            aiAgentService,
            teamAgentPartyId,
            ownerId,
            "General delivery",
            "Handles broad delivery tasks.",
            "Workspace build",
            "General AI resource for team-scoped HR matching.");
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            outsideAgentPartyId,
            ownerId,
            "Skill-guided implementation",
            "Owns required specialist delivery work.",
            "Workspace build",
            "Specialist AI resource outside the selected team.");
        await aiAgentService.SynchronizeDirectoryProjectionAsync();
        var aiDirectory = await aiAgentService.ListAgentDirectoryAsync();
        var teamTechnicalAgentId = Assert.Single(aiDirectory, item => item.PartyId == teamAgentPartyId).TechnicalAgentId;
        Assert.True(teamTechnicalAgentId.HasValue);

        var technicalAgents = await agentWorkspaceService.ListAgentsAsync(includeTemplates: false);
        if (technicalAgents.All(item => item.Id != teamTechnicalAgentId.Value))
        {
            var teamAgentEditor = await agentWorkspaceService.GetAgentEditorAsync();
            teamAgentEditor.Id = teamTechnicalAgentId.Value;
            teamAgentEditor.Name = $"General Team Agent {suffix}";
            teamAgentEditor.RoleTitle = "General delivery agent";
            teamAgentEditor.Summary = "AgentFramework backing agent for team-scoped HR matching.";
            teamAgentEditor.Instructions = "Participate in the selected delivery team.";
            teamAgentEditor.Status = AgentLifecycleStatus.Active;
            teamAgentEditor.IsTemplate = false;
            teamAgentEditor.TemplateKey = $"team-scope-{teamTechnicalAgentId.Value:N}";
            await agentWorkspaceService.SaveAgentAsync(teamAgentEditor);
        }

        var skillDefinition = await hrService.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel
        {
            Name = $"Team Scope Specialist Skill {suffix}",
            Category = "Delivery",
            Description = "Required role capability for team-scoped HR matching proof."
        });
        Assert.True(skillDefinition.IsSuccess, string.Join(" | ", skillDefinition.Errors.Select(error => error.Message)));

        var skillAssignment = await hrService.SavePartySkillAsync(new PartySkillEditorModel
        {
            PartyId = outsideAgentPartyId,
            SkillId = skillDefinition.Value,
            Proficiency = SkillProficiencyLevel.Expert,
            YearsExperience = 6,
            CertificationStatus = "Validated",
            Notes = "Specialist outside the selected delivery team."
        });
        Assert.True(skillAssignment.IsSuccess, string.Join(" | ", skillAssignment.Errors.Select(error => error.Message)));

        var teamId = await agentWorkspaceService.SaveAgentTeamAsync(new AgentTeamEditorModel
        {
            Name = $"Primary Delivery Team {suffix}",
            Description = "Contains the preferred delivery pod for HR matching.",
            AgentIds = [teamTechnicalAgentId.Value]
        });

        var definition = BuildSkillGuidedLaunchPlanningDefinition(projectId, skillDefinition.Value);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Team scoped launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test team-scoped HR match validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var matchResult = await processesService.MatchLaunchPlanWithHrManagerAsync(
            launchResult.Value,
            teamId,
            "integration-tests");
        Assert.True(matchResult.IsSuccess, string.Join(" | ", matchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var role = Assert.Single(details!.Roles);
        Assert.True(role.SelectedCandidateId.HasValue);
        var selectedCandidate = Assert.Single(role.Candidates, item => item.Id == role.SelectedCandidateId.Value);
        var teamCandidate = Assert.Single(role.Candidates, item => item.PartyId == teamAgentPartyId);

        Assert.Equal(outsideAgentPartyId, selectedCandidate.PartyId);
        Assert.Equal(teamId, selectedCandidate.AgentTeamId);
        Assert.Equal($"Primary Delivery Team {suffix}", selectedCandidate.AgentTeamName);
        Assert.True(selectedCandidate.IsOutsideSelectedTeam);
        Assert.Equal(teamId, teamCandidate.AgentTeamId);
        Assert.False(teamCandidate.IsOutsideSelectedTeam);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_prefers_blazor_specialist_for_blazor_app_delivery()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Blazor launch proof {suffix}",
            objective: "Build a working Blazor Web App with browser-visible interactions.");
        var definition = BuildBlazorLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Blazor app launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Build a small Blazor Web App and validate it with dotnet build, dotnet run, and browser proof.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var role = Assert.Single(details!.Roles);
        Assert.True(role.SelectedCandidateId.HasValue);
        var selectedCandidate = Assert.Single(role.Candidates, item => item.Id == role.SelectedCandidateId.Value);
        var workspaceAnalyst = Assert.Single(role.Candidates, item => string.Equals(item.DisplayName, "Programming Workspace Analyst", StringComparison.Ordinal));

        Assert.Equal("Blazor Application Developer", selectedCandidate.DisplayName);
        Assert.True(selectedCandidate.Score > workspaceAnalyst.Score);
        Assert.Contains(role.Candidates, item => string.Equals(item.DisplayName, ".NET Application Developer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_keeps_blazor_specialist_on_implementation_roles_only()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Blazor governed launch proof {suffix}",
            objective: "Build a working Blazor Web App with browser-visible interactions.");
        var definition = BuildBlazorGovernedLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Blazor governed launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Build a small Blazor Web App and validate it with dotnet build, dotnet run, and browser proof.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var productOwner = GetSelectedCandidate(details!, "Product owner");
        var deliveryManager = GetSelectedCandidate(details, "Delivery manager");
        var leadEngineer = GetSelectedCandidate(details, "Lead engineer");

        Assert.Equal("Blazor Application Developer", leadEngineer.DisplayName);
        Assert.Equal("Delivery Manager", deliveryManager.DisplayName);
        Assert.DoesNotContain(productOwner.DisplayName, TechnicalImplementationAgentNames);
        Assert.DoesNotContain(deliveryManager.DisplayName, TechnicalImplementationAgentNames);

        var productOwnerBlazorCandidate = GetCandidate(details, "Product owner", "Blazor Application Developer");
        var deliveryManagerBlazorCandidate = GetCandidate(details, "Delivery manager", "Blazor Application Developer");
        Assert.True(productOwner.Score > productOwnerBlazorCandidate.Score);
        Assert.True(deliveryManager.Score > deliveryManagerBlazorCandidate.Score);
    }

    [Fact]
    public async Task StartRunAsync_direct_assisted_start_uses_ai_directory_for_agent_capable_roles()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Direct Blazor staffing proof {suffix}",
            objective: "Build a working Blazor Web App with browser-visible interactions.");
        var definition = BuildBlazorGovernedLaunchPlanningDefinition(projectId);
        var productOwnerRoleId = definition.Roles.Single(item => item.Key == "product-owner").Id!.Value;
        var deliveryManagerRoleId = definition.Roles.Single(item => item.Key == "delivery-manager").Id!.Value;
        var leadEngineerRoleId = definition.Roles.Single(item => item.Key == "lead-engineer").Id!.Value;
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = $"Direct Blazor app run {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Build a small Blazor Web App and validate it with dotnet build, dotnet run, and browser proof."
        });
        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetRunDetailsAsync(runResult.Value);
        var assignmentsByRole = details.Assignments.ToDictionary(item => item.RoleRequirementId);
        Assert.False(assignmentsByRole[leadEngineerRoleId].IsCapabilityGap);
        Assert.Equal("Blazor Application Developer", assignmentsByRole[leadEngineerRoleId].DisplayName);
        Assert.Equal("Delivery Manager", assignmentsByRole[deliveryManagerRoleId].DisplayName);
        Assert.DoesNotContain(assignmentsByRole[productOwnerRoleId].DisplayName, TechnicalImplementationAgentNames);
        Assert.DoesNotContain(assignmentsByRole[deliveryManagerRoleId].DisplayName, TechnicalImplementationAgentNames);

        var stepRun = Assert.Single(details.StepRuns);
        Assert.Equal("Blazor Application Developer", stepRun.CurrentExecutorName);
        Assert.NotEqual(ProcessCapabilityGapSeverity.Critical, stepRun.CapabilityGapSeverity);

        var run = await processesService.GetRunAsync(runResult.Value);
        Assert.NotNull(run);
        Assert.Equal(
            details.StepRuns.Count(item => item.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None),
            run!.CapabilityGapCount);
    }

    [Fact]
    public async Task ProvisionLaunchPlanAsync_assigns_enabled_provider_to_bound_providerless_ai_resource()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Providerless launch provisioning proof {suffix}",
            objective: "Provision a process-selected AI resource before governed execution.");
        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            $"Providerless Launch Manager {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"providerless.manager.{suffix}@example.test");
        var aiPartyId = await CreatePartyAsync(
            partyDirectoryService,
            $"Providerless Process Agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AiSteward,
            $"providerless.agent.{suffix}@example.test");

        await SaveAssignmentAsync(projectPartyBridge, projectId, managerId, ProjectPartyAssignmentRole.Manager, "manager", true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, aiPartyId, ProjectPartyAssignmentRole.AiAgent, "providerless-agent", true);
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            aiPartyId,
            managerId,
            "Process role execution",
            "Execute launch-plan work after provisioning assigns a provider.",
            "Process execution",
            "Providerless profile created to prove process provisioning repairs runnable provider assignment.",
            assignProvider: false);

        var initialWorkspace = await aiAgentService.GetAgentWorkspaceAsync(aiPartyId);
        Assert.NotNull(initialWorkspace);
        Assert.True(initialWorkspace!.TechnicalAgentId.HasValue);
        Assert.Null(initialWorkspace.Profile.ProviderProfileId);

        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Providerless launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test providerless process provisioning validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        var role = Assert.Single(details!.Roles);
        var providerlessCandidate = Assert.Single(role.Candidates, item => item.PartyId == aiPartyId);
        Assert.True(providerlessCandidate.RequiresProvisioning);
        Assert.Contains("runnable", providerlessCandidate.AvailabilitySummary, StringComparison.OrdinalIgnoreCase);

        var selectResult = await processesService.SelectLaunchCandidateAsync(new ProcessLaunchCandidateSelectionRequest
        {
            LaunchPlanId = launchResult.Value,
            LaunchPlanRoleId = role.Id,
            CandidateId = providerlessCandidate.Id
        });
        Assert.True(selectResult.IsSuccess, string.Join(" | ", selectResult.Errors.Select(error => error.Message)));

        var submitResult = await processesService.SubmitLaunchPlanForApprovalAsync(launchResult.Value, "integration-tests");
        Assert.True(submitResult.IsSuccess, string.Join(" | ", submitResult.Errors.Select(error => error.Message)));

        var approveResult = await processesService.DecideLaunchPlanApprovalAsync(new ProcessLaunchApprovalDecisionRequest
        {
            LaunchPlanId = launchResult.Value,
            Status = ProcessLaunchApprovalStatus.Approved,
            ResolutionSummary = "Approved providerless provisioning proof.",
            DecidedBy = "integration-tests"
        });
        Assert.True(approveResult.IsSuccess, string.Join(" | ", approveResult.Errors.Select(error => error.Message)));

        var provisionResult = await processesService.ProvisionLaunchPlanAsync(launchResult.Value, "integration-tests");
        Assert.True(provisionResult.IsSuccess, string.Join(" | ", provisionResult.Errors.Select(error => error.Message)));

        var provisionedWorkspace = await aiAgentService.GetAgentWorkspaceAsync(aiPartyId);
        Assert.NotNull(provisionedWorkspace);
        Assert.True(provisionedWorkspace!.Profile.ProviderProfileId.HasValue);
        Assert.False(string.IsNullOrWhiteSpace(provisionedWorkspace.ProviderName));

        details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        Assert.Equal(ProcessLaunchPlanStatus.Ready, details!.Status);
        role = Assert.Single(details.Roles);
        Assert.False(role.RequiresProvisioning);
        Assert.Equal(providerlessCandidate.Id, role.SelectedCandidateId);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_uses_project_structure_context_for_generic_implementation_role()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Project structure launch proof {suffix}",
            objective: "Deliver the requested application from the project structure.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build interactive app",
                "Implementation",
                "Build a small Blazor Web App under C:\\programovani\\dotnet\\StructuredLaunchProof. Validate dotnet build, dotnet run, and browser-visible behavior.",
                null));
        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Generic app launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var role = Assert.Single(details!.Roles);
        Assert.True(role.SelectedCandidateId.HasValue);
        var selectedCandidate = Assert.Single(role.Candidates, item => item.Id == role.SelectedCandidateId.Value);
        var workspaceAnalyst = Assert.Single(role.Candidates, item => string.Equals(item.DisplayName, "Programming Workspace Analyst", StringComparison.Ordinal));
        var qaObserver = Assert.Single(role.Candidates, item => string.Equals(item.DisplayName, "Delivery QA Observer", StringComparison.Ordinal));

        Assert.Equal("Blazor Application Developer", selectedCandidate.DisplayName);
        Assert.True(selectedCandidate.Score > workspaceAnalyst.Score);
        Assert.True(selectedCandidate.Score > qaObserver.Score);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_uses_selected_project_structure_stack_for_javascript_work()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"JavaScript project-structure launch proof {suffix}",
            objective: "Mixed batch: one .NET CLI app, one Blazor app, and one JavaScript app. The selected work item decides the stack.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build PantryPalette JS app",
                "Implementation",
                "Build a small JavaScript app under C:\\repositories\\CanDoItAll\\output\\process-core-sim-20260506\\pantry-palette-js. Validate package scripts and browser-visible behavior.",
                null,
                ObjectSubtype: "task"));
        var definition = BuildJavaScriptArchitectureAndImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"JavaScript app launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var architect = GetSelectedCandidate(details!, "Solution architect");
        var implementer = GetSelectedCandidate(details, "Lead engineer");
        var dotnetArchitect = GetCandidate(details, "Solution architect", ".NET Solution Architect");
        var dotnetDeveloper = GetCandidate(details, "Lead engineer", ".NET Application Developer");
        var blazorDeveloper = GetCandidate(details, "Lead engineer", "Blazor Application Developer");

        Assert.Equal("JavaScript Solution Architect", architect.DisplayName);
        Assert.Equal("JavaScript Application Developer", implementer.DisplayName);
        Assert.True(architect.Score > dotnetArchitect.Score);
        Assert.True(implementer.Score > dotnetDeveloper.Score);
        Assert.True(implementer.Score > blazorDeveloper.Score);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_uses_static_client_web_context_to_prefer_javascript_work()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Static client web launch proof {suffix}",
            objective: "Deliver a browser-visible static web page with no backend and static hosting.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build static browser application",
                "Implementation",
                "Build a browser game as a static web page under C:\\programovani\\candoitall-dev-output\\static-client-web-proof. No backend; all state is local to the app; host on ordinary static web hosting; keyboard controls are required.",
                null,
                ObjectSubtype: "task"));
        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Static client web launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var implementer = GetSelectedCandidate(details!, "Lead engineer");
        var javascriptDeveloper = GetCandidate(details, "Lead engineer", "JavaScript Application Developer");
        var dotnetDeveloper = GetCandidate(details, "Lead engineer", ".NET Application Developer");
        var blazorDeveloper = GetCandidate(details, "Lead engineer", "Blazor Application Developer");

        Assert.Equal("JavaScript Application Developer", implementer.DisplayName);
        Assert.True(javascriptDeveloper.Score > dotnetDeveloper.Score);
        Assert.True(javascriptDeveloper.Score > blazorDeveloper.Score);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_uses_blazor_wasm_pwa_context_to_prefer_blazor_specialist_over_static_client_javascript()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Blazor WASM PWA launch proof {suffix}",
            objective: "Deliver a client-only Blazor WebAssembly PWA with static hosting.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build Blazor WebAssembly PWA",
                "Implementation",
                "Build a Blazor WebAssembly PWA Tetris game under C:\\programovani\\dotnet-demo\\output. No backend; all state is local to the app; host on ordinary static hosting; include PWA manifest and service worker; keyboard controls are required.",
                null,
                ObjectSubtype: "task"));
        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Blazor WASM PWA launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var implementer = GetSelectedCandidate(details!, "Lead engineer");
        var javascriptDeveloper = GetCandidate(details, "Lead engineer", "JavaScript Application Developer");
        var blazorDeveloper = GetCandidate(details, "Lead engineer", "Blazor Application Developer");

        Assert.Equal("Blazor Application Developer", implementer.DisplayName);
        Assert.True(blazorDeveloper.Score > javascriptDeveloper.Score);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_prefers_dotnet_qa_for_blazor_quality_review()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Blazor QA launch proof {suffix}",
            objective: "Review a Blazor WebAssembly PWA delivery with .NET validation and browser proof.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Review Blazor WebAssembly PWA quality",
                "Quality",
                "Review a Blazor WebAssembly PWA under C:\\programovani\\dotnet-demo\\output. Validate dotnet build, runtime behavior, browser proof, manifest behavior, service worker behavior, and keyboard controls.",
                null,
                ObjectSubtype: "task"));
        var definition = BuildGenericQaLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Blazor QA launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var selectedQa = GetSelectedCandidate(details!, "QA lead");
        var dotnetQa = GetCandidate(details, "QA lead", ".NET QA Review Lead");
        var javascriptQa = GetCandidate(details, "QA lead", "JavaScript QA Review Lead");

        Assert.Equal(".NET QA Review Lead", selectedQa.DisplayName);
        Assert.True(dotnetQa.Score > javascriptQa.Score);

        var matchResult = await processesService.MatchLaunchPlanWithHrManagerAsync(
            launchResult.Value,
            agentTeamId: null,
            requestedBy: "integration-tests");
        Assert.True(matchResult.IsSuccess, string.Join(" | ", matchResult.Errors.Select(error => error.Message)));

        var matchedDetails = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(matchedDetails);
        var matchedDotnetQa = GetCandidate(matchedDetails!, "QA lead", ".NET QA Review Lead");
        var matchedJavascriptQa = GetCandidate(matchedDetails, "QA lead", "JavaScript QA Review Lead");

        Assert.True(matchedDotnetQa.IsRecommended);
        Assert.False(matchedJavascriptQa.IsRecommended);
        Assert.True(matchedDotnetQa.Score > matchedJavascriptQa.Score);

        var secondMatchResult = await processesService.MatchLaunchPlanWithHrManagerAsync(
            launchResult.Value,
            agentTeamId: null,
            requestedBy: "integration-tests");
        Assert.True(secondMatchResult.IsSuccess, string.Join(" | ", secondMatchResult.Errors.Select(error => error.Message)));

        var matchedAgainDetails = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(matchedAgainDetails);
        var matchedAgainDotnetQa = GetCandidate(matchedAgainDetails!, "QA lead", ".NET QA Review Lead");
        var matchedAgainJavascriptQa = GetCandidate(matchedAgainDetails, "QA lead", "JavaScript QA Review Lead");

        Assert.Equal(matchedDotnetQa.Score, matchedAgainDotnetQa.Score);
        Assert.Equal(matchedJavascriptQa.Score, matchedAgainJavascriptQa.Score);
    }

    [Fact]
    public async Task StartRunAsync_direct_static_client_web_context_prefers_javascript_developer_for_implementation_role()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Direct static client web staffing proof {suffix}",
            objective: "Deliver a browser-visible static web page with no backend and static hosting.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build static browser application",
                "Implementation",
                "Build a browser app as a static web page under C:\\programovani\\candoitall-dev-output\\direct-static-client-web-proof. No backend; all state is local to the app; host on ordinary static web hosting; keyboard controls are required.",
                null,
                ObjectSubtype: "task"));
        var definition = BuildJavaScriptArchitectureAndImplementationLaunchPlanningDefinition(projectId);
        var architectRoleId = definition.Roles.Single(item => item.Key == "solution-architect").Id!.Value;
        var leadEngineerRoleId = definition.Roles.Single(item => item.Key == "lead-engineer").Id!.Value;
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = $"Direct static browser app run {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            }
        });
        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetRunDetailsAsync(runResult.Value);
        var assignmentsByRole = details.Assignments.ToDictionary(item => item.RoleRequirementId);

        Assert.Equal("JavaScript Solution Architect", assignmentsByRole[architectRoleId].DisplayName);
        Assert.Equal("JavaScript Application Developer", assignmentsByRole[leadEngineerRoleId].DisplayName);

        var stepsByTitle = details.StepRuns.ToDictionary(item => item.Title);
        Assert.Equal("JavaScript Solution Architect", stepsByTitle["Review architecture"].CurrentExecutorName);
        Assert.Equal("JavaScript Application Developer", stepsByTitle["Build requested app"].CurrentExecutorName);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_does_not_select_blazor_specialist_from_negated_stack_wording()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Negated Blazor launch proof {suffix}",
            objective: "Deliver a generic .NET console app without a browser UI.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build Harbor Ledger console app",
                "Implementation",
                "Build a small C#/.NET console app under C:\\programovani\\candoitall-dev-output\\harbor-ledger-proof. Archetype: console app, not Blazor, not Razor, not browser UI. Validate dotnet build and console runtime output.",
                null,
                ObjectSubtype: "task"));
        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Console app launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var implementer = GetSelectedCandidate(details!, "Lead engineer");
        var dotnetDeveloper = GetCandidate(details, "Lead engineer", ".NET Application Developer");
        var blazorDeveloper = GetCandidate(details, "Lead engineer", "Blazor Application Developer");

        Assert.True(
            dotnetDeveloper.Score > blazorDeveloper.Score,
            $".NET score {dotnetDeveloper.Score} should outrank Blazor score {blazorDeveloper.Score} for non-Blazor .NET console work. Selected {implementer.DisplayName} with score {implementer.Score}.");
        Assert.Equal(".NET Application Developer", implementer.DisplayName);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_ignores_sibling_stack_when_project_structure_context_is_selected()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Sibling stack launch proof {suffix}",
            objective: "Mixed batch: one C#/.NET console app and one JavaScript browser app. The selected work item decides the staffing stack.");
        var selectedWorkItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build Harbor Ledger console app",
                "Implementation",
                "Build a small C#/.NET console app under C:\\programovani\\candoitall-dev-output\\harbor-ledger-proof. Archetype: console app, not Blazor, not Razor, not browser UI, and not JavaScript. Validate dotnet build and console runtime output.",
                null,
                ObjectSubtype: "task"));
        await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build Market Mosaic JavaScript app",
                "Implementation",
                "Build a small JavaScript browser app under C:\\programovani\\candoitall-dev-output\\market-mosaic-proof. Validate package scripts and browser-visible behavior.",
                null,
                ObjectSubtype: "task"));
        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Console app launch with sibling scenario {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = selectedWorkItem.Id,
                ParentNodeTitle = selectedWorkItem.Title
            },
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);

        var implementer = GetSelectedCandidate(details!, "Lead engineer");
        var dotnetDeveloper = GetCandidate(details, "Lead engineer", ".NET Application Developer");
        var javascriptDeveloper = GetCandidate(details, "Lead engineer", "JavaScript Application Developer");

        Assert.True(
            dotnetDeveloper.Score > javascriptDeveloper.Score,
            $".NET score {dotnetDeveloper.Score} should outrank JavaScript score {javascriptDeveloper.Score} when the selected work item is C#/.NET and the JavaScript work is only a sibling. Selected {implementer.DisplayName} with score {implementer.Score}.");
        Assert.Equal(".NET Application Developer", implementer.DisplayName);
    }

    [Fact]
    public void ExternalTargetPathNormalization_strips_escaped_line_break_labels()
    {
        var serviceType = typeof(ProcessesService).Assembly.GetType("CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService");
        Assert.NotNull(serviceType);

        var method = serviceType!.GetMethod(
            "TryNormalizeAbsoluteExternalPathCandidate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        object?[] parameters =
        [
            @"C:\repositories\CanDoItAll\output\process-core-sim-20260506\pantry-palette-js\nRequired output directory",
            null
        ];

        var normalized = (bool)method!.Invoke(null, parameters)!;

        Assert.True(normalized);
        Assert.Equal(
            @"C:\repositories\CanDoItAll\output\process-core-sim-20260506\pantry-palette-js",
            parameters[1]);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_infers_project_structure_context_from_single_process_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Linked process launch proof {suffix}",
            objective: "Use the project structure target when a process is launched from the process workspace.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build neighborhood tool",
                "Implementation target",
                "Build a small application under C:\\programovani\\dotnet\\LinkedProcessLaunchProof and validate it end to end.",
                null));
        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var processNodeKey = $"process-definition:{saveResult.Value:D}";
        await workbenchService.LinkObjectsAsync(projectId, workItem.Id, processNodeKey, ProjectObjectLinkKind.Uses);

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Linked app launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Created from the process workspace.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        Assert.True(ProcessProjectStructureContextFormatter.TryParse(details!.TriggerReason, out var parsedContext));
        Assert.NotNull(parsedContext);
        Assert.Equal(projectId, parsedContext!.ProjectId);
        Assert.Equal(processNodeKey, parsedContext.NodeId);
        Assert.Equal(workItem.Id, parsedContext.ParentNodeId);
        Assert.Equal(workItem.Title, parsedContext.ParentNodeTitle);
        Assert.Contains("Project structure target", details.TriggerReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLaunchPlanAsync_reuses_open_project_structure_launch_plan_for_same_context()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(
            projectsService,
            $"Project structure retry launch proof {suffix}",
            objective: "Use a pending project-structure launch plan when the same start request is retried.");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build reusable launch target",
                "Implementation target",
                "Build a small application under C:\\programovani\\dotnet\\ReusableLaunchProof and validate it end to end.",
                null));
        var definition = BuildGenericImplementationLaunchPlanningDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var request = new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Reusable app launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Started from project structure.",
            ProjectStructureContext = new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-node",
                NodeTitle = "Delivery process",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            RequestedBy = "project-structure"
        };

        var firstLaunchResult = await processesService.CreateLaunchPlanAsync(request);
        Assert.True(firstLaunchResult.IsSuccess, string.Join(" | ", firstLaunchResult.Errors.Select(error => error.Message)));

        var retryLaunchResult = await processesService.CreateLaunchPlanAsync(request);
        Assert.True(retryLaunchResult.IsSuccess, string.Join(" | ", retryLaunchResult.Errors.Select(error => error.Message)));

        Assert.Equal(firstLaunchResult.Value, retryLaunchResult.Value);

        var launchPlans = await processesService.ListLaunchPlansAsync(saveResult.Value, projectId);
        Assert.Single(launchPlans, item => item.Name == request.LaunchName);
    }

    private static readonly string[] TechnicalImplementationAgentNames =
    [
        "Blazor Application Developer",
        ".NET Application Developer",
        "JavaScript Application Developer",
        "Programming Workspace Analyst"
    ];

    private static LaunchPlanningDefinitionFixture BuildLaunchPlanningDefinition(Guid projectId)
    {
        var builderRoleId = Guid.NewGuid();
        var reviewerRoleId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        const string builderRoleName = "Application builder agent";
        const string reviewerRoleName = "Application reviewer agent";

        return new LaunchPlanningDefinitionFixture(
            new ProcessDefinitionEditorModel
            {
                ProjectId = projectId,
                Name = "Launch planning proof process",
                Summary = "Creates a launch plan for a simple app delivery.",
                ValueStatement = "Launch planning must resolve AI candidates before approval starts.",
                CustomerName = "Integration proof customer",
                OwnerName = "Integration proof owner",
                GovernancePolicySummary = "Launch planning stays explicit and durable.",
                ChangeSummary = "Launch planning integration proof.",
                ConstitutionRuleSummary = "No execution starts before staffing and approval.",
                OperatingModeSummary = "Assisted execution.",
                SimulationReadinessSummary = "Safe for deterministic validation.",
                Roles =
                [
                    new ProcessRoleEditorModel
                    {
                        Id = builderRoleId,
                        Key = "application-builder-ai",
                        DisplayName = builderRoleName,
                        Purpose = "Generate the app delivery.",
                        StaffingIntent = "Technical AI builder.",
                        PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                        PreferredExecutorKind = "AI agent",
                        DefaultAllocationPercent = 100
                    },
                    new ProcessRoleEditorModel
                    {
                        Id = reviewerRoleId,
                        Key = "application-reviewer-ai",
                        DisplayName = reviewerRoleName,
                        Purpose = "Review the app delivery.",
                        StaffingIntent = "Technical AI reviewer.",
                        PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                        PreferredExecutorKind = "AI agent",
                        DefaultAllocationPercent = 100
                    }
                ],
                MessagingPolicies =
                [
                    new ProcessRoleMessagingPolicyEditorModel
                    {
                        SourceRoleRequirementId = builderRoleId,
                        TargetRoleRequirementId = reviewerRoleId
                    }
                ],
                Steps =
                [
                    new ProcessStepEditorModel
                    {
                        Id = buildStepId,
                        Key = "build-app",
                        Title = "Build app",
                        StepKind = ProcessStepKind.Start,
                        InputContractSummary = "Simple app specification.",
                        OutputContractSummary = "App delivery prepared for review.",
                        EvidenceContractSummary = "Generated assets are captured.",
                        DecisionRightsSummary = "Builder completes deterministic generation.",
                        ExceptionPolicySummary = "Fail when the app delivery is missing.",
                        TargetLeadHours = 1,
                        CanvasX = 180,
                        CanvasY = 180,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                RoleRequirementId = builderRoleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible
                            }
                        ]
                    },
                    new ProcessStepEditorModel
                    {
                        Key = "review-app",
                        Title = "Review app",
                        StepKind = ProcessStepKind.Review,
                        InputContractSummary = "Generated app delivery.",
                        OutputContractSummary = "Validated app delivery.",
                        EvidenceContractSummary = "Review evidence is captured.",
                        DecisionRightsSummary = "Reviewer confirms app readiness.",
                        ExceptionPolicySummary = "Do not close without review evidence.",
                        TargetLeadHours = 1,
                        Dependencies =
                        [
                            new ProcessStepDependencyEditorModel
                            {
                                Id = Guid.NewGuid(),
                                DependsOnStepId = buildStepId
                            }
                        ],
                        CanvasX = 520,
                        CanvasY = 180,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                RoleRequirementId = reviewerRoleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible
                            }
                        ]
                    }
                ]
            },
            builderRoleName,
            reviewerRoleName);
    }

    private static ProcessDefinitionEditorModel BuildSkillGuidedLaunchPlanningDefinition(Guid projectId, Guid requiredSkillId)
    {
        var builderRoleId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Skill-guided launch planning proof process",
            Summary = "Prefers AI candidates whose CRM-HR party skills match the role requirements.",
            ValueStatement = "AI launch planning must not recommend an arbitrary bound agent when an explicit skill-matched resource exists.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "Required skills remain explicit in launch planning.",
            ChangeSummary = "Skill-guided launch planning integration proof.",
            ConstitutionRuleSummary = "Do not ignore required skills for AI staffing.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = builderRoleId,
                    Key = "skill-guided-builder-ai",
                    DisplayName = "Skill-guided builder agent",
                    Purpose = "Generate the requested delivery artifact.",
                    StaffingIntent = "Technical AI builder selected from CRM-HR and AgentFramework.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    RequiredSkillIds = [requiredSkillId],
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = buildStepId,
                    Key = "build-artifact",
                    Title = "Build artifact",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Skill-guided delivery request.",
                    OutputContractSummary = "Delivery artifact prepared.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Selected AI resource completes the work.",
                    ExceptionPolicySummary = "Fail when no suitable AI resource is selected.",
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = builderRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildRoleFitLaunchPlanningDefinition(Guid projectId)
    {
        var navigatorRoleId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Role-fit launch planning proof process",
            Summary = "Prefers AI candidates whose factual role fit matches the process role even when explicit party skills are missing.",
            ValueStatement = "Launch planning must not recommend the first alphabetically bound agent when a better role match is available.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "Role wording and staffing intent remain meaningful in launch planning.",
            ChangeSummary = "Role-fit launch planning integration proof.",
            ConstitutionRuleSummary = "Do not collapse skill-less AI role selection to the first bound directory entry.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = navigatorRoleId,
                    Key = "scenario-ledger-navigator",
                    DisplayName = "Scenario ledger navigator",
                    Purpose = "Owns scenario-ledger navigation, traceability, and execution flow control.",
                    StaffingIntent = "Select the AI resource whose role wording and capabilities best match scenario-ledger navigation.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = buildStepId,
                    Key = "navigate-ledger",
                    Title = "Navigate ledger",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Role-fit launch planning request.",
                    OutputContractSummary = "Role-fit candidate selected.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Selected AI resource owns the navigation work.",
                    ExceptionPolicySummary = "Fail when an arbitrary agent is selected ahead of a better role match.",
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = navigatorRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildBlazorLaunchPlanningDefinition(Guid projectId)
    {
        var leadEngineerRoleId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Blazor launch planning proof process",
            Summary = "Prefers a Blazor specialist when the requested app delivery is explicitly Blazor.",
            ValueStatement = "Technology-specific app delivery must route to the most relevant specialist without hardcoding a sample domain.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "Role matching stays generic and technology-aware.",
            ChangeSummary = "Blazor specialist launch planning integration proof.",
            ConstitutionRuleSummary = "Do not route Blazor implementation to a broad analyst when a Blazor specialist is available.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = leadEngineerRoleId,
                    Key = "lead-engineer",
                    DisplayName = "Lead engineer",
                    Purpose = "Build a small Blazor Web App with visible interactive behavior.",
                    StaffingIntent = "Select a Blazor-capable AI implementation specialist with .NET build, test, run, and browser validation capability.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = buildStepId,
                    Key = "build-blazor-app",
                    Title = "Build Blazor app",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Blazor app delivery request.",
                    OutputContractSummary = "Working Blazor app prepared with validation evidence.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Selected AI resource owns implementation.",
                    ExceptionPolicySummary = "Fail when a less relevant generalist is selected ahead of a Blazor specialist.",
                    AllowedOperations =
                    [
                        ProcessStepOperation.WriteManagedProcessArtifacts,
                        ProcessStepOperation.MutateProductTarget,
                        ProcessStepOperation.RunValidation,
                        ProcessStepOperation.CaptureRuntimeProof
                    ],
                    OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetMutable,
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = leadEngineerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildBlazorGovernedLaunchPlanningDefinition(Guid projectId)
    {
        var productOwnerRoleId = Guid.NewGuid();
        var deliveryManagerRoleId = Guid.NewGuid();
        var leadEngineerRoleId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Blazor governed launch planning proof process",
            Summary = "Keeps technology specialists on implementation roles while coordination roles use role-fit staffing.",
            ValueStatement = "Technology context must inform app-build staffing without collapsing every governance role onto the same specialist.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "Role matching separates role identity from work-item technology context.",
            ChangeSummary = "Blazor governed launch planning integration proof.",
            ConstitutionRuleSummary = "Do not select a technical implementation specialist for product ownership or delivery management solely because the work item is technical.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = productOwnerRoleId,
                    Key = "product-owner",
                    DisplayName = "Product owner",
                    Purpose = "Convert business intent into acceptance boundaries, priority decisions, and value trade-offs.",
                    StaffingIntent = "Select a scope and stakeholder decision owner, not an implementation specialist.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.CustomerContact,
                    PreferredExecutorKind = "person-or-agent",
                    DefaultAllocationPercent = 35
                },
                new ProcessRoleEditorModel
                {
                    Id = deliveryManagerRoleId,
                    Key = "delivery-manager",
                    DisplayName = "Delivery manager",
                    Purpose = "Keep delivery feasible, staffed, sequenced, and escalation-ready.",
                    StaffingIntent = "Select a delivery-side coordinator with governance and readiness focus.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person-or-agent",
                    DefaultAllocationPercent = 50
                },
                new ProcessRoleEditorModel
                {
                    Id = leadEngineerRoleId,
                    Key = "lead-engineer",
                    DisplayName = "Lead engineer",
                    Purpose = "Build a small Blazor Web App with visible interactive behavior.",
                    StaffingIntent = "Select a Blazor-capable AI implementation specialist with .NET build, test, run, and browser validation capability.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = buildStepId,
                    Key = "build-blazor-app",
                    Title = "Build Blazor app",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Blazor app delivery request.",
                    OutputContractSummary = "Working Blazor app prepared with validation evidence.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Product owner owns scope, delivery manager owns sequencing, and lead engineer owns implementation.",
                    ExceptionPolicySummary = "Fail when technology context overrides role accountability.",
                    AllowedOperations =
                    [
                        ProcessStepOperation.WriteManagedProcessArtifacts,
                        ProcessStepOperation.MutateProductTarget,
                        ProcessStepOperation.RunValidation,
                        ProcessStepOperation.CaptureRuntimeProof
                    ],
                    OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetMutable,
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = productOwnerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Approver
                        },
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = deliveryManagerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Approver
                        },
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = leadEngineerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildGenericImplementationLaunchPlanningDefinition(Guid projectId)
    {
        var leadEngineerRoleId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Generic implementation launch planning proof process",
            Summary = "Routes a generic implementation role from the project-structure delivery context.",
            ValueStatement = "Launch planning must use project-structure work context instead of only role titles.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "Role matching stays generic and context-aware.",
            ChangeSummary = "Project-structure launch context integration proof.",
            ConstitutionRuleSummary = "Do not ignore the selected work item when staffing generic implementation roles.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = leadEngineerRoleId,
                    Key = "lead-engineer",
                    DisplayName = "Lead engineer",
                    Purpose = "Build the requested application.",
                    StaffingIntent = "Select an AI implementation specialist with build, run, and browser validation capability.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = buildStepId,
                    Key = "build-requested-app",
                    Title = "Build requested app",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Project-structure work item describes the requested app.",
                    OutputContractSummary = "Working app prepared with validation evidence.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Selected AI resource owns implementation.",
                    ExceptionPolicySummary = "Fail when project-structure context is ignored.",
                    AllowedOperations =
                    [
                        ProcessStepOperation.WriteManagedProcessArtifacts,
                        ProcessStepOperation.MutateProductTarget,
                        ProcessStepOperation.RunValidation,
                        ProcessStepOperation.CaptureRuntimeProof
                    ],
                    OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetMutable,
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = leadEngineerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildGenericQaLaunchPlanningDefinition(Guid projectId)
    {
        var qaRoleId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Generic quality launch planning proof process",
            Summary = "Routes a generic QA role from the selected project-structure delivery context.",
            ValueStatement = "Launch planning must staff quality review from the selected work stack.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "QA staffing remains generic and context-aware.",
            ChangeSummary = "Project-structure quality review launch context integration proof.",
            ConstitutionRuleSummary = "Do not route .NET or Blazor quality review to a JavaScript-only reviewer when .NET QA is available.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = qaRoleId,
                    Key = "qa-lead",
                    DisplayName = "QA lead",
                    Purpose = "Review delivered application quality and validation evidence.",
                    StaffingIntent = "Select an AI quality reviewer with build, runtime, and browser validation capability for the selected app stack.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 40
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = reviewStepId,
                    Key = "quality-review",
                    Title = "Review delivered app quality",
                    StepKind = ProcessStepKind.Review,
                    InputContractSummary = "Project-structure work item and implementation evidence describe the requested app.",
                    OutputContractSummary = "Quality disposition prepared with validation evidence.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Selected QA resource owns the quality disposition.",
                    ExceptionPolicySummary = "Fail when project-structure stack context is ignored.",
                    AllowedOperations =
                    [
                        ProcessStepOperation.ReadProcessContext,
                        ProcessStepOperation.ReadProjectStructure,
                        ProcessStepOperation.ReadUpstreamArtifacts,
                        ProcessStepOperation.RunValidation,
                        ProcessStepOperation.CaptureRuntimeProof
                    ],
                    OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetReadOnly,
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = qaRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildJavaScriptArchitectureAndImplementationLaunchPlanningDefinition(Guid projectId)
    {
        var architectRoleId = Guid.NewGuid();
        var leadEngineerRoleId = Guid.NewGuid();
        var architectureStepId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Generic JavaScript delivery launch planning proof process",
            Summary = "Routes architecture and implementation roles from the selected project-structure stack.",
            ValueStatement = "Launch planning must choose specialists from selected work context, not unrelated sibling batch context.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "Role matching stays generic and selected-work aware.",
            ChangeSummary = "JavaScript project-structure launch context integration proof.",
            ConstitutionRuleSummary = "Do not route JavaScript work to .NET specialists when JavaScript specialists are available.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = architectRoleId,
                    Key = "solution-architect",
                    DisplayName = "Solution architect",
                    Purpose = "Review architecture and canonical-model impact for the selected app.",
                    StaffingIntent = "Select the architecture specialist that matches the selected implementation stack.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 50
                },
                new ProcessRoleEditorModel
                {
                    Id = leadEngineerRoleId,
                    Key = "lead-engineer",
                    DisplayName = "Lead engineer",
                    Purpose = "Build the requested application.",
                    StaffingIntent = "Select an AI implementation specialist with build, run, and browser validation capability.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = architectureStepId,
                    Key = "architecture-review",
                    Title = "Review architecture",
                    StepKind = ProcessStepKind.Review,
                    InputContractSummary = "Selected work item describes the requested app.",
                    OutputContractSummary = "Architecture direction recorded.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Selected architecture resource owns architecture guidance.",
                    ExceptionPolicySummary = "Fail when selected work stack is ignored.",
                    AllowedOperations =
                    [
                        ProcessStepOperation.ReadProcessContext,
                        ProcessStepOperation.ReadProjectStructure,
                        ProcessStepOperation.ReadUpstreamArtifacts
                    ],
                    OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetReadOnly,
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = architectRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = buildStepId,
                    Key = "build-requested-app",
                    Title = "Build requested app",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Architecture direction and selected work item.",
                    OutputContractSummary = "Working app prepared with validation evidence.",
                    EvidenceContractSummary = "Launch planning proof only.",
                    DecisionRightsSummary = "Selected AI resource owns implementation.",
                    ExceptionPolicySummary = "Fail when selected work stack is ignored.",
                    AllowedOperations =
                    [
                        ProcessStepOperation.WriteManagedProcessArtifacts,
                        ProcessStepOperation.MutateProductTarget,
                        ProcessStepOperation.RunValidation,
                        ProcessStepOperation.CaptureRuntimeProof
                    ],
                    OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetMutable,
                    TargetLeadHours = 1,
                    CanvasX = 460,
                    CanvasY = 180,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = architectureStepId
                        }
                    ],
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = leadEngineerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projectsService,
        string name,
        string? description = null,
        string? objective = null)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = description ?? $"{name} description",
            Objective = objective ?? $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task SaveAssignmentAsync(
        IProjectPartyIntegrationBridge projectPartyBridge,
        Guid projectId,
        Guid partyId,
        ProjectPartyAssignmentRole role,
        string nodeKey,
        bool isPrimary)
    {
        var result = await projectPartyBridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = role,
            NodeKey = nodeKey,
            AllocationPercent = 100m,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
            Notes = $"Assignment for {role}",
            IsPrimary = isPrimary,
            Source = "integration-tests"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
    }

    private static async Task<Guid> SaveApprovedAiProfileAsync(
        AiAgentService aiAgentService,
        Guid partyId,
        Guid ownerPartyId,
        string capabilityName,
        string capabilityScope,
        string toolAccess,
        string notes,
        bool assignProvider = true)
    {
        var provider = assignProvider
            ? await ResolveDefaultProviderAsync(aiAgentService, partyId)
            : null;
        var profile = await aiAgentService.SaveAgentProfileAsync(new AiAgentProfileEditorModel
        {
            PartyId = partyId,
            ProviderProfileId = provider?.Id,
            DefaultModel = provider?.DefaultModel ?? "scenario-local",
            ExecutionMode = AiExecutionMode.Remote,
            OwnerPartyId = ownerPartyId,
            ValidationStatus = AiValidationStatus.Approved,
            Notes = notes,
            LastChangedBy = "integration-tests",
            Capabilities =
            [
                new AiCapabilityEditorModel
                {
                    Name = capabilityName,
                    Scope = capabilityScope,
                    ToolAccess = toolAccess,
                    Limitations = "Deterministic proof only.",
                    Notes = "Launch-plan proof."
                }
            ]
        });

        Assert.True(profile.IsSuccess, string.Join(" | ", profile.Errors.Select(error => error.Message)));
        return profile.Value;
    }

    private static async Task<AiProviderOptionModel> ResolveDefaultProviderAsync(
        AiAgentService aiAgentService,
        Guid partyId)
    {
        var workspace = await aiAgentService.GetAgentWorkspaceAsync(partyId);
        Assert.NotNull(workspace);

        var provider = workspace!.ProviderOptions
            .Where(item => item.IsEnabled)
            .OrderByDescending(item => !string.IsNullOrWhiteSpace(item.DefaultModel))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        Assert.NotNull(provider);
        return provider!;
    }

    private static ProcessLaunchCandidateViewModel GetSelectedCandidate(
        ProcessLaunchPlanDetails details,
        string roleName)
    {
        var role = Assert.Single(details.Roles, item => string.Equals(item.DisplayName, roleName, StringComparison.Ordinal));
        Assert.True(role.SelectedCandidateId.HasValue);
        return Assert.Single(role.Candidates, item => item.Id == role.SelectedCandidateId.Value);
    }

    private static ProcessLaunchCandidateViewModel GetCandidate(
        ProcessLaunchPlanDetails details,
        string roleName,
        string candidateName)
    {
        var role = Assert.Single(details.Roles, item => string.Equals(item.DisplayName, roleName, StringComparison.Ordinal));
        return Assert.Single(role.Candidates, item => string.Equals(item.DisplayName, candidateName, StringComparison.Ordinal));
    }

    private sealed record LaunchPlanningDefinitionFixture(
        ProcessDefinitionEditorModel Editor,
        string BuilderRoleName,
        string ReviewerRoleName);
}
