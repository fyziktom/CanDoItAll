using System.Diagnostics;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessActiveRunSummaryPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task LoadActiveRunSummariesAsync_reports_many_active_runs_with_timing()
    {
        const int runCount = 12;

        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Active run summary timing {suffix}");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, $"Active Run Manager {suffix}");
        var assignmentResult = await projectPartyBridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });
        Assert.True(assignmentResult.IsSuccess, string.Join(" | ", assignmentResult.Errors.Select(error => error.Message)));

        var roleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, roleId));
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        for (var index = 0; index < runCount; index++)
        {
            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                RunName = $"Active timing run {index + 1:00}",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration timing seed."
            });
            Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));
        }

        var runs = await processesService.ListRunsAsync(saveResult.Value, projectId);
        Assert.Equal(runCount, runs.Count);
        Assert.All(runs, run => Assert.Equal(ProcessRunStatus.Active, run.Status));
        var observedRunId = runs.First().Id;
        await SeedObservableRuntimeHealthAsync(dbContextFactory, observedRunId);
        runs = await processesService.ListRunsAsync(saveResult.Value, projectId);

        var stopwatch = Stopwatch.StartNew();
        var summaries = await runDetailsLoader.LoadActiveRunSummariesAsync(runs);
        stopwatch.Stop();

        output.WriteLine($"LoadActiveRunSummariesAsync elapsed: {stopwatch.ElapsedMilliseconds} ms for {runCount} active runs.");
        output.WriteLine($"Summaries returned: {summaries.Count}.");

        Assert.Equal(runCount, summaries.Count);
        Assert.All(summaries, summary =>
        {
            Assert.Equal(ProcessRunStatus.Active, summary.RunStatus);
            Assert.True(summary.PendingOutboxCount >= 0);
            Assert.True(summary.BlockedOrFailedStepCount >= 0);
        });
        var observedSummary = Assert.Single(summaries, summary => summary.RunId == observedRunId);
        Assert.True(observedSummary.PendingOutboxCount >= 1);
        Assert.Equal(1, observedSummary.DeadLetteredOutboxCount);
        Assert.Equal(1, observedSummary.BlockedOrFailedStepCount);
    }

    private static async Task SeedObservableRuntimeHealthAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.Sequence)
            .FirstAsync();
        stepRun.Status = ProcessStepRunStatus.Blocked;

        var now = DateTimeOffset.UtcNow;
        dbContext.Set<ProcessOutboxRecord>().AddRange(
            new ProcessOutboxRecord
            {
                ProcessRunId = runId,
                CommandKey = "integration-pending-observation",
                PayloadJson = "{}",
                Status = ProcessOutboxRecordStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new ProcessOutboxRecord
            {
                ProcessRunId = runId,
                CommandKey = "integration-deadletter-observation",
                PayloadJson = "{}",
                Status = ProcessOutboxRecordStatus.DeadLettered,
                LastError = "Integration dead-letter proof.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        await dbContext.SaveChangesAsync();
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

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId, Guid roleId)
    {
        var intakeStepId = Guid.NewGuid();
        var deliveryStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Active summary timing process",
            Summary = "Small process used to time active run summary loading.",
            ValueStatement = "Keep concurrent active run observation efficient.",
            CustomerName = "Integration tests",
            OwnerName = "Active Run Manager",
            GovernancePolicySummary = "Runtime observation should not require full detail loads per active run.",
            ChangeSummary = "Initial timing definition.",
            ConstitutionRuleSummary = "Preserve process runtime behavior while timing observation cost.",
            OperatingModeSummary = "Assisted execution with a two-step process.",
            SimulationReadinessSummary = "Safe for local integration timing.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "delivery-manager",
                    DisplayName = "Delivery manager",
                    Purpose = "Own the active run.",
                    StaffingIntent = "Primary manager assignment.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery manager snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture intake",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Timing input.",
                    OutputContractSummary = "Timing intake output.",
                    EvidenceContractSummary = "No artifact required for timing.",
                    DecisionRightsSummary = "Delivery manager owns intake.",
                    ExceptionPolicySummary = "Block on missing timing input.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = deliveryStepId,
                    Key = "delivery",
                    Title = "Complete delivery",
                    StepKind = ProcessStepKind.Work,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            DependsOnStepId = intakeStepId
                        }
                    ],
                    InputContractSummary = "Timing intake output.",
                    OutputContractSummary = "Timing delivery output.",
                    EvidenceContractSummary = "No artifact required for timing.",
                    DecisionRightsSummary = "Delivery manager owns delivery.",
                    ExceptionPolicySummary = "Block on missing timing delivery.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }
}
