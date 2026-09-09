using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

internal static class GovernanceBrowserEntry {
    public static async Task Main(string[] args) {
        if (args.Length != 2 || args[0] is not ("prepare" or "serve" or "serve-history" or "serve-definitions" or "serve-completion")) {
            throw new ArgumentException("Use prepare, serve, serve-history, serve-definitions or serve-completion followed by the repository root.");
        }
        var repository = Path.GetFullPath(args[1]);
        var output = Path.Combine(repository, ".artifacts", "governance-final", "browser-data");
        Directory.CreateDirectory(output);
        if (args[0] == "prepare") {
            await PrepareAsync(output);
            return;
        }
        var environment = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(output, "environment.json")))!;
        foreach (var pair in environment) {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
        var fixture = new GovernanceBrowserState { DefinitionsEnabled = args[0] is "serve-definitions" or "serve-completion", CompletionEnabled = args[0] == "serve-completion" };
        fixture.Definitions.ExecutionEnabled = fixture.CompletionEnabled;
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:17315");
        await using var control = builder.Build();
        control.MapPost("/fixture/mode/{mode}", (GovernanceBrowserMode mode) => {
            fixture.Mode = mode;
            return Results.Ok();
        });
        control.MapPost("/fixture/diagnostics/{mode}", (DiagnosticsBrowserMode mode) => {
            fixture.DiagnosticsMode = mode;
            return Results.Ok();
        });
        control.MapPost("/fixture/history/{mode}", (HistoryBrowserMode mode) => {
            fixture.History.Mode = mode;
            return Results.Ok();
        });
        control.MapGet("/fixture/history", () => Results.Json(new {
            fixture.History.Mode, fixture.History.Model, fixture.History.Ready,
            fixture.History.Searches, fixture.History.MetadataReads, fixture.History.ContentReads
        }));
        control.MapPost("/fixture/release", () => {
            fixture.Release();
            return Results.Ok();
        });
        control.MapGet("/fixture/definitions", () => Results.Json(new {
            fixture.Definitions.ListReads, fixture.Definitions.EditorReads, fixture.Definitions.Saves,
            fixture.Definitions.Search, fixture.Definitions.Tags, fixture.Definitions.Status, fixture.Definitions.StatusChanges
        }));
        control.MapPost("/fixture/definitions/{mode}", (DefinitionCatalogBrowserMode mode) => {
            fixture.Definitions.Mode = mode;
            return Results.Ok();
        });
        control.MapGet("/fixture/state", () => Results.Json(new {
            fixture.Mode, fixture.CatalogReads, fixture.ListReads, fixture.DetailReads, fixture.OwnerCanceled, fixture.DiagnosticsDashboardReads
        }));
        control.MapGet("/fixture/completion", () => Results.Json(new {
            fixture.Conversations.Sends, fixture.Conversations.Creates, fixture.Conversations.Renames, fixture.Conversations.Archives,
            fixture.Settings.VoiceSaves, fixture.Settings.Samples, fixture.Settings.FloatingSaves, fixture.Settings.Applies
        }));
        control.MapPost("/fixture/completion/{mode}", (AgentCompletionBrowserMode mode) => {
            fixture.Settings.Mode = mode;
            fixture.Conversations.SetMode(mode);
            return Results.Ok();
        });
        control.MapPost("/fixture/stop", () => {
            fixture.Release();
            stop.TrySetResult();
            return Results.Ok();
        });
        await control.StartAsync();
        await using var app = new GovernanceBrowserApplication(repository, fixture);
        app.UseKestrel(5285);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (args[0] == "serve-history") {
            await HistoryBrowserReads.SeedAsync(app.Services, fixture.History);
        }
        Console.WriteLine("Governance browser fixture ready.");
        await stop.Task;
        await control.StopAsync();
    }

    private static async Task PrepareAsync(string output) {
        if (File.Exists(Path.Combine(output, "environment.json"))) {
            throw new InvalidOperationException("An isolated Governance fixture already exists. Reuse its recorded environment.");
        }
        var environment = CanDoItAllTestEnvironment.CreateUnder(output, "isolated");
        var database = environment.CreatePostgreSqlProfile("governance-smoke");
        var overrides = new Dictionary<string, string?> {
            ["Api:Authorization:Enabled"] = "false",
            ["ApiAccess:Authorization:Enabled"] = "false",
            ["WebHost:HttpsRedirectionEnabled"] = "false",
            ["Logging:LogLevel:Default"] = "Warning",
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            ["Workflows:ExampleSeed:Enabled"] = "false",
            ["Workflows:ExampleSeed:SeedSampleWorkspaceFiles"] = "false",
            ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false"
        };
        await using (var services = await TestApplicationBootstrap.BuildServiceProviderAsync(database, "CanDoItAll.Web", TestSchemaBootstrapModules.Full, overrides)) {
            await using var scope = services.CreateAsyncScope();
            var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
            var provider = await registry.SaveProviderAsync(new ProviderProfileEditorModel {
                Name = "Governance rendering provider", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
                BaseUrl = "http://127.0.0.1:11434", DefaultModel = "governance-fixture", SuggestedModels = ["governance-fixture"],
                IsEnabled = true, IsPrivateProvider = true, ModelPrices = [new() { Model = "governance-fixture", InputPerMillionTokensUsd = 0.1m, OutputPerMillionTokensUsd = 0.2m }]
            });
            var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
            foreach (var (id, name) in new[] { (GovernanceBrowserState.AgentA, "Governance agent A"), (GovernanceBrowserState.AgentB, "Governance agent B") }) {
                await workspace.SaveAgentAsync(new AgentEditorModel {
                    Id = id, Name = name, RoleTitle = "UI fixture", Instructions = "Rendering fixture; never invoke a provider or tool.",
                    ProviderProfileId = provider, Model = "governance-fixture", Status = AgentLifecycleStatus.Active
                });
            }
            var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
            foreach (var run in new[] {
                GovernanceBrowserState.Run(GovernanceBrowserState.Run1, GovernanceBrowserState.AgentA, "Governance run 1", 2),
                GovernanceBrowserState.Run(GovernanceBrowserState.Run2, GovernanceBrowserState.AgentA, "Governance run 2", 1),
                GovernanceBrowserState.Run(GovernanceBrowserState.RunB, GovernanceBrowserState.AgentB, "Governance run B", 0) }) {
                await store.SaveExecutionRunDetailAsync(new(run, null,
                    [new(Guid.NewGuid(), run.AgentId, null, GovernanceBrowserState.Observed, ExecutionState.Completed, "Execution", GovernanceBrowserState.Denied) { ExecutionRunId = run.Id }],
                    [new(Guid.NewGuid(), run.AgentId, null, GovernanceBrowserState.Observed, RunOutcome.Succeeded, "Fixture", "governance-fixture", 125, 30, 40, 1) { ExecutionRunId = run.Id }]) {
                    Approvals = [new("approval-" + run.Id, run.Id, "call", "Review tool", "tool", GovernanceBrowserState.Denied, "{}",
                        ExecutionApprovalStatus.Approved, GovernanceBrowserState.Observed, GovernanceBrowserState.Observed, "manual", "", "")],
                    Artifacts = [new(Guid.NewGuid(), run.Id, "text", "Review result", "artifacts/review.txt", "text/plain", "Fixture", GovernanceBrowserState.Denied, GovernanceBrowserState.Observed)],
                    Checkpoints = [new(Guid.NewGuid(), run.Id, "session", "checkpoint", "Saved", ExecutionState.Completed, [],
                        GovernanceBrowserState.Observed, null, "", "manual", "", "", "", "", "", "", "")]
                });
            }
        }
        var variables = new Dictionary<string, string>(database.CreateEnvironmentVariables(overrides));
        var connection = new NpgsqlConnectionStringBuilder(variables["Database__ConnectionString"]);
        var passwordPath = Path.Combine(output, "database-password.txt");
        await File.WriteAllTextAsync(passwordPath, connection.Password ?? string.Empty);
        connection.Remove("Password");
        variables["Database__ConnectionString"] = connection.ConnectionString;
        variables["Database__PasswordFile"] = passwordPath;
        await File.WriteAllTextAsync(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(variables));
        Console.WriteLine("Prepared isolated Governance rendering data; no provider or tool was invoked.");
    }
}

