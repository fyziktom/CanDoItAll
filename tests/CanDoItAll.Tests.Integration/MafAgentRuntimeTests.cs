using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class MafAgentRuntimeTests
{
    [Fact]
    public void SnapshotUpdate_copies_mutable_content_collections()
    {
        var snapshotMethod = typeof(MafAgentRuntime).GetMethod(
                                 "SnapshotUpdate",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("SnapshotUpdate method was not found.");

        var update = new AgentResponseUpdate(
            ChatRole.Assistant,
            [
                new TextContent("Initial content")
            ])
        {
            AuthorName = "runtime",
            ResponseId = "response-1"
        };

        var snapshot = Assert.IsType<AgentResponseUpdate>(snapshotMethod.Invoke(null, [update]));
        update.Contents.Add(new TextContent("Late mutation"));

        Assert.NotSame(update.Contents, snapshot.Contents);
        Assert.Single(snapshot.Contents);
        Assert.Equal("response-1", snapshot.ResponseId);
        Assert.Equal("runtime", snapshot.AuthorName);
    }

    [Fact]
    public void SnapshotUpdate_copies_tool_call_argument_graph()
    {
        var snapshotMethod = typeof(MafAgentRuntime).GetMethod(
                                 "SnapshotUpdate",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("SnapshotUpdate method was not found.");

        var nestedArguments = new Dictionary<string, object?>
        {
            ["path"] = "artifacts/demo.md",
            ["options"] = new Dictionary<string, object?>
            {
                ["recursive"] = true
            },
            ["tags"] = new List<object?>
            {
                "architecture"
            }
        };
        var toolCall = new FunctionCallContent("call-1", "workspace_write_file", nestedArguments);
        var update = new AgentResponseUpdate(ChatRole.Assistant, [toolCall]);

        var snapshot = Assert.IsType<AgentResponseUpdate>(snapshotMethod.Invoke(null, [update]));

        nestedArguments["path"] = "artifacts/changed.md";
        ((Dictionary<string, object?>)nestedArguments["options"]!)["recursive"] = false;
        ((List<object?>)nestedArguments["tags"]!).Add("late-mutation");

        var snapshottedToolCall = Assert.IsType<FunctionCallContent>(Assert.Single(snapshot.Contents));
        Assert.NotSame(toolCall.Arguments, snapshottedToolCall.Arguments);
        Assert.Equal("artifacts/demo.md", snapshottedToolCall.Arguments!["path"]);

        var snapshottedOptions = Assert.IsType<Dictionary<string, object?>>(snapshottedToolCall.Arguments["options"]);
        Assert.True((bool)snapshottedOptions["recursive"]!);

        var snapshottedTags = Assert.IsType<List<object?>>(snapshottedToolCall.Arguments["tags"]);
        Assert.Single(snapshottedTags);
        Assert.Equal("architecture", snapshottedTags[0]);
    }

    [Fact]
    public void SnapshotUpdate_converts_opaque_tool_calls_into_detached_function_calls()
    {
        var snapshotMethod = typeof(MafAgentRuntime).GetMethod(
                                 "SnapshotUpdate",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("SnapshotUpdate method was not found.");

        var opaqueToolCall = new OpaqueToolCallContent(
            "call-opaque",
            "provider-native-web-search",
            new Dictionary<string, object?>
            {
                ["query"] = "basic unit conversion best practices"
            });
        var update = new AgentResponseUpdate(ChatRole.Assistant, [opaqueToolCall]);

        var snapshot = Assert.IsType<AgentResponseUpdate>(snapshotMethod.Invoke(null, [update]));
        var snapshottedToolCall = Assert.IsType<FunctionCallContent>(Assert.Single(snapshot.Contents));

        Assert.NotSame(opaqueToolCall, snapshottedToolCall);
        Assert.Equal("call-opaque", snapshottedToolCall.CallId);
        Assert.Equal("provider-native-web-search", snapshottedToolCall.Name);
        Assert.Equal("basic unit conversion best practices", snapshottedToolCall.Arguments!["query"]);
    }

    [Fact]
    public void Stored_output_disabled_responses_do_not_request_reasoning_encrypted_content()
    {
        var decisionMethod = typeof(MafAgentRuntime).GetMethod(
                                 "ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses",
                                 BindingFlags.NonPublic | BindingFlags.Static)
                             ?? throw new InvalidOperationException("Stored-output reasoning-content decision method was not found.");

        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);

        var includeReasoningEncryptedContent = Assert.IsType<bool>(decisionMethod.Invoke(null, [provider, true]));

        Assert.False(includeReasoningEncryptedContent);
    }

    [Fact]
    public void ApplyStructuredResponseFormat_sets_json_schema_response_format()
    {
        var applyMethod = typeof(MafAgentRuntime).GetMethod(
                              "ApplyStructuredResponseFormat",
                              BindingFlags.NonPublic | BindingFlags.Static)
                          ?? throw new InvalidOperationException("ApplyStructuredResponseFormat method was not found.");
        var chatOptions = new ChatOptions();
        var contract = AgentStructuredOutputContract.For<ProcessStepOutcomeResult>(
            "process_step_outcome_result",
            "Process step outcome contract.");

        applyMethod.Invoke(null, [chatOptions, contract]);

        var responseFormat = Assert.IsType<ChatResponseFormatJson>(chatOptions.ResponseFormat);
        Assert.Equal("process_step_outcome_result", responseFormat.SchemaName);
        Assert.NotNull(responseFormat.Schema);
    }

    [Fact]
    public void CreateFinalizerCapture_attaches_process_step_outcome_tool()
    {
        var createMethod = typeof(MafAgentRuntime).GetMethod(
                               "CreateFinalizerCapture",
                               BindingFlags.NonPublic | BindingFlags.Static)
                           ?? throw new InvalidOperationException("CreateFinalizerCapture method was not found.");

        var capture = createMethod.Invoke(null, [AgentStructuredOutputContracts.ProcessStepOutcomeResult, AgentFinalizerMode.Required])
                      ?? throw new InvalidOperationException("Finalizer capture was not created.");
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            capture.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(capture));

        Assert.Contains(
            tools,
            tool => string.Equals(tool.Name, AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName, StringComparison.Ordinal));
    }

    [Fact]
    public void CreateFinalizerCapture_omits_tool_when_finalizer_mode_is_disabled()
    {
        var createMethod = typeof(MafAgentRuntime).GetMethod(
                               "CreateFinalizerCapture",
                               BindingFlags.NonPublic | BindingFlags.Static)
                           ?? throw new InvalidOperationException("CreateFinalizerCapture method was not found.");

        var capture = createMethod.Invoke(null, [AgentStructuredOutputContracts.ProcessStepOutcomeResult, AgentFinalizerMode.Disabled]);

        Assert.Null(capture);
    }

    [Fact]
    public void TryBuildRequiredFinalizerRuntimeResponse_returns_authoritative_finalizer_json()
    {
        var method = typeof(MafAgentRuntime).GetMethod(
                         "TryBuildRequiredFinalizerRuntimeResponse",
                         BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException("Required finalizer response builder was not found.");
        var outcome = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Scope packet completed.",
            EvidenceRefs = ["workspace://artifacts/scope-packets/example.md"],
            NextActions = [],
            HumanReadableSummaryMarkdown = "Completed."
        };
        var timestamp = DateTimeOffset.UtcNow;
        var response = Assert.IsType<AgentRuntimeResponse>(method.Invoke(
            null,
            [
                AgentStructuredOutputContracts.ProcessStepOutcomeResult,
                AgentFinalizerMode.Required,
                "runtime-session",
                "{\"session\":true}",
                new[]
                {
                    new AgentFinalizerInvocation(
                        AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
                        JsonSerializer.Serialize(outcome, AgentOutputJson.SerializerOptions),
                        Sequence: 1)
                },
                new[]
                {
                    new AgentToolInvocationTrace(
                        AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
                        ToolInvocationClassification.Read,
                        Sequence: 1,
                        StartedAtUtc: timestamp,
                        CompletedAtUtc: timestamp,
                        Succeeded: true,
                        FailureMessage: string.Empty)
                }
            ]));

        var parsed = JsonSerializer.Deserialize<ProcessStepOutcomeResult>(
            response.ResponseText,
            AgentOutputJson.SerializerOptions);
        Assert.NotNull(parsed);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, parsed.Status);
        Assert.Equal("runtime-session", response.RuntimeSessionKey);
        Assert.Equal("{\"session\":true}", response.SerializedSessionStateJson);
        Assert.Single(response.FinalizerInvocations);
        Assert.Single(response.ToolInvocationTraces);
    }

    [Fact]
    public void ExecuteRunAsync_checks_required_finalizer_after_streaming_finishes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.cs"));
        var streamingLoopEnd = source.IndexOf(
            "var postStreamingFinalizerResponse = await TryCreateFinalizerResponseAfterRequiredFinalizerAsync",
            StringComparison.Ordinal);

        Assert.True(streamingLoopEnd >= 0, "The runtime must check captured required finalizers after the streaming loop completes.");
        var providerCatch = source.IndexOf("catch (Exception exception)", streamingLoopEnd, StringComparison.Ordinal);
        Assert.True(providerCatch > streamingLoopEnd, "The post-streaming finalizer check must run before provider failure handling.");
    }

    [Fact]
    public void Required_finalizer_tool_call_short_circuits_before_post_finalizer_model_turn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.cs"));
        var factorySource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.AgentFactory.cs"));

        Assert.Contains("catch (RequiredFinalizerCapturedException exception)", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("TryCreateFinalizerResponseAfterEarlyFinalizerAsync", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("throw new RequiredFinalizerCapturedException(functionName)", factorySource, StringComparison.Ordinal);
        Assert.Contains("IsRequiredFinalizerTool(functionName, finalizerPolicy, finalizerMode)", factorySource, StringComparison.Ordinal);
    }

    [Fact]
    public void Required_finalizer_session_serialization_is_bounded()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.cs"));

        Assert.Contains("FinalizerSessionSerializationTimeout", runtimeSource, StringComparison.Ordinal);
        Assert.Contains(".WaitAsync(", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("catch (TimeoutException)", runtimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Approval_continuation_rejects_missing_or_incompatible_serialized_session_state()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sessionSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.Session.cs"));

        Assert.Contains(
            "Cannot continue pending tool approvals because serialized Microsoft Agent Framework session state is unavailable or incompatible",
            sessionSource,
            StringComparison.Ordinal);
        Assert.Contains("isApprovalContinuation && (session.Compatibility?.PendingApprovals.Count ?? 0) > 0", sessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendFinalizerInstructions_uses_json_response_wording_for_required_mode()
    {
        var appendMethod = typeof(MafAgentRuntime).GetMethod(
                               "AppendFinalizerInstructions",
                               BindingFlags.NonPublic | BindingFlags.Static)
                           ?? throw new InvalidOperationException("AppendFinalizerInstructions method was not found.");
        Assert.True(AgentFinalizerPolicies.TryResolveForStructuredOutput(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            out var policy));

        var instructions = Assert.IsType<string>(appendMethod.Invoke(
            null,
            ["Base instructions.", policy, AgentFinalizerMode.Required, true]));

        Assert.Contains("Call `submit_process_step_outcome` exactly once", instructions, StringComparison.Ordinal);
        Assert.Contains("return exactly one JSON object", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not use Markdown, prose, code fences, or any extra text", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendFinalizerInstructions_uses_shadow_comparison_wording_for_shadow_mode()
    {
        var appendMethod = typeof(MafAgentRuntime).GetMethod(
                               "AppendFinalizerInstructions",
                               BindingFlags.NonPublic | BindingFlags.Static)
                           ?? throw new InvalidOperationException("AppendFinalizerInstructions method was not found.");
        Assert.True(AgentFinalizerPolicies.TryResolveForStructuredOutput(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            out var policy));

        var instructions = Assert.IsType<string>(appendMethod.Invoke(
            null,
            ["Base instructions.", policy, AgentFinalizerMode.Shadow, true]));

        Assert.Contains("Finalizer tool shadow policy", instructions, StringComparison.Ordinal);
        Assert.Contains("at most once", instructions, StringComparison.Ordinal);
        Assert.Contains("final assistant response JSON is the source of truth", instructions, StringComparison.Ordinal);
        Assert.Contains("Do not use Markdown, prose, code fences, or any extra text", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_tool_guard_does_not_embed_workflow_process_guidance()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.cs"));

        Assert.DoesNotContain("Workflow.Tests", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Workflow/Components/Pages/Home.razor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("If this is the workflow process", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveToolInvocationSignature_distinguishes_long_write_content_with_same_visible_prefix()
    {
        var signatureMethod = typeof(MafAgentRuntime).GetMethod(
                                  "ResolveToolInvocationSignature",
                                  BindingFlags.NonPublic | BindingFlags.Static)
                              ?? throw new InvalidOperationException("Tool invocation signature method was not found.");
        var prefix = new string('x', 180);
        var firstCall = new FunctionCallContent(
            "call-1",
            "workspace_write_file",
            new Dictionary<string, object?>
            {
                ["path"] = "external-target/C/programovani/dotnet/output/Components/Pages/Home.razor",
                ["content"] = prefix + "first build fix",
                ["overwrite"] = true
            });
        var secondCall = new FunctionCallContent(
            "call-2",
            "workspace_write_file",
            new Dictionary<string, object?>
            {
                ["path"] = "external-target/C/programovani/dotnet/output/Components/Pages/Home.razor",
                ["content"] = prefix + "second build fix",
                ["overwrite"] = true
            });

        var firstSignature = Assert.IsType<string>(signatureMethod.Invoke(null, [firstCall]));
        var secondSignature = Assert.IsType<string>(signatureMethod.Invoke(null, [secondCall]));

        Assert.NotEqual(firstSignature, secondSignature);
        Assert.Contains("#", firstSignature, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file|", firstSignature, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveProviderNetworkTimeout_honors_provider_timeout_metadata()
    {
        var timeoutMethod = typeof(MafAgentRuntime).GetMethod(
                                "ResolveProviderNetworkTimeout",
                                BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("Provider timeout resolver was not found.");
        var provider = CreateProvider("""{"timeoutSeconds":600}""");

        var timeout = Assert.IsType<TimeSpan>(timeoutMethod.Invoke(null, [provider]));

        Assert.Equal(TimeSpan.FromSeconds(600), timeout);
    }

    [Fact]
    public void ResolveProviderNetworkTimeout_clamps_invalid_provider_timeout_metadata()
    {
        var timeoutMethod = typeof(MafAgentRuntime).GetMethod(
                                "ResolveProviderNetworkTimeout",
                                BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("Provider timeout resolver was not found.");
        var provider = CreateProvider("""{"timeoutSeconds":1}""");

        var timeout = Assert.IsType<TimeSpan>(timeoutMethod.Invoke(null, [provider]));

        Assert.Equal(TimeSpan.FromSeconds(5), timeout);
    }

    [Fact]
    public async Task CreateCapabilityState_skips_compaction_for_governed_process_even_when_agent_requests_it()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = """{"enableCompaction":true}"""
        };
        var provider = CreateProvider("{}");
        var run = CreateExecutionRunForAuditScope(agent, provider, "{}") with
        {
            ProcessRunId = Guid.NewGuid().ToString("D"),
            ProcessStepId = Guid.NewGuid().ToString("D")
        };
        var progressMessages = new List<string>();

        object state;
        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            state = await InvokeCreateCapabilityStateAsync(
                runtime,
                agent,
                provider,
                Array.Empty<CapabilityCatalogItem>(),
                progressMessages);
        }

        Assert.DoesNotContain("CompactionProvider", ReadContextProviderTypeNames(state));
        Assert.Contains(
            progressMessages,
            item => item.Contains("governed process automation", StringComparison.Ordinal) &&
                    item.Contains("must not be summarized", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_skips_compaction_for_auto_approved_non_interactive_runs()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = """{"enableCompaction":true}"""
        };
        var provider = CreateProvider("{}");
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages,
            suppressApprovalRequirements: true);

        Assert.DoesNotContain("CompactionProvider", ReadContextProviderTypeNames(state));
        Assert.Contains(
            progressMessages,
            item => item.Contains("auto-approved non-interactive execution", StringComparison.Ordinal) &&
                    item.Contains("must not block unattended tool continuations", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_interactive_compaction_with_expanded_defaults()
    {
        var previousOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
            var agent = CreateToolAgent();
            var provider = CreateProvider("{}");
            var progressMessages = new List<string>();

            var state = await InvokeCreateCapabilityStateAsync(
                runtime,
                agent,
                provider,
                Array.Empty<CapabilityCatalogItem>(),
                progressMessages);

            Assert.Contains("CompactionProvider", ReadContextProviderTypeNames(state));
            Assert.Contains(
                progressMessages,
                item => item.Contains("32 turns", StringComparison.Ordinal) &&
                        item.Contains("64000 tokens", StringComparison.Ordinal) &&
                        item.Contains("40 tool messages", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousOpenAiApiKey);
        }
    }

    [Fact]
    public async Task CreateCapabilityState_skips_unsupported_provider_native_web_search_for_ollama()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var architectAgent = Assert.Single(
            seed.Agents,
            item => string.Equals(item.Name, "Portfolio Architect", StringComparison.Ordinal));
        var ollamaProvider = Assert.Single(
            seed.Providers,
            item => item.Kind == ProviderKind.Ollama &&
                    string.Equals(item.Name, "Remote Ollama", StringComparison.Ordinal));
        var selectedCapabilityIds = architectAgent.Capabilities
            .Where(item =>
                string.Equals(item.CapabilityKey, "provider-native-web-search", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.CapabilityKey, "provider-health", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.CapabilityKey, "workspace-search", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.CapabilityId)
            .ToHashSet();
        var capabilities = seed.Capabilities
            .Where(item => selectedCapabilityIds.Contains(item.Id))
            .ToList();
        var agent = architectAgent with
        {
            ProviderProfileId = ollamaProvider.Id,
            Model = ollamaProvider.DefaultModel
        };
        var progressMessages = new List<string>();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateAsync(runtime, agent, ollamaProvider, capabilities, progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var toolNames = tools
            .Select(item => item.Name)
            .ToList();

        Assert.Contains(toolNames, item => string.Equals(item, "workspace_search", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(toolNames, item => string.Equals(item, "provider_health", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(toolNames, item => string.Equals(item, "provider-native-web-search", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            progressMessages,
            item => item.Contains("Skipping capability 'Provider-Native Web Search'", StringComparison.Ordinal) &&
                    item.Contains("Remote Ollama", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_skips_retired_workspace_delivery_skill_even_when_its_service_type_no_longer_matches_the_legacy_assembly_name()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0] with
        {
            ConfigurationJson = "{}"
        };
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var retiredCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Skill,
            "workspace-delivery-skill",
            "Workspace Delivery Skill",
            "Legacy workspace delivery capability retained by stale metadata.",
            string.Empty,
            """{"registeredSkillServiceType":"Legacy.WorkspaceDeliverySkill, Legacy.Sandbox"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            false);
        var progressMessages = new List<string>();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());

        var exception = await Record.ExceptionAsync(() => InvokeCreateCapabilityStateAsync(
            runtime,
            seededAgent,
            provider,
            [retiredCapability],
            progressMessages));

        Assert.Null(exception);
        Assert.DoesNotContain(
            progressMessages,
            item => item.Contains("Workspace Delivery Skill", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_skips_capability_when_raw_configuration_keeps_legacy_workspace_delivery_marker()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var retiredCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Skill,
            "legacy-workspace-capability",
            "Legacy Workspace Capability",
            "Legacy workspace capability retained by stale metadata.",
            string.Empty,
            """{"registeredSkillServiceType":"Legacy.WorkspaceDeliverySkill, Legacy.Sandbox","legacyServiceType":"CanDoItAll.AgentFramework.Sandbox.Hosting.WorkspaceDeliverySkill, CanDoItAll.AgentFramework.Sandbox"}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            false);
        var progressMessages = new List<string>();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());

        var exception = await Record.ExceptionAsync(() => InvokeCreateCapabilityStateAsync(
            runtime,
            seededAgent,
            provider,
            [retiredCapability],
            progressMessages));

        Assert.Null(exception);
        Assert.DoesNotContain(
            progressMessages,
            item => item.Contains("Legacy Workspace Capability", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_skips_disabled_builtin_tool_configuration()
    {
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0] with
        {
            ConfigurationJson = "{}"
        };
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var disabledReadCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            "workspace-read-file",
            "Workspace Read File",
            "Reads files from the workspace when explicitly enabled.",
            string.Empty,
            """{"tool":"workspace_read_file","enabled":false}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
        var progressMessages = new List<string>();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            seededAgent,
            provider,
            [disabledReadCapability],
            progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var toolNames = tools
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("workspace_read_file", toolNames);
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_configured_external_workspace_read_tools_without_capability_assignment()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                new AgentWorkspaceToolAccessSettings
                {
                    CanReadFiles = true,
                    AllowedExternalTargetAliases =
                    [
                        "external-target/C/repositories/demo"
                    ]
                })
        };
        var provider = CreateProvider("{}");
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var toolNames = tools
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("workspace_list_files", toolNames);
        Assert.Contains("workspace_search", toolNames);
        Assert.Contains("workspace_read_file", toolNames);
        Assert.Contains("workspace_stat_path", toolNames);
        Assert.DoesNotContain("workspace_write_file", toolNames);
        Assert.Contains(
            progressMessages,
            item => item.Contains("Attached configured workspace file and storage tools", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_prompt_grounded_workspace_read_tools_without_preconfigured_external_roots()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                new AgentWorkspaceToolAccessSettings
                {
                    CanReadFiles = true,
                    CanWriteFiles = false
                })
        };
        var provider = CreateProvider("{}");
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            "{}",
            """analyze "C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler" and add architecture""",
            AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson));
        var run = CreateExecutionRunForAuditScope(agent, provider, metadataJson);
        var progressMessages = new List<string>();

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            var state = await InvokeCreateCapabilityStateAsync(
                runtime,
                agent,
                provider,
                Array.Empty<CapabilityCatalogItem>(),
                progressMessages);
            var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
                state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
            var toolNames = tools
                .Select(item => item.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Contains("workspace_list_files", toolNames);
            Assert.Contains("workspace_search", toolNames);
            Assert.Contains("workspace_read_file", toolNames);
            Assert.Contains("workspace_stat_path", toolNames);
            Assert.DoesNotContain("workspace_write_file", toolNames);
        }
    }

    [Fact]
    public void WorkspaceRuntimePlugin_maps_prompt_grounded_absolute_external_path_to_alias_before_listing()
    {
        var workspaceRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cda-workspace-{Guid.NewGuid():N}")).FullName;
        var externalRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"BikeRepairSlotScheduler-{Guid.NewGuid():N}")).FullName;
        File.WriteAllText(Path.Combine(externalRoot, "BikeRepairSlotScheduler.csproj"), "<Project />");

        try
        {
            var agent = CreateToolAgent() with
            {
                ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                    "{}",
                    new AgentWorkspaceToolAccessSettings
                    {
                        CanReadFiles = true,
                        CanWriteFiles = false
                    })
            };
            var provider = CreateProvider("{}");
            var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
                "{}",
                $"""analyze "{externalRoot}" and add architecture""",
                AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson));
            var run = CreateExecutionRunForAuditScope(agent, provider, metadataJson);
            var pluginType = typeof(MafAgentRuntime).GetNestedType("WorkspaceRuntimePlugin", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("WorkspaceRuntimePlugin type was not found.");
            var commandService = new WorkspaceCommandExecutionService(workspaceRoot, new LocalWorkspaceProcessHost());
            var artifactService = new WorkspaceArtifactToolService(workspaceRoot, commandService);
            var plugin = Activator.CreateInstance(
                             pluginType,
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                             binder: null,
                             args:
                             [
                                 new WorkspaceFileService(workspaceRoot),
                                 commandService,
                                 artifactService,
                                 workspaceRoot,
                                 AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson)
                             ],
                             culture: null)
                         ?? throw new InvalidOperationException("WorkspaceRuntimePlugin could not be created.");
            var listMethod = pluginType.GetMethod("ListWorkspaceFiles", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ListWorkspaceFiles method was not found.");

            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var result = Assert.IsType<WorkspaceFileListResult>(listMethod.Invoke(plugin, [externalRoot, "*", 20]));

                Assert.True(result.Succeeded, result.Message);
                Assert.Equal(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(externalRoot), result.RootPath);
                Assert.Contains(result.Entries, entry => entry.RelativePath.EndsWith("BikeRepairSlotScheduler.csproj", StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WorkspaceRuntimePlugin_allows_dotnet_new_parent_when_parent_and_name_resolve_to_grounded_external_target()
    {
        var workspaceRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cda-workspace-{Guid.NewGuid():N}")).FullName;
        var externalParent = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cda-external-{Guid.NewGuid():N}")).FullName;
        var projectName = "UnitConverterApp";
        var externalRoot = Path.Combine(externalParent, projectName);
        var externalParentAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(externalParent)
                                  ?? throw new InvalidOperationException("External parent alias could not be normalized.");
        var externalRootAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(externalRoot)
                               ?? throw new InvalidOperationException("External root alias could not be normalized.");

        try
        {
            var agent = CreateToolAgent() with
            {
                ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                    "{}",
                    AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment))
            };
            var provider = CreateProvider("{}");
            var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
                "{}",
                $"""implement a Blazor app in "{externalRoot}" """,
                AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson));
            var run = CreateExecutionRunForAuditScope(agent, provider, metadataJson);
            var pluginType = typeof(MafAgentRuntime).GetNestedType("WorkspaceRuntimePlugin", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("WorkspaceRuntimePlugin type was not found.");
            var processHost = new CapturingWorkspaceProcessHost();
            var commandService = new WorkspaceCommandExecutionService(workspaceRoot, processHost);
            var artifactService = new WorkspaceArtifactToolService(workspaceRoot, commandService);
            var plugin = Activator.CreateInstance(
                             pluginType,
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                             binder: null,
                             args:
                             [
                                 new WorkspaceFileService(workspaceRoot),
                                 commandService,
                                 artifactService,
                                 workspaceRoot,
                                 AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson)
                             ],
                             culture: null)
                         ?? throw new InvalidOperationException("WorkspaceRuntimePlugin could not be created.");
            var dotnetNewMethod = pluginType.GetMethod("DotnetWorkspaceNew", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("DotnetWorkspaceNew method was not found.");

            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var invocation = dotnetNewMethod.Invoke(plugin, ["blazor", projectName, externalParentAlias, false, 300]);
                var task = Assert.IsAssignableFrom<Task<WorkspaceCommandExecutionResult>>(invocation);
                var result = await task;

                Assert.True(result.Succeeded, result.Message);
                Assert.Equal(externalParentAlias, result.WorkingDirectory);
                Assert.Contains(externalRootAlias, result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
            }

            var request = processHost.LastRequest ?? throw new InvalidOperationException("workspace_dotnet_new did not invoke the process host.");
            Assert.Equal("workspace_dotnet_new", request.ToolName);
            Assert.Equal("dotnet_new", request.RecipeId);
            Assert.Equal(Path.GetFullPath(externalParent), Path.GetFullPath(request.WorkingDirectory));
            Assert.Contains("-n", request.Arguments);
            Assert.Contains(projectName, request.Arguments);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
            Directory.Delete(externalParent, recursive: true);
        }
    }

    [Fact]
    public void WorkspaceRuntimePlugin_denies_dotnet_new_parent_when_requested_scaffold_root_is_not_grounded()
    {
        var workspaceRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cda-workspace-{Guid.NewGuid():N}")).FullName;
        var externalParent = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cda-external-{Guid.NewGuid():N}")).FullName;
        var externalRoot = Path.Combine(externalParent, "UnitConverterApp");
        var externalParentAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(externalParent)
                                  ?? throw new InvalidOperationException("External parent alias could not be normalized.");

        try
        {
            var agent = CreateToolAgent() with
            {
                ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                    "{}",
                    AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment))
            };
            var provider = CreateProvider("{}");
            var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
                "{}",
                $"""implement a Blazor app in "{externalRoot}" """,
                AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson));
            var run = CreateExecutionRunForAuditScope(agent, provider, metadataJson);
            var pluginType = typeof(MafAgentRuntime).GetNestedType("WorkspaceRuntimePlugin", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("WorkspaceRuntimePlugin type was not found.");
            var commandService = new WorkspaceCommandExecutionService(workspaceRoot, new CapturingWorkspaceProcessHost());
            var artifactService = new WorkspaceArtifactToolService(workspaceRoot, commandService);
            var plugin = Activator.CreateInstance(
                             pluginType,
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                             binder: null,
                             args:
                             [
                                 new WorkspaceFileService(workspaceRoot),
                                 commandService,
                                 artifactService,
                                 workspaceRoot,
                                 AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson)
                             ],
                             culture: null)
                         ?? throw new InvalidOperationException("WorkspaceRuntimePlugin could not be created.");
            var dotnetNewMethod = pluginType.GetMethod("DotnetWorkspaceNew", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("DotnetWorkspaceNew method was not found.");

            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var exception = Assert.Throws<TargetInvocationException>(() =>
                    dotnetNewMethod.Invoke(plugin, ["blazor", "OtherApp", externalParentAlias, false, 300]));
                var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);

                Assert.Contains("allowed external workspace roots", innerException.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
            Directory.Delete(externalParent, recursive: true);
        }
    }

    [Fact]
    public void WorkspaceRuntimePlugin_denies_recursive_delete_of_grounded_external_product_root_and_tests()
    {
        var workspaceRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cda-workspace-{Guid.NewGuid():N}")).FullName;
        var externalRoot = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cda-external-product-{Guid.NewGuid():N}")).FullName;
        var testsRoot = Directory.CreateDirectory(Path.Combine(externalRoot, "tests")).FullName;
        File.WriteAllText(Path.Combine(externalRoot, "Program.cs"), "var builder = WebApplication.CreateBuilder(args);");
        File.WriteAllText(Path.Combine(testsRoot, "UnitTests.csproj"), "<Project />");
        var externalRootAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(externalRoot)
                               ?? throw new InvalidOperationException("External root alias could not be normalized.");

        try
        {
            var agent = CreateToolAgent() with
            {
                ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                    "{}",
                    new AgentWorkspaceToolAccessSettings
                    {
                        Profile = AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                        CanReadFiles = true,
                        CanWriteFiles = true,
                        CanManageWorkspacePaths = true,
                        AllowedExternalTargetAliases = [externalRootAlias]
                    })
            };
            var provider = CreateProvider("{}");
            var run = CreateExecutionRunForAuditScope(agent, provider, "{}");
            var pluginType = typeof(MafAgentRuntime).GetNestedType("WorkspaceRuntimePlugin", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("WorkspaceRuntimePlugin type was not found.");
            var commandService = new WorkspaceCommandExecutionService(workspaceRoot, new CapturingWorkspaceProcessHost());
            var artifactService = new WorkspaceArtifactToolService(workspaceRoot, commandService);
            var plugin = Activator.CreateInstance(
                             pluginType,
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                             binder: null,
                             args:
                             [
                                 new WorkspaceFileService(workspaceRoot),
                                 commandService,
                                 artifactService,
                                 workspaceRoot,
                                 AgentWorkspaceToolAccessMetadata.Read(agent.ConfigurationJson)
                             ],
                             culture: null)
                         ?? throw new InvalidOperationException("WorkspaceRuntimePlugin could not be created.");
            var deleteMethod = pluginType.GetMethod("DeleteWorkspacePath", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("DeleteWorkspacePath method was not found.");

            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var rootException = Assert.Throws<TargetInvocationException>(() =>
                    deleteMethod.Invoke(plugin, [externalRootAlias, true]));
                var rootInnerException = Assert.IsType<InvalidOperationException>(rootException.InnerException);
                Assert.Contains("Refusing to delete grounded external target root", rootInnerException.Message, StringComparison.OrdinalIgnoreCase);

                var testsException = Assert.Throws<TargetInvocationException>(() =>
                    deleteMethod.Invoke(plugin, [$"{externalRootAlias}/tests", true]));
                var testsInnerException = Assert.IsType<InvalidOperationException>(testsException.InnerException);
                Assert.Contains("protected external product directory", testsInnerException.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_configured_storage_driver_tools_and_wraps_writes()
    {
        var services = new ServiceCollection()
            .AddSingleton<IStorageCatalogService>(new EmptyStorageCatalogService())
            .AddSingleton<IStorageDriverRegistry>(new EmptyStorageDriverRegistry())
            .BuildServiceProvider();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services);
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                new AgentWorkspaceToolAccessSettings
                {
                    CanReadStorage = true,
                    CanWriteStorage = true,
                    AllowAllStorageCatalogs = true
                })
        };
        var provider = CreateProvider("{}");
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

        Assert.Contains(tools, item => string.Equals(item.Name, "storage_catalog_list", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, item => string.Equals(item.Name, "storage_read_text_file", StringComparison.OrdinalIgnoreCase));
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(
            tools,
            item => string.Equals(item.Name, "storage_write_text_file", StringComparison.OrdinalIgnoreCase)));
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(
            tools,
            item => string.Equals(item.Name, "storage_delete_object", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_development_profile_workspace_tools_without_catalog_assignments()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment))
        };
        var provider = CreateProvider("{}");
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages);
        var toolNames = ReadToolNames(state);

        Assert.Contains("workspace_read_file", toolNames);
        Assert.Contains("workspace_write_file", toolNames);
        Assert.Contains("workspace_dotnet_build", toolNames);
        Assert.Contains("workspace_dotnet_test", toolNames);
        Assert.Contains("workspace_dotnet_run", toolNames);
        Assert.Contains("workspace_dotnet_new", toolNames);
        Assert.Contains("workspace_pwsh_run_script", toolNames);
        Assert.Contains("workspace_copy_path", toolNames);
        Assert.Contains("workspace_delete_path", toolNames);
    }

    [Fact]
    public async Task CreateCapabilityState_applies_governed_process_tool_profile_override_to_configured_tools()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly))
        };
        var provider = CreateProvider("{}");
        var metadataJson = ExecutionInvocationMetadata.ApplyProcessCooperation(
            "{}",
            new AgentProcessCooperationMetadata(
                AgentProcessCooperationMode.ProcessArtifactHandoff,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                "Governed implementation step uses software-development workspace tools."));
        var run = CreateExecutionRunForAuditScope(agent, provider, metadataJson) with
        {
            SourceKind = "process-step",
            SourceId = Guid.NewGuid().ToString("D"),
            RequestedByKind = "system",
            ProcessRunId = Guid.NewGuid().ToString("D"),
            ProcessStepId = Guid.NewGuid().ToString("D")
        };
        var progressMessages = new List<string>();

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            var state = await InvokeCreateCapabilityStateAsync(
                runtime,
                agent,
                provider,
                Array.Empty<CapabilityCatalogItem>(),
                progressMessages);
            var toolNames = ReadToolNames(state);

            Assert.Contains("workspace_read_file", toolNames);
            Assert.Contains("workspace_write_file", toolNames);
            Assert.Contains("workspace_dotnet_build", toolNames);
            Assert.Contains("workspace_dotnet_test", toolNames);
            Assert.Contains("workspace_dotnet_run", toolNames);
            Assert.Contains("workspace_dotnet_new", toolNames);
            Assert.Contains("workspace_pwsh_run_script", toolNames);
        }

        Assert.Contains(
            progressMessages,
            message => message.Contains("software-development", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_qa_profile_validation_tools_without_project_mutation_tools()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.QualityValidation))
        };
        var provider = CreateProvider("{}");
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages);
        var toolNames = ReadToolNames(state);

        Assert.Contains("workspace_read_file", toolNames);
        Assert.Contains("workspace_write_file", toolNames);
        Assert.Contains("workspace_dotnet_build", toolNames);
        Assert.Contains("workspace_dotnet_test", toolNames);
        Assert.Contains("workspace_dotnet_run", toolNames);
        Assert.Contains("workspace_pwsh_run_script", toolNames);
        Assert.DoesNotContain("workspace_dotnet_new", toolNames);
        Assert.DoesNotContain("workspace_copy_path", toolNames);
        Assert.DoesNotContain("workspace_delete_path", toolNames);
    }

    [Fact]
    public async Task CreateCapabilityState_filters_catalog_workspace_tools_denied_by_read_only_profile()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var readOnlySettings = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly);
        readOnlySettings.AllowedExternalTargetAliases =
        [
            "external-target/C/repositories/demo"
        ];
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}", readOnlySettings)
        };
        var provider = CreateProvider("{}");
        var progressMessages = new List<string>();
        var capabilities = new[]
        {
            CreateWorkspaceToolCapability("workspace-write-file", "workspace_write_file"),
            CreateWorkspaceToolCapability("workspace-dotnet-build", "workspace_dotnet_build"),
            CreateWorkspaceToolCapability("workspace-dotnet-run", "workspace_dotnet_run")
        };

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            capabilities,
            progressMessages);
        var toolNames = ReadToolNames(state);

        Assert.Contains("workspace_read_file", toolNames);
        Assert.DoesNotContain("workspace_write_file", toolNames);
        Assert.DoesNotContain("workspace_dotnet_build", toolNames);
        Assert.DoesNotContain("workspace_dotnet_run", toolNames);
    }

    [Fact]
    public async Task CreateCapabilityState_filters_workspace_plugin_tools_by_effective_workspace_access()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly))
        };
        var provider = CreateProvider("{}");
        var capability = CreateWorkspacePluginCapability();
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            [capability],
            progressMessages);
        var toolNames = ReadToolNames(state);

        Assert.Contains("workspace_read_file", toolNames);
        Assert.DoesNotContain("workspace_write_file", toolNames);
        Assert.DoesNotContain("workspace_dotnet_build", toolNames);
        Assert.DoesNotContain("workspace_dotnet_test", toolNames);
        Assert.DoesNotContain("workspace_dotnet_run", toolNames);
        Assert.DoesNotContain("workspace_dotnet_new", toolNames);
    }

    [Fact]
    public async Task CreateCapabilityState_applies_governed_process_tool_profile_override_to_workspace_plugin_tools()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly))
        };
        var provider = CreateProvider("{}");
        var metadataJson = ExecutionInvocationMetadata.ApplyProcessCooperation(
            "{}",
            new AgentProcessCooperationMetadata(
                AgentProcessCooperationMode.ProcessArtifactHandoff,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                "Governed implementation step uses software-development workspace tools."));
        var run = CreateExecutionRunForAuditScope(agent, provider, metadataJson) with
        {
            SourceKind = "process-step",
            SourceId = Guid.NewGuid().ToString("D"),
            RequestedByKind = "system",
            ProcessRunId = Guid.NewGuid().ToString("D"),
            ProcessStepId = Guid.NewGuid().ToString("D")
        };
        var capability = CreateWorkspacePluginCapability();
        var progressMessages = new List<string>();

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            var state = await InvokeCreateCapabilityStateAsync(
                runtime,
                agent,
                provider,
                [capability],
                progressMessages);
            var toolNames = ReadToolNames(state);

            Assert.Contains("workspace_read_file", toolNames);
            Assert.Contains("workspace_write_file", toolNames);
            Assert.Contains("workspace_dotnet_build", toolNames);
            Assert.Contains("workspace_dotnet_test", toolNames);
            Assert.Contains("workspace_dotnet_run", toolNames);
            Assert.Contains("workspace_dotnet_new", toolNames);
        }
    }

    [Fact]
    public async Task Approval_filter_omits_unusable_mutation_tools_for_manual_ollama_run()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment))
        };
        var provider = CreateProvider("{}");
        var capability = CreateWorkspacePluginCapability();
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            [capability],
            progressMessages);
        await InvokeFilterUnusableApprovalToolsAsync(runtime, state, provider, suppressApprovalRequirements: false, progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var toolNames = tools
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("workspace_read_file", toolNames);
        Assert.DoesNotContain("workspace_write_file", toolNames);
        Assert.Contains(
            progressMessages,
            message => message.Contains("Omitted mutation tool(s) that require MAF approval", StringComparison.Ordinal));
    }

    [Fact]
    public void Maf_approval_required_function_wraps_plain_function_tool()
    {
        var function = AIFunctionFactory.Create(
            (string value) => value.Trim(),
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionSave,
            "Saves a process definition.");
        var wrapped = new ApprovalRequiredAIFunction(function);
        var chatOptions = new ChatOptions
        {
            Tools = [wrapped]
        };

        Assert.Equal(function.Name, wrapped.Name);
        Assert.Contains(chatOptions.Tools, tool => tool is ApprovalRequiredAIFunction);
    }

    [Fact]
    public async Task Browser_mcp_wrapper_omits_screenshot_image_payload_from_model_result()
    {
        var wrapperType = typeof(MafAgentRuntime)
            .GetNestedType("McpCapabilityBuilder", BindingFlags.NonPublic)
            ?.GetNestedType("BrowserMcpModelContextBoundedAIFunction", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Browser MCP context-bounding wrapper was not found.");
        var imagePayload = new string('A', 18000);
        var innerFunction = AIFunctionFactory.Create(
            (string filename) => new BrowserMcpResult(
            [
                new BrowserMcpContent(Text: null, Data: imagePayload),
                new BrowserMcpContent(Text: "Screenshot captured.", Data: null)
            ]),
            "browser_take_screenshot",
            "Captures a screenshot.");
        var wrapper = Assert.IsAssignableFrom<AIFunction>(Activator.CreateInstance(
            wrapperType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [innerFunction],
            culture: null));

        var result = await wrapper.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["filename"] = "proof/browser-home.png"
            }));
        var compactResult = Assert.IsType<string>(result);

        Assert.Contains("Saved artifact: proof/browser-home.png", compactResult, StringComparison.Ordinal);
        Assert.Contains("Screenshot image content was omitted from model context.", compactResult, StringComparison.Ordinal);
        Assert.Contains("Screenshot captured.", compactResult, StringComparison.Ordinal);
        Assert.DoesNotContain(imagePayload[..200], compactResult, StringComparison.Ordinal);
        Assert.True(compactResult.Length < 2600);
    }

    [Fact]
    public async Task Browser_mcp_wrapper_preserves_bounded_snapshot_text()
    {
        var wrapperType = typeof(MafAgentRuntime)
            .GetNestedType("McpCapabilityBuilder", BindingFlags.NonPublic)
            ?.GetNestedType("BrowserMcpModelContextBoundedAIFunction", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Browser MCP context-bounding wrapper was not found.");
        var longSnapshotText = "heading Basic App Converter" + Environment.NewLine + new string('x', 20000);
        var innerFunction = AIFunctionFactory.Create(
            (string filename) => new BrowserMcpResult(
            [
                new BrowserMcpContent(Text: longSnapshotText, Data: null)
            ]),
            "browser_snapshot",
            "Captures an accessibility snapshot.");
        var wrapper = Assert.IsAssignableFrom<AIFunction>(Activator.CreateInstance(
            wrapperType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [innerFunction],
            culture: null));

        var result = await wrapper.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["filename"] = "proof/browser-snapshot.md"
            }));
        var compactResult = Assert.IsType<string>(result);

        Assert.Contains("Saved artifact: proof/browser-snapshot.md", compactResult, StringComparison.Ordinal);
        Assert.Contains("heading Basic App Converter", compactResult, StringComparison.Ordinal);
        Assert.Contains("[truncated]", compactResult, StringComparison.Ordinal);
        Assert.InRange(compactResult.Length, 11000, 12500);
    }

    [Fact]
    public async Task Workspace_plugin_mutation_tools_remain_available_when_approval_requirements_are_suppressed()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent() with
        {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment))
        };
        var provider = CreateProvider("{}");
        var capability = CreateWorkspacePluginCapability();
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            [capability],
            progressMessages,
            suppressApprovalRequirements: true);
        await InvokeFilterUnusableApprovalToolsAsync(runtime, state, provider, suppressApprovalRequirements: true, progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var writeTool = Assert.Single(tools, tool => string.Equals(tool.Name, "workspace_write_file", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotType<ApprovalRequiredAIFunction>(writeTool);
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_internal_project_structure_tools_by_default_when_workspace_services_are_available()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    AllowedProjectIds =
                    [
                        Guid.NewGuid()
                    ]
                })
        };
        var progressMessages = new List<string>();
        var runtime = new MafAgentRuntime(application.RootPath, scope.ServiceProvider);

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var toolNames = tools
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedToolNames = new[]
        {
            "project_structure_projects_list",
            "project_structure_project_create",
            "project_structure_project_update",
            "project_structure_hierarchy_get",
            "project_structure_subproject_link",
            "project_structure_read",
            "project_structure_checklist",
            "project_structure_dependencies_query",
            "project_structure_node_create",
            "project_structure_node_update",
            "project_structure_node_move",
            "project_structure_node_recompose",
            "project_structure_node_reparent",
            "project_structure_approval_request",
            "project_structure_asset_get",
            "project_structure_asset_create_revision",
            "project_structure_import",
            "project_structure_knowledge_query",
            "project_structure_analytics_query",
            "project_structure_project_lease_acquire",
            "project_structure_repo_branch_lease_acquire",
            "project_structure_lease_get",
            "project_structure_lease_release"
        };

        foreach (var toolName in expectedToolNames)
        {
            Assert.Contains(toolName, toolNames);
        }

        Assert.Contains(
            progressMessages,
            item => item.Contains("Attached internal project-structure tools", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateCapabilityState_attaches_internal_process_tools_by_default_when_workspace_services_are_available()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProcessAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProcessAccessSettings
                {
                    CanRead = true,
                    AllowedDefinitionIds =
                    [
                        Guid.NewGuid()
                    ]
                })
        };
        var progressMessages = new List<string>();
        var runtime = new MafAgentRuntime(application.RootPath, scope.ServiceProvider);

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var toolNames = tools
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedToolNames = new[]
        {
            "processes_definitions_list",
            "processes_definition_editor_get",
            "processes_definition_save",
            "processes_definition_publish",
            "processes_definition_delete",
            "processes_definition_export",
            "processes_definition_import",
            "processes_runs_list",
            "processes_run_detail_get",
            "processes_analytics_get",
            "processes_run_start",
            "processes_step_transition",
            "processes_assignment_resolve",
            "processes_artifact_record",
            "processes_party_options_list",
            "processes_executor_options_list",
            "processes_templates_list",
            "processes_template_get",
            "processes_template_mermaid_get",
            "processes_template_import",
            "processes_template_baseline_scenarios_list"
        };

        foreach (var toolName in expectedToolNames)
        {
            Assert.Contains(toolName, toolNames);
        }

        foreach (var toolName in ProcessMutationTools())
        {
            var tool = Assert.Single(tools, item => string.Equals(item.Name, toolName, StringComparison.OrdinalIgnoreCase));
            Assert.IsType<ApprovalRequiredAIFunction>(tool);
        }

        foreach (var toolName in ProcessReadTools())
        {
            var tool = Assert.Single(tools, item => string.Equals(item.Name, toolName, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotType<ApprovalRequiredAIFunction>(tool);
        }

        Assert.Contains(
            progressMessages,
            item => item.Contains("Attached internal process-module tools", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Internal_process_mutation_tools_remain_available_when_approval_requirements_are_suppressed()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProcessAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProcessAccessSettings
                {
                    CanRead = true,
                    CanWrite = true,
                    AllowedDefinitionIds =
                    [
                        Guid.NewGuid()
                    ]
                })
        };
        var progressMessages = new List<string>();
        var runtime = new MafAgentRuntime(application.RootPath, scope.ServiceProvider);

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            progressMessages,
            suppressApprovalRequirements: true);
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

        foreach (var toolName in ProcessMutationTools())
        {
            var tool = Assert.Single(tools, item => string.Equals(item.Name, toolName, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotType<ApprovalRequiredAIFunction>(tool);
        }
    }

    private sealed class OpaqueToolCallContent(
        string callId,
        string name,
        IDictionary<string, object?> arguments) : ToolCallContent(callId)
    {
        public string Name { get; } = name;

        public IDictionary<string, object?> Arguments { get; } = arguments;
    }

    private sealed class CapturingWorkspaceProcessHost : IWorkspaceProcessHost
    {
        public WorkspaceProcessExecutionRequest? LastRequest { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary()
        {
            return new ExecutionBoundaryDescriptor(
                Mode: "Test",
                FilesystemScope: "Test workspace.",
                NetworkScope: "None.",
                CredentialScope: "None.",
                HostLabel: "Capturing process host",
                IsEnforcedByHost: true,
                Notes: "Captures command requests without starting a process.");
        }

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: 0,
                Stdout: "ok",
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                TimedOut: false,
                Boundary: DescribeBoundary(),
                FailureMessage: string.Empty));
        }
    }

    private static ProviderProfile CreateProvider(string configurationJson)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Remote Ollama",
            ProviderKind.Ollama,
            "http://192.168.10.132:11434",
            string.Empty,
            "gptoss32k:latest",
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            true,
            false,
            configurationJson,
            string.Empty,
            "Not checked",
            null,
            ["gptoss32k:latest"]);
    }

    private static AgentDefinition CreateToolAgent()
    {
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "Tool Agent",
            RoleTitle: "Runtime engineer",
            Summary: "Tests capability composition.",
            Instructions: "Use tools only when required.",
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
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static CapabilityCatalogItem CreateWorkspacePluginCapability()
    {
        return new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            "workspace-plugin",
            "Workspace Plugin",
            "Workspace tools.",
            string.Empty,
            """{"tool":"workspace-plugin","enabled":true}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            false);
    }

    private static CapabilityCatalogItem CreateWorkspaceToolCapability(string key, string toolName)
    {
        return new CapabilityCatalogItem(
            Guid.NewGuid(),
            CapabilityKind.Tool,
            key,
            key,
            $"{toolName} test capability.",
            string.Empty,
            $$"""{"tool":"{{toolName}}","enabled":true}""",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            true);
    }

    private static HashSet<string> ReadToolNames(object state)
    {
        var tools = Assert.IsAssignableFrom<IEnumerable<AITool>>(
            state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

        return tools
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ReadContextProviderTypeNames(object state)
    {
        var contextProviders = Assert.IsAssignableFrom<IEnumerable<AIContextProvider>>(
            state.GetType().GetProperty("ContextProviders", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

        return contextProviders
            .Select(provider => provider.GetType().Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ProcessMutationTools()
    {
        return
        [
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionSave,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionPublish,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionDelete,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionImport,
            AgentToolInvocationPolicyMetadata.ProcessesRunStart,
            AgentToolInvocationPolicyMetadata.ProcessesStepTransition,
            AgentToolInvocationPolicyMetadata.ProcessesAssignmentResolve,
            AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateImport
        ];
    }

    private static string[] ProcessReadTools()
    {
        return
        [
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionsList,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionEditorGet,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionExport,
            AgentToolInvocationPolicyMetadata.ProcessesRunsList,
            AgentToolInvocationPolicyMetadata.ProcessesRunDetailGet,
            AgentToolInvocationPolicyMetadata.ProcessesAnalyticsGet,
            AgentToolInvocationPolicyMetadata.ProcessesPartyOptionsList,
            AgentToolInvocationPolicyMetadata.ProcessesExecutorOptionsList,
            AgentToolInvocationPolicyMetadata.ProcessesTemplatesList,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateGet,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateMermaidGet,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList
        ];
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root from the test output directory.");
    }

    private static ExecutionRunRecord CreateExecutionRunForAuditScope(
        AgentDefinition agent,
        ProviderProfile provider,
        string metadataJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            ChatSessionId: null,
            Title: "Prompt grounded external path",
            SourceKind: "chat-session",
            SourceId: string.Empty,
            CorrelationId: string.Empty,
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "integration-test",
            MetadataJson: metadataJson,
            InputSummary: "Analyze external path.",
            ResultSummary: string.Empty,
            ProviderName: provider.Name,
            Model: provider.DefaultModel,
            State: ExecutionState.Preparing,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            AutoApprovePendingToolCalls: false,
            ProcessRunId: string.Empty,
            ProcessStepId: string.Empty,
            SchedulerRunId: string.Empty,
            MessageId: string.Empty,
            Revision: 1L,
            StructuredOutputContractKey: string.Empty,
            StructuredOutputTypeName: string.Empty,
            StructuredOutputSchemaName: string.Empty,
            StructuredOutputSchemaDescription: string.Empty);
    }

    private sealed class EmptyStorageCatalogService : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([]);

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StorageCatalogRecord?>(null);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => Task.FromResult(rule);
    }

    private sealed class EmptyStorageDriverRegistry : IStorageDriverRegistry
    {
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds { get; } = [];

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver driver)
        {
            driver = null!;
            return false;
        }

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => throw new InvalidOperationException("No storage drivers are registered for this test.");
    }

    private sealed record BrowserMcpResult(IReadOnlyList<object> Content);

    private sealed record BrowserMcpContent(string? Text, string? Data);

    private static async Task<object> InvokeCreateCapabilityStateAsync(
        MafAgentRuntime runtime,
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        List<string> progressMessages,
        bool suppressApprovalRequirements = false)
    {
        var method = typeof(MafAgentRuntime).GetMethod(
                         "CreateCapabilityStateAsync",
                         BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? throw new InvalidOperationException("CreateCapabilityStateAsync method was not found.");
        var invocation = method.Invoke(
            runtime,
            [
                agent,
                provider,
                capabilities,
                Array.Empty<AgentMemoryRecord>(),
                (Func<ExecutionState, string, string, Task>)((_, _, message) =>
                {
                    progressMessages.Add(message);
                    return Task.CompletedTask;
                }),
                CancellationToken.None,
                suppressApprovalRequirements
            ]);
        var task = Assert.IsAssignableFrom<Task>(invocation);
        await task;

        return task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetValue(task)
               ?? throw new InvalidOperationException("CreateCapabilityStateAsync did not produce a result.");
    }

    private static async Task InvokeFilterUnusableApprovalToolsAsync(
        MafAgentRuntime runtime,
        object state,
        ProviderProfile provider,
        bool suppressApprovalRequirements,
        List<string> progressMessages)
    {
        var method = typeof(MafAgentRuntime).GetMethod(
                         "FilterUnusableApprovalToolsAsync",
                         BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? throw new InvalidOperationException("FilterUnusableApprovalToolsAsync method was not found.");
        var invocation = method.Invoke(
            runtime,
            [
                state,
                provider,
                suppressApprovalRequirements,
                (Func<ExecutionState, string, string, Task>)((_, _, message) =>
                {
                    progressMessages.Add(message);
                    return Task.CompletedTask;
                })
            ]);
        var task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
    }
}
