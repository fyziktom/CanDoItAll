using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;

internal static class BrowserEntry {
    public static async Task Main(string[] args) {
        var repository = Path.GetFullPath(args[^1]);
        await using var environment = CanDoItAllTestEnvironment.CreateUnder(Path.Combine(repository, ".mcp-state"), "overview-browser-data");
        var database = environment.CreatePostgreSqlProfile("overview-browser");
        var overrides = new Dictionary<string, string?> {
            ["Api:Authorization:Enabled"] = "false",
            ["ApiAccess:Authorization:Enabled"] = "false",
            ["WebHost:HttpsRedirectionEnabled"] = "false",
            ["Logging:LogLevel:Default"] = "Warning"
        };
        await using var services = await TestApplicationBootstrap.BuildServiceProviderAsync(database, "CanDoItAll.Web", TestSchemaBootstrapModules.Full, overrides);
        var fixture = new OverviewFixture();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:17305");
        await using var control = builder.Build();
        control.MapPost("/fixture/mode/{mode}", (OverviewMode mode) => { fixture.Mode = mode; return Results.Ok(); });
        control.MapGet("/fixture/state", () => Results.Json(new { fixture.Mode, fixture.OverviewReads, fixture.HeaderReads, fixture.UsageReads, fixture.Cancelled }));
        control.MapPost("/fixture/release", () => { fixture.Release(); return Results.Ok(); });
        control.MapPost("/fixture/stop", () => { fixture.Release(); stop.TrySetResult(); return Results.Ok(); });
        await control.StartAsync();
        foreach (var variable in database.CreateEnvironmentVariables(overrides)) {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
        await using var application = new OverviewApplication(repository, fixture);
        application.UseKestrel(5275);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Console.WriteLine(JsonSerializer.Serialize(new { state = "FixtureReady" }));
        await stop.Task;
        await control.StopAsync();
    }
}

internal sealed class OverviewApplication(string repository, OverviewFixture fixture) : WebApplicationFactory<Program> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.Combine(repository, "src", "App", "CanDoItAll.Web"));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        builder.ConfigureServices(services => {
            var factory = services.Last(x => x.ServiceType == typeof(IAgentFrameworkWorkspaceService)).ImplementationFactory!;
            services.AddScoped<IAgentFrameworkWorkspaceService>(provider => {
                var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, OverviewWorkspace>();
                var observer = (OverviewWorkspace)(object)proxy;
                observer.Inner = (IAgentFrameworkWorkspaceService)factory(provider);
                observer.Fixture = fixture;
                return proxy;
            });
            services.AddSingleton<IBoundAgentResourceQuery>(new OverviewBound(fixture));
            services.RemoveAll<IProviderUsageProjectionSource>();
            services.AddSingleton<IProviderUsageProjectionSource>(new OverviewUsage(fixture, ProviderUsageWorkloadKind.Agent));
            services.AddSingleton<IProviderUsageProjectionSource>(new OverviewUsage(fixture, ProviderUsageWorkloadKind.SimpleChat));
        });
    }
}

internal enum OverviewMode { Normal, Empty, OverviewFailure, UsageFailure, HeaderFailure, BoundFailure, Partial, HoldOverview, HoldUsage, Long }