internal sealed class GovernanceBrowserApplication(string repository, GovernanceBrowserState fixture) : WebApplicationFactory<Program> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.Combine(repository, "src", "App", "CanDoItAll.Web"));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        if (fixture.DefinitionsEnabled) {
            builder.ConfigureServices(services => {
                services.AddSingleton<ILlmChatDefinitionUiGateway>(fixture.Definitions);
                services.AddSingleton<ILlmChatUiAuthorizationFacade>(fixture.Definitions);
                services.AddSingleton<ILlmChatProviderUiGateway>(fixture.Definitions);
            });
        }
        if (fixture.CompletionEnabled) {
            builder.ConfigureServices(services => AgentCompletionBrowserServices.Register(services, fixture));
        }
        builder.ConfigureServices(services => services.AddScoped<IProviderRequestHistory>(provider =>
            new HistoryBrowserReads(ActivatorUtilities.CreateInstance<ProviderRequestHistoryService>(provider), fixture)));
        builder.ConfigureServices(services => services.AddScoped<IAgentDiagnosticsReads>(provider =>
            new DiagnosticsBrowserReads(new AgentDiagnosticsReads(provider.GetRequiredService<IAgentFrameworkWorkspaceService>()), fixture)));
        builder.ConfigureServices(services => services.AddScoped<IAgentGovernanceReads>(provider =>
            new GovernanceBrowserReads(new AgentGovernanceReads(provider.GetRequiredService<IAgentFrameworkWorkspaceService>()), fixture)));
    }
}

