using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Unit;

public sealed class CapabilityCuratorAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Provider_fails_closed_for_identity_spoofs_and_reauthorizes_each_invocation()
    {
        var harness = CreateHarness([CapabilityCuratorAgentCapabilityKeys.CatalogSearch]);
        var wrongId = harness.Context with { Agent = harness.Context.Agent with { Id = Guid.NewGuid() } };
        var wrongTemplateKey = harness.Context with
        {
            Agent = harness.Context.Agent with { TemplateKey = "capability-curator-spoof" }
        };
        var suspended = harness.Context with
        {
            Agent = harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
        };
        var template = harness.Context with
        {
            Agent = harness.Context.Agent with { IsTemplate = true }
        };
        var toolsDisabled = harness.Context with
        {
            Agent = harness.Context.Agent with
            {
                Permissions = harness.Context.Agent.Permissions with { CanUseTools = false }
            }
        };
        var wrongPurpose = harness.Context with
        {
            Purpose = AgentRuntimeToolProviderPurpose.GovernedProcessAutomation
        };
        var assignment = Assert.Single(harness.Context.Agent.Capabilities);
        var catalogSpoof = harness.Context with
        {
            Capabilities = harness.Context.Capabilities
                .Select(item => item.Id == assignment.CapabilityId
                    ? item with { Key = $"{item.Key}-spoof" }
                    : item)
                .ToArray()
        };

        foreach (var context in new[]
                 {
                     wrongId,
                     wrongTemplateKey,
                     suspended,
                     template,
                     toolsDisabled,
                     wrongPurpose,
                     catalogSpoof
                 })
        {
            Assert.Empty(await harness.Provider.CreateToolsAsync(context, CancellationToken.None));
            Assert.Empty(harness.Provider.GetToolMetadata(context));
        }

        var search = Assert.IsAssignableFrom<AIFunction>(Assert.Single(
            await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None)));
        harness.Workspace.Agents = harness.Workspace.Agents
            .Select(agent => agent.Id == CapabilityCuratorAgentIdentity.AgentId
                ? agent with { Capabilities = [] }
                : agent)
            .ToArray();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => search.InvokeAsync(
            new AIFunctionArguments
            {
                ["request"] = new CapabilityCuratorCatalogSearchInput()
            }).AsTask());
    }

    [Fact]
    public async Task Provider_attaches_assigned_tools_to_exact_managed_hr_identity_and_fails_closed_for_spoofs()
    {
        var harness = CreateHarness(
            [CapabilityCuratorAgentCapabilityKeys.CatalogSearch],
            useHrActor: true);

        var search = Assert.IsAssignableFrom<AIFunction>(Assert.Single(
            await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None)));
        Assert.Equal(
            AgentToolInvocationPolicyMetadata.CapabilityCuratorCatalogSearch,
            Assert.Single(harness.Provider.GetToolMetadata(harness.Context)).ToolName);

        var wrongId = harness.Context with
        {
            Agent = harness.Context.Agent with { Id = Guid.NewGuid() }
        };
        var wrongTemplateKey = harness.Context with
        {
            Agent = harness.Context.Agent with { TemplateKey = $"{HrAgentIdentity.TemplateKey}-spoof" }
        };
        var suspended = harness.Context with
        {
            Agent = harness.Context.Agent with { Status = AgentLifecycleStatus.Suspended }
        };
        var template = harness.Context with
        {
            Agent = harness.Context.Agent with { IsTemplate = true }
        };
        var toolsDisabled = harness.Context with
        {
            Agent = harness.Context.Agent with
            {
                Permissions = harness.Context.Agent.Permissions with { CanUseTools = false }
            }
        };
        var revokedAssignment = harness.Context with
        {
            Agent = harness.Context.Agent with { Capabilities = [] }
        };

        foreach (var context in new[]
                 {
                     wrongId,
                     wrongTemplateKey,
                     suspended,
                     template,
                     toolsDisabled,
                     revokedAssignment
                 })
        {
            Assert.Empty(await harness.Provider.CreateToolsAsync(context, CancellationToken.None));
            Assert.Empty(harness.Provider.GetToolMetadata(context));
        }

        var wrongCase = CreateHarness(
            [CapabilityCuratorAgentCapabilityKeys.CatalogSearch.ToUpperInvariant()],
            useHrActor: true);
        Assert.Empty(await wrongCase.Provider.CreateToolsAsync(wrongCase.Context, CancellationToken.None));
        Assert.Empty(wrongCase.Provider.GetToolMetadata(wrongCase.Context));

        harness.Workspace.Agents = harness.Workspace.Agents
            .Select(agent => agent.Id == HrAgentIdentity.AgentId
                ? agent with { Capabilities = [] }
                : agent)
            .ToArray();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => search.InvokeAsync(
            new AIFunctionArguments
            {
                ["request"] = new CapabilityCuratorCatalogSearchInput()
            }).AsTask());
    }

    [Fact]
    public async Task Hr_actor_attaches_only_least_privilege_curation_tools_even_if_assignment_tools_are_assigned()
    {
        var harness = CreateHarness(
            CapabilityCuratorAgentIdentity.ToolCapabilityKeys,
            useHrActor: true);
        var expectedToolNames = CapabilityCuratorAgentCapabilityKeys.ToolNameToCapabilityKey
            .Where(item => HrAgentIdentity.CapabilityCurationCapabilityKeys.Contains(item.Value))
            .Select(item => item.Key)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var tools = await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None);
        var metadata = harness.Provider.GetToolMetadata(harness.Context);

        Assert.Equal(
            expectedToolNames,
            tools.Select(tool => tool.Name).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(
            expectedToolNames,
            metadata.Select(item => item.ToolName).OrderBy(item => item, StringComparer.Ordinal));
        Assert.DoesNotContain(
            AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet,
            tools.Select(tool => tool.Name),
            StringComparer.Ordinal);
        Assert.DoesNotContain(
            AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate,
            tools.Select(tool => tool.Name),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Hr_actor_can_create_custom_inline_skill_and_verify_only_after_assignment()
    {
        var harness = CreateHarness(
            [
                CapabilityCuratorAgentCapabilityKeys.Save,
                CapabilityCuratorAgentCapabilityKeys.Verify
            ],
            useHrActor: true);
        var tools = await CreateToolDictionaryAsync(harness);
        var save = tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave];
        var verify = tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify];

        var created = await InvokeAsync<CapabilityCuratorEditorResult>(
            save,
            CreateInlineSkillCandidate(
                "hr-authored-inline-skill",
                "HR authored inline skill",
                "Apply this narrowly scoped HR-authored skill."));

        Assert.Equal(HrAgentIdentity.AgentId, harness.Context.Agent.Id);
        Assert.Equal(AgentToolInvocationPolicyMetadata.CapabilityCuratorSave, save.Name);
        Assert.Equal("hr-authored-inline-skill", created.Key);
        Assert.Equal("hr-authored-inline-skill", created.Configuration.Skill!.InlineName);
        Assert.Equal(
            "Apply this narrowly scoped HR-authored skill.",
            created.Configuration.Skill.InlineInstructions);
        Assert.False(created.IsBuiltIn);
        Assert.Equal(1, harness.Workspace.SaveCapabilityCallCount);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeAsync<CapabilityCuratorVerifyResult>(
                verify,
                new CapabilityCuratorVerifyInput(
                    harness.TargetAgentId,
                    created.CapabilityId)));

        var savedCapability = Assert.Single(
            harness.Workspace.Capabilities,
            capability => capability.Id == created.CapabilityId);
        harness.Workspace.Agents = harness.Workspace.Agents
            .Select(agent => agent.Id == harness.TargetAgentId
                ? agent with
                {
                    Capabilities = agent.Capabilities
                        .Append(ToAssignment(savedCapability))
                        .ToArray()
                }
                : agent)
            .ToArray();

        var verified = await InvokeAsync<CapabilityCuratorVerifyResult>(
            verify,
            new CapabilityCuratorVerifyInput(
                harness.TargetAgentId,
                created.CapabilityId));

        Assert.Equal(CapabilityProofStatus.Verified, verified.ProofStatus);
        Assert.Equal(harness.TargetAgentId, verified.AgentId);
        Assert.Equal(created.CapabilityId, verified.CapabilityId);
    }

    [Fact]
    public async Task Capability_assignment_gates_tools_exactly_and_rejects_wrong_case()
    {
        var harness = CreateHarness([CapabilityCuratorAgentCapabilityKeys.EditorGet]);
        var tools = await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal(AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet, tool.Name);

        var wrongCase = CreateHarness([CapabilityCuratorAgentCapabilityKeys.EditorGet.ToUpperInvariant()]);
        Assert.Empty(await wrongCase.Provider.CreateToolsAsync(wrongCase.Context, CancellationToken.None));
        Assert.Empty(wrongCase.Provider.GetToolMetadata(wrongCase.Context));
    }

    [Fact]
    public async Task Search_and_editor_are_bounded_exact_and_return_typed_configuration()
    {
        var harness = CreateHarness();
        var tools = await CreateToolDictionaryAsync(harness);

        var search = await InvokeAsync<CapabilityCuratorCatalogSearchResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorCatalogSearch],
            new CapabilityCuratorCatalogSearchInput(
                text: "custom skill",
                kind: ModelCapabilityKind.Skill,
                tags: ["custom"],
                pageSize: 10));

        var item = Assert.Single(search.Items);
        Assert.Equal(harness.CustomCapabilityId, item.CapabilityId);
        Assert.Equal(1, search.TotalCount);
        Assert.Equal(1, search.TotalPages);

        var editor = await InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet],
            new CapabilityCuratorEditorGetInput(harness.CustomCapabilityId));

        Assert.Equal(harness.CustomCapabilityId, editor.CapabilityId);
        Assert.NotEmpty(editor.Fingerprint);
        Assert.NotNull(editor.Configuration.Skill);
        Assert.Equal(CapabilityCuratorSkillSource.Inline, editor.Configuration.Skill!.Source);
        Assert.Equal("Use the custom skill.", editor.Configuration.Skill.InlineInstructions);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet],
            new CapabilityCuratorEditorGetInput(Guid.NewGuid())));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CapabilityCuratorCatalogSearchInput(pageSize: 51));
    }

    [Fact]
    public async Task Save_creates_and_updates_custom_capabilities_with_fingerprint_guards()
    {
        var harness = CreateHarness();
        var tools = await CreateToolDictionaryAsync(harness);
        var create = CreateInlineSkillCandidate("new-custom-skill", "New custom skill", "Initial instructions.");

        var created = await InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            create);

        Assert.False(created.IsBuiltIn);
        Assert.Equal("Initial instructions.", created.Configuration.Skill!.InlineInstructions);
        Assert.Equal(1, harness.Workspace.SaveCapabilityCallCount);

        var updated = await InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            CreateInlineSkillCandidate(
                created.Key,
                created.Name,
                "Updated instructions.",
                created.CapabilityId,
                created.Fingerprint));
        Assert.Equal("Updated instructions.", updated.Configuration.Skill!.InlineInstructions);
        Assert.NotEqual(created.Fingerprint, updated.Fingerprint);
        Assert.Equal(created.Fingerprint, harness.Workspace.LastSavedCapabilityEditor!.ExpectedFingerprint);

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            CreateInlineSkillCandidate(
                created.Key,
                created.Name,
                "Stale overwrite.",
                created.CapabilityId,
                created.Fingerprint)));

        var builtIn = harness.Context.Capabilities.First(item => item.IsBuiltIn);
        var builtInEditor = await InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet],
            new CapabilityCuratorEditorGetInput(builtIn.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            CreateInlineSkillCandidate(
                "cannot-edit-built-in",
                "Cannot edit built in",
                "No.",
                builtIn.Id,
                builtInEditor.Fingerprint)));

        var nullKey = create with { Key = null! };
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            nullKey));
        Assert.DoesNotContain("NullReferenceException", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_tests_issue_exact_one_time_receipts_required_by_tool_and_mcp_saves()
    {
        var harness = CreateHarness();
        var tools = await CreateToolDictionaryAsync(harness);
        var toolCandidate = new CapabilityCuratorSaveInput(
            CapabilityId: null,
            ExpectedFingerprint: null,
            ModelCapabilityKind.Tool,
            "draft-process-tool",
            "Draft process tool",
            "Setup candidate",
            Tags: ["custom"],
            ToolConfiguration: new CapabilityCuratorToolConfigurationInput(
                CapabilityCuratorToolKind.ExternalProcess,
                "draft_process_tool",
                "external.draft-process-tool",
                ExternalProcess: new CapabilityCuratorExternalProcessToolInput(
                    "dotnet",
                    ["--info"],
                    AllowedExecutableNames: ["dotnet"])));
        var mcpCandidate = new CapabilityCuratorSaveInput(
            CapabilityId: null,
            ExpectedFingerprint: null,
            ModelCapabilityKind.McpServer,
            "draft-mcp",
            "Draft MCP",
            "Setup candidate",
            Tags: ["custom"],
            McpConfiguration: new CapabilityCuratorMcpConfigurationInput(
                CapabilityCuratorMcpTransport.Stdio,
                ServerName: "draft-mcp",
                Command: "npx",
                Arguments: ["-y", "example-mcp"],
                EnvironmentVariableBindings: new Dictionary<string, string>
                {
                    ["API_KEY"] = "EXAMPLE_API_KEY"
                },
                AllowedTools: ["ping"]));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            toolCandidate));

        var toolResult = await InvokeAsync<CapabilityCuratorToolSetupTestResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(toolCandidate, "{}", "tool-correlation"));
        var mcpResult = await InvokeAsync<CapabilityCuratorMcpSetupTestResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorMcpSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(mcpCandidate, CorrelationId: "mcp-correlation"));

        Assert.True(toolResult.SetupResult.IsSuccess);
        Assert.True(mcpResult.SetupResult.IsSuccess);
        Assert.NotNull(toolResult.Attestation);
        Assert.NotNull(mcpResult.Attestation);
        Assert.Equal(0, harness.Workspace.SaveCapabilityCallCount);
        var toolRequest = Assert.Single(harness.SetupFlow.ToolRequests);
        Assert.Equal("dotnet", toolRequest.Capability.EndpointOrPath);
        var mcpRequest = Assert.Single(harness.SetupFlow.McpRequests);
        Assert.Contains("EXAMPLE_API_KEY", mcpRequest.Capability.ConfigurationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"environmentVariables\"", mcpRequest.Capability.ConfigurationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("API_KEY_VALUE", mcpRequest.Capability.ConfigurationJson, StringComparison.Ordinal);

        var savedTool = await InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            toolCandidate with { SetupAttestationToken = toolResult.Attestation!.Token });
        Assert.Equal(ModelCapabilityKind.Tool, savedTool.Kind);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            toolCandidate with { SetupAttestationToken = toolResult.Attestation.Token }));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            mcpCandidate with
            {
                Name = "Changed after setup",
                SetupAttestationToken = mcpResult.Attestation!.Token
            }));
        var refreshedMcpResult = await InvokeAsync<CapabilityCuratorMcpSetupTestResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorMcpSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(mcpCandidate, CorrelationId: "mcp-correlation-2"));
        var savedMcp = await InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            mcpCandidate with { SetupAttestationToken = refreshedMcpResult.Attestation!.Token });
        Assert.Equal(ModelCapabilityKind.McpServer, savedMcp.Kind);
        Assert.Equal(2, harness.Workspace.SaveCapabilityCallCount);
    }

    [Fact]
    public async Task Setup_candidates_reject_inline_credentials_but_accept_binding_references()
    {
        var harness = CreateHarness();
        var tools = await CreateToolDictionaryAsync(harness);
        var validHttp = new CapabilityCuratorSaveInput(
            null,
            null,
            ModelCapabilityKind.Tool,
            "bound-http-tool",
            "Bound HTTP tool",
            "Uses a header binding.",
            ToolConfiguration: new CapabilityCuratorToolConfigurationInput(
                CapabilityCuratorToolKind.ExternalHttp,
                "bound_http_tool",
                "external.bound-http-tool",
                ExternalHttp: new CapabilityCuratorExternalHttpToolInput(
                    "POST",
                    "https://example.test/api",
                    new Dictionary<string, string> { ["Authorization"] = "BOUND_API_KEY" })));

        var validResult = await InvokeAsync<CapabilityCuratorToolSetupTestResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(validHttp));
        Assert.True(validResult.SetupResult.IsSuccess);
        Assert.NotNull(validResult.Attestation);

        harness.SetupFlow.ToolSetupSucceeds = false;
        var failedResult = await InvokeAsync<CapabilityCuratorToolSetupTestResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(validHttp));
        Assert.False(failedResult.SetupResult.IsSuccess);
        Assert.Null(failedResult.Attestation);
        harness.SetupFlow.ToolSetupSucceeds = true;

        foreach (var endpoint in new[]
                 {
                     "https://user:password@example.test/api",
                     "https://example.test/api?api_key=literal-value",
                     "https://example.test/api?access_token=literal-value"
                 })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => InvokeAsync<CapabilityCuratorToolSetupTestResult>(
                tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest],
                new CapabilityCuratorCapabilitySetupTestInput(validHttp with
                {
                    ToolConfiguration = validHttp.ToolConfiguration! with
                    {
                        ExternalHttp = validHttp.ToolConfiguration.ExternalHttp! with { Endpoint = endpoint }
                    }
                })));
        }

        foreach (var argument in new[]
                 {
                     "--token=literal-value",
                     "--api-key=literal-value",
                     "--password",
                     "Authorization: Bearer literal-value",
                     "Bearer literal-value"
                 })
        {
            var processCandidate = validHttp with
            {
                Key = $"unsafe-process-{Guid.NewGuid():N}",
                ToolConfiguration = new CapabilityCuratorToolConfigurationInput(
                    CapabilityCuratorToolKind.ExternalProcess,
                    "unsafe_process_tool",
                    "external.unsafe-process-tool",
                    ExternalProcess: new CapabilityCuratorExternalProcessToolInput(
                        "dotnet",
                        [argument],
                        AllowedExecutableNames: ["dotnet"]))
            };
            await Assert.ThrowsAsync<ArgumentException>(() => InvokeAsync<CapabilityCuratorToolSetupTestResult>(
                tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest],
                new CapabilityCuratorCapabilitySetupTestInput(processCandidate)));
        }

        var unsafeMcp = new CapabilityCuratorSaveInput(
            null,
            null,
            ModelCapabilityKind.McpServer,
            "unsafe-mcp",
            "Unsafe MCP",
            "Contains an inline credential.",
            McpConfiguration: new CapabilityCuratorMcpConfigurationInput(
                CapabilityCuratorMcpTransport.Stdio,
                ServerName: "unsafe-mcp",
                Command: "npx",
                Arguments: ["--api-key=literal-value"],
                AllowedTools: ["ping"]));
        await Assert.ThrowsAsync<ArgumentException>(() => InvokeAsync<CapabilityCuratorMcpSetupTestResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorMcpSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(unsafeMcp)));
    }

    [Fact]
    public void Setup_attestations_are_bound_to_session_kind_and_candidate_and_are_consumed_on_failure()
    {
        var store = new CapabilityCuratorSetupAttestationStore(TimeProvider.System);

        var wrongSession = store.Issue("session-a", CapabilityCuratorSetupKind.Tool, "fingerprint-a");
        Assert.Throws<UnauthorizedAccessException>(() => store.Consume(
            "session-b",
            CapabilityCuratorSetupKind.Tool,
            "fingerprint-a",
            wrongSession.Token));
        Assert.Throws<UnauthorizedAccessException>(() => store.Consume(
            "session-a",
            CapabilityCuratorSetupKind.Tool,
            "fingerprint-a",
            wrongSession.Token));

        var wrongKind = store.Issue("session-a", CapabilityCuratorSetupKind.Tool, "fingerprint-a");
        Assert.Throws<UnauthorizedAccessException>(() => store.Consume(
            "session-a",
            CapabilityCuratorSetupKind.Mcp,
            "fingerprint-a",
            wrongKind.Token));

        var wrongCandidate = store.Issue("session-a", CapabilityCuratorSetupKind.Tool, "fingerprint-a");
        Assert.Throws<UnauthorizedAccessException>(() => store.Consume(
            "session-a",
            CapabilityCuratorSetupKind.Tool,
            "fingerprint-b",
            wrongCandidate.Token));

        var exact = store.Issue("session-a", CapabilityCuratorSetupKind.Tool, "fingerprint-a");
        store.Consume(
            "session-a",
            CapabilityCuratorSetupKind.Tool,
            "fingerprint-a",
            exact.Token);
        Assert.Throws<UnauthorizedAccessException>(() => store.Consume(
            "session-a",
            CapabilityCuratorSetupKind.Tool,
            "fingerprint-a",
            exact.Token));

        var timeProvider = new AdjustableTimeProvider(
            DateTimeOffset.Parse("2026-07-21T12:00:00Z"));
        var expiringStore = new CapabilityCuratorSetupAttestationStore(timeProvider);
        var expiring = expiringStore.Issue(
            "session-a",
            CapabilityCuratorSetupKind.Tool,
            "fingerprint-a");
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        Assert.Throws<UnauthorizedAccessException>(() => expiringStore.Consume(
            "session-a",
            CapabilityCuratorSetupKind.Tool,
            "fingerprint-a",
            expiring.Token));
    }

    [Fact]
    public async Task Assignment_editor_update_and_verify_preserve_unrelated_assignments_and_protect_privilege()
    {
        var harness = CreateHarness();
        var tools = await CreateToolDictionaryAsync(harness);
        var assignmentEditor = await InvokeAsync<CapabilityCuratorAssignmentEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet],
            new CapabilityCuratorAssignmentEditorGetInput(harness.TargetAgentId));
        var unrelated = Assert.Single(assignmentEditor.SelectedCapabilityIds);

        var attached = await InvokeAsync<CapabilityCuratorAssignmentUpdateResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate],
            new CapabilityCuratorAssignmentUpdateInput(
                harness.TargetAgentId,
                harness.CustomCapabilityId,
                CapabilityCuratorAssignmentAction.Attach,
                assignmentEditor.UpdatedAtUtc));

        Assert.True(attached.IsAttached);
        Assert.Contains(unrelated, attached.SelectedCapabilityIds);
        Assert.Contains(harness.CustomCapabilityId, attached.SelectedCapabilityIds);

        var verified = await InvokeAsync<CapabilityCuratorVerifyResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify],
            new CapabilityCuratorVerifyInput(harness.TargetAgentId, harness.CustomCapabilityId));
        Assert.Equal(CapabilityProofStatus.Verified, verified.ProofStatus);

        var privileged = harness.Context.Capabilities.First();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorAssignmentUpdateResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate],
            new CapabilityCuratorAssignmentUpdateInput(
                harness.TargetAgentId,
                privileged.Id,
                CapabilityCuratorAssignmentAction.Attach,
                attached.UpdatedAtUtc)));
        Assert.Equal(5, ManagedAgentPrivilegedAgentIds.All.Count);
        foreach (var privilegedAgentId in ManagedAgentPrivilegedAgentIds.All)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorAssignmentEditorResult>(
                tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet],
                new CapabilityCuratorAssignmentEditorGetInput(privilegedAgentId)));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorAssignmentUpdateResult>(
                tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate],
                new CapabilityCuratorAssignmentUpdateInput(
                    privilegedAgentId,
                    harness.CustomCapabilityId,
                    CapabilityCuratorAssignmentAction.Attach,
                    harness.Context.Agent.UpdatedAtUtc)));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorVerifyResult>(
                tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify],
                new CapabilityCuratorVerifyInput(privilegedAgentId, harness.CustomCapabilityId)));
        }

        harness.Workspace.Agents = harness.Workspace.Agents
            .Select(agent => agent.Id == harness.TargetAgentId ? agent with { IsTemplate = true } : agent)
            .ToArray();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorAssignmentEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet],
            new CapabilityCuratorAssignmentEditorGetInput(harness.TargetAgentId)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => InvokeAsync<CapabilityCuratorVerifyResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify],
            new CapabilityCuratorVerifyInput(harness.TargetAgentId, harness.CustomCapabilityId)));
    }

    [Fact]
    public async Task Custom_capabilities_assigned_to_privileged_agents_cannot_be_updated_or_setup_tested()
    {
        var harness = CreateHarness();
        var tools = await CreateToolDictionaryAsync(harness);
        var current = await InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet],
            new CapabilityCuratorEditorGetInput(harness.CustomCapabilityId));
        harness.Workspace.Agents = harness.Workspace.Agents
            .Select(agent => agent.Id == CapabilityCuratorAgentIdentity.AgentId
                ? agent with
                {
                    Capabilities = agent.Capabilities
                        .Append(ToAssignment(harness.Workspace.Capabilities.Single(
                            capability => capability.Id == harness.CustomCapabilityId)))
                        .ToArray()
                }
                : agent)
            .ToArray();
        var update = CreateInlineSkillCandidate(
            current.Key,
            current.Name,
            "Blocked instructions.",
            current.CapabilityId,
            current.Fingerprint);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorEditorResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorSave],
            update));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync<CapabilityCuratorToolSetupTestResult>(
            tools[AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest],
            new CapabilityCuratorCapabilitySetupTestInput(update with
            {
                Kind = ModelCapabilityKind.Tool,
                SkillConfiguration = null,
                ToolConfiguration = new CapabilityCuratorToolConfigurationInput(
                    CapabilityCuratorToolKind.ExternalProcess,
                    "blocked_tool",
                    "external.blocked-tool",
                    ExternalProcess: new CapabilityCuratorExternalProcessToolInput(
                        "dotnet",
                        ["--info"],
                        AllowedExecutableNames: ["dotnet"]))
            })));
        Assert.Equal(0, harness.Workspace.SaveCapabilityCallCount);
        Assert.Empty(harness.SetupFlow.ToolRequests);
    }

    [Fact]
    public void Metadata_classifies_all_tools_and_redacts_capability_configuration_from_approval_audit()
    {
        var reads = new[]
        {
            AgentToolInvocationPolicyMetadata.CapabilityCuratorCatalogSearch,
            AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet,
            AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet
        };
        var mutations = new Dictionary<string, ToolCapabilitySideEffectKind>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorSave] = ToolCapabilitySideEffectKind.InternalStateMutation,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest] = ToolCapabilitySideEffectKind.ExternalAction,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorMcpSetupTest] = ToolCapabilitySideEffectKind.ExternalAction,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate] = ToolCapabilitySideEffectKind.InternalStateMutation,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify] = ToolCapabilitySideEffectKind.ExternalAction
        };

        Assert.Equal(8, reads.Length + mutations.Count);
        Assert.All(reads, toolName =>
        {
            Assert.True(ToolContractCatalog.IsKnownToolName(toolName));
            Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(toolName));
            Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
            Assert.True(ToolCapabilityRegistry.TryResolve(toolName, out var metadata));
            Assert.Equal(ToolCapabilitySideEffectKind.InternalDataRead, metadata.SideEffectKind);
        });
        Assert.All(mutations, expected =>
        {
            Assert.True(ToolContractCatalog.IsKnownToolName(expected.Key));
            Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(expected.Key));
            Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(expected.Key));
            Assert.True(ToolCapabilityRegistry.TryResolve(expected.Key, out var metadata));
            Assert.Equal(expected.Value, metadata.SideEffectKind);
        });

        var capabilityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string inlineInstructions = "Confidential inline instructions for Project Nightfall.";
        const string processArgument = "--customer-secret=Nightfall";
        const string endpoint = "https://private.example.test/nightfall";
        const string binding = "NIGHTFALL_API_KEY";
        const string setupAttestationToken = "one-time-nightfall-setup-attestation-token";
        const string jsonInput = "{\"privateCustomer\":\"Nightfall\"}";
        const string configurationContent = "Private custom configuration content";
        var request = new
        {
            candidate = new
            {
                capabilityId,
                key = "nightfall-capability",
                name = "Nightfall capability",
                endpointOrPath = endpoint,
                skillConfiguration = new { inlineInstructions },
                toolConfiguration = new
                {
                    externalProcess = new { arguments = new[] { processArgument } },
                    externalHttp = new
                    {
                        endpoint,
                        headerBindings = new Dictionary<string, string> { ["Authorization"] = binding }
                    }
                },
                otherConfiguration = new { content = configurationContent },
                setupAttestationToken
            },
            jsonInput
        };
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest,
        [
            new KeyValuePair<string, object?>("request", request)
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest,
            redacted);
        var audit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest,
            JsonSerializer.Serialize(new { request }));

        Assert.Contains(capabilityId.ToString("D"), signature, StringComparison.Ordinal);
        Assert.Contains("capability-curator-approval-redacted-v1", audit, StringComparison.Ordinal);
        foreach (var sensitive in new[]
                 {
                     inlineInstructions,
                     processArgument,
                     endpoint,
                     binding,
                     setupAttestationToken,
                     jsonInput,
                     configurationContent
                 })
        {
            Assert.DoesNotContain(sensitive, signature, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitive, audit, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Module_registers_authorization_and_provider_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        var authorization = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(CapabilityCuratorAgentRuntimeAuthorizationService));
        var provider = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
            descriptor.ImplementationType == typeof(CapabilityCuratorAgentRuntimeToolProvider));
        var attestationStore = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(CapabilityCuratorSetupAttestationStore));

        Assert.Equal(ServiceLifetime.Scoped, authorization.Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, provider.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, attestationStore.Lifetime);
    }

    private static CapabilityCuratorSaveInput CreateInlineSkillCandidate(
        string key,
        string name,
        string instructions,
        Guid? capabilityId = null,
        string? expectedFingerprint = null)
        => new(
            capabilityId,
            expectedFingerprint,
            ModelCapabilityKind.Skill,
            key,
            name,
            "Custom inline skill",
            ["custom"],
            SkillConfiguration: new CapabilityCuratorSkillConfigurationInput(
                CapabilityCuratorSkillSource.Inline,
                InlineName: name,
                InlineDescription: "Custom inline skill",
                InlineInstructions: instructions));

    private static async Task<IReadOnlyDictionary<string, AITool>> CreateToolDictionaryAsync(RuntimeHarness harness)
        => (await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);

    private static RuntimeHarness CreateHarness(
        IEnumerable<string>? capabilityKeys = null,
        bool useHrActor = false)
    {
        var now = DateTimeOffset.Parse("2026-07-21T12:00:00Z");
        var keys = (capabilityKeys ?? CapabilityCuratorAgentCapabilityKeys.ToolNameToCapabilityKey.Values).ToArray();
        var runtimeCapabilities = keys.Select(key => new CapabilityCatalogItem(
            Guid.NewGuid(),
            ModelCapabilityKind.Tool,
            key,
            key,
            string.Empty,
            string.Empty,
            string.Empty,
            CapabilityProofStatus.Verified,
            string.Empty,
            now,
            IsBuiltIn: true)).ToArray();
        var customCapabilityId = Guid.NewGuid();
        var customCapability = new CapabilityCatalogItem(
            customCapabilityId,
            ModelCapabilityKind.Skill,
            "custom-skill",
            "Custom skill",
            "A custom skill for search.",
            "inline://custom-skill",
            """
            {"skillSource":"inline","inlineSkill":{"name":"custom-skill","description":"Custom","instructions":"Use the custom skill."},"scriptApproval":true,"scriptExecution":{"approvalRequired":true,"trustLevel":"InlineSkill"}}
            """,
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            IsBuiltIn: false)
        {
            Tags = ["custom"]
        };
        var unrelatedCapability = new CapabilityCatalogItem(
            Guid.NewGuid(),
            ModelCapabilityKind.Skill,
            "unrelated-skill",
            "Unrelated skill",
            string.Empty,
            "inline://unrelated-skill",
            "{}",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            IsBuiltIn: false);
        var actorAssignments = runtimeCapabilities.Select(ToAssignment).ToArray();
        var providerProfileId = Guid.NewGuid();
        var actor = CreateAgent(
            useHrActor ? HrAgentIdentity.AgentId : CapabilityCuratorAgentIdentity.AgentId,
            useHrActor ? HrAgentIdentity.DefaultDisplayName : CapabilityCuratorAgentIdentity.DefaultDisplayName,
            useHrActor ? HrAgentIdentity.TemplateKey : CapabilityCuratorAgentIdentity.TemplateKey,
            providerProfileId,
            actorAssignments,
            now);
        var targetAgentId = Guid.NewGuid();
        var target = CreateAgent(
            targetAgentId,
            "Target agent",
            "target-agent",
            providerProfileId,
            [ToAssignment(unrelatedCapability)],
            now);
        var providerProfile = new ProviderProfile(
            providerProfileId,
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            actor.Model,
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CapabilityWorkspaceProxy>();
        var workspace = (CapabilityWorkspaceProxy)(object)workspaceService;
        workspace.Agents = [actor, target];
        workspace.Capabilities = runtimeCapabilities
            .Append(customCapability)
            .Append(unrelatedCapability)
            .ToArray();
        var setupFlow = new RecordingSetupFlowService();
        var runtimeProvider = new CapabilityCuratorAgentRuntimeToolProvider(
            workspaceService,
            setupFlow,
            new CapabilityCuratorAgentRuntimeAuthorizationService(workspaceService),
            new CapabilityCuratorSetupAttestationStore(TimeProvider.System));
        var context = new AgentRuntimeToolProviderContext(
            actor,
            providerProfile,
            workspace.Capabilities,
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "capability-curator-runtime-test",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
        return new RuntimeHarness(
            runtimeProvider,
            context,
            workspace,
            setupFlow,
            customCapabilityId,
            targetAgentId);
    }

    private static AgentDefinition CreateAgent(
        Guid id,
        string name,
        string templateKey,
        Guid providerProfileId,
        IReadOnlyList<AgentCapabilityAssignment> capabilities,
        DateTimeOffset now)
        => new(
            id,
            name,
            "Capability management",
            "Manages capabilities.",
            "Use assigned tools only.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "gpt-5.4-mini",
            AgentWorkloadKind.Management,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            templateKey,
            AgentPermissionsPolicy.Default with { CanUseTools = true },
            capabilities,
            [],
            now,
            now);

    private static AgentCapabilityAssignment ToAssignment(CapabilityCatalogItem capability)
        => new(
            capability.Id,
            capability.Key,
            capability.Kind,
            capability.ProofStatus,
            capability.LastVerifiedAtUtc,
            capability.ProofNotes);

    private static async Task<TResult> InvokeAsync<TResult>(AITool tool, object request)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(new AIFunctionArguments
        {
            ["request"] = request
        });
        return rawResult switch
        {
            TResult result => result,
            JsonElement element => JsonSerializer.Deserialize<TResult>(element.GetRawText(), JsonOptions)
                ?? throw new InvalidOperationException("Capability Curator runtime tool returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Unexpected Capability Curator runtime tool result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record RuntimeHarness(
        CapabilityCuratorAgentRuntimeToolProvider Provider,
        AgentRuntimeToolProviderContext Context,
        CapabilityWorkspaceProxy Workspace,
        RecordingSetupFlowService SetupFlow,
        Guid CustomCapabilityId,
        Guid TargetAgentId);

    private sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan value) => utcNow = utcNow.Add(value);
    }

    private sealed class RecordingSetupFlowService : IAgentCapabilitySetupFlowService
    {
        public List<CapabilityToolSetupTestRequest> ToolRequests { get; } = [];

        public List<CapabilityMcpSetupTestRequest> McpRequests { get; } = [];

        public bool ToolSetupSucceeds { get; set; } = true;

        public Task<CapabilitySetupTestResult> TestToolSetupAsync(
            CapabilityToolSetupTestRequest request,
            CancellationToken cancellationToken = default)
        {
            ToolRequests.Add(request);
            return Task.FromResult(new CapabilitySetupTestResult(
                ToolSetupSucceeds,
                new CapabilityIdentity(AccessCapabilityKind.Tool, CapabilityKey.Create(request.Capability.Key)),
                request.CorrelationId,
                []));
        }

        public Task<McpSetupTestResult> TestMcpSetupAsync(
            CapabilityMcpSetupTestRequest request,
            CancellationToken cancellationToken = default)
        {
            McpRequests.Add(request);
            var identity = new CapabilityIdentity(
                AccessCapabilityKind.McpServer,
                CapabilityKey.Create(request.Capability.Key));
            return Task.FromResult(new McpSetupTestResult(
                true,
                identity,
                McpServerKey.Create(request.Capability.Key),
                request.CorrelationId,
                [],
                [],
                [],
                CleanupCompleted: true));
        }

        public Task<CapabilityAccessPreviewResult> PreviewAccessAsync(
            CapabilityAccessPreviewRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private class CapabilityWorkspaceProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];

        public int SaveCapabilityCallCount { get; private set; }

        public CapabilityEditorModel? LastSavedCapabilityEditor { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult(ListAgents((bool)args![0]!)),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) => Task.FromResult(Capabilities),
                nameof(IAgentFrameworkWorkspaceService.GetCapabilityEditorAsync) =>
                    GetCapabilityEditorAsync((Guid?)args![0]),
                nameof(IAgentFrameworkWorkspaceService.SaveCapabilityAsync) =>
                    SaveCapabilityAsync((CapabilityEditorModel)args![0]!),
                nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync) =>
                    GetAgentEditorAsync((Guid?)args![0]),
                nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) =>
                    SaveAgentAsync((AgentEditorModel)args![0]!),
                nameof(IAgentFrameworkWorkspaceService.VerifyCapabilityAsync) =>
                    VerifyCapabilityAsync((Guid)args![0]!, (Guid)args[1]!),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this runtime-provider test.")
            };
        }

        private Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId)
        {
            var capability = Capabilities.SingleOrDefault(item => item.Id == capabilityId)
                ?? throw new InvalidOperationException("Capability was not found.");
            var editor = CapabilityEditorModel.FromDefinition(capability);
            editor.ExpectedFingerprint = CapabilityEditorConcurrency.ComputeFingerprint(editor);
            return Task.FromResult(editor);
        }

        private Task<Guid> SaveCapabilityAsync(CapabilityEditorModel editor)
        {
            SaveCapabilityCallCount++;
            var current = editor.Id.HasValue
                ? Capabilities.SingleOrDefault(item => item.Id == editor.Id.Value)
                : null;
            if (current is not null)
            {
                var actualFingerprint = CapabilityEditorConcurrency.ComputeFingerprint(
                    CapabilityEditorModel.FromDefinition(current));
                if (!string.Equals(actualFingerprint, editor.ExpectedFingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Capability concurrency conflict.");
                }
            }

            LastSavedCapabilityEditor = editor;

            var saved = new CapabilityCatalogItem(
                editor.Id ?? Guid.NewGuid(),
                editor.Kind,
                editor.Key,
                editor.Name,
                editor.Description,
                editor.EndpointOrPath,
                editor.ConfigurationJson,
                current?.ProofStatus ?? CapabilityProofStatus.NotRun,
                current?.ProofNotes ?? string.Empty,
                current?.LastVerifiedAtUtc,
                editor.IsBuiltIn)
            {
                Tags = editor.Tags.ToArray()
            };
            Capabilities = Capabilities
                .Where(item => item.Id != saved.Id)
                .Append(saved)
                .ToArray();
            return Task.FromResult(saved.Id);
        }

        private IReadOnlyList<AgentDefinition> ListAgents(bool includeTemplates)
            => includeTemplates
                ? Agents
                : Agents.Where(agent => !agent.IsTemplate).ToArray();

        private Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId)
        {
            var agent = Agents.SingleOrDefault(item => item.Id == agentId)
                ?? throw new InvalidOperationException("Agent was not found.");
            return Task.FromResult(AgentEditorModel.FromDefinition(agent));
        }

        private Task<Guid> SaveAgentAsync(AgentEditorModel editor)
        {
            var current = Agents.Single(item => item.Id == editor.Id);
            if (current.UpdatedAtUtc != editor.ExpectedUpdatedAtUtc)
            {
                throw new InvalidOperationException("Agent concurrency conflict.");
            }

            var assignments = editor.SelectedCapabilityIds
                .Select(id => ToAssignment(Capabilities.Single(item => item.Id == id)))
                .ToArray();
            var updated = current with
            {
                Capabilities = assignments,
                UpdatedAtUtc = current.UpdatedAtUtc.AddMinutes(1)
            };
            Agents = Agents.Select(agent => agent.Id == updated.Id ? updated : agent).ToArray();
            return Task.FromResult(updated.Id);
        }

        private Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId)
        {
            var checkedAtUtc = DateTimeOffset.Parse("2026-07-21T13:00:00Z");
            Capabilities = Capabilities.Select(capability => capability.Id == capabilityId
                ? capability with
                {
                    ProofStatus = CapabilityProofStatus.Verified,
                    ProofNotes = "Verified by test.",
                    LastVerifiedAtUtc = checkedAtUtc
                }
                : capability).ToArray();
            Agents = Agents.Select(agent => agent.Id == agentId
                ? agent with
                {
                    Capabilities = agent.Capabilities.Select(assignment => assignment.CapabilityId == capabilityId
                        ? assignment with
                        {
                            ProofStatus = CapabilityProofStatus.Verified,
                            ProofNotes = "Verified by test.",
                            LastVerifiedAtUtc = checkedAtUtc
                        }
                        : assignment).ToArray()
                }
                : agent).ToArray();
            return Task.CompletedTask;
        }
    }
}