internal sealed class OverviewFixture {
    private TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationToken owner;
    public OverviewMode Mode { get; set; }
    public int OverviewReads;
    public int HeaderReads;
    public int UsageReads;
    public bool Cancelled => owner.IsCancellationRequested;
    public async Task HoldAsync(CancellationToken token) {
        owner = token;
        await held.Task;
    }
    public void Release() {
        held.TrySetResult();
        held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public async Task<AgentOverviewSnapshot> ReadOverviewAsync(CancellationToken token) {
        Interlocked.Increment(ref OverviewReads);
        if (Mode == OverviewMode.HoldOverview) {
            await HoldAsync(token);
        }
        if (Mode == OverviewMode.OverviewFailure) {
            throw new IOException("Controlled private Overview infrastructure detail.");
        }
        return Mode == OverviewMode.Empty ? AgentOverviewSnapshot.Empty : AgentOverviewSnapshot.Empty with {
            Totals = AgentOverviewTotals.Empty with { AgentCount = 42, ProviderCount = 3, CapabilityCount = 12, TeamCount = 2, SessionCount = 18, ActiveRuns = 3, FailedRuns = 2 },
            TeamShortcuts = [new(Guid.Parse("580e7395-0890-43ad-b27e-e19f0369d571"), Mode == OverviewMode.Long ? "Long interdisciplinary product research, international synthesis and evidence verification team" : "Product research and synthesis", "Safe fixture", "groups", 4),
                new(Guid.Parse("2ac59e8b-f96c-486e-a9ce-8a3a3d952fce"), "Delivery governance", "Safe fixture", "groups", 3)]
        };
    }
}

public class OverviewWorkspace : DispatchProxy {
    public IAgentFrameworkWorkspaceService Inner { get; set; } = default!;
    internal OverviewFixture Fixture { get; set; } = default!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        if (method!.Name == nameof(IAgentFrameworkWorkspaceService.GetAgentOverviewAsync)) {
            return Fixture.ReadOverviewAsync((CancellationToken)args![0]!);
        }
        if (method.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
            Interlocked.Increment(ref Fixture.HeaderReads);
            if (Fixture.Mode == OverviewMode.HeaderFailure) {
                return Task.FromException<IReadOnlyList<AgentDefinition>>(new IOException("Controlled private HR infrastructure detail."));
            }
        }
        return method.Invoke(Inner, args);
    }
}

internal sealed class OverviewBound(OverviewFixture fixture) : IBoundAgentResourceQuery {
    public Task<int> CountAsync(CancellationToken cancellationToken = default) => fixture.Mode == OverviewMode.BoundFailure
        ? Task.FromException<int>(new IOException("Controlled private bound-resource detail.")) : Task.FromResult(6);
}

internal sealed class OverviewUsage(OverviewFixture fixture, ProviderUsageWorkloadKind kind) : IProviderUsageProjectionSource {
    public string SourceName => kind.ToString();
    public ProviderUsageWorkloadKind WorkloadKind => kind;
    public async ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref fixture.UsageReads);
        if (fixture.Mode == OverviewMode.HoldUsage) {
            await fixture.HoldAsync(cancellationToken);
        }
        if (fixture.Mode == OverviewMode.UsageFailure) {
            throw new IOException("Controlled private usage infrastructure detail.");
        }
        var rows = fixture.Mode == OverviewMode.Empty ? [] : Enumerable.Range(1, 5).Select(index => new ProviderUsageContribution(
            $"{kind}-{index}", kind, kind == ProviderUsageWorkloadKind.Agent ? ProviderUsageConsumerKind.Agent : ProviderUsageConsumerKind.SimpleChatDefinition,
            $"{kind}-{index}", fixture.Mode == OverviewMode.Long ? $"Long {kind} {index} interdisciplinary research and delivery assistant with detailed responsibility" : $"{kind} {index} research and delivery assistant", null, fixture.Mode == OverviewMode.Long ? $"Long {(index % 2 == 0 ? "research" : "delivery")} provider for international multi-stage technical analysis" : index % 2 == 0 ? "Research provider" : "Delivery provider",
            ProviderKind.OpenAi, "Representative model", $"{kind}-{index}", index == 2 ? ProviderUsageExecutionOutcome.Failed : ProviderUsageExecutionOutcome.Succeeded,
            index == 3 ? ProviderUsageCompleteness.UsageUnavailable : ProviderUsageCompleteness.Observed,
            index == 3 ? ProviderUsagePricingCompleteness.Unpriced : ProviderUsagePricingCompleteness.CalculatedAtExecution,
            new(100 * index, 0, 0, 50 * index, 0, 150 * index), index == 3 ? null : 0.02m * index, DateTimeOffset.Parse("2026-09-01T12:00:00Z"))).ToArray();
        return new(SourceName, kind, fixture.Mode == OverviewMode.Partial ? ProviderUsageSourceState.Partial : ProviderUsageSourceState.Complete,
            rows, DateTimeOffset.Parse("2026-09-01T12:00:00Z"), fixture.Mode == OverviewMode.Partial ? new("fixture-partial", "Some observations are unavailable.") : null);
    }
}