internal enum GovernanceBrowserMode { Normal, HoldDetail, HoldList, FailDetail, FailList, Long, Removed, Poison }

internal sealed class GovernanceBrowserState {
    public bool DefinitionsEnabled { get; init; }
    public bool CompletionEnabled { get; init; }
    public ConversationBrowserFixture Conversations { get; } = new();
    public AgentSettingsBrowserFixture Settings { get; } = new();
    public DefinitionCatalogBrowserFixture Definitions { get; } = new();
    public const string Denied = "governance-denied-payload";
    public static readonly Guid AgentA = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000001");
    public static readonly Guid AgentB = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000002");
    public static readonly Guid Run1 = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000011");
    public static readonly Guid Run2 = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000012");
    public static readonly Guid RunB = Guid.Parse("83cbd161-4bcb-45f9-9cb7-130000000021");
    public static readonly DateTimeOffset Observed = new(2026, 3, 29, 1, 30, 0, TimeSpan.Zero);
    private TaskCompletionSource held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationToken owner;
    public HistoryBrowserState History { get; } = new();
    public GovernanceBrowserMode Mode { get; set; }
    public DiagnosticsBrowserMode DiagnosticsMode { get; set; }
    public int DiagnosticsDashboardReads;
    public int CatalogReads;
    public int ListReads;
    public int DetailReads;
    public bool OwnerCanceled => owner.IsCancellationRequested;

    public async Task HoldAsync(CancellationToken token) {
        owner = token;
        await held.Task;
    }

    public void Release() {
        held.TrySetResult();
        held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static ExecutionRunRecord Run(Guid id, Guid agent, string title, int minutes)
        => new(id, agent, null, title, "manual", "", "", "", "", "", "{}", Denied, Denied, "Fixture", "governance-fixture",
            ExecutionState.Completed, RunOutcome.Succeeded, Observed.AddMinutes(minutes), Observed.AddMinutes(minutes),
            null, null, "", JsonSerializer.Serialize(new { hidden = Denied }), []) { StructuredOutputRawOutput = Denied, StructuredOutputValidationErrorsJson = JsonSerializer.Serialize(new[] { Denied }) };
}

internal sealed class GovernanceBrowserReads(IAgentGovernanceReads inner, GovernanceBrowserState fixture) : IAgentGovernanceReads {
    public Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken token) {
        Interlocked.Increment(ref fixture.CatalogReads);
        return inner.ReadAgentsAsync(token);
    }

    public async Task<IReadOnlyList<ExecutionRunRecord>> ReadRunsAsync(Guid? agentId, CancellationToken token) {
        Interlocked.Increment(ref fixture.ListReads);
        var mode = fixture.Mode;
        var rows = await inner.ReadRunsAsync(agentId, token);
        if (mode == GovernanceBrowserMode.HoldList && agentId == GovernanceBrowserState.AgentA) {
            await fixture.HoldAsync(token);
        }
        if (mode == GovernanceBrowserMode.FailList) {
            throw new IOException(GovernanceBrowserState.Denied);
        }
        return rows.Where(run => mode != GovernanceBrowserMode.Removed || run.Id != GovernanceBrowserState.Run2)
            .Select(run => Transform(run, mode)).ToArray();
    }

    public async Task<ExecutionRunDetail> ReadDetailAsync(Guid id, CancellationToken token) {
        Interlocked.Increment(ref fixture.DetailReads);
        var mode = fixture.Mode;
        var detail = await inner.ReadDetailAsync(id, token);
        if (mode == GovernanceBrowserMode.HoldDetail && id == GovernanceBrowserState.Run1) {
            await fixture.HoldAsync(token);
        }
        if (mode == GovernanceBrowserMode.FailDetail) {
            throw new IOException(GovernanceBrowserState.Denied);
        }
        return mode == GovernanceBrowserMode.Poison
            ? GovernancePoisonFixture.Create(detail.Run)
            : detail with { Run = Transform(detail.Run, mode) };
    }

    private static ExecutionRunRecord Transform(ExecutionRunRecord run, GovernanceBrowserMode mode)
        => mode == GovernanceBrowserMode.Long ? run with {
            Title = "<img id='governance-injected' src=x> " + new string('x', 1800),
            ProviderName = "<strong>Provider</strong> " + new string('p', 1800)
        } : mode == GovernanceBrowserMode.Poison ? GovernancePoisonFixture.Create(run).Run : run;
}
