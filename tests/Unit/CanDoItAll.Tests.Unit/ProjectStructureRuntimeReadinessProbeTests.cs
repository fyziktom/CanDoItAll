using System.Net;
using System.Security.Authentication;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureRuntimeReadinessProbeTests
{
    private static readonly Uri AppUrl = new("http://localhost:5149/");

    [Fact]
    public async Task Returns_serving_once_the_loopback_url_answers()
    {
        var handler = new SequenceHandler(
            _ => throw new HttpRequestException("Connection refused."),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var launcher = new FakeLauncher();
        var probe = new ProjectStructureRuntimeReadinessProbe(new HandlerClientFactory(handler));

        var readiness = await probe.WaitUntilServingAsync(launcher, "node-1", AppUrl, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.True(readiness.IsServing, readiness.Message);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Reports_the_process_exit_instead_of_waiting_for_the_url()
    {
        var launcher = new FakeLauncher
        {
            Exit = new ProjectStructureRuntimeExitRecord(
                4242,
                1,
                DateTimeOffset.Parse("2026-09-26T12:00:00Z"),
                DateTimeOffset.Parse("2026-09-26T12:00:01Z"),
                "error CS1002: ; expected")
        };
        var probe = new ProjectStructureRuntimeReadinessProbe(new HandlerClientFactory(
            new SequenceHandler(_ => throw new HttpRequestException("Connection refused."))));

        var readiness = await probe.WaitUntilServingAsync(launcher, "node-1", AppUrl, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Equal(ProjectStructureRuntimeReadinessStatus.Exited, readiness.Status);
        Assert.Contains("code 1", readiness.Message, StringComparison.Ordinal);
        Assert.Contains("CS1002", readiness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Times_out_while_the_process_keeps_running_without_serving()
    {
        var probe = new ProjectStructureRuntimeReadinessProbe(new HandlerClientFactory(
            new SequenceHandler(_ => throw new HttpRequestException("Connection refused."))));

        var readiness = await probe.WaitUntilServingAsync(
            new FakeLauncher(),
            "node-1",
            AppUrl,
            TimeSpan.FromMilliseconds(300),
            CancellationToken.None);

        Assert.Equal(ProjectStructureRuntimeReadinessStatus.TimedOut, readiness.Status);
    }

    [Fact]
    public async Task Treats_a_tls_handshake_failure_as_a_listening_application()
    {
        var probe = new ProjectStructureRuntimeReadinessProbe(new HandlerClientFactory(
            new SequenceHandler(_ => throw new HttpRequestException(
                "The SSL connection could not be established.",
                new AuthenticationException("The remote certificate is invalid.")))));

        var readiness = await probe.WaitUntilServingAsync(
            new FakeLauncher(),
            "node-1",
            new Uri("https://localhost:7149/"),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(readiness.IsServing, readiness.Message);
    }

    [Fact]
    public async Task Refuses_to_probe_a_remote_url()
    {
        var probe = new ProjectStructureRuntimeReadinessProbe(new HandlerClientFactory(
            new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        await Assert.ThrowsAsync<ArgumentException>(() => probe.WaitUntilServingAsync(
            new FakeLauncher(),
            "node-1",
            new Uri("https://example.com/"),
            TimeSpan.FromSeconds(1),
            CancellationToken.None));
    }

    private sealed class SequenceHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return Task.FromResult(response(request));
        }
    }

    private sealed class HandlerClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeLauncher : IProjectStructureRuntimeLauncher
    {
        public ProjectStructureRuntimeExitRecord? Exit { get; init; }

        public bool IsAvailable => true;

        public bool IsRunning(string nodeId) => Exit is null;

        public async Task<ProjectStructureRuntimeExitRecord?> WaitForExitAsync(
            string nodeId,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (Exit is not null)
            {
                return Exit;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(timeout.TotalMilliseconds, 50)), cancellationToken);
            return null;
        }

        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
            => throw new NotSupportedException();

        public ProjectStructureRuntimeLaunchResolution Resolve(
            ProjectObjectType objectType,
            string? objectSubtype,
            string? notes,
            string metadataJson,
            ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
            => throw new NotSupportedException();

        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
            ProjectStructureNode node,
            ProjectStructureRuntimeLaunchMode mode,
            ProjectStructureRuntimeLaunchApproval approval,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
