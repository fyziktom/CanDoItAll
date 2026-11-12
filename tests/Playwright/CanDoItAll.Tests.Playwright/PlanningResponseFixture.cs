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

internal sealed class PlanningResponseFixture : IAsyncDisposable {
    private WebApplication app = null!;
    private int requests;
    internal string BaseUrl { get; private set; } = string.Empty;
    internal int Requests => Volatile.Read(ref requests);
    internal TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal static async Task<PlanningResponseFixture> StartAsync() {
        var fixture = new PlanningResponseFixture();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        fixture.app = builder.Build();
        fixture.app.MapPost("/v1/chat/completions", async (HttpContext context) => {
            using var input = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var ordinal = Interlocked.Increment(ref fixture.requests);
            Assert.InRange(ordinal, 1, 2);
            Assert.False(input.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean());
            Assert.False(input.RootElement.TryGetProperty("tools", out var tools) && tools.GetArrayLength() != 0);
            Assert.Equal(2, input.RootElement.GetProperty("messages").GetArrayLength());
            if (ordinal == 2) {
                fixture.RefreshEntered.TrySetResult();
                await fixture.ReleaseRefresh.Task.WaitAsync(TimeSpan.FromSeconds(90), context.RequestAborted);
            }
            var content = JsonSerializer.Serialize(new { version = 1, tasks = Enumerable.Range(1, 2).Select(index => new {
                title = $"Generation {ordinal} task {index}", description = "Review <script>plain text</script> Ω 東京",
                notes = "Preserve the original project.", priority = 3
            }) });
            await context.Response.WriteAsJsonAsync(new {
                id = $"chatcmpl-planning-{ordinal}", @object = "chat.completion", created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = input.RootElement.GetProperty("model").GetString(),
                choices = new[] { new { index = 0, message = new { role = "assistant", content }, finish_reason = "stop" } },
                usage = new { prompt_tokens = 11, completion_tokens = 7, total_tokens = 18 }
            }, context.RequestAborted);
        });
        try {
            await fixture.app.StartAsync();
            fixture.BaseUrl = fixture.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/v1";
            return fixture;
        } catch {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        ReleaseRefresh.TrySetResult();
        await app.DisposeAsync();
    }
}
