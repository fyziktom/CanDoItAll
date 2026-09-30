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

internal sealed class LiveModelRequestProxy : IAsyncDisposable {
    internal const string BudgetFileVariable = "CANDOITALL_LIVE_REQUEST_BUDGET_FILE";
    internal const int PerExecutionLimit = 10;
    internal const int CampaignLimit = 40;
    private readonly Guid execution = Guid.NewGuid();
    private readonly string budgetPath;
    private readonly HttpClient client = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(4) };
    private WebApplication? app;
    private int admitted;
    private int successful;
    private int refused;
    internal int Admitted => Volatile.Read(ref admitted);
    internal int Successful => Volatile.Read(ref successful);
    internal int Refused => Volatile.Read(ref refused);
    internal string BaseUrl { get; private set; } = string.Empty;
    internal Guid Execution => execution;

    internal LiveModelRequestProxy(string budgetPath) => this.budgetPath = budgetPath;

    internal static async Task<LiveModelRequestProxy> StartAsync() {
        var path = Environment.GetEnvironmentVariable(BudgetFileVariable);
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path)) {
            throw new InvalidOperationException("Live proof requires an existing absolute campaign budget journal path.");
        }
        var proxy = new LiveModelRequestProxy(path);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        proxy.app = builder.Build();
        proxy.app.Run(proxy.ForwardAsync);
        await proxy.app.StartAsync();
        proxy.BaseUrl = proxy.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/v1";
        return proxy;
    }

    private async Task ForwardAsync(HttpContext context) {
        if (!HttpMethods.IsPost(context.Request.Method) || context.Request.Path != "/v1/responses" || context.Request.QueryString.HasValue) {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (!await ReserveAsync(context.RequestAborted)) {
            Interlocked.Increment(ref refused);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = new { message = "The live proof request budget is exhausted." } }, context.RequestAborted);
            return;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses") {
            Content = new StreamContent(context.Request.Body)
        };
        request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToArray());
        request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
        if (response.Headers.TryGetValues("x-request-id", out var requestIds)) {
            context.Response.Headers["x-request-id"] = requestIds.ToArray();
        }
        if (response.IsSuccessStatusCode) {
            Interlocked.Increment(ref successful);
        }
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    internal async Task<bool> ReserveAsync(CancellationToken cancellationToken) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        FileStream journal;
        while (true) {
            deadline.Token.ThrowIfCancellationRequested();
            try {
                journal = new FileStream(budgetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                break;
            } catch (IOException) {
                await Task.Delay(20, deadline.Token);
            }
        }
        await using (journal) {
            var reservations = await JsonSerializer.DeserializeAsync<List<Reservation>>(journal, cancellationToken: deadline.Token)
                ?? throw new InvalidOperationException("The live campaign budget journal is invalid.");
            if (reservations.Count >= CampaignLimit || reservations.Count(item => item.Execution == execution) >= PerExecutionLimit) {
                return false;
            }
            reservations.Add(new(execution, reservations.Count + 1, DateTimeOffset.UtcNow));
            journal.Position = 0;
            await JsonSerializer.SerializeAsync(journal, reservations, cancellationToken: deadline.Token);
            journal.SetLength(journal.Position);
            journal.Flush(flushToDisk: true);
            Interlocked.Increment(ref admitted);
            return true;
        }
    }

    public async ValueTask DisposeAsync() {
        if (app is not null) {
            await app.DisposeAsync();
        }
        client.Dispose();
    }
    private sealed record Reservation(Guid Execution, int Ordinal, DateTimeOffset AdmittedAtUtc);
}
