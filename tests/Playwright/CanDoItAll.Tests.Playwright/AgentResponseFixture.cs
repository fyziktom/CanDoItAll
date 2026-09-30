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

internal sealed class AgentResponseFixture : IAsyncDisposable {
    private WebApplication app = null!;
    private int requests;
    private int externalRequests;
    internal int ExternalRequests => Volatile.Read(ref externalRequests);
    internal string BaseUrl { get; private set; } = "";
    internal string Arguments { get; set; } = "";
    internal string Reply { get; set; } = "";
    internal IReadOnlyList<Func<JsonElement, ScriptedTurn>>? Steps { get; set; }
    internal Func<int, Task>? BeforeResponse { get; set; }
    internal bool HoldReply { get; set; } = true;
    internal sealed record ScriptedCall(string Name, string Arguments);
    internal sealed record ScriptedTurn(IReadOnlyList<ScriptedCall> Calls, string? Text = null);
    internal int Requests => Volatile.Read(ref requests);
    internal TaskCompletionSource PrefixFlushed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseReply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static async Task<AgentResponseFixture> StartAsync(string toolName) {
        var fixture = new AgentResponseFixture();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        fixture.app = builder.Build();
        fixture.app.MapPost("/v1/responses", async (HttpContext context) => {
            using var input = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var ordinal = Interlocked.Increment(ref fixture.requests);
            Assert.InRange(ordinal, 1, fixture.Steps?.Count ?? 2);
            Assert.True(input.RootElement.GetProperty("stream").GetBoolean());
            var turn = fixture.Steps is { } steps ? steps[ordinal - 1](input.RootElement)
                : ordinal == 1 ? new ScriptedTurn([new(toolName, fixture.Arguments)]) : new ScriptedTurn([], fixture.Reply);
            var items = new List<object>();
            foreach (var call in turn.Calls) {
                Assert.Contains(input.RootElement.GetProperty("tools").EnumerateArray(),
                    item => item.TryGetProperty("name", out var name) && name.GetString() == call.Name);
                var id = $"function_{ordinal}_{items.Count}";
                items.Add(new { type = "function_call", id, call_id = $"call_{ordinal}_{items.Count}", name = call.Name,
                    arguments = call.Arguments, status = "completed" });
            }
            if (turn.Text is { } text) {
                items.Add(new { type = "message", id = "message_done", role = "assistant", status = "completed",
                    content = new[] { new { type = "output_text", text, annotations = Array.Empty<object>() } } });
            }
            if (fixture.BeforeResponse is not null) {
                await fixture.BeforeResponse(ordinal);
            }
            var response = new { id = $"response_{ordinal}", @object = "response", created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                status = "completed", model = input.RootElement.GetProperty("model").GetString(), output = items,
                parallel_tool_calls = false, tools = Array.Empty<object>(), usage = new { input_tokens = 8, output_tokens = 5, total_tokens = 13 } };
            context.Response.ContentType = "text/event-stream";
            var sequence = 0;
            for (var index = 0; index < items.Count; index++) {
                await WriteAsync("response.output_item.added", new { type = "response.output_item.added", sequence_number = sequence++, output_index = index, item = items[index] });
                if (turn.Text is { } reply) {
                    if (fixture.Steps is null) {
                        Assert.Contains(input.RootElement.GetProperty("input").EnumerateArray(),
                            value => value.TryGetProperty("type", out var type) && type.GetString() == "function_call_output");
                    }
                    await WriteAsync("response.output_text.delta", new { type = "response.output_text.delta", sequence_number = sequence++,
                        output_index = index, item_id = "message_done", content_index = 0, delta = reply });
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                    fixture.PrefixFlushed.TrySetResult();
                    if (fixture.HoldReply) {
                        await fixture.ReleaseReply.Task.WaitAsync(TimeSpan.FromSeconds(60), context.RequestAborted);
                    }
                }
                await WriteAsync("response.output_item.done", new { type = "response.output_item.done", sequence_number = sequence++, output_index = index, item = items[index] });
            }
            await WriteAsync("response.completed", new { type = "response.completed", sequence_number = sequence, response });

            Task WriteAsync(string name, object value) => context.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(value)}\n\n", context.RequestAborted);
        });
        fixture.app.MapGet("/{**path}", () => {
            Interlocked.Increment(ref fixture.externalRequests);
            return "Unexpected external source canary";
        });
        await fixture.app.StartAsync();
        fixture.BaseUrl = fixture.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/v1";
        return fixture;
    }

    public async ValueTask DisposeAsync() {
        ReleaseReply.TrySetResult();
        await app.DisposeAsync();
    }
}
