using System.Collections.Concurrent;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

namespace CanDoItAll.Tests.Integration;

public sealed partial class SchedulerWorkspaceOwnerPersistenceTests {
    private const string QuartzProcessorCategory = "Quartz.ContainerConfigurationProcessor";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quartz_rebinds_current_owner_logging_before_lazy_resolution_after_prior_host_disposal(bool buildNextHostBeforeDisposal) {
        await using var environment = CanDoItAllTestEnvironment.Create("scheduler-quartz-logger-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var firstLogs = new QuartzOwnerLogs();
        var nextLogs = new QuartzOwnerLogs();
        var plan = CreatePlan();
        plan.StartAtUtc = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());
        ILoggerFactory firstFactory;
        ITypeLoadHelper firstHelper;
        TestApplication? next = null;
        try {
            await using (var first = await TestApplication.CreateAsync(LoggingHarness(firstLogs))) {
                firstFactory = first.Services.GetRequiredService<ILoggerFactory>();
                await using (var database = await first.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync()) {
                    database.Add(plan);
                    await database.SaveChangesAsync();
                }
                await using var scope = first.Services.CreateAsyncScope();
                var projection = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerTriggerScheduler>();
                firstHelper = scope.ServiceProvider.GetRequiredService<ITypeLoadHelper>();
                Assert.IsType<SimpleTypeLoadHelper>(firstHelper);
                Assert.Same(firstHelper, first.Services.GetRequiredService<ITypeLoadHelper>());
                var scheduler = await scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
                try {
                    await projection.SynchronizePlanAsync(plan.Id);
                    await AssertQuartzProjectionAsync(first, scheduler, plan);
                    AssertOwnerLogging(firstLogs, plan.Id);
                    if (buildNextHostBeforeDisposal) {
                        next = await TestApplication.CreateAsync(LoggingHarness(nextLogs));
                        Assert.DoesNotContain(QuartzProcessorCategory, nextLogs.Categories);
                    }
                } finally {
                    await scheduler.Shutdown(waitForJobsToComplete: true);
                }
            }
            Assert.True(firstLogs.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => firstFactory.CreateLogger("disposed-owner-probe"));
            var completedFirstLogs = firstLogs.Events.ToArray();
            var completedFirstCategories = firstLogs.Categories.ToArray();
            next ??= await TestApplication.CreateAsync(LoggingHarness(nextLogs));
            Assert.NotSame(firstFactory, next.Services.GetRequiredService<ILoggerFactory>());
            await using var nextScope = next.Services.CreateAsyncScope();
            var nextProjection = nextScope.ServiceProvider.GetRequiredService<ISchedulerPlannerTriggerScheduler>();
            var nextHelper = nextScope.ServiceProvider.GetRequiredService<ITypeLoadHelper>();
            Assert.IsType<SimpleTypeLoadHelper>(nextHelper);
            Assert.NotSame(firstHelper, nextHelper);
            Assert.Same(nextHelper, next.Services.GetRequiredService<ITypeLoadHelper>());
            Assert.Contains(QuartzProcessorCategory, nextLogs.Categories);
            var nextScheduler = await nextScope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            try {
                await nextProjection.SynchronizePlanAsync(plan.Id);
                await AssertQuartzProjectionAsync(next, nextScheduler, plan);
                AssertOwnerLogging(nextLogs, plan.Id);
                Assert.Equal(completedFirstLogs, firstLogs.Events.ToArray());
                Assert.Equal(completedFirstCategories, firstLogs.Categories.ToArray());
            } finally {
                await nextScheduler.Shutdown(waitForJobsToComplete: true);
            }
        } finally {
            if (next is not null) {
                await next.DisposeAsync();
            }
        }
        Assert.True(nextLogs.IsDisposed);

        TestHarnessOptions LoggingHarness(QuartzOwnerLogs logs) => new() {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigureServices = services => {
                services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
                services.AddSingleton<ILoggerProvider>(_ => logs);
            }
        };
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    public async Task Scheduler_module_preserves_custom_type_loader_factory_and_lifetime_on_repeat_registration(ServiceLifetime lifetime) {
        IServiceCollection services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        var created = new List<ITypeLoadHelper>();
        var custom = new ServiceDescriptor(typeof(ITypeLoadHelper), _ => {
            var helper = new SimpleTypeLoadHelper();
            created.Add(helper);
            return helper;
        }, lifetime);
        services.Add(custom);
        services.AddSchedulerPlannerModule(configuration);
        services.AddSchedulerPlannerModule(configuration);
        Assert.Same(custom, Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITypeLoadHelper)));
        await using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITypeLoadHelper>();
        var second = provider.GetRequiredService<ITypeLoadHelper>();
        Assert.Same(created[0], first);
        if (lifetime == ServiceLifetime.Singleton) {
            Assert.Same(first, second);
            Assert.Single(created);
        } else {
            Assert.NotSame(first, second);
            Assert.Equal(2, created.Count);
            Assert.Same(created[1], second);
        }
        first.Initialize();
        Assert.Equal(typeof(SchedulerPlannerQuartzJob), first.LoadType(typeof(SchedulerPlannerQuartzJob).AssemblyQualifiedName));
    }

    private static async Task AssertQuartzProjectionAsync(TestApplication application, IScheduler scheduler, SchedulerPlan original) {
        Assert.False(scheduler.IsStarted);
        Assert.False(scheduler.IsShutdown);
        var jobKey = new JobKey($"plan-{original.Id:N}", "candoitall-scheduler-planner");
        var job = Assert.IsAssignableFrom<IJobDetail>(await scheduler.GetJobDetail(jobKey));
        Assert.Equal(typeof(SchedulerPlannerQuartzJob), job.JobType);
        Assert.Equal(original.Id.ToString("N"), job.JobDataMap.GetString(SchedulerPlannerQuartzJob.PlanIdKey));
        Assert.Equal(original.SchedulerTriggerId.ToString("N"), job.JobDataMap.GetString(SchedulerPlannerQuartzJob.SchedulerTriggerIdKey));
        Assert.Equal(original.SchedulerTriggerKey, job.JobDataMap.GetString(SchedulerPlannerQuartzJob.SchedulerTriggerKeyKey));
        var trigger = Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(await scheduler.GetTriggersOfJob(jobKey)));
        Assert.Equal(new TriggerKey($"plan-{original.Id:N}", "candoitall-scheduler-planner-runtime"), trigger.Key);
        Assert.Equal(jobKey, trigger.JobKey);
        Assert.Equal(original.CronExpression, trigger.CronExpressionString);
        Assert.Equal(original.TimeZoneId, trigger.TimeZone.Id);
        Assert.Equal(original.StartAtUtc, trigger.StartTimeUtc);
        Assert.NotNull(trigger.GetNextFireTimeUtc());
        await using var database = await application.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        var saved = await database.Set<SchedulerPlan>().AsNoTracking().SingleAsync(row => row.Id == original.Id);
        Assert.Equal(original.TargetId, saved.TargetId);
        Assert.Equal(original.TargetVersionId, saved.TargetVersionId);
        Assert.Equal(original.SchedulerTriggerId, saved.SchedulerTriggerId);
        Assert.Equal(original.SchedulerTriggerKey, saved.SchedulerTriggerKey);
        Assert.Equal(original.StartAtUtc, saved.StartAtUtc);
        Assert.Equal(original.CronExpression, saved.CronExpression);
        Assert.Equal(original.InputJson, saved.InputJson);
        Assert.Equal(original.StructureAuthorityJson, saved.StructureAuthorityJson);
        Assert.Equal(trigger.GetNextFireTimeUtc(), saved.NextPlannedFireAtUtc);
        Assert.False(await database.Set<SchedulerPlanRun>().AnyAsync(row => row.PlanId == original.Id));
    }

    private static void AssertOwnerLogging(QuartzOwnerLogs logs, Guid planId) {
        Assert.False(logs.IsDisposed);
        Assert.Contains(QuartzProcessorCategory, logs.Categories);
        Assert.Contains(logs.Events, entry => entry.Category == QuartzProcessorCategory);
        Assert.Contains(logs.Events, entry => entry.Category == typeof(SchedulerPlannerTriggerScheduler).FullName &&
            entry.Message.Contains(planId.ToString(), StringComparison.Ordinal));
    }

    private sealed class QuartzOwnerLogs : ILoggerProvider {
        public ConcurrentQueue<string> Categories { get; } = new();
        public ConcurrentQueue<(string Category, string Message)> Events { get; } = new();
        public bool IsDisposed { get; private set; }

        public ILogger CreateLogger(string categoryName) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Categories.Enqueue(categoryName);
            return new OwnerLogger(this, categoryName);
        }

        public void Dispose() {
            IsDisposed = true;
        }

        private sealed class OwnerLogger(QuartzOwnerLogs owner, string category) : ILogger {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
                owner.Events.Enqueue((category, formatter(state, exception)));
            }
        }
    }
}
