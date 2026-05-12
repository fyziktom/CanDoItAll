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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    public async Task Concurrent_materialize_calls_only_run_the_materializer_once()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-concurrent-materialize");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var materializer = new CountingMaterializer(TimeSpan.FromMilliseconds(150));

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton<IPluginIngressMaterializer>(materializer);
            });

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
            envelopeId = (await inbox.AcceptAsync(new PluginIngressEnvelopeRequest(
                "email",
                "crm-sync",
                "materialize-once",
                "cursor-100",
                """{"mode":"count"}"""))).EnvelopeId;
        }

        var results = await RunConcurrentlyAsync(
            2,
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
                return await inbox.MaterializeAsync(envelopeId, CountingMaterializer.Key);
            });

        Assert.Equal(1, materializer.CallCount);
        Assert.All(results, item => Assert.Equal(PluginIngressState.Materialized, item.State));
        Assert.All(results, item => Assert.Equal("counted materialization", item.MaterializationSummary));
    }

    [Fact]
    public async Task Already_materialized_envelope_returns_existing_snapshot_without_reinvoking_plugin_code()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-reread-materialize");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var materializer = new CountingMaterializer(TimeSpan.Zero);

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton<IPluginIngressMaterializer>(materializer);
            });

        PluginIngressEnvelopeSnapshot first;
        PluginIngressEnvelopeSnapshot second;
        await using (var scope = provider.CreateAsyncScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
            var envelopeId = (await inbox.AcceptAsync(new PluginIngressEnvelopeRequest(
                "email",
                "crm-sync",
                "materialize-reread",
                "cursor-101",
                """{"mode":"count"}"""))).EnvelopeId;

            first = await inbox.MaterializeAsync(envelopeId, CountingMaterializer.Key);
            second = await inbox.MaterializeAsync(envelopeId, CountingMaterializer.Key);
        }

        Assert.Equal(1, materializer.CallCount);
        Assert.Equal(PluginIngressState.Materialized, first.State);
        Assert.Equal(PluginIngressState.Materialized, second.State);
        Assert.Equal(first.MaterializationSummary, second.MaterializationSummary);
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

    [Fact]
    public async Task Once_like_trigger_is_retired_after_first_fire()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-retire-once-like");
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
            OwnerKey = "retire-plugin",
            TriggerKey = "retire-due-date",
            IsEnabled = true,
            TriggerKind = AutomationTriggerKind.DueDateProjection,
            CronExpression = string.Empty,
            TimeZoneId = "UTC",
            StartAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(300),
            PayloadJson = """{"mode":"retire"}""",
            DedupeKey = "retire-due-date",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(() => Task.FromResult(sink.TriggerRequests.Count > 0), TimeSpan.FromSeconds(10));

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var trigger = await dbContext.Set<AutomationTriggerRecord>()
            .SingleAsync(item => item.Id == triggerId);

        Assert.False(trigger.IsEnabled);
        Assert.NotNull(trigger.LastFiredAtUtc);
        Assert.Null(trigger.NextPlannedFireAtUtc);
    }

    [Fact]
    public async Task One_shot_trigger_is_not_rehydrated_after_it_has_already_fired()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-no-rehydrate-once");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var firstSink = new MessageSink();
        var triggerId = Guid.NewGuid();

        await using (var firstProvider = await BuildProviderAsync(
                         profile,
                         services =>
                         {
                             services.AddSingleton(firstSink);
                             services.AddScoped<IAutomationMessageHandler, TriggerFireCaptureHandler>();
                         }))
        {
            await InsertTriggerRecordAsync(firstProvider, new AutomationTriggerRecord
            {
                Id = triggerId,
                OwnerKind = AutomationTriggerOwnerKind.Plugin,
                OwnerKey = "one-shot-plugin",
                TriggerKey = "run-once",
                IsEnabled = true,
                TriggerKind = AutomationTriggerKind.Once,
                CronExpression = string.Empty,
                TimeZoneId = "UTC",
                StartAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(300),
                PayloadJson = """{"mode":"one-shot"}""",
                DedupeKey = "run-once",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            await using var hostedServices = await HostedServiceHarness.StartAsync(firstProvider);
            await WaitForAsync(() => Task.FromResult(firstSink.TriggerRequests.Count > 0), TimeSpan.FromSeconds(10));
        }

        var secondSink = new MessageSink();
        await using var restartedProvider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(secondSink);
                services.AddScoped<IAutomationMessageHandler, TriggerFireCaptureHandler>();
            });
        await using var restartedHostedServices = await HostedServiceHarness.StartAsync(restartedProvider);
        await using var restartedScope = restartedProvider.CreateAsyncScope();
        var scheduler = await restartedScope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("candoitall-automation-triggers"));

        Assert.DoesNotContain(jobKeys, jobKey => jobKey.Name == $"trigger-{triggerId:N}");
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.Empty(secondSink.TriggerRequests);
    }

    [Fact]
    public async Task Trigger_registry_save_returns_reloaded_next_fire_time_after_quartz_projection()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-trigger-save-reload");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);
        await using var scope = provider.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IAutomationTriggerRegistry>();

        var definition = new AutomationTriggerDefinition(
            Guid.NewGuid(),
            AutomationTriggerOwnerKind.Plugin,
            "crm-sync",
            "reload-next-fire",
            true,
            AutomationTriggerKind.Once,
            string.Empty,
            "UTC",
            DateTimeOffset.UtcNow.AddMinutes(5),
            null,
            AutomationTriggerMisfirePolicy.FireOnceNow,
            """{"mode":"reload"}""",
            "reload-next-fire",
            null,
            null,
            DateTimeOffset.UtcNow);

        var saved = await registry.SaveAsync(definition);
        var roundTrip = await registry.GetAsync(saved.Id);

        Assert.NotNull(saved.NextPlannedFireAtUtc);
        Assert.NotNull(roundTrip);
        Assert.Equal(roundTrip!.NextPlannedFireAtUtc, saved.NextPlannedFireAtUtc);
        Assert.Equal(roundTrip.UpdatedAtUtc, saved.UpdatedAtUtc);
    }

    private static Task<ServiceProvider> BuildProviderAsync(
        TestDatabaseProfile profile,
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        CancellationToken cancellationToken = default)
    {
        var mergedConfigurationOverrides = CreateRuntimeConfigurationOverrides(configurationOverrides);
        Quartz.Logging.LogProvider.IsDisabled = false;
        Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpQuartzLogProvider());

        return TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            mergedConfigurationOverrides,
            services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddHostedService<AutomationSchedulerProjectionHostedService>();
                services.AddHostedService<AutomationMessagePumpWorker>();
                services.AddHostedService<ConnectorOutboxDrainWorker>();
                services.AddHostedService<LegacyBackgroundJobQueueBridgeWorker>();
                configureServices?.Invoke(services);
            },
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, string?> CreateRuntimeConfigurationOverrides(
        IReadOnlyDictionary<string, string?>? overrides)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Automation:Runtime:MessageDispatchBatchSize"] = "20",
            ["Automation:Runtime:MessagePollInterval"] = "00:00:00.020",
            ["Automation:Runtime:ConnectorOutboxBatchSize"] = "20",
            ["Automation:Runtime:ConnectorOutboxPollInterval"] = "00:00:00.020",
            ["Automation:Runtime:LegacyBackgroundQueuePollInterval"] = "00:00:00.020",
            ["Automation:Runtime:DeliveryLeaseDuration"] = "00:05:00",
            ["Automation:Runtime:ConnectorCommandLeaseDuration"] = "00:05:00",
            ["Automation:Runtime:WorkerFailureBackoff"] = "00:00:00.050",
            ["Automation:Runtime:Mqtt:Enabled"] = "false",
            ["Automation:Runtime:Mqtt:Host"] = string.Empty
        };

        if (overrides is null)
        {
            return merged;
        }

        foreach (var pair in overrides)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
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

    private static async Task<bool> HasActiveConnectorLeaseAsync(ServiceProvider provider, Guid commandId)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var command = await dbContext.Set<ConnectorCommandRecord>()
            .FirstOrDefaultAsync(item => item.Id == commandId);
        return command is not null &&
               !string.IsNullOrWhiteSpace(command.LeaseToken) &&
               command.LeaseExpiresAtUtc.HasValue &&
               command.LeaseExpiresAtUtc.Value > DateTimeOffset.UtcNow;
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
    public async Task Plugin_ingress_cursor_save_trims_keys_before_lookup()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-ingress-cursor-trim");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await using var scope = provider.CreateAsyncScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await inbox.SaveCursorAsync(" email ", " crm-sync ", " cursor-003 ");
        await inbox.SaveCursorAsync("email", "crm-sync", "cursor-004");

        Assert.Equal("cursor-004", await inbox.GetCursorAsync(" email ", " crm-sync "));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var rows = await dbContext.Set<PluginIngressCursorRecord>()
            .Where(item => item.SourceKind == "email" && item.SourceKey == "crm-sync")
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("cursor-004", rows.Single().CursorValue);
    }

    [Fact]
    public async Task Concurrent_first_cursor_save_reuses_the_same_cursor_row()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-ingress-cursor-concurrent");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        await RunConcurrentlyAsync(
            2,
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
                await inbox.SaveCursorAsync(" email ", " crm-sync ", "cursor-005");
                return true;
            });

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationInbox = verificationScope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var rows = await dbContext.Set<PluginIngressCursorRecord>()
            .Where(item => item.SourceKind == "email" && item.SourceKey == "crm-sync")
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("cursor-005", await verificationInbox.GetCursorAsync("email", "crm-sync"));
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

    [Fact]
    public async Task Automation_runtime_options_bind_from_configuration()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-options-config");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var overrides = new Dictionary<string, string?>
        {
            ["Automation:Runtime:MessagePollInterval"] = "00:00:00.123",
            ["Automation:Runtime:ConnectorOutboxPollInterval"] = "00:00:00.456",
            ["Automation:Runtime:LegacyBackgroundQueuePollInterval"] = "00:00:00.789",
            ["Automation:Runtime:MessageDispatchBatchSize"] = "11",
            ["Automation:Runtime:ConnectorOutboxBatchSize"] = "17",
            ["Automation:Runtime:DeliveryLeaseDuration"] = "00:03:00",
            ["Automation:Runtime:ConnectorCommandLeaseDuration"] = "00:04:00",
            ["Automation:Runtime:WorkerFailureBackoff"] = "00:00:09",
            ["Automation:Runtime:Mqtt:Enabled"] = "true",
            ["Automation:Runtime:Mqtt:ClientId"] = "integration-bridge",
            ["Automation:Runtime:Mqtt:Host"] = "mqtt.example.test",
            ["Automation:Runtime:Mqtt:Port"] = "2883",
            ["Automation:Runtime:Mqtt:TopicPrefix"] = "custom/runtime"
        };

        await using var provider = await BuildProviderAsync(profile, configurationOverrides: overrides);
        await using var scope = provider.CreateAsyncScope();
        var runtimeOptions = scope.ServiceProvider.GetRequiredService<IOptions<AutomationRuntimeOptions>>().Value;

        Assert.Equal(TimeSpan.FromMilliseconds(123), runtimeOptions.MessagePollInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(456), runtimeOptions.ConnectorOutboxPollInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(789), runtimeOptions.LegacyBackgroundQueuePollInterval);
        Assert.Equal(11, runtimeOptions.MessageDispatchBatchSize);
        Assert.Equal(17, runtimeOptions.ConnectorOutboxBatchSize);
        Assert.Equal(TimeSpan.FromMinutes(3), runtimeOptions.DeliveryLeaseDuration);
        Assert.Equal(TimeSpan.FromMinutes(4), runtimeOptions.ConnectorCommandLeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(9), runtimeOptions.WorkerFailureBackoff);
        Assert.True(runtimeOptions.Mqtt.Enabled);
        Assert.Equal("integration-bridge", runtimeOptions.Mqtt.ClientId);
        Assert.Equal("mqtt.example.test", runtimeOptions.Mqtt.Host);
        Assert.Equal(2883, runtimeOptions.Mqtt.Port);
        Assert.Equal("custom/runtime", runtimeOptions.Mqtt.TopicPrefix);
    }

    [Fact]
    public async Task Automation_mqtt_bridge_reads_production_configuration_without_test_only_overrides()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-mqtt-config-bridge");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var logSink = new TestLoggerSink();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton<ILoggerProvider>(new TestLoggerProvider(logSink));
            },
            new Dictionary<string, string?>
            {
                ["Automation:Runtime:Mqtt:Enabled"] = "true",
                ["Automation:Runtime:Mqtt:Host"] = string.Empty,
                ["Automation:Runtime:Mqtt:ClientId"] = "bridge-from-config",
                ["Automation:Runtime:Mqtt:TopicPrefix"] = "config/runtime"
            });

        await using var scope = provider.CreateAsyncScope();
        var bridge = scope.ServiceProvider.GetServices<IAutomationTelemetryBridge>()
            .OfType<MqttAutomationTelemetryBridge>()
            .Single();

        await bridge.PublishAsync(
            new AutomationTelemetryEvent(
                AutomationExecutionLogKind.Published,
                "automation-envelope",
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid(),
                null,
                "Bridge configuration test.",
                "{}"),
            CancellationToken.None);

        Assert.Contains(
            logSink.Entries,
            entry =>
                entry.LogLevel == LogLevel.Warning &&
                entry.Message.Contains("Automation MQTT telemetry is enabled but no host is configured.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Concurrent_message_publish_with_same_dedupe_key_returns_single_envelope()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-publish-concurrency");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        var envelopeIds = await RunConcurrentlyAsync(
            8,
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
                return await publisher.PublishAsync(
                    new TestOperationalEnvelope("shared-dedupe"),
                    new AutomationPublishOptions(DedupeKey: "shared-dedupe-key"));
            });

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.Single(envelopeIds.Distinct());
        Assert.Equal(
            1,
            await dbContext.Set<AutomationEnvelopeRecord>()
                .CountAsync(item =>
                    item.EnvelopeType == AutomationEnvelopeTypeNames.For<TestOperationalEnvelope>() &&
                    item.DedupeKey == "shared-dedupe-key"));
    }

    [Fact]
    public async Task Concurrent_ingress_accept_with_same_external_message_returns_single_envelope()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-ingress-concurrency");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        var results = await RunConcurrentlyAsync(
            8,
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IPluginIngressInbox>();
                return await inbox.AcceptAsync(new PluginIngressEnvelopeRequest(
                    "email",
                    "crm-sync",
                    "shared-message",
                    "cursor-01",
                    """{"subject":"hello"}"""));
            });

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.Single(results.Select(item => item.EnvelopeId).Distinct());
        Assert.Equal(1, await dbContext.Set<PluginIngressEnvelopeRecord>().CountAsync());
        Assert.Equal(7, results.Count(item => item.IsDuplicate));
    }

    [Fact]
    public async Task Concurrent_connector_enqueue_with_same_idempotency_key_returns_single_command()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-outbox-concurrency");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        await using var provider = await BuildProviderAsync(profile);

        Guid projectId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            projectId = await CreateProjectAsync(projectsService, "Concurrent outbox dedupe");
        }

        var results = await RunConcurrentlyAsync(
            8,
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
                return await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
                    projectId,
                    WebhookResourceConnectorPlugin.PluginKey,
                    "deliver",
                    """{"endpointUrl":"https://example.com/hooks/concurrency"}""",
                    "shared-idempotency-key",
                    "integration-tests"));
            });

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.Single(results.Select(item => item.CommandId).Distinct());
        Assert.Equal(
            1,
            await dbContext.Set<ConnectorCommandRecord>()
                .CountAsync(item =>
                    item.ProjectId == projectId &&
                    item.ConnectorPluginKey == WebhookResourceConnectorPlugin.PluginKey &&
                    item.CommandKey == "deliver" &&
                    item.IdempotencyKey == "shared-idempotency-key"));
        Assert.Equal(7, results.Count(item => item.IsDuplicate));
    }

    [Fact]
    public async Task Parallel_dispatchers_do_not_process_the_same_delivery_twice()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-parallel-dispatchers");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, SlowSuccessfulOperationalHandler>();
            });

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(new TestOperationalEnvelope("parallel-dispatch"));
        }

        var results = await RunConcurrentlyAsync(
            2,
            () => DispatchPendingAsync(provider, 1));

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var attempts = await dbContext.Set<AutomationDeliveryAttemptRecord>()
            .Where(item => item.EnvelopeId == envelopeId)
            .ToListAsync();

        Assert.Equal(1, results.Sum());
        Assert.Single(sink.Messages);
        Assert.Single(attempts);
    }

    [Fact]
    public async Task Parallel_connector_outbox_workers_do_not_process_the_same_command_twice()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-parallel-outbox-workers");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var handler = new TestConnectorCommandHandler(
            TimeSpan.FromMilliseconds(100),
            ConnectorCommandExecutionResult.Completed("""{"delivery":"parallel"}"""));

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
            var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
            var projectId = await CreateProjectAsync(projectsService, "Parallel outbox worker");
            commandId = (await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
                projectId,
                WebhookResourceConnectorPlugin.PluginKey,
                "deliver",
                """{"endpointUrl":"https://example.com/hooks/parallel"}""",
                "parallel-command",
                "integration-tests"))).CommandId;
        }

        var results = await RunConcurrentlyAsync(
            2,
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
                return await outbox.ProcessPendingAsync(1, TimeSpan.FromMinutes(1));
            });

        var snapshot = await GetConnectorSnapshotAsync(provider, commandId);

        Assert.Equal(1, results.Sum());
        Assert.NotNull(snapshot);
        Assert.Equal(ConnectorCommandStatus.Completed, snapshot!.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Direct_process_async_claims_a_lease_before_execution()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-direct-process-lease");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var handler = new TestConnectorCommandHandler(
            TimeSpan.FromMilliseconds(150),
            ConnectorCommandExecutionResult.Completed("""{"delivery":"direct"}"""));

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
            var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
            var projectId = await CreateProjectAsync(projectsService, "Direct process lease");
            commandId = (await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
                projectId,
                WebhookResourceConnectorPlugin.PluginKey,
                "deliver",
                """{"endpointUrl":"https://example.com/hooks/direct"}""",
                "direct-process-command",
                "integration-tests"))).CommandId;
        }

        await using var processingScope = provider.CreateAsyncScope();
        var processingOutbox = processingScope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
        var processTask = processingOutbox.ProcessAsync(commandId);
        await WaitForAsync(
            async () => await HasActiveConnectorLeaseAsync(provider, commandId),
            TimeSpan.FromSeconds(5));
        var status = await processTask;
        var snapshot = await GetConnectorSnapshotAsync(provider, commandId);

        Assert.Equal(ConnectorCommandStatus.Completed, status);
        Assert.NotNull(snapshot);
        Assert.Equal(ConnectorCommandStatus.Completed, snapshot!.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Concurrent_direct_process_calls_do_not_execute_the_same_command_twice()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-concurrent-direct-process");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var handler = new TestConnectorCommandHandler(
            TimeSpan.FromMilliseconds(150),
            ConnectorCommandExecutionResult.Completed("""{"delivery":"direct-parallel"}"""));

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
            var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
            var projectId = await CreateProjectAsync(projectsService, "Concurrent direct process");
            commandId = (await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
                projectId,
                WebhookResourceConnectorPlugin.PluginKey,
                "deliver",
                """{"endpointUrl":"https://example.com/hooks/direct-parallel"}""",
                "direct-process-parallel-command",
                "integration-tests"))).CommandId;
        }

        var results = await RunConcurrentlyAsync(
            2,
            async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
                return await outbox.ProcessAsync(commandId);
            });

        var snapshot = await GetConnectorSnapshotAsync(provider, commandId);

        Assert.Contains(ConnectorCommandStatus.Completed, results);
        Assert.Single(handler.Requests);
        Assert.NotNull(snapshot);
        Assert.Equal(ConnectorCommandStatus.Completed, snapshot!.Status);
    }

    [Fact]
    public async Task Abandoned_delivery_lease_can_be_reclaimed()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-reclaim-lease");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, SuccessfulOperationalHandler>();
            });

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(new TestOperationalEnvelope("reclaim-me"));
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var staleDelivery = await dbContext.Set<AutomationEnvelopeDeliveryRecord>()
                .SingleAsync(item => item.EnvelopeId == envelopeId);
            var staleTime = DateTimeOffset.UtcNow.AddMinutes(-10);
            staleDelivery.State = AutomationDeliveryState.Running;
            staleDelivery.AttemptCount = 1;
            staleDelivery.LastAttemptAtUtc = staleTime;
            staleDelivery.AvailableAtUtc = staleTime;
            staleDelivery.LockedAtUtc = staleTime;
            staleDelivery.LockToken = "stale-lease";
            staleDelivery.UpdatedAtUtc = staleTime;
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(1, await DispatchPendingAsync(provider, 1));

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var verificationContext = await verificationFactory.CreateDbContextAsync();
        var delivery = await verificationContext.Set<AutomationEnvelopeDeliveryRecord>()
            .SingleAsync(item => item.EnvelopeId == envelopeId);

        Assert.Single(sink.Messages);
        Assert.Equal(AutomationDeliveryState.Completed, delivery.State);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Equal(string.Empty, delivery.LockToken);
        Assert.Null(delivery.LockedAtUtc);
    }

    [Fact]
    public async Task Automation_message_pump_worker_continues_after_transient_dispatch_failure()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-worker-dispatch-failure");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();
        var clockState = new ArmedThrowOnceClockState();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddHostedService<AutomationMessagePumpWorker>();
                services.AddSingleton(sink);
                services.AddScoped<IAutomationMessageHandler, SuccessfulOperationalHandler>();
                services.AddSingleton(clockState);
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(serviceProvider => new ThrowOnceArmedClock(serviceProvider.GetRequiredService<ArmedThrowOnceClockState>()));
            });

        Guid envelopeId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IAutomationMessagePublisher>();
            envelopeId = await publisher.PublishAsync(new TestOperationalEnvelope("worker-survives-dispatch-failure"));
        }

        clockState.Arm();

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(
            async () => await GetEnvelopeStateAsync(provider, envelopeId) == AutomationEnvelopeState.Completed,
            TimeSpan.FromSeconds(10));

        Assert.Equal(1, clockState.FailureCount);
        Assert.Single(sink.Messages);
    }

    [Fact]
    public async Task Connector_outbox_worker_continues_after_transient_processing_failure()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-worker-outbox-failure");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var handler = new TestConnectorCommandHandler(ConnectorCommandExecutionResult.Completed("""{"delivery":"worker"}"""));
        var clockState = new ArmedThrowOnceClockState();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddHostedService<ConnectorOutboxDrainWorker>();
                services.AddSingleton(handler);
                services.AddSingleton<IConnectorCommandHandler>(serviceProvider => serviceProvider.GetRequiredService<TestConnectorCommandHandler>());
                services.AddSingleton(clockState);
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(serviceProvider => new ThrowOnceArmedClock(serviceProvider.GetRequiredService<ArmedThrowOnceClockState>()));
            });

        Guid commandId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
            var projectId = await CreateProjectAsync(projectsService, "Outbox worker failure");
            commandId = (await outbox.EnqueueAsync(new ConnectorCommandEnqueueRequest(
                projectId,
                WebhookResourceConnectorPlugin.PluginKey,
                "deliver",
                """{"endpointUrl":"https://example.com/hooks/worker"}""",
                "worker-outbox-command",
                "integration-tests"))).CommandId;
        }

        clockState.Arm();

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(
            async () => (await GetConnectorSnapshotAsync(provider, commandId))?.Status == ConnectorCommandStatus.Completed,
            TimeSpan.FromSeconds(10));

        Assert.Equal(1, clockState.FailureCount);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Legacy_background_queue_items_are_forwarded_to_durable_runtime_when_legacy_mode_is_enabled()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("automation-runtime-legacy-queue-forward");
        var profile = testEnvironment.CreateManagedSqliteProfile("primary");
        var sink = new MessageSink();
        var correlationId = Guid.NewGuid();

        await using var provider = await BuildProviderAsync(
            profile,
            services =>
            {
                services.AddSingleton(sink);
                services.AddScoped<IAutomationBackgroundJobHandler, BackgroundJobCaptureHandler>();
            });

        await using (var scope = provider.CreateAsyncScope())
        {
            var backgroundJobQueue = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();
            await backgroundJobQueue.EnqueueAsync(new BackgroundJobRequest(
                BackgroundJobCaptureHandler.JobTypeValue,
                correlationId,
                "Legacy queued job",
                new Dictionary<string, string>
                {
                    ["origin"] = "legacy"
                }));
        }

        await using var hostedServices = await HostedServiceHarness.StartAsync(provider);
        await WaitForAsync(
            () => Task.FromResult(sink.BackgroundJobRequests.Count > 0),
            TimeSpan.FromSeconds(10));

        await using var verificationScope = provider.CreateAsyncScope();
        var dbContextFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var backgroundJob = await dbContext.Set<BackgroundJobRecord>()
            .SingleAsync(item => item.CorrelationId == correlationId);

        Assert.Single(sink.BackgroundJobRequests);
        Assert.Equal(BackgroundJobCaptureHandler.JobTypeValue, sink.BackgroundJobRequests.Single().JobType);
        Assert.Equal(BackgroundJobCaptureHandler.JobTypeValue, backgroundJob.JobType);
        Assert.Equal(BackgroundJobState.Succeeded.ToString(), backgroundJob.State);
    }

    private static async Task<IReadOnlyList<T>> RunConcurrentlyAsync<T>(
        int workerCount,
        Func<Task<T>> action)
    {
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(async () =>
            {
                await startGate.Task;
                return await action();
            }))
            .ToArray();

        startGate.SetResult();
        return await Task.WhenAll(tasks);
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

        private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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

    private sealed class CountingMaterializer(TimeSpan delay) : IPluginIngressMaterializer
    {
        private readonly TimeSpan _delay = delay;
        private int _callCount;

        public const string Key = "test.counting";

        public string MaterializerKey => Key;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<PluginIngressMaterializationResult> MaterializeAsync(
            PluginIngressEnvelopeSnapshot envelope,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken);
            }

            return PluginIngressMaterializationResult.Success("counted materialization");
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

    private sealed class SlowSuccessfulOperationalHandler(MessageSink sink) : AutomationMessageHandler<TestOperationalEnvelope>
    {
        protected override async Task<AutomationMessageHandleResult> HandleAsync(
            TestOperationalEnvelope envelope,
            AutomationMessageContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            sink.Messages.Enqueue(new MessageCapture(
                HandlerKey,
                envelope.Value,
                context.EnvelopeId,
                context.CorrelationId,
                context.CausationId));
            return AutomationMessageHandleResult.Completed();
        }
    }

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

    private sealed class ArmedThrowOnceClockState
    {
        private int _armed;
        private int _failures;

        public int FailureCount => Volatile.Read(ref _failures);

        public void Arm()
        {
            Interlocked.Exchange(ref _armed, 1);
        }

        public bool TryConsumeFailure()
        {
            if (Interlocked.Exchange(ref _armed, 0) != 1)
            {
                return false;
            }

            Interlocked.Increment(ref _failures);
            return true;
        }
    }

    private sealed class ThrowOnceArmedClock(ArmedThrowOnceClockState state) : IClock
    {
        private readonly IClock _innerClock = new SystemClock();

        public DateTimeOffset GetUtcNow()
        {
            if (state.TryConsumeFailure())
            {
                throw new InvalidOperationException("Injected transient clock failure.");
            }

            return _innerClock.GetUtcNow();
        }
    }

    private sealed class TestLoggerSink
    {
        public ConcurrentQueue<TestLogEntry> Entries { get; } = new();
    }

    private sealed record TestLogEntry(
        string Category,
        LogLevel LogLevel,
        string Message);

    private sealed class TestLoggerProvider(TestLoggerSink sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(categoryName, sink);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger(
        string categoryName,
        TestLoggerSink sink) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NoOpDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Entries.Enqueue(new TestLogEntry(
                categoryName,
                logLevel,
                formatter(state, exception)));
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
        private readonly TimeSpan _delay;

        public TestConnectorCommandHandler(params ConnectorCommandExecutionResult[] results)
            : this(TimeSpan.Zero, results)
        {
        }

        public TestConnectorCommandHandler(TimeSpan delay, params ConnectorCommandExecutionResult[] results)
        {
            _delay = delay;
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
            return ExecuteAsyncCore(cancellationToken);
        }

        private async Task<ConnectorCommandExecutionResult> ExecuteAsyncCore(CancellationToken cancellationToken)
        {
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken);
            }

            return _results.Count > 0
                ? _results.Dequeue()
                : ConnectorCommandExecutionResult.Completed("""{"delivery":"default"}""");
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
