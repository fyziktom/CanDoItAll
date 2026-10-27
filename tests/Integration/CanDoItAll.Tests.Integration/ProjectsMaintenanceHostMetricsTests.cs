using System.Runtime.ExceptionServices;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Tests.Maintenance;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectsMaintenanceHostMetricsTests {
    [Fact]
    [Trait("Category", "HostPlatform")]
    [Trait("Category", "LiveProcess")]
    public async Task Registered_pools_and_receipt_families_keep_the_definition_worker_observable() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var resources = await ProjectsMaintenanceCapacityResources.CreateAsync(5, 1, false, 0, deadline.Token);
        CanDoItAllTestEnvironment? environment = null;
        TestApplication? app = null;
        Exception? failure = null;
        try {
            environment = resources.CreateEnvironment();
            await resources.RecordEnvironmentAsync(environment, deadline.Token);
            var profile = resources.CreateProfile(environment);
            await resources.RecordDatabaseAsync(profile, deadline.Token);
            app = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile });
            var database = app.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
            var binding = await app.Services.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(database.Id, deadline.Token);
            await using var worker = new ProjectsMaintenanceCapacityWorker(app, resources, database.Id, resources.Run, 1, 5);
            await worker.LaunchAsync(binding, deadline.Token);
            await worker.StartAsync(deadline.Token);
            while (worker.Accounting.Snapshot().Count(frame => frame.Cycle is not null) < 2) {
                worker.ThrowIfFailed();
                await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
            }
            await worker.StopAsync(deadline.Token);
            var observations = worker.Accounting.Snapshot();
            Assert.Equal(MaintenanceFrameKind.Stopped, observations[^1].Kind);
            Assert.DoesNotContain(observations, frame => frame.Kind == MaintenanceFrameKind.Failure);
            Assert.Equal(0, worker.Accounting.ConfirmedRows);
            Assert.Equal(0, worker.Accounting.ConfirmedBytes);
        } catch (Exception exception) {
            failure = exception;
        } finally {
            failure = await resources.CleanupAsync(app, environment, failure);
        }
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
