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

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RuntimeProbeHandler(RuntimeProbeSnapshot snapshot) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(snapshot) });
    }
}
