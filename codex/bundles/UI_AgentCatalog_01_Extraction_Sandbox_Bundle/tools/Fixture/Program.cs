using System.Text.Json;
using CanDoItAll.Tests.Support;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.AgentFramework.UI.Catalog;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

var repository = Path.GetFullPath(args[0]);
var output = Path.Combine(repository, ".mcp-state", "catalog-data");
Directory.CreateDirectory(output);
var environment = CanDoItAllTestEnvironment.CreateUnder(output, "isolated");
var database = environment.CreatePostgreSqlProfile("catalog-measurement");
var overrides = new Dictionary<string, string?> {
    ["Api:Authorization:Enabled"] = "false",
    ["ApiAccess:Authorization:Enabled"] = "false",
    ["WebHost:HttpsRedirectionEnabled"] = "false",
    ["Logging:LogLevel:Default"] = "Warning",
    ["Workflows:ExampleSeed:Enabled"] = "false",
    ["Workflows:ExampleSeed:SeedSampleWorkspaceFiles"] = "false"
};
await using (var services = await TestApplicationBootstrap.BuildServiceProviderAsync(
    database, "CanDoItAll.Web", TestSchemaBootstrapModules.Full, overrides)) {
    await using var scope = services.CreateAsyncScope();
    var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
    var providerId = await registry.SaveProviderAsync(new ProviderProfileEditorModel {
        Name = "Catalog isolated provider", Kind = ProviderKind.Ollama,
        Transport = ProviderTransportKind.ChatCompletions, BaseUrl = "http://127.0.0.1:11434",
        DefaultModel = "catalog-model", SuggestedModels = ["catalog-model"], IsEnabled = true,
        IsPrivateProvider = true, ModelPrices = [new() { Model = "catalog-model" }]
    });
    var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
    var ids = new List<Guid>();
    for (var i = 0; i < 12; i++) {
        ids.Add(await workspace.SaveAgentAsync(new AgentEditorModel {
            Name = i == 0 ? "Catalog representative with a deliberately long display name" : $"Catalog specialist {i + 1:00}",
            RoleTitle = i == 0 ? "" : "Catalog UI specialist",
            Summary = i == 0 ? "" : "Representative catalog card for controlled UI and asset verification.",
            Instructions = "Only a local rendering fixture. Do not call a model.",
            ProviderProfileId = providerId, Model = string.Empty, Status = AgentLifecycleStatus.Active,
            Tags = ["catalog-fixture", "architecture", "long-visible-tag"],
            AvatarImageUrl = i % 2 == 0 ? AgentAvatarImageCatalog.BundledAvatarUrls[i % 8] : ""
        }));
    }
    await workspace.SaveAgentTeamAsync(new AgentTeamEditorModel {
        Name = "Catalog measurement team", Description = "Representative controlled membership", AgentIds = ids.Take(6).ToList()
    });
    await scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>()
        .EnsureCurrentOrganizationCatalogAsync();
    var agents = await workspace.ListAgentsAsync(false);
    var teams = await workspace.ListAgentTeamsAsync();
    var providers = await scope.ServiceProvider.GetRequiredService<IProviderRuntimeAdministrationService>().ListProvidersAsync();
    var snapshot = new AgentCatalogSnapshot(agents, teams, providers.ToDictionary(item => item.Id, item => item.IsPrivateProvider));
    await File.WriteAllTextAsync(Path.Combine(output, "snapshot.json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
}
var variables = new Dictionary<string, string>(database.CreateEnvironmentVariables(overrides));
var connection = new NpgsqlConnectionStringBuilder(variables["Database__ConnectionString"]);
var passwordPath = Path.Combine(output, "database-password.txt");
await File.WriteAllTextAsync(passwordPath, connection.Password ?? "");
connection.Remove("Password");
variables["Database__ConnectionString"] = connection.ConnectionString;
variables["Database__PasswordFile"] = passwordPath;
variables["ASPNETCORE_ENVIRONMENT"] = "Development";
variables["DOTNET_ENVIRONMENT"] = "Development";
variables["DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"] = "1";
variables["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"] = "1";
await File.WriteAllTextAsync(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(variables));
Console.WriteLine(JsonSerializer.Serialize(new { state = "Prepared", profile = database.ProfileKey, root = environment.RootPath, cleanup = "Task-owned isolated database retained through both full-app measurement phases." }));
