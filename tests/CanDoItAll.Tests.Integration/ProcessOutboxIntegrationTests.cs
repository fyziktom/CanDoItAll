using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessOutboxIntegrationTests
{
    [Fact]
    public async Task SaveAsync_remains_successful_when_activity_dispatch_requires_retry()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process outbox save");
        harness.Failures.FailDefinitionSaveActivityOnce = true;

        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        var saveOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "save-definition", saveResult.Value, null);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, saveOutboxRecord.Status);
        Assert.Equal(1, saveOutboxRecord.AttemptCount);
        Assert.True(await SearchDocumentExistsAsync(dbContextFactory, "process-definition", saveResult.Value.ToString()));
        Assert.Equal(0, await CountActivityAsync(dbContextFactory, "create-definition", saveResult.Value));

        await ForceNextAttemptDueAsync(dbContextFactory, saveOutboxRecord.Id);
        Assert.Equal(1, await outboxService.ProcessPendingAsync());

        saveOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "save-definition", saveResult.Value, null);
        Assert.Equal(ProcessOutboxRecordStatus.Completed, saveOutboxRecord.Status);
        Assert.Equal(2, saveOutboxRecord.AttemptCount);
        Assert.Equal(1, await CountActivityAsync(dbContextFactory, "create-definition", saveResult.Value));
        Assert.True(await SearchDocumentExistsAsync(dbContextFactory, "process-definition", saveResult.Value.ToString()));
    }

    [Fact]
    public async Task PublishAsync_remains_successful_when_activity_dispatch_requires_retry()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process outbox publish");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        harness.Failures.FailDefinitionPublishActivityOnce = true;

        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(publishResult.IsSuccess);
        var publishOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "publish-definition", saveResult.Value, null);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, publishOutboxRecord.Status);
        Assert.Equal(1, publishOutboxRecord.AttemptCount);
        Assert.Equal(0, await CountActivityAsync(dbContextFactory, "publish-definition", saveResult.Value));
        Assert.True(await HasPublishedVersionAsync(dbContextFactory, saveResult.Value));

        await ForceNextAttemptDueAsync(dbContextFactory, publishOutboxRecord.Id);
        Assert.Equal(1, await outboxService.ProcessPendingAsync());

        publishOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "publish-definition", saveResult.Value, null);
        Assert.Equal(ProcessOutboxRecordStatus.Completed, publishOutboxRecord.Status);
        Assert.Equal(2, publishOutboxRecord.AttemptCount);
        Assert.Equal(1, await CountActivityAsync(dbContextFactory, "publish-definition", saveResult.Value));
        Assert.True(await HasPublishedVersionAsync(dbContextFactory, saveResult.Value));
    }

    [Fact]
    public async Task DeleteAsync_remains_successful_when_search_dispatch_requires_retry()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process outbox delete");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True(await SearchDocumentExistsAsync(dbContextFactory, "process-definition", saveResult.Value.ToString()));
        harness.Failures.FailDefinitionDeleteSearchOnce = true;

        await processesService.DeleteAsync(saveResult.Value);

        Assert.False(await DefinitionExistsAsync(dbContextFactory, saveResult.Value));
        var deleteOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "delete-definition", saveResult.Value, null);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, deleteOutboxRecord.Status);
        Assert.Equal(1, deleteOutboxRecord.AttemptCount);
        Assert.True(await SearchDocumentExistsAsync(dbContextFactory, "process-definition", saveResult.Value.ToString()));

        await ForceNextAttemptDueAsync(dbContextFactory, deleteOutboxRecord.Id);
        Assert.Equal(1, await outboxService.ProcessPendingAsync());

        deleteOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "delete-definition", saveResult.Value, null);
        Assert.Equal(ProcessOutboxRecordStatus.Completed, deleteOutboxRecord.Status);
        Assert.Equal(2, deleteOutboxRecord.AttemptCount);
        Assert.False(await SearchDocumentExistsAsync(dbContextFactory, "process-definition", saveResult.Value.ToString()));
    }

    [Fact]
    public async Task StartRunAsync_remains_successful_when_activity_dispatch_requires_retry()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync();
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process outbox run");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);
        harness.Failures.FailStartRunActivityOnce = true;

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Process outbox run validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate durable process outbox"
        });

        Assert.True(runResult.IsSuccess);
        Assert.True(await RunExistsAsync(dbContextFactory, runResult.Value));
        var startRunOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "start-run", saveResult.Value, runResult.Value);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, startRunOutboxRecord.Status);
        Assert.Equal(1, startRunOutboxRecord.AttemptCount);
        Assert.Equal(0, await CountActivityAsync(dbContextFactory, "start-run", runResult.Value));

        await ForceNextAttemptDueAsync(dbContextFactory, startRunOutboxRecord.Id);
        Assert.True(await outboxService.ProcessPendingAsync() >= 1);

        startRunOutboxRecord = await GetOutboxRecordAsync(dbContextFactory, "start-run", saveResult.Value, runResult.Value);
        Assert.Equal(ProcessOutboxRecordStatus.Completed, startRunOutboxRecord.Status);
        Assert.Equal(2, startRunOutboxRecord.AttemptCount);
        Assert.Equal(1, await CountActivityAsync(dbContextFactory, "start-run", runResult.Value));
        Assert.True(await RunExistsAsync(dbContextFactory, runResult.Value));
    }

    [Fact]
    public async Task StartRunAsync_enqueues_automation_dispatch_for_durable_processing()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process outbox deferred automation start");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Deferred automation dispatch start",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate durable automation dispatch"
        });

        Assert.True(runResult.IsSuccess);

        var automationDispatchRecords = await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value);
        var automationDispatchRecord = Assert.Single(automationDispatchRecords);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, automationDispatchRecord.Status);
        Assert.Equal(0, automationDispatchRecord.AttemptCount);
        Assert.Equal(0, harness.AutomationDispatch.CallCount);

        Assert.Equal(1, await outboxService.ProcessPendingAsync());

        automationDispatchRecords = await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value);
        automationDispatchRecord = Assert.Single(automationDispatchRecords);
        Assert.Equal(ProcessOutboxRecordStatus.Completed, automationDispatchRecord.Status);
        Assert.Equal(1, automationDispatchRecord.AttemptCount);
        Assert.Equal(1, harness.AutomationDispatch.CallCount);
    }

    [Fact]
    public async Task Parallel_ProcessPendingAsync_calls_do_not_dispatch_same_automation_record_twice()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        harness.AutomationDispatch.HoldDispatch = true;

        var projectId = await CreateProjectAsync(projectsService, "Process outbox parallel automation claim");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Parallel automation dispatch claim",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate concurrent process outbox claim"
        });

        Assert.True(runResult.IsSuccess);

        var firstDrain = outboxService.ProcessPendingAsync(1, TimeSpan.FromMinutes(1));
        await harness.AutomationDispatch.FirstDispatchStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var secondDrain = outboxService.ProcessPendingAsync(1, TimeSpan.FromMinutes(1));

        Assert.Equal(0, await secondDrain);
        Assert.Equal(1, harness.AutomationDispatch.CallCount);

        harness.AutomationDispatch.ReleaseDispatch();

        Assert.Equal(1, await firstDrain);
        var automationDispatchRecord = Assert.Single(await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value));
        Assert.Equal(ProcessOutboxRecordStatus.Completed, automationDispatchRecord.Status);
        Assert.Equal(1, automationDispatchRecord.AttemptCount);
        Assert.Equal(1, harness.AutomationDispatch.CallCount);
    }

    [Fact]
    public async Task ProcessPendingAsync_can_skip_persisted_automation_dispatches_created_before_runtime_start()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process outbox persisted automation skip");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Persisted automation dispatch should not resume",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate runtime startup dispatch boundary"
        });

        Assert.True(runResult.IsSuccess);

        var cutoff = DateTimeOffset.UtcNow;
        await MoveAutomationDispatchCreatedAtAsync(dbContextFactory, runResult.Value, cutoff.AddMinutes(-5));

        Assert.Equal(0, await outboxService.ProcessPendingAsync(
            minimumAutomationDispatchCreatedAtUtc: cutoff));
        Assert.Equal(0, harness.AutomationDispatch.CallCount);

        var automationDispatchRecord = Assert.Single(await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value));
        Assert.Equal(ProcessOutboxRecordStatus.Pending, automationDispatchRecord.Status);
        Assert.Equal(0, automationDispatchRecord.AttemptCount);

        Assert.Equal(1, await outboxService.ProcessPendingAsync());
        Assert.Equal(1, harness.AutomationDispatch.CallCount);
    }

    [Fact]
    public async Task TransitionStepAsync_leaves_automation_dispatch_for_durable_worker()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process outbox deferred automation transition");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Deferred automation dispatch transition",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate deferred transition dispatch"
        });

        Assert.True(runResult.IsSuccess);

        Assert.Equal(1, await outboxService.ProcessPendingAsync());

        Assert.Equal(1, harness.AutomationDispatch.CallCount);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start the intake work.",
            DecidedBy = "integration-tests"
        });

        Assert.True(transitionResult.IsSuccess);

        var automationDispatchRecords = await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value);
        Assert.Equal(2, automationDispatchRecords.Count);
        var latestDispatchRecord = automationDispatchRecords[^1];
        Assert.Equal(ProcessOutboxRecordStatus.Pending, latestDispatchRecord.Status);
        Assert.Equal(0, latestDispatchRecord.AttemptCount);
        Assert.Equal(1, harness.AutomationDispatch.CallCount);

        Assert.Equal(1, await outboxService.ProcessPendingAsync());

        automationDispatchRecords = await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value);
        latestDispatchRecord = automationDispatchRecords[^1];
        Assert.Equal(ProcessOutboxRecordStatus.Completed, latestDispatchRecord.Status);
        Assert.Equal(1, latestDispatchRecord.AttemptCount);
        Assert.Equal(2, harness.AutomationDispatch.CallCount);
    }

    [Fact]
    public async Task Automation_dispatch_lease_prevents_parallel_reclaim_during_long_agent_work()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        harness.AutomationDispatch.HoldDispatch = true;

        var projectId = await CreateProjectAsync(projectsService, "Process outbox long automation lease");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Long automation dispatch lease",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate long automation dispatch lease"
        });

        Assert.True(runResult.IsSuccess);

        var firstDrain = outboxService.ProcessPendingAsync(1, TimeSpan.FromMinutes(1));
        await harness.AutomationDispatch.FirstDispatchStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var automationDispatchRecord = Assert.Single(await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value));
        Assert.Equal(ProcessOutboxRecordStatus.Pending, automationDispatchRecord.Status);
        Assert.Equal(1, automationDispatchRecord.AttemptCount);
        Assert.True(automationDispatchRecord.LeaseExpiresAtUtc >= DateTimeOffset.UtcNow.AddMinutes(20));
        Assert.Equal(1, harness.AutomationDispatch.CallCount);

        await WaitForAsync(
            async () =>
            {
                var records = await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value);
                var record = Assert.Single(records);
                return record.Status == ProcessOutboxRecordStatus.Pending &&
                       record.AttemptCount == 1 &&
                       record.UpdatedAtUtc > automationDispatchRecord.UpdatedAtUtc &&
                       record.LeaseExpiresAtUtc > automationDispatchRecord.LeaseExpiresAtUtc;
            },
            TimeSpan.FromSeconds(12));

        Assert.Equal(0, await outboxService.ProcessPendingAsync(1, TimeSpan.FromMinutes(1)));
        Assert.Equal(1, harness.AutomationDispatch.CallCount);

        harness.AutomationDispatch.ReleaseDispatch();
        Assert.Equal(1, await firstDrain);
        await WaitForAsync(
            async () =>
            {
                var records = await ListAutomationDispatchRecordsAsync(dbContextFactory, runResult.Value);
                return records.Count == 1 &&
                       records[0].Status == ProcessOutboxRecordStatus.Completed &&
                       records[0].AttemptCount == 1;
            },
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecoverActiveRunsAsync_queues_dispatches_without_running_agents_inline()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var recoveryService = scope.ServiceProvider.GetRequiredService<ProcessRunRecoveryService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process recovery deferred automation");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var firstRunId = await StartDeferredAutomationRunAsync(processesService, saveResult.Value, projectId, "Recovery deferred automation A");
        var secondRunId = await StartDeferredAutomationRunAsync(processesService, saveResult.Value, projectId, "Recovery deferred automation B");

        await AssignFirstStepExecutorAsync(dbContextFactory, firstRunId, Guid.NewGuid());
        await AssignFirstStepExecutorAsync(dbContextFactory, secondRunId, Guid.NewGuid());
        await CompleteAutomationDispatchesAsync(dbContextFactory, firstRunId, secondRunId);

        Assert.Equal(0, await CountPendingAutomationDispatchesAsync(dbContextFactory, firstRunId, secondRunId));
        Assert.Equal(2, await CountRecoveryCandidateStepsAsync(dbContextFactory, firstRunId, secondRunId));
        Assert.Equal(0, harness.AutomationDispatch.CallCount);

        Assert.Equal(2, await recoveryService.RecoverActiveRunsAsync());
        Assert.Equal(0, harness.AutomationDispatch.CallCount);

        var firstRunDispatches = await ListAutomationDispatchRecordsAsync(dbContextFactory, firstRunId);
        var secondRunDispatches = await ListAutomationDispatchRecordsAsync(dbContextFactory, secondRunId);
        Assert.Equal(2, firstRunDispatches.Count);
        Assert.Equal(2, secondRunDispatches.Count);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, firstRunDispatches[^1].Status);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, secondRunDispatches[^1].Status);
        Assert.Equal(0, firstRunDispatches[^1].AttemptCount);
        Assert.Equal(0, secondRunDispatches[^1].AttemptCount);

        Assert.Equal(0, await recoveryService.RecoverActiveRunsAsync());
        Assert.Equal(0, harness.AutomationDispatch.CallCount);
    }

    [Fact]
    public async Task RecoverActiveRunsAsync_queues_reopened_inprogress_step_inside_failed_run()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var recoveryService = scope.ServiceProvider.GetRequiredService<ProcessRunRecoveryService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process recovery failed run reopened step");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runId = await StartDeferredAutomationRunAsync(processesService, saveResult.Value, projectId, "Recovery failed run reopened step");
        await AssignFirstStepExecutorAsync(dbContextFactory, runId, Guid.NewGuid());
        await CompleteAutomationDispatchesAsync(dbContextFactory, runId);
        await MarkRunFailedWithInProgressFirstStepAsync(dbContextFactory, runId);

        Assert.Equal(0, await CountPendingAutomationDispatchesAsync(dbContextFactory, runId));

        Assert.Equal(1, await recoveryService.RecoverActiveRunsAsync());
        Assert.Equal(0, harness.AutomationDispatch.CallCount);

        var dispatches = await ListAutomationDispatchRecordsAsync(dbContextFactory, runId);
        Assert.Equal(2, dispatches.Count);
        Assert.Equal(ProcessOutboxRecordStatus.Pending, dispatches[^1].Status);
        Assert.Equal(0, dispatches[^1].AttemptCount);
    }

    [Fact]
    public async Task RecoverActiveRunsAsync_releases_stranded_startup_automation_dispatch_leases()
    {
        await using var harness = await ProcessOutboxHarness.CreateAsync(trackAutomationDispatch: true);
        await using var scope = harness.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var recoveryService = scope.ServiceProvider.GetRequiredService<ProcessRunRecoveryService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process recovery stranded lease");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runId = await StartDeferredAutomationRunAsync(processesService, saveResult.Value, projectId, "Recovery stranded automation lease");
        await AssignFirstStepExecutorAsync(dbContextFactory, runId, Guid.NewGuid());

        var automationDispatchRecord = Assert.Single(await ListAutomationDispatchRecordsAsync(dbContextFactory, runId));
        await LeaseAutomationDispatchAsync(dbContextFactory, automationDispatchRecord.Id, DateTimeOffset.UtcNow.AddMinutes(30));

        Assert.Equal(0, await outboxService.ProcessPendingAsync(1, TimeSpan.FromMinutes(1)));
        Assert.Equal(0, harness.AutomationDispatch.CallCount);

        Assert.Equal(0, await recoveryService.RecoverActiveRunsAsync(reclaimStrandedAutomationDispatchLeases: true));

        automationDispatchRecord = Assert.Single(await ListAutomationDispatchRecordsAsync(dbContextFactory, runId));
        Assert.Equal(ProcessOutboxRecordStatus.Pending, automationDispatchRecord.Status);
        Assert.Equal(0, automationDispatchRecord.AttemptCount);
        Assert.Equal(string.Empty, automationDispatchRecord.LeaseToken);
        Assert.Null(automationDispatchRecord.LeaseExpiresAtUtc);

        Assert.Equal(1, await outboxService.ProcessPendingAsync(1, TimeSpan.FromMinutes(1)));
        Assert.Equal(1, harness.AutomationDispatch.CallCount);
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

    private static async Task<Guid> StartDeferredAutomationRunAsync(
        ProcessesService processesService,
        Guid processDefinitionId,
        Guid projectId,
        string runName)
    {
        var result = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = processDefinitionId,
            ProjectId = projectId,
            RunName = runName,
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate deferred recovery dispatch"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task AssignFirstStepExecutorAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId,
        Guid executorPartyId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.Sequence)
            .FirstAsync();
        stepRun.CurrentExecutorPartyId = executorPartyId;
        stepRun.CurrentExecutorName = "Automation test agent";
        await dbContext.SaveChangesAsync();
    }

    private static async Task CompleteAutomationDispatchesAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        params Guid[] runIds)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;
        var records = await dbContext.Set<ProcessOutboxRecord>()
            .Where(item =>
                item.ProcessRunId.HasValue &&
                runIds.Contains(item.ProcessRunId.Value) &&
                item.CommandKey == ProcessOutboxService.AutomationDispatchCommandKey)
            .ToListAsync();
        foreach (var record in records)
        {
            record.Status = ProcessOutboxRecordStatus.Completed;
            record.CompletedAtUtc = now;
            record.UpdatedAtUtc = now;
            record.LeaseToken = string.Empty;
            record.LeaseExpiresAtUtc = null;
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task MarkRunFailedWithInProgressFirstStepAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;
        var run = await dbContext.Set<ProcessRun>()
            .SingleAsync(item => item.Id == runId);
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.Sequence)
            .FirstAsync();

        run.Status = ProcessRunStatus.Failed;
        run.CompletedAtUtc = now.AddMinutes(-1);
        run.UpdatedAtUtc = now;
        stepRun.Status = ProcessStepRunStatus.InProgress;
        stepRun.StartedAtUtc = now.AddMinutes(-15);
        stepRun.CompletedAtUtc = null;
        await dbContext.SaveChangesAsync();
    }

    private static async Task LeaseAutomationDispatchAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid outboxId,
        DateTimeOffset leaseExpiresAtUtc)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var record = await dbContext.Set<ProcessOutboxRecord>()
            .SingleAsync(item => item.Id == outboxId);
        record.LeaseToken = Guid.NewGuid().ToString("N");
        record.LeaseExpiresAtUtc = leaseExpiresAtUtc;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> CountPendingAutomationDispatchesAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        params Guid[] runIds)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProcessOutboxRecord>()
            .CountAsync(item =>
                item.ProcessRunId.HasValue &&
                runIds.Contains(item.ProcessRunId.Value) &&
                item.CommandKey == ProcessOutboxService.AutomationDispatchCommandKey &&
                item.Status == ProcessOutboxRecordStatus.Pending);
    }

    private static async Task<int> CountRecoveryCandidateStepsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        params Guid[] runIds)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProcessStepRun>()
            .CountAsync(item =>
                runIds.Contains(item.ProcessRunId) &&
                item.CurrentExecutorPartyId.HasValue &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress));
    }

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();
        var approvalStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Process outbox definition",
            Summary = "Validates durable side effects for process commands.",
            ValueStatement = "Keep process command semantics stable when side effects retry.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Activity and search dispatch must not rewrite command success.",
            ChangeSummary = "Initial process outbox definition.",
            ConstitutionRuleSummary = "Published versions remain immutable.",
            OperatingModeSummary = "Assisted execution with durable side effects.",
            SimulationReadinessSummary = "Safe for retry-oriented integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "process-owner",
                    DisplayName = "Process owner",
                    Purpose = "Own the definition and runtime flow.",
                    StaffingIntent = "Primary governance owner.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Process owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "capture-intake",
                    Title = "Capture intake",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 160,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = approvalStepId,
                    Key = "approve-outcome",
                    Title = "Approve outcome",
                    StepKind = ProcessStepKind.Approval,
                    RequiresApproval = true,
                    TargetLeadHours = 1,
                    CanvasX = 460,
                    CanvasY = 180,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = intakeStepId
                        }
                    ],
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Approver
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<ProcessOutboxRecord> GetOutboxRecordAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        string commandKey,
        Guid definitionId,
        Guid? runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProcessOutboxRecord>()
            .SingleAsync(item =>
                item.CommandKey == commandKey &&
                item.ProcessDefinitionId == definitionId &&
                item.ProcessRunId == runId);
    }

    private static async Task<IReadOnlyList<ProcessOutboxRecord>> ListAutomationDispatchRecordsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var records = await dbContext.Set<ProcessOutboxRecord>()
            .Where(item =>
                item.CommandKey == ProcessOutboxService.AutomationDispatchCommandKey &&
                item.ProcessRunId == runId)
            .ToListAsync();
        return records
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private static async Task ForceNextAttemptDueAsync(IDbContextFactory<AppDbContext> dbContextFactory, Guid outboxId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var record = await dbContext.Set<ProcessOutboxRecord>()
            .SingleAsync(item => item.Id == outboxId);
        record.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();
    }

    private static async Task MoveAutomationDispatchCreatedAtAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId,
        DateTimeOffset createdAtUtc)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var record = await dbContext.Set<ProcessOutboxRecord>()
            .SingleAsync(item =>
                item.CommandKey == ProcessOutboxService.AutomationDispatchCommandKey &&
                item.ProcessRunId == runId);
        record.CreatedAtUtc = createdAtUtc;
        record.UpdatedAtUtc = createdAtUtc;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> CountActivityAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        string action,
        Guid artifactId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ActivityEntry>()
            .CountAsync(item => item.Action == action && item.ArtifactId == artifactId);
    }

    private static async Task<bool> SearchDocumentExistsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        string sourceType,
        string sourceKey)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<SearchDocument>()
            .AnyAsync(item => item.SourceType == sourceType && item.SourceKey == sourceKey);
    }

    private static async Task<bool> DefinitionExistsAsync(IDbContextFactory<AppDbContext> dbContextFactory, Guid definitionId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProcessDefinition>()
            .AnyAsync(item => item.Id == definitionId);
    }

    private static async Task<bool> HasPublishedVersionAsync(IDbContextFactory<AppDbContext> dbContextFactory, Guid definitionId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProcessDefinitionVersion>()
            .AnyAsync(item => item.ProcessDefinitionId == definitionId && item.Status == ProcessVersionStatus.Published);
    }

    private static async Task<bool> RunExistsAsync(IDbContextFactory<AppDbContext> dbContextFactory, Guid runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProcessRun>()
            .AnyAsync(item => item.Id == runId);
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(await predicate(), "The expected background process did not complete before the timeout.");
    }

    private sealed class ProcessOutboxHarness : IAsyncDisposable
    {
        private ProcessOutboxHarness(
            CanDoItAllTestEnvironment testEnvironment,
            ServiceProvider services,
            SideEffectFailureState failures,
            TrackingAutomationDispatchService automationDispatch)
        {
            TestEnvironment = testEnvironment;
            Services = services;
            Failures = failures;
            AutomationDispatch = automationDispatch;
        }

        public CanDoItAllTestEnvironment TestEnvironment { get; }

        public ServiceProvider Services { get; }

        public SideEffectFailureState Failures { get; }

        public TrackingAutomationDispatchService AutomationDispatch { get; }

        public static async Task<ProcessOutboxHarness> CreateAsync(bool trackAutomationDispatch = false)
        {
            var testEnvironment = CanDoItAllTestEnvironment.Create("process-outbox-tests");
            var profile = testEnvironment.CreatePostgreSqlProfile("primary");
            var failures = new SideEffectFailureState();
            var automationDispatch = new TrackingAutomationDispatchService();
            var services = await TestApplicationBootstrap.BuildServiceProviderAsync(
                profile,
                "CanDoItAll.Tests",
                TestSchemaBootstrapModules.Full,
                configureServices: collection =>
                {
                    collection.AddSingleton(failures);

                    collection.RemoveAll<IActivityStream>();
                    collection.AddScoped<ThrowOnceActivityStream>();
                    collection.AddScoped<IActivityStream>(serviceProvider => serviceProvider.GetRequiredService<ThrowOnceActivityStream>());

                    collection.RemoveAll<ISearchIndexService>();
                    collection.AddScoped<SearchIndexService>();
                    collection.AddScoped<ThrowOnceSearchIndexService>();
                    collection.AddScoped<ISearchIndexService>(serviceProvider => serviceProvider.GetRequiredService<ThrowOnceSearchIndexService>());

                    if (trackAutomationDispatch)
                    {
                        collection.RemoveAll<IProcessRunAutomationDispatchService>();
                        collection.AddSingleton(automationDispatch);
                        collection.AddScoped<IProcessRunAutomationDispatchService>(serviceProvider => serviceProvider.GetRequiredService<TrackingAutomationDispatchService>());
                    }
                });

            return new ProcessOutboxHarness(testEnvironment, services, failures, automationDispatch);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await TestEnvironment.DisposeAsync();
        }
    }

    private sealed class SideEffectFailureState
    {
        public bool FailDefinitionSaveActivityOnce { get; set; }

        public bool FailDefinitionPublishActivityOnce { get; set; }

        public bool FailStartRunActivityOnce { get; set; }

        public bool FailDefinitionDeleteSearchOnce { get; set; }

        public bool ShouldFailActivity(string action)
        {
            return action switch
            {
                "create-definition" or "update-definition" => ConsumeDefinitionSaveActivityFailure(),
                "publish-definition" => ConsumeDefinitionPublishActivityFailure(),
                "start-run" => ConsumeStartRunActivityFailure(),
                _ => false
            };
        }

        public bool ShouldFailSearchDelete(string sourceType)
        {
            if (sourceType != "process-definition" || !FailDefinitionDeleteSearchOnce)
            {
                return false;
            }

            FailDefinitionDeleteSearchOnce = false;
            return true;
        }

        private bool ConsumeDefinitionSaveActivityFailure()
        {
            if (!FailDefinitionSaveActivityOnce)
            {
                return false;
            }

            FailDefinitionSaveActivityOnce = false;
            return true;
        }

        private bool ConsumeDefinitionPublishActivityFailure()
        {
            if (!FailDefinitionPublishActivityOnce)
            {
                return false;
            }

            FailDefinitionPublishActivityOnce = false;
            return true;
        }

        private bool ConsumeStartRunActivityFailure()
        {
            if (!FailStartRunActivityOnce)
            {
                return false;
            }

            FailStartRunActivityOnce = false;
            return true;
        }
    }

    private sealed class ThrowOnceActivityStream(
        ActivityService inner,
        SideEffectFailureState failures) : IActivityStream
    {
        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default)
        {
            if (failures.ShouldFailActivity(request.Action))
            {
                throw new InvalidOperationException($"Forced activity failure for '{request.Action}'.");
            }

            return inner.RecordAsync(request, cancellationToken);
        }
    }

    private sealed class ThrowOnceSearchIndexService(
        SearchIndexService inner,
        SideEffectFailureState failures) : ISearchIndexService
    {
        public Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default)
        {
            return inner.UpsertAsync(input, cancellationToken);
        }

        public Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default)
        {
            if (failures.ShouldFailSearchDelete(sourceType))
            {
                throw new InvalidOperationException($"Forced search delete failure for '{sourceType}:{sourceKey}'.");
            }

            return inner.DeleteAsync(sourceType, sourceKey, cancellationToken);
        }

        public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int take = 12, CancellationToken cancellationToken = default)
        {
            return inner.SearchAsync(query, take, cancellationToken);
        }
    }

    private sealed class TrackingAutomationDispatchService : IProcessRunAutomationDispatchService
    {
        private int callCount;
        private readonly TaskCompletionSource firstDispatchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseDispatch = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => callCount;

        public bool HoldDispatch { get; set; }

        public Task FirstDispatchStarted => firstDispatchStarted.Task;

        public void ReleaseDispatch()
        {
            releaseDispatch.TrySetResult();
        }

        public async Task DispatchAsync(
            Guid processRunId,
            Guid? triggerStepRunId,
            string trigger,
            Func<CancellationToken, Task>? renewLeaseAsync = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref callCount);
            if (renewLeaseAsync is not null)
            {
                await renewLeaseAsync(cancellationToken);
            }

            firstDispatchStarted.TrySetResult();
            if (HoldDispatch)
            {
                await releaseDispatch.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
