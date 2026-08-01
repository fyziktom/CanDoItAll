using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class WatchSupervisorServiceIntegrationTests
{
    [Fact]
    public async Task ProcessWatchLineAsync_confirms_runtime_readiness_with_matching_iteration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false",
                ["Manager:ReadinessTimeoutSeconds"] = "1",
                ["Manager:ReadinessUrls:0"] = "http://127.0.0.1:5188/_dev/runtime"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["http://127.0.0.1:5188"]))),
            configuration);

        await service.ProcessWatchLineAsync("Building");
        await service.ProcessWatchLineAsync("Now listening on: http://127.0.0.1:5188");

        var snapshot = await service.WaitForReadyAsync(0, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(WatchState.Ready, snapshot!.State);
        Assert.Equal(1, snapshot.ExpectedWatchIteration);
        Assert.Equal(1, snapshot.ConfirmedWatchIteration);
    }

    [Fact]
    public async Task ProcessWatchLineAsync_keeps_multiple_listening_urls_on_the_same_startup_iteration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false",
                ["Manager:ReadinessTimeoutSeconds"] = "1",
                ["Manager:ReadinessUrls:0"] = "https://localhost:7271/_dev/runtime"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["https://localhost:7271", "http://localhost:5032"]))),
            configuration);

        await service.ProcessWatchLineAsync("Building");
        await service.ProcessWatchLineAsync("Now listening on: https://localhost:7271");
        await service.ProcessWatchLineAsync("Now listening on: http://localhost:5032");

        var snapshot = service.GetStatus();

        Assert.Equal(WatchState.Ready, snapshot.State);
        Assert.Equal(1, snapshot.ExpectedWatchIteration);
        Assert.Equal(1, snapshot.ConfirmedWatchIteration);
        Assert.Contains("https://localhost:7271", snapshot.ActiveUrls);
        Assert.Contains("http://localhost:5032", snapshot.ActiveUrls);
    }

    [Fact]
    public async Task ProcessWatchLineAsync_treats_stderr_progress_lines_as_non_error_logs()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["http://127.0.0.1:5188"]))),
            configuration);

        await service.ProcessWatchLineAsync("dotnet watch : Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.", isError: true);

        var log = Assert.Single(service.GetLogs(1));
        Assert.False(log.IsError);
    }

    [Fact]
    public async Task ProcessWatchLineAsync_waits_for_a_listening_url_before_probing_runtime_readiness()
    {
        var handler = new RuntimeProbeHandler(new RuntimeProbeSnapshot(true, "Ready", 1, ["http://127.0.0.1:5188"]));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Manager:AutoStartWatch"] = "false",
                ["Manager:ReadinessTimeoutSeconds"] = "1",
                ["Manager:ReadinessUrls:0"] = "http://127.0.0.1:5188/_dev/runtime"
            })
            .Build();

        var service = new WatchSupervisorService(
            NullLogger<WatchSupervisorService>.Instance,
            new FakeHttpClientFactory(handler),
            configuration);

        await service.ProcessWatchLineAsync("Building");
        await service.ProcessWatchLineAsync("dotnet watch : Waiting for changes", isError: true);

        Assert.Equal(0, handler.RequestCount);

        await service.ProcessWatchLineAsync("Now listening on: http://127.0.0.1:5188");

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(WatchState.Ready, service.GetStatus().State);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RuntimeProbeHandler(RuntimeProbeSnapshot snapshot) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(snapshot) });
        }
    }
}
