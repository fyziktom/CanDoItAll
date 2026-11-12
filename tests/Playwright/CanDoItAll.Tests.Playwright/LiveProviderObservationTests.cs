using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "HostPlatform")]
public sealed class LiveProviderObservationTests {
    [Theory]
    [InlineData("completed", false, LiveProviderTerminal.Completed)]
    [InlineData("incomplete", false, LiveProviderTerminal.Incomplete)]
    [InlineData("failed", false, LiveProviderTerminal.Failed)]
    [InlineData("cancelled", false, LiveProviderTerminal.Cancelled)]
    [InlineData("incomplete", true, LiveProviderTerminal.Incomplete)]
    public async Task Http_success_and_provider_terminal_are_distinct_and_only_allowlisted_evidence_survives(
        string status, bool streaming, LiveProviderTerminal expected) {
        await using var environment = CanDoItAllTestEnvironment.Create("provider-status-proof");
        var journal = Path.Combine(environment.RootPath, "reservations.json");
        await File.WriteAllTextAsync(journal, "[]");
        var response = JsonSerializer.Serialize(new { status, incomplete_details = new { reason = "max_output_tokens" },
            usage = new { input_tokens = 5, output_tokens = 150 }, output_text = "private-output-canary", error = new { message = "private-error-canary" } });
        var body = streaming ? $"event: response.{status}\ndata: {{\"response\":{response}}}\n\n" : response;
        var transport = new FixtureTransport(_ => new(HttpStatusCode.OK) {
            Content = new StringContent(body, Encoding.UTF8, streaming ? "text/event-stream" : "application/json")
        });
        await using var proxy = new LiveModelRequestProxy(journal, transport);
        var context = Context();
        await proxy.ForwardAsync(context);
        Assert.Equal(1, transport.Requests);
        Assert.Equal(1, proxy.Admitted);
        Assert.Equal(1, proxy.Attempts);
        Assert.Equal(1, proxy.Successful);
        var observed = Assert.Single(proxy.Observations);
        Assert.Equal(expected, observed.Terminal);
        Assert.Equal(200, observed.HttpStatus);
        Assert.Equal(LiveProviderReason.OutputTokenLimit, observed.Reason);
        Assert.Equal(150, observed.OutputTokens);
        var evidence = JsonSerializer.Serialize(observed);
        Assert.DoesNotContain("private-", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-credential", evidence, StringComparison.Ordinal);
        Assert.Equal(body, Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transport_failure_or_http_rejection_remains_an_attempt_and_cannot_be_completed(bool reject) {
        await using var environment = CanDoItAllTestEnvironment.Create("provider-failure-proof");
        var journal = Path.Combine(environment.RootPath, "reservations.json");
        await File.WriteAllTextAsync(journal, "[]");
        var transport = new FixtureTransport(_ => reject ? new(HttpStatusCode.TooManyRequests) {
            Content = new StringContent("private-provider-error")
        } : throw new HttpRequestException("private-transport-error"));
        await using var proxy = new LiveModelRequestProxy(journal, transport);
        var failure = await Record.ExceptionAsync(() => proxy.ForwardAsync(Context()));
        Assert.Equal(!reject, failure is HttpRequestException);
        Assert.Equal(1, proxy.Attempts);
        Assert.Equal(0, proxy.Successful);
        Assert.Equal(reject ? LiveProviderTerminal.HttpRejected : LiveProviderTerminal.TransportFailure, Assert.Single(proxy.Observations).Terminal);
    }

    [Fact]
    public async Task Request_after_the_execution_limit_is_refused_before_the_transport() {
        await using var environment = CanDoItAllTestEnvironment.Create("provider-refusal-proof");
        var journal = Path.Combine(environment.RootPath, "reservations.json");
        await File.WriteAllTextAsync(journal, "[]");
        var transport = new FixtureTransport(_ => throw new InvalidOperationException("No transport is authorized."));
        await using var proxy = new LiveModelRequestProxy(journal, transport);
        for (var index = 0; index < LiveModelRequestProxy.PerExecutionLimit; index++) {
            Assert.True(await proxy.ReserveAsync(CancellationToken.None));
        }
        var context = Context();
        await proxy.ForwardAsync(context);
        Assert.Equal(429, context.Response.StatusCode);
        Assert.Equal(1, proxy.Refused);
        Assert.Equal(0, transport.Requests);
        Assert.Equal(0, proxy.Attempts);
        Assert.Empty(proxy.Observations);
    }

    private static DefaultHttpContext Context() {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/v1/responses";
        context.Request.Headers.Authorization = "Bearer fixture-credential";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class FixtureTransport(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
        internal int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Requests++;
            return Task.FromResult(respond(request));
        }
    }
}
