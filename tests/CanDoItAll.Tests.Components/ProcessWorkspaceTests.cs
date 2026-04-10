using Bunit;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessWorkspaceTests
{
    [Fact]
    public async Task Global_workspace_loads_persisted_definitions_on_the_first_render_without_query_parameters()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, "Global processes workspace project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<ProcessWorkspace>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Process management", cut.Markup);
            Assert.Contains("Workspace-visible process", cut.Markup);
        });
    }

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Workspace-visible process",
            Summary = "Ensures the global processes workspace loads on first render.",
            ValueStatement = "Show persisted definitions without query-string routing.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Persist the current workspace model without token dependencies.",
            ChangeSummary = "Initial component-test definition.",
            ConstitutionRuleSummary = "The first render must hydrate persisted definitions.",
            OperatingModeSummary = "Authoring-first workspace validation.",
            SimulationReadinessSummary = "Safe for component validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "workspace-owner",
                    DisplayName = "Workspace owner",
                    Purpose = "Own the workspace verification flow.",
                    StaffingIntent = "Single accountable owner for the smoke definition.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Workspace owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "workspace-intake",
                    Title = "Capture workspace intake",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Definition metadata.",
                    OutputContractSummary = "Loaded workspace definition.",
                    EvidenceContractSummary = "Visible definition list entry.",
                    DecisionRightsSummary = "Workspace owner confirms the first render.",
                    ExceptionPolicySummary = "Escalate when the list remains empty.",
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Keep the workspace owner assigned."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "workspace-review",
                    Title = "Review rendered workspace",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Loaded workspace definition.",
                    OutputContractSummary = "Rendered process workspace state.",
                    EvidenceContractSummary = "Process name visible in the first render.",
                    DecisionRightsSummary = "Workspace owner confirms visibility.",
                    ExceptionPolicySummary = "Fail when the page does not hydrate.",
                    TargetLeadHours = 1,
                    DependsOnStepId = intakeStepId,
                    CanvasX = 420,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Keep the workspace owner assigned."
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

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
