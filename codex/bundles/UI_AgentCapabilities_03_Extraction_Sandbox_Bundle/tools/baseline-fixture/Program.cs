using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

var repository = Path.GetFullPath(args[0]);
var closure = Path.Combine(repository, "codex", "bundles", "UI_AgentCapabilities_02G_PreExtraction_Correctness_Bundle", "closure.md");
if (!File.Exists(closure) || !File.ReadAllText(closure).Contains("Status: CLOSED", StringComparison.Ordinal)) {
    throw new InvalidOperationException("Capabilities-02G must close before preparing the live measurement fixture.");
}
var output = Path.Combine(repository, ".mcp-state", "capa03-data");
Directory.CreateDirectory(output);
var environment = CanDoItAllTestEnvironment.CreateUnder(output, "isolated");
var database = environment.CreatePostgreSqlProfile("capabilities-measurement");
var overrides = new Dictionary<string, string?> {
    ["Api:Authorization:Enabled"] = "false",
    ["ApiAccess:Authorization:Enabled"] = "false",
    ["WebHost:HttpsRedirectionEnabled"] = "false",
    ["Logging:LogLevel:Default"] = "Warning",
    ["Workflows:ExampleSeed:Enabled"] = "false",
    ["Workflows:ExampleSeed:SeedSampleWorkspaceFiles"] = "false"
};
var agentId = Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000001");
var mcpId = Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000011");
var observed = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
var capabilities = new[] {
    new CapabilityCatalogItem(mcpId, CapabilityKind.McpServer, "capa03-mcp", "CAPA03 MCP", "Rendering specimen; no connector is configured or invoked.",
        "render-only:capa03-mcp", "{}", CapabilityProofStatus.Verified, "Display fixture only; no diagnostic was performed.", observed, false) { Tags = ["capa03-benchmark"] },
    new CapabilityCatalogItem(Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000012"), CapabilityKind.Skill, "capa03-skill", "CAPA03 Skill", "Rendering specimen for an unassigned skill.",
        "render-only:capa03-skill", "{}", CapabilityProofStatus.NotRun, "Display fixture only.", null, false) { Tags = ["capa03-benchmark"] },
    new CapabilityCatalogItem(Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000013"), CapabilityKind.Tool, "capa03-tool", "CAPA03 Tool", "Rendering specimen for an unassigned tool.",
        "render-only:capa03-tool", "{}", CapabilityProofStatus.PendingReview, "Display fixture only.", null, false) { Tags = ["capa03-benchmark"] }
};
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());
await using (var services = await TestApplicationBootstrap.BuildServiceProviderAsync(database, "CanDoItAll.Web", TestSchemaBootstrapModules.Full, overrides)) {
    await using var scope = services.CreateAsyncScope();
    var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
    var providerId = await registry.SaveProviderAsync(new ProviderProfileEditorModel {
        Id = Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000021"),
        Name = "CAPA03 rendering provider", Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
        BaseUrl = "http://127.0.0.1:11434", DefaultModel = "capa03-model", SuggestedModels = ["capa03-model"],
        IsEnabled = true, IsPrivateProvider = true, ModelPrices = [new() { Model = "capa03-model" }]
    });
    var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
    await store.UpdateCatalogAsync(catalog => catalog with {
        Capabilities = catalog.Capabilities.Concat(capabilities).ToList()
    });
    var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
    await workspace.SaveAgentAsync(new AgentEditorModel {
        Id = agentId, Name = "CAPA03 Measurement", RoleTitle = "UI measurement", Instructions = "Rendering fixture; never invoke a model or tool.",
        ProviderProfileId = providerId, Model = string.Empty, Status = AgentLifecycleStatus.Active,
        SelectedCapabilityIds = [mcpId], Tags = ["capa03-benchmark"], AvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarUrls[0]
    });
    await scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>().EnsureCurrentOrganizationCatalogAsync();
    using var session = new AgentCapabilitiesSession(new AgentCapabilitiesReads(workspace));
    if (!await session.LoadAsync(agentId) || session.SelectedAgent?.Id != agentId || session.Draft is null) {
        throw new InvalidOperationException("The exact measurement target did not load.");
    }
    await File.WriteAllTextAsync(Path.Combine(output, "capabilities.json"), JsonSerializer.Serialize(session.Snapshot, json));
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
    state = "Prepared", agentId, mcpId, profile = database.ProfileKey,
    fixture = "capabilities.json", root = environment.RootPath,
    scope = "Task-owned isolated data retained across pre/post full-app measurements; rendering only."
}, json));
Console.WriteLine("Prepared isolated rendering fixture; runtime credentials remain in ignored private files.");
