using System.Collections.Concurrent;
using System.Text.Json;
using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl.Matchers;

namespace CanDoItAll.Tests.Integration;

public sealed class AutomationRuntimeIntegrationTests
{
    [Fact]
    public async Task AutomationWorkspaceService_aggregates_multiple_signal_sources_without_last_registration_wins()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-signals");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddScoped<IAutomationSignalSource>(_ => new FixedAutomationSignalSource(
                    new AutomationSignalItem("Ops", "Source one", "First signal", "/one", "info")));
                services.AddScoped<IAutomationSignalSource>(_ => new FixedAutomationSignalSource(
                    new AutomationSignalItem("Ops", "Source two", "Second signal", "/two", "warning")));
            });

        await using var scope = provider.CreateAsyncScope();
        var automationWorkspaceService = scope.ServiceProvider.GetRequiredService<AutomationWorkspaceService>();

        var signals = await automationWorkspaceService.ListSignalsAsync();

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, item => item.Title == "Source one");
        Assert.Contains(signals, item => item.Title == "Source two");
    }

    [Fact]
    public async Task Operational_messages_do_not_materialize_workbench_nodes_by_default()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-operational-default");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);
        var envelopeType = AutomationEnvelopeTypeNames.For<TestOperationalEnvelope>();

        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var initialContext = await dbContextFactory.CreateDbContextAsync();
        var initialNodeCount = await initialContext.Set<ProjectObjectRecord>().CountAsync();

        var envelopeId = await publisher.PublishAsync(new TestOperationalEnvelope("default-envelope"));

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(initialNodeCount, await verificationContext.Set<ProjectObjectRecord>().CountAsync());
        Assert.NotNull(await verificationContext.Set<AutomationEnvelopeRecord>().SingleOrDefaultAsync(item => item.Id == envelopeId));
        Assert.Equal(
            AutomationEnvelopeState.Completed,
            await verificationContext.Set<AutomationEnvelopeRecord>()
                .Where(item => item.Id == envelopeId)
                .Select(item => item.State)
                .SingleAsync());
        Assert.Equal(
            envelopeType,
            await verificationContext.Set<AutomationEnvelopeRecord>()
                .Where(item => item.Id == envelopeId)
                .Select(item => item.EnvelopeType)
                .SingleAsync());
    }

    [Fact]
    public async Task Explicit_materializer_can_turn_an_execution_result_into_a_domain_artifact()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-materializer");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddScoped<IPluginIngressMaterializer, ProjectNodeIngressMaterializer>();
            });

        await using var scope = provider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projectsService, "Explicit materializer proof");
        var payload = JsonSerializer.Serialize(new MaterializerPayload(projectId, "Explicit node", "explicit-node"));

        var acceptResult = await inbox.AcceptAsync(new PluginIngressEnvelopeRequest(
            "email",
            "crm-sync",
            "message-001",
            "cursor-001",
            payload));

        await using var beforeContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await beforeContext.Set<ProjectObjectRecord>().AnyAsync(item => item.ProjectId == projectId));

        var envelope = await inbox.MaterializeAsync(acceptResult.EnvelopeId, ProjectNodeIngressMaterializer.Key);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(PluginIngressState.Materialized, envelope.State);
        Assert.Contains(
            await verificationContext.Set<ProjectObjectRecord>().Where(item => item.ProjectId == projectId).ToListAsync(),
            item => item.NodeKey == "explicit-node" && item.Title == "Explicit node");
    }

    [Fact]
    public async Task Automation_trigger_definition_round_trips_with_cron_timezone_and_misfire_policy()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-trigger-roundtrip");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);
        await using var scope = provider.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IAutomationTriggerRegistry>();
        var definition = new AutomationTriggerDefinition(
            Guid.NewGuid(),
            AutomationTriggerOwnerKind.Plugin,
            "crm-sync",
            "hourly-refresh",
            true,
            AutomationTriggerKind.Cron,
            "0 0/15 * * * ?",
            "UTC",
            DateTimeOffset.UtcNow.AddMinutes(1),
            DateTimeOffset.UtcNow.AddHours(2),
            AutomationTriggerMisfirePolicy.DoNothing,
            """{"mode":"hourly"}""",
            "crm-sync-hourly",
            null,
            null,
            DateTimeOffset.UtcNow);

        var saved = await registry.SaveAsync(definition);
        var roundTrip = await registry.GetAsync(saved.Id);

        Assert.NotNull(roundTrip);
        Assert.Equal(definition.CronExpression, roundTrip!.CronExpression);
        Assert.Equal(definition.TimeZoneId, roundTrip.TimeZoneId);
        Assert.Equal(definition.MisfirePolicy, roundTrip.MisfirePolicy);
        Assert.Equal(definition.OwnerKind, roundTrip.OwnerKind);
        Assert.Equal(definition.TriggerKey, roundTrip.TriggerKey);
    }

    [Fact]
    public async Task Quartz_scheduler_bridge_rehydrates_canonical_triggers_on_startup()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-quartz-rehydrate");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var triggerId = Guid.NewGuid();

        await using (var provider = await BuildProviderAsync(profile))
        {
            await InsertTriggerRecordAsync(provider, new AutomationTriggerRecord
            {
                Id = triggerId,
                OwnerKind = AutomationTriggerOwnerKind.Plugin,
                OwnerKey = "rehydrate-plugin",
                TriggerKey = "rehydrate",
                IsEnabled = true,
                TriggerKind = AutomationTriggerKind.Cron,
                CronExpression = "0 0/10 * * * ?",
                TimeZoneId = "UTC",
                PayloadJson = "{}",
                DedupeKey = "rehydrate",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await using var restartedProvider = await BuildProviderAsync(profile);
        await using var hostedServices = await HostedServiceHarness.StartAsync(restartedProvider);
        await using var scope = restartedProvider.CreateAsyncScope();
        var scheduler = await scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();

        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("candoitall-automation-triggers"));

        Assert.Contains(jobKeys, jobKey => jobKey.Name == $"trigger-{triggerId:N}");
    }

    [Fact]
    public async Task Quartz_trigger_fire_publishes_durable_work_instead_of_running_plugin_logic_inline()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-trigger-fire");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();
        var triggerId = Guid.NewGuid();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, TriggerFireCaptureHandler>();
            });

        await InsertTriggerRecordAsync(provider, new AutomationTriggerRecord
        {
            Id = triggerId,
            OwnerKind = AutomationTriggerOwnerKind.Plugin,
            OwnerKey = "trigger-plugin",
            TriggerKey = "fire-once",
            IsEnabled = true,
            TriggerKind = AutomationTriggerKind.Once,
            CronExpression = string.Empty,
            TimeZoneId = "UTC",
            StartAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(300),
            PayloadJson = """{"step":"fire"}""",
            DedupeKey = "fire-once",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(() => Task.FromResult(sink.TriggerRequests.Count > 0), TimeSpan.FromSeconds(10));

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Set<AutomationEnvelopeRecord>().AnyAsync(item => item.EnvelopeType == AutomationEnvelopeTypeNames.For<AutomationTriggerFireRequest>()));
        Assert.True(await dbContext.Set<AutomationTriggerRecord>().AnyAsync(item => item.Id == triggerId && item.LastFiredAtUtc.HasValue));
    }

    private static Task<ServiceProvider> BuildProviderAsync(
        TestDatabaseProfile profile,
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        CancellationToken cancellationToken = default)
    {
        Quartz.Logging.LogProvider.IsDisabled = false;
        Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpQuartzLogProvider());

        return TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configurationOverrides,
            services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddHostedService<AutomationSchedulerProjectionHostedService>();
                services.AddHostedService<AutomationMessagePumpWorker>();
                services.AddHostedService<ConnectorOutboxDrainWorker>();
                services.AddHostedService<LegacyBackgroundJobQueueBridgeWorker>();
                services.Configure<AutomationRuntimeOptions>(options =>
                {
                    options.MessageDispatchBatchSize = 20;
                    options.MessagePollInterval = TimeSpan.FromMilliseconds(20);
                    options.ConnectorOutboxBatchSize = 20;
                    options.ConnectorOutboxPollInterval = TimeSpan.FromMilliseconds(20);
                    options.LegacyBackgroundQueuePollInterval = TimeSpan.FromMilliseconds(20);
                    options.Mqtt.Enabled = false;
                    options.Mqtt.Host = string.Empty;
                });
                configureServices?.Invoke(services);
            },
            cancellationToken);
    }

    private static async Task InsertTriggerRecordAsync(ServiceProvider provider, AutomationTriggerRecord record)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Set<AutomationTriggerRecord>().AddAsync(record);
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

        Assert.True(result.IsSuccess);
        return result.Value;
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

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        throw new TimeoutException($"Condition was not satisfied within {timeout}.");
    }

    [Fact]
    public async Task Internal_message_dispatch_retries_then_dead_letters_failed_handlers_idempotently()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-dead-letter");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddScoped<IAutomationMessageHandler, AlwaysRetryHandler>();
            });

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(
                new TestOperationalEnvelope("retry-me"),
                new AutomationPublishOptions(MaxAttempts: 2));
        }

        Assert.Equal(1, await DispatchPendingAsync(provider));
        await ForceAllDeliveriesDueAsync(provider);
        Assert.Equal(1, await DispatchPendingAsync(provider));
        Assert.Equal(0, await DispatchPendingAsync(provider));

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var delivery = await dbContext.Set<AutomationEnvelopeDeliveryRecord>()
            .SingleAsync(item => item.EnvelopeId == envelopeId);
        var attempts = await dbContext.Set<AutomationDeliveryAttemptRecord>()
            .Where(item => item.EnvelopeId == envelopeId)
            .OrderBy(item => item.AttemptNumber)
            .ToListAsync();
        var deadLetters = await dbContext.Set<AutomationDeadLetterRecord>()
            .Where(item => item.EnvelopeId == envelopeId)
            .ToListAsync();
        var envelope = await dbContext.Set<AutomationEnvelopeRecord>()
            .SingleAsync(item => item.Id == envelopeId);

        Assert.Equal(AutomationDeliveryState.DeadLettered, delivery.State);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Equal(2, attempts.Count);
        Assert.Single(deadLetters);
        Assert.Equal(AutomationEnvelopeState.DeadLettered, envelope.State);
    }

    [Fact]
    public async Task Internal_message_publish_fans_out_to_multiple_subscribers()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-fanout");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, FanOutPrimaryHandler>();
                services.AddScoped<IAutomationMessageHandler, FanOutSecondaryHandler>();
            });

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(new TestOperationalEnvelope("fan-out"));
        }

        Assert.Equal(2, await DispatchPendingAsync(provider));

        var handlerKeys = sink.Messages
            .Select(item => item.HandlerKey)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, sink.Messages.Count);
        Assert.Equal(
            new[]
            {
                typeof(FanOutPrimaryHandler).FullName!,
                typeof(FanOutSecondaryHandler).FullName!
            },
            handlerKeys);

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var deliveries = await dbContext.Set<AutomationEnvelopeDeliveryRecord>()
            .Where(item => item.EnvelopeId == envelopeId)
            .ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, item => Assert.Equal(AutomationDeliveryState.Completed, item.State));
    }

    [Fact]
    public async Task Internal_message_delivery_survives_restart_boundary()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-restart");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();
        Guid envelopeId;

        await using (var firstProvider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, SuccessfulOperationalHandler>();
            }))
        {
            await using var firstScope = firstProvider.CreateAsyncScope();
            var publisher = firstScope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(new TestOperationalEnvelope("restart-boundary"));
        }

        await using var secondProvider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, SuccessfulOperationalHandler>();
            });

        Assert.Equal(1, await DispatchPendingAsync(secondProvider));
        Assert.Contains(sink.Messages, item => item.Value == "restart-boundary");

        await using var verificationScope = secondProvider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(
            AutomationEnvelopeState.Completed,
            await dbContext.Set<AutomationEnvelopeRecord>()
                .Where(item => item.Id == envelopeId)
                .Select(item => item.State)
                .SingleAsync());
    }

    [Fact]
    public async Task Connector_outbox_pending_commands_are_processed_by_a_hosted_worker()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-outbox-worker");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var handler = new TestConnectorCommandHandler(
            ConnectorCommandExecutionResult.Completed("""{"delivery":"worker"}"""));

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(handler);
                services.AddSingleton<IConnectorCommandHandler>(serviceProvider => serviceProvider.GetRequiredService<TestConnectorCommandHandler>());
            });

        Guid commandId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
            var projectId = await CreateProjectAsync(projects, "Hosted outbox worker");
            var enqueue = await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
                projectId,
                WebhookResourceConnectorPlugin.PluginKey,
                "deliver",
                """{"endpointUrl":"https://example.com/hooks/runtime"}""",
                "worker-runtime-command",
                "integration-tests"));
            commandId = enqueue.CommandId;
        }

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(
            async () => (await GetConnectorSnapshotAsync(provider, commandId))?.Status == ConnectorCommandStatus.Completed,
            TimeSpan.FromSeconds(10));

        var snapshot = await GetConnectorSnapshotAsync(provider, commandId);

        Assert.NotNull(snapshot);
        Assert.Equal(ConnectorCommandStatus.Completed, snapshot!.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Queued_background_work_is_consumed_by_a_runtime_worker()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-background-worker");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationBackgroundJobHandler, BackgroundJobCaptureHandler>();
            });

        Guid jobId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var scheduler = scope.ServiceProvider.GetRequiredService<IAutomationBackgroundJobScheduler>();
            jobId = await scheduler.ScheduleAsync(
                BackgroundJobCaptureHandler.JobTypeValue,
                "Durable background work");
        }

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(
            async () => string.Equals(await GetBackgroundJobStateAsync(provider, jobId), BackgroundJobState.Succeeded.ToString(), StringComparison.Ordinal),
            TimeSpan.FromSeconds(10));

        Assert.Single(sink.BackgroundJobRequests);
        Assert.Equal(BackgroundJobCaptureHandler.JobTypeValue, sink.BackgroundJobRequests.Single().JobType);
    }

    [Fact]
    public async Task Due_triggers_are_dispatched_without_manual_invocation()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-due-trigger");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();
        var triggerId = Guid.NewGuid();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, TriggerFireCaptureHandler>();
            });

        await InsertTriggerRecordAsync(provider, new AutomationTriggerRecord
        {
            Id = triggerId,
            OwnerKind = AutomationTriggerOwnerKind.Plugin,
            OwnerKey = "due-trigger-plugin",
            TriggerKey = "run-automatically",
            IsEnabled = true,
            TriggerKind = AutomationTriggerKind.Once,
            CronExpression = string.Empty,
            TimeZoneId = "UTC",
            StartAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(250),
            PayloadJson = """{"mode":"automatic"}""",
            DedupeKey = "automatic-trigger",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(() => Task.FromResult(sink.TriggerRequests.Count > 0), TimeSpan.FromSeconds(10));

        Assert.Contains(sink.TriggerRequests, item => item.TriggerId == triggerId);
    }

    private static async Task<int> DispatchPendingAsync(ServiceProvider provider, int take = 20)
    {
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IAutomationMessageDispatcher>();
        return await dispatcher.DispatchPendingAsync(take);
    }

    private static async Task ForceAllDeliveriesDueAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);

        var deliveries = await dbContext.Set<AutomationEnvelopeDeliveryRecord>()
            .Where(item => item.State == AutomationDeliveryState.RetryScheduled || item.State == AutomationDeliveryState.Pending)
            .ToListAsync();
        foreach (var delivery in deliveries)
        {
            delivery.AvailableAtUtc = now;
            delivery.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<ConnectorCommandSnapshot?> GetConnectorSnapshotAsync(ServiceProvider provider, Guid commandId)
    {
        await using var scope = provider.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
        return await outbox.GetAsync(commandId);
    }

    private static async Task<string?> GetBackgroundJobStateAsync(ServiceProvider provider, Guid jobId)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<BackgroundJobRecord>()
            .Where(item => item.Id == jobId)
            .Select(item => item.State)
            .SingleOrDefaultAsync();
    }

    private static async Task<AutomationEnvelopeState?> GetEnvelopeStateAsync(ServiceProvider provider, Guid envelopeId)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<AutomationEnvelopeRecord>()
            .Where(item => item.Id == envelopeId)
            .Select(item => (AutomationEnvelopeState?)item.State)
            .SingleOrDefaultAsync();
    }

    [Fact]
    public async Task Plugin_ingress_inbox_deduplicates_external_envelopes()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-ingress-dedup");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var request = new PluginIngressEnvelopeRequest(
            "email",
            "crm-sync",
            "external-001",
            "cursor-001",
            """{"subject":"hello"}""");

        var first = await inbox.AcceptAsync(request);
        var second = await inbox.AcceptAsync(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.EnvelopeId, second.EnvelopeId);
        Assert.Equal(1, await dbContext.Set<PluginIngressEnvelopeRecord>().CountAsync());
    }

    [Fact]
    public async Task Plugin_ingress_cursor_progress_is_persisted_across_runs()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-ingress-cursor");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");

        await using (var firstProvider = await BuildProviderAsync(profile))
        {
            await using var firstScope = firstProvider.CreateAsyncScope();
            var inbox = firstScope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
            await inbox.SaveCursorAsync("email", "crm-sync", "cursor-002");
        }

        await using var secondProvider = await BuildProviderAsync(profile);
        await using var secondScope = secondProvider.CreateAsyncScope();
        var verificationInbox = secondScope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();

        Assert.Equal("cursor-002", await verificationInbox.GetCursorAsync("email", "crm-sync"));
    }

    [Fact]
    public async Task Ingress_envelope_can_remain_unmaterialized_until_explicit_handler_runs()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-ingress-explicit");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddScoped<IPluginIngressMaterializer, ProjectNodeIngressMaterializer>();
            });

        await using var scope = provider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projectsService, "Ingress explicit materialization");
        var payload = JsonSerializer.Serialize(new MaterializerPayload(projectId, "Deferred node", "deferred-node"));

        var accepted = await inbox.AcceptAsync(new PluginIngressEnvelopeRequest(
            "whatsapp",
            "crm-sync",
            "message-007",
            "cursor-007",
            payload));
        var snapshot = await inbox.GetAsync(accepted.EnvelopeId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.NotNull(snapshot);
        Assert.Equal(PluginIngressState.Accepted, snapshot!.State);
        Assert.Null(snapshot.MaterializedAtUtc);
        Assert.Equal(string.Empty, snapshot.MaterializerKey);
        Assert.False(await dbContext.Set<ProjectObjectRecord>().AnyAsync(item => item.ProjectId == projectId && item.NodeKey == "deferred-node"));
    }

    [Fact]
    public async Task Execution_telemetry_preserves_correlation_and_causation_across_dispatch()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-telemetry");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(new MessageSink());
                services.AddScoped<IAutomationMessageHandler, SuccessfulOperationalHandler>();
            });

        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        Guid envelopeId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(
                new TestOperationalEnvelope("telemetry"),
                new AutomationPublishOptions(
                    CorrelationId: correlationId,
                    CausationId: causationId,
                    MaxAttempts: 1));
        }

        Assert.Equal(1, await DispatchPendingAsync(provider));

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var eventKinds = await dbContext.Set<AutomationExecutionLogRecord>()
            .Where(item => item.CorrelationId == correlationId && item.CausationId == causationId)
            .Select(item => item.EventKind)
            .ToListAsync();
        var attempts = await dbContext.Set<AutomationDeliveryAttemptRecord>()
            .Where(item => item.EnvelopeId == envelopeId)
            .ToListAsync();

        Assert.Contains(AutomationExecutionLogKind.Published, eventKinds);
        Assert.Contains(AutomationExecutionLogKind.DeliveryStarted, eventKinds);
        Assert.Contains(AutomationExecutionLogKind.DeliveryCompleted, eventKinds);
        Assert.Single(attempts);
        Assert.Equal(correlationId, attempts[0].CorrelationId);
        Assert.Equal(causationId, attempts[0].CausationId);
    }

    [Fact]
    public async Task Dead_letter_items_are_visible_to_operators()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-operator-dead-letter");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddScoped<IAutomationMessageHandler, AlwaysRetryHandler>();
            });

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(
                new TestOperationalEnvelope("operator-view"),
                new AutomationPublishOptions(MaxAttempts: 1));
        }

        Assert.Equal(1, await DispatchPendingAsync(provider));

        await using var verificationScope = provider.CreateAsyncScope();
        var inspectionService = verificationScope.ServiceProvider.GetRequiredService<IAutomationRuntimeInspectionService>();
        var deadLetters = await inspectionService.ListDeadLettersAsync();

        Assert.Contains(deadLetters, item =>
            item.EnvelopeId == envelopeId &&
            item.HandlerKey == typeof(AlwaysRetryHandler).FullName &&
            item.ErrorMessage.Contains("retry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Core_runtime_still_functions_when_mqtt_bridge_is_disabled()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-mqtt-disabled");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, SuccessfulOperationalHandler>();
                services.Configure<AutomationRuntimeOptions>(options =>
                {
                    options.Mqtt.Enabled = false;
                    options.Mqtt.Host = "127.0.0.1";
                });
            });

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(new TestOperationalEnvelope("mqtt-disabled"));
        }

        await WaitForAsync(
            async () => await GetEnvelopeStateAsync(provider, envelopeId) == AutomationEnvelopeState.Completed,
            TimeSpan.FromSeconds(10));

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Contains(sink.Messages, item => item.Value == "mqtt-disabled");
        Assert.Equal(
            AutomationEnvelopeState.Completed,
            await dbContext.Set<AutomationEnvelopeRecord>()
                .Where(item => item.Id == envelopeId)
                .Select(item => item.State)
                .SingleAsync());
        Assert.True(await dbContext.Set<AutomationExecutionLogRecord>().AnyAsync());
    }

    private sealed record TestOperationalEnvelope(string Value);

    private sealed record MaterializerPayload(Guid ProjectId, string Title, string NodeKey);

    private sealed record MessageCapture(
        string HandlerKey,
        string Value,
        Guid EnvelopeId,
        Guid? CorrelationId,
        Guid? CausationId);

    private sealed class MessageSink
    {
        public ConcurrentQueue<MessageCapture> Messages { get; } = new();

        public ConcurrentQueue<AutomationTriggerFireRequest> TriggerRequests { get; } = new();

        public ConcurrentQueue<AutomationBackgroundJobRequest> BackgroundJobRequests { get; } = new();
    }

    private sealed class FixedAutomationSignalSource(params AutomationSignalItem[] items) : IAutomationSignalSource
    {
        public Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AutomationSignalItem>>(items);
        }
    }

    private sealed class ProjectNodeIngressMaterializer(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IClock clock) : IPluginIngressMaterializer
    {
        public const string Key = "test.project-node";

        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public string MaterializerKey => Key;

        public async Task<PluginIngressMaterializationResult> MaterializeAsync(
            PluginIngressEnvelopeSnapshot envelope,
            CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Deserialize<MaterializerPayload>(envelope.PayloadJson, SerializerOptions);
            if (payload is null || payload.ProjectId == Guid.Empty || string.IsNullOrWhiteSpace(payload.NodeKey) || string.IsNullOrWhiteSpace(payload.Title))
            {
                return PluginIngressMaterializationResult.Failure("Ingress payload is invalid for project-node materialization.");
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Set<ProjectObjectRecord>().AddAsync(new ProjectObjectRecord
            {
                ProjectId = payload.ProjectId,
                NodeKey = payload.NodeKey,
                ObjectType = ProjectObjectType.Note,
                ObjectSubtype = "automation-ingress",
                Title = payload.Title,
                CreatedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow()
            }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return PluginIngressMaterializationResult.Success($"Created node '{payload.NodeKey}'.");
        }
    }

    private abstract class SuccessfulOperationalHandlerBase(MessageSink sink) : AutomationMessageHandler<TestOperationalEnvelope>
    {
        protected override Task<AutomationMessageHandleResult> HandleAsync(
            TestOperationalEnvelope envelope,
            AutomationMessageContext context,
            CancellationToken cancellationToken)
        {
            sink.Messages.Enqueue(new MessageCapture(
                HandlerKey,
                envelope.Value,
                context.EnvelopeId,
                context.CorrelationId,
                context.CausationId));
            return Task.FromResult(AutomationMessageHandleResult.Completed());
        }
    }

    private sealed class SuccessfulOperationalHandler(MessageSink sink) : SuccessfulOperationalHandlerBase(sink);

    private sealed class FanOutPrimaryHandler(MessageSink sink) : SuccessfulOperationalHandlerBase(sink);

    private sealed class FanOutSecondaryHandler(MessageSink sink) : SuccessfulOperationalHandlerBase(sink);

    private sealed class AlwaysRetryHandler : AutomationMessageHandler<TestOperationalEnvelope>
    {
        protected override Task<AutomationMessageHandleResult> HandleAsync(
            TestOperationalEnvelope envelope,
            AutomationMessageContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(AutomationMessageHandleResult.RetryScheduled($"Please retry envelope '{envelope.Value}'."));
        }
    }

    private sealed class TriggerFireCaptureHandler(MessageSink sink) : AutomationMessageHandler<AutomationTriggerFireRequest>
    {
        protected override Task<AutomationMessageHandleResult> HandleAsync(
            AutomationTriggerFireRequest envelope,
            AutomationMessageContext context,
            CancellationToken cancellationToken)
        {
            sink.TriggerRequests.Enqueue(envelope);
            return Task.FromResult(AutomationMessageHandleResult.Completed());
        }
    }

    private sealed class BackgroundJobCaptureHandler(MessageSink sink) : IAutomationBackgroundJobHandler
    {
        public const string JobTypeValue = "integration.background-job";

        public string JobType => JobTypeValue;

        public Task<AutomationMessageHandleResult> HandleAsync(
            AutomationBackgroundJobRequest request,
            CancellationToken cancellationToken)
        {
            sink.BackgroundJobRequests.Enqueue(request);
            return Task.FromResult(AutomationMessageHandleResult.Completed());
        }
    }

    private sealed class HostedServiceHarness : IAsyncDisposable
    {
        private readonly IReadOnlyList<IHostedService> _hostedServices;
        private readonly IScheduler? _scheduler;
        private readonly TestHostApplicationLifetime? _applicationLifetime;

        private HostedServiceHarness(
            IReadOnlyList<IHostedService> hostedServices,
            IScheduler? scheduler,
            TestHostApplicationLifetime? applicationLifetime)
        {
            _hostedServices = hostedServices;
            _scheduler = scheduler;
            _applicationLifetime = applicationLifetime;
        }

        public static async Task<HostedServiceHarness> StartAsync(ServiceProvider provider)
        {
            var hostedServices = provider.GetServices<IHostedService>().ToList();
            foreach (var hostedService in hostedServices)
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            var scheduler = default(IScheduler);
            var schedulerFactory = provider.GetService<ISchedulerFactory>();
            if (schedulerFactory is not null)
            {
                scheduler = await schedulerFactory.GetScheduler();
                await scheduler.Start();
            }

            var applicationLifetime = provider.GetService<IHostApplicationLifetime>() as TestHostApplicationLifetime;
            applicationLifetime?.NotifyStarted();

            return new HostedServiceHarness(hostedServices, scheduler, applicationLifetime);
        }

        public async ValueTask DisposeAsync()
        {
            _applicationLifetime?.NotifyStopping();

            if (_scheduler is not null && !_scheduler.IsShutdown)
            {
                await _scheduler.Shutdown(waitForJobsToComplete: true);
            }

            foreach (var hostedService in _hostedServices.Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            _applicationLifetime?.NotifyStopped();
        }
    }

    private sealed class TestConnectorCommandHandler : IConnectorCommandHandler
    {
        private readonly Queue<ConnectorCommandExecutionResult> _results;

        public TestConnectorCommandHandler(params ConnectorCommandExecutionResult[] results)
        {
            _results = new Queue<ConnectorCommandExecutionResult>(results);
        }

        public List<ConnectorCommandExecutionRequest> Requests { get; } = [];

        public bool CanHandle(string connectorPluginKey, string commandKey)
        {
            return string.Equals(connectorPluginKey, WebhookResourceConnectorPlugin.PluginKey, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(commandKey, "deliver", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ConnectorCommandExecutionResult> ExecuteAsync(
            ConnectorCommandExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                _results.Count > 0
                    ? _results.Dequeue()
                    : ConnectorCommandExecutionResult.Completed("""{"delivery":"default"}"""));
        }
    }

    private sealed class NoOpQuartzLogProvider : Quartz.Logging.ILogProvider
    {
        public Quartz.Logging.Logger GetLogger(string name)
        {
            return static (_, _, _, _) => false;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return NoOpDisposable.Instance;
        }

        public IDisposable OpenMappedContext(string key, object value, bool destructure = false)
        {
            return NoOpDisposable.Instance;
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly IDisposable Instance = new NoOpDisposable();

        public void Dispose()
        {
        }
    }
}
