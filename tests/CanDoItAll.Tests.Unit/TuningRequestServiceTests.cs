using System.Threading.Channels;
using CanDoItAll.Manager;
using Microsoft.Extensions.Configuration;

namespace CanDoItAll.Tests.Unit;

public sealed class TuningRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_requires_known_capsule()
    {
        var service = CreateService(
            new FakeWatchSupervisor(),
            new FakeCapsuleCatalogService { Coverage = HealthyCoverage });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new TuningRequestCreateModel("missing", "ProjectsPage", "/projects", null, null, null, "Tighten spacing")));

        Assert.Contains("capsule key", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_marks_request_verification_failed_when_capsule_drift_exists()
    {
        var watchSupervisor = new FakeWatchSupervisor
        {
            ReadySnapshot = new WatchStatusSnapshot(WatchState.Ready, "Ready", 8, 2, 1, 1, DateTimeOffset.UtcNow, ["http://127.0.0.1:5188"])
        };
        var capsules = new FakeCapsuleCatalogService
        {
            Coverage = new CapsuleCoverageSummary(4, 2, 0, 1, 1, ["missing.cs"], ["broken.razor: Missing required fields"], DateTimeOffset.UtcNow)
        };
        capsules.Records["page-projectspage"] = SampleCapsule;

        var service = CreateService(watchSupervisor, capsules);
        var record = await service.CreateAsync(
            new TuningRequestCreateModel("page-projectspage", "ProjectsPage", "/projects", Guid.NewGuid(), "route:/projects", null, "Update spacing", AutoSubmit: true));

        var completed = await WaitForStatusAsync(service, record.Id, TuningRequestStatus.VerificationFailed);
        Assert.True(completed.CapsuleDriftDetected);
        Assert.NotNull(completed.ReadyWatchEventId);
        Assert.Contains("Capsule drift detected", completed.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<TuningRequestRecord> WaitForStatusAsync(TuningRequestService service, Guid requestId, TuningRequestStatus expectedStatus)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var current = service.Get(requestId);
            if (current?.Status == expectedStatus)
            {
                return current;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Timed out waiting for status {expectedStatus}.");
    }

    private static TuningRequestService CreateService(IWatchSupervisor watchSupervisor, ICapsuleCatalogService capsuleCatalogService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:TuningModeEnabled"] = "true",
                ["Manager:ReviewBeforeSend"] = "false"
            })
            .Build();

        return new TuningRequestService(configuration, watchSupervisor, capsuleCatalogService);
    }

    private static CapsuleCoverageSummary HealthyCoverage => new(1, 1, 0, 0, 0, [], [], DateTimeOffset.UtcNow);

    private static CapsuleRecord SampleCapsule => new(
        "page-projectspage",
        "src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor",
        "page",
        "ProjectsPage",
        "Projects workspace page.",
        "project-list",
        "ProjectsService",
        "stale-selection",
        "component:ProjectsPageTests",
        DateTimeOffset.UtcNow);

    private sealed class FakeWatchSupervisor : IWatchSupervisor
    {
        public WatchStatusSnapshot ReadySnapshot { get; init; } = new(WatchState.Ready, "Ready", 1, 1, 1, 1, DateTimeOffset.UtcNow, []);

        public IReadOnlyList<WatchLogEntry> GetLogs(int take = 200) => [];

        public WatchStatusSnapshot GetStatus() => ReadySnapshot;

        public Task ProcessWatchLineAsync(string line, bool isError = false, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ChannelReader<WatchEvent> Subscribe(out Guid subscriptionId)
        {
            subscriptionId = Guid.NewGuid();
            return Channel.CreateUnbounded<WatchEvent>().Reader;
        }

        public void Unsubscribe(Guid subscriptionId)
        {
        }

        public Task<WatchStatusSnapshot?> WaitForReadyAsync(long afterEventId, TimeSpan timeout, CancellationToken cancellationToken)
            => Task.FromResult<WatchStatusSnapshot?>(ReadySnapshot);
    }

    private sealed class FakeCapsuleCatalogService : ICapsuleCatalogService
    {
        public Dictionary<string, CapsuleRecord> Records { get; } = new(StringComparer.OrdinalIgnoreCase);

        public CapsuleCoverageSummary Coverage { get; init; } = HealthyCoverage;

        public IReadOnlyList<CapsuleRecord> GetChangedSince(DateTimeOffset sinceUtc) => Records.Values.ToList();

        public CapsuleCoverageSummary GetCoverage() => Coverage;

        public IReadOnlyList<CapsuleRecord> GetIndex() => Records.Values.ToList();

        public CapsuleRecord? GetSymbol(string symbolId) => Records.GetValueOrDefault(symbolId);

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
