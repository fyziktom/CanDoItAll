using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
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
            ["Base instructions.", policy, AgentFinalizerMode.Required]));

        Assert.Contains("Call `submit_process_step_outcome` exactly once", instructions, StringComparison.Ordinal);
        Assert.Contains("return a JSON object", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not use Markdown or prose", instructions, StringComparison.Ordinal);
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
            ["Base instructions.", policy, AgentFinalizerMode.Shadow]));

        Assert.Contains("Finalizer tool shadow policy", instructions, StringComparison.Ordinal);
        Assert.Contains("runtime can compare both outputs", instructions, StringComparison.Ordinal);
        Assert.Contains("Do not use Markdown or prose", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_tool_guard_does_not_embed_calculator_process_guidance()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.cs"));

        Assert.DoesNotContain("Calculator.Tests", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Calculator/Components/Pages/Home.razor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("If this is the calculator process", source, StringComparison.Ordinal);
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
        var seededAgent = seed.Agents[0];
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
        var seededAgent = seed.Agents[0];
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
    public async Task Approval_filter_omits_unusable_mutation_tools_for_manual_ollama_run()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent();
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
    public async Task Workspace_plugin_mutation_tools_remain_available_when_approval_requirements_are_suppressed()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolAgent();
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

        Assert.Contains(
            progressMessages,
            item => item.Contains("Attached internal process-module tools", StringComparison.Ordinal));
    }

    private sealed class OpaqueToolCallContent(
        string callId,
        string name,
        IDictionary<string, object?> arguments) : ToolCallContent(callId)
    {
        public string Name { get; } = name;

        public IDictionary<string, object?> Arguments { get; } = arguments;
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
