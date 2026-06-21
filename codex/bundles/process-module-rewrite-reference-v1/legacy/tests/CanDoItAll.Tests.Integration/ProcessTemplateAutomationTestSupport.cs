using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

internal static class ProcessTemplateAutomationTestSupport
{
    private static readonly string[] SoftwareDeliveryPrerequisiteTemplateKeys =
    [
        "dotnet-solution-setup",
        "dotnet-feature-function-implementation",
        "dotnet-development-slice",
        "dotnet-architecture-design-review",
        "dotnet-runtime-command-writeback",
        "dotnet-ui-screenshot-writeback"
    ];

    public static Task<TestApplication> CreateProcessMockEnabledApplicationAsync(TestHarnessOptions? options = null)
    {
        var configurationOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (options?.ConfigurationOverrides is not null)
        {
            foreach (var (key, value) in options.ConfigurationOverrides)
            {
                configurationOverrides[key] = value;
            }
        }

        configurationOverrides[$"{ProcessMockAgentOptions.SectionName}:Enabled"] = "true";

        return TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = options?.TestEnvironment,
            ActiveProfile = options?.ActiveProfile,
            SchemaModules = options?.SchemaModules ?? TestSchemaBootstrapModules.Full,
            ConfigurationOverrides = configurationOverrides,
            ConfigureServices = options?.ConfigureServices
        });
    }

    public static async Task<Guid> CreateProjectAsync(
        ProjectsService projectsService,
        string name,
        string currentPhase)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = currentPhase
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    public static async Task<ProcessTemplateAutomationRunResult> ExecuteTemplateWithProcessMockAgentsAsync(
        IServiceProvider serviceProvider,
        string templateKey,
        Guid projectId,
        string definitionName,
        string launchName,
        string triggerReason,
        IReadOnlyDictionary<string, string> processMockRoleByTemplateRoleKey,
        ProcessProjectStructureContext? projectStructureContext = null,
        TimeSpan? timeout = null)
    {
        var catalogService = serviceProvider.GetRequiredService<ProcessMockAgentCatalogService>();
        var partyDirectoryService = serviceProvider.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = serviceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = serviceProvider.GetRequiredService<ProcessesService>();
        var projectionService = serviceProvider.GetRequiredService<ProcessTemplateProjectionService>();
        var outboxService = serviceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspaceFactory = serviceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var context = await catalogService.EnsureCatalogAsync();
        Assert.NotNull(context);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var managerId = await CreateHumanManagerAsync(
            partyDirectoryService,
            $"{definitionName} Manager {suffix}",
            $"process.template.manager.{suffix}@example.test");
        await SaveAssignmentAsync(projectPartyBridge, projectId, managerId, ProjectPartyAssignmentRole.Manager, "manager", true);

        foreach (var prerequisiteTemplateKey in ResolvePrerequisiteTemplateKeys(templateKey))
        {
            await ImportAndPublishTemplateAsync(
                processesService,
                projectionService,
                prerequisiteTemplateKey,
                projectId);
        }

        var envelope = projectionService.GetProjectedEnvelope(templateKey, projectId, definitionName);
        var importResult = await processesService.ImportAsync(envelope);
        Assert.True(importResult.IsSuccess, string.Join(" | ", importResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(importResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = importResult.Value,
            ProjectId = projectId,
            LaunchName = launchName,
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = triggerReason,
            ProjectStructureContext = projectStructureContext,
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        await SelectProcessMockLaunchCandidatesAsync(
            processesService,
            launchResult.Value,
            context!,
            processMockRoleByTemplateRoleKey);

        var submitResult = await processesService.SubmitLaunchPlanForApprovalAsync(launchResult.Value, "integration-tests");
        Assert.True(submitResult.IsSuccess, string.Join(" | ", submitResult.Errors.Select(error => error.Message)));

        var approveResult = await processesService.DecideLaunchPlanApprovalAsync(new ProcessLaunchApprovalDecisionRequest
        {
            LaunchPlanId = launchResult.Value,
            Status = ProcessLaunchApprovalStatus.Approved,
            ResolutionSummary = $"Manager approved deterministic process-mock execution for {definitionName}.",
            DecidedBy = "integration-tests"
        });
        Assert.True(approveResult.IsSuccess, string.Join(" | ", approveResult.Errors.Select(error => error.Message)));

        var executeResult = await processesService.ExecuteLaunchPlanAsync(new ProcessLaunchExecutionRequest
        {
            LaunchPlanId = launchResult.Value,
            RequestedBy = "integration-tests"
        });
        Assert.True(executeResult.IsSuccess, string.Join(" | ", executeResult.Errors.Select(error => error.Message)));
        var runId = executeResult.Value;

        await DrainProcessOutboxUntilRunSettledAsync(
            outboxService,
            dbContextFactory,
            processesService,
            workspaceService,
            runId,
            timeout ?? TimeSpan.FromSeconds(90));

        var run = await processesService.GetRunAsync(runId);
        Assert.NotNull(run);
        Assert.Equal(ProcessRunStatus.Completed, run!.Status);

        var stepRuns = await processesService.ListStepRunsAsync(runId);
        var artifactRecords = await ListRunArtifactRecordsAsync(dbContextFactory, runId);
        var outboxRecords = await ListRunOutboxRecordsAsync(dbContextFactory, runId);
        var executionRuns = (await workspaceService.ListExecutionRunsAsync(new ExecutionRunQuery(
                SourceKind: ProcessMockAgentCatalog.ProcessSourceKind,
                Take: 100,
                ProcessRunId: runId.ToString("D"))))
            .OrderBy(executionRun => executionRun.CreatedAtUtc)
            .ToList();

        Assert.NotEmpty(outboxRecords);
        Assert.All(outboxRecords, outbox => Assert.Equal(ProcessOutboxRecordStatus.Completed, outbox.Status));
        Assert.NotEmpty(executionRuns);
        Assert.All(executionRuns, executionRun =>
        {
            Assert.Equal(ProcessMockAgentCatalog.ProcessSourceKind, executionRun.SourceKind);
            Assert.Equal(runId.ToString("D"), executionRun.ProcessRunId);
            Assert.True(Guid.TryParse(executionRun.ProcessStepId, out var stepRunId));
            Assert.Contains(stepRuns, stepRun => stepRun.Id == stepRunId);
            Assert.Equal(ProcessMockAgentCatalog.ProviderName, executionRun.ProviderName);
            Assert.Equal(ProcessMockAgentCatalog.Model, executionRun.Model);
            Assert.Equal(ExecutionState.Completed, executionRun.State);
            Assert.Equal(RunOutcome.Succeeded, executionRun.Outcome);
        });

        return new ProcessTemplateAutomationRunResult(
            runId,
            run,
            stepRuns,
            artifactRecords,
            outboxRecords,
            executionRuns);
    }

    private static IReadOnlyList<string> ResolvePrerequisiteTemplateKeys(string templateKey)
    {
        return string.Equals(templateKey, ProcessTemplateCatalogInventory.SoftwareDeliveryTemplateKey, StringComparison.OrdinalIgnoreCase)
            ? SoftwareDeliveryPrerequisiteTemplateKeys
            : [];
    }

    private static async Task ImportAndPublishTemplateAsync(
        ProcessesService processesService,
        ProcessTemplateProjectionService projectionService,
        string templateKey,
        Guid projectId)
    {
        var importResult = await processesService.ImportAsync(projectionService.GetProjectedEnvelope(templateKey, projectId));
        Assert.True(importResult.IsSuccess, string.Join(" | ", importResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(importResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));
    }

    private static async Task SelectProcessMockLaunchCandidatesAsync(
        ProcessesService processesService,
        Guid launchPlanId,
        ProcessMockAgentCatalogContext context,
        IReadOnlyDictionary<string, string> processMockRoleByTemplateRoleKey)
    {
        var launchDetails = await processesService.GetLaunchPlanAsync(launchPlanId);
        Assert.NotNull(launchDetails);

        var missingRequiredRoleKeys = launchDetails!.Roles
            .Where(role => role.IsRequired)
            .Where(role => !processMockRoleByTemplateRoleKey.ContainsKey(role.RoleKey))
            .Select(role => role.RoleKey)
            .OrderBy(roleKey => roleKey, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            missingRequiredRoleKeys.Length == 0,
            $"Process-mock role mapping is missing required launch roles: {string.Join(", ", missingRequiredRoleKeys)}.");

        foreach (var (templateRoleKey, processMockRoleKey) in processMockRoleByTemplateRoleKey)
        {
            var launchRole = Assert.Single(
                launchDetails.Roles,
                role => string.Equals(role.RoleKey, templateRoleKey, StringComparison.Ordinal));
            Assert.True(
                context.AgentIdsByRoleKey.TryGetValue(processMockRoleKey, out var technicalAgentId),
                $"Process-mock role '{processMockRoleKey}' was not seeded.");

            var selectResult = await processesService.SelectLaunchTechnicalAgentAsync(new ProcessLaunchTechnicalAgentSelectionRequest
            {
                LaunchPlanId = launchPlanId,
                LaunchPlanRoleId = launchRole.Id,
                TechnicalAgentId = technicalAgentId
            });
            Assert.True(selectResult.IsSuccess, string.Join(" | ", selectResult.Errors.Select(error => error.Message)));
        }
    }

    private static async Task DrainProcessOutboxUntilRunSettledAsync(
        ProcessOutboxService outboxService,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProcessesService processesService,
        IAgentFrameworkWorkspaceService workspaceService,
        Guid runId,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await outboxService.ProcessPendingAsync(20, TimeSpan.FromMinutes(1));

            var outboxRecords = await ListRunOutboxRecordsAsync(dbContextFactory, runId);
            var deadLetteredRecord = outboxRecords.FirstOrDefault(item => item.Status == ProcessOutboxRecordStatus.DeadLettered);
            if (deadLetteredRecord is not null)
            {
                Assert.Fail(await BuildRunDiagnosticsAsync(
                    dbContextFactory,
                    processesService,
                    workspaceService,
                    runId,
                    $"Run outbox record {deadLetteredRecord.Id:D} dead-lettered. LastError={deadLetteredRecord.LastError}"));
            }

            var run = await processesService.GetRunAsync(runId);
            if (run?.Status == ProcessRunStatus.Completed &&
                outboxRecords.All(item => item.Status == ProcessOutboxRecordStatus.Completed))
            {
                return;
            }

            if (run?.Status == ProcessRunStatus.Failed)
            {
                Assert.Fail(await BuildRunDiagnosticsAsync(
                    dbContextFactory,
                    processesService,
                    workspaceService,
                    runId,
                    $"Process template automation run {runId:D} failed before completion."));
            }

            await Task.Delay(50);
        }

        Assert.Fail(await BuildRunDiagnosticsAsync(
            dbContextFactory,
            processesService,
            workspaceService,
            runId,
            $"Timed out waiting for process template automation run {runId:D} to complete."));
    }

    private static async Task<IReadOnlyList<ProcessOutboxRecord>> ListRunOutboxRecordsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var records = await dbContext.Set<ProcessOutboxRecord>()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync();
        return records
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessArtifactRecord>> ListRunArtifactRecordsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var records = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync();
        return records
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private static async Task<string> BuildRunDiagnosticsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProcessesService processesService,
        IAgentFrameworkWorkspaceService workspaceService,
        Guid runId,
        string reason)
    {
        var builder = new StringBuilder()
            .AppendLine(reason);

        var run = await processesService.GetRunAsync(runId);
        builder.Append("Run status: ")
            .AppendLine(run?.Status.ToString() ?? "missing");

        var stepRuns = await processesService.ListStepRunsAsync(runId);
        builder.AppendLine("Steps:");
        foreach (var stepRun in stepRuns.OrderBy(stepRun => stepRun.Sequence))
        {
            builder.Append("  ")
                .Append(stepRun.Sequence)
                .Append(": ")
                .Append(stepRun.Title)
                .Append(" / ")
                .Append(stepRun.Status)
                .Append(" / branch=")
                .Append(stepRun.SelectedBranchOutcomeTitle);
            if (!string.IsNullOrWhiteSpace(stepRun.DecisionSummary))
            {
                builder.Append(" / decision=")
                    .Append(stepRun.DecisionSummary);
            }

            if (!string.IsNullOrWhiteSpace(stepRun.BlockedReason))
            {
                builder.Append(" / blocked=")
                    .Append(stepRun.BlockedReason);
            }

            if (!string.IsNullOrWhiteSpace(stepRun.RefusalReason))
            {
                builder.Append(" / refusal=")
                    .Append(stepRun.RefusalReason);
            }

            if (!string.IsNullOrWhiteSpace(stepRun.ExceptionSummary))
            {
                builder.Append(" / exception=")
                    .Append(stepRun.ExceptionSummary);
            }

            builder.AppendLine();
        }

        var outboxRecords = await ListRunOutboxRecordsAsync(dbContextFactory, runId);
        builder.AppendLine("Outbox:");
        foreach (var outbox in outboxRecords)
        {
            builder.Append("  ")
                .Append(outbox.CommandKey)
                .Append(" / ")
                .Append(outbox.Status)
                .Append(" / attempts=")
                .Append(outbox.AttemptCount)
                .Append(" / lastError=")
                .AppendLine(outbox.LastError);
        }

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var childRuns = await dbContext.Set<ProcessRun>()
                .AsNoTracking()
                .Where(item => item.RootRunId == runId && item.Id != runId)
                .OrderBy(item => item.HierarchyDepth)
                .ThenBy(item => item.CreatedAtUtc)
                .ToListAsync();
            builder.AppendLine("Child runs:");
            foreach (var childRun in childRuns)
            {
                builder.Append("  ")
                    .Append(childRun.Name)
                    .Append(" / ")
                    .Append(childRun.Status)
                    .Append(" / id=")
                    .Append(childRun.Id)
                    .Append(" / parentStep=")
                    .AppendLine(childRun.ParentStepRunId?.ToString("D") ?? string.Empty);

                var childSteps = await processesService.ListStepRunsAsync(childRun.Id);
                foreach (var childStep in childSteps.OrderBy(step => step.Sequence))
                {
                    builder.Append("    ")
                        .Append(childStep.Sequence)
                        .Append(": ")
                        .Append(childStep.Title)
                        .Append(" / ")
                        .Append(childStep.Status)
                        .Append(" / executor=")
                        .Append(childStep.CurrentExecutorName)
                        .Append(" / branch=")
                        .Append(childStep.SelectedBranchOutcomeTitle);
                    if (!string.IsNullOrWhiteSpace(childStep.BlockedReason))
                    {
                        builder.Append(" / blocked=")
                            .Append(childStep.BlockedReason);
                    }

                    if (!string.IsNullOrWhiteSpace(childStep.ExceptionSummary))
                    {
                        builder.Append(" / exception=")
                            .Append(childStep.ExceptionSummary);
                    }

                    builder.AppendLine();
                }
            }
        }

        var executionRuns = await workspaceService.ListExecutionRunsAsync(new ExecutionRunQuery(
            Take: 50,
            ProcessRunId: runId.ToString("D")));
        builder.AppendLine("Execution runs:");
        foreach (var executionRun in executionRuns.OrderBy(executionRun => executionRun.CreatedAtUtc))
        {
            builder.Append("  ")
                .Append(executionRun.Title)
                .Append(" / ")
                .Append(executionRun.ProviderName)
                .Append(" / ")
                .Append(executionRun.Model)
                .Append(" / ")
                .Append(executionRun.State)
                .Append(" / ")
                .Append(executionRun.Outcome)
                .Append(" / step=")
                .Append(executionRun.ProcessStepId)
                .Append(" / result=")
                .AppendLine(executionRun.ResultSummary);
        }

        return builder.ToString();
    }

    private static async Task<Guid> CreateHumanManagerAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
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
}

internal sealed record ProcessTemplateAutomationRunResult(
    Guid RunId,
    ProcessRunListItem Run,
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessArtifactRecord> ArtifactRecords,
    IReadOnlyList<ProcessOutboxRecord> OutboxRecords,
    IReadOnlyList<ExecutionRunRecord> ExecutionRuns);
