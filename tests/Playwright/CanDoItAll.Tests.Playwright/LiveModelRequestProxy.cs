using System.Net;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
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
    private readonly HttpClient client;
    private readonly ConcurrentQueue<LiveProviderObservation> observations = new();
    private int attempts;
    private int disposed;
    private WebApplication? app;
    private int admitted;
    private int successful;
    private int refused;
    internal int Admitted => Volatile.Read(ref admitted);
    internal int Successful => Volatile.Read(ref successful);
    internal int Refused => Volatile.Read(ref refused);
    internal int Attempts => Volatile.Read(ref attempts);
    internal IReadOnlyList<LiveProviderObservation> Observations => observations.ToArray();
    internal string BaseUrl { get; private set; } = string.Empty;
    internal Guid Execution => execution;

    internal LiveModelRequestProxy(string budgetPath, HttpMessageHandler? transport = null) {
        this.budgetPath = budgetPath;
        client = new(transport ?? new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(4) };
    }

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

    internal async Task ForwardAsync(HttpContext context) {
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
        var observation = new LiveProviderObservation(Interlocked.Increment(ref attempts), null,
            LiveProviderTerminal.Missing, LiveProviderReason.None, null, null, null);
        try {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            observation = observation with { HttpStatus = (int)response.StatusCode,
                Terminal = response.IsSuccessStatusCode ? LiveProviderTerminal.Missing : LiveProviderTerminal.HttpRejected };
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
            if (response.Headers.TryGetValues("x-request-id", out var requestIds)) {
                var ids = requestIds.ToArray();
                context.Response.Headers["x-request-id"] = ids;
                observation = observation with { RequestCorrelationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', ids)))) };
            }
            if (response.IsSuccessStatusCode) {
                Interlocked.Increment(ref successful);
            }
            if (!response.IsSuccessStatusCode) {
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
                return;
            }
            if (response.Content.Headers.ContentType?.MediaType == "text/event-stream") {
                using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(context.RequestAborted));
                while (await reader.ReadLineAsync(context.RequestAborted) is { } line) {
                    if (line.StartsWith("data:", StringComparison.Ordinal) && line.Length <= AgentResponseObservationLimit) {
                        observation = LiveProviderStatusReader.Read(line[5..].TrimStart(), observation);
                    }
                    await context.Response.WriteAsync(line + "\n", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
            } else {
                var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
                if (payload.Length <= AgentResponseObservationLimit) {
                    observation = LiveProviderStatusReader.Read(payload, observation);
                }
                await context.Response.WriteAsync(payload, context.RequestAborted);
            }
        } catch {
            observation = observation with { Terminal = LiveProviderTerminal.TransportFailure };
            throw;
        } finally {
            observations.Enqueue(observation);
        }
    }

    private const int AgentResponseObservationLimit = 4_194_304;

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
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        if (app is not null) {
            await app.DisposeAsync();
        }
        client.Dispose();
    }
    private sealed record Reservation(Guid Execution, int Ordinal, DateTimeOffset AdmittedAtUtc);
}
