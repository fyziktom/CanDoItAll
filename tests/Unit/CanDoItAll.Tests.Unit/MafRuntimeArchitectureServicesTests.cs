using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Unit;

public sealed class MafRuntimeArchitectureServicesTests
{
    [Fact]
    public void MafRuntimeArchitectureServices_registers_runtime_collaborators()
    {
        var services = new ServiceCollection();

        services.AddMafRuntimeArchitectureServices();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<MafRuntimeDependencyResolver>(provider.GetRequiredService<IMafRuntimeDependencyResolver>());
        Assert.IsType<MafProviderCredentialService>(provider.GetRequiredService<IMafProviderCredentialService>());
        Assert.IsType<MafProviderAgentFactory>(provider.GetRequiredService<IMafProviderAgentFactory>());
        Assert.IsType<MafProviderStreamingRunner>(provider.GetRequiredService<IMafProviderStreamingRunner>());
        Assert.IsType<RuntimeToolProviderComposer>(provider.GetRequiredService<IRuntimeToolProviderComposer>());
        Assert.IsType<RuntimeToolProviderAccessFilter>(provider.GetRequiredService<IRuntimeToolProviderAccessFilter>());
        Assert.IsType<NoOpMafRuntimeCompositionMetrics>(provider.GetRequiredService<IMafRuntimeCompositionMetrics>());
        Assert.IsType<MafApprovalContinuationDriver>(provider.GetRequiredService<IMafApprovalContinuationDriver>());
        Assert.IsType<MafRuntimeSessionPersistenceDriver>(provider.GetRequiredService<IMafRuntimeSessionPersistenceDriver>());
    }

