using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using CanDoItAll.Web;
using CanDoItAll.Web.Composition;

var repoRoot = Directory.GetCurrentDirectory();
var apiPort = GetFreeTcpPort();
var apiBaseUrl = $"http://127.0.0.1:{apiPort}";

await using var testEnvironment = CanDoItAllTestEnvironment.Create("architect-ollama-smoke");
var profile = testEnvironment.CreateManagedSqliteProfile("primary");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = "ArchitectOllamaSmoke",
    ContentRootPath = repoRoot,
    EnvironmentName = Environments.Development
});
builder.WebHost.UseUrls(apiBaseUrl);
builder.Configuration.AddConfiguration(TestApplicationBootstrap.BuildConfiguration(profile));
builder.Services.AddLogging();
builder.Services.TryAddSingleton<IJSRuntime, UnsupportedJsRuntime>();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllInfrastructure(builder.Configuration, builder.Environment, CanDoItAll.Web.Composition.ModuleAssemblies.All);
builder.Services.AddCanDoItAllRuntimeDatabaseSwitching();
builder.Services.AddCanDoItAllRuntimeModules();
builder.Services.AddMermaidJS();
builder.Services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
foreach (var hostedService in builder.Services
             .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
             .ToList())
{
    builder.Services.Remove(hostedService);
}

var app = builder.Build();
app.MapProjectStructureAgentApi();
await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);

ProjectStructureAgentSetupGuide setupGuide;
await using (var adminScope = app.Services.CreateAsyncScope())
{
    var administrationService = adminScope.ServiceProvider.GetRequiredService<ProjectStructureAgentAdministrationService>();
    await administrationService.SaveSettingsAsync(new ProjectStructureAgentWorkspaceSettingsModel
    {
        CentralBaseUrl = apiBaseUrl,
        InstallScriptPath = @"tools\Install-CanDoItAllProjectStructureMcp.ps1",
        SetupReadmePath = @"docs\project-structure-mcp-setup.md",
        DefaultAutoApproveMinutes = 60,
        DefaultApprovalRequiredMinutes = 60
    });

    var profileSave = await administrationService.SaveProfileAsync(new ProjectStructureAgentProfileEditorModel
    {
        Name = "Architect Smoke Agent",
        Description = "Temporary smoke-test token for the project-structure MCP.",
        IsEnabled = true,
        CapabilityMask = ProjectStructureAgentCapability.All,
        AutoApproveMinutes = 60,
        ApprovalRequiredMinutes = 60,
        RequireApprovalForAllMutations = false,
        GenerateNewToken = true
    });

    if (!profileSave.IsSuccess)
    {
        throw new InvalidOperationException(profileSave.Errors.FirstOrDefault()?.Message ?? "Project-structure profile save failed.");
    }

    setupGuide = await administrationService.BuildSetupGuideAsync(profileSave.Value);
}

