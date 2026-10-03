using System.Collections.Concurrent;
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

internal sealed class AgentAuthoringSetupFixture : IAsyncDisposable {
    internal const string ToolName = "mcp_ca1_echo";
    private WebApplication app = null!;
    private int httpCalls;
    private int starts;
    private int lists;
    internal string BaseUrl { get; private set; } = string.Empty;
    internal int HttpCalls => Volatile.Read(ref httpCalls);
    internal int McpStarts => Volatile.Read(ref starts);
    internal int McpLists => Volatile.Read(ref lists);
    internal ConcurrentQueue<JsonElement> Invocations { get; } = new();

    internal static async Task<AgentAuthoringSetupFixture> StartAsync() {
        var fixture = new AgentAuthoringSetupFixture();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => {
            server.Listen(IPAddress.Loopback, 0);
            server.Limits.MaxRequestBodySize = 32 * 1024;
        });
        fixture.app = builder.Build();
        fixture.app.MapPost("/tool", async (HttpContext context) => {
            Assert.InRange(Interlocked.Increment(ref fixture.httpCalls), 1, 4);
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            Assert.Equal(JsonValueKind.Object, body.RootElement.ValueKind);
            await context.Response.WriteAsJsonAsync(new { ok = true }, context.RequestAborted);
        });
        fixture.app.MapGet("/mcp", () => Results.StatusCode(405));
        fixture.app.MapDelete("/mcp", () => Results.NoContent());
        fixture.app.MapPost("/mcp", async (HttpContext context) => {
            using var request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var root = request.RootElement;
            if (!root.TryGetProperty("id", out var id)) {
                context.Response.StatusCode = 202;
                return;
            }
            object result;
            switch (root.GetProperty("method").GetString()) {
                case "initialize":
                    Assert.InRange(Interlocked.Increment(ref fixture.starts), 1, 12);
                    result = new { protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(),
                        capabilities = new { tools = new { listChanged = false } }, serverInfo = new { name = "CA1 owned MCP", version = "1.0" } };
                    break;
                case "tools/list":
                    Assert.InRange(Interlocked.Increment(ref fixture.lists), 1, 12);
                    result = new { tools = new[] { new { name = ToolName, description = "Echo the explicit owned fixture marker.",
                        inputSchema = new { type = "object", properties = new { value = new { type = "string" } }, required = new[] { "value" }, additionalProperties = false } } } };
                    break;
                case "tools/call":
                    Assert.Equal(ToolName, root.GetProperty("params").GetProperty("name").GetString());
                    var arguments = root.GetProperty("params").GetProperty("arguments").Clone();
                    fixture.Invocations.Enqueue(arguments);
                    Assert.InRange(fixture.Invocations.Count, 1, 2);
                    result = new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(new { ok = true, value = arguments.GetProperty("value").GetString() }) } }, isError = false };
                    break;
                case "ping":
                    result = new { };
                    break;
                default:
                    throw new InvalidOperationException("Unexpected method at the bounded MCP fixture.");
            }
            await context.Response.WriteAsJsonAsync(new { jsonrpc = "2.0", id, result }, context.RequestAborted);
        });
        await fixture.app.StartAsync();
        fixture.BaseUrl = fixture.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return fixture;
    }
    public async ValueTask DisposeAsync() => await app.DisposeAsync();
}