    [Fact]
    public async Task MafProviderStreamingRunner_does_not_use_provider_timeout_as_runtime_agent_cancellation()
    {
        var runner = new MafProviderStreamingRunner(new TestMafProviderStreamingDispatchGate());
        var runtimeAgent = new DelayedStreamingAgent(TimeSpan.FromMilliseconds(1200));
        var runtimeSession = await runtimeAgent.CreateSessionAsync();
        var provider = CreateProviderProfile(configurationJson: "{\"timeoutSeconds\":1}");
        var updates = new List<AgentResponseUpdate>();

        await foreach (var update in runner.RunStreamingAsync(
                           provider,
                           "unit-model",
                           runtimeAgent,
                           runtimeSession,
                           [new ChatMessage(ChatRole.User, "run")],
                           new ChatClientAgentRunOptions(new ChatOptions()),
                           CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.True(runtimeAgent.DelayCompleted);
        Assert.False(runtimeAgent.DelayTokenWasCanceled);
        Assert.Contains(updates, update => update.Text.Contains("completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MafProviderStreamingRunner_times_out_an_idle_stream_and_cancels_the_underlying_enumerator()
    {
        var runner = new MafProviderStreamingRunner(
            new TestMafProviderStreamingDispatchGate(),
            _ => TimeSpan.FromMilliseconds(50));
        var runtimeAgent = new DelayedStreamingAgent(TimeSpan.FromMinutes(1));
        var runtimeSession = await runtimeAgent.CreateSessionAsync();
        var provider = CreateProviderProfile();

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var _ in runner.RunStreamingAsync(
                               provider,
                               "unit-model",
                               runtimeAgent,
                               runtimeSession,
                               [new ChatMessage(ChatRole.User, "run")],
                               new ChatClientAgentRunOptions(new ChatOptions()),
                               CancellationToken.None))
            {
            }
        });

        Assert.Contains("made no semantic progress", exception.Message, StringComparison.Ordinal);
        Assert.True(runtimeAgent.DelayTokenWasCanceled);
    }

    [Fact]
    public async Task MafProviderStreamingRunner_resets_the_semantic_idle_deadline_after_each_update()
    {
        var runner = new MafProviderStreamingRunner(
            new TestMafProviderStreamingDispatchGate(),
            _ => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(15));
        var runtimeAgent = new DelayedStreamingAgent(TimeSpan.FromMilliseconds(800), updateCount: 3);
        var runtimeSession = await runtimeAgent.CreateSessionAsync();
        var provider = CreateProviderProfile();
        var updates = new List<AgentResponseUpdate>();

        await foreach (var update in runner.RunStreamingAsync(
                           provider,
                           "unit-model",
                           runtimeAgent,
                           runtimeSession,
                           [new ChatMessage(ChatRole.User, "run")],
                           new ChatClientAgentRunOptions(new ChatOptions()),
                           CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.True(runtimeAgent.DelayCompleted);
        Assert.False(runtimeAgent.DelayTokenWasCanceled);
        Assert.Equal(3, updates.Count);
    }

    [Fact]
    public async Task MafProviderStreamingRunner_does_not_treat_empty_heartbeat_updates_as_semantic_progress()
    {
        var runner = new MafProviderStreamingRunner(
            new TestMafProviderStreamingDispatchGate(),
            _ => TimeSpan.FromMilliseconds(80));
        var runtimeAgent = new DelayedStreamingAgent(
            TimeSpan.FromMilliseconds(10),
            emitSemanticUpdates: false,
            runUntilCancelled: true);
        var runtimeSession = await runtimeAgent.CreateSessionAsync();
        var provider = CreateProviderProfile();

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var _ in runner.RunStreamingAsync(
                               provider,
                               "unit-model",
                               runtimeAgent,
                               runtimeSession,
                               [new ChatMessage(ChatRole.User, "run")],
                               new ChatClientAgentRunOptions(new ChatOptions()),
                               CancellationToken.None))
            {
            }
        });

        Assert.Contains("made no semantic progress", exception.Message, StringComparison.Ordinal);
        Assert.True(runtimeAgent.StreamCancellationWasRequested);
    }

    [Fact]
    public async Task MafProviderStreamingRunner_enforces_absolute_deadline_when_semantic_updates_continue()
    {
        var runner = new MafProviderStreamingRunner(
            new TestMafProviderStreamingDispatchGate(),
            _ => TimeSpan.FromSeconds(1),
            _ => TimeSpan.FromMilliseconds(80));
        var runtimeAgent = new DelayedStreamingAgent(
            TimeSpan.FromMilliseconds(10),
            runUntilCancelled: true);
        var runtimeSession = await runtimeAgent.CreateSessionAsync();
        var provider = CreateProviderProfile();

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var _ in runner.RunStreamingAsync(
                               provider,
                               "unit-model",
                               runtimeAgent,
                               runtimeSession,
                               [new ChatMessage(ChatRole.User, "run")],
                               new ChatClientAgentRunOptions(new ChatOptions()),
                               CancellationToken.None))
            {
            }
        });

        Assert.Contains("absolute stream deadline", exception.Message, StringComparison.Ordinal);
        Assert.True(runtimeAgent.StreamCancellationWasRequested);
    }

    [Fact]
    public void Maf_runtime_collaborators_are_top_level_types()
    {
        var collaboratorTypes = new[]
        {
            typeof(RuntimeCapabilityComposer),
            typeof(MafRuntimeAgentFactory),
            typeof(MafFinalizerToolFactory),
            typeof(MafRuntimeExecutionOptionsResolver),
            typeof(MafRuntimeToolInvocationResultClassifier),
            typeof(ContextCapabilityBuilder),
            typeof(SkillCapabilityBuilder),
            typeof(McpCapabilityBuilder),
            typeof(ToolCapabilityBuilder),
            typeof(WorkspaceRuntimePlugin),
            typeof(WorkspaceImageAnalysisModelResolver),
            typeof(StorageRuntimePlugin),
            typeof(WorkspaceSearchSupport),
            typeof(InputAttachmentPreparer),
            typeof(InputAttachmentSupport),
            typeof(RequestScopedSessionContentScrubber),
            typeof(ProcessArtifactRecoveryService),
            typeof(ProviderRuntimeDiagnostics),
            typeof(RepeatedToolInvocationGuard),
            typeof(RequiredFinalizerCapturedException)
        };

        Assert.All(collaboratorTypes, type => Assert.False(type.IsNested, $"{type.FullName} must not be nested under MafAgentRuntime."));
    }

    [Fact]
    public void MafAgentRuntime_is_not_a_split_partial_namespace()
    {
        var root = FindRepoRoot();
        var runtimeRoot = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime");
        var runtimeFiles = Directory
            .EnumerateFiles(runtimeRoot, "MafAgentRuntime*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(runtimeRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["MafAgentRuntime.cs"], runtimeFiles);
        Assert.DoesNotContain(
            "partial class MafAgentRuntime",
            File.ReadAllText(Path.Combine(runtimeRoot, "MafAgentRuntime.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void MafAgentRuntime_partials_do_not_hide_private_nested_runtime_types()
    {
        var root = FindRepoRoot();
        var runtimeRoot = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime");
        var violations = Directory
            .EnumerateFiles(runtimeRoot, "MafAgentRuntime*.cs", SearchOption.AllDirectories)
            .SelectMany(path => FindPrivateNestedTypeDeclarations(path, runtimeRoot))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void MafAgentRuntime_does_not_own_capability_composition_partials()
    {
        var root = FindRepoRoot();
        var capabilityRoot = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "Capabilities");
        var violations = Directory
            .EnumerateFiles(capabilityRoot, "MafAgentRuntime.Capabilities*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void MafAgentRuntime_no_longer_owns_approval_and_session_persistence_algorithms()
    {
        var root = FindRepoRoot();
        var runtimeSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.cs"));

        Assert.DoesNotContain("ConcurrentDictionary<Guid, IReadOnlyList<ToolApprovalRequestContent>>", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static PendingToolApprovalRecord MapPendingApproval", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task<string?> TrySerializePersistableRuntimeSessionAsync", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private IEnumerable<ChatMessage> CreateApprovalInputMessages", runtimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCapabilityComposer_is_not_a_split_partial_namespace()
    {
        var root = FindRepoRoot();
        var capabilityRoot = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "Capabilities");
        var partialComposerDeclarations = Directory
            .EnumerateFiles(capabilityRoot, "RuntimeCapabilityComposer*.cs", SearchOption.TopDirectoryOnly)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = Path.GetRelativePath(root, path),
                    Line = index + 1,
                    Text = line
                }))
            .Where(item => item.Text.Contains("partial class RuntimeCapabilityComposer", StringComparison.Ordinal))
            .Select(item => $"{item.Path}:{item.Line}:{item.Text.Trim()}")
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(partialComposerDeclarations);
    }

    [Fact]
    public void WorkspaceRuntimePlugin_no_longer_owns_image_model_resolution()
    {
        var root = FindRepoRoot();
        var pluginSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "Workspace",
            "WorkspaceRuntimePlugin.cs"));

        Assert.DoesNotContain("internal static string ResolveProviderImageAnalysisModel", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool IsVisionCapableProviderModel", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageAnalysisModelConfigurationKeys", pluginSource, StringComparison.Ordinal);
        Assert.Contains(
            "WorkspaceImageAnalysisModelResolver.ResolveProviderImageAnalysisModel",
            File.ReadAllText(Path.Combine(
                root,
                "src",
                "MAF",
                "Common",
                "CanDoItAll.AgentFramework.Maf",
                "Runtime",
                "Input",
                "InputAttachmentPreparer.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCapabilityDescriptorCatalog_creates_tool_descriptor_without_composer()
    {
        var catalog = new RuntimeCapabilityDescriptorCatalog();
        var capability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            ModelCapabilityKind.Tool,
            "workspace-read-file",
            "Read workspace file",
            "Read a workspace text file.",
            string.Empty,
            """{"tool":"workspace_read_file"}""",
            CapabilityProofStatus.Verified,
            string.Empty,
            DateTimeOffset.UtcNow,
            IsBuiltIn: true);

        var descriptor = catalog.CreateCatalogCapabilityDescriptor(capability);

        Assert.Equal("workspace_read_file", descriptor.RuntimeToolName?.Value);
        Assert.Contains(CapabilityOperationClassification.Read, descriptor.OperationClassifications);
    }

    [Fact]
    public void MafApprovalContinuationDriver_maps_and_replays_pending_function_approval()
    {
        var driver = new MafApprovalContinuationDriver();
        var toolCall = new FunctionCallContent(
            "call-001",
            "workspace_write_file",
            new Dictionary<string, object?>
            {
                ["path"] = "artifacts/result.md"
            });
        var request = new ToolApprovalRequestContent("approval-001", toolCall);

        var pending = driver.MapPendingApproval(request);

        Assert.Equal("approval-001", pending.ApprovalId);
        Assert.Equal("call-001", pending.CallId);
        Assert.Equal("workspace_write_file", pending.ToolName);
        Assert.Equal("function", pending.ToolKind);
        Assert.Contains("artifacts/result.md", pending.ArgumentsJson, StringComparison.Ordinal);

        var session = CreateSession();
        driver.StorePendingApprovals(session.Id, [request]);

        var messages = driver.CreateApprovalInputMessages(session, approved: true).ToList();

        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Single(message.Contents);
    }

    [Fact]
    public void MafApprovalContinuationDriver_rehydrates_legacy_pending_approval_records()
    {
        var driver = new MafApprovalContinuationDriver();
        var session = CreateSession(
            new ChatSessionRuntimeCompatibilityRecord(
                runtimeSessionKey: "conversation-001",
                serializedSessionStateJson: null,
                pendingApprovals:
                [
                    new PendingToolApprovalRecord(
                        "approval-001",
                        "call-001",
                        "workspace_write_file",
                        "function",
                        string.Empty,
                        """{"path":"artifacts/result.md"}""")
                ]));

        var messages = driver.CreateApprovalInputMessages(session, approved: false).ToList();

        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Single(message.Contents);
    }

    [Fact]
    public void MafRuntimeSessionPersistenceDriver_skips_governed_process_steps_without_pending_approvals()
    {
        var driver = new MafRuntimeSessionPersistenceDriver();
        var options = CreateExecutionOptions(contextIntent: AgentRuntimeContextIntent.Empty with
        {
            IsGovernedProcessStep = true
        });

        Assert.True(driver.ShouldSkipRuntimeSessionSerialization(options, []));
        Assert.Contains(
            "governed process step",
            driver.ResolveRuntimeSessionSerializationSkipMessage(options),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_tests_do_not_reflect_private_capability_composition_methods()
    {
        var root = FindRepoRoot();
        var unitTestRoot = Path.Combine(root, "tests", "Unit", "CanDoItAll.Tests.Unit");
        var forbiddenPatterns = new[]
        {
            "CreateCapabilityStateAsync",
            "CreateCapabilityStateCoreAsync",
            "CreateRuntimeCapabilityAccessPlan"
        };
        var violations = Directory
            .EnumerateFiles(unitTestRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                nameof(MafRuntimeArchitectureServicesTests) + ".cs",
                StringComparison.Ordinal))
            .SelectMany(path => FindForbiddenPrivateRuntimeReflection(path, unitTestRoot, forbiddenPatterns))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void MafRuntimeDependencyResolver_prefers_registered_provider_dependencies()
    {
        var gateway = new TestMafProviderRuntimeGateway();
        var streamingGate = new TestMafProviderStreamingDispatchGate();
        var services = new ServiceCollection();
        services.AddSingleton<IMafProviderRuntimeGateway>(gateway);
        services.AddSingleton<IMafProviderStreamingDispatchGate>(streamingGate);
        using var provider = services.BuildServiceProvider();
        var resolver = new MafRuntimeDependencyResolver();

        var dependencies = resolver.ResolveProviderDependencies(provider);

        Assert.Same(gateway, dependencies.ProviderRuntimeGateway);
        Assert.Same(streamingGate, dependencies.ProviderStreamingDispatchGate);
    }

    [Fact]
    public void RuntimeToolProviderComposer_orders_registrations_deterministically()
    {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());
        var late = new TestRuntimeToolProvider(
            20,
            CreateDescriptor("tests.late"),
            "late_runtime_tool");
        var early = new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.early"),
            "early_runtime_tool");

        var registrations = composer.ComposeRegistrations([late, early]);

        Assert.Equal(["tests.early", "tests.late"], registrations.Select(item => item.Descriptor.ProviderKey));
    }

    [Fact]
    public void RuntimeToolProviderComposer_rejects_duplicate_provider_keys()
    {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            composer.ComposeRegistrations(
            [
                new TestRuntimeToolProvider(10, CreateDescriptor("tests.duplicate"), "first_tool"),
                new TestRuntimeToolProvider(20, CreateDescriptor("tests.duplicate"), "second_tool")
            ]));

        Assert.Contains("Runtime tool provider key(s) must be unique", exception.Message, StringComparison.Ordinal);
        Assert.Contains("tests.duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeToolProviderComposer_attaches_tools_metadata_and_approval_wrappers()
    {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());
        var provider = new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.process"),
            "processes_runs_list",
            "processes_run_start");
        var registrations = composer.ComposeRegistrations([provider]);
        var state = new RuntimeCapabilityState();
        var accessPlan = CreateAllowAllAccessPlan();

        var result = await composer.AttachAsync(
            new RuntimeToolProviderAttachmentRequest(
                state,
                accessPlan,
                registrations,
                CreateContext(),
                SuppressApprovalRequirements: false),
            CancellationToken.None);

        Assert.Equal(2, result.AttachedToolCount);
        Assert.Equal("tests.process", Assert.Single(state.RuntimeToolProviderDescriptors).ProviderKey);
        Assert.Equal(["processes_runs_list", "processes_run_start"], state.Tools.Select(tool => tool.Name));
        Assert.IsNotType<ApprovalRequiredAIFunction>(Assert.Single(state.Tools, tool => tool.Name == "processes_runs_list"));
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(state.Tools, tool => tool.Name == "processes_run_start"));
        Assert.Contains(state.RuntimeToolMetadata, metadata =>
            metadata.ToolName == "processes_run_start" &&
            metadata.OperationKind == AgentRuntimeToolOperationKind.Mutation);
        Assert.Contains("Attached 2 tool(s) from 1 registered runtime tool provider(s).", result.ProgressMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeToolProviderComposer_allow_only_provider_key_policy_prunes_other_provider_tools()
    {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());
        var allowedProvider = new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.allowed-provider"),
            "allowed_runtime_tool");
        var deniedProvider = new TestRuntimeToolProvider(
            20,
            CreateDescriptor("tests.denied-provider"),
            "denied_runtime_tool");
        var registrations = composer.ComposeRegistrations([allowedProvider, deniedProvider]);
        var allowedProviderTag = RuntimeToolProviderCapabilityTags.CreateProviderKeyTag("tests.allowed-provider");
        var allowOnlyPolicy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("allow-selected-runtime-provider"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByTag(allowedProviderTag),
                "Only the selected runtime provider is allowed.")
        ],
        CapabilityAccessDefaultEffect.DenyAll,
        CapabilityAccessScope.ProcessStep,
        "Runtime provider is outside the selected process-step scope.");
        var state = new RuntimeCapabilityState();

        var result = await composer.AttachAsync(
            new RuntimeToolProviderAttachmentRequest(
                state,
                CreateAccessPlan([allowOnlyPolicy]),
                registrations,
                CreateContext(),
                SuppressApprovalRequirements: false),
            CancellationToken.None);

        Assert.Equal(1, result.AttachedToolCount);
        Assert.Equal(["allowed_runtime_tool"], state.Tools.Select(tool => tool.Name));
        Assert.Equal("tests.allowed-provider", Assert.Single(state.RuntimeToolProviderDescriptors).ProviderKey);
        Assert.Contains(state.EffectiveCapabilityDescriptors, descriptor =>
            descriptor.RuntimeToolName?.Value == "allowed_runtime_tool" &&
            descriptor.Tags.Contains(allowedProviderTag));
        Assert.Contains(state.CapabilityAccessDiagnostics, diagnostic =>
            diagnostic.Identity.Key.Value == "denied-runtime-tool" &&
            diagnostic.Category == CapabilityDiagnosticCategory.AccessPolicy);
    }

    [Fact]
    public void MafProviderCredentialService_resolves_configuration_credentials()
    {
        var key = $"UNIT_TEST_OPENAI_KEY_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [key] = "unit-test-secret"
            })
            .Build());
        services.AddMafRuntimeArchitectureServices();
        using var provider = services.BuildServiceProvider();
        var credentialService = provider.GetRequiredService<IMafProviderCredentialService>();

        var resolution = credentialService.Resolve(CreateProviderProfile(apiKeyEnvironmentVariable: key));

        Assert.True(resolution.IsResolved);
        Assert.Equal("unit-test-secret", resolution.ApiKey);
        Assert.Contains(key, resolution.ResolutionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolInvocationTraceRecorder_records_completion_state()
    {
        var recorder = new ToolInvocationTraceRecorder();

        var sequence = recorder.Start(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            "signature",
            new AgentRuntimeToolOwnership("provider.key", "Provider", "workspace_write_file"));
        recorder.Complete(sequence, succeeded: false, "denied");

        var trace = Assert.Single(recorder.Snapshot());
        Assert.Equal("workspace_write_file", trace.ToolName);
        Assert.Equal(ToolInvocationClassification.Mutation, trace.Classification);
        Assert.False(trace.Succeeded);
        Assert.Equal("denied", trace.FailureMessage);
        Assert.Equal("provider.key", trace.RuntimeToolProviderKey);
    }

    [Fact]
    public void InMemoryMafRuntimeCompositionMetrics_records_measurements()
    {
        var metrics = new InMemoryMafRuntimeCompositionMetrics();
        var measurement = new MafRuntimeCompositionMeasurement(
            "capability.test",
            TimeSpan.FromMilliseconds(5),
            "unit-test");

        metrics.Record(measurement);

        Assert.Equal([measurement], metrics.Snapshot());
    }

    private static RuntimeCapabilityAccessPlan CreateAllowAllAccessPlan()
        => CreateAccessPlan([]);

    private static RuntimeCapabilityAccessPlan CreateAccessPlan(IReadOnlyList<CapabilityAccessPolicy> policies)
        => new(
            new EffectiveCapabilitySet([], []),
            AllowedCatalogCapabilities: [],
            policies,
            new CapabilityAccessPolicyEvaluator(),
            InitialAllowedCapabilities: [],
            InitialDiagnostics: [],
            CatalogCapabilitiesByIdentity: new Dictionary<CapabilityIdentity, CapabilityCatalogItem>(),
            DescriptorsByKey: new Dictionary<string, CapabilityExposureDescriptor>(StringComparer.OrdinalIgnoreCase),
            CorrelationId: "unit-test");

    private static AgentRuntimeToolProviderContext CreateContext()
        => new(
            CreateAgent(),
            CreateProviderProfile(),
            Capabilities: [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: string.Empty,
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static ChatSessionRecord CreateSession(
        ChatSessionRuntimeCompatibilityRecord? compatibility = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Architecture test session",
            now,
            now,
            [],
            Compatibility: compatibility);
    }

    private static AgentRuntimeExecutionOptions CreateExecutionOptions(
        AgentRuntimeContextIntent? contextIntent = null,
        IReadOnlyList<AgentRuntimeInputAttachment>? inputAttachments = null)
        => new(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0,
            ContextIntent: contextIntent,
            InputAttachments: inputAttachments);

    private static AgentRuntimeToolProviderDescriptor CreateDescriptor(string providerKey)
        => new(
            providerKey,
            $"Test provider {providerKey}",
            "Test runtime provider.",
            ["tests"],
            [AgentRuntimeToolProviderPurpose.InteractiveChat]);

    private static AgentDefinition CreateAgent()
        => new(
            Id: Guid.NewGuid(),
            Name: "Runtime Architecture Agent",
            RoleTitle: "Tester",
            Summary: "Tests runtime architecture services.",
            Instructions: "Use supplied tools.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: string.Empty,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default with
            {
                CanUseTools = true,
                CanAskOtherAgents = false,
                RequiresApprovalForExternalCalls = false
            },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static ProviderProfile CreateProviderProfile(
        string apiKeyEnvironmentVariable = "OPENAI_API_KEY",
        string configurationJson = "{}")
        => new(
            Guid.NewGuid(),
            "Unit Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            apiKeyEnvironmentVariable,
            "gpt-4.1",
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            false,
            true,
            configurationJson,
            string.Empty,
            "Not checked",
            null,
            []);

    private static IEnumerable<string> FindPrivateNestedTypeDeclarations(
        string path,
        string runtimeRoot)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("private sealed class ", StringComparison.Ordinal) ||
                trimmed.StartsWith("private sealed partial class ", StringComparison.Ordinal) ||
                trimmed.StartsWith("private sealed record ", StringComparison.Ordinal) ||
                trimmed.StartsWith("private enum ", StringComparison.Ordinal))
            {
                yield return $"{Path.GetRelativePath(runtimeRoot, path)}:{lineNumber}:{trimmed}";
            }
        }
    }

    private static IEnumerable<string> FindForbiddenPrivateRuntimeReflection(
        string path,
        string unitTestRoot,
        IReadOnlyList<string> forbiddenPatterns)
    {
        var source = File.ReadAllText(path);
        if (!source.Contains("typeof(MafAgentRuntime).GetMethod(", StringComparison.Ordinal))
        {
            yield break;
        }

        foreach (var pattern in forbiddenPatterns)
        {
            if (source.Contains($"\"{pattern}\"", StringComparison.Ordinal))
            {
                yield return $"{Path.GetRelativePath(unitTestRoot, path)} reflects private runtime method '{pattern}'.";
            }
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed class TestRuntimeToolProvider : IAgentRuntimeToolProvider
    {
        private readonly IReadOnlyList<string> toolNames;

        public TestRuntimeToolProvider(
            int order,
            AgentRuntimeToolProviderDescriptor descriptor,
            params string[] toolNames)
        {
            Order = order;
            Descriptor = descriptor;
            this.toolNames = toolNames;
        }

        public int Order { get; }

        public AgentRuntimeToolProviderDescriptor Descriptor { get; }

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<AITool>>(toolNames
                .Select(toolName => AIFunctionFactory.Create(() => "ok", toolName, "Test runtime provider tool."))
                .ToList());
        }
    }

    private sealed class TestMafProviderRuntimeGateway : IMafProviderRuntimeGateway
    {
        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderImageChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            IReadOnlyList<ProviderChatAttachment> attachments,
            string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestMafProviderStreamingDispatchGate : IMafProviderStreamingDispatchGate
    {
        public ValueTask<IAsyncDisposable> EnterAsync(
            ProviderProfile provider,
            string model,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());
    }

    private sealed class DelayedStreamingAgent(
        TimeSpan delay,
        int updateCount = 1,
        bool emitSemanticUpdates = true,
        bool runUntilCancelled = false) : AIAgent
    {
        public bool DelayCompleted { get; private set; }

        public bool DelayTokenWasCanceled { get; private set; }

        public bool StreamCancellationWasRequested { get; private set; }

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AgentSession>(new DelayedStreamingAgentSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(JsonSerializer.SerializeToElement(new { ok = true }, jsonSerializerOptions));

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AgentSession>(new DelayedStreamingAgentSession());

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => RunCoreStreamingAsync(messages, session, options, cancellationToken)
                .ToAgentResponseAsync(cancellationToken);

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var cancellationRegistration = cancellationToken.Register(
                () => StreamCancellationWasRequested = true);

            for (var updateIndex = 0; runUntilCancelled || updateIndex < updateCount; updateIndex++)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    DelayTokenWasCanceled = true;
                    throw;
                }

                yield return emitSemanticUpdates
                    ? new AgentResponseUpdate(
                        ChatRole.Assistant,
                        [new TextContent($"completed {updateIndex + 1}")])
                    : new AgentResponseUpdate(ChatRole.Assistant, []);
            }

            DelayCompleted = !runUntilCancelled;
        }
    }

    private sealed class DelayedStreamingAgentSession : AgentSession;

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
