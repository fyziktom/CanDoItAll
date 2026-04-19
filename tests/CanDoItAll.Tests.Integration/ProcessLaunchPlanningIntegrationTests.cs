using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
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
            "Build calculator",
            "Create a simple Blazor calculator delivery.",
            "Workspace build",
            "Deterministic builder profile for launch planning validation.");
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            reviewerId,
            managerId,
            "Review calculator",
            "Inspect calculator delivery evidence.",
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
            "Build calculator",
            "Create a simple Blazor calculator delivery.",
            "Workspace build",
            "Deterministic builder profile for substitute validation.");
        await SaveApprovedAiProfileAsync(
            aiAgentService,
            reviewerId,
            substituteId,
            "Review calculator",
            "Inspect calculator delivery evidence.",
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

    private static LaunchPlanningDefinitionFixture BuildLaunchPlanningDefinition(Guid projectId)
    {
        var builderRoleId = Guid.NewGuid();
        var reviewerRoleId = Guid.NewGuid();
        var buildStepId = Guid.NewGuid();

        const string builderRoleName = "Calculator builder agent";
        const string reviewerRoleName = "Calculator reviewer agent";

        return new LaunchPlanningDefinitionFixture(
            new ProcessDefinitionEditorModel
            {
                ProjectId = projectId,
                Name = "Launch planning proof process",
                Summary = "Creates a launch plan for a simple calculator delivery.",
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
                        Key = "calculator-builder-ai",
                        DisplayName = builderRoleName,
                        Purpose = "Generate the calculator delivery.",
                        StaffingIntent = "Technical AI builder.",
                        PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
                        PreferredExecutorKind = "AI agent",
                        DefaultAllocationPercent = 100
                    },
                    new ProcessRoleEditorModel
                    {
                        Id = reviewerRoleId,
                        Key = "calculator-reviewer-ai",
                        DisplayName = reviewerRoleName,
                        Purpose = "Review the calculator delivery.",
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
                        Key = "build-calculator",
                        Title = "Build calculator",
                        StepKind = ProcessStepKind.Start,
                        InputContractSummary = "Simple calculator specification.",
                        OutputContractSummary = "Calculator delivery prepared for review.",
                        EvidenceContractSummary = "Generated assets are captured.",
                        DecisionRightsSummary = "Builder completes deterministic generation.",
                        ExceptionPolicySummary = "Fail when the calculator delivery is missing.",
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
                        Key = "review-calculator",
                        Title = "Review calculator",
                        StepKind = ProcessStepKind.Review,
                        InputContractSummary = "Generated calculator delivery.",
                        OutputContractSummary = "Validated calculator delivery.",
                        EvidenceContractSummary = "Review evidence is captured.",
                        DecisionRightsSummary = "Reviewer confirms calculator readiness.",
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

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
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

    private static async Task SaveApprovedAiProfileAsync(
        AiAgentService aiAgentService,
        Guid partyId,
        Guid ownerPartyId,
        string capabilityName,
        string capabilityScope,
        string toolAccess,
        string notes)
    {
        var profile = await aiAgentService.SaveAgentProfileAsync(new AiAgentProfileEditorModel
        {
            PartyId = partyId,
            DefaultModel = "scenario-local",
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
    }

    private sealed record LaunchPlanningDefinitionFixture(
        ProcessDefinitionEditorModel Editor,
        string BuilderRoleName,
        string ReviewerRoleName);
}
