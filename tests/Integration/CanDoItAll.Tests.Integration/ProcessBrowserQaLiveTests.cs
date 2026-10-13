using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessBrowserQaLiveTests(ITestOutputHelper output) {
    private const string DefinitionKey = "browser-qa-acceptance";
    private const string RoleKey = "delivery-owner";
    private const string Model = "gpt-6-luna";

    [LiveBrowserQaFact]
    public async Task Real_provider_delivers_an_interactive_page_and_completes_native_browser_QA() {
        Assert.Equal("1", Environment.GetEnvironmentVariable("CANDOITALL_RUN_LIVE_PROVIDER_TESTS"));
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
        var evidence = Environment.GetEnvironmentVariable("CANDOITALL_LIVE_PROOF_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(evidence));
        Directory.CreateDirectory(evidence);
        await using var environment = CanDoItAllTestEnvironment.Create("browser-qa-live");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var marker = Guid.NewGuid().ToString("N");
        var pack = WriteTemplate(environment.RootPath, marker);
        await using var app = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = profile,
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton(new ProcessTemplatePackLoader(pack)))
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId,
            new() { Name = "Owned live browser QA proof" })).IsSuccess);
        var workspace = services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>().GetOrganizationWorkspaceService();
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        var catalog = await store.LoadCatalogAsync();
        var provider = catalog.Providers.First(item => item.Kind == ProviderKind.OpenAi &&
            item.Transport == ProviderTransportKind.Responses);
        var browser = catalog.Capabilities.Single(item => item.Key == "playwright-local-mcp");
        var agent = catalog.Agents.First() with {
            Id = Guid.NewGuid(), Name = "Owned browser QA agent", RoleTitle = "Delivery owner",
            Instructions = "Complete only the assigned bounded process step using actual tools. Never claim a file, browser observation or screenshot without a successful tool result. Use exact artifact references in the required finalizer.",
            Status = AgentLifecycleStatus.Active, IsTemplate = false, TemplateKey = string.Empty,
            ProviderProfileId = provider.Id, Model = Model, Workload = AgentWorkloadKind.Programming,
            ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged, EnableBackgroundResponses = false,
            Permissions = AgentPermissionsPolicy.Default, Tags = [RoleKey],
            Capabilities = [new(browser.Id, browser.Key, browser.Kind, browser.ProofStatus, browser.LastVerifiedAtUtc, browser.ProofNotes)],
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment))
        };
        await store.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, agent] });
        var authority = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.UserInterface);
        var launched = await services.GetRequiredService<ProcessLaunchApplicationService>().LaunchAsync(new(
            DefinitionKey, null, null, projectId, null, "live-browser-proof", new Dictionary<string, string>(),
            RunReadiness: true, Execute: false) {
            Authority = authority, ProjectAdmission = authority.ProjectAdmission, CallerIntentId = new(Guid.NewGuid()),
            ExecutorOverrides = [.. new[] { "implement", "qa" }.Select(step => new ProcessLaunchExecutorOverride(
                step, RoleKey, ProcessLaunchExecutorKinds.Agent, agent.Id.ToString("D"), agent.Name, "Owned live proof"))]
        });
        Assert.NotNull(launched.RunId);
        output.WriteLine($"Project={projectId} Process={launched.RunId} Model={Model} Fixture={environment.RootPath}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var dispatch = services.GetRequiredService<ProcessRuntimeDispatchApplicationService>();
        await dispatch.ExecuteReadyAsync(launched.RunId.Value, "live-browser-proof", timeout.Token);
        var state = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(launched.RunId.Value);
        var runs = await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(ProcessRunId: launched.RunId.Value.ToString()));
        var details = await Task.WhenAll(runs.Select(run => workspace.GetExecutionRunDetailAsync(run.Id)));
        await File.WriteAllTextAsync(Path.Combine(evidence, "process-state.json"), JsonSerializer.Serialize(state, JsonSerializerOptions.Web));
        foreach (var detail in details) {
            await File.WriteAllTextAsync(Path.Combine(evidence, $"execution-{detail.Run.Id:N}.json"), JsonSerializer.Serialize(detail, JsonSerializerOptions.Web));
            output.WriteLine($"Execution={detail.Run.Id} State={detail.Run.State} Outcome={detail.Run.Outcome} Tools={detail.ToolReceipts.Count}");
            foreach (var receipt in detail.ToolReceipts.Where(item => item.InvocationOutcome != AgentToolInvocationOutcome.Succeeded)) {
                output.WriteLine($"Tool={receipt.ToolName} Outcome={receipt.InvocationOutcome} Code={receipt.FailureCode} Message={receipt.FailureMessage}");
            }
        }
        Assert.NotNull(state);
        Assert.Equal(ProcessRuntimeStatus.Completed, state.Status);
        Assert.Equal(2, state.Steps.Count);
        Assert.All(state.Steps, step => Assert.Equal(1, step.AttemptNumber));
        var receipts = details.SelectMany(detail => detail.ToolReceipts).ToArray();
        foreach (var name in new[] { "workspace_write_file", "workspace_static_serve", "browser_navigate", "browser_snapshot", "browser_click", "browser_take_screenshot" }) {
            Assert.Contains(receipts, item => item.ToolName == name && item.InvocationOutcome == AgentToolInvocationOutcome.Succeeded);
        }
        Assert.DoesNotContain(receipts, item => item.ToolName.StartsWith("browser_", StringComparison.Ordinal) &&
            item.InvocationOutcome == AgentToolInvocationOutcome.Failed);
        var screenshot = Assert.Single(details.SelectMany(detail => detail.Artifacts), artifact =>
            artifact.RelativePath.Contains("/browser/", StringComparison.Ordinal) && artifact.RelativePath.EndsWith(".png", StringComparison.Ordinal));
        var source = Path.Combine(profile.WorkspaceRootPath, screenshot.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var bytes = await File.ReadAllBytesAsync(source);
        Assert.True(bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.Equal(1920, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(1080, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
        await File.WriteAllBytesAsync(Path.Combine(evidence, "qa-screenshot.png"), bytes);
        await File.WriteAllTextAsync(Path.Combine(evidence, "lineage.json"), JsonSerializer.Serialize(new {
            projectId, runId = launched.RunId, marker, model = Model, screenshot.RelativePath,
            executionIds = details.Select(detail => detail.Run.Id), toolCount = receipts.Length
        }, JsonSerializerOptions.Web));
        output.WriteLine($"Verified screenshot={screenshot.RelativePath}; marker={marker}");
    }

    private static string WriteTemplate(string root, string marker) {
        var pack = Path.Combine(root, "process-templates");
        var directory = Path.Combine(pack, DefinitionKey);
        Directory.CreateDirectory(directory);
        var implementation = Step("implement", "Implement counter", "Start",
            $"Create one self-contained index.html under this process's managed artifact root in app/index.html. The visible heading must be 'Counter proof {marker}'. Show a counter initially 0 and a button named Increment which increases it. Use inline CSS and JavaScript, no downloads, no external services, no build tooling. Write your required managed step note with the exact app directory and file references. Finish with the required finalizer and actual evidence references; QA belongs to the next step.",
            [ProcessOperationContractNames.ReadProcessContext, ProcessOperationContractNames.WriteManagedProcessArtifacts]);
        var qa = Step("qa", "Browser QA", "Work",
            "Read the implementation note. Serve the actual delivered app directory with workspace_static_serve. Use its returned loopback URL, resize the browser to 1920x1080, navigate, inspect a bounded depth=2 snapshot, click Increment, and confirm the counter is 1 with another bounded snapshot. Capture exactly one viewport screenshot with fullPage=false and a fresh filename under this process's managed screenshots directory. Use the returned VERIFIED artifact path in your step note and finalizer; do not report the requested filename as proof. Check console errors, stop the runtime with workspace_dotnet_stop, then finalize with actual note and screenshot evidence. Do not modify implementation files. Do not omit actual browser actions.",
            [ProcessOperationContractNames.ReadProcessContext, ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts, ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.LaunchRuntime, ProcessOperationContractNames.CaptureRuntimeProof]);
        qa.Order = 1;
        qa.DependsOnStepKey = implementation.Key;
        qa.ArtifactInputs = [new() { SourceStepKey = implementation.Key, ArtifactExpectationKey = "note" }];
        var definition = new ProcessTemplateDefinitionDocument {
            Key = DefinitionKey, DisplayName = "Owned live browser QA", Summary = "Actual delivery followed by browser QA.",
            Criticality = "Low", OperatingMode = "GovernedLive", AutonomyLevel = "Guarded",
            RoleUsages = [new() { Key = RoleKey, RoleResourceKey = RoleKey, DisplayName = "Delivery owner",
                PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent, IsRequired = true }],
            Steps = [implementation, qa]
        };
        File.WriteAllText(Path.Combine(pack, "manifest.json"), JsonSerializer.Serialize(new ProcessTemplatePackManifest {
            PackKey = DefinitionKey, Name = "Live browser proof", Version = "1.0.0", GeneratedAtUtc = DateTimeOffset.UtcNow,
            Processes = [new() { Key = DefinitionKey, RelativePath = DefinitionKey }]
        }));
        File.WriteAllText(Path.Combine(directory, "definition.json"), JsonSerializer.Serialize(definition));
        return pack;
    }

    private static ProcessTemplateDefinitionStepDocument Step(string key, string title, string kind, string notes, List<string> operations) => new() {
        Key = key, Title = title, StepKind = kind, Notes = notes, AllowedOperations = operations,
        OperationTargetScope = ProcessOperationContractNames.ManagedProcessArtifactsOnly,
        RoleAssignments = [new() { RoleKey = RoleKey, ResponsibilityKind = "Responsible", IsRequired = true }],
        ArtifactExpectations = [new() { Key = "note", Title = "Step evidence", ArtifactKind = "Evidence", IsRequired = true,
            TrustRequirement = "ReviewRequired", SensitivityLevel = "Internal", RetentionDays = 1,
            ValidationRequirementSummary = "Actual delivered files or browser observations with exact current-run references." }]
    };
}

internal sealed class LiveBrowserQaFactAttribute : FactAttribute {
    public LiveBrowserQaFactAttribute() {
        if (Environment.GetEnvironmentVariable("CANDOITALL_RUN_LIVE_PROVIDER_TESTS") != "1") {
            Skip = "Set CANDOITALL_RUN_LIVE_PROVIDER_TESTS=1 and configure the provider, PostgreSQL fixture and proof directory.";
        }
    }
}
