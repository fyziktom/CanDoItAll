using System.Text.Json;
using CanDoItAll.Manager;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = ManagerHostEnvironment.ResolveEnvironmentName()
});
builder.WebHost.UseUrls(builder.Configuration["Manager:Url"] ?? "http://127.0.0.1:6407");

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ManagerSessionService>();
builder.Services.AddSingleton<CapsuleCatalogService>();
builder.Services.AddSingleton<ICapsuleCatalogService>(serviceProvider => serviceProvider.GetRequiredService<CapsuleCatalogService>());
builder.Services.AddSingleton<WatchSupervisorService>();
builder.Services.AddSingleton<IWatchSupervisor>(serviceProvider => serviceProvider.GetRequiredService<WatchSupervisorService>());
builder.Services.AddSingleton<TailwindWatchSupervisorService>();
builder.Services.AddSingleton<ITuningExecutionAdapter, LocalProcessTuningExecutionAdapter>();
builder.Services.AddSingleton<TuningRequestService>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<WatchSupervisorService>());
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<TailwindWatchSupervisorService>());
builder.Services.AddHostedService<CapsuleRefreshService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", (HttpContext httpContext, ManagerSessionService session, WatchSupervisorService watch, TailwindWatchSupervisorService tailwind, CapsuleCatalogService capsules, IConfiguration configuration) =>
{
    var options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new ManagerOptions();
    var workspaceRoot = ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, options);
    var watchProjectPath = ManagerStatusResponseFactory.ResolveWatchProjectPath(workspaceRoot, options);
    var status = ManagerStatusResponseFactory.Create(
        app.Environment.EnvironmentName,
        session.SessionToken,
        workspaceRoot,
        watchProjectPath,
        watch.GetStatus(),
        tailwind.GetStatus(),
        options,
        $"{httpContext.Request.Scheme}://{httpContext.Request.Host}");
    var html = ManagerDashboardPage.Render(
        status,
        capsules.GetCoverage(),
        openApiAvailable: app.Environment.IsDevelopment());

    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/api/status", (HttpContext httpContext, ManagerSessionService session, WatchSupervisorService watch, TailwindWatchSupervisorService tailwind, IConfiguration configuration) =>
{
    var options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new ManagerOptions();
    var workspaceRoot = ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, options);
    var watchProjectPath = ManagerStatusResponseFactory.ResolveWatchProjectPath(workspaceRoot, options);
    var status = ManagerStatusResponseFactory.Create(
        app.Environment.EnvironmentName,
        session.SessionToken,
        workspaceRoot,
        watchProjectPath,
        watch.GetStatus(),
        tailwind.GetStatus(),
        options,
        $"{httpContext.Request.Scheme}://{httpContext.Request.Host}");

    return Results.Ok(status);
})
.WithName("GetManagerStatus");

app.MapGet("/api/watch/status", (WatchSupervisorService watchSupervisor) => Results.Ok(watchSupervisor.GetStatus()));
app.MapGet("/api/watch/logs", (int? take, WatchSupervisorService watchSupervisor) => Results.Ok(watchSupervisor.GetLogs(take ?? 200)));
app.MapGet("/api/tailwind/status", (TailwindWatchSupervisorService tailwindSupervisor) => Results.Ok(tailwindSupervisor.GetStatus()));
app.MapGet("/api/tailwind/logs", (int? take, TailwindWatchSupervisorService tailwindSupervisor) => Results.Ok(tailwindSupervisor.GetLogs(take ?? 200)));
app.MapGet("/api/watch/wait-ready", async (long? afterEventId, int? timeoutMs, WatchSupervisorService watchSupervisor, CancellationToken cancellationToken) =>
{
    var result = await watchSupervisor.WaitForReadyAsync(afterEventId ?? 0, TimeSpan.FromMilliseconds(timeoutMs ?? 90_000), cancellationToken);
    return result is null ? Results.NoContent() : Results.Ok(result);
});
app.MapGet("/api/watch/events", async (HttpContext httpContext, WatchSupervisorService watchSupervisor, CancellationToken cancellationToken) =>
{
    httpContext.Response.Headers.ContentType = "text/event-stream";
    var reader = watchSupervisor.Subscribe(out var subscriptionId);
    try
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            await WriteSseAsync(httpContext.Response, "watch", item, cancellationToken);
        }
    }
    finally
    {
        watchSupervisor.Unsubscribe(subscriptionId);
    }
});

app.MapGet("/api/capsules/index", (CapsuleCatalogService capsules) => Results.Ok(capsules.GetIndex()));
app.MapGet("/api/capsules/coverage", (CapsuleCatalogService capsules) => Results.Ok(capsules.GetCoverage()));
app.MapGet("/api/capsules/symbols/{symbolId}", (string symbolId, CapsuleCatalogService capsules) =>
{
    var record = capsules.GetSymbol(symbolId);
    return record is null ? Results.NotFound() : Results.Ok(record);
});
app.MapGet("/api/capsules/changed", (string? sinceUtc, CapsuleCatalogService capsules) =>
{
    var since = DateTimeOffset.TryParse(sinceUtc, out var parsed) ? parsed : DateTimeOffset.UtcNow.AddHours(-1);
    return Results.Ok(capsules.GetChangedSince(since));
});

app.MapPost("/api/tuning/requests", async (HttpContext httpContext, ManagerSessionService session, TuningRequestService tuningRequests, TuningRequestCreateModel model, CancellationToken cancellationToken) =>
{
    if (!session.IsAuthorized(httpContext.Request))
    {
        return Results.Unauthorized();
    }

    try
    {
        var record = await tuningRequests.CreateAsync(model, cancellationToken);
        return Results.Ok(record);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

app.MapGet("/api/tuning/requests/{requestId:guid}", (Guid requestId, TuningRequestService tuningRequests) =>
{
    var record = tuningRequests.Get(requestId);
    return record is null ? Results.NotFound() : Results.Ok(record);
});

app.MapGet("/api/tuning/requests/{requestId:guid}/events", async (Guid requestId, HttpContext httpContext, TuningRequestService tuningRequests, CancellationToken cancellationToken) =>
{
    httpContext.Response.Headers.ContentType = "text/event-stream";
    var reader = tuningRequests.Subscribe(requestId, out var subscriptionId);
    try
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            await WriteSseAsync(httpContext.Response, "tuning", item, cancellationToken);
        }
    }
    finally
    {
        tuningRequests.Unsubscribe(requestId, subscriptionId);
    }
});

app.MapPost("/api/tuning/requests/{requestId:guid}/cancel", async (Guid requestId, HttpContext httpContext, ManagerSessionService session, TuningRequestService tuningRequests, CancellationToken cancellationToken) =>
{
    if (!session.IsAuthorized(httpContext.Request))
    {
        return Results.Unauthorized();
    }

    await tuningRequests.CancelAsync(requestId, cancellationToken);
    return Results.Ok();
});

app.MapPost("/api/tuning/requests/{requestId:guid}/submit", async (Guid requestId, HttpContext httpContext, ManagerSessionService session, TuningRequestService tuningRequests, CancellationToken cancellationToken) =>
{
    if (!session.IsAuthorized(httpContext.Request))
    {
        return Results.Unauthorized();
    }

    try
    {
        var record = await tuningRequests.SubmitAsync(requestId, cancellationToken);
        return Results.Ok(record);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

app.Run();

static async Task WriteSseAsync(HttpResponse response, string eventName, object payload, CancellationToken cancellationToken)
{
    await response.WriteAsync($"event: {eventName}\n", cancellationToken);
    await response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n", cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
}

public partial class Program;
