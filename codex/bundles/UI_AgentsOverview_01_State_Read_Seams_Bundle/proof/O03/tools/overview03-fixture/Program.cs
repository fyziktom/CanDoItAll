using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components.Overview;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

var repository = Path.GetFullPath(args[0]);
var closure = Path.Combine(repository, "codex/bundles/UI_AgentsOverview_01_State_Read_Seams_Bundle/proof/O02/closure.md");
if (!File.Exists(closure) || !File.ReadAllText(closure).Contains("CLOSED", StringComparison.Ordinal)) {
    throw new InvalidOperationException("O02 must close before the measurement fixture is prepared.");
}
var output = Path.Combine(repository, ".mcp-state/overview03-data");
Directory.CreateDirectory(output);
if (File.Exists(Path.Combine(output, "receipt.json"))) {
    throw new InvalidOperationException("The frozen isolated fixture already exists.");
}
var environment = CanDoItAllTestEnvironment.CreateUnder(output, "isolated");
var database = environment.CreatePostgreSqlProfile("overview-measurement");
var overrides = new Dictionary<string, string?> {
    ["Api:Authorization:Enabled"] = "false",
    ["ApiAccess:Authorization:Enabled"] = "false",
    ["WebHost:HttpsRedirectionEnabled"] = "false",
    ["Logging:LogLevel:Default"] = "Warning",
    ["Workflows:ExampleSeed:Enabled"] = "false",
    ["Workflows:ExampleSeed:SeedSampleWorkspaceFiles"] = "false"
};
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());
var observed = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);
await using (var services = await TestApplicationBootstrap.BuildServiceProviderAsync(database, "CanDoItAll.Web", TestSchemaBootstrapModules.Full, overrides)) {
    await using var scope = services.CreateAsyncScope();
    var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
    var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
    var runs = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
    var agents = new List<Guid>();
    for (var index = 0; index < 2; index++) {
        var providerId = Guid.Parse($"03100000-0000-0000-0000-{index + 1:000000000000}");
        var agentId = Guid.Parse($"03200000-0000-0000-0000-{index + 1:000000000000}");
        var providerName = index == 0 ? "Overview local inference" : "Overview evaluation";
        var agentName = index == 0 ? "Product research" : "Delivery governance";
        await registry.SaveProviderAsync(new ProviderProfileEditorModel {
            Id = providerId, Name = providerName, Kind = ProviderKind.Ollama,
            Transport = ProviderTransportKind.ChatCompletions, BaseUrl = "http://127.0.0.1:11434",
            DefaultModel = "overview-rendering", SuggestedModels = ["overview-rendering"],
            IsEnabled = true, IsPrivateProvider = true, ModelPrices = [new() { Model = "overview-rendering" }]
        });
        await workspace.SaveAgentAsync(new AgentEditorModel {
            Id = agentId, Name = agentName, RoleTitle = "UI measurement",
            Instructions = "Rendering fixture; never invoke a model or tool.",
            ProviderProfileId = providerId, Model = string.Empty, Status = AgentLifecycleStatus.Active,
            AvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarUrls[index]
        });
        agents.Add(agentId);
        for (var observation = 0; observation < 3; observation++) {
            var sequence = index * 3 + observation + 1;
            var runId = Guid.Parse($"03300000-0000-0000-0000-{sequence:000000000000}");
            var outcome = observation == 2 ? RunOutcome.Failed : RunOutcome.Succeeded;
            var run = new ExecutionRunRecord(runId, agentId, null, "Overview rendering evidence", "ui-measurement", "overview03",
                "", "", "fixture", "test", "{}", "Synthetic rendering evidence", "No runtime was invoked", providerName,
                "overview-rendering", outcome == RunOutcome.Failed ? ExecutionState.Failed : ExecutionState.Completed,
                outcome, observed, observed, observed, observed, "", null, [], ProviderProfileId: providerId);
            var observationValue = new ProviderUsageObservation(Guid.Parse($"03400000-0000-0000-0000-{sequence:000000000000}"), observed,
                providerName, ProviderKind.Ollama, "overview-rendering", ProviderTransportKind.ChatCompletions,
                ProviderUsageSourcePhases.AgentRuntime, observation == 2 ? ProviderUsageObservationStatus.UsageUnavailable : ProviderUsageObservationStatus.Observed,
                1000 * sequence, 0, 200 * sequence, 0, 1200 * sequence, 0) {
                AgentId = agentId, ExecutionRunId = runId, ProviderProfileId = providerId,
                CalculatedCostUsd = observation == 2 ? null : 0.02m * sequence
            };
            await runs.SaveExecutionRunDetailAsync(new(run, null, [], []) { UsageObservations = [observationValue] });
        }
    }
    await workspace.SaveAgentTeamAsync(new AgentTeamEditorModel {
        Id = Guid.Parse("03500000-0000-0000-0000-000000000001"), Name = "Research and delivery", AgentIds = agents
    });
    await scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>().EnsureCurrentOrganizationCatalogAsync();
    var query = scope.ServiceProvider.GetRequiredService<ProviderUsageQueryService>();
    var catalogAgents = await workspace.ListAgentsAsync(includeTemplates: false);
    var avatars = catalogAgents.ToDictionary(agent => agent.Id.ToString("D"), agent => agent.AvatarImageUrl);
    var overview = await workspace.GetAgentOverviewAsync();
    var usage = await query.QueryAsync(ProviderUsageWorkloadSelection.Both);
    if (usage.Totals.UsageObservationCount != 6 || usage.Providers.Count != 2 || usage.Consumers.Count != 2) {
        throw new InvalidOperationException("The registered usage projection did not return the exact canonical fixture.");
    }
    var state = AgentsOverviewPresentation.Create(overview, usage, ProviderUsageWorkloadSelection.Both, false, false, null, null, avatars);
    await File.WriteAllTextAsync(Path.Combine(output, "overview.json"), JsonSerializer.Serialize(state, json));
}
var variables = new Dictionary<string, string>(database.CreateEnvironmentVariables(overrides));
var connection = new NpgsqlConnectionStringBuilder(variables["Database__ConnectionString"]);
var passwordPath = Path.Combine(output, "database-password.txt");
await File.WriteAllTextAsync(passwordPath, connection.Password ?? string.Empty);
connection.Remove("Password");
variables["Database__ConnectionString"] = connection.ConnectionString;
variables["Database__PasswordFile"] = passwordPath;
await File.WriteAllTextAsync(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(variables));
await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
    state = "Prepared", databaseName = connection.Database, profile = database.ProfileKey,
    fixture = "overview.json", root = environment.RootPath,
    scope = "Isolated canonical production stores and queries; synthetic completed observations; no provider invocation."
}, json));
Console.WriteLine("Prepared isolated Overview measurement fixture; credentials remain in ignored private files.");
