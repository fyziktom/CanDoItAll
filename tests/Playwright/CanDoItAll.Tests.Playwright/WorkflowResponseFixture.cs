using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Playwright;

internal sealed class WorkflowResponseFixture : IAsyncDisposable {
    private WebApplication app = null!;
    private int requests;
    internal string BaseUrl { get; private set; } = "";
    internal int Requests => Volatile.Read(ref requests);

    internal static async Task<WorkflowResponseFixture> StartAsync(string output) {
        var fixture = new WorkflowResponseFixture();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        fixture.app = builder.Build();
        fixture.app.MapPost("/v1/responses", async (HttpContext context) => {
            using var input = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            Assert.False(input.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean());
            Assert.False(input.RootElement.TryGetProperty("temperature", out _));
            Assert.Equal(150, input.RootElement.GetProperty("max_output_tokens").GetInt32());
            Assert.Equal(1, Interlocked.Increment(ref fixture.requests));
            await context.Response.WriteAsJsonAsync(new {
                id = "resp_workflow_fixture", @object = "response", created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                status = "completed", model = input.RootElement.GetProperty("model").GetString(),
                output = new[] { new { id = "msg_workflow_fixture", type = "message", status = "completed", role = "assistant",
                    content = new[] { new { type = "output_text", text = output, annotations = Array.Empty<object>() } } } },
                parallel_tool_calls = false, tools = Array.Empty<object>(), usage = new { input_tokens = 5, output_tokens = 20, total_tokens = 25 }
            }, context.RequestAborted);
        });
        await fixture.app.StartAsync();
        fixture.BaseUrl = fixture.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/v1";
        return fixture;
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
