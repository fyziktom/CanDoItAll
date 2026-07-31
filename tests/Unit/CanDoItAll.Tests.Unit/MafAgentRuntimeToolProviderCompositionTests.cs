using System.Reflection;
using CapabilityExposureDescriptor = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityExposureDescriptor;
using AccessCapabilityDiagnosticCategory = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityDiagnosticCategory;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using AccessCapabilityOperationClassification = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityOperationClassification;
using EffectiveCapabilitySet = CanDoItAll.AgentFramework.Capabilities.Abstractions.EffectiveCapabilitySet;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using McpToolName = CanDoItAll.AgentFramework.Capabilities.Abstractions.McpToolName;

namespace CanDoItAll.Tests.Unit;

public sealed class MafAgentRuntimeToolProviderCompositionTests
{
    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_zero_registered_providers_does_not_attach_process_tools()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), progressMessages);

        var tools = ReadTools(state);
        Assert.Empty(tools);
        Assert.DoesNotContain(tools, tool =>
            tool.Name.StartsWith("processes_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(progressMessages, message =>
            message.Contains("registered runtime tool provider", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(progressMessages, message =>
            message.Contains("process-module tools", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_invokes_fake_providers_in_deterministic_order()
    {
        var lateProvider = new TestRuntimeToolProvider(20, "late_runtime_tool");
        var earlyProvider = new TestRuntimeToolProvider(10, "early_runtime_tool");
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(lateProvider);
        services.AddSingleton<IAgentRuntimeToolProvider>(earlyProvider);
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());
        var agent = CreateToolEnabledAgent();
        var provider = CreateProviderProfile();
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(runtime, agent, provider, progressMessages);

        var toolNames = ReadTools(state)
            .Select(tool => tool.Name)
            .ToList();
        Assert.Equal(["early_runtime_tool", "late_runtime_tool"], toolNames);
        Assert.Single(earlyProvider.Contexts);
        Assert.Single(lateProvider.Contexts);
        Assert.Equal(agent.Id, earlyProvider.Contexts[0].Agent.Id);
        Assert.Equal(provider.Id, earlyProvider.Contexts[0].Provider.Id);
        Assert.Equal(AgentRuntimeToolProviderPurpose.InteractiveChat, earlyProvider.Contexts[0].Purpose);
        Assert.False(earlyProvider.Contexts[0].SuppressApprovalRequirements);
        var descriptors = ReadProviderDescriptors(state);
        Assert.Equal(2, descriptors.Count);
        Assert.All(descriptors, descriptor =>
            Assert.StartsWith("legacy:", descriptor.ProviderKey, StringComparison.Ordinal));
        Assert.Contains(progressMessages, message =>
            message.Contains("Attached 2 tool(s) from 2 registered runtime tool provider(s).", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_propagates_authoritative_runtime_session_key()
    {
        var provider = new TestRuntimeToolProvider(10, "runtime_session_probe");
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(provider);
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        _ = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            [],
            AgentRuntimeContextIntent.Empty,
            runtimeSessionKey: "maf-runtime-session-17");

        var context = Assert.Single(provider.Contexts);
        Assert.Equal("maf-runtime-session-17", context.RuntimeSessionKey);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_delivers_exact_invocation_attachment()
    {
        var runtimeProvider = new TestRuntimeToolProvider(
            10,
            "runtime_attachment_probe");
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(runtimeProvider);
        var runtime = RuntimeCapabilityComposer.CreateDefault(
            Path.GetTempPath(),
            services.BuildServiceProvider());
        var attachment = new RuntimeToolTestAttachment("exact");
        var envelope = CreateRuntimeToolAttachmentEnvelope(attachment);

        _ = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            [],
            AgentRuntimeContextIntent.Empty,
            contextAttachments: [envelope]);

        var context = Assert.Single(runtimeProvider.Contexts);
        var capturedEnvelope = Assert.Single(
            context.GetAttachments<RuntimeToolTestAttachment>());
        Assert.True(capturedEnvelope.TryGetAttachment<RuntimeToolTestAttachment>(
            out var capturedAttachment));
        Assert.Same(attachment, capturedAttachment);
    }

    [Fact]
    public void MafAgentRuntimeHandoff_keeps_invocation_attachments_entry_agent_only()
    {
        var entryAgentId = Guid.NewGuid();
        var attachment = new RuntimeToolTestAttachment("entry-only");
        var transientContext = new AgentRuntimeTransientContext(
            "Entry context",
            WorkspaceScopeDescriptor.Sandbox,
            [CreateRuntimeToolAttachmentEnvelope(attachment)]);
        var runtimeOptions = new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: false,
            MaxStructuredOutputRepairAttempts: 0)
        {
            TransientContext = transientContext
        };

        var entryOptions =
            MafRuntimeAgentFactory.ResolveHandoffParticipantExecutionOptions(
                runtimeOptions,
                entryAgentId,
                entryAgentId);
        var otherOptions =
            MafRuntimeAgentFactory.ResolveHandoffParticipantExecutionOptions(
                runtimeOptions,
                Guid.NewGuid(),
                entryAgentId);

        Assert.Same(transientContext, entryOptions.TransientContext);
        Assert.Single(
            entryOptions.TransientContext!
                .GetAttachments<RuntimeToolTestAttachment>());
        Assert.Null(otherOptions.TransientContext);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_skips_registered_providers_when_context_disables_them()
    {
        var provider = new TestRuntimeToolProvider(10, "runtime_tool_should_not_attach");
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(provider);
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            [],
            AgentRuntimeContextIntent.Empty with
            {
                RuntimeToolProvidersEnabled = false
            });

        Assert.Empty(provider.Contexts);
        Assert.Empty(ReadTools(state));
        Assert.Empty(ReadProviderDescriptors(state));
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.RuntimeToolProvider &&
            source.Decision == AgentRuntimeContextSourceDecision.Excluded &&
            source.Reason.Contains("disabled by execution context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_tool_free_context_skips_every_capability_attachment_path()
    {
        var provider = new TestRuntimeToolProvider(10, "runtime_tool_should_not_attach");
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(provider);
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());
        var workspaceAccess = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(workspaceAccess));
        var capabilities = new[]
        {
            CreateToolCapability("provider-health", "provider_health"),
            CreateSkillCapability(
                "inline://tool-free-test",
                """
                {
                  "inlineSkill": {
                    "name": "tool-free-test",
                    "instructions": "This skill must not be attached."
                  }
                }
                """)
        };
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            capabilities,
            AgentRuntimeContextIntent.Empty with
            {
                ToolCapabilitiesEnabled = false
            },
            progressMessages);

        Assert.Empty(provider.Contexts);
        Assert.Empty(ReadTools(state));
        Assert.Empty(ReadProviderDescriptors(state));
        Assert.Empty(ReadContextProviders(state));
        Assert.Empty(ReadFrameworkToolNames(state));
        Assert.False(ReadHasApprovalTools(state));
        Assert.All(ReadContextSources(state), source =>
            Assert.Equal(AgentRuntimeContextSourceDecision.Excluded, source.Decision));
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.Skills);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.WorkspaceTools);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.RuntimeToolProvider);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.CatalogCapability);
        Assert.Contains(progressMessages, message =>
            message.Contains("explicitly tool-free", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_records_provider_descriptor_metadata()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.provider-a"),
            "provider_descriptor_tool"));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), progressMessages);

        var descriptor = Assert.Single(ReadProviderDescriptors(state));
        Assert.Equal("tests.provider-a", descriptor.ProviderKey);
        Assert.Equal("Test provider tests.provider-a", descriptor.DisplayName);
        Assert.Contains("tests", descriptor.DomainTags);
        Assert.Contains(AgentRuntimeToolProviderPurpose.InteractiveChat, descriptor.SupportedPurposes);
        Assert.Contains(progressMessages, message =>
            message.Contains("tests.provider-a", StringComparison.Ordinal) &&
            message.Contains("Test provider tests.provider-a", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_rejects_duplicate_provider_keys()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.duplicate-provider"),
            "first_provider_tool"));
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            20,
            CreateDescriptor("tests.duplicate-provider"),
            "second_provider_tool"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []);
        });

        Assert.Contains("Runtime tool provider key(s) must be unique", exception.Message, StringComparison.Ordinal);
        Assert.Contains("tests.duplicate-provider", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AgentRuntimeToolProviderDescriptor_rejects_null_or_empty_provider_key(string? providerKey)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeToolProviderDescriptor(providerKey!, "Display", "Description"));

        Assert.Equal("providerKey", exception.ParamName);
    }

    [Fact]
    public void AgentRuntimeToolProviderDescriptor_rejects_empty_display_name()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeToolProviderDescriptor("tests.provider", " ", "Description"));

        Assert.Equal("displayName", exception.ParamName);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_infers_tool_operation_metadata_from_policy_catalog()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.process-provider"),
            "processes_runs_list",
            "processes_run_start",
            "unclassified_provider_tool"));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []);

        var metadata = ReadToolMetadata(state);
        var readMetadata = Assert.Single(metadata, item => item.ToolName == "processes_runs_list");
        Assert.Equal("tests.process-provider", readMetadata.ProviderKey);
        Assert.Equal(AgentRuntimeToolOperationKind.Read, readMetadata.OperationKind);
        Assert.False(readMetadata.RequiresApprovalByDefault);
        Assert.Contains("tests", readMetadata.OwnershipTags);

        var mutationMetadata = Assert.Single(metadata, item => item.ToolName == "processes_run_start");
        Assert.Equal(AgentRuntimeToolOperationKind.Mutation, mutationMetadata.OperationKind);
        Assert.True(mutationMetadata.RequiresApprovalByDefault);

        var unknownMetadata = Assert.Single(metadata, item => item.ToolName == "unclassified_provider_tool");
        Assert.Equal(AgentRuntimeToolOperationKind.Unknown, unknownMetadata.OperationKind);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_rejects_tool_metadata_for_unknown_tool_name()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.metadata-provider"),
            ["metadata_known_tool"],
            [
                new AgentRuntimeToolMetadata(
                    "tests.metadata-provider",
                    "metadata_unknown_tool",
                    AgentRuntimeToolOperationKind.Read,
                    requiresApprovalByDefault: false)
            ]));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []));

        Assert.Contains("declared metadata for unknown tool name(s)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("metadata_unknown_tool", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_rejects_duplicate_provider_tool_names()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(10, "duplicate_runtime_tool"));
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(20, "duplicate_runtime_tool"));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []));

        Assert.Contains("Runtime tool provider", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate_runtime_tool", exception.Message, StringComparison.Ordinal);
        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_wraps_policy_mutation_tools_from_providers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            "processes_runs_list",
            "processes_run_start"));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []);

        var tools = ReadTools(state);
        Assert.IsNotType<ApprovalRequiredAIFunction>(Assert.Single(tools, tool => tool.Name == "processes_runs_list"));
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(tools, tool => tool.Name == "processes_run_start"));
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_read_only_step_filters_registered_runtime_tool_providers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.project-structure-provider"),
            AgentToolInvocationPolicyMetadata.ProjectStructureRead,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.WriteManagedProcessArtifacts));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureRead, toolNames);
        Assert.DoesNotContain(AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate, toolNames);
        Assert.DoesNotContain(AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart, toolNames);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.RuntimeToolProvider &&
            source.Decision == AgentRuntimeContextSourceDecision.Included &&
            source.ItemCount == 1);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_start_project_node_step_keeps_only_matching_runtime_mutation_tool()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.project-structure-provider"),
            AgentToolInvocationPolicyMetadata.ProjectStructureRead,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.StartProjectNodeProcess));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureRead, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart, toolNames);
        Assert.DoesNotContain(AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate, toolNames);
        Assert.Equal(2, toolNames.Count);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_external_action_project_structure_write_step_keeps_node_and_asset_create_tools()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.project-structure-provider"),
            AgentToolInvocationPolicyMetadata.ProjectStructureRead,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.WriteManagedProcessArtifacts));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureRead, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate, toolNames);
        Assert.DoesNotContain(AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart, toolNames);
    }

    [Fact]
    public async Task INV_MAF_ACCESS_002_runtime_provider_filter_uses_shared_policy_diagnostics()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.project-structure-provider"),
            AgentToolInvocationPolicyMetadata.ProjectStructureRead,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.StartProjectNodeProcess));

        var effectiveCapabilities = ReadEffectiveCapabilities(state);
        Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
            capability.RuntimeToolName?.Value == AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart);
        Assert.Contains(effectiveCapabilities.Diagnostics, diagnostic =>
            diagnostic.Identity.Key.Value == AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate.Replace('_', '-') &&
            diagnostic.Category == AccessCapabilityDiagnosticCategory.AccessPolicy);
    }

    [Fact]
    public async Task INV_MAF_ACCESS_003_catalog_descriptors_use_isolated_factory_source_paths()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-regression-descriptor-test-" + Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(workspaceRoot, "skills", "sample");
        Directory.CreateDirectory(skillRoot);
        await File.WriteAllTextAsync(Path.Combine(skillRoot, "SKILL.md"), "# Sample skill");
        try
        {
            var runtime = RuntimeCapabilityComposer.CreateDefault(workspaceRoot, new ServiceCollection().BuildServiceProvider());
            var state = await InvokeCreateCapabilityStateCoreAsync(
                runtime,
                CreateToolEnabledAgent(),
                CreateProviderProfile(),
                [
                    CreateSkillCapability("skills/sample"),
                    CreateToolCapability("workspace-read-file", "workspace_read_file")
                ],
                AgentRuntimeContextIntent.Empty);

            var effectiveCapabilities = ReadEffectiveCapabilities(state);
            Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
                capability.Identity.Key.Value == "sample-skill" &&
                capability.SourcePath?.Value == "Templates/Capabilities/skills/file/sample-skill.json");
            Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
                capability.RuntimeToolName?.Value == "workspace_read_file" &&
                capability.SourcePath?.Value == "Templates/Capabilities/tools/workspace-read-file.json");
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Default_template_agent_runtime_composes_app_owned_skills_tools_process_workflow_and_mcp_descriptors()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.project-process-workflow-provider"),
            AgentToolInvocationPolicyMetadata.ProjectStructureRead,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStatusGet,
            AgentToolInvocationPolicyMetadata.ProcessesRunsList,
            AgentToolInvocationPolicyMetadata.ProcessesRunStart));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));
        var provider = CreateProviderProfile();
        var capabilities = LoadDefaultTemplateCapabilities(
            "aspnet-core-skill",
            "run-tests",
            "concrete-deliverable-delivery-inline-skill",
            "playwright-local-mcp",
            "workspace-read-file",
            "workspace-dotnet-test",
            "workspace-dotnet-run",
            "workspace-dotnet-stop");
        var processIntent = CreateProcessContextIntent(
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.RunValidation,
            ProcessOperationContractNames.LaunchRuntime,
            ProcessOperationContractNames.CaptureRuntimeProof,
            ProcessOperationContractNames.StartProjectNodeProcess,
            ProcessOperationContractNames.ExecuteExternalAction);

        var accessPlan = InvokeCreateRuntimeCapabilityAccessPlan(
            runtime,
            agent,
            capabilities,
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            processIntent);
        var initialAllowed = ReadInitialAllowedCapabilities(accessPlan);

        Assert.Contains(initialAllowed, capability =>
            capability.Identity.Kind == AccessCapabilityKind.Skill &&
            capability.Identity.Key.Value == "aspnet-core-skill" &&
            capability.SourcePath?.Value == "Templates/Capabilities/skills/inline/aspnet-core-skill.json");
        Assert.Contains(initialAllowed, capability =>
            capability.Identity.Kind == AccessCapabilityKind.McpServer &&
            capability.Identity.Key.Value == "playwright-local-mcp" &&
            capability.McpServerKey?.Value == "playwright-local" &&
            capability.OperationClassifications.Contains(AccessCapabilityOperationClassification.BrowserAccess));
        Assert.Contains(initialAllowed, capability =>
            capability.RuntimeToolName?.Value == "workspace_dotnet_run");
        Assert.Contains(initialAllowed, capability =>
            capability.RuntimeToolName?.Value == "workspace_dotnet_stop");

        var progressMessages = new List<string>();
        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            provider,
            capabilities.Where(capability => capability.Kind != CapabilityKind.McpServer).ToList(),
            processIntent,
            progressMessages);

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("workspace_read_file", toolNames);
        Assert.Contains("workspace_dotnet_test", toolNames);
        Assert.Contains("workspace_dotnet_run", toolNames);
        Assert.Contains("workspace_dotnet_stop", toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureRead, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStatusGet, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProcessesRunsList, toolNames);
        Assert.Contains(AgentToolInvocationPolicyMetadata.ProcessesRunStart, toolNames);

        Assert.Contains(ReadFrameworkToolNames(state), toolName =>
            string.Equals(toolName, AgentToolInvocationPolicyMetadata.LoadSkill, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ReadFrameworkToolNames(state), toolName =>
            string.Equals(toolName, AgentToolInvocationPolicyMetadata.ReadSkillResource, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ReadFrameworkToolNames(state), toolName =>
            string.Equals(toolName, AgentToolInvocationPolicyMetadata.RunSkillScript, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.Skills &&
            source.Decision == AgentRuntimeContextSourceDecision.Included &&
            source.ItemCount == 3);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.WorkspaceTools &&
            source.Decision == AgentRuntimeContextSourceDecision.Included);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.RuntimeToolProvider &&
            source.Decision == AgentRuntimeContextSourceDecision.Included);
        Assert.Contains(progressMessages, message =>
            message.Contains("Loaded 0 file skill root(s), 3 inline skill(s), and 0 DI-provided skill(s)", StringComparison.Ordinal));

        var effectiveCapabilities = ReadEffectiveCapabilities(state);
        Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
            capability.Identity.Kind == AccessCapabilityKind.Skill &&
            capability.Identity.Key.Value == "concrete-deliverable-delivery-inline-skill");
        Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
            capability.RuntimeToolName?.Value == AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart);
        Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
            capability.RuntimeToolName?.Value == AgentToolInvocationPolicyMetadata.ProcessesRunStart);
    }

    [Fact]
    public async Task Local_mcp_capability_uses_runtime_client_factory_and_exposes_invocable_schema_tools()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-local-mcp-runtime-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            var toolName = McpToolName.Create("browser_snapshot");
            using var schemaDocument = JsonDocument.Parse("""
                {
                  "type": "object",
                  "properties": {
                    "url": {
                      "type": "string"
                    }
                  },
                  "required": [
                    "url"
                  ]
                }
                """);
            var fakeFactory = new FakeMcpClientFactory(new FakeMcpServerScript(
                Tools:
                [
                    new DiscoveredMcpTool(
                        toolName,
                        "Snapshot page state.",
                        schemaDocument.RootElement.Clone())
                ],
                ToolResults: new Dictionary<McpToolName, string>
                {
                    [toolName] = """{"content":[{"type":"text","text":"snapshot ok"}]}"""
                }));
            var services = new ServiceCollection();
            services.AddSingleton<IMcpClientFactory>(fakeFactory);
            var runtime = RuntimeCapabilityComposer.CreateDefault(workspaceRoot, services.BuildServiceProvider());
            var progressMessages = new List<string>();

            var state = await InvokeCreateCapabilityStateCoreAsync(
                runtime,
                CreateToolEnabledAgent(),
                CreateProviderProfile(),
                [CreateLocalMcpCapability()],
                CreateProcessContextIntent(ProcessOperationContractNames.CaptureRuntimeProof),
                progressMessages);

            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(ReadTools(state), tool => tool.Name == "browser_snapshot"));
            var result = await tool.InvokeAsync(
                new AIFunctionArguments
                {
                    ["url"] = "https://example.test"
                },
                CancellationToken.None);

            Assert.Equal(1, fakeFactory.CreatedClients);
            Assert.NotNull(fakeFactory.LastClient);
            Assert.Equal(1, fakeFactory.LastClient.StartCount);
            Assert.Equal(1, fakeFactory.LastClient.ListToolsCount);
            Assert.Equal(1, fakeFactory.LastClient.CallCount);
            Assert.Equal("object", tool.JsonSchema.GetProperty("type").GetString());
            Assert.Contains("url", tool.JsonSchema.GetRawText(), StringComparison.Ordinal);
            Assert.Contains("snapshot ok", result?.ToString(), StringComparison.Ordinal);
            Assert.Contains(progressMessages, message =>
                message.Contains("Attached 1 MCP tool(s) from 'Playwright Local MCP'", StringComparison.Ordinal));

            foreach (var disposable in ReadAsyncDisposables(state))
            {
                await disposable.DisposeAsync();
            }

            Assert.Equal(1, fakeFactory.LastClient.StopCount);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_read_only_step_does_not_attach_broad_workspace_tools()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.ReadProcessContext));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace_write_file", toolNames);
        Assert.DoesNotContain("workspace_dotnet_test", toolNames);
        Assert.DoesNotContain("workspace_pwsh_run_script", toolNames);
        Assert.DoesNotContain("workspace_dotnet_new", toolNames);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.WorkspaceTools &&
            source.Decision == AgentRuntimeContextSourceDecision.Excluded);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_validation_step_attaches_validation_tools_without_write_tools()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.RunValidation));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_test", toolNames);
        Assert.Contains("workspace_read_file", toolNames);
        Assert.DoesNotContain("workspace_write_file", toolNames);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.WorkspaceTools &&
            source.Decision == AgentRuntimeContextSourceDecision.Included &&
            source.ItemCount > 0);
    }

    [Fact]
    public async Task INV_MAF_ACCESS_001_process_policy_records_effective_capability_diagnostics()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.RunValidation));

        var effectiveCapabilities = ReadEffectiveCapabilities(state);
        Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
            capability.RuntimeToolName?.Value == "workspace_dotnet_test");
        Assert.Contains(effectiveCapabilities.Diagnostics, diagnostic =>
            diagnostic.Identity.Key.Value == "workspace-write-file" &&
            diagnostic.Category == AccessCapabilityDiagnosticCategory.AccessPolicy &&
            diagnostic.Reason.Contains("do not include", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ReadTools(state), tool =>
            string.Equals(tool.Name, "workspace_write_file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_managed_artifact_write_step_attaches_workspace_writes_without_product_mutation_tools()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("workspace_write_file", toolNames);
        Assert.Contains("workspace_append_file", toolNames);
        Assert.Contains("workspace_write_spreadsheet", toolNames);
        Assert.Contains("workspace_read_file", toolNames);
        Assert.DoesNotContain("workspace_dotnet_new", toolNames);
        Assert.DoesNotContain("workspace_create_directory", toolNames);
        Assert.DoesNotContain("workspace_delete_path", toolNames);
        Assert.DoesNotContain("workspace_pwsh_run_script", toolNames);
        Assert.DoesNotContain("workspace_dotnet_test", toolNames);

        var effectiveCapabilities = ReadEffectiveCapabilities(state);
        Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
            capability.RuntimeToolName?.Value == "workspace_write_file");
        Assert.Contains(effectiveCapabilities.AllowedCapabilities, capability =>
            capability.RuntimeToolName?.Value == "workspace_write_spreadsheet");
        Assert.Contains(effectiveCapabilities.Diagnostics, diagnostic =>
            diagnostic.Identity.Key.Value == "workspace-dotnet-new" &&
            diagnostic.Category == AccessCapabilityDiagnosticCategory.AccessPolicy);
    }

    [Fact]
    public async Task MafAgentRuntimeProjectStructureContext_business_analysis_agent_attaches_workspace_spreadsheet_writer()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.BusinessAnalysis)));
        var projectId = Guid.NewGuid();

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            AgentRuntimeContextIntent.Empty with
            {
                SourceKind = "project-structure",
                SourceId = projectId.ToString("D"),
                WorkspaceScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"))
            });

        Assert.Contains(ReadTools(state), tool => tool.Name == "workspace_write_spreadsheet");
        Assert.Contains(ReadEffectiveCapabilities(state).AllowedCapabilities, capability =>
            capability.RuntimeToolName?.Value == "workspace_write_spreadsheet");
    }

    [Fact]
    public async Task MafAgentRuntimeWorkspaceTools_skips_configured_tools_when_context_disables_them()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            AgentRuntimeContextIntent.Empty with
            {
                WorkspaceToolsEnabled = false
            });

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace_read_file", toolNames);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.WorkspaceTools &&
            source.Decision == AgentRuntimeContextSourceDecision.Excluded &&
            source.Reason.Contains("disabled by execution context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MafAgentRuntimeWorkspaceTools_skips_catalog_workspace_tools_when_context_disables_them()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var capabilities = new[]
        {
            CreateToolCapability("workspace-read-file", "workspace_read_file"),
            CreateToolCapability("provider-health", "provider_health")
        };

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateToolEnabledAgent(),
            CreateProviderProfile(),
            capabilities,
            AgentRuntimeContextIntent.Empty with
            {
                WorkspaceToolsEnabled = false
            });

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace_read_file", toolNames);
        Assert.Contains("provider_health", toolNames);
        Assert.Contains(ReadContextSources(state), source =>
            source.Category == AgentRuntimeContextSourceCategories.CatalogCapability &&
            source.SourceId == "workspace-read-file" &&
            source.Decision == AgentRuntimeContextSourceDecision.Excluded &&
            source.Reason.Contains("disabled by execution context", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_runtime_proof_step_attaches_image_analysis_tool()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.QualityValidation)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.CaptureRuntimeProof));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("workspace_analyze_image", toolNames);
        Assert.Contains("workspace_analyze_images", toolNames);
        Assert.Contains("workspace_inspect_image", toolNames);
        Assert.DoesNotContain("workspace_dotnet_new", toolNames);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_mutating_product_step_keeps_scaffold_tool_for_software_development_agent()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.MutateProductTarget,
                ProcessOperationContractNames.RunValidation));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_new", toolNames);
        Assert.Contains("workspace_write_file", toolNames);
        Assert.Contains("workspace_dotnet_build", toolNames);
        Assert.Contains("workspace_pwsh_run_script", toolNames);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_mutating_product_step_keeps_configured_workspace_tools_when_catalog_contains_same_tool()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var workspaceAccess = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(workspaceAccess));
        var capabilities = LoadDefaultTemplateCapabilities(
            "workspace-dotnet-new",
            "workspace-pwsh-run-script",
            "workspace-dotnet-build",
            "workspace-write-file");
        var processIntent = CreateProcessContextIntent(
            ProcessOperationContractNames.ReadProjectStructure,
            ProcessOperationContractNames.MutateProductTarget,
            ProcessOperationContractNames.RunValidation,
            ProcessOperationContractNames.WriteManagedProcessArtifacts);

        var accessPlan = InvokeCreateRuntimeCapabilityAccessPlan(
            runtime,
            agent,
            capabilities,
            workspaceAccess,
            processIntent);

        var configuredTag = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityTag.Create("configured");
        var initialAllowed = ReadInitialAllowedCapabilities(accessPlan);
        Assert.Contains(initialAllowed, capability =>
            capability.RuntimeToolName?.Value == "workspace_pwsh_run_script" &&
            capability.Tags.Contains(configuredTag));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            capabilities,
            processIntent);

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_new", toolNames);
        Assert.Contains("workspace_write_file", toolNames);
        Assert.Contains("workspace_dotnet_build", toolNames);
        Assert.Contains("workspace_pwsh_run_script", toolNames);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_mutating_product_step_keeps_git_mutation_tools_for_software_development_agent()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));

        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            CreateProviderProfile(),
            [],
            CreateProcessContextIntent(
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.MutateProductTarget,
                ProcessOperationContractNames.RunValidation));

        var toolNames = ReadTools(state).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("workspace_git_status", toolNames);
        Assert.Contains("workspace_git_diff", toolNames);
        Assert.Contains("workspace_git_log", toolNames);
        Assert.Contains("workspace_git_show", toolNames);
        Assert.Contains("workspace_git_add", toolNames);
        Assert.Contains("workspace_git_unstage", toolNames);
        Assert.Contains("workspace_git_commit", toolNames);
        Assert.Contains("workspace_git_branch_create", toolNames);
        Assert.Contains("workspace_git_switch", toolNames);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_two_step_process_reduces_tool_surface_against_agent_baseline()
    {
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var agent = CreateToolEnabledAgent(CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)));
        var provider = CreateProviderProfile();

        var baselineState = await InvokeCreateCapabilityStateAsync(runtime, agent, provider, []);
        var readOnlyStepState = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            provider,
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.ReadProcessContext));
        var validationStepState = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            agent,
            provider,
            [],
            CreateProcessContextIntent(ProcessOperationContractNames.RunValidation));

        var baselineToolNames = ReadTools(baselineState).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var readOnlyToolNames = ReadTools(readOnlyStepState).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validationToolNames = ReadTools(validationStepState).Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(readOnlyToolNames.Count < baselineToolNames.Count);
        Assert.True(validationToolNames.Count < baselineToolNames.Count);
        Assert.Empty(readOnlyToolNames);
        Assert.Contains("workspace_dotnet_test", validationToolNames);
        Assert.Contains("workspace_git_status", validationToolNames);
        Assert.Contains("workspace_git_diff", validationToolNames);
        Assert.DoesNotContain("workspace_git_add", validationToolNames);
        Assert.DoesNotContain("workspace_git_commit", validationToolNames);
        Assert.DoesNotContain("workspace_write_file", validationToolNames);
        Assert.DoesNotContain("workspace_dotnet_new", validationToolNames);
        Assert.Contains(ReadContextSources(readOnlyStepState), source =>
            source.Category == AgentRuntimeContextSourceCategories.WorkspaceTools &&
            source.Decision == AgentRuntimeContextSourceDecision.Excluded);
    }

    [Fact]
    public async Task MafAgentRuntimeProcessContext_read_only_step_skips_skill_provider()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-skill-test-" + Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(workspaceRoot, "skills", "sample");
        Directory.CreateDirectory(skillRoot);
        await File.WriteAllTextAsync(Path.Combine(skillRoot, "SKILL.md"), "# Sample skill");
        try
        {
            var runtime = RuntimeCapabilityComposer.CreateDefault(workspaceRoot, new ServiceCollection().BuildServiceProvider());
            var state = await InvokeCreateCapabilityStateCoreAsync(
                runtime,
                CreateToolEnabledAgent(),
                CreateProviderProfile(),
                [CreateSkillCapability("skills/sample")],
                CreateProcessContextIntent(ProcessOperationContractNames.ReadProcessContext));

            Assert.DoesNotContain(ReadFrameworkToolNames(state), toolName =>
                string.Equals(toolName, AgentToolInvocationPolicyMetadata.LoadSkill, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(ReadContextSources(state), source =>
                source.Category == AgentRuntimeContextSourceCategories.Skills &&
                source.Decision == AgentRuntimeContextSourceDecision.Excluded);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MafAgentRuntimeSkillsProvider_disables_read_only_and_script_approval_when_script_approval_is_not_required()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-skill-approval-test-" + Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(workspaceRoot, "skills", "sample");
        Directory.CreateDirectory(skillRoot);
        await File.WriteAllTextAsync(Path.Combine(skillRoot, "SKILL.md"), "# Sample skill");
        try
        {
            var runtime = RuntimeCapabilityComposer.CreateDefault(workspaceRoot, new ServiceCollection().BuildServiceProvider());
            var state = await InvokeCreateCapabilityStateCoreAsync(
                runtime,
                CreateToolEnabledAgent(),
                CreateProviderProfile(),
                [CreateSkillCapability("skills/sample")],
                AgentRuntimeContextIntent.Empty,
                suppressApprovalRequirements: false);

            var options = ReadAgentSkillsProviderOptions(state);
            Assert.True(options.DisableLoadSkillApproval);
            Assert.True(options.DisableReadSkillResourceApproval);
            Assert.True(options.DisableRunSkillScriptApproval);
            Assert.False(ReadHasApprovalTools(state));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MafAgentRuntimeSkillsProvider_keeps_script_approval_when_capability_requires_it()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "candoitall-skill-script-approval-test-" + Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(workspaceRoot, "skills", "sample");
        Directory.CreateDirectory(skillRoot);
        await File.WriteAllTextAsync(Path.Combine(skillRoot, "SKILL.md"), "# Sample skill");
        try
        {
            var runtime = RuntimeCapabilityComposer.CreateDefault(workspaceRoot, new ServiceCollection().BuildServiceProvider());
            var state = await InvokeCreateCapabilityStateCoreAsync(
                runtime,
                CreateToolEnabledAgent(),
                CreateProviderProfile(),
                [CreateSkillCapability("skills/sample", "{\"scriptExecution\":{\"approvalRequired\":true}}")],
                AgentRuntimeContextIntent.Empty,
                suppressApprovalRequirements: false);

            var options = ReadAgentSkillsProviderOptions(state);
            Assert.True(options.DisableLoadSkillApproval);
            Assert.True(options.DisableReadSkillResourceApproval);
            Assert.False(options.DisableRunSkillScriptApproval);
            Assert.True(ReadHasApprovalTools(state));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_reports_provider_failures_with_provider_type()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new ThrowingRuntimeToolProvider());
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []));

        Assert.Contains(nameof(ThrowingRuntimeToolProvider), exception.Message, StringComparison.Ordinal);
        Assert.Contains("failed to create tools", exception.Message, StringComparison.Ordinal);
        Assert.Contains("provider failed intentionally", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MafAgentRuntimeComposition_rejects_retired_catalog_memory_capability()
    {
        var capability = CreateLegacyMemoryCapability();
        var agent = CreateToolEnabledAgent() with
        {
            Capabilities =
            [
                new AgentCapabilityAssignment(
                    capability.Id,
                    capability.Key,
                    capability.Kind,
                    CapabilityProofStatus.Verified,
                    DateTimeOffset.UtcNow,
                    string.Empty)
            ]
        };
        var runtime = RuntimeCapabilityComposer.CreateDefault(
            Path.GetTempPath(),
            new ServiceCollection().BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateCoreAsync(
                runtime,
                agent,
                CreateProviderProfile(),
                [capability],
                AgentRuntimeContextIntent.Empty));

        Assert.Contains("retired", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("agent Memory settings", exception.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<AITool> ReadTools(object state)
        => Assert.IsAssignableFrom<IEnumerable<AITool>>(
                state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static IReadOnlyList<AgentRuntimeToolProviderDescriptor> ReadProviderDescriptors(object state)
        => Assert.IsAssignableFrom<IEnumerable<AgentRuntimeToolProviderDescriptor>>(
                state.GetType().GetProperty("RuntimeToolProviderDescriptors", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static IReadOnlyList<AgentRuntimeToolMetadata> ReadToolMetadata(object state)
        => Assert.IsAssignableFrom<IEnumerable<AgentRuntimeToolMetadata>>(
                state.GetType().GetProperty("RuntimeToolMetadata", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static IReadOnlyList<AIContextProvider> ReadContextProviders(object state)
        => Assert.IsAssignableFrom<IEnumerable<AIContextProvider>>(
                state.GetType().GetProperty("ContextProviders", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static AgentSkillsProviderOptions ReadAgentSkillsProviderOptions(object state)
    {
        var provider = Assert.Single(ReadContextProviders(state), contextProvider =>
            string.Equals(contextProvider.GetType().FullName, typeof(AgentSkillsProvider).FullName, StringComparison.Ordinal));

        var options = EnumerateInstanceFields(provider.GetType())
            .Where(field => typeof(AgentSkillsProviderOptions).IsAssignableFrom(field.FieldType))
            .Select(field => field.GetValue(provider))
            .OfType<AgentSkillsProviderOptions>()
            .SingleOrDefault();

        return Assert.IsType<AgentSkillsProviderOptions>(options);
    }

    private static EffectiveCapabilitySet ReadEffectiveCapabilities(object state)
        => Assert.IsType<EffectiveCapabilitySet>(
            state.GetType().GetProperty("EffectiveCapabilities", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

    private static IReadOnlyList<AgentRuntimeContextManifestSource> ReadContextSources(object state)
        => Assert.IsAssignableFrom<IEnumerable<AgentRuntimeContextManifestSource>>(
                state.GetType().GetProperty("ContextSources", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static IReadOnlyList<string> ReadFrameworkToolNames(object state)
        => Assert.IsAssignableFrom<IEnumerable<string>>(
                state.GetType().GetProperty("FrameworkToolNames", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static IReadOnlyList<IAsyncDisposable> ReadAsyncDisposables(object state)
        => Assert.IsAssignableFrom<IEnumerable<IAsyncDisposable>>(
                state.GetType().GetProperty("AsyncDisposables", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static bool ReadHasApprovalTools(object state)
        => Assert.IsType<bool>(
            state.GetType().GetProperty("HasApprovalTools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

    private static IEnumerable<FieldInfo> EnumerateInstanceFields(Type type)
    {
        for (var currentType = type; currentType is not null; currentType = currentType.BaseType)
        {
            foreach (var field in currentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                yield return field;
            }
        }
    }

    private static AgentRuntimeToolProviderDescriptor CreateDescriptor(string providerKey)
        => new(
            providerKey,
            $"Test provider {providerKey}",
            "Test runtime provider.",
            ["tests"],
            [AgentRuntimeToolProviderPurpose.InteractiveChat]);

    private static async Task<object> InvokeCreateCapabilityStateAsync(
        RuntimeCapabilityComposer composer,
        AgentDefinition agent,
        ProviderProfile provider,
        List<string> progressMessages,
        bool suppressApprovalRequirements = false)
    {
        return await composer.CreateCapabilityStateAsync(
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            Array.Empty<AgentMemoryRecord>(),
            (_, _, message) =>
            {
                progressMessages.Add(message);
                return Task.CompletedTask;
            },
            CancellationToken.None,
            suppressApprovalRequirements);
    }

    private static async Task<object> InvokeCreateCapabilityStateCoreAsync(
        RuntimeCapabilityComposer composer,
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        AgentRuntimeContextIntent contextIntent,
        List<string>? progressMessages = null,
        bool suppressApprovalRequirements = true,
        string runtimeSessionKey = "",
        IReadOnlyList<AgentChatContextAttachmentEnvelope>? contextAttachments = null)
    {
        return await composer.CreateCapabilityStateCoreAsync(
            agent,
            provider,
            string.IsNullOrWhiteSpace(agent.Model) ? provider.DefaultModel : agent.Model,
            capabilities,
            Array.Empty<AgentMemoryRecord>(),
            (_, _, message) =>
            {
                progressMessages?.Add(message);
                return Task.CompletedTask;
            },
            CancellationToken.None,
            suppressApprovalRequirements,
            WorkspaceScopeDescriptor.Sandbox,
            contextIntent,
            runtimeSessionKey,
            contextAttachments);
    }

    private static AgentChatContextAttachmentEnvelope
        CreateRuntimeToolAttachmentEnvelope(
            IAgentChatContextAttachment attachment)
    {
        return new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind("tests.runtime-tool"),
            new SnapshotContentFingerprint("content-1"),
            new SnapshotCoverageFingerprint("coverage-1"),
            new DatabaseProfileGeneration(1),
            new SnapshotFreshnessFingerprint("freshness-1"),
            new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero),
            freshUntilUtc: null,
            attachment: attachment)
            .CreateEnvelope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(
                    new AgentChatContextSourceKind("tests"),
                    new AgentChatContextSourceId("runtime-tool")),
                WorkspaceScopeDescriptor.Sandbox,
                new AgentChatContextContributorId("runtime-tool"),
                new ModulePublicationRevision(1));
    }

    private static object InvokeCreateRuntimeCapabilityAccessPlan(
        RuntimeCapabilityComposer composer,
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent)
    {
        return composer.CreateRuntimeCapabilityAccessPlan(
            agent,
            capabilities,
            workspaceToolAccess,
            contextIntent,
            storageToolsAvailable: false);
    }

    private static IReadOnlyList<CapabilityExposureDescriptor> ReadInitialAllowedCapabilities(object accessPlan)
        => Assert.IsAssignableFrom<IEnumerable<CapabilityExposureDescriptor>>(
                accessPlan.GetType().GetProperty("InitialAllowedCapabilities", BindingFlags.Public | BindingFlags.Instance)?.GetValue(accessPlan))
            .ToList();

    private static IReadOnlyList<CapabilityCatalogItem> LoadDefaultTemplateCapabilities(params string[] keys)
    {
        var capabilities = CapabilityTemplateSeedMaterializer.MaterializeDefaultCapabilities(new CapabilityTemplatePackLoader().Load())
            .ToDictionary(capability => capability.Key, StringComparer.OrdinalIgnoreCase);

        return keys.Select(key => capabilities[key]).ToList();
    }

    private static AgentDefinition CreateToolEnabledAgent(string configurationJson = "{}")
        => new(
            Id: Guid.NewGuid(),
            Name: "Tool Provider Agent",
            RoleTitle: "Tester",
            Summary: "Tests runtime tool providers.",
            Instructions: "Use supplied tools.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: string.Empty,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: configurationJson,
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

    private static string CreateWorkspaceToolConfiguration(AgentWorkspaceToolAccessSettings accessSettings)
        => AgentWorkspaceToolAccessMetadata.Write("{}", accessSettings);

    private static AgentRuntimeContextIntent CreateProcessContextIntent(params string[] allowedOperations)
        => new(
            SourceKind: "process-step",
            SourceId: "unit-test",
            ProcessRunId: Guid.NewGuid().ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"),
            TargetScope: ProcessOperationContractNames.ManagedOutputProduct,
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: allowedOperations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase),
            AllowsProductMutation: allowedOperations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase),
            WorkspaceToolProfile: null,
            WorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
            AllowedOperations: allowedOperations);

    private static CapabilityCatalogItem CreateSkillCapability(
        string endpointOrPath,
        string configurationJson = "{}")
        => new(
            Id: Guid.NewGuid(),
            Kind: CapabilityKind.Skill,
            Key: "sample-skill",
            Name: "Sample skill",
            Description: "Sample skill.",
            EndpointOrPath: endpointOrPath,
            ConfigurationJson: configurationJson,
            ProofStatus: CapabilityProofStatus.Verified,
            ProofNotes: string.Empty,
            LastVerifiedAtUtc: DateTimeOffset.UtcNow,
            IsBuiltIn: false);

    private static CapabilityCatalogItem CreateToolCapability(string key, string toolName)
        => new(
            Id: Guid.NewGuid(),
            Kind: CapabilityKind.Tool,
            Key: key,
            Name: key,
            Description: key + " test capability.",
            EndpointOrPath: string.Empty,
            ConfigurationJson: "{\"tool\":\"" + toolName + "\"}",
            ProofStatus: CapabilityProofStatus.Verified,
            ProofNotes: string.Empty,
            LastVerifiedAtUtc: DateTimeOffset.UtcNow,
            IsBuiltIn: false);

    private static CapabilityCatalogItem CreateLegacyMemoryCapability()
        => new(
            Id: Guid.NewGuid(),
            Kind: CapabilityKind.Memory,
            Key: "legacy-mem0-memory",
            Name: "Legacy Mem0 memory",
            Description: "Legacy catalog memory capability.",
            EndpointOrPath: "https://api.mem0.ai",
            ConfigurationJson: "{\"provider\":\"mem0\",\"endpoint\":\"https://api.mem0.ai\",\"apiKeyEnvironmentVariable\":\"CANDOITALL_TEST_RETIRED_MEM0_KEY\"}",
            ProofStatus: CapabilityProofStatus.Verified,
            ProofNotes: string.Empty,
            LastVerifiedAtUtc: DateTimeOffset.UtcNow,
            IsBuiltIn: false);

    private static CapabilityCatalogItem CreateLocalMcpCapability()
        => new(
            Id: Guid.NewGuid(),
            Kind: CapabilityKind.McpServer,
            Key: "playwright-local-mcp",
            Name: "Playwright Local MCP",
            Description: "Local browser automation MCP.",
            EndpointOrPath: string.Empty,
            ConfigurationJson: """
                {
                  "serverName": "playwright-local",
                  "command": "node",
                  "arguments": [
                    "server.js"
                  ],
                  "workingDirectory": ".",
                  "messageFraming": "newlineDelimitedJson",
                  "allowedTools": [
                    "browser_snapshot"
                  ],
                  "approvalMode": "NeverRequire",
                  "timeoutSeconds": 5
                }
                """,
            ProofStatus: CapabilityProofStatus.Verified,
            ProofNotes: string.Empty,
            LastVerifiedAtUtc: DateTimeOffset.UtcNow,
            IsBuiltIn: false);

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.NewGuid(),
            "Unit Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.ChatCompletions,
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

    private sealed class TestRuntimeToolProvider : IAgentRuntimeToolProvider
    {
        private readonly IReadOnlyList<string> toolNames;
        private readonly IReadOnlyList<AgentRuntimeToolMetadata> metadata;

        public TestRuntimeToolProvider(int order, params string[] toolNames)
            : this(order, null, toolNames)
        {
        }

        public TestRuntimeToolProvider(
            int order,
            AgentRuntimeToolProviderDescriptor? descriptor,
            params string[] toolNames)
            : this(order, descriptor, toolNames, [])
        {
        }

        public TestRuntimeToolProvider(
            int order,
            AgentRuntimeToolProviderDescriptor? descriptor,
            IReadOnlyList<string> toolNames,
            IReadOnlyList<AgentRuntimeToolMetadata> metadata)
        {
            Order = order;
            Descriptor = descriptor;
            this.toolNames = toolNames;
            this.metadata = metadata;
        }

        public int Order { get; }

        public AgentRuntimeToolProviderDescriptor? Descriptor { get; }

        public List<AgentRuntimeToolProviderContext> Contexts { get; } = [];

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Contexts.Add(context);
            return ValueTask.FromResult<IReadOnlyList<AITool>>(toolNames
                .Select(toolName => AIFunctionFactory.Create(() => "ok", toolName, "Test runtime provider tool."))
                .ToList());
        }

        public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
            AgentRuntimeToolProviderContext context)
            => metadata;
    }

    private sealed record RuntimeToolTestAttachment(string Value) :
        IAgentChatContextAttachment;

    private sealed class ThrowingRuntimeToolProvider : IAgentRuntimeToolProvider
    {
        public int Order => 0;

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("provider failed intentionally");
    }
}
