using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Modules.Security;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using ProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using ProviderEditor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class Maf122ProcessExecutionIntegrationTests(ITestOutputHelper output) {
    private const string DefinitionKey = "maf122-note";
    private const string StepKey = "write-note";
    private const string RoleKey = "delivery-manager";
    private const string Model = "maf122-fixture-model";
    private const int NativeRequestAttempts = 4;

    public enum ProviderScenario { Complete, Unauthorized, TransientBeforeEffect, RepairFinalizer, ExhaustNativeRetryBudget, CancelWhileWaiting }

    [Theory]
    [InlineData(ProviderScenario.Complete)]
    [InlineData(ProviderScenario.Unauthorized)]
    [InlineData(ProviderScenario.TransientBeforeEffect)]
    [InlineData(ProviderScenario.RepairFinalizer)]
    [InlineData(ProviderScenario.ExhaustNativeRetryBudget)]
    [InlineData(ProviderScenario.CancelWhileWaiting)]
    public async Task Real_MAF_process_dispatch_preserves_failure_recovery_and_artifact_identity(ProviderScenario scenario) {
        await using var environment = CanDoItAllTestEnvironment.Create("maf122-process-provider");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var packRoot = WriteTemplate(environment.RootPath);
        using var wire = new ProviderWire(scenario);
        using var logs = new FailureLogs();
        using var client = new HttpClient(wire);
        await using var app = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigureServices = services => {
                services.AddSingleton<ILoggerProvider>(logs);
                services.Replace(ServiceDescriptor.Singleton(new ProcessTemplatePackLoader(packRoot)));
                services.Replace(ServiceDescriptor.Singleton<IProviderHttpClientSelector>(new ProviderClient(client)));
            }
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId,
            new() { Name = "MAF process acceptance" })).IsSuccess);
        var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = "MAF process fixture credential", Kind = SecretKind.ApiKey,
            SecretValue = "fixture-placeholder", Scope = "workspace"
        });
        Assert.True(secret.IsSuccess);
        var providerId = await services.GetRequiredService<IProviderProfileRegistry>().SaveProviderAsync(new ProviderEditor {
            Name = "MAF process fixture", BaseUrl = "https://provider.example.test/v1",
            ApiKeyEnvironmentVariable = $"secret:{secret.Value:D}", DefaultModel = Model,
            Transport = ProviderTransportKind.ChatCompletions, SupportsStreaming = false, SuggestedModels = [Model]
        });
        var workspace = services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>().GetOrganizationWorkspaceService();
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        var catalog = await store.LoadCatalogAsync();
        var agent = catalog.Agents.First() with {
            Id = Guid.NewGuid(), Name = "MAF acceptance delivery manager", RoleTitle = "Delivery manager",
            Status = AgentLifecycleStatus.Active, IsTemplate = false, TemplateKey = string.Empty,
            ProviderProfileId = providerId, Model = Model, Workload = AgentWorkloadKind.Management,
            ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            Permissions = AgentPermissionsPolicy.Default, Capabilities = [], Tags = [RoleKey],
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}",
                new AgentWorkspaceToolAccessSettings { Profile = AgentWorkspaceToolProfileKind.BusinessAnalysis })
        };
        await store.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, agent] });
        var authority = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.UserInterface);
        var request = new ProcessLaunchRequest(DefinitionKey, null, null, projectId, null,
            "maf122-acceptance", new Dictionary<string, string>(), RunReadiness: true, Execute: false) {
            Authority = authority, ProjectAdmission = authority.ProjectAdmission, CallerIntentId = new(Guid.NewGuid()),
            ExecutorOverrides = [new(StepKey, RoleKey, ProcessLaunchExecutorKinds.Agent,
                agent.Id.ToString("D"), agent.Name, "Bounded acceptance fixture")]
        };
        var launched = await services.GetRequiredService<ProcessLaunchApplicationService>().LaunchAsync(request);

        Assert.NotNull(launched.RunId);
        var stepId = Assert.Single(launched.LaunchPlan.Steps).StepInstanceId;
        var assignment = await services.GetRequiredService<IProcessRuntimeStepAssignmentStore>().LoadAsync(launched.RunId.Value, stepId);
        Assert.NotNull(assignment);
        wire.ArtifactPath = ProcessManagedArtifactEvidence.BuildManagedStepArtifactPath(assignment);
        wire.Marker = launched.RunId.Value.ToString();
        var dispatcher = services.GetRequiredService<ProcessRuntimeDispatchApplicationService>();
        var execution = dispatcher.ExecuteReadyAsync(launched.RunId.Value, "maf122-acceptance");
        if (scenario == ProviderScenario.CancelWhileWaiting) {
            await wire.Entered.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await using var cancellationScope = app.Services.CreateAsyncScope();
            var cancelled = await cancellationScope.ServiceProvider.GetRequiredService<ProcessRuntimeOperatorApplicationService>()
                .RequestCancellationAsync(new(launched.RunId.Value, "maf122-acceptance", "Cancel the owned acceptance run."));
            Assert.True(cancelled.Succeeded);
        }
        var result = await execution.WaitAsync(TimeSpan.FromMinutes(3));

        var state = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(launched.RunId.Value);
        Assert.NotNull(state);
        output.WriteLine($"Run={state.RunId} Status={state.Status} WireRequests={wire.Requests}");
        foreach (var entry in logs.Entries) {
            output.WriteLine(entry);
        }
        var runs = await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(ProcessRunId: launched.RunId.Value.ToString()));
        Assert.NotEmpty(runs);
        var details = await Task.WhenAll(runs.Select(run => workspace.GetExecutionRunDetailAsync(run.Id)));
        foreach (var detail in details.Where(detail => detail.Run.Outcome == RunOutcome.Failed)) {
            var terminal = Assert.Single(detail.ExecutionLog, entry => entry.State == ExecutionState.Failed);
            Assert.Equal(detail.Run.CompletedAtUtc, terminal.CreatedAtUtc);
        }
        if (scenario == ProviderScenario.CancelWhileWaiting) {
            Assert.Equal(ProcessRuntimeStatus.Cancelled, state.Status);
            Assert.Equal(RunOutcome.Cancelled, Assert.Single(details).Run.Outcome);
            Assert.All(details, detail => Assert.Empty(detail.ToolReceipts));
            Assert.Empty(state.AvailableArtifactSlots);
            await dispatcher.ExecuteReadyAsync(launched.RunId.Value, "maf122-acceptance");
            Assert.Equal(1, wire.Requests);
            return;
        }
        if (scenario == ProviderScenario.ExhaustNativeRetryBudget) {
            Assert.NotEqual(ProcessRuntimeStatus.Completed, state.Status);
            Assert.All(details, detail => Assert.Empty(detail.ToolReceipts));
            Assert.Empty(state.AvailableArtifactSlots);
            var exhausted = Assert.Single(state.AppliedResults);
            Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, exhausted.RecoveryDecision!.DecisionKind);
            Assert.Equal(ProcessRuntimeStatus.Blocked, state.Status);
            Assert.Equal(NativeRequestAttempts, wire.Requests);
            Assert.Equal(1, Assert.Single(state.Steps).AttemptNumber);
            Assert.Contains(exhausted.Diagnostics, diagnostic => diagnostic.ExecutionSafetyAttestation is not null);
            await dispatcher.ExecuteReadyAsync(launched.RunId.Value, "maf122-acceptance");
            Assert.Equal(NativeRequestAttempts, wire.Requests);
            output.WriteLine($"Native retry budget exhausted; process remains {state.Status}. Requests={wire.Requests}");
            return;
        }
        if (scenario == ProviderScenario.Unauthorized) {
            Assert.Equal(1, wire.Requests);
            Assert.Equal(ProcessRuntimeStatus.Failed, result.Status);
            var detail = Assert.Single(details);
            Assert.Equal(RunOutcome.Failed, detail.Run.Outcome);
            Assert.Empty(detail.ToolReceipts);
            Assert.Empty(detail.Artifacts);
            var receipt = Assert.Single(state.AppliedResults);
            Assert.Equal(detail.Run.Id, receipt.ExecutionRunId!.Value.Value);
            Assert.Contains(receipt.Diagnostics, diagnostic => diagnostic.Code == ProcessExecutionAdapterDiagnosticCodes.AgentProviderRejected);
            Assert.Equal(1, Assert.Single(state.Steps).AttemptNumber);
            return;
        }
        Assert.Equal(ProcessRuntimeStatus.Completed, result.Status);
        var writes = details.SelectMany(detail => detail.ToolReceipts)
            .Where(receipt => receipt.ToolName == ToolContractCatalog.WorkspaceWriteFile).ToArray();
        Assert.Single(writes, receipt => receipt.EffectState == AgentToolEffectState.Committed);
        Assert.Equal(2, writes.Length);
        var artifact = Assert.Single(details.SelectMany(detail => detail.Artifacts), item =>
            item.RelativePath.EndsWith($"/process-runs/{wire.Marker}/steps/{StepKey}.md", StringComparison.Ordinal));
        Assert.Equal(1, wire.WriteProposals);
        var note = services.GetRequiredService<IWorkspaceFileService>().ReadTextFile(artifact.RelativePath);
        Assert.Contains(wire.Marker, note.Content, StringComparison.Ordinal);
        Assert.Single(state.AvailableArtifactSlots);
        output.WriteLine($"Step={stepId} Attempts={Assert.Single(state.Steps).AttemptNumber} Artifact={artifact.Id} Path={artifact.RelativePath}");
        foreach (var receipt in state.AppliedResults) {
            output.WriteLine($"Execution={receipt.ExecutionRunId} Recovery={receipt.RecoveryDecision?.DecisionKind} Codes={string.Join(',', receipt.Diagnostics.Select(item => item.Code))}");
        }
        if (scenario == ProviderScenario.TransientBeforeEffect) {
            Assert.Equal(1, Assert.Single(state.Steps).AttemptNumber);
            Assert.Single(details);
            Assert.Equal(3, wire.Requests);
            Assert.Empty(state.BlockedRecoveryActions);
            output.WriteLine("One pre-effect provider retry recovered within the original durable execution; no process rework or human decision.");
        } else {
            Assert.Equal(1, Assert.Single(state.Steps).AttemptNumber);
        }
        Assert.Equal(scenario == ProviderScenario.RepairFinalizer ? 2 : 1, wire.FinalizerProposals);
    }

    private static string WriteTemplate(string root) {
        var packRoot = Path.Combine(root, "process-templates");
        var definitionRoot = Path.Combine(packRoot, DefinitionKey);
        Directory.CreateDirectory(definitionRoot);
        var definition = new ProcessTemplateDefinitionDocument {
            Key = DefinitionKey, DisplayName = "MAF bounded note", Summary = "Write one current-run note.",
            Criticality = "Low", OperatingMode = "GovernedLive", AutonomyLevel = "Guarded",
            RoleUsages = [new() {
                Key = RoleKey, RoleResourceKey = RoleKey, DisplayName = "Delivery manager",
                PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent, IsRequired = true
            }],
            Steps = [new() {
                Key = StepKey, Title = "Write acceptance note", StepKind = "Start",
                Notes = "Write a short note containing the unique process run id, then submit the required finalizer with its actual evidence reference.",
                AllowedOperations = [ProcessOperationContractNames.ReadProcessContext, ProcessOperationContractNames.WriteManagedProcessArtifacts],
                OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly,
                RoleAssignments = [new() { RoleKey = RoleKey, ResponsibilityKind = "Responsible", IsRequired = true }],
                ArtifactExpectations = [new() {
                    Key = "note", Title = "Acceptance note", ArtifactKind = "Evidence", IsRequired = true,
                    TrustRequirement = "ReviewRequired", SensitivityLevel = "Internal", RetentionDays = 1,
                    ValidationRequirementSummary = "Contains the current run marker and is persisted as steps/write-note.md."
                }]
            }]
        };
        var manifest = new ProcessTemplatePackManifest {
            PackKey = DefinitionKey, Name = "MAF acceptance", Version = "1.0.0", GeneratedAtUtc = DateTimeOffset.UtcNow,
            Processes = [new() { Key = DefinitionKey, RelativePath = DefinitionKey }]
        };
        File.WriteAllText(Path.Combine(packRoot, "manifest.json"), JsonSerializer.Serialize(manifest));
        File.WriteAllText(Path.Combine(definitionRoot, "definition.json"), JsonSerializer.Serialize(definition));
        return packRoot;
    }

    private sealed class FailureLogs : ILoggerProvider {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new FailureLogger(Entries);
        public void Dispose() { }

        private sealed class FailureLogger(ConcurrentQueue<string> entries) : ILogger {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) {
                if (IsEnabled(logLevel)) {
                    entries.Enqueue(formatter(state, exception));
                }
            }
        }
    }

    private sealed class ProviderClient(HttpClient client) : IProviderHttpClientSelector {
        public bool TryGetClient(ProviderProfile provider, [NotNullWhen(true)] out HttpClient? selectedClient) {
            Assert.Equal("provider.example.test", new Uri(provider.BaseUrl).Host);
            selectedClient = client;
            return true;
        }
    }

    private sealed class ProviderWire(ProviderScenario scenario) : HttpMessageHandler {
        public int Requests { get; private set; }
        public int WriteProposals { get; private set; }
        public int FinalizerProposals { get; private set; }
        public string ArtifactPath { get; set; } = string.Empty;
        public string Marker { get; set; } = string.Empty;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Requests++;
            Assert.InRange(Requests, 1, 24);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var streaming = payload.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean();
            if (scenario == ProviderScenario.CancelWhileWaiting) {
                Entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (scenario is ProviderScenario.Unauthorized or ProviderScenario.ExhaustNativeRetryBudget ||
                scenario == ProviderScenario.TransientBeforeEffect && Requests == 1) {
                var status = scenario == ProviderScenario.Unauthorized ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable;
                return Response(status, JsonSerializer.Serialize(new { error = new {
                    message = "Service request failed", type = "api_error",
                    code = scenario == ProviderScenario.Unauthorized ? "invalid_api_key" : "service_unavailable"
                } }));
            }
            var functions = payload.RootElement.GetProperty("tools").EnumerateArray()
                .Select(tool => tool.GetProperty("function").GetProperty("name").GetString()).ToArray();
            string toolName;
            object arguments;
            if (WriteProposals == 0) {
                toolName = ToolContractCatalog.WorkspaceWriteFile;
                arguments = new { path = ArtifactPath, content = "# MAF acceptance\n\n" + Marker + "\n", overwrite = false };
                WriteProposals++;
            } else {
                toolName = AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName;
                FinalizerProposals++;
                var invalid = scenario == ProviderScenario.RepairFinalizer && FinalizerProposals == 1;
                arguments = new { result = new ProcessStepOutcomeResult {
                    Status = ProcessStepOutcomeStatus.Completed, Reason = "The current-run note was written.",
                    EvidenceRefs = invalid ? [] : [ArtifactPath], HumanReadableSummaryMarkdown = "Created note " + Marker
                } };
            }
            Assert.Contains(toolName, functions);
            if (streaming) {
                var chunk = JsonSerializer.Serialize(new {
                    id = "chatcmpl-" + Requests, @object = "chat.completion.chunk", created = 1_785_710_400, model = Model,
                    choices = new[] { new {
                        index = 0, delta = new {
                            role = "assistant", tool_calls = new[] { new {
                                index = 0, id = "call-" + Requests, type = "function",
                                function = new { name = toolName, arguments = JsonSerializer.Serialize(arguments, AgentOutputJson.SerializerOptions) }
                            } }
                        }, finish_reason = "tool_calls"
                    } }
                });
                return new(HttpStatusCode.OK) {
                    Content = new StringContent("data: " + chunk + "\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
                };
            }
            return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new {
                id = "chatcmpl-" + Requests, @object = "chat.completion", created = 1_785_710_400, model = Model,
                choices = new[] { new {
                    index = 0, message = new {
                        role = "assistant", content = (string?)null,
                        tool_calls = new[] { new {
                            id = "call-" + Requests, type = "function",
                            function = new { name = toolName, arguments = JsonSerializer.Serialize(arguments, AgentOutputJson.SerializerOptions) }
                        } }
                    }, finish_reason = "tool_calls"
                } },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            }));
        }

        private static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
