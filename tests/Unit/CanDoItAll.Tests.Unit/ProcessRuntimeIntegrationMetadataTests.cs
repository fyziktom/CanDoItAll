using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Capabilities = CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeIntegrationMetadataTests
{
    [Fact]
    public void Process_execution_metadata_maps_project_launch_context_to_trusted_scope()
    {
        var projectId = Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9");
        var assignment = CreateAssignment(projectId);

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);
        using var metadataDocument = System.Text.Json.JsonDocument.Parse(metadataJson);
        var metadataRoot = metadataDocument.RootElement;

        var scope = ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);
        var launchAgent = ExecutionInvocationMetadata.ResolveProjectStructureLaunchAgent(run);
        var allowedOperations = ExecutionInvocationMetadata.ResolveProcessStepAllowedOperations(run);
        var writableAliases = ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(run);

        Assert.NotNull(scope);
        Assert.Equal(WorkspaceScopeKind.Project, scope!.Kind);
        Assert.Equal(projectId.ToString("D"), scope.Key);
        Assert.NotNull(launchAgent);
        Assert.Equal("codex-process-e2e", launchAgent!.AgentId);
        Assert.Equal("Codex Process E2E", launchAgent.AgentName);
        Assert.Equal("LUCYSPOWER", launchAgent.MachineName);
        Assert.Equal(@"C:\programovani\dotnet\output", launchAgent.RepositoryRoot);
        Assert.Equal("main", launchAgent.BranchName);
        Assert.Equal("codex-process-e2e-session", launchAgent.SessionId);
        Assert.Contains(ProcessOperationContractNames.ReadProjectStructure, allowedOperations);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, allowedOperations);
        Assert.Contains(ResolveTestProductAlias(assignment), writableAliases);
        Assert.Equal(
            AgentFinalizerPolicies.RequiredFinalizerModeValue,
            metadataRoot.GetProperty(AgentFinalizerPolicies.FinalizerModeMetadataKey).GetString());
        Assert.Equal(
            ExecutionInvocationMetadata.DefaultGovernedRepairAttempts,
            metadataRoot.GetProperty(ExecutionInvocationMetadata.MaxStructuredOutputRepairAttemptsMetadataKey).GetInt32());
        Assert.Equal(1, ExecutionInvocationMetadata.DefaultGovernedRepairAttempts);
        Assert.True(metadataRoot.GetProperty(ExecutionInvocationMetadata.RequireStructuredOutputValidationMetadataKey).GetBoolean());

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            var auditScope = Assert.IsType<WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState>(
                WorkspaceExecutionAuditContext.Current);
            Assert.NotNull(auditScope.ContextWorkspaceScope);
            Assert.Equal(WorkspaceScopeKind.Project, auditScope.ContextWorkspaceScope!.Kind);
            Assert.Equal(projectId.ToString("D"), auditScope.ContextWorkspaceScope.Key);
        }
    }

    [Fact]
    public void Process_execution_metadata_maps_project_structure_process_node_context()
    {
        var projectId = Guid.Parse("3324868f-66e2-478a-bb8f-14f32a5db1e9");
        var parentRunNodeId = "process-run:154fb190-7fad-491a-93e9-52d6bed977f5";
        var childRunNodeId = "process-run:107fffa9-d72e-4f4e-b838-5b02ad24c5a7";
        var assignment = CreateAssignment(
            projectId,
            launchVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProjectId"] = projectId.ToString("D"),
                ["CurrentProcessRunNodeId"] = childRunNodeId,
                ["ProcessRunNodeId"] = parentRunNodeId,
                ["ParentProcessRunNodeId"] = parentRunNodeId,
                ["TargetProcessRunNodeId"] = parentRunNodeId
            });

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        var context = ExecutionInvocationMetadata.ResolveProjectStructureProcessNodeContext(run);

        Assert.NotNull(context);
        Assert.Equal(childRunNodeId, context!.CurrentProcessRunNodeId);
        Assert.Equal(parentRunNodeId, context.ProcessRunNodeId);
        Assert.Equal(parentRunNodeId, context.ParentProcessRunNodeId);
        Assert.Equal(parentRunNodeId, context.TargetProcessRunNodeId);
        Assert.Equal(parentRunNodeId, context.PreferredWritebackNodeId);

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            var auditScope = Assert.IsType<WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState>(
                WorkspaceExecutionAuditContext.Current);
            Assert.Equal(parentRunNodeId, auditScope.ProjectStructureProcessNodeContext?.PreferredWritebackNodeId);
        }
    }

    [Fact]
    public void Process_execution_metadata_disables_browser_tools_without_runtime_proof_operation()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        Assert.False(ExecutionInvocationMetadata.ResolveProcessBrowserToolsAllowed(run));
    }

    [Fact]
    public void Process_execution_metadata_allows_browser_tools_for_runtime_proof_operation()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof
            ],
            ProcessOperationContractNames.ExternalProductTargetReadOnly);

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        Assert.True(ExecutionInvocationMetadata.ResolveProcessBrowserToolsAllowed(run));
    }

    [Fact]
    public void Governed_process_without_browser_metadata_fails_closed()
    {
        var run = CreateTrustedProcessRun("{}");

        Assert.False(ExecutionInvocationMetadata.ResolveProcessBrowserToolsAllowed(run));
    }

    [Fact]
    public void Governed_process_with_malformed_browser_metadata_fails_closed()
    {
        var run = CreateTrustedProcessRun("""{"agentProcessBrowserToolsAllowed":"not-a-boolean"}""");

        Assert.False(ExecutionInvocationMetadata.ResolveProcessBrowserToolsAllowed(run));
    }

    [Fact]
    public void Process_execution_metadata_does_not_infer_browser_tools_from_screenshot_step_key()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction
            ],
            ProcessOperationContractNames.ExternalActionControlled,
            stepKey: "capture-ui-screenshots-after-repair");

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);
        var allowedOperations = ExecutionInvocationMetadata.ResolveProcessStepAllowedOperations(run);

        Assert.False(ExecutionInvocationMetadata.ResolveProcessBrowserToolsAllowed(run));
        Assert.DoesNotContain(ProcessOperationContractNames.LaunchRuntime, allowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.CaptureRuntimeProof, allowedOperations);
    }

    [Fact]
    public void Process_execution_metadata_grants_read_only_product_alias_for_external_action_controller()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalActionControlled,
            stepKey: "prepare-solution-skeleton");

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        var writableAliases = ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(run);
        var readOnlyAliases = ExecutionInvocationMetadata.ResolveReadOnlyExternalTargetAliases(run);

        Assert.Empty(writableAliases);
        Assert.Contains(ResolveTestProductAlias(assignment), readOnlyAliases);
        Assert.False(ExecutionInvocationMetadata.ResolveProcessAllowsProductMutation(run));
    }

    [Fact]
    public void Process_execution_metadata_marks_configured_mutation_before_handoff_step()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.MutateProductTarget,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            stepKey: "code-change");
        assignment = assignment with
        {
            LaunchVariables = new Dictionary<string, string>(assignment.LaunchVariables, StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProductMutationBeforeManagedOutputRequiredStepKeys] =
                    JsonSerializer.Serialize(new[] { "code-change", "feature-repair" }),
                [ProcessRuntimeLaunchVariables.ProductMutationToolNames] =
                    JsonSerializer.Serialize(new[] { "workspace_write_file", "workspace_dotnet_new" }),
                [ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys] =
                    JsonSerializer.Serialize(new[] { "product-repair-applied" })
            }
        };

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        Assert.True(ExecutionInvocationMetadata.ResolveProcessAllowsProductMutation(run));
        Assert.True(ExecutionInvocationMetadata.ResolveProcessRequiresProductMutationBeforeManagedOutput(run));
        Assert.Equal(
            ["workspace_dotnet_new", "workspace_write_file"],
            ExecutionInvocationMetadata.ResolveProcessProductMutationToolNames(run));
        Assert.Equal(
            ["product-repair-applied"],
            ExecutionInvocationMetadata.ResolveProcessProductMutationRequiredBranchOutcomeKeys(run));

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            var auditScope = Assert.IsType<WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState>(
                WorkspaceExecutionAuditContext.Current);
            Assert.Equal(["product-repair-applied"], auditScope.ProcessProductMutationRequiredBranchOutcomeKeys);
        }
    }

    [Fact]
    public void Process_execution_metadata_rejects_contribution_key_owned_by_late_generic_metadata()
    {
        var assignment = CreateAssignment(Guid.NewGuid());
        assignment = assignment with
        {
            LaunchVariables = new Dictionary<string, string>(assignment.LaunchVariables, StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductMutationToolNames] =
                    JsonSerializer.Serialize(new[] { "workspace_write_file" })
            }
        };
        var composer = new ProcessExecutionMetadataComposer(
        [
            new FixedMetadataContribution(
                "test.generic-key-collision",
                100,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [ExecutionInvocationMetadata.ProcessProductMutationToolNamesMetadataKey] =
                        new[] { "workspace_read_file" }
                })
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => composer.Compose(assignment));

        Assert.Contains(
            ExecutionInvocationMetadata.ProcessProductMutationToolNamesMetadataKey,
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("conflicts with a generic process metadata key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_execution_metadata_round_trips_dispatch_claim_identity()
    {
        var assignment = CreateAssignment(Guid.NewGuid());
        var dispatchClaimIdentity = new ProcessDispatchClaimIdentity(Guid.NewGuid());
        var metadataJson = new ProcessExecutionMetadataComposer(
        [
            new BrowserExecutionMetadataContribution()
        ]).ComposeClaimedExecution(assignment, dispatchClaimIdentity);
        var executionRun = CreateTrustedProcessRun(metadataJson);

        Assert.True(ProcessDispatchClaimExecutionMetadata.Matches(
            executionRun,
            dispatchClaimIdentity));
        Assert.False(ProcessDispatchClaimExecutionMetadata.Matches(
            executionRun,
            new ProcessDispatchClaimIdentity(Guid.NewGuid())));
    }

    [Fact]
    public void Agent_runtime_options_include_process_context_intent_from_trusted_step_metadata()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.RunValidation
            ],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            stepKey: "targeted-validation");
        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson) with
        {
            SourceId = assignment.StepKey,
            ProcessRunId = assignment.RunId.Value.ToString("D"),
            ProcessStepId = assignment.StepInstanceId.Value.ToString("D")
        };
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "CreateRuntimeContextIntent",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateRuntimeContextIntent method was not found.");

        var contextIntent = Assert.IsType<AgentRuntimeContextIntent>(method.Invoke(
            null,
            [run, ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run)]));

        Assert.True(contextIntent.IsGovernedProcessStep);
        Assert.Equal("process-step", contextIntent.SourceKind);
        Assert.Equal("targeted-validation", contextIntent.SourceId);
        Assert.Equal(assignment.RunId.Value.ToString("D"), contextIntent.ProcessRunId);
        Assert.Equal(assignment.StepInstanceId.Value.ToString("D"), contextIntent.ProcessStepId);
        Assert.Equal(ProcessOperationContractNames.ExternalProductTargetReadOnly, contextIntent.TargetScope);
        Assert.False(contextIntent.AllowsProductMutation);
        Assert.True(contextIntent.RuntimeToolProvidersEnabled);
        Assert.True(contextIntent.WorkspaceToolsEnabled);
        Assert.True(contextIntent.ToolCapabilitiesEnabled);
        Assert.Contains(ProcessOperationContractNames.ReadProcessContext, contextIntent.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.RunValidation, contextIntent.AllowedOperations);
    }

    [Fact]
    public void Agent_runtime_options_preserve_disabled_runtime_tool_provider_metadata()
    {
        var metadataJson = ExecutionInvocationMetadata.ApplyToolCapabilitiesEnabled(
            ExecutionInvocationMetadata.ApplyWorkspaceToolsEnabled(
                ExecutionInvocationMetadata.ApplyRuntimeToolProvidersEnabled("{}", enabled: false),
                enabled: false),
            enabled: false);
        var run = CreateTrustedProcessRun(metadataJson);
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "CreateRuntimeContextIntent",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateRuntimeContextIntent method was not found.");

        var contextIntent = Assert.IsType<AgentRuntimeContextIntent>(method.Invoke(
            null,
            [run, ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run)]));

        Assert.False(contextIntent.RuntimeToolProvidersEnabled);
        Assert.False(contextIntent.WorkspaceToolsEnabled);
        Assert.False(contextIntent.ToolCapabilitiesEnabled);
    }

    [Fact]
    public void Agent_chat_run_options_preserve_default_capabilities_and_can_request_tool_free_execution()
    {
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "BuildChatMetadataJson",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildChatMetadataJson method was not found.");
        const string baseMetadataJson = "{\"marker\":\"preserved\"}";
        var operationId = AgentExecutionOperationId.New();

        var defaultMetadataJson = Assert.IsType<string>(method.Invoke(
            null,
            [new AgentChatRunOptions(operationId), baseMetadataJson]));
        var toolFreeMetadataJson = Assert.IsType<string>(method.Invoke(
            null,
            [
                new AgentChatRunOptions(operationId)
                {
                    ToolCapabilitiesEnabled = false
                },
                baseMetadataJson
            ]));

        using var defaultDocument = JsonDocument.Parse(defaultMetadataJson);
        using var toolFreeDocument = JsonDocument.Parse(toolFreeMetadataJson);
        Assert.Equal("preserved", defaultDocument.RootElement.GetProperty("marker").GetString());
        Assert.False(defaultDocument.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.ToolCapabilitiesEnabledMetadataKey,
            out _));
        Assert.Equal("preserved", toolFreeDocument.RootElement.GetProperty("marker").GetString());
        Assert.False(toolFreeDocument.RootElement
            .GetProperty(ExecutionInvocationMetadata.ToolCapabilitiesEnabledMetadataKey)
            .GetBoolean());
        Assert.True(ExecutionInvocationMetadata.ResolveToolCapabilitiesEnabled(
            CreateTrustedProcessRun(defaultMetadataJson)));
        Assert.False(ExecutionInvocationMetadata.ResolveToolCapabilitiesEnabled(
            CreateTrustedProcessRun(toolFreeMetadataJson)));
    }

    [Fact]
    public void Process_execution_metadata_authorizes_only_exact_inherited_parent_artifact_reads()
    {
        var projectId = Guid.NewGuid();
        var parentRunId = Guid.NewGuid();
        var parentRefs = new[]
        {
            $"artifacts/process-runs/{parentRunId:D}/steps/implementation.md",
            $"artifacts/process-runs/{parentRunId:D}/steps/qa-validation.md"
        };
        var assignment = CreateAssignment(
            projectId,
            launchVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProjectId"] = projectId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs] =
                    ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactRefs(parentRefs)
            });

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        Assert.Equal(parentRefs, ExecutionInvocationMetadata.ResolveAllowedManagedArtifactReadRefs(run));

        using (WorkspaceExecutionAuditContext.BeginScope(run))
        {
            var auditScope = Assert.IsType<WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState>(
                WorkspaceExecutionAuditContext.Current);
            Assert.Equal(parentRefs, auditScope.AllowedManagedArtifactReadRefs);
        }
    }

    [Fact]
    public void Process_execution_metadata_authorizes_primary_artifact_read_for_automatic_diagnostic_recovery()
    {
        var assignment = CreateAssignment(Guid.NewGuid()) with
        {
            Prompt = $"""
                {ProcessRuntimeRecoveryInstructionHeadings.RuntimeDiagnosticRecovery}:
                Repair the rejected managed-artifact completion gate.
                """
        };
        var primaryRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/steps/{assignment.StepKey}.md";

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        Assert.Equal(
            [primaryRef],
            ExecutionInvocationMetadata.ResolveAllowedManagedArtifactReadRefs(run));
    }

    [Fact]
    public void Process_execution_metadata_carries_scoped_capability_policy_to_runtime_intent()
    {
        var assignment = CreateAssignment(Guid.NewGuid()) with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                Directives =
                [
                    new ProcessCapabilityScopeDirective
                    {
                        Kind = ProcessCapabilityScopeDirectiveKind.AllowOnly,
                        Target = new ProcessCapabilityScopeTarget
                        {
                            Kind = ProcessCapabilityScopeTargetKind.RuntimeToolProviderKey,
                            Value = "management.provider"
                        },
                        Reason = "Management-only step."
                    },
                    new ProcessCapabilityScopeDirective
                    {
                        Kind = ProcessCapabilityScopeDirectiveKind.Deny,
                        Target = new ProcessCapabilityScopeTarget
                        {
                            Kind = ProcessCapabilityScopeTargetKind.CapabilityTag,
                            Value = "development"
                        },
                        Reason = "Development capabilities are suppressed for this step."
                    },
                    new ProcessCapabilityScopeDirective
                    {
                        Kind = ProcessCapabilityScopeDirectiveKind.Require,
                        Target = new ProcessCapabilityScopeTarget
                        {
                            Kind = ProcessCapabilityScopeTargetKind.CapabilityIdentity,
                            Value = nameof(Capabilities.CapabilityKind.Tool),
                            SecondaryValue = "workspace-read-file"
                        },
                        Reason = "The management step still needs read-only workspace evidence."
                    }
                ],
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "qa-browser-screenshot",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "browser_take_screenshot",
                        Activation = ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool,
                        Reason = "QA proof requires current-run screenshot evidence when UI proof is active."
                    }
                ]
            }
        };
        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson) with
        {
            SourceId = assignment.StepKey,
            ProcessRunId = assignment.RunId.Value.ToString("D"),
            ProcessStepId = assignment.StepInstanceId.Value.ToString("D")
        };
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "CreateRuntimeContextIntent",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateRuntimeContextIntent method was not found.");

        var contextIntent = Assert.IsType<AgentRuntimeContextIntent>(method.Invoke(
            null,
            [run, ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run)]));

        var resolvedScope = ExecutionInvocationMetadata.ResolveRuntimeCapabilityScopeOverride(run);
        Assert.NotNull(resolvedScope);
        Assert.NotNull(contextIntent.CapabilityScopeOverride);
        Assert.Equal(resolvedScope!.Policies.Count, contextIntent.CapabilityScopeOverride!.Policies.Count);
        Assert.Equal(resolvedScope.RequiredCapabilities.Count, contextIntent.CapabilityScopeOverride.RequiredCapabilities.Count);
        Assert.Equal(resolvedScope.RequiredReceipts.Count, contextIntent.CapabilityScopeOverride.RequiredReceipts.Count);
        var policy = Assert.Single(resolvedScope.Policies);
        Assert.Equal(Capabilities.CapabilityAccessDefaultEffect.DenyAll, policy.DefaultEffect);
        Assert.Contains(policy.Rules, rule =>
            rule.Effect == Capabilities.CapabilityAccessEffect.Allow &&
            rule.Selector.Kind == Capabilities.CapabilitySelectorKind.Tag &&
            rule.Selector.Tag == Capabilities.RuntimeToolProviderCapabilityTags.CreateProviderKeyTag("management.provider"));
        Assert.Contains(policy.Rules, rule =>
            rule.Effect == Capabilities.CapabilityAccessEffect.Deny &&
            rule.Selector.Kind == Capabilities.CapabilitySelectorKind.Tag &&
            rule.Selector.Tag == Capabilities.CapabilityTag.Create("development"));
        Assert.Contains(policy.Rules, rule =>
            rule.Effect == Capabilities.CapabilityAccessEffect.Require &&
            rule.Selector.Kind == Capabilities.CapabilitySelectorKind.CapabilityKey);
        var required = Assert.Single(resolvedScope.RequiredCapabilities);
        Assert.Equal(Capabilities.CapabilityKind.Tool, required.Kind);
        Assert.Equal(Capabilities.CapabilityKey.Create("workspace-read-file"), required.Key);
        var requiredReceipt = Assert.Single(resolvedScope.RequiredReceipts);
        Assert.Equal("qa-browser-screenshot", requiredReceipt.Key);
        Assert.Equal("browser_take_screenshot", requiredReceipt.ToolName);
        Assert.Equal(AgentRuntimeRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool, requiredReceipt.Activation);
    }

    [Fact]
    public void Process_runtime_capability_scope_metadata_fails_closed_when_malformed()
    {
        var metadataJson = $$"""
            {
              "{{ExecutionInvocationMetadata.RuntimeCapabilityScopeOverrideMetadataKey}}": {
                "policies": "invalid-policy-list",
                "requiredCapabilities": []
              }
            }
            """;
        var run = CreateTrustedProcessRun(metadataJson);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExecutionInvocationMetadata.ResolveRuntimeCapabilityScopeOverride(run));

        Assert.Contains("capability scope metadata is malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(128_000, 64, 32_000)]
    [InlineData(55_000, 42, 12_000)]
    public void Runtime_usage_mapper_warns_when_context_manifest_exceeds_budget(
        int estimatedInputTokens,
        int toolCount,
        int toolSchemaEstimatedTokens)
    {
        var run = CreateTrustedProcessRun("{}");
        var usageObservation = new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "OpenAI default",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-test",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 10,
            CachedInputTokens: 0,
            OutputTokens: 2,
            ReasoningTokens: 0,
            TotalTokens: 12,
            ToolCallCount: 0)
        {
            DiagnosticsJson = $$"""
                {
                  "contextAssemblyManifest": {
                    "totals": {
                      "estimatedInputTokens": {{estimatedInputTokens}},
                      "inputMessageCount": 3,
                      "toolCount": {{toolCount}},
                      "toolSchemaEstimatedTokens": {{toolSchemaEstimatedTokens}}
                    },
                    "sources": [
                      { "category": "workspace-tools" },
                      { "category": "skills" }
                    ]
                  }
                }
                """
        };
        var readerType = typeof(ProcessesModuleServiceCollectionExtensions)
            .Assembly
            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessRuntimeUsageTelemetryReader")
            ?? throw new InvalidOperationException("Usage telemetry reader type was not found.");
        var method = readerType.GetMethod("MapUsageObservation", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("MapUsageObservation method was not found.");

        var observation = Assert.IsType<ProcessRuntimeUsageObservation>(method.Invoke(
            null,
            [usageObservation, run, ProcessRunId.New(), Array.Empty<ProviderProfile>()]));

        Assert.Equal(estimatedInputTokens, observation.ContextEstimatedInputTokens);
        Assert.Equal(toolCount, observation.ContextToolCount);
        Assert.Equal(toolSchemaEstimatedTokens, observation.ContextToolSchemaEstimatedTokens);
        Assert.Equal(2, observation.ContextSourceCount);
        Assert.True(observation.ContextBudgetExceeded);
        Assert.Contains($"EstimatedInputTokens={estimatedInputTokens}", observation.ContextBudgetWarning, StringComparison.Ordinal);
        Assert.Contains($"ToolCount={toolCount}", observation.ContextBudgetWarning, StringComparison.Ordinal);
        Assert.Contains($"ToolSchemaEstimatedTokens={toolSchemaEstimatedTokens}", observation.ContextBudgetWarning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_usage_reader_batches_execution_run_lookup_for_multiple_process_runs()
    {
        var now = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
        var firstProcessRunId = ProcessRunId.New();
        var secondProcessRunId = ProcessRunId.New();
        var unrelatedProcessRunId = ProcessRunId.New();
        var firstRun = CreateUsageExecutionRun(firstProcessRunId, now.AddMinutes(-3));
        var secondRun = CreateUsageExecutionRun(secondProcessRunId, now.AddMinutes(-2));
        var unrelatedRun = CreateUsageExecutionRun(unrelatedProcessRunId, now.AddMinutes(-1));
        var usageObservation = new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: now.AddMinutes(-2),
            ProviderName: "OpenAI default",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-test",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 100,
            CachedInputTokens: 10,
            OutputTokens: 50,
            ReasoningTokens: 0,
            TotalTokens: 150,
            ToolCallCount: 1)
        {
            ExecutionRunId = firstRun.Id,
            ProcessRunId = firstProcessRunId.ToString(),
            CalculatedCostUsd = 0.42m
        };
        var workspace = new UsageTelemetryWorkspaceService(
            [firstRun, secondRun, unrelatedRun],
            new Dictionary<Guid, ExecutionRunDetail>
            {
                [firstRun.Id] = CreateExecutionRunDetail(firstRun, [usageObservation]),
                [secondRun.Id] = CreateExecutionRunDetail(secondRun, []),
                [unrelatedRun.Id] = CreateExecutionRunDetail(unrelatedRun, [])
            });
        var reader = new AgentFrameworkProcessRuntimeUsageTelemetryReader(
            new WorkspaceBackedAgentReferenceDataProvider(workspace, new AgentReferenceDataCache()),
            workspace);

        var observations = await reader.ListAsync(
            new ProcessRuntimeUsageTelemetryQuery(
                [firstProcessRunId, secondProcessRunId],
                now.AddHours(-1),
                now.AddHours(1),
                TakePerRun: 25));

        var query = Assert.Single(workspace.ExecutionRunQueries);
        Assert.Null(query.ProcessRunId);
        Assert.Equal(
            new HashSet<string>(
                [firstProcessRunId.ToString(), secondProcessRunId.ToString()],
                StringComparer.OrdinalIgnoreCase),
            query.ProcessRunIds.ToHashSet(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(51, query.Take);
        Assert.Equal(2, workspace.ExecutionRunDetailRequestCount);
        var observation = Assert.Single(observations);
        Assert.Equal(firstProcessRunId, observation.RunId);
        Assert.Equal(0.42m, observation.ActualCostUsd);
    }

    [Fact]
    public async Task Execution_observation_reader_batches_headers_and_skips_details_for_summary_shape()
    {
        var now = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        var firstProcessRunId = ProcessRunId.New();
        var secondProcessRunId = ProcessRunId.New();
        var unrelatedProcessRunId = ProcessRunId.New();
        var firstStepId = ProcessStepInstanceId.New();
        var secondStepId = ProcessStepInstanceId.New();
        var firstRun = CreateUsageExecutionRun(firstProcessRunId, now.AddMinutes(-2)) with
        {
            ProcessStepId = firstStepId.ToString(),
            ResultSummary = "First header result"
        };
        var secondRun = CreateUsageExecutionRun(secondProcessRunId, now.AddMinutes(-1)) with
        {
            ProcessStepId = secondStepId.ToString(),
            ResultSummary = "Second header result"
        };
        var unrelatedRun = CreateUsageExecutionRun(unrelatedProcessRunId, now);
        var workspace = new UsageTelemetryWorkspaceService(
            [firstRun, secondRun, unrelatedRun],
            new Dictionary<Guid, ExecutionRunDetail>());
        var reader = new AgentFrameworkProcessExecutionObservationReader(
            new EmptyAgentReferenceDataProvider(now),
            workspace);

        var read = await reader.ReadAsync(
            new ProcessExecutionObservationQuery(
                [firstProcessRunId, secondProcessRunId],
                now.AddHours(-1),
                now.AddHours(1),
                TakePerRun: 2)
            {
                StepInstanceIds = [firstStepId, secondStepId],
                DetailLevel = ProcessExecutionObservationDetailLevel.Summary
            });
        var observations = read.Items;

        Assert.True(read.IsComplete);
        var executionRunQuery = Assert.Single(workspace.ExecutionRunQueries);
        Assert.Null(executionRunQuery.ProcessRunId);
        Assert.Null(executionRunQuery.ProcessStepId);
        Assert.Equal(
            [firstProcessRunId.ToString(), secondProcessRunId.ToString()],
            executionRunQuery.ProcessRunIds);
        Assert.Equal(
            [firstStepId.ToString(), secondStepId.ToString()],
            executionRunQuery.ProcessStepIds);
        Assert.Equal(12, executionRunQuery.Take);
        Assert.Equal(0, workspace.ExecutionRunDetailRequestCount);
        Assert.Equal(2, observations.Count);
        var firstObservation = Assert.Single(observations, observation => observation.RunId == firstProcessRunId);
        Assert.Equal(firstRun.AgentId, firstObservation.AgentId);
        Assert.Equal(firstRun.StartedAtUtc, firstObservation.StartedAtUtc);
        Assert.Equal(firstRun.CompletedAtUtc, firstObservation.CompletedAtUtc);
        Assert.Equal("First header result", firstObservation.ResultSummary);
        Assert.Empty(firstObservation.RecentActivities);
        Assert.Empty(firstObservation.RecentTools);
        Assert.Empty(firstObservation.Artifacts);
    }

    [Fact]
    public async Task Execution_observation_reader_preserves_full_detail_loading_by_default()
    {
        var now = new DateTimeOffset(2026, 7, 24, 13, 0, 0, TimeSpan.Zero);
        var processRunId = ProcessRunId.New();
        var executionRun = CreateUsageExecutionRun(processRunId, now) with
        {
            ResultSummary = "Header result"
        };
        var detailRun = executionRun with
        {
            ResultSummary = "Detailed result"
        };
        var workspace = new UsageTelemetryWorkspaceService(
            [executionRun],
            new Dictionary<Guid, ExecutionRunDetail>
            {
                [executionRun.Id] = CreateExecutionRunDetail(detailRun, [])
            });
        var reader = new AgentFrameworkProcessExecutionObservationReader(
            new EmptyAgentReferenceDataProvider(now),
            workspace);

        var observations = await reader.ListAsync(
            new ProcessExecutionObservationQuery(
                [processRunId],
                now.AddHours(-1),
                now.AddHours(1),
                TakePerRun: 1));

        Assert.Single(workspace.ExecutionRunQueries);
        Assert.Equal(1, workspace.ExecutionRunDetailRequestCount);
        var observation = Assert.Single(observations);
        Assert.Equal("Detailed result", observation.ResultSummary);
    }

    [Fact]
    public async Task Execution_observation_reader_reports_per_step_cap_boundary_as_incomplete()
    {
        var now = new DateTimeOffset(2026, 7, 24, 13, 30, 0, TimeSpan.Zero);
        var processRunId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        var olderRun = CreateUsageExecutionRun(processRunId, now.AddMinutes(-2)) with
        {
            ProcessStepId = stepId.ToString()
        };
        var newerRun = CreateUsageExecutionRun(processRunId, now.AddMinutes(-1)) with
        {
            ProcessStepId = stepId.ToString()
        };
        var workspace = new UsageTelemetryWorkspaceService(
            [olderRun, newerRun],
            new Dictionary<Guid, ExecutionRunDetail>());
        var reader = new AgentFrameworkProcessExecutionObservationReader(
            new EmptyAgentReferenceDataProvider(now),
            workspace);

        var result = await reader.ReadAsync(
            new ProcessExecutionObservationQuery(
                [processRunId],
                now.AddHours(-1),
                now.AddHours(1),
                TakePerRun: 1)
            {
                StepInstanceIds = [stepId],
                DetailLevel = ProcessExecutionObservationDetailLevel.Summary
            });

        Assert.False(result.IsComplete);
        Assert.Single(result.Items);
        Assert.Equal(newerRun.Id, result.Items[0].ExecutionRunId);
        Assert.Equal(2, Assert.Single(workspace.ExecutionRunQueries).Take);
    }

    [Fact]
    public async Task Runtime_usage_reader_reports_per_run_cap_boundary_as_incomplete()
    {
        var now = new DateTimeOffset(2026, 7, 24, 14, 0, 0, TimeSpan.Zero);
        var processRunId = ProcessRunId.New();
        var executionRun = CreateUsageExecutionRun(
            processRunId,
            now.AddMinutes(-2));
        var firstUsage = new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: now.AddMinutes(-2),
            ProviderName: "OpenAI default",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-test",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 100,
            CachedInputTokens: 10,
            OutputTokens: 50,
            ReasoningTokens: 0,
            TotalTokens: 150,
            ToolCallCount: 1)
        {
            ExecutionRunId = executionRun.Id,
            ProcessRunId = processRunId.ToString()
        };
        var secondUsage = firstUsage with
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now.AddMinutes(-1)
        };
        var workspace = new UsageTelemetryWorkspaceService(
            [executionRun],
            new Dictionary<Guid, ExecutionRunDetail>
            {
                [executionRun.Id] = CreateExecutionRunDetail(
                    executionRun,
                    [firstUsage, secondUsage])
            });
        var reader = new AgentFrameworkProcessRuntimeUsageTelemetryReader(
            new EmptyAgentReferenceDataProvider(now),
            workspace);

        var result = await reader.ReadAsync(
            new ProcessRuntimeUsageTelemetryQuery(
                [processRunId],
                now.AddHours(-1),
                now.AddHours(1),
                TakePerRun: 1));

        Assert.False(result.IsComplete);
        Assert.Single(result.Items);
        Assert.Equal(firstUsage.Id, result.Items[0].UsageObservationId);
        Assert.Equal(2, Assert.Single(workspace.ExecutionRunQueries).Take);
    }

    [Fact]
    public async Task Runtime_usage_reader_counts_typed_process_workflow_fact_once_when_agent_telemetry_has_same_id()
    {
        var fixture = CreateWorkflowUsageTelemetryFixture();
        var workflowStore = new InMemoryWorkflowUsageObservationStore();
        await workflowStore.AppendAsync(fixture.WorkflowObservation);
        var reader = new WorkflowAwareProcessRuntimeUsageTelemetryReader(fixture.AgentReader, workflowStore);

        var result = await reader.ReadAsync(new ProcessRuntimeUsageTelemetryQuery(
            [fixture.ProcessRunId],
            fixture.Now.AddHours(-1),
            fixture.Now.AddHours(1),
            TakePerRun: 25));

        Assert.True(result.IsComplete);
        var observation = Assert.Single(result.Items);
        Assert.Equal(fixture.WorkflowObservation.Id.Value, observation.UsageObservationId);
        Assert.Equal(fixture.ProcessRunId, observation.RunId);
        Assert.Equal(fixture.WorkflowObservation.RunId!.Value.Value, observation.ExecutionRunId);
        Assert.Equal(1, observation.ToolCallCount);
        Assert.Equal(0.42m, observation.ActualCostUsd);
    }

    [Fact]
    public async Task Runtime_usage_reader_reports_merged_workflow_cap_boundary_as_incomplete()
    {
        var fixture = CreateWorkflowUsageTelemetryFixture();
        var workflowStore = new InMemoryWorkflowUsageObservationStore();
        var secondObservation = fixture.WorkflowObservation with
        {
            Id = WorkflowUsageObservationId.New(),
            InvocationId = Guid.NewGuid(),
            RecordedAtUtc = fixture.Now.AddMinutes(1)
        };
        await workflowStore.AppendRangeAsync(
            [fixture.WorkflowObservation, secondObservation]);
        var reader = new WorkflowAwareProcessRuntimeUsageTelemetryReader(
            fixture.AgentReader,
            workflowStore);

        var result = await reader.ReadAsync(
            new ProcessRuntimeUsageTelemetryQuery(
                [fixture.ProcessRunId],
                fixture.Now.AddHours(-1),
                fixture.Now.AddHours(1),
                TakePerRun: 1));

        Assert.False(result.IsComplete);
        var observation = Assert.Single(result.Items);
        Assert.Equal(
            fixture.WorkflowObservation.Id.Value,
            observation.UsageObservationId);
    }

    [Fact]
    public async Task Runtime_usage_reader_rejects_invalid_query_boundaries()
    {
        var fixture = CreateWorkflowUsageTelemetryFixture();
        var reader = new WorkflowAwareProcessRuntimeUsageTelemetryReader(
            fixture.AgentReader,
            new InMemoryWorkflowUsageObservationStore());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ListAsync(
            new ProcessRuntimeUsageTelemetryQuery(
                [fixture.ProcessRunId],
                fixture.Now.AddHours(-1),
                fixture.Now.AddHours(1),
                TakePerRun: 0)).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ListAsync(
            new ProcessRuntimeUsageTelemetryQuery(
                [fixture.ProcessRunId],
                fixture.Now.AddHours(1),
                fixture.Now.AddHours(-1),
                TakePerRun: 25)).AsTask());
    }

    [Fact]
    public async Task Runtime_usage_reader_rejects_same_id_immutable_dimension_drift()
    {
        var fixture = CreateWorkflowUsageTelemetryFixture();
        var workflowStore = new InMemoryWorkflowUsageObservationStore();
        await workflowStore.AppendAsync(fixture.WorkflowObservation with
        {
            InputTokens = 101,
            TotalTokens = 151
        });
        var reader = new WorkflowAwareProcessRuntimeUsageTelemetryReader(fixture.AgentReader, workflowStore);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ListAsync(
            new ProcessRuntimeUsageTelemetryQuery(
                [fixture.ProcessRunId],
                fixture.Now.AddHours(-1),
                fixture.Now.AddHours(1),
                TakePerRun: 25)).AsTask());

        Assert.Contains("conflicting immutable dimensions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_usage_reader_rejects_uncorrelated_workflow_fact()
    {
        var fixture = CreateWorkflowUsageTelemetryFixture();
        var uncorrelated = fixture.WorkflowObservation with
        {
            Id = WorkflowUsageObservationId.New(),
            RunId = null
        };
        var reader = new WorkflowAwareProcessRuntimeUsageTelemetryReader(
            fixture.AgentReader,
            new FixedWorkflowUsageObservationStore([uncorrelated]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ListAsync(
            new ProcessRuntimeUsageTelemetryQuery(
                [fixture.ProcessRunId],
                fixture.Now.AddHours(-1),
                fixture.Now.AddHours(1),
                TakePerRun: 25)).AsTask());

        Assert.Contains("not correlated to a workflow run", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_execution_metadata_does_not_trust_repository_root_as_product_target_alias()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.MutateProductTarget
            ],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            launchVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProjectId"] = Guid.NewGuid().ToString("D"),
                ["AgentId"] = "codex-process-e2e",
                ["AgentName"] = "Codex Process E2E",
                ["MachineName"] = "LUCYSPOWER",
                ["RepositoryRoot"] = @"C:\repositories\CanDoItAll",
                ["OutputRoot"] = @"C:\programovani\dotnet\output",
                ["ProductRoot"] = @"C:\programovani\dotnet\output",
                ["BranchName"] = "main",
                ["SessionId"] = "codex-process-e2e-session"
            });

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        var writableAliases = ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(run);

        Assert.Contains(ResolveTestProductAlias(assignment), writableAliases);
        Assert.DoesNotContain("external-target/C/repositories/CanDoItAll", writableAliases);
    }

    [Fact]
    public void Process_execution_metadata_trusts_output_folder_as_product_target_alias()
    {
        var assignment = CreateAssignment(
            Guid.NewGuid(),
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.MutateProductTarget
            ],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            launchVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProjectId"] = Guid.NewGuid().ToString("D"),
                ["OutputFolder"] = @"C:\programovani\dotnet\output"
            });

        var metadataJson = BuildProcessExecutionMetadata(assignment);
        var run = CreateTrustedProcessRun(metadataJson);

        var writableAliases = ExecutionInvocationMetadata.ResolveAllowedExternalTargetAliases(run);

        Assert.Contains(ResolveTestProductAlias(assignment), writableAliases);
    }

    private static WorkflowUsageTelemetryFixture CreateWorkflowUsageTelemetryFixture()
    {
        var now = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero);
        var processRunId = ProcessRunId.New();
        var workflowRunId = WorkflowRunId.New();
        var executionRun = CreateUsageExecutionRun(processRunId, now);
        var usageId = Guid.NewGuid();
        var providerObservation = new ProviderUsageObservation(
            usageId,
            now,
            "OpenAI default",
            ProviderKind.OpenAi,
            "gpt-test",
            ProviderTransportKind.Responses,
            ProviderUsageSourcePhases.AgentRuntime,
            ProviderUsageObservationStatus.Observed,
            InputTokens: 100,
            CachedInputTokens: 10,
            OutputTokens: 50,
            ReasoningTokens: 0,
            TotalTokens: 150,
            ToolCallCount: 1)
        {
            ExecutionRunId = workflowRunId.Value,
            ProcessRunId = processRunId.ToString(),
            CalculatedCostUsd = 0.42m
        };
        var workspace = new UsageTelemetryWorkspaceService(
            [executionRun],
            new Dictionary<Guid, ExecutionRunDetail>
            {
                [executionRun.Id] = CreateExecutionRunDetail(executionRun, [providerObservation])
            });
        var agentReader = new AgentFrameworkProcessRuntimeUsageTelemetryReader(
            new WorkspaceBackedAgentReferenceDataProvider(workspace, new AgentReferenceDataCache()),
            workspace);
        var assignmentId = new WorkflowProcessAssignmentId(Guid.Parse(executionRun.ProcessStepId));
        var workflowObservation = new WorkflowUsageObservation(
            new WorkflowUsageObservationId(usageId),
            workflowRunId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            new WorkflowNodeId("process-workflow-node"),
            ExecutorId: null,
            ComponentId: null,
            WorkflowUsageProducerKind.LlmComponent,
            Guid.NewGuid(),
            Attempt: 1,
            ProviderProfileId: null,
            "OpenAI default",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-test",
            ProviderUsageSourcePhases.AgentRuntime,
            WorkflowUsageStatus.Observed,
            WorkflowPricingStatus.Known,
            WorkflowUsagePricingProvenance.PricingProfileSnapshot,
            InputTokens: 100,
            CachedInputTokens: 10,
            OutputTokens: 50,
            ReasoningTokens: 0,
            TotalTokens: 150,
            ToolCallCount: 1,
            CostUsd: 0.42m,
            PricingProfileHash: "process-workflow-profile",
            PricingVersion: "v1",
            ProviderRequestId: string.Empty,
            ProviderResponseId: string.Empty,
            now.AddSeconds(-1),
            now,
            now,
            new WorkflowLaunchOrigin.ProcessAssignment(
                new WorkflowProcessRunId(processRunId.Value),
                assignmentId,
                new WorkflowLaunchCorrelationId("process-workflow-usage")));
        return new WorkflowUsageTelemetryFixture(
            now,
            processRunId,
            agentReader,
            workflowObservation);
    }

    private static ExecutionRunRecord CreateUsageExecutionRun(
        ProcessRunId processRunId,
        DateTimeOffset updatedAtUtc)
    {
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "usage-test",
            CorrelationId: processRunId.ToString(),
            CausationId: "step-001",
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Input",
            ResultSummary: "Result",
            ProviderName: "OpenAI default",
            Model: "gpt-test",
            State: ExecutionState.Completed,
            Outcome: RunOutcome.Succeeded,
            CreatedAtUtc: updatedAtUtc.AddMinutes(-1),
            UpdatedAtUtc: updatedAtUtc,
            StartedAtUtc: updatedAtUtc.AddMinutes(-1),
            CompletedAtUtc: updatedAtUtc,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId.ToString(),
            ProcessStepId: ProcessStepInstanceId.New().ToString());
    }

    private static ExecutionRunDetail CreateExecutionRunDetail(
        ExecutionRunRecord run,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        return new ExecutionRunDetail(run, null, [], [])
        {
            UsageObservations = usageObservations
        };
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        Guid projectId,
        IReadOnlyList<string>? allowedOperations = null,
        string? operationTargetScope = null,
        IReadOnlyDictionary<string, string>? launchVariables = null,
        string stepKey = "resolve-blazor-contract")
    {
        var resolvedLaunchVariables = AddTestExternalTargetAuthority(
            launchVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProjectId"] = projectId.ToString("D"),
                ["AgentId"] = "codex-process-e2e",
                ["AgentName"] = "Codex Process E2E",
                ["MachineName"] = "LUCYSPOWER",
                ["RepositoryRoot"] = @"C:\programovani\dotnet\output",
                ["OutputRoot"] = @"C:\programovani\dotnet\output",
                ["ProductRoot"] = @"C:\programovani\dotnet\output",
                ["BranchName"] = "main",
                ["SessionId"] = "codex-process-e2e-session"
            });
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            stepKey,
            "blazor-engineer",
            "lead-engineer",
            "Blazor engineer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Blazor engineer",
            "Resolve the project contract.",
            "sha256:readiness",
            "Resolved from live profile.",
            [ArtifactSlotId.New()],
            [],
            allowedOperations ??
            [
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.MutateProductTarget
            ],
            operationTargetScope ?? ProcessOperationContractNames.ExternalProductTargetMutable,
            resolvedLaunchVariables,
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyDictionary<string, string> AddTestExternalTargetAuthority(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        const string rootId = "0123456789abcdef01234567";
        var productAlias = ExternalTargetAliasCodec.BuildAlias(
            rootId,
            ["programovani", "dotnet", "output"]);
        var resolved = new Dictionary<string, string>(launchVariables, StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ExternalTargetRoot] = productAlias,
            [ProcessRuntimeLaunchVariables.OutputRootAlias] = productAlias,
            [ProcessRuntimeLaunchVariables.ProductRootAlias] = productAlias,
            [ProcessRuntimeLaunchVariables.WorkspaceAlias] = productAlias,
            [ProcessRuntimeLaunchVariables.ExternalTargetRootBindings] = JsonSerializer.Serialize(
                new[]
                {
                    new ExternalTargetRootBinding(
                        rootId,
                        "test-host",
                        "test-protected-root-token")
                })
        };
        return resolved;
    }

    private static string ResolveTestProductAlias(ProcessRuntimeStepAssignment assignment)
        => assignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductRootAlias];

    private static string BuildProcessExecutionMetadata(ProcessRuntimeStepAssignment assignment)
        => new ProcessExecutionMetadataComposer(
        [
            new BrowserExecutionMetadataContribution()
        ]).Compose(assignment);

    private static ExecutionRunRecord CreateTrustedProcessRun(string metadataJson)
    {
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "resolve-blazor-contract",
            CorrelationId: "run-001",
            CausationId: "step-001",
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: "run-001",
            ProcessStepId: "step-001");
    }

    private sealed record WorkflowUsageTelemetryFixture(
        DateTimeOffset Now,
        ProcessRunId ProcessRunId,
        AgentFrameworkProcessRuntimeUsageTelemetryReader AgentReader,
        WorkflowUsageObservation WorkflowObservation);

    private sealed class EmptyAgentReferenceDataProvider(DateTimeOffset now) : IAgentReferenceDataProvider
    {
        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                [],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                now,
                TimeSpan.Zero));
        }
    }

    private sealed class FixedWorkflowUsageObservationStore(
        IReadOnlyList<WorkflowUsageObservation> observations) : IWorkflowUsageObservationStore
    {
        public Task AppendAsync(
            WorkflowUsageObservation observation,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AppendRangeAsync(
            IReadOnlyList<WorkflowUsageObservation> observationsToAppend,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkflowUsageObservation>> ListAsync(
            WorkflowUsageObservationQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(observations);

        public Task<WorkflowListPage<WorkflowUsageObservation>> ListPageAsync(
            WorkflowUsageObservationPageRequest request,
            CancellationToken cancellationToken = default)
        {
            var items = observations
                .Skip(request.PageIndex * request.PageSize)
                .Take(request.PageSize)
                .ToArray();
            return Task.FromResult(new WorkflowListPage<WorkflowUsageObservation>(
                items,
                request.PageIndex,
                request.PageSize,
                observations.Count));
        }
    }

    private sealed class FixedMetadataContribution(
        string contributionKey,
        int order,
        IReadOnlyDictionary<string, object> metadata) : IProcessExecutionMetadataContribution
    {
        public string ContributionKey => contributionKey;

        public int Order => order;

        public IReadOnlyDictionary<string, object> BuildMetadata(
            ProcessExecutionMetadataContributionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return metadata;
        }
    }

    private sealed class UsageTelemetryWorkspaceService(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        IReadOnlyDictionary<Guid, ExecutionRunDetail> executionRunDetails) : IAgentFrameworkWorkspaceService
    {
        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public List<ExecutionRunQuery> ExecutionRunQueries { get; } = [];

        public int ExecutionRunDetailRequestCount { get; private set; }

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([]);

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
            ExecutionRunQuery query,
            CancellationToken cancellationToken = default)
        {
            ExecutionRunQueries.Add(query);
            var processRunIds = query.ProcessRunIds.ToHashSet(
                StringComparer.OrdinalIgnoreCase);
            var processStepIds = query.ProcessStepIds.ToHashSet(
                StringComparer.OrdinalIgnoreCase);
            var result = executionRuns
                .Where(run => query.ProcessRunId is null ||
                              string.Equals(run.ProcessRunId, query.ProcessRunId, StringComparison.OrdinalIgnoreCase))
                .Where(run => processRunIds.Count == 0 ||
                              processRunIds.Contains(run.ProcessRunId))
                .Where(run => query.ProcessStepId is null ||
                              string.Equals(run.ProcessStepId, query.ProcessStepId, StringComparison.OrdinalIgnoreCase))
                .Where(run => processStepIds.Count == 0 ||
                              processStepIds.Contains(run.ProcessStepId))
                .Where(run => query.UpdatedFromUtc is null || run.UpdatedAtUtc >= query.UpdatedFromUtc)
                .Where(run => query.UpdatedToUtc is null || run.UpdatedAtUtc <= query.UpdatedToUtc)
                .OrderByDescending(run => run.UpdatedAtUtc)
                .Take(query.Take)
                .ToList();
            return Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(result);
        }

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
        {
            ExecutionRunDetailRequestCount++;
            return executionRunDetails.TryGetValue(executionRunId, out var detail)
                ? Task.FromResult(detail)
                : throw new InvalidOperationException("Execution run detail was not found.");
        }

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(bool includeTemplates = true, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(Guid providerId, ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(Guid agentId, Guid? preferredSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> RenameChatSessionAsync(Guid agentId, Guid chatSessionId, string title, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ExecuteRunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, AgentExecutionOperationId activityOperationId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, AgentExecutionOperationId activityOperationId, IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            AgentChatRunOptions options,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            AgentExecutionOperationId activityOperationId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            AgentExecutionOperationId activityOperationId,
            IReadOnlyList<PendingToolApprovalDecision> decisions,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
            => new("This fake workspace method is not used by the usage telemetry reader test.");
    }
}