try
{
    var settingsPath = Path.Combine(repoRoot, ".codex-temp", "architect-ollama-smoke", "CanDoItAll.Mcp.ProjectStructure.smoke.json");
    Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
    await File.WriteAllTextAsync(settingsPath, setupGuide.SettingsJson);
    Guid deliveryProjectId;
    ProjectStructureNode deliveryBlock;
    ProjectStructureNode architectureDecision;
    ProjectStructureNode buildFeature;
    ProjectStructureNode releaseValidation;
    ProjectStructureNode releaseMilestone;

    await using (var seedScope = app.Services.CreateAsyncScope())
    {
        var projectsService = seedScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = seedScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectSave = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Release Readiness Portal",
            Description = "Temporary smoke-test project for the architecture steward.",
            Objective = "Summarize the important structure, milestones, work items, and risks for a generic delivery.",
            CurrentPhase = "Architecture",
            Status = ProjectStatus.Active
        });

        if (!projectSave.IsSuccess)
        {
            throw new InvalidOperationException(projectSave.Errors.FirstOrDefault()?.Message ?? "Delivery project seed failed.");
        }

        deliveryProjectId = projectSave.Value;
        deliveryBlock = await workbench.CreateObjectAsync(
            deliveryProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delivery lane",
                "Delivery branch",
                "Owns implementation, validation, and release evidence.",
                null,
                420,
                220,
                null,
                null,
                "delivery"));
        architectureDecision = await workbench.CreateObjectAsync(
            deliveryProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Decision,
                "Canonical workflow boundary",
                "Architecture decision",
                "Decide how state, validation rules, and release evidence are shared.",
                deliveryBlock.Id,
                620,
                180,
                null,
                null,
                "decision",
                null,
                """{"owners":["Architecture"],"risk":"medium"}"""));
        buildFeature = await workbench.CreateObjectAsync(
            deliveryProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build primary interaction flow",
                "Implementation",
                "Implement the requested user workflow with validation.",
                deliveryBlock.Id,
                640,
                320,
                null,
                null,
                "task",
                null,
                """{"owners":["Engineering"],"surface":"Blazor"}""",
                7200));
        releaseValidation = await workbench.CreateObjectAsync(
            deliveryProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ValidationRun,
                "Release validation sweep",
                "QA gate",
                "Verify core behavior, error handling, and release evidence.",
                deliveryBlock.Id,
                840,
                320,
                null,
                null,
                "validation",
                null,
                """{"owners":["QA"],"evidence":"required"}""",
                3600));
        releaseMilestone = await workbench.CreateObjectAsync(
            deliveryProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Milestone,
                "Delivery v1 sign-off",
                "Release milestone",
                "Ship only after architecture decision, feature work, and validation are finished.",
                deliveryBlock.Id,
                1040,
                260,
                null,
                null,
                "release",
                null,
                """{"owners":["Release"]}"""));

        await workbench.LinkObjectsAsync(deliveryProjectId, buildFeature.Id, architectureDecision.Id, ProjectObjectLinkKind.DependsOn);
        await workbench.LinkObjectsAsync(deliveryProjectId, releaseValidation.Id, buildFeature.Id, ProjectObjectLinkKind.DependsOn);
        await workbench.LinkObjectsAsync(deliveryProjectId, releaseMilestone.Id, releaseValidation.Id, ProjectObjectLinkKind.DependsOn);
    }

    await app.StartAsync();

    await using var runtimeScope = app.Services.CreateAsyncScope();
    var workspaceService = runtimeScope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();

    var ollamaProvider = (await workspaceService.ListProvidersAsync())
        .Single(item => item.Kind == CanDoItAll.AgentFramework.Models.ProviderKind.Ollama &&
                        string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));
    var architectAgent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
        .Single(item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal)) with
    {
        ProviderProfileId = ollamaProvider.Id,
        Model = ollamaProvider.DefaultModel
    };
    var selectedCapabilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "project-structure-central",
        "provider-native-web-search",
        "provider-health"
    };
    var capabilityIds = architectAgent.Capabilities
        .Where(item => selectedCapabilityKeys.Contains(item.CapabilityKey))
        .Select(item => item.CapabilityId)
        .ToHashSet();
    var capabilities = (await workspaceService.ListCapabilitiesAsync())
        .Where(item => capabilityIds.Contains(item.Id))
        .Select(capability => string.Equals(capability.Key, "project-structure-central", StringComparison.OrdinalIgnoreCase)
            ? RewriteProjectStructureCapability(capability, repoRoot, settingsPath)
            : capability)
        .ToList();
    var session = new ChatSessionRecord(
        Guid.NewGuid(),
        architectAgent.Id,
        "Architect Ollama smoke",
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        Messages: []);
    var runtime = new MafAgentRuntime(repoRoot, app.Services);
    var progress = new List<string>();

    var response = await runtime.RunAsync(
        architectAgent,
        ollamaProvider,
        session,
        capabilities,
        [],
        $"The delivery project structure is stored under project id {deliveryProjectId}. Do not use workspace search. Use the project-structure MCP, preferably project_structure_read for that exact project id, then summarize the important architecture and delivery points. Mention the delivery lane, the architecture decision, the implementation work item, the validation gate, the release milestone, and the dependency chain. Keep the answer concise and factual.",
        runtimeSessionKey: null,
        progressCallback: (_, phase, message) =>
        {
            progress.Add($"{phase}: {message}");
            return Task.CompletedTask;
        },
        cancellationToken: CancellationToken.None,
        suppressApprovalRequirements: true);

    Console.WriteLine("=== Progress ===");
    foreach (var entry in progress)
    {
        Console.WriteLine(entry);
    }

    Console.WriteLine();
    Console.WriteLine("=== Response ===");
    Console.WriteLine(response.ResponseText);
    Console.WriteLine();
    Console.WriteLine("=== Tokens ===");
    Console.WriteLine($"Input={response.InputTokens} Output={response.OutputTokens} ToolCalls={response.ToolCalls}");
}
finally
{
    await app.StopAsync();
}

static CapabilityCatalogItem RewriteProjectStructureCapability(CapabilityCatalogItem capability, string repoRoot, string settingsPath)
{
    var configuration = JsonNode.Parse(capability.ConfigurationJson)?.AsObject()
        ?? throw new InvalidOperationException("Project-structure capability configuration could not be parsed.");
    var allowedTools = configuration["allowedTools"]?.AsArray()
        ?? throw new InvalidOperationException("Project-structure capability is missing allowedTools.");
    var rewritten = new JsonObject
    {
        ["transport"] = "stdio",
        ["serverName"] = "candoitall-project-structure",
        ["command"] = "dotnet",
        ["workingDirectory"] = repoRoot,
        ["approvalMode"] = "NeverRequire",
        ["arguments"] = new JsonArray
        {
            Path.Combine(repoRoot, "src", "CanDoItAll.Mcp.ProjectStructure", "bin", "Debug", "net10.0", "CanDoItAll.Mcp.ProjectStructure.dll"),
            "--settings",
            settingsPath
        },
        ["allowedTools"] = allowedTools.DeepClone()
    };

    return capability with
    {
        ConfigurationJson = rewritten.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web))
    };
}

static HttpClient CreateAgentHttpClient(string baseUrl, string token, string repoRoot)
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(baseUrl, UriKind.Absolute)
    };
    client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentId, "architect-ollama-smoke");
    client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentName, "Architect Ollama Smoke");
    client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.MachineName, Environment.MachineName);
    client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, repoRoot);
    client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.BranchName, "codex/architect-ollama-smoke");
    client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
    client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentToken, token);
    return client;
}

static async Task<T> PostAndReadAsync<T>(HttpClient client, string path, object payload)
{
    using var response = await client.PostAsJsonAsync(path, payload);
    if (response.StatusCode != HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Request to '{path}' failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }

    return await response.Content.ReadFromJsonAsync<T>()
           ?? throw new InvalidOperationException($"Request to '{path}' returned no payload.");
}

static int GetFreeTcpPort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    try
    {
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
    finally
    {
        listener.Stop();
    }
}

file sealed class UnsupportedJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        throw new NotSupportedException($"JavaScript interop '{identifier}' is not available in this smoke harness.");
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        throw new NotSupportedException($"JavaScript interop '{identifier}' is not available in this smoke harness.");
    }
}
