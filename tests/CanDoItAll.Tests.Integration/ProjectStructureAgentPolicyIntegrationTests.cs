using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAgentPolicyIntegrationTests
{
    [Fact]
    public async Task Administration_service_builds_setup_guidance_and_project_override_policy()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var administrationService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentAdministrationService>();
        var knowledgeService = scope.ServiceProvider.GetRequiredService<ProjectManagementKnowledgeService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var projectResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Policy Project",
            Description = "Used to validate project overrides.",
            Objective = "Exercise central policy evaluation.",
            CurrentPhase = "Planning"
        });

        Assert.True(projectResult.IsSuccess);

        await administrationService.SaveSettingsAsync(new ProjectStructureAgentWorkspaceSettingsModel
        {
            CentralBaseUrl = "http://main-machine:5032",
            InstallScriptPath = @"tools\Install-CanDoItAllProjectStructureMcp.ps1",
            SetupReadmePath = @"docs\project-structure-mcp-setup.md",
            DefaultAutoApproveMinutes = 10,
            DefaultApprovalRequiredMinutes = 45
        });

        var saveProfileResult = await administrationService.SaveProfileAsync(new ProjectStructureAgentProfileEditorModel
        {
            Name = "Policy Agent",
            Description = "Covers setup guidance and project overrides.",
            CapabilityMask = ProjectStructureAgentCapability.ReadStructure | ProjectStructureAgentCapability.MutateStructure | ProjectStructureAgentCapability.ReadKnowledge,
            AutoApproveMinutes = 10,
            ApprovalRequiredMinutes = 45,
            GenerateNewToken = true,
            ProjectOverrides =
            [
                new ProjectStructureAgentProjectOverrideEditorModel
                {
                    ProjectId = projectResult.Value,
                    CapabilityMask = ProjectStructureAgentCapability.ReadStructure | ProjectStructureAgentCapability.ReadKnowledge,
                    AutoApproveMinutes = 0,
                    ApprovalRequiredMinutes = 0,
                    IsEnabled = true
                }
            ]
        });

        Assert.True(saveProfileResult.IsSuccess);
        var savedProfile = await administrationService.GetProfileAsync(saveProfileResult.Value);
        var setupGuide = await administrationService.BuildSetupGuideAsync(saveProfileResult.Value);
        var authorization = await administrationService.AuthorizeAsync(
            savedProfile.TokenValue,
            ProjectStructureAgentCapability.ReadKnowledge,
            projectResult.Value,
            estimatedMinutes: null,
            enforceMutationApproval: false);
        var guidance = await knowledgeService.QueryAsync(new ProjectManagementKnowledgeQuery(
            Categories: [ProjectManagementKnowledgeCategory.Mission],
            Take: 5));

        Assert.Equal("Policy Agent", authorization.ProfileName);
        Assert.Equal("Project override: Policy Project", authorization.Policy.PolicySource);
        Assert.Contains("http://main-machine:5032", setupGuide.SettingsJson, StringComparison.Ordinal);
        Assert.Contains("Install-CanDoItAllProjectStructureMcp.ps1", setupGuide.PowerShellCommand, StringComparison.Ordinal);
        Assert.Contains(guidance, entry => entry.IsMissionAnchor && entry.Guidance.Contains("surrounding better", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Project_structure_api_requires_estimate_when_mutation_thresholds_are_configured()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var administrationService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentAdministrationService>();
            var summary = Assert.Single(await administrationService.ListProfilesAsync());
            var profile = await administrationService.GetProfileAsync(summary.Id);
            profile.AutoApproveMinutes = 5;
            profile.ApprovalRequiredMinutes = 15;
            profile.GenerateNewToken = false;

            var saveResult = await administrationService.SaveProfileAsync(profile);
            Assert.True(saveResult.IsSuccess);
        }

        var missingEstimateResponse = await host.Client.PostAsJsonAsync(
            "/api/project-structure-mcp/projects",
            new ProjectStructureProjectSaveRequest(
                "Estimate gated project",
                "API policy validation",
                "Mutation should require an estimate header once thresholds are configured.",
                "Planning",
                ProjectStatus.Active));

        Assert.Equal(HttpStatusCode.Forbidden, missingEstimateResponse.StatusCode);
        var missingEstimateError = await missingEstimateResponse.Content.ReadAsStringAsync();
        Assert.Contains("EstimateRequired", missingEstimateError, StringComparison.Ordinal);

        using var allowedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/project-structure-mcp/projects")
        {
            Content = JsonContent.Create(new ProjectStructureProjectSaveRequest(
                "Estimate approved project",
                "API policy validation",
                "Mutation includes a safe estimate.",
                "Planning",
                ProjectStatus.Active))
        };
        allowedRequest.Headers.Add(ProjectStructureAgentHttpHeaders.EstimatedMinutes, "10");

        var allowedResponse = await host.Client.SendAsync(allowedRequest);
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
    }

    [Fact]
    public async Task Project_structure_api_test_host_can_start_and_dispose_repeatedly()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
            var response = await host.Client.GetAsync("/api/project-structure-mcp/projects");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
