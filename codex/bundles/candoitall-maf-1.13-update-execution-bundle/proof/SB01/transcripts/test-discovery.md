# SB01 focused test discovery
Working directory: C:\repositories\CanDoItAll
Command: rg -n 'Maf|AgentFramework|ProviderRuntime|Finalizer|Workflow|ProcessRuntimeDispatch|ProjectStructureAgent' tests -g '*.cs'
Timestamp: 2026-07-07T20:28:02.1938086-04:00

tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:7:public sealed class AgentFrameworkExecutionCapabilityFilteringIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:31:            """{"registeredSkillServiceType":"CanDoItAll.AgentFramework.Sandbox.Hosting.WorkspaceDeliverySkill, CanDoItAll.AgentFramework.Sandbox"}""",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:109:            """{"registeredSkillServiceType":"Legacy.WorkspaceDeliverySkill, Legacy.Sandbox","legacyServiceType":"CanDoItAll.AgentFramework.Sandbox.Hosting.WorkspaceDeliverySkill, CanDoItAll.AgentFramework.Sandbox"}""",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:226:            """{"registeredSkillServiceType":"CanDoItAll.AgentFramework.Sandbox.Hosting.WorkspaceDeliverySkill, CanDoItAll.AgentFramework.Sandbox"}""",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:298:            "CanDoItAll.AgentFramework.Core.AgentFrameworkWorkspaceExecutionService, CanDoItAll.AgentFramework.Core",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:300:            ?? throw new InvalidOperationException("Could not resolve AgentFrameworkWorkspaceExecutionService.");
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:315:            "CanDoItAll.AgentFramework.Core.AgentFrameworkWorkspaceExecutionService, CanDoItAll.AgentFramework.Core",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs:317:            ?? throw new InvalidOperationException("Could not resolve AgentFrameworkWorkspaceExecutionService.");
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:4:using CanDoItAll.AgentFramework.Capabilities.Templates;
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:5:using CanDoItAll.AgentFramework.Mcp;
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:6:using CanDoItAll.AgentFramework.Mcp.Abstractions;
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:7:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:8:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:11:using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
tests\Integration\CanDoItAll.Tests.Integration\AgentCapabilitySetupApiIntegrationTests.cs:12:using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;
tests\Playwright\CanDoItAll.Tests.Playwright\AgentCapabilitySetupFlowPlaywrightTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Playwright\CanDoItAll.Tests.Playwright\AgentCapabilitySetupFlowPlaywrightTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Playwright\CanDoItAll.Tests.Playwright\AgentCapabilitySetupFlowPlaywrightTests.cs:88:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Unit\CanDoItAll.Tests.Unit\A2ARemoteAgentToolFactoryTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\A2ARemoteAgentToolFactoryTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\A2ARemoteAgentToolFactoryTests.cs:40:                        AuthSecretConfigurationKey = "AgentFramework:A2A:SecuredToken"
tests\Unit\CanDoItAll.Tests.Unit\AgentA2AHostCardFactoryTests.cs:2:using CanDoItAll.AgentFramework.Hosting;
tests\Unit\CanDoItAll.Tests.Unit\AgentA2AHostCardFactoryTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentA2AMetadataTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentA2AMetadataTests.cs:22:                    AuthSecretConfigurationKey = "AgentFramework:A2A:DeliveryQaToken",
tests\Unit\CanDoItAll.Tests.Unit\AgentA2AMetadataTests.cs:40:        Assert.Equal("AgentFramework:A2A:DeliveryQaToken", endpoint.AuthSecretConfigurationKey);
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:21:    public async Task Maf_provider_converts_successful_contribution_to_chat_messages()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:55:    public async Task Maf_provider_records_skipped_contribution_trace()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:78:    public async Task Maf_provider_surfaces_failed_result_as_typed_exception()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:100:    public async Task Maf_provider_honors_cancellation()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:122:    public async Task Maf_runtime_attaches_enabled_contributors_in_deterministic_order()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:140:            .OfType<MafAgentContextContributionProvider>()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:151:            .OfType<MafAgentContextContributionProvider>()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:162:    public async Task Maf_runtime_uses_context_workspace_scope_override_for_contributors()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:187:        var provider = Assert.Single(contextProviders.OfType<MafAgentContextContributionProvider>());
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:197:    public async Task Maf_runtime_rejects_duplicate_contributor_ids()
tests\Unit\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:448:    private static MafAgentContextContributionProvider CreateProvider(
tests\Unit\CanDoItAll.Tests.Unit\AgentExecutionCancellationRegistryTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentExecutionCancellationRegistryTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkRuntimeSwitchingIntegrationTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkRuntimeSwitchingIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkRuntimeSwitchingIntegrationTests.cs:5:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkRuntimeSwitchingIntegrationTests.cs:12:public sealed class AgentFrameworkRuntimeSwitchingIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkRuntimeSwitchingIntegrationTests.cs:26:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkRuntimeSwitchingIntegrationTests.cs:67:        var restartedWorkspaceService = restartedScope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Memory\CanDoItAll.Memory.Tests\ManualMemorySourceIngestionTests.cs:158:            WorkflowId: null,
tests\Memory\CanDoItAll.Memory.Tests\ManualMemorySourceIngestionTests.cs:159:            WorkflowNodeId: null,
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\WorkspaceImageAnalysisPromptNormalizerTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\WorkspaceImageAnalysisPromptNormalizerTests.cs:3:namespace CanDoItAll.Tests.Unit.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkPersistenceIntegrationTests.cs:1:using CanDoItAll.AgentFramework.Persistence;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkPersistenceIntegrationTests.cs:5:public sealed class AgentFrameworkPersistenceIntegrationTests
tests\Memory\CanDoItAll.Memory.Tests\HostCompositionDependencyRemovalTests.cs:19:        "CanDoItAll.AgentFramework.Rag.Qdrant",
tests\Memory\CanDoItAll.Memory.Tests\HostCompositionDependencyRemovalTests.cs:20:        "CanDoItAll.AgentFramework.SemanticCompletion.Driver",
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:4:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:11:public sealed class AgentFinalizerPolicyTests
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:306:        var validator = new DefaultAgentFinalizerValidator();
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:318:        var validator = new DefaultAgentFinalizerValidator();
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:320:        var invocation = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:336:        var validator = new DefaultAgentFinalizerValidator();
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:338:        var first = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:342:        var second = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:357:        var validator = new DefaultAgentFinalizerValidator();
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:359:        var first = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:363:        var second = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:369:        var normalized = AgentFinalizerInvocationNormalizer.NormalizeRequired(policy, [first, second]);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:383:        var validator = new DefaultAgentFinalizerValidator();
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:385:        var invocation = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:399:        var validator = new DefaultAgentFinalizerValidator();
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:401:        var unrelatedTextTool = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:423:        var result = AgentFinalizerSequenceValidator.Validate(policy, traces);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:427:        Assert.Equal(2, result.FinalizerSequence);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:442:        var result = AgentFinalizerSequenceValidator.Validate(policy, traces);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:465:        var result = AgentFinalizerSequenceValidator.Validate(policy, traces);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:481:        var mode = AgentFinalizerPolicies.ResolveMode(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:485:        Assert.Equal(AgentFinalizerMode.Shadow, mode);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:492:            metadataJson: $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.RequiredFinalizerModeValue}}"}""");
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:494:        var mode = AgentFinalizerPolicies.ResolveMode(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:498:        Assert.Equal(AgentFinalizerMode.Required, mode);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:505:            metadataJson: $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.ShadowFinalizerModeValue}}"}""");
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:507:        var mode = AgentFinalizerPolicies.ResolveMode(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:511:        Assert.Equal(AgentFinalizerMode.Shadow, mode);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:518:            metadataJson: $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.DisabledFinalizerModeValue}}"}""");
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:520:        var mode = AgentFinalizerPolicies.ResolveMode(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:524:        Assert.Equal(AgentFinalizerMode.Disabled, mode);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:534:        var resolved = AgentFinalizerPolicies.TryResolveForStructuredOutput(unknownContract, out var policy);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:547:                Resolved: AgentFinalizerPolicies.TryResolveForStructuredOutput(contract, out var policy),
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:577:        var resolvedTool = MafFinalizerDriver.ResolveRequiredFinalizerTool(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:580:        MafFinalizerDriver.ConfigureRequiredFinalizerRepairChatOptions(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:584:        var repairOptions = MafFinalizerDriver.CreateRequiredFinalizerRepairRunOptions(policy, resolvedTool);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:629:        var directResult = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:634:        var wrappedResult = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:677:        var result = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:711:        var result = MafFinalizerDriver.TryNormalizeFinalizerJsonRepairText(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:718:        var validation = new DefaultAgentFinalizerValidator().Validate(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:720:            [new AgentFinalizerInvocation(policy.ToolName, argumentsJson, Sequence: 1)]);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:730:        var recorder = new MafFinalizerDriver.StreamedFinalizerInvocationRecorder(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:732:            AgentFinalizerMode.Required);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:756:        var invocation = Assert.Single(recorder.SnapshotFinalizerInvocations());
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:760:        var validation = new DefaultAgentFinalizerValidator().Validate(policy, [invocation]);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:769:    public void Finalizer_capture_accepts_process_step_outcome_result_as_json_string_argument()
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:772:        var capture = CreateFinalizerCapture(policy);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:782:        var validation = new DefaultAgentFinalizerValidator().Validate(policy, snapshot);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:790:    public void Finalizer_capture_normalizes_json_string_argument_missing_reason()
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:793:        var capture = CreateFinalizerCapture(policy);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:811:        var validation = new DefaultAgentFinalizerValidator().Validate(policy, snapshot);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:821:        var invalidCaptured = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:825:        var synthesizedRepair = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:830:        var effective = MafFinalizerDriver.CreateEffectiveFinalizerInvocations(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:832:            AgentFinalizerMode.Required,
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:841:        var validation = new DefaultAgentFinalizerValidator().Validate(policy, effective);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:852:        var first = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:856:        var second = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:860:        var third = new AgentFinalizerInvocation(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:865:        var effective = MafFinalizerDriver.CreateEffectiveFinalizerInvocations(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:867:            AgentFinalizerMode.Required,
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:876:        var validation = new DefaultAgentFinalizerValidator().Validate(policy, effective);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:889:        var toolRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerRepairPrompt(policy, previousText);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:890:        var jsonRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerJsonRepairPrompt(policy, previousText);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:908:        var toolRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerRepairPrompt(policy, null, repairContext);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:909:        var jsonRepairPrompt = MafFinalizerDriver.BuildRequiredFinalizerJsonRepairPrompt(policy, null, repairContext);
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:928:            FinalizerMode: AgentFinalizerMode.Required,
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:936:        var useFrameworkHistory = MafRuntimeSessionBuilder.ShouldUseFrameworkManagedHistory(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:964:        var orderedProviders = AgentFrameworkWorkspaceExecutionService.OrderGovernedProcessProviderOverrideCandidates(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:982:        var isUnsupported = MafModelParametersBuilder.IsReasoningEffortConfiguredButTransportUnsupported(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:996:                FinalizerMode: AgentFinalizerMode.Required,
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1004:            AgentFinalizerPolicies.RequiredFinalizerModeValue,
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1005:            root.GetProperty(AgentFinalizerPolicies.FinalizerModeMetadataKey).GetString());
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1051:        var launchAgent = new ProjectStructureAgentIdentityDescriptor(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1077:            new ProjectStructureAgentIdentityDescriptor(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1097:    public void AgentFramework_runtime_options_include_context_workspace_scope_from_metadata()
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1104:        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1116:    private static AgentFinalizerPolicy CreatePolicy()
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1118:        return AgentFinalizerPolicy.Required<ProcessStepOutcomeResult>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFinalizerPolicyTests.cs:1124:    private static FinalizerCapture CreateFinalizerCapture(AgentFinalizerPolicy policy)
tests\Integration\CanDoItAll.Tests.Integration\ApiTestHost.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\ApiTestHost.cs:102:        app.MapProjectStructureAgentApi();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:5:using CanDoItAll.AgentFramework.Persistence;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:14:public sealed class AgentFrameworkExecutionRunTrackingIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:35:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:100:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:163:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:225:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:278:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:345:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:407:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:454:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:531:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:582:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:631:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:681:                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:688:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:707:            entry => entry.Phase == "Finalizer validation" &&
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:731:                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)],
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:735:                            ProviderUsageSourcePhases.FinalizerShortCircuit,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:747:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:769:        Assert.Equal(ProviderUsageSourcePhases.FinalizerShortCircuit, usage.SourcePhase);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:793:                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:800:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:810:                Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:823:            entry => entry.Phase == "Finalizer validation" &&
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:844:                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)],
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:848:                            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:862:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:874:                    Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:884:            entry => entry.Phase == "Finalizer sequencing" &&
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:905:                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:912:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:923:                Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:958:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1006:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1017:                    Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1028:            entry => entry.Phase == "Finalizer validation" &&
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1064:    private static string CreateRequiredFinalizerMetadata()
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1066:        return $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.RequiredFinalizerModeValue}}"}""";
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1146:    private static AgentFinalizerInvocation CreateFinalizerInvocation(ProcessStepOutcomeResult outcome)
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1148:        return new AgentFinalizerInvocation(
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1149:            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1177:        services.RemoveAll<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1188:        services.AddScoped<IAgentExecutionCheckpointBridge>(serviceProvider => new WorkflowBackedAgentExecutionCheckpointBridge(
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1195:        services.AddScoped<IAgentFrameworkWorkspaceService, AgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1381:        public IReadOnlyList<AgentFinalizerInvocation> InitialFinalizerInvocations { get; init; } = [];
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1404:        public IReadOnlyList<AgentFinalizerInvocation> ContinuationFinalizerInvocations { get; init; } = [];
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1471:                FinalizerInvocations = InitialFinalizerInvocations,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1473:                    ? CreateFinalizerToolInvocationTraces(InitialFinalizerInvocations)
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1510:                FinalizerInvocations = ContinuationFinalizerInvocations,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1512:                    ? CreateFinalizerToolInvocationTraces(ContinuationFinalizerInvocations)
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1517:        private static IReadOnlyList<AgentToolInvocationTrace> CreateFinalizerToolInvocationTraces(
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1518:            IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations)
tests\Integration\CanDoItAll.Tests.Integration\AgentTeamCatalogIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\AgentTeamCatalogIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentTeamCatalogIntegrationTests.cs:4:using CanDoItAll.AgentFramework.Persistence;
tests\Integration\CanDoItAll.Tests.Integration\AgentTeamCatalogIntegrationTests.cs:5:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\AgentTeamCatalogIntegrationTests.cs:270:        IAgentFrameworkWorkspaceService workspaceService,
tests\Memory\CanDoItAll.Memory.Tests\MemoryHttpDriverTests.cs:211:                WorkflowId: "workflow-1",
tests\Memory\CanDoItAll.Memory.Tests\MemoryHttpDriverTests.cs:212:                WorkflowNodeId: "node-1",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Capabilities\CapabilityAccessPolicyEvaluatorTests.cs:1:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Capabilities\CapabilityAccessPolicyEvaluatorTests.cs:2:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Capabilities\CapabilityAccessPolicyEvaluatorTests.cs:3:using CanDoItAll.AgentFramework.Capabilities.Templates;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Capabilities\CapabilityAccessPolicyEvaluatorTests.cs:5:namespace CanDoItAll.Tests.Unit.AgentFramework.Capabilities;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ComfyUiProviderDriverTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ComfyUiProviderDriverTests.cs:5:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ComfyUiProviderDriverTests.cs:7:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ComfyUiProviderDriverTests.cs:160:    public async Task ComfyUiProviderDriver_RejectsMissingWorkflowTemplate()
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ComfyUiProviderDriverTests.cs:298:            [ComfyUiProviderOptions.WorkflowTemplateJsonKey] = """
tests\Memory\CanDoItAll.Memory.Tests\MemoryFoundationCheckpointTests.cs:39:            "CanDoItAll.AgentFramework.Rag",
tests\Memory\CanDoItAll.Memory.Tests\MemoryFoundationCheckpointTests.cs:71:            "CanDoItAll.AgentFramework.Rag"
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:2:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:4:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:130:        var providerProjectRoot = Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Providers");
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:150:            "src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:157:            Assert.DoesNotContain("CanDoItAll.AgentFramework.Providers", text, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:162:            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:166:            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:170:            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/ProviderRuntimeDiagnostics.cs")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:173:            "ProviderRuntimeVoiceDriver",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:174:            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Voice/AgentVoiceDriverFactory.cs")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderArchitectureFoundationTests.cs:176:        var imageToolProviderSource = File.ReadAllText(Path.Combine(root, "src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs"));
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:4:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:12:public sealed class AgentFrameworkExecutionRecoveryIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:25:                var recoveryWorkerType = typeof(AgentFrameworkModuleAssemblyMarker).Assembly.GetType(
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:26:                    "CanDoItAll.Modules.AgentFramework.AgentFrameworkExecutionRecoveryWorker",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:28:                    ?? throw new InvalidOperationException("AgentFrameworkExecutionRecoveryWorker type was not found.");
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:37:            var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:160:            var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:220:        var recoveryServiceType = typeof(AgentFrameworkModuleAssemblyMarker).Assembly.GetType(
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:221:            "CanDoItAll.Modules.AgentFramework.AgentFrameworkExecutionRecoveryService",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRecoveryIntegrationTests.cs:223:            ?? throw new InvalidOperationException("AgentFrameworkExecutionRecoveryService type was not found.");
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:5:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:7:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:411:    public void ConcreteDrivers_ConsumersUseProviderRuntimeAdoptionBoundaries()
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:416:            "src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:423:            Assert.DoesNotContain("CanDoItAll.AgentFramework.Providers", text, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:427:            "MafProviderRuntimeGateway",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:428:            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:432:            File.ReadAllText(Path.Combine(root, "src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:436:            File.ReadAllText(Path.Combine(root, "src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:439:            "ProviderRuntimeVoiceDriver",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ConcreteProviderDriverTests.cs:440:            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Voice/AgentVoiceDriverFactory.cs")),
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:28:        var zeroProviderRuntime = await runtime.ExecuteContextQueryAsync(
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:39:        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, zeroProviderRuntime.Selection.Status);
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:41:        Assert.False(zeroProviderRuntime.DriverDispatchAttempted);
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:150:            zeroProviderRuntime,
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:326:            WorkflowId: "workflow-regression",
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:327:            WorkflowNodeId: "node-regression",
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:338:            WorkflowId: "workflow-regression",
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:339:            WorkflowNodeId: "node-regression",
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:362:        MemoryRuntimeOperationResult zeroProviderRuntime,
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:415:                RuntimeStatus = zeroProviderRuntime.Selection.Status.ToString(),
tests\Memory\CanDoItAll.Memory.Tests\MemoryEndToEndObservabilityProofTests.cs:416:                RuntimeDispatchAttempted = zeroProviderRuntime.DriverDispatchAttempted,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:5:using CanDoItAll.AgentFramework.Persistence;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:6:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:11:public sealed class AgentFrameworkWorkspaceSeedIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:21:        var retiredBundleWorkflowCapabilityId = Guid.NewGuid();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:34:        var retiredBundleWorkflowCapability = new CapabilityCatalogItem(
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:35:            retiredBundleWorkflowCapabilityId,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:38:            "Bundle Workflow Skill",
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:58:            Capabilities = seed.Capabilities.Concat([retiredCapability, retiredBundleWorkflowCapability]).ToList(),
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:71:                            retiredBundleWorkflowCapabilityId,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:72:                            retiredBundleWorkflowCapability.Key,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:73:                            retiredBundleWorkflowCapability.Kind,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:88:        Assert.DoesNotContain(normalized.Capabilities, item => item.Id == retiredBundleWorkflowCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:91:        Assert.DoesNotContain(normalizedFinancialStrategist.Capabilities, item => item.CapabilityId == retiredBundleWorkflowCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:303:        Assert.Contains("flux1-dev.safetensors", root.GetProperty(ComfyUiProviderConfigurationKeys.WorkflowTemplateJson).GetString(), StringComparison.Ordinal);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:846:        var playwrightWorkflowCapabilityId = capabilityIdsByKey["candoitall-watch-playwright-loop"];
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:912:        AssertHasCapabilities(programmingAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:913:        AssertHasCapabilities(qaAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:915:        AssertHasCapabilities(uiReviewAgent, playwrightCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:917:        AssertHasCapabilities(releaseManagerAgent, playwrightCapabilityId, playwrightWorkflowCapabilityId, concreteDeliverableDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:919:        AssertHasCapabilities(dotnetDeveloperAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:920:        AssertHasCapabilities(blazorDeveloperAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, blazorSsrDeliveryCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, workspaceDotnetNewCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:921:        AssertHasCapabilities(dotnetQaAgent, playwrightCapabilityId, codeanalyticsCapabilityId, componentsCapabilityId, frontendThemeCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, runTestsCapabilityId, mstestCapabilityId, concreteDeliverableDeliveryCapabilityId, dotnetAppDeliveryCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, workspaceDotnetRunCapabilityId, workspaceDotnetStopCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:923:        AssertHasCapabilities(javascriptDeveloperAgent, playwrightCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, workspaceSourceRagCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:924:        AssertHasCapabilities(javascriptQaAgent, playwrightCapabilityId, concreteDeliverableDeliveryCapabilityId, frontendSkillCapabilityId, playwrightWorkflowCapabilityId, createDirectoryCapabilityId, writeFileCapabilityId, appendFileCapabilityId, inspectImageCapabilityId, analyzeImageCapabilityId, analyzeImagesCapabilityId, pwshRunScriptCapabilityId);
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:1624:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:1652:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:1682:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:1706:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:1724:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:1744:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs:1761:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:4:using CanDoItAll.AgentFramework.Persistence;
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:9:public sealed class AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:16:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:66:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:246:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs:371:        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Memory\CanDoItAll.Memory.Tests\MemoryAsyncWorkerTests.cs:301:            WorkflowId: null,
tests\Memory\CanDoItAll.Memory.Tests\MemoryAsyncWorkerTests.cs:302:            WorkflowNodeId: null,
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:3:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:5:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:7:public sealed class ProviderRuntimeLifecycleTests
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:18:        var pool = new ProviderRuntimePool(descriptorSource, new ProviderRuntimeHandleFactory(TestProviderFactory));
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:43:        var pool = new ProviderRuntimePool(descriptorSource, new ProviderRuntimeHandleFactory(TestProviderFactory));
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:49:            await pool.InvalidateAsync(providerId, ProviderRuntimeInvalidationReason.ProfileSaved);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:62:        await using var handle = new ProviderRuntimeHandle(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:63:            ProviderRuntimeDescriptor.FromProfile(provider),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:68:                new ProviderRuntimeDispatchRequest<int>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:91:        await using var handle = new ProviderRuntimeHandle(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:92:            ProviderRuntimeDescriptor.FromProfile(provider),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:98:            new ProviderRuntimeDispatchRequest<int>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:110:            new ProviderRuntimeDispatchRequest<int>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:127:        await using var handle = new ProviderRuntimeHandle(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:128:            ProviderRuntimeDescriptor.FromProfile(provider),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:135:            new ProviderRuntimeDispatchRequest<int>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:149:            new ProviderRuntimeDispatchRequest<int>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:173:        await using var handle = new ProviderRuntimeHandle(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:174:            ProviderRuntimeDescriptor.FromProfile(provider),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:181:            new ProviderRuntimeDispatchRequest<int>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:195:            new ProviderRuntimeDispatchRequest<int>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:220:            "IProviderRuntimeGateway",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:226:            typeof(ProviderRuntimePool),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:227:            typeof(ProviderRuntimeHandle),
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:228:            typeof(ProviderRuntimeHandleFactory)
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:243:            .EnumerateFiles(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Providers", "Runtime"), "*.cs")
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:258:            "src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:265:            Assert.DoesNotContain("CanDoItAll.AgentFramework.Providers", text, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:269:    private static ProviderRuntimeDescriptor CreateDescriptor(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:273:        return ProviderRuntimeDescriptor.FromProfile(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:332:    private sealed class MutableDescriptorSource(ProviderRuntimeDescriptor descriptor) : IProviderRuntimeDescriptorSource
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:334:        public ProviderRuntimeDescriptor Descriptor { get; set; } = descriptor;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeLifecycleTests.cs:336:        public Task<ProviderRuntimeDescriptor> GetRequiredAsync(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderDispatchLaneGateTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderDispatchLaneGateTests.cs:2:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderDispatchLaneGateTests.cs:4:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderDispatchLaneGateTests.cs:61:    public async Task ProviderRuntimeHandle_dispatch_enforces_descriptor_timeout_and_clears_pending_request()
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderDispatchLaneGateTests.cs:64:        var descriptor = ProviderRuntimeDescriptor.FromProfile(provider, timeoutSeconds: 5);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderDispatchLaneGateTests.cs:65:        await using var handle = new ProviderRuntimeHandle(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderDispatchLaneGateTests.cs:69:        var request = new ProviderRuntimeDispatchRequest<string>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:5:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:7:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:9:public sealed class ProviderRuntimeImageGenerationServiceTests
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:20:        await using var runtimePool = new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:22:            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:25:        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:56:        await using var runtimePool = new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:58:            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:61:        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:84:        await using var runtimePool = new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:86:            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderRuntimeImageGenerationServiceTests.cs:89:        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:3:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:5:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:291:    private static ProviderRuntimePool CreateRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:297:            provider => ProviderRuntimeDescriptor.FromProfile(provider));
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:303:        return new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:305:            new ProviderRuntimeHandleFactory(factory));
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:334:        IReadOnlyDictionary<Guid, ProviderRuntimeDescriptor> descriptors) : IProviderRuntimeDescriptorSource
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\ProviderBatchJobBalancerTests.cs:336:        public Task<ProviderRuntimeDescriptor> GetRequiredAsync(
tests\Memory\CanDoItAll.Memory.Tests\MemoryProviderRegistryTests.cs:54:        var developerResult = registry.SelectProvider(policy, new MemoryProviderSelectionContext(AgentId: "agent-dev", AgentRole: "developer", WorkflowId: null, WorkflowNodeId: null, ProcessId: null));
tests\Memory\CanDoItAll.Memory.Tests\MemoryProviderRegistryTests.cs:55:        var analystResult = registry.SelectProvider(policy, new MemoryProviderSelectionContext(AgentId: "agent-ba", AgentRole: "business-analyst", WorkflowId: null, WorkflowNodeId: null, ProcessId: null));
tests\Integration\CanDoItAll.Tests.Integration\CognitiveMemoryProceduralPersistenceModelTests.cs:112:            CognitiveMemoryProcedureAutomationBindingKind.WorkflowExecutorGuidance,
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:3:using CanDoItAll.AgentFramework.ProviderPipelines;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:5:namespace CanDoItAll.Tests.Unit.AgentFramework.Providers.Pipelines;
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:222:        var projectFile = File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.ProviderPipelines/CanDoItAll.AgentFramework.ProviderPipelines.csproj"));
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:224:        Assert.Contains("CanDoItAll.AgentFramework.Models", projectFile, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:225:        Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", projectFile, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:232:                .EnumerateFiles(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.ProviderPipelines"), "*.cs")
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:234:        foreach (var forbidden in new[] { "Maf", "Modules.Workspace", "Blazor", "EntityFramework", "OpenAI", "OllamaSharp", "Comfy" })
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:246:            "src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:247:            "src/MAF/Common/CanDoItAll.AgentFramework.Voice/CanDoItAll.AgentFramework.Voice.csproj",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:248:            "src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
tests\Unit\CanDoItAll.Tests.Unit\AgentFramework\Providers\Pipelines\ProviderLocalBatchDispatcherTests.cs:255:            Assert.DoesNotContain("CanDoItAll.AgentFramework.ProviderPipelines", text, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\AgentImageGenerationAccessMetadataTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:6:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:7:using CanDoItAll.AgentFramework.Voice;
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:262:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:281:    public async Task AgentVoiceService_Transcribe_UsesProviderRuntimeSpeechDriver()
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:292:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:329:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:359:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:396:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:425:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:456:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:504:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:541:            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:618:        var runtimePool = new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:620:            new ProviderRuntimeHandleFactory(builder.Build()));
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:623:            new AgentVoiceDriverFactory(new ProviderRuntimeVoiceDriver(descriptorStore, runtimePool)));
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:635:        ProviderRuntimePool runtimePool,
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:775:    private sealed class InMemoryWorkflowSettingsService(AgentVoiceSettings voiceSettings) : IWorkflowSettingsService
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:777:        private WorkflowSettings settings = WorkflowSettings.Default with
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:782:        public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:787:        public Task<WorkflowSettings> SaveSettingsAsync(
tests\Unit\CanDoItAll.Tests.Unit\AgentVoiceTests.cs:788:            WorkflowSettings settings,
tests\Unit\CanDoItAll.Tests.Unit\AgentProviderModelParameterPolicyTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Memory\CanDoItAll.Memory.Tests\MemoryProtocolContractsTests.cs:34:                WorkflowId: "workflow-1",
tests\Memory\CanDoItAll.Memory.Tests\MemoryProtocolContractsTests.cs:35:                WorkflowNodeId: "node-query",
tests\Memory\CanDoItAll.Memory.Tests\MemoryProtocolContractsTests.cs:40:                AllowedSourceScopes: [MemorySourceScope.Project, MemorySourceScope.Workflow],
tests\Support\CanDoItAll.Tests.Support\TestSchemaBootstrapModules.cs:11:    ProjectStructureAgent = 1 << 4,
tests\Support\CanDoItAll.Tests.Support\TestSchemaBootstrapModules.cs:13:    Full = Workspace | Projects | PromptFactory | Workbench | ProjectStructureAgent
tests\Unit\CanDoItAll.Tests.Unit\AgentHandoffMetadataTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentProviderFailureDisplayFormatterTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentProviderFailureDisplayFormatterTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Memory\CanDoItAll.Memory.Tests\NativeRemoteMemoryProviderDriverTests.cs:155:                WorkflowId: "workflow-1",
tests\Memory\CanDoItAll.Memory.Tests\NativeRemoteMemoryProviderDriverTests.cs:156:                WorkflowNodeId: "node-1",
tests\Unit\CanDoItAll.Tests.Unit\AgentToolInvocationPolicyTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentToolInvocationPolicyTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentToolInvocationPolicyTests.cs:2761:        foreach (var toolName in ToolContractCatalog.FinalizerToolNames)
tests\Unit\CanDoItAll.Tests.Unit\AgentToolInvocationPolicyTests.cs:2922:            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowAddOptions,
tests\Unit\CanDoItAll.Tests.Unit\AgentToolInvocationPolicyTests.cs:2923:            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStatusGet,
tests\Unit\CanDoItAll.Tests.Unit\AgentToolInvocationPolicyTests.cs:2956:            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowDefinitionCreate,
tests\Unit\CanDoItAll.Tests.Unit\AgentToolInvocationPolicyTests.cs:2957:            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart,
tests\Memory\CanDoItAll.Memory.Tests\MemoryOperationHandlerTests.cs:24:        var executor = new FakeWorkflowExecutorMemoryRoute(handler);
tests\Memory\CanDoItAll.Memory.Tests\MemoryOperationHandlerTests.cs:39:        Assert.Equal(MemoryOperationCallerKind.WorkflowExecutor, executorResult.OperationRecord.Extensions.GetMemoryOperationCaller()?.Kind);
tests\Memory\CanDoItAll.Memory.Tests\MemoryOperationHandlerTests.cs:140:        yield return [MemoryOperationCaller.WorkflowExecutor("workflow.executor.memory-query", CreateRequester())];
tests\Memory\CanDoItAll.Memory.Tests\MemoryOperationHandlerTests.cs:217:            WorkflowId: "workflow-1",
tests\Memory\CanDoItAll.Memory.Tests\MemoryOperationHandlerTests.cs:218:            WorkflowNodeId: "node-1",
tests\Memory\CanDoItAll.Memory.Tests\MemoryOperationHandlerTests.cs:242:    private sealed class FakeWorkflowExecutorMemoryRoute(IMemoryOperationHandler handler)
tests\Memory\CanDoItAll.Memory.Tests\MemoryOperationHandlerTests.cs:249:                    MemoryOperationCaller.WorkflowExecutor("workflow.executor.memory-query", CreateRequester()),
tests\Unit\CanDoItAll.Tests.Unit\AgentOutputContractTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentOutputContractTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentOutputContractTests.cs:306:                    OwnedPaths = ["src/MAF/Common/CanDoItAll.AgentFramework.Core"],
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:67:                WorkflowId: null,
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:68:                WorkflowNodeId: null,
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:75:                WorkflowId: null,
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:76:                WorkflowNodeId: null,
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:187:            "CanDoItAll.AgentFramework.Rag.Qdrant",
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:188:            "CanDoItAll.AgentFramework.SemanticCompletion.Driver"
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:310:            WorkflowId: "workflow-32",
tests\Memory\CanDoItAll.Memory.Tests\MemoryTestSuiteRebalanceCheckpointTests.cs:311:            WorkflowNodeId: "node-32",
tests\Unit\CanDoItAll.Tests.Unit\AgentReferenceDataProviderTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentReferenceDataProviderTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentReferenceDataProviderTests.cs:134:        IReadOnlyList<ProviderProfile> providers) : IAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\AgentReferenceDataProviderTests.cs:263:        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkRuntimeToolReceiptTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkRuntimeToolReceiptTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkRuntimeToolReceiptTests.cs:6:public sealed class AgentFrameworkRuntimeToolReceiptTests
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkRuntimeToolReceiptTests.cs:54:        var receipts = AgentFrameworkWorkspaceExecutionService.CreateRuntimeProviderToolReceipts(run, response);
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:3:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:4:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:5:using CanDoItAll.AgentFramework.Mcp;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:6:using CanDoItAll.AgentFramework.Mcp.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:7:using CanDoItAll.AgentFramework.Skills;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:8:using CanDoItAll.AgentFramework.Skills.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:9:using CanDoItAll.AgentFramework.Tools;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:10:using CanDoItAll.AgentFramework.Tools.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:185:                CapabilityAccessScope.WorkflowNode,
tests\Unit\CanDoItAll.Tests.Unit\CapabilityFoundationHardeningTests.cs:187:                "Workflow node forbids external capabilities.")
tests\Memory\CanDoItAll.Memory.Tests\MemoryMcpDriverTests.cs:2:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Memory\CanDoItAll.Memory.Tests\MemoryMcpDriverTests.cs:3:using CanDoItAll.AgentFramework.Mcp.Abstractions;
tests\Memory\CanDoItAll.Memory.Tests\MemoryMcpDriverTests.cs:254:                WorkflowId: "workflow-alpha",
tests\Memory\CanDoItAll.Memory.Tests\MemoryMcpDriverTests.cs:255:                WorkflowNodeId: "node-alpha",
tests\Memory\CanDoItAll.Memory.Tests\MemorySourceGatewayTests.cs:1:using AgentCore = CanDoItAll.AgentFramework.Core;
tests\Memory\CanDoItAll.Memory.Tests\MemorySourceGatewayTests.cs:60:            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkflowRuntime]));
tests\Memory\CanDoItAll.Memory.Tests\MemorySourceGatewayTests.cs:76:            AgentCore.MemorySourceKind.WorkflowRuntime,
tests\Memory\CanDoItAll.Memory.Tests\MemorySourceGatewayTests.cs:77:            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkflowRuntime]));
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:482:            "Workflow answer was accepted by the operator."));
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:554:            "Workflow answer was accepted by the operator.");
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:1724:        var workspace = new FakeAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:1767:        var workspace = new FakeAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:1824:        var workspace = new FakeAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:1870:            new FakeAgentFrameworkWorkspaceService());
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:1883:            new FakeAgentFrameworkWorkspaceService());
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:3218:        IAgentFrameworkWorkspaceService? workspaceService = null)
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:3223:            workspaceService ?? new FakeAgentFrameworkWorkspaceService(),
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:3322:    private sealed class FakeAgentFrameworkWorkspaceService : IAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:3540:        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default)
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs:3541:            => Task.FromResult<IReadOnlyList<ExecutionWorkflowCheckpointRecord>>([]);
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:2:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:3:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:4:using CanDoItAll.AgentFramework.Capabilities.Templates;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:5:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:6:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:7:using CapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:202:                CapabilityAccessScope.WorkflowNode,
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:204:                "Workflow node forbids external servers.")
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:247:                CapabilityAccessScope.WorkflowNode,
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:249:                "Workflow node forbids browser MCP server attachment."),
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:253:                CapabilityAccessScope.WorkflowNode,
tests\Unit\CanDoItAll.Tests.Unit\CapabilityContractsTests.cs:255:                "Workflow node forbids search MCP tool attachment.")
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkProcessRuntimeCancellationObserverTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkProcessRuntimeCancellationObserverTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkProcessRuntimeCancellationObserverTests.cs:10:public sealed class AgentFrameworkProcessRuntimeCancellationObserverTests
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkProcessRuntimeCancellationObserverTests.cs:20:        var observer = new AgentFrameworkProcessRuntimeCancellationObserver(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkProcessRuntimeCancellationObserverTests.cs:23:            NullLogger<AgentFrameworkProcessRuntimeCancellationObserver>.Instance);
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkProcessRuntimeCancellationObserverTests.cs:42:        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("Marked 1 AgentFramework execution run", StringComparison.Ordinal));
tests\Memory\CanDoItAll.Memory.Tests\MemorySourceGatewayHardeningCheckpointTests.cs:32:            "CanDoItAll.Modules.AgentFramework",
tests\Memory\CanDoItAll.Memory.Tests\MemorySourceGatewayHardeningCheckpointTests.cs:40:            "IWorkflowRuntimeEvidenceSourceProvider",
tests\Memory\CanDoItAll.Memory.Tests\MemorySourceGatewayHardeningCheckpointTests.cs:71:            "CanDoItAll.AgentFramework.Core",
tests\Support\CanDoItAll.Tests.Support\TestFileSystem.cs:32:                GC.WaitForPendingFinalizers();
tests\Support\CanDoItAll.Tests.Support\TestFileSystem.cs:39:                GC.WaitForPendingFinalizers();
tests\Integration\CanDoItAll.Tests.Integration\CognitiveMemoryWorkspacePersistenceModelTests.cs:53:            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.WorkflowRun, workflowRunId: Guid.Parse("10000000-0000-0000-0000-000000000001")),
tests\Memory\CanDoItAll.Memory.Tests\MemoryLedgerLifecycleTests.cs:186:            WorkflowId: "workflow-1",
tests\Memory\CanDoItAll.Memory.Tests\MemoryLedgerLifecycleTests.cs:187:            WorkflowNodeId: "node-query",
tests\Memory\CanDoItAll.Memory.Tests\MemoryRuntimePersistenceTests.cs:260:                CanDoItAll.AgentFramework.Core.MemorySourceKind.WorkbenchProjectStructure,
tests\Memory\CanDoItAll.Memory.Tests\MemoryRuntimePersistenceTests.cs:265:                MemorySourceGatewayPolicy.Allow([CanDoItAll.AgentFramework.Core.MemorySourceKind.WorkbenchProjectStructure]),
tests\Memory\CanDoItAll.Memory.Tests\MemoryRuntimePersistenceTests.cs:280:            WorkflowId: null,
tests\Memory\CanDoItAll.Memory.Tests\MemoryRuntimePersistenceTests.cs:281:            WorkflowNodeId: null,
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:4:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:5:using CanDoItAll.AgentFramework.Capabilities.Templates;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:6:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:7:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:8:using CanDoItAll.AgentFramework.Persistence;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:31:        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.Tool, byKey["workspace-dotnet-test"].Kind);
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:32:        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer, byKey["playwright-local-mcp"].Kind);
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:33:        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.Skill, byKey["aspnet-core-skill"].Kind);
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:34:        Assert.Equal(CanDoItAll.AgentFramework.Models.CapabilityKind.Rag, byKey["workspace-source-rag"].Kind);
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedMaterializationTests.cs:162:                capability.Kind == CanDoItAll.AgentFramework.Models.CapabilityKind.Tool &&
tests\Unit\CanDoItAll.Tests.Unit\BrowserMcpArtifactPathServiceTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\BrowserMcpArtifactPathServiceTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:2:using CanDoItAll.AgentFramework.Hosting;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:9:public sealed class AgentFrameworkHostingServiceCollectionTests
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:12:    public async Task AddAgentFrameworkCore_builds_with_scope_validation()
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:22:            services.AddAgentFrameworkCore(workspaceRoot);
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:31:            Assert.IsType<MafWorkflowCompiler>(scope.ServiceProvider.GetRequiredService<IWorkflowMafCompiler>());
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:32:            Assert.IsType<CompositeWorkflowExecutorExecutionObserver>(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:33:                scope.ServiceProvider.GetRequiredService<IWorkflowExecutorExecutionObserver>());
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:34:            var backendCatalog = scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeBackendCatalog>();
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:35:            var inProcessBackend = backendCatalog.GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:49:    public async Task AddAgentFrameworkCore_catalog_service_rejects_unknown_executor()
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:59:            services.AddAgentFrameworkCore(workspaceRoot);
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:67:            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:70:                new WorkflowDefinitionSaveRequest(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:75:                    WorkflowLifecycleStatus.Active,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:76:                    CreateExecutorWorkflowGraph(new WorkflowExecutorId("missing.executor")),
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:77:                    WorkflowSettings.Default.DefaultRuntimePolicy)));
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:90:    private static WorkflowGraph CreateExecutorWorkflowGraph(WorkflowExecutorId executorId)
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:92:        var start = new WorkflowNodeId("start");
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:93:        var tool = new WorkflowNodeId("tool");
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:94:        var end = new WorkflowNodeId("end");
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:95:        return new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:98:                CreateNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:101:                    WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:104:                    new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:110:                        InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:111:                        ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")) with
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:115:                        ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:117:                CreateNode(end, WorkflowNodeKind.End, inputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:125:    private static WorkflowNode CreateNode(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:126:        WorkflowNodeId id,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:127:        WorkflowNodeKind kind,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:128:        WorkflowValueShape? inputShape = null,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:129:        WorkflowValueShape? resultShape = null)
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:135:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:144:    private static WorkflowEdge CreateEdge(
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:146:        WorkflowNodeId source,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:147:        WorkflowNodeId target)
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:149:            new WorkflowEdgeId(id),
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:154:            WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\AgentFrameworkHostingServiceCollectionTests.cs:157:            Routing = WorkflowEdgeRouting.Always
tests\Integration\CanDoItAll.Tests.Integration\CognitiveMemorySourceIngestionPersistenceTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\CognitiveMemorySourceIngestionPersistenceTests.cs:224:        var workflowProvider = new FakeWorkflowRuntimeEvidenceSourceProvider();
tests\Memory\CanDoItAll.Memory.Tests\MemoryRuntimeCheckpointTests.cs:22:            "CanDoItAll.AgentFramework.Rag"
tests\Memory\CanDoItAll.Memory.Tests\MemoryRuntimeCheckpointTests.cs:186:            WorkflowId: null,
tests\Memory\CanDoItAll.Memory.Tests\MemoryRuntimeCheckpointTests.cs:187:            WorkflowNodeId: null,
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedHardeningCheckpointTests.cs:4:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedHardeningCheckpointTests.cs:5:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedHardeningCheckpointTests.cs:6:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedHardeningCheckpointTests.cs:7:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedHardeningCheckpointTests.cs:8:using CanDoItAll.AgentFramework.Persistence;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedHardeningCheckpointTests.cs:9:using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityTemplateSeedHardeningCheckpointTests.cs:10:using SeedCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;
tests\Playwright\CanDoItAll.Tests.Playwright\WorkflowShellSmokeTests.cs:6:public sealed class WorkflowShellSmokeTests
tests\Playwright\CanDoItAll.Tests.Playwright\WorkflowShellSmokeTests.cs:10:    public WorkflowShellSmokeTests(PlaywrightAppFixture fixture)
tests\Playwright\CanDoItAll.Tests.Playwright\WorkflowShellSmokeTests.cs:16:    public async Task Workflow_shell_creates_and_runs_starter_preview_on_large_screen()
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalServicesTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalServicesTests.cs:2:using CanDoItAll.AgentFramework.Rag.Driver.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalServicesTests.cs:3:using CanDoItAll.AgentFramework.Rag.Driver.Models;
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:10:        var source = ReadRepositoryFile("src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs");
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:24:        var source = ReadRepositoryFiles("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities");
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:35:        var accessSource = ReadRepositoryFiles("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities");
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:36:        var runtimeProviderComposerSource = ReadRepositoryFile("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs");
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:37:        var policySource = ReadRepositoryFile("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs");
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:54:        var mafSource = ReadRepositoryFiles("src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities");
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:55:        var evaluatorSource = ReadRepositoryFile("src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs");
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:70:            ReadRepositoryFile("src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs"),
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:71:            ReadRepositoryFile("src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalHttpToolInvoker.cs"),
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:72:            ReadRepositoryFile("src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ToolDiagnostics.cs"),
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:73:            ReadRepositoryFile("src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/McpSetupTestService.cs"),
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:74:            ReadRepositoryFile("src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Diagnostics/McpDiagnostics.cs"));
tests\Unit\CanDoItAll.Tests.Unit\CapabilityMigrationCleanupGuardTests.cs:81:        Assert.Contains("MaskedDetail", ReadRepositoryFile("src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs"), StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\AgentWorkspaceToolAccessMetadataTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\AgentWorkspaceToolAccessMetadataTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:16:public sealed class EmailWorkflowSwitchScenarioTests
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:25:        => await RunEmailWorkflowSwitchValidationAsync(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:42:            await RunEmailWorkflowSwitchValidationAsync(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:55:    private static async Task RunEmailWorkflowSwitchValidationAsync(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:70:        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:73:            ConfigureEmailWorkflowServices);
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:79:        var definition = await PostAndReadAsync<WorkflowDefinition>(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:82:            CreateEmailWorkflowDefinitionSaveRequest(component.Id));
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:88:            edge => edge.Routing.Kind == WorkflowRouteKind.SwitchCase &&
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:93:            edge => edge.Routing.Kind == WorkflowRouteKind.SwitchCase &&
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:95:        Assert.Contains(definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:130:                new EmailWorkflowProof(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:138:    private static void ConfigureEmailWorkflowServices(IServiceCollection services)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:140:        services.RemoveAll<IWorkflowLlmComponentInvoker>();
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:141:        services.AddSingleton<IWorkflowLlmComponentInvoker, EmailWorkflowLlmInvoker>();
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:145:        ProjectStructureAgentApiTestHost host,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:148:        WorkflowDefinition definition,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:162:        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:172:        var options = await PostAndReadAsync<ProjectStructureWorkflowAddOptionsResult>(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:175:            new ProjectStructureWorkflowAddOptionsInput(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:188:        var workflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:191:            new ProjectStructureWorkflowNodeCreateInput(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:197:        var started = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:200:            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: leaseToken));
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:202:        Assert.Equal(WorkflowRunState.Completed, started.Status.State);
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:206:        var status = await GetAndReadAsync<ProjectStructureWorkflowRunStatus>(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:268:            Modality: WorkflowModality.Text,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:269:            ModelSettings: new WorkflowModelSettings(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:313:    private static WorkflowDefinitionSaveRequest CreateEmailWorkflowDefinitionSaveRequest(WorkflowComponentId componentId)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:316:            new WorkflowSourceIngestionExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:330:            new WorkflowProjectStructureExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:332:                Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:341:            new WorkflowProjectStructureExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:343:                Operation = WorkflowProjectStructureOperation.CreateAsset,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:352:        var nodes = new List<WorkflowNode>
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:354:            CreateWorkflowNode("start", WorkflowNodeKind.Start),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:355:            CreateExecutorWorkflowNode("ingest-email-sources", WorkflowExecutorIds.SourceIngestion, sourceSettingsJson),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:356:            CreateLlmWorkflowNode("classify-email", componentId),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:357:            CreateWorkflowNode("email-switch", WorkflowNodeKind.Triage),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:358:            CreateExecutorWorkflowNode("create-email-task-nodes", WorkflowExecutorIds.ProjectStructure, taskSettingsJson, CreateJsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:359:            CreateExecutorWorkflowNode("create-asap-response-task", WorkflowExecutorIds.ProjectStructure, taskSettingsJson, CreateJsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:360:            CreateExecutorWorkflowNode("store-informative-summary", WorkflowExecutorIds.ProjectStructure, assetSettingsJson, CreateJsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:361:            CreateExecutorWorkflowNode("store-default-summary", WorkflowExecutorIds.ProjectStructure, assetSettingsJson, CreateJsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:362:            CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateJsonShape())
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:364:        var edges = new List<WorkflowEdge>
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:366:            CreateWorkflowEdge("start-to-ingest", "start", "ingest-email-sources"),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:367:            CreateWorkflowEdge("ingest-to-classify", "ingest-email-sources", "classify-email"),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:368:            CreateWorkflowEdge("classify-to-switch", "classify-email", "email-switch"),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:369:            CreateWorkflowEdge("switch-to-tasks", "email-switch", "create-email-task-nodes", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchCase("$.route", "\"tasks\"", WorkflowRouteValueKind.String, "Tasks")),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:370:            CreateWorkflowEdge("switch-to-asap", "email-switch", "create-asap-response-task", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchCase("$.route", "\"asap_response\"", WorkflowRouteValueKind.String, "ASAP response")),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:371:            CreateWorkflowEdge("switch-to-info", "email-switch", "store-informative-summary", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchCase("$.route", "\"informative\"", WorkflowRouteValueKind.String, "Informative")),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:372:            CreateWorkflowEdge("switch-to-default", "email-switch", "store-default-summary", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchDefault("DEFAULT")),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:373:            CreateWorkflowEdge("tasks-to-end", "create-email-task-nodes", "end"),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:374:            CreateWorkflowEdge("asap-to-end", "create-asap-response-task", "end"),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:375:            CreateWorkflowEdge("info-to-end", "store-informative-summary", "end"),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:376:            CreateWorkflowEdge("default-to-end", "store-default-summary", "end")
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:379:        return new WorkflowDefinitionSaveRequest(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:384:            Status: WorkflowLifecycleStatus.Active,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:385:            Graph: new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:386:            RuntimePolicy: new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:387:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:394:    private static WorkflowNode CreateWorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:396:        WorkflowNodeKind kind,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:397:        WorkflowValueShape? inputShape = null,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:398:        WorkflowValueShape? resultShape = null)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:400:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:404:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:413:    private static WorkflowNode CreateExecutorWorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:415:        WorkflowExecutorId executorId,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:417:        WorkflowValueShape? inputShape = null)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:419:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:420:            WorkflowNodeKind.Executor,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:423:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:434:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:441:    private static WorkflowNode CreateLlmWorkflowNode(string id, WorkflowComponentId componentId)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:443:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:444:            WorkflowNodeKind.LlmCall,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:447:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:456:    private static WorkflowEdge CreateWorkflowEdge(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:460:        WorkflowEdgeKind kind = WorkflowEdgeKind.Direct,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:461:        WorkflowEdgeRouting? routing = null)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:463:            new WorkflowEdgeId(id),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:464:            new WorkflowNodeId(source),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:466:            new WorkflowNodeId(target),
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:471:            Routing = routing ?? WorkflowEdgeRouting.Always
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:474:    private static WorkflowValueShape CreateJsonShape()
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:475:        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:477:    private static async Task<WorkflowValidationResult> ValidateDefinitionAsync(HttpClient client, WorkflowDefinition definition)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:482:        return JsonSerializer.Deserialize<WorkflowValidationResult>(body, JsonOptions)
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:731:    private sealed class EmailWorkflowLlmInvoker : IWorkflowLlmComponentInvoker
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:733:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:734:            WorkflowDefinition definition,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:735:            WorkflowNode node,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:737:            WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:774:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:1045:    private sealed record EmailWorkflowProof(
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:1047:        Guid WorkflowId,
tests\Integration\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs:1048:        Guid WorkflowVersionId,
tests\Support\CanDoItAll.Tests.Support\TestApplicationBootstrap.cs:8:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\FileSandboxWorkspaceStoreLockIntegrationTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\FileSandboxWorkspaceStoreLockIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\FileSandboxWorkspaceStoreLockIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Persistence;
tests\Integration\CanDoItAll.Tests.Integration\FileSandboxWorkspaceStoreLockIntegrationTests.cs:4:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\MafAgentRuntimeHandoffTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Integration\CanDoItAll.Tests.Integration\MafAgentRuntimeHandoffTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\MafAgentRuntimeHandoffTests.cs:6:using Microsoft.Agents.AI.Workflows;
tests\Integration\CanDoItAll.Tests.Integration\MafAgentRuntimeHandoffTests.cs:11:public sealed class MafAgentRuntimeHandoffTests
tests\Integration\CanDoItAll.Tests.Integration\MafAgentRuntimeHandoffTests.cs:22:        var build = MafHandoffWorkflowFactory.Build(
tests\Integration\CanDoItAll.Tests.Integration\MafAgentRuntimeHandoffTests.cs:50:        var build = MafHandoffWorkflowFactory.Build(
tests\Integration\CanDoItAll.Tests.Integration\DatabaseMigrationIntegrationTests.cs:12:    private const string WorkflowCheckpointsMigrationId = "20260529111314_AddWorkflowCheckpoints";
tests\Integration\CanDoItAll.Tests.Integration\DatabaseMigrationIntegrationTests.cs:25:    private const string GenericMemoryProviderRuntimeMigrationId = "20260705163628_GenericMemoryProviderRuntime";
tests\Integration\CanDoItAll.Tests.Integration\DatabaseMigrationIntegrationTests.cs:34:        WorkflowCheckpointsMigrationId,
tests\Integration\CanDoItAll.Tests.Integration\DatabaseMigrationIntegrationTests.cs:47:        GenericMemoryProviderRuntimeMigrationId,
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryProjectionAdapterTests.cs:1:using CanDoItAll.AgentFramework.Rag.Driver.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryProjectionAdapterTests.cs:2:using CanDoItAll.AgentFramework.Rag.Driver.Models;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryProjectionAdapterTests.cs:3:using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Embeddings;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryProjectionAdapterTests.cs:4:using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Semantics;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryConsolidationEngineTests.cs:323:        await SeedSourceAsync(fixture, projectId, "WorkflowRuntime", "WorkflowRun", "Workflow decision approved a deployment plan.", withEvidence: true);
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryProcedureSkillMemoryTests.cs:60:            CognitiveMemoryProcedureAutomationBindingKind.WorkflowTemplate,
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryProcedureSkillMemoryTests.cs:73:            CognitiveMemoryProcedureAutomationBindingKind.MafProcedureGuidance,
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemorySignalLedgerTests.cs:143:            CognitiveMemorySignalSourceKind.WorkflowRun,
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemorySignalLedgerTests.cs:203:            CognitiveMemorySignalSourceKind.WorkflowRun,
tests\Integration\CanDoItAll.Tests.Integration\LiveSpecialistAgentScenarioIntegrationTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\LiveSpecialistAgentScenarioIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\LiveSpecialistAgentScenarioIntegrationTests.cs:3:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\LiveSpecialistAgentScenarioIntegrationTests.cs:138:        IAgentFrameworkWorkspaceService workspaceService,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:5:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:6:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:25:            plugin.Descriptor.WorkflowExecutors,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:28:                        executor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsExternalData) &&
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:29:                        executor.SideEffects.Kind == WorkflowExecutorSideEffectKind.ExternalRead &&
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:32:            plugin.Descriptor.WorkflowExecutors,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:34:                        executor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.IdempotentExternalMarker) &&
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:35:                        executor.SideEffects.ExternalMutationKind == WorkflowExecutorExternalMutationKind.ProcessedMarker &&
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:39:            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor) &&
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:40:                          descriptor.ImplementationType == typeof(Office365DownloadByAddressWorkflowExecutor));
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:48:            new WorkflowNodeInput("""
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:56:            new GmailWorkflowExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:88:            new WorkflowNodeInput("""
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:96:            new Office365WorkflowExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:122:            new WorkflowNodeInput("""
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:132:            new Office365MessageAddressWorkflowExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:162:            new WorkflowNodeInput("""{"emailAddress":"sender@example.test"}"""),
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:163:            new Office365MessageAddressWorkflowExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:188:        var payload = InvokeMarkProcessedPayloadFactory<GmailMarkProcessedWorkflowExecutor, GmailMessageLabelMutationResult>(
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:189:            new WorkflowNodeInput("""{"runContext":{"gmailProcessing":{"idempotencyKey":"gmail:msg-1"}}}"""),
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:215:        var payload = InvokeMarkProcessedPayloadFactory<Office365MarkProcessedWorkflowExecutor, Office365MessageCategoryMutationResult>(
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:216:            new WorkflowNodeInput("""{"runContext":{"office365Processing":{"idempotencyKey":"office365:graph-1"}}}"""),
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:244:        var settings = new Office365MessageAddressWorkflowExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:255:            new WorkflowNodeInput("""
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:911:        WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:912:        GmailWorkflowExecutorSettings settings,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:915:        => InvokeDownloadPayloadFactory<GmailDownloadByLabelWorkflowExecutor>(
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:922:        WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:923:        Office365WorkflowExecutorSettings settings,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:926:        => InvokeDownloadPayloadFactory<Office365DownloadByCategoryWorkflowExecutor>(
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:933:        WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:934:        Office365MessageAddressWorkflowExecutorSettings settings,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:937:        => InvokeDownloadPayloadFactory<Office365DownloadByAddressWorkflowExecutor>(
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:944:        Office365MessageAddressWorkflowExecutorSettings settings,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:945:        WorkflowNodeInput input)
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:946:        => (Office365MessageAddressFilterSettings)(typeof(Office365DownloadByAddressWorkflowExecutor).GetMethod(
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:953:        WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\EmailPluginClientTests.cs:964:        WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryCommonGuardrailTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:4:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:26:            var executionServiceField = typeof(AgentFrameworkWorkspaceService).GetField(
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:29:                ?? throw new InvalidOperationException("AgentFrameworkWorkspaceService.executionService field was not found.");
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:64:            var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:104:        var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
tests\Integration\CanDoItAll.Tests.Integration\ManagedSeedExecutionFallbackIntegrationTests.cs:138:        var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryTemporalReplaySchedulerTests.cs:49:            CognitiveMemoryActorKind.WorkflowExecutor,
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryTemporalReplaySchedulerTests.cs:82:        Assert.Equal(CognitiveMemoryActorKind.WorkflowExecutor, steps[1].ActorKind);
tests\Unit\CanDoItAll.Tests.Unit\ContextualAgentWorkspaceContextBuilderTests.cs:1:using CanDoItAll.AgentFramework.Components;
tests\Unit\CanDoItAll.Tests.Unit\ContextualAgentWorkspaceContextBuilderTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:8:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:9:using CanDoItAll.AgentFramework.Maf;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:10:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:12:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:138:                services.Configure<WorkflowExampleCatalogSeedOptions>(options =>
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:151:        await GrantAsync(host, Office365PluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:154:        var definitions = await ReadWorkflowDefinitionsAsync(host);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:158:        Assert.Equal(WorkflowLifecycleStatus.Active, office365Summary.Status);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:163:            new ProjectStructureWorkflowAddOptionsInput());
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:166:        var options = JsonSerializer.Deserialize<ProjectStructureWorkflowAddOptionsResult>(optionsBody, JsonOptions)!;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:168:        var workflowOption = Assert.Single(options.Workflows, item => item.WorkflowId == office365Summary.Id);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:351:                PluginCapabilityKind.WorkflowExecutor,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:353:                    new PluginWorkflowExecutorDescriptor(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:354:                        RuntimePackageFixtureWorkflowExecutor.ExecutorId,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:357:                        WorkflowExecutorCategoryKind.Utility,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:360:                        WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:361:                        WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:362:                        WorkflowExecutorExecutionPolicy.Default)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:389:        var executorCatalog = scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:393:        var executor = Assert.Single(executors, item => item.Id == RuntimePackageFixtureWorkflowExecutor.ExecutorId);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:397:        Assert.Equal(WorkflowExecutorSourceKind.LocalPackage, executor.Source.Kind);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:428:            WorkflowExecutorId: new WorkflowExecutorId("integration.logs.executor")));
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:450:    public void Workflow_executor_observer_registration_composes_plugin_sink_regardless_module_order(bool pluginsFirst)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:463:            services.AddAgentFrameworkModule(configuration);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:467:            services.AddAgentFrameworkModule(configuration);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:472:            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorExecutionObserver))
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:475:            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorExecutionAuditSink))
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:478:        Assert.Contains(observerDescriptors, descriptor => descriptor.ImplementationType == typeof(CompositeWorkflowExecutorExecutionObserver));
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:479:        Assert.DoesNotContain(observerDescriptors, descriptor => descriptor.ImplementationType == typeof(PluginWorkflowExecutorExecutionObserver));
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:480:        Assert.Contains(sinkDescriptors, descriptor => descriptor.ImplementationType == typeof(PluginWorkflowExecutorExecutionObserver));
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:481:        Assert.Single(sinkDescriptors, descriptor => descriptor.ImplementationType == typeof(PluginWorkflowExecutorExecutionObserver));
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:487:        WorkflowExecutorDescriptor descriptor,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:490:        var executor = CreateThrowingWorkflowExecutor(descriptor, out var invocationProbe);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:491:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:492:        var compiler = new MafWorkflowCompiler(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:493:            new WorkflowDefinitionValidator(catalog),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:494:            new WorkflowExecutorInvoker(catalog, [executor]));
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:495:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, Array.Empty<LlmCallComponent>());
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:497:        var definition = CreateSingleExecutorSimulationWorkflow(node);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:498:        var request = new WorkflowRunStartRequest(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:502:            WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:506:            PreviewSimulationPlan = new WorkflowPreviewSimulationPlan(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:508:                new WorkflowPreviewSimulationStep(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:516:        var result = await backend.StartAsync(definition, request, WorkflowRunId.New());
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:519:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:770:        var resolved = await oauthService.ResolveWorkflowConnectionIdAsync(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:787:            oauthService.ResolveWorkflowConnectionIdAsync(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:851:            Capabilities = PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.HostCommand
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:862:        var missingWorkflowGrant = evaluator.Evaluate(descriptor.Id, PluginCapabilityKind.WorkflowExecutor);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:865:            new PluginGrantUpdateRequest(PluginCapabilityKind.WorkflowExecutor, PluginGrantState.Granted),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:867:        var allowedWorkflowGrant = evaluator.Evaluate(descriptor.Id, PluginCapabilityKind.WorkflowExecutor);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:885:        Assert.False(missingWorkflowGrant.Allowed);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:886:        Assert.Equal(PluginGrantDecisionKind.GrantMissing, missingWorkflowGrant.Kind);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:888:        Assert.True(allowedWorkflowGrant.Allowed);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:914:            var executorCatalog = scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:934:            var executorCatalog = scope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:943:                new PluginGrantUpdateRequest(PluginCapabilityKind.WorkflowExecutor, PluginGrantState.Granted),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:960:            Assert.Contains(settings!.Grants, item => item.Capability == PluginCapabilityKind.WorkflowExecutor);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:963:            Assert.Equal(WorkflowExecutorSourceKind.LocalPackage, initialDockerStart.Source.Kind);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:972:        var updatedCatalog = verifiedScope.ServiceProvider.GetRequiredService<IWorkflowExecutorCatalog>();
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:992:                services.RemoveAll<IWorkflowLlmComponentInvoker>();
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:993:                services.AddScoped<IWorkflowLlmComponentInvoker, DockerLogSummaryLlmInvoker>();
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:997:        var workflow = CreateDockerProofWorkflow(component.Id);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1001:            new WorkflowTestRunRequest(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1002:                WorkflowId: null,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1006:                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1010:        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions)!;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1014:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1030:                new WorkflowExecutorPermissionPolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1031:                    WorkflowExecutorCapabilityFlags.ReadsExternalData |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1032:                    WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1033:                    WorkflowExecutorCapabilityFlags.UsesSecrets |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1034:                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1035:                    WorkflowExecutorApprovalRequirement.NotRequired),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1036:                ReadSimulationTemplate(typeof(GmailPluginConstants), "GmailWorkflowSimulationTemplates", "DownloadByLabel")),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1047:                new WorkflowExecutorPermissionPolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1048:                    WorkflowExecutorCapabilityFlags.WritesExternalData |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1049:                    WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1050:                    WorkflowExecutorCapabilityFlags.UsesSecrets |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1051:                    WorkflowExecutorCapabilityFlags.IdempotentExternalMarker |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1052:                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1053:                    WorkflowExecutorApprovalRequirement.NotRequired),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1054:                ReadSimulationTemplate(typeof(GmailPluginConstants), "GmailWorkflowSimulationTemplates", "MarkProcessed")),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1065:                new WorkflowExecutorPermissionPolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1066:                    WorkflowExecutorCapabilityFlags.ReadsExternalData |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1067:                    WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1068:                    WorkflowExecutorCapabilityFlags.UsesSecrets |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1069:                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1070:                    WorkflowExecutorApprovalRequirement.NotRequired),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1071:                ReadSimulationTemplate(typeof(Office365PluginConstants), "Office365WorkflowSimulationTemplates", "DownloadByCategory")),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1082:                new WorkflowExecutorPermissionPolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1083:                    WorkflowExecutorCapabilityFlags.ReadsExternalData |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1084:                    WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1085:                    WorkflowExecutorCapabilityFlags.UsesSecrets |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1086:                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1087:                    WorkflowExecutorApprovalRequirement.NotRequired),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1088:                ReadSimulationTemplate(typeof(Office365PluginConstants), "Office365WorkflowSimulationTemplates", "DownloadByAddress")),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1099:                new WorkflowExecutorPermissionPolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1100:                    WorkflowExecutorCapabilityFlags.WritesExternalData |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1101:                    WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1102:                    WorkflowExecutorCapabilityFlags.UsesSecrets |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1103:                    WorkflowExecutorCapabilityFlags.IdempotentExternalMarker |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1104:                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1105:                    WorkflowExecutorApprovalRequirement.NotRequired),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1106:                ReadSimulationTemplate(typeof(Office365PluginConstants), "Office365WorkflowSimulationTemplates", "MarkProcessed")),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1117:                new WorkflowExecutorPermissionPolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1118:                    WorkflowExecutorCapabilityFlags.RunsHostCommand |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1119:                    WorkflowExecutorCapabilityFlags.EmitsArtifacts |
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1120:                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1121:                    WorkflowExecutorApprovalRequirement.AlwaysRequired),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1122:                ReadSimulationTemplate(typeof(DockerPluginConstants), "DockerWorkflowSimulationTemplates", "CommandResult")),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1127:    private static WorkflowExecutorSimulationDescriptor ReadSimulationTemplate(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1137:        return (WorkflowExecutorSimulationDescriptor)(property.GetValue(null)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1141:    private static WorkflowExecutorDescriptor CreatePluginSimulationDescriptor(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1142:        WorkflowExecutorId executorId,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1147:        WorkflowExecutorPermissionPolicy permissionPolicy,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1148:        WorkflowExecutorSimulationDescriptor simulation)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1153:            WorkflowExecutorCategoryKind.Data,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1156:            WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1160:            WorkflowExecutorExecutionPolicy.Default,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1163:            Source = WorkflowExecutorSourceDescriptor.BundledPlugin(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1169:            SideEffects = permissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.WritesExternalData)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1170:                ? WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1173:                : permissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsExternalData)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1174:                    ? WorkflowExecutorSideEffectDescriptor.ExternalRead("workflow-email-external-read/v1")
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1175:                    : WorkflowExecutorSideEffectDescriptor.None,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1176:            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Scenario06 fake-mode preview proof."),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1180:    private static WorkflowDefinition CreateSingleExecutorSimulationWorkflow(WorkflowNode executorNode)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1183:        return new WorkflowDefinition(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1184:            WorkflowId.New(),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1185:            WorkflowVersionId.New(),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1188:            WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1189:            new WorkflowGraph(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1190:                new WorkflowNodeId("start"),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1192:                    CreateNode("start", WorkflowNodeKind.Start),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1194:                    CreateNode("end", WorkflowNodeKind.End, inputShape: JsonShape(), resultShape: JsonShape())
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1200:            new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1201:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1210:    private static WorkflowNode CreateSimulationExecutorNode(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1212:        WorkflowExecutorDescriptor descriptor)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1214:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1215:            WorkflowNodeKind.Executor,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1218:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1224:                InputShape: WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1232:    private static IWorkflowExecutor CreateThrowingWorkflowExecutor(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1233:        WorkflowExecutorDescriptor descriptor,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1234:        out WorkflowExecutorInvocationProbe invocationProbe)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1236:        var executor = DispatchProxy.Create<IWorkflowExecutor, WorkflowExecutorInvocationProbe>();
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1237:        invocationProbe = (WorkflowExecutorInvocationProbe)(object)executor;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1345:                PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.HostCommand,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1351:                        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 }),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1356:                        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 900, CaptureOutputArtifact = true }),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1361:                        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120, CaptureOutputArtifact = true }),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1366:                        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 30, CaptureOutputArtifact = true })
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1384:    private static PluginWorkflowExecutorDescriptor CreateDockerExecutor(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1385:        WorkflowExecutorId executorId,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1388:        WorkflowExecutorExecutionPolicy defaultPolicy)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1393:            WorkflowExecutorCategoryKind.Command,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1396:            WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1397:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "Docker command JSON result"),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1527:        await GrantAsync(host, DockerPluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1543:                Modality: WorkflowModality.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1544:                ModelSettings: new WorkflowModelSettings(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1551:                ResultShape: WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1558:    private static WorkflowDefinition CreateDockerProofWorkflow(WorkflowComponentId summaryComponentId)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1560:        var settings = new DockerWorkflowExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1569:        return new WorkflowDefinition(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1570:            WorkflowId.New(),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1571:            WorkflowVersionId.New(),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1574:            WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1575:            new WorkflowGraph(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1576:                new WorkflowNodeId("start"),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1578:                    CreateNode("start", WorkflowNodeKind.Start, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1583:                        inputShape: WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1600:                    CreateNode("summarize", WorkflowNodeKind.LlmCall, summaryComponentId, inputShape: JsonShape(), resultShape: WorkflowValueShape.Text),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1601:                    CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1610:            new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1611:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1620:    private static WorkflowNode CreateExecutorNode(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1622:        WorkflowExecutorId executorId,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1623:        DockerWorkflowExecutorSettings settings,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1624:        WorkflowValueShape inputShape,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1625:        WorkflowValueShape resultShape,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1628:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1629:            WorkflowNodeKind.Executor,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1632:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1643:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1650:    private static WorkflowNode CreateNode(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1652:        WorkflowNodeKind kind,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1653:        WorkflowComponentId? componentId = null,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1654:        WorkflowValueShape? inputShape = null,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1655:        WorkflowValueShape? resultShape = null)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1657:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1661:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1667:                InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1668:                ResultShape: resultShape ?? WorkflowValueShape.Text));
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1670:    private static WorkflowEdge CreateEdge(string id, string source, string target)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1672:            new WorkflowEdgeId(id),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1673:            new WorkflowNodeId(source),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1675:            new WorkflowNodeId(target),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1677:            WorkflowEdgeKind.Direct,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1680:    private static WorkflowValueShape JsonShape()
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1681:        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1683:    private static async Task<IReadOnlyList<WorkflowExecutorDescriptor>> ReadWorkflowExecutorCatalogAsync(ApiTestHost host)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1688:        return JsonSerializer.Deserialize<IReadOnlyList<WorkflowExecutorDescriptor>>(body, JsonOptions)!;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1691:    private static async Task<IReadOnlyList<WorkflowCatalogItem>> ReadWorkflowDefinitionsAsync(ApiTestHost host)
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1696:        return JsonSerializer.Deserialize<IReadOnlyList<WorkflowCatalogItem>>(body, JsonOptions)!;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1736:    private class WorkflowExecutorInvocationProbe : DispatchProxy
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1738:        public WorkflowExecutorDescriptor Descriptor { get; set; } = null!;
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1746:                nameof(IWorkflowExecutor.ExecuteAsync) => ExecuteAsync(),
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1750:        private ValueTask<WorkflowNodeExecutionResult> ExecuteAsync()
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1757:    private sealed class DockerLogSummaryLlmInvoker : IWorkflowLlmComponentInvoker
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1759:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1760:            WorkflowDefinition definition,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1761:            WorkflowNode node,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1763:            WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1772:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1780:public sealed class RuntimePackageFixtureWorkflowExecutor : IWorkflowExecutor
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1782:    public static WorkflowExecutorId ExecutorId { get; } = new("integration.runtime.fixture");
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1784:    public WorkflowExecutorDescriptor Descriptor { get; } = new(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1788:        WorkflowExecutorCategoryKind.Utility,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1791:        WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1792:        WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1795:        WorkflowExecutorExecutionPolicy.Default,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1798:    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1799:        WorkflowExecutorExecutionContext context,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1800:        WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1802:        => ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Integration\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:1805:            WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryWorkspaceAttentionTests.cs:23:            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.WorkflowRun, workflowRunId: Guid.Parse("10000000-0000-0000-0000-000000000001")),
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemoryWorkspaceAttentionTests.cs:47:        Assert.Contains(CognitiveMemoryWorkspaceFrameKind.WorkflowRun, frameKinds);
tests\Unit\CanDoItAll.Tests.Unit\CrmHrResourceSourceGatewayAdapterTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\CognitiveMemorySourceIngestionTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Components\CanDoItAll.Tests.Components\ChatWorkspacePanelTests.cs:2:using CanDoItAll.AgentFramework.Components;
tests\Components\CanDoItAll.Tests.Components\ChatWorkspacePanelTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Playwright\CanDoItAll.Tests.Playwright\MemoryProviderManagementPlaywrightTests.cs:730:        processStartInfo.Environment["Workflows__ExampleSeed__Enabled"] = "false";
tests\Playwright\CanDoItAll.Tests.Playwright\MemoryProviderManagementPlaywrightTests.cs:731:        processStartInfo.Environment["Workflows__ExampleSeed__SeedSampleWorkspaceFiles"] = "false";
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:2:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:3:using CanDoItAll.AgentFramework.Capabilities.Templates;
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:6:using CanDoItAll.Modules.AgentFramework;
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:7:using CanDoItAll.Modules.AgentFramework.Pages.Components;
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:24:                Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:61:                Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer,
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:84:        var workspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:88:            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:142:        var workspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Components\CanDoItAll.Tests.Components\CapabilitySetupFlowServiceTests.cs:145:            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:10:    private const string ProjectStructureWorkflowResultTitle = "Browser workflow generated summary";
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:35:        var definition = await SaveProjectStructureWorkflowDefinitionAsync(fixture.BaseUrl);
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:37:        await CreateProjectAsync(page, "Playwright Workflow Structure", "Validation");
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:104:        await workflowStatusCard.GetByText("Workflow is ready to start", new() { Exact = false }).WaitForAsync();
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:150:                $"Workflow status did not complete in the selection panel. Rendered status: {renderedStatus}",
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:156:            snapshot => snapshot.Nodes.Any(node => string.Equals(node.Title, ProjectStructureWorkflowResultTitle, StringComparison.Ordinal)),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:159:        var resultNodeId = await FindNodeIdByTitleAsync(page, ProjectStructureWorkflowResultTitle);
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:175:    private static async Task<WorkflowDefinition> SaveProjectStructureWorkflowDefinitionAsync(string baseUrl)
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:184:            BuildProjectStructureWorkflowDefinitionSaveRequest());
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:192:        return await response.Content.ReadFromJsonAsync<WorkflowDefinition>() ??
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:193:            throw new InvalidOperationException("Workflow definition save returned no payload.");
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:217:    private static WorkflowDefinitionSaveRequest BuildProjectStructureWorkflowDefinitionSaveRequest()
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:220:            new WorkflowProjectStructureExecutorSettings
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:222:                Operation = WorkflowProjectStructureOperation.CreateAsset,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:224:                Title = ProjectStructureWorkflowResultTitle,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:234:        return new WorkflowDefinitionSaveRequest(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:239:            Status: WorkflowLifecycleStatus.Active,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:240:            Graph: new WorkflowGraph(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:241:                new WorkflowNodeId("start"),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:243:                    CreateProjectStructureWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:244:                    CreateProjectStructureExecutorWorkflowNode("create-result-asset", executorSettingsJson),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:245:                    CreateProjectStructureWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateProjectStructureJsonShape())
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:248:                    CreateProjectStructureWorkflowEdge("start-to-result-asset", "start", "create-result-asset"),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:249:                    CreateProjectStructureWorkflowEdge("result-asset-to-end", "create-result-asset", "end")
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:251:            RuntimePolicy: new WorkflowRuntimePolicy(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:252:                WorkflowRuntimeBackendKind.InProcess,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:259:    private static WorkflowNode CreateProjectStructureWorkflowNode(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:261:        WorkflowNodeKind kind,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:262:        WorkflowValueShape? inputShape = null,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:263:        WorkflowValueShape? resultShape = null)
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:265:        return new WorkflowNode(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:266:            new WorkflowNodeId(id),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:270:            new WorkflowNodeSettings(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:276:                InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:277:                ResultShape: resultShape ?? WorkflowValueShape.Text));
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:280:    private static WorkflowNode CreateProjectStructureExecutorWorkflowNode(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:284:        return new WorkflowNode(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:285:            new WorkflowNodeId(id),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:286:            WorkflowNodeKind.Executor,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:289:            new WorkflowNodeSettings(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:295:                InputShape: WorkflowValueShape.Text,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:298:                ExecutorId = WorkflowExecutorIds.ProjectStructure,
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:300:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:308:    private static WorkflowValueShape CreateProjectStructureJsonShape()
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:309:        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:311:    private static WorkflowEdge CreateProjectStructureWorkflowEdge(string id, string source, string target)
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:313:        return new WorkflowEdge(
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:314:            new WorkflowEdgeId(id),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:315:            new WorkflowNodeId(source),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:317:            new WorkflowNodeId(target),
tests\Playwright\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureWorkflows.cs:319:            WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\ContextualAgentAccessResolverTests.cs:1:using CanDoItAll.AgentFramework.Components;
tests\Components\CanDoItAll.Tests.Components\ContextualAgentAccessResolverTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\AgentChatModalTests.cs:2:using CanDoItAll.AgentFramework.Components;
tests\Components\CanDoItAll.Tests.Components\AgentChatModalTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:277:public sealed class FakeWorkflowRuntimeEvidenceSourceProvider : IWorkflowRuntimeEvidenceSourceProvider
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:290:        WorkflowRuntimeEvidenceSourceRequest request,
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:305:            MemorySourceKind.WorkflowRuntime,
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:313:            MemorySourceSnapshotId.Create(MemorySourceKind.WorkflowRuntime, runId, snapshotAnchor),
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:314:            MemorySourceKind.WorkflowRuntime,
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:330:            MemorySourceKind.WorkflowRuntime,
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:332:            MemorySourceEntityKind.WorkflowRun,
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:337:            MemorySourceKind.WorkflowRuntime,
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:338:            MemorySourceEntityKind.WorkflowRun,
tests\Support\CanDoItAll.Tests.Support\CognitiveMemory\CognitiveMemoryFakes.cs:344:            new MemorySourceProvenance(MemorySourceKind.WorkflowRuntime, runId, MemorySourceEntityKind.WorkflowRun, runId.ToString("D"), $"/fake/workflow/{runId:D}"),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:16:public sealed class ProjectStructureWorkflowScenarioHarnessTests
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:25:    public async Task WorkflowScenarioHarness_runs_twenty_project_structure_workflow_cases()
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:41:        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:44:            ConfigureGroundedWorkflowServices);
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:56:    public async Task WorkflowScenarioHarness_runs_twenty_project_structure_workflow_cases_on_postgresql()
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:81:            await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:86:                ConfigureGroundedWorkflowServices);
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:104:        ProjectStructureAgentApiTestHost host,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:106:        IReadOnlyList<WorkflowScenario> scenarios,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:117:                "Workflow scenario harness",
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:161:            Assert.Equal(WorkflowRunState.Completed.ToString(), result.State);
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:172:    private static void ConfigureGroundedWorkflowServices(IServiceCollection services)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:174:        services.RemoveAll<IWorkflowLlmComponentInvoker>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:175:        services.AddSingleton<IWorkflowLlmComponentInvoker, GroundedScenarioWorkflowLlmInvoker>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:179:        ProjectStructureAgentApiTestHost host,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:182:        WorkflowScenario scenario)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:190:                "Workflow input",
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:215:        var definition = await PostAndReadAsync<WorkflowDefinition>(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:218:            CreateScenarioWorkflowDefinitionSaveRequest(scenario, component.Id));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:220:        var inputSettings = ProjectStructureWorkflowInputSettings.Default();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:226:            .Select(source => new ProjectStructureWorkflowInputSource(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:240:        var options = await PostAndReadAsync<ProjectStructureWorkflowAddOptionsResult>(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:243:            new ProjectStructureWorkflowAddOptionsInput(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:250:        var workflowNode = await PostAndReadAsync<ProjectStructureWorkflowNodeCreateResult>(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:253:            new ProjectStructureWorkflowNodeCreateInput(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:259:        var started = await PostAndReadAsync<ProjectStructureWorkflowNodeStartResult>(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:262:            new ProjectStructureWorkflowNodeStartInput(WorkflowRuntimeBackendKind.InProcess, LeaseToken: leaseToken));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:265:            started.Status.State == WorkflowRunState.Completed,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:270:        var status = await GetAndReadAsync<ProjectStructureWorkflowRunStatus>(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:319:        ProjectStructureWorkflowAddOptionsResult options,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:320:        WorkflowScenario scenario,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:359:    private static LlmCallComponentSaveRequest CreateScenarioComponentRequest(WorkflowScenario scenario)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:366:            Modality: WorkflowModality.Text,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:367:            ModelSettings: new WorkflowModelSettings(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:409:    private static WorkflowDefinitionSaveRequest CreateScenarioWorkflowDefinitionSaveRequest(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:410:        WorkflowScenario scenario,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:411:        WorkflowComponentId componentId)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:414:            new WorkflowSourceIngestionExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:428:            new WorkflowProjectStructureExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:430:                Operation = WorkflowProjectStructureOperation.CreateAsset,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:439:        var nodes = new List<WorkflowNode>
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:441:            CreateWorkflowNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:442:            CreateExecutorWorkflowNode("ingest-sources", WorkflowExecutorIds.SourceIngestion, sourceSettingsJson),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:443:            CreateLlmWorkflowNode("summarize-sources", componentId),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:444:            CreateExecutorWorkflowNode("create-summary-asset", WorkflowExecutorIds.ProjectStructure, assetSettingsJson, CreateJsonShape())
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:446:        var edges = new List<WorkflowEdge>
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:448:            CreateWorkflowEdge("start-to-ingest", "start", "ingest-sources"),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:449:            CreateWorkflowEdge("ingest-to-llm", "ingest-sources", "summarize-sources"),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:450:            CreateWorkflowEdge("llm-to-summary", "summarize-sources", "create-summary-asset")
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:456:                new WorkflowStorageFileExecutorSettings
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:458:                    Operation = WorkflowStorageFileOperation.WriteText,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:464:            nodes.Add(CreateExecutorWorkflowNode("write-result-file", WorkflowExecutorIds.StorageFile, fileSettingsJson, CreateJsonShape()));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:465:            edges.Add(CreateWorkflowEdge("llm-to-file", "summarize-sources", "write-result-file"));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:466:            edges.Add(CreateWorkflowEdge("summary-to-end", "create-summary-asset", "end"));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:467:            edges.Add(CreateWorkflowEdge("file-to-end", "write-result-file", "end"));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:471:            edges.Add(CreateWorkflowEdge("summary-to-end", "create-summary-asset", "end"));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:474:        nodes.Add(CreateWorkflowNode("end", WorkflowNodeKind.End, inputShape: CreateJsonShape()));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:475:        return new WorkflowDefinitionSaveRequest(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:480:            Status: WorkflowLifecycleStatus.Active,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:481:            Graph: new WorkflowGraph(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:482:                new WorkflowNodeId("start"),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:485:            RuntimePolicy: new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:486:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:493:    private static WorkflowNode CreateWorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:495:        WorkflowNodeKind kind,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:496:        WorkflowValueShape? inputShape = null,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:497:        WorkflowValueShape? resultShape = null)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:499:        return new WorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:500:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:504:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:510:                InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:511:                ResultShape: resultShape ?? WorkflowValueShape.Text));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:514:    private static WorkflowNode CreateExecutorWorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:516:        WorkflowExecutorId executorId,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:518:        WorkflowValueShape? inputShape = null)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:520:        return new WorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:521:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:522:            WorkflowNodeKind.Executor,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:525:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:531:                InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:536:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:544:    private static WorkflowNode CreateLlmWorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:546:        WorkflowComponentId componentId)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:548:        return new WorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:549:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:550:            WorkflowNodeKind.LlmCall,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:553:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:563:    private static WorkflowEdge CreateWorkflowEdge(string id, string source, string target)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:565:        return new WorkflowEdge(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:566:            new WorkflowEdgeId(id),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:567:            new WorkflowNodeId(source),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:569:            new WorkflowNodeId(target),
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:571:            WorkflowEdgeKind.Direct,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:575:    private static WorkflowValueShape CreateJsonShape()
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:576:        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:578:    private static IReadOnlyList<WorkflowScenario> BuildScenarios(string syntheticRoot)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:831:    private static WorkflowScenario CreateScenario(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:837:        IReadOnlyList<WorkflowInputSourceSpec> sources,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:848:        return new WorkflowScenario(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:861:             Workflow result generated by the project-structure scenario harness.
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:876:    private static WorkflowInputSourceSpec FileSource(string key, string label, string value)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:877:        => new(ProjectStructureWorkflowInputSourceKind.FilePath, key, label, value);
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:879:    private static WorkflowInputSourceSpec FolderSource(string key, string label, string value)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:880:        => new(ProjectStructureWorkflowInputSourceKind.FolderPath, key, label, value);
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:888:    private static void ValidateScenarioSources(WorkflowScenario scenario)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:892:            if (source.Kind == ProjectStructureWorkflowInputSourceKind.FilePath)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:897:            if (source.Kind == ProjectStructureWorkflowInputSourceKind.FolderPath)
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1062:    private sealed class GroundedScenarioWorkflowLlmInvoker : IWorkflowLlmComponentInvoker
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1064:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1065:            WorkflowDefinition definition,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1066:            WorkflowNode node,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1068:            WorkflowNodeInput input,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1139:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1195:            builder.AppendLine($"- Workflow node: {EscapeMarkdown(ReadNestedString(root, "runContext", "workflowNodeId"))}");
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1390:    private sealed record WorkflowScenario(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1396:        IReadOnlyList<WorkflowInputSourceSpec> Sources,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1404:    private sealed record WorkflowInputSourceSpec(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1405:        ProjectStructureWorkflowInputSourceKind Kind,
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureWorkflowScenarioHarnessTests.cs:1439:        string WorkflowName,
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeAttachmentTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeAttachmentTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeAttachmentTests.cs:7:public sealed class MafAgentRuntimeAttachmentTests
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeAttachmentTests.cs:32:        var message = MafRuntimeSessionBuilder.CreateUserInputMessage(
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeAttachmentTests.cs:280:            FinalizerMode: AgentFinalizerMode.Disabled,
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeAttachmentTests.cs:380:        return MafRuntimeSessionBuilder.CreatePromptInputMessages(agent, provider, session, prompt, runtimeOptions).ToList();
tests\Unit\CanDoItAll.Tests.Unit\ImageGenerationAgentRuntimeToolProviderTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ImageGenerationAgentRuntimeToolProviderTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ImageGenerationAgentRuntimeToolProviderTests.cs:3:using CanDoItAll.AgentFramework.Tooling;
tests\Unit\CanDoItAll.Tests.Unit\ImageGenerationAgentRuntimeToolProviderTests.cs:5:using CanDoItAll.Modules.AgentFramework;
tests\Components\CanDoItAll.Tests.Components\AgentProviderProfilesPanelPricingTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Components\CanDoItAll.Tests.Components\AgentProviderProfilesPanelPricingTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\AgentProviderProfilesPanelPricingTests.cs:5:using CanDoItAll.Modules.AgentFramework.Pages.Components;
tests\Components\CanDoItAll.Tests.Components\AgentProviderProfilesPanelPricingTests.cs:17:        var workspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Components\CanDoItAll.Tests.Components\AgentAvatarRenderingTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\AgentAvatarRenderingTests.cs:4:using CanDoItAll.Modules.AgentFramework.Pages.Components;
tests\Unit\CanDoItAll.Tests.Unit\LocalWorkspaceProcessHostTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:3:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:11:public sealed class MafAgentRuntimeProviderHealthTests
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:22:        var messages = ProviderRuntimeDiagnostics.BuildProviderTestInputMessages(request);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:42:        var messages = ProviderRuntimeDiagnostics.BuildProviderTestInputMessages(request);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:59:    public async Task MafRuntimeProviderDiagnostics_UseProviderRuntimeGateway()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:61:        var gateway = new CapturingMafProviderRuntimeGateway();
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:63:            .AddSingleton<IMafProviderRuntimeGateway>(gateway)
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:65:        var runtime = new MafAgentRuntime(Path.GetTempPath(), services);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:89:    public async Task MafProviderRuntimeGateway_CorrelatesConcurrentChatDispatch()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:96:        await using var runtimePool = new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:98:            new ProviderRuntimeHandleFactory(factory));
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:99:        var gateway = new MafProviderRuntimeGateway(descriptorSource, runtimePool);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:120:    public async Task MafProviderRuntimeGateway_UnsupportedMaintenanceCapabilityFailsExplicitly()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:126:        await using var runtimePool = new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:128:            new ProviderRuntimeHandleFactory(factory));
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:129:        var gateway = new MafProviderRuntimeGateway(descriptorSource, runtimePool);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:140:    public async Task MafProviderRuntimeGateway_OllamaMaintenanceUsesProviderCapabilityDriver()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:149:        await using var runtimePool = new ProviderRuntimePool(
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:151:            new ProviderRuntimeHandleFactory(factory));
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:152:        var gateway = new MafProviderRuntimeGateway(descriptorSource, runtimePool);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:177:            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:178:            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs"
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeProviderHealthTests.cs:231:    private sealed class CapturingMafProviderRuntimeGateway : IMafProviderRuntimeGateway
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:17:internal sealed class ProjectStructureAgentApiTestHost : IAsyncDisposable
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:19:    private ProjectStructureAgentApiTestHost(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:42:    public static async Task<ProjectStructureAgentApiTestHost> CreateAsync()
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:47:    public static async Task<ProjectStructureAgentApiTestHost> CreateAsync(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:84:        app.MapProjectStructureAgentApi();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:98:        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentId, "api-test-agent");
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:99:        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentName, "API Test Agent");
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:100:        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.MachineName, "api-test-machine");
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:101:        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, IntegrationTestPaths.RepositoryRoot);
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:102:        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.BranchName, "tests/project-structure");
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:103:        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentApiTestHost.cs:105:        return new ProjectStructureAgentApiTestHost(testEnvironment, activeProfile, app, client);
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:8:using CanDoItAll.AgentFramework.Core;
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:9:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:11:using CanDoItAll.Modules.AgentFramework;
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:28:public sealed class ProjectStructureAgentIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:30:    private static readonly ProjectStructureAgentContext DefaultAgent = new(
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:287:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:686:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:689:        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:714:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:865:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1143:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1228:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1329:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1447:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1566:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1873:                ["AgentFramework:ProcessMockAgents:Enabled"] = "true"
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1879:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1981:                ["AgentFramework:ProcessMockAgents:Enabled"] = "false"
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:1987:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2308:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2373:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2471:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2540:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2591:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2635:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2687:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2738:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2834:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2886:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2920:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2923:        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2952:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:2985:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:3019:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs:3053:        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
tests\Integration\CanDoItAll.Tests.Integration\WorkbenchSourceSnapshotIntegrationTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolInvocationResultTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolInvocationResultTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolInvocationResultTests.cs:6:public sealed class MafAgentRuntimeToolInvocationResultTests
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolInvocationResultTests.cs:13:        var succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(result);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolInvocationResultTests.cs:26:        var succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(result);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolInvocationResultTests.cs:39:        var message = MafRuntimeToolInvocationResultClassifier.ResolveFailureMessage(result);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:2:using CapabilityExposureDescriptor = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityExposureDescriptor;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:3:using AccessCapabilityDiagnosticCategory = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityDiagnosticCategory;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:4:using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:5:using AccessCapabilityOperationClassification = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityOperationClassification;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:6:using EffectiveCapabilitySet = CanDoItAll.AgentFramework.Capabilities.Abstractions.EffectiveCapabilitySet;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:7:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:8:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:9:using CanDoItAll.AgentFramework.Mcp;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:10:using CanDoItAll.AgentFramework.Mcp.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:11:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:12:using CanDoItAll.AgentFramework.Persistence;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:13:using CanDoItAll.AgentFramework.Tooling;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:18:using McpToolName = CanDoItAll.AgentFramework.Capabilities.Abstractions.McpToolName;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:22:public sealed class MafAgentRuntimeToolProviderCompositionTests
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:25:    public async Task MafAgentRuntimeToolProviderComposition_zero_registered_providers_does_not_attach_process_tools()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:43:    public async Task MafAgentRuntimeToolProviderComposition_invokes_fake_providers_in_deterministic_order()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:76:    public async Task MafAgentRuntimeToolProviderComposition_skips_registered_providers_when_context_disables_them()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:103:    public async Task MafAgentRuntimeToolProviderComposition_records_provider_descriptor_metadata()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:126:    public async Task MafAgentRuntimeToolProviderComposition_rejects_duplicate_provider_keys()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:170:    public async Task MafAgentRuntimeToolProviderComposition_infers_tool_operation_metadata_from_policy_catalog()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:199:    public async Task MafAgentRuntimeToolProviderComposition_rejects_tool_metadata_for_unknown_tool_name()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:223:    public async Task MafAgentRuntimeToolProviderComposition_rejects_duplicate_provider_tool_names()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:239:    public async Task MafAgentRuntimeToolProviderComposition_wraps_policy_mutation_tools_from_providers()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:256:    public async Task MafAgentRuntimeProcessContext_read_only_step_filters_registered_runtime_tool_providers()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:288:    public async Task MafAgentRuntimeProcessContext_start_project_node_step_keeps_only_matching_runtime_mutation_tool()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:314:    public async Task MafAgentRuntimeProcessContext_external_action_project_structure_write_step_keeps_node_and_asset_create_tools()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:414:            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart,
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:415:            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStatusGet,
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:476:        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart, toolNames);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:477:        Assert.Contains(AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStatusGet, toolNames);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:505:            capability.RuntimeToolName?.Value == AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:589:    public async Task MafAgentRuntimeProcessContext_read_only_step_does_not_attach_broad_workspace_tools()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:612:    public async Task MafAgentRuntimeProcessContext_validation_step_attaches_validation_tools_without_write_tools()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:659:    public async Task MafAgentRuntimeProcessContext_managed_artifact_write_step_attaches_workspace_writes_without_product_mutation_tools()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:697:    public async Task MafAgentRuntimeWorkspaceTools_skips_configured_tools_when_context_disables_them()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:721:    public async Task MafAgentRuntimeWorkspaceTools_skips_catalog_workspace_tools_when_context_disables_them()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:751:    public async Task MafAgentRuntimeProcessContext_runtime_proof_step_attaches_image_analysis_tool()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:771:    public async Task MafAgentRuntimeProcessContext_mutating_product_step_keeps_scaffold_tool_for_software_development_agent()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:794:    public async Task MafAgentRuntimeProcessContext_mutating_product_step_keeps_configured_workspace_tools_when_catalog_contains_same_tool()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:817:        var configuredTag = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityTag.Create("configured");
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:838:    public async Task MafAgentRuntimeProcessContext_mutating_product_step_keeps_git_mutation_tools_for_software_development_agent()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:866:    public async Task MafAgentRuntimeProcessContext_two_step_process_reduces_tool_surface_against_agent_baseline()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:906:    public async Task MafAgentRuntimeProcessContext_read_only_step_skips_skill_provider()
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeToolProviderCompositionTests.cs:935:    public async Task MafAgentRuntimeToolProviderComposition_reports_provider_failures_with_provider_type()
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:2:using CanDoItAll.Modules.AgentFramework.Pages.Components;
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:6:public sealed class AgentFrameworkStatusDisplayAdapterTests
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:11:        Assert.Equal(new AgentFrameworkStatusBadge("Verified", "success"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.Verified));
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:12:        Assert.Equal(new AgentFrameworkStatusBadge("Pending review", "warning"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.PendingReview));
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:13:        Assert.Equal(new AgentFrameworkStatusBadge("Failed", "danger"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.Failed));
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:14:        Assert.Equal(new AgentFrameworkStatusBadge("Not run", "neutral"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.NotRun));
tests\Components\CanDoItAll.Tests.Components\AgentFrameworkStatusDisplayAdapterTests.cs:42:        Assert.Equal(new AgentFrameworkStatusBadge("Disabled", "warning"), badge);
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeImageAnalysisModelTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeImageAnalysisModelTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafAgentRuntimeImageAnalysisModelTests.cs:6:public sealed class MafAgentRuntimeImageAnalysisModelTests
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:1:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:2:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:4:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:6:using CanDoItAll.AgentFramework.Providers;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:7:using CanDoItAll.AgentFramework.Tooling;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:12:using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:16:public sealed class MafRuntimeArchitectureServicesTests
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:19:    public void MafRuntimeArchitectureServices_registers_runtime_collaborators()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:23:        services.AddMafRuntimeArchitectureServices();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:26:        Assert.IsType<MafRuntimeDependencyResolver>(provider.GetRequiredService<IMafRuntimeDependencyResolver>());
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:27:        Assert.IsType<MafProviderCredentialService>(provider.GetRequiredService<IMafProviderCredentialService>());
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:28:        Assert.IsType<MafProviderAgentFactory>(provider.GetRequiredService<IMafProviderAgentFactory>());
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:29:        Assert.IsType<MafProviderStreamingRunner>(provider.GetRequiredService<IMafProviderStreamingRunner>());
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:32:        Assert.IsType<NoOpMafRuntimeCompositionMetrics>(provider.GetRequiredService<IMafRuntimeCompositionMetrics>());
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:33:        Assert.IsType<MafApprovalContinuationDriver>(provider.GetRequiredService<IMafApprovalContinuationDriver>());
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:34:        Assert.IsType<MafRuntimeSessionPersistenceDriver>(provider.GetRequiredService<IMafRuntimeSessionPersistenceDriver>());
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:38:    public void Maf_runtime_collaborators_are_top_level_types()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:43:            typeof(MafRuntimeAgentFactory),
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:44:            typeof(MafRuntimeExecutionOptionsResolver),
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:45:            typeof(MafRuntimeToolInvocationResultClassifier),
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:58:            typeof(ProviderRuntimeDiagnostics),
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:60:            typeof(RequiredFinalizerCapturedException)
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:63:        Assert.All(collaboratorTypes, type => Assert.False(type.IsNested, $"{type.FullName} must not be nested under MafAgentRuntime."));
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:67:    public void MafAgentRuntime_is_not_a_split_partial_namespace()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:75:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:78:            .EnumerateFiles(runtimeRoot, "MafAgentRuntime*.cs", SearchOption.AllDirectories)
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:83:        Assert.Equal(["MafAgentRuntime.cs"], runtimeFiles);
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:85:            "partial class MafAgentRuntime",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:86:            File.ReadAllText(Path.Combine(runtimeRoot, "MafAgentRuntime.cs")),
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:91:    public void MafAgentRuntime_partials_do_not_hide_private_nested_runtime_types()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:99:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:102:            .EnumerateFiles(runtimeRoot, "MafAgentRuntime*.cs", SearchOption.AllDirectories)
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:111:    public void MafAgentRuntime_does_not_own_capability_composition_partials()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:119:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:123:            .EnumerateFiles(capabilityRoot, "MafAgentRuntime.Capabilities*.cs", SearchOption.TopDirectoryOnly)
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:132:    public void MafAgentRuntime_no_longer_owns_approval_and_session_persistence_algorithms()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:140:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:142:            "MafAgentRuntime.cs"));
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:159:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:188:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:203:                "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:234:    public void MafApprovalContinuationDriver_maps_and_replays_pending_function_approval()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:236:        var driver = new MafApprovalContinuationDriver();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:265:    public void MafApprovalContinuationDriver_rehydrates_legacy_pending_approval_records()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:267:        var driver = new MafApprovalContinuationDriver();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:291:    public void MafRuntimeSessionPersistenceDriver_skips_governed_process_steps_without_pending_approvals()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:293:        var driver = new MafRuntimeSessionPersistenceDriver();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:321:                nameof(MafRuntimeArchitectureServicesTests) + ".cs",
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:331:    public void MafRuntimeDependencyResolver_prefers_registered_provider_dependencies()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:333:        var gateway = new TestMafProviderRuntimeGateway();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:334:        var streamingGate = new TestMafProviderStreamingDispatchGate();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:336:        services.AddSingleton<IMafProviderRuntimeGateway>(gateway);
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:337:        services.AddSingleton<IMafProviderStreamingDispatchGate>(streamingGate);
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:339:        var resolver = new MafRuntimeDependencyResolver();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:343:        Assert.Same(gateway, dependencies.ProviderRuntimeGateway);
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:463:    public void MafProviderCredentialService_resolves_configuration_credentials()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:473:        services.AddMafRuntimeArchitectureServices();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:475:        var credentialService = provider.GetRequiredService<IMafProviderCredentialService>();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:505:    public void InMemoryMafRuntimeCompositionMetrics_records_measurements()
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:507:        var metrics = new InMemoryMafRuntimeCompositionMetrics();
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:508:        var measurement = new MafRuntimeCompositionMeasurement(
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:563:            FinalizerMode: AgentFinalizerMode.Disabled,
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:652:        if (!source.Contains("typeof(MafAgentRuntime).GetMethod(", StringComparison.Ordinal))
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:711:    private sealed class TestMafProviderRuntimeGateway : IMafProviderRuntimeGateway
tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs:741:    private sealed class TestMafProviderStreamingDispatchGate : IMafProviderStreamingDispatchGate
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:8:public sealed class WorkflowApiIntegrationTests
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:11:    public async Task Workflow_api_saves_validates_and_runs_workflow()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:20:        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:25:        var definitions = JsonSerializer.Deserialize<IReadOnlyList<WorkflowCatalogItem>>(listBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:30:        var validation = JsonSerializer.Deserialize<WorkflowValidationResult>(validationBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:34:            new WorkflowTestRunRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:39:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:43:        var testRun = JsonSerializer.Deserialize<WorkflowTestRunResult>(testRunBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:49:        Assert.Equal(WorkflowRunState.Completed, testRun.Run.State);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:54:    public async Task Workflow_api_exports_imports_and_changes_definition_lifecycle()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:59:            CreateDefinitionSaveRequest(componentId: WorkflowComponentId.New(), graph: CreatePassthroughGraph()));
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:62:        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:69:        var published = JsonSerializer.Deserialize<WorkflowDefinition>(publishBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:76:        var suspended = JsonSerializer.Deserialize<WorkflowDefinition>(suspendBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:81:        var envelope = JsonSerializer.Deserialize<WorkflowDefinitionExportEnvelope>(exportBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:85:            new WorkflowDefinitionImportRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:88:                WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:89:                PreserveWorkflowId: false));
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:92:        var imported = JsonSerializer.Deserialize<WorkflowDefinition>(importBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:94:        Assert.Equal(WorkflowLifecycleStatus.Active, published.Status);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:97:        Assert.Equal(WorkflowLifecycleStatus.Suspended, suspended.Status);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:98:        Assert.Equal(WorkflowDefinitionExchangeFormats.Current, envelope.SourceFormat);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:102:        Assert.Equal(WorkflowLifecycleStatus.Draft, imported.Status);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:107:    public async Task Workflow_api_rejects_invalid_definition_on_save()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:112:            CreateDefinitionSaveRequest(WorkflowComponentId.New()));
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:116:        Assert.Contains("Workflow definition save failed validation", saveBody, StringComparison.OrdinalIgnoreCase);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:121:    public async Task Workflow_api_round_trips_typed_route_metadata()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:126:            new WorkflowDefinitionSaveRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:130:                Description: "Workflow route metadata API proof.",
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:131:                Status: WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:133:                RuntimePolicy: new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:134:                    WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:141:        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:146:        var detail = JsonSerializer.Deserialize<WorkflowDefinitionDetail>(detailBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:153:                Assert.Equal(WorkflowRouteKind.SwitchCase, switchCase.Routing.Kind);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:160:                Assert.Equal(WorkflowRouteKind.SwitchDefault, switchDefault.Routing.Kind);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:166:    public async Task Workflow_api_test_run_pauses_human_input_only_when_route_reaches_node()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:169:        var draft = new WorkflowDefinition(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:170:            WorkflowId.New(),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:171:            WorkflowVersionId.New(),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:173:            "Workflow API proof for execution-position HITL.",
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:174:            WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:176:            new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:177:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:187:            new WorkflowTestRunRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:188:                WorkflowId: null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:192:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:196:        var automatic = JsonSerializer.Deserialize<WorkflowTestRunResult>(automaticBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:200:            new WorkflowTestRunRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:201:                WorkflowId: null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:205:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:209:        var manual = JsonSerializer.Deserialize<WorkflowTestRunResult>(manualBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:212:        Assert.Equal(WorkflowRunState.Completed, automatic.Run?.State);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:215:        Assert.Equal(WorkflowRunState.WaitingForInput, manual.Run?.State);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:217:        Assert.Equal(WorkflowExternalRequestKind.HumanInput, request.Kind);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:218:        Assert.Equal(new WorkflowNodeId("human"), request.NodeId);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:220:            workflowEvent.Kind == WorkflowEventKind.WaitingForInput &&
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:221:            workflowEvent.NodeId == new WorkflowNodeId("human"));
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:222:        var waitingPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(waitingEvent.PayloadJson, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:223:        Assert.Equal(WorkflowEventPayloadSource.ExternalRequest, waitingPayload.Source);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:225:        Assert.Equal(WorkflowExternalRequestKind.HumanInput, waitingPayload.RequestKind);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:226:        Assert.Equal(new WorkflowNodeId("human"), waitingPayload.NodeId);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:229:        Assert.Equal(WorkflowCheckpointKind.Completed, automaticCheckpoint.Kind);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:230:        Assert.Equal(WorkflowResumeAvailability.NotSupported, automaticCheckpoint.ResumeAvailability);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:232:        Assert.Equal(WorkflowCheckpointKind.WaitingForInput, manualCheckpoint.Kind);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:234:        Assert.Equal(WorkflowCheckpointTrustBoundary.MetadataOnly, manualCheckpoint.TrustBoundary);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:239:    public async Task Workflow_api_returns_validation_failure_for_invalid_test_run()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:242:        var draft = CreateDefinition(WorkflowComponentId.New());
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:246:            new WorkflowTestRunRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:247:                WorkflowId: null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:251:                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:254:        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:258:        Assert.Contains(result.Validation.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidComponentReference);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:262:    public async Task Workflow_api_test_run_rejects_unregistered_durable_backend_policy_before_runtime()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:268:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:269:                WorkflowRuntimeBackendKind.DurableTask,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:278:            new WorkflowTestRunRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:279:                WorkflowId: null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:283:                RequestedBackend: WorkflowRuntimeBackendKind.DurableTask,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:286:        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:291:            issue.Code == WorkflowValidationIssueCode.UnsupportedRuntimeBackend &&
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:296:    public async Task Workflow_api_runtime_backend_catalog_marks_unregistered_durable_backends_unavailable()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:303:        var backends = JsonSerializer.Deserialize<IReadOnlyList<WorkflowRuntimeBackendDescriptor>>(body, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:305:        var inProcess = Assert.Single(backends, backend => backend.Kind == WorkflowRuntimeBackendKind.InProcess);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:306:        var durableTask = Assert.Single(backends, backend => backend.Kind == WorkflowRuntimeBackendKind.DurableTask);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:307:        var azureFunctions = Assert.Single(backends, backend => backend.Kind == WorkflowRuntimeBackendKind.AzureFunctions);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:308:        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Registered, inProcess.Availability);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:311:        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, durableTask.Availability);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:315:        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, azureFunctions.Availability);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:320:    public async Task Workflow_api_rejects_unregistered_durable_backend_policy_on_save()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:329:                runtimePolicy: new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:330:                    WorkflowRuntimeBackendKind.DurableTask,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:339:        Assert.Contains(nameof(WorkflowRuntimeBackendKind.DurableTask), body, StringComparison.Ordinal);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:343:    public async Task Workflow_api_rejects_unregistered_durable_backend_start_request()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:352:        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:360:                requestedBackend = WorkflowRuntimeBackendKind.DurableTask
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:366:        Assert.Contains(nameof(WorkflowRuntimeBackendKind.DurableTask), body, StringComparison.Ordinal);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:370:    public async Task Workflow_api_test_run_applies_payload_policy_to_large_runtime_payloads()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:373:        var draft = new WorkflowDefinition(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:374:            WorkflowId.New(),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:375:            WorkflowVersionId.New(),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:377:            "Workflow API proof for runtime payload artifact policy.",
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:378:            WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:380:            new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:381:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:388:        var settings = WorkflowSettings.Default with
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:390:            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:402:            new WorkflowTestRunRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:403:                WorkflowId: null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:407:                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:411:        var result = JsonSerializer.Deserialize<WorkflowTestRunResult>(body, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:412:        var started = Assert.Single(result.Events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:413:        var startedPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(started.PayloadJson, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:421:            artifact.Kind == WorkflowArtifactKind.Json &&
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:424:            artifact.Kind == WorkflowArtifactKind.Json &&
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:425:            artifact.NodeId == new WorkflowNodeId("logic"));
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:428:            artifact.Kind == WorkflowArtifactKind.Json &&
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:438:    public async Task Workflow_contract_lists_control_and_validation_routes()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:484:    public async Task Workflow_api_exposes_agent_provider_options_for_llm_components()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:491:        var options = JsonSerializer.Deserialize<IReadOnlyList<WorkflowProviderOption>>(body, JsonOptions())!;
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:506:    private static WorkflowDefinitionSaveRequest CreateDefinitionSaveRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:507:        WorkflowComponentId componentId,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:508:        WorkflowGraph? graph = null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:509:        WorkflowRuntimePolicy? runtimePolicy = null)
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:511:        return new WorkflowDefinitionSaveRequest(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:515:            Description: "Workflow created by API integration tests.",
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:516:            Status: WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:518:            RuntimePolicy: runtimePolicy ?? new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:519:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:526:    private static WorkflowDefinition CreateDefinition(WorkflowComponentId componentId)
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:528:        return new WorkflowDefinition(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:529:            WorkflowId.New(),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:530:            WorkflowVersionId.New(),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:533:            WorkflowLifecycleStatus.Draft,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:535:            new WorkflowRuntimePolicy(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:536:                WorkflowRuntimeBackendKind.InProcess,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:545:    private static WorkflowGraph CreateGraph(WorkflowComponentId componentId)
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:547:        return new WorkflowGraph(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:548:            new WorkflowNodeId("start"),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:550:                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:551:                CreateNode("llm", WorkflowNodeKind.LlmCall, componentId),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:552:                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:560:    private static WorkflowGraph CreatePassthroughGraph()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:562:        return new WorkflowGraph(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:563:            new WorkflowNodeId("start"),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:565:                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:566:                CreateNode("logic", WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:567:                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:575:    private static WorkflowGraph CreateRoutingGraph()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:577:        return new WorkflowGraph(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:578:            new WorkflowNodeId("start"),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:580:                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:581:                CreateNode("enterprise", WorkflowNodeKind.StrictLogic, inputShape: JsonShape(), resultShape: JsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:582:                CreateNode("standard", WorkflowNodeKind.StrictLogic, inputShape: JsonShape(), resultShape: JsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:583:                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonShape())
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:590:                    WorkflowEdgeKind.Conditional,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:591:                    WorkflowEdgeRouting.SwitchCase(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:594:                        WorkflowRouteValueKind.String,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:600:                    WorkflowEdgeKind.Conditional,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:601:                    WorkflowEdgeRouting.SwitchDefault("Default")),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:607:    private static WorkflowGraph CreateHumanInputRoutingGraph()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:609:        return new WorkflowGraph(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:610:            new WorkflowNodeId("start"),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:612:                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:613:                CreateNode("human", WorkflowNodeKind.HumanInput, inputShape: JsonShape(), resultShape: JsonShape()),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:614:                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonShape())
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:621:                    WorkflowEdgeKind.Conditional,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:622:                    WorkflowEdgeRouting.SwitchCase(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:625:                        WorkflowRouteValueKind.String,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:631:                    WorkflowEdgeKind.Conditional,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:632:                    WorkflowEdgeRouting.SwitchDefault("Automatic")),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:637:    private static WorkflowValueShape JsonShape()
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:638:        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:640:    private static WorkflowNode CreateNode(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:642:        WorkflowNodeKind kind,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:643:        WorkflowComponentId? componentId = null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:644:        WorkflowValueShape? inputShape = null,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:645:        WorkflowValueShape? resultShape = null)
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:647:        return new WorkflowNode(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:648:            new WorkflowNodeId(id),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:652:            new WorkflowNodeSettings(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:658:                InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:659:                ResultShape: resultShape ?? WorkflowValueShape.Text));
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:662:    private static WorkflowEdge CreateEdge(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:666:        WorkflowEdgeKind kind = WorkflowEdgeKind.Direct,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:667:        WorkflowEdgeRouting? routing = null)
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:669:        return new WorkflowEdge(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:670:            new WorkflowEdgeId(id),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:671:            new WorkflowNodeId(source),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:673:            new WorkflowNodeId(target),
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:678:            Routing = routing ?? WorkflowEdgeRouting.Always
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:689:            Modality: WorkflowModality.Text,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:690:            ModelSettings: new WorkflowModelSettings(
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:696:            InputShape: WorkflowValueShape.Text,
tests\Integration\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs:697:            ResultShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:5:public sealed class MafWorkflowAdapterIsolationTests
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:8:    public void Maf_workflow_adapter_types_live_in_adapter_assembly()
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:11:            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:12:            typeof(MafWorkflowCompiler).Assembly.GetName().Name);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:14:            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:15:            typeof(MafInProcessWorkflowExecutionBackend).Assembly.GetName().Name);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:17:            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:18:            typeof(MafWorkflowEventNormalizer).Assembly.GetName().Name);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:20:            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:21:            typeof(MafWorkflowLlmComponentInvoker).Assembly.GetName().Name);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:24:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:25:            typeof(MafAgentRuntime).Assembly.GetName().Name);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:29:    public void Workflow_owned_projects_do_not_reference_maf_adapter_or_maf_project()
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:34:            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Abstractions\CanDoItAll.AgentFramework.Workflows.Abstractions.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:35:            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Builder\CanDoItAll.AgentFramework.Workflows.Builder.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:36:            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\CanDoItAll.AgentFramework.Workflows.Core.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:37:            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Runtime\CanDoItAll.AgentFramework.Workflows.Runtime.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:38:            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Templates\CanDoItAll.AgentFramework.Workflows.Templates.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:39:            @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:40:            @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Core\CanDoItAll.AgentFramework.WorkflowExecutors.Core.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:41:            @"src\MAF\WorkflowExecutors\Standard\CanDoItAll.AgentFramework.WorkflowExecutors.Standard\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.csproj",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:42:            @"src\MAF\WorkflowExecutors\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins.csproj"
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:48:            Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", text, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:49:            Assert.DoesNotContain("CanDoItAll.AgentFramework.Workflows.MafAdapter", text, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:59:            @"src\MAF\Common\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:62:            @"src\Modules\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:64:        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Singleton)", hostRegistration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:65:        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Scoped)", moduleRegistration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:69:            Assert.DoesNotContain("TryAddScoped<MafWorkflowCompiler>", registration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:70:            Assert.DoesNotContain("MafInProcessWorkflowExecutionBackend>", registration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:71:            Assert.DoesNotContain("AddStandardWorkflowExecutors(", registration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:79:        var adapterRoot = Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.MafAdapter");
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:82:            "MafInProcessWorkflowExecutionBackend.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:83:            "MafWorkflowCompiler.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:84:            "MafWorkflowEventNormalizer.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:85:            "MafWorkflowLlmComponentInvoker.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:86:            "MafHandoffWorkflowFactory.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:87:            "MafConfiguredFileArtifactResolver.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:88:            "WorkflowBackendExternalRequestCapture.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:89:            "WorkflowBackendProgressEventObserver.cs",
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:90:            "MafWorkflowAdapterServiceCollectionExtensions.cs"
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:98:        var mafWorkflowDirectory = Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime", "Workflows");
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:99:        var remainingMafWorkflowFiles = Directory.Exists(mafWorkflowDirectory)
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:100:            ? Directory.GetFiles(mafWorkflowDirectory, "*.cs", SearchOption.AllDirectories)
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:102:        Assert.Empty(remainingMafWorkflowFiles);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowAdapterIsolationTests.cs:104:        Assert.InRange(CountLines(Path.Combine(adapterRoot, "MafInProcessWorkflowExecutionBackend.cs")), 1, 500);
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:6:using CanDoItAll.AgentFramework.Voice;
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:1005:        IAgentFrameworkWorkspaceService? agentWorkspaceService = null,
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:1050:    private sealed class RecordingManagerChatWorkspaceService : IAgentFrameworkWorkspaceService
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:1319:        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2201:                new ProcessDefinitionWorkflowPreferenceProjection(
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2202:                    ProcessDefinitionRoleWorkflowPreferenceKind.AnyActiveWorkflow,
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2203:                    WorkflowDefinitionId: null,
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2204:                    WorkflowVersionId: null,
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2272:                            WorkflowOutputId: string.Empty,
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2273:                            WorkflowOutputName: string.Empty,
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2274:                            ProcessDefinitionWorkflowOutputKind.Unspecified,
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2517:                        WorkflowOutputId: "adr-output",
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2518:                        WorkflowOutputName: "Architecture decision record",
tests\Components\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs:2519:                        ProcessDefinitionWorkflowOutputKind.Artifact,
tests\Components\CanDoItAll.Tests.Components\MemoryProvidersPageTests.cs:7:using CanDoItAll.Modules.AgentFramework;
tests\Components\CanDoItAll.Tests.Components\MemoryProvidersPageTests.cs:101:                new AgentFrameworkShellNavigationContributor(),
tests\Components\CanDoItAll.Tests.Components\MemoryProvidersPageTests.cs:136:        Assert.DoesNotContain("CanDoItAll.AgentFramework.Rag", sourceText);
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:4:using Microsoft.Agents.AI.Workflows;
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:9:public sealed class MafPackageBaselineReflectionTests
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:11:    private const string ExpectedMafAssemblyVersionPrefix = "1.8.0.";
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:14:    public void Maf18_symbols_are_classified_from_loaded_runtime_assemblies()
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:21:            typeof(WorkflowBuilder).Assembly,
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:40:                        assembly.Value.StartsWith(ExpectedMafAssemblyVersionPrefix, StringComparison.Ordinal));
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:43:            assembly => assembly.Key.Contains("Workflows", StringComparison.Ordinal) &&
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:44:                        assembly.Value.StartsWith(ExpectedMafAssemblyVersionPrefix, StringComparison.Ordinal));
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:47:        Assert.Contains("WorkflowBuilder", availableTypeNames);
tests\Unit\CanDoItAll.Tests.Unit\MafPackageBaselineReflectionTests.cs:48:        Assert.Contains("AgentWorkflowBuilder", availableTypeNames);
tests\Components\CanDoItAll.Tests.Components\MemoryProviderOperationsPageTests.cs:303:            WorkflowId: null,
tests\Components\CanDoItAll.Tests.Components\MemoryProviderOperationsPageTests.cs:304:            WorkflowNodeId: null,
tests\Unit\CanDoItAll.Tests.Unit\MafToolInvocationArgumentFormatterTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafToolInvocationArgumentFormatterTests.cs:7:public sealed class MafToolInvocationArgumentFormatterTests
tests\Unit\CanDoItAll.Tests.Unit\MafToolInvocationArgumentFormatterTests.cs:22:        var description = MafToolInvocationArgumentFormatter.DescribeToolInvocation(toolCall);
tests\Unit\CanDoItAll.Tests.Unit\MafToolInvocationArgumentFormatterTests.cs:32:        var summary = MafToolInvocationArgumentFormatter.DescribeArguments("not-json");
tests\Components\CanDoItAll.Tests.Components\PluginsPageTests.cs:6:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\PluginsPageTests.cs:63:            services.AddSingleton<ICanDoItAllPlugin, NoWorkflowExecutorPlugin>();
tests\Components\CanDoItAll.Tests.Components\PluginsPageTests.cs:246:            WorkflowExecutorId: Office365PluginConstants.DownloadByCategoryExecutorId));
tests\Components\CanDoItAll.Tests.Components\PluginsPageTests.cs:337:    private sealed class NoWorkflowExecutorPlugin : ICanDoItAllPlugin
tests\Components\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs:254:    public void Workflow_definition_nodes_expose_start_workflow_without_add_workflow()
tests\Components\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs:257:        var node = CreateNode("workflow-definition:11111111-1111-1111-1111-111111111111", ProjectObjectType.WorkflowDefinition, "Delivery workflow", 0, 0);
tests\Unit\CanDoItAll.Tests.Unit\ManagedSeedProviderFallbacksTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:5:using CanDoItAll.AgentFramework.Workflows.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:6:using Microsoft.Agents.AI.Workflows;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:10:public sealed class MafWorkflowEventNormalizerTests
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:18:        var normalizer = new MafWorkflowEventNormalizer();
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:19:        var bindings = MafWorkflowEventBindingIndex.FromDefinition(definition);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:20:        var runId = WorkflowRunId.New();
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:24:            new WorkflowOutputEvent(new WorkflowNodeInput("{\"result\":\"ok\"}"), "work-a"),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:27:        var payload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(record.PayloadJson, JsonOptions)!;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:29:        Assert.Equal(WorkflowEventKind.Output, record.Kind);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:30:        Assert.Equal(new WorkflowNodeId("work-a"), record.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:31:        Assert.Equal(new WorkflowNodeId("work-a"), payload.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:32:        Assert.Equal(new WorkflowExecutorId("test.echo"), payload.ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:34:        Assert.Contains("Workflow output", record.Message, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:40:        var executor = new EchoWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:41:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:43:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:44:                    new MafWorkflowCompiler(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:45:                        new WorkflowDefinitionValidator(),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:46:                        new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor])),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:49:            new InMemoryWorkflowRunStore());
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:54:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:58:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:63:        Assert.Equal(WorkflowRunState.Completed, run.State);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:66:                workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted &&
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:67:                workflowEvent.NodeId == new WorkflowNodeId("work-a"))
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:68:            .Select(workflowEvent => JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(workflowEvent.PayloadJson, JsonOptions))
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:71:        Assert.Equal(new WorkflowExecutorId("test.echo"), completed.ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:72:        Assert.Equal(new WorkflowNodeId("work-a"), completed.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:81:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:83:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:84:                    new FailingWorkflowCompiler("Workflow compile failed token=raw-token-value."),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:87:            new InMemoryWorkflowRunStore());
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:92:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:96:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:100:            workflowEvent.Kind == WorkflowEventKind.Error);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:101:        var payload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(error.PayloadJson, JsonOptions)!;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:102:        var diagnostic = JsonSerializer.Deserialize<WorkflowFailureDiagnosticEnvelope>(payload.InlineJson, JsonOptions)!;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:104:        Assert.Equal(WorkflowRunState.Failed, run.State);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:105:        Assert.Equal("WorkflowCompilationFailed", payload.EventType);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:106:        Assert.Equal(WorkflowFailureKind.Runtime, diagnostic.Kind);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:107:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:108:        Assert.Equal(WorkflowFailureSourceKind.RuntimeBackend, diagnostic.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:109:        Assert.Equal(WorkflowRuntimeBackendKind.InProcess, diagnostic.Source.BackendKind);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:110:        Assert.Equal(definition.Id, diagnostic.WorkflowId);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:111:        Assert.Equal(definition.VersionId, diagnostic.WorkflowVersionId);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:119:    private static WorkflowDefinition CreateDefinition()
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:121:        return new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:122:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:123:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:126:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:127:            new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:128:                new WorkflowNodeId("start"),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:130:                    CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:133:                    CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:140:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:141:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:150:    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:152:        return new WorkflowNode(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:153:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:157:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:163:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:164:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:167:    private static WorkflowNode CreateExecutorNode(string id)
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:169:        return CreateNode(id, WorkflowNodeKind.Executor) with
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:171:            Settings = CreateNode(id, WorkflowNodeKind.Executor).Settings with
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:173:                ExecutorId = new WorkflowExecutorId("test.echo"),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:175:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:180:    private static WorkflowEdge CreateEdge(string id, string source, string target)
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:182:        return new WorkflowEdge(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:183:            new WorkflowEdgeId(id),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:184:            new WorkflowNodeId(source),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:186:            new WorkflowNodeId(target),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:188:            WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:192:    private sealed class EchoWorkflowExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:194:        public WorkflowExecutorDescriptor Descriptor { get; } = new(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:195:            new WorkflowExecutorId("test.echo"),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:198:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:201:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:202:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:205:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:208:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:209:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:210:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:213:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:220:    private sealed class FailingWorkflowCompiler(string errorMessage) : IWorkflowMafCompiler
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:222:        public MafWorkflowBuildResult Compile(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:223:            WorkflowDefinition definition,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:225:            => Compile(definition, components, WorkflowPreviewSimulationPlan.Empty);
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:227:        public MafWorkflowBuildResult Compile(
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:228:            WorkflowDefinition definition,
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:230:            WorkflowPreviewSimulationPlan? previewSimulationPlan)
tests\Unit\CanDoItAll.Tests.Unit\MafWorkflowEventNormalizerTests.cs:233:                WorkflowCompilationResult.Failed(WorkflowValidationResult.Success, errorMessage));
tests\Unit\CanDoItAll.Tests.Unit\MafWorkspaceSearchSupportTests.cs:1:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\MafWorkspaceSearchSupportTests.cs:5:public sealed class MafWorkspaceSearchSupportTests
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:5:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:154:    public void AgentFrameworkModule_registers_generic_contributor_and_native_module_does_not_register_maf_memory_surfaces()
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:157:        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:174:            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor) &&
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:175:                          descriptor.ImplementationType == typeof(CognitiveMemoryRecallWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:178:            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor) &&
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:179:                          descriptor.ImplementationType == typeof(CognitiveMemoryProbeWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:182:            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor) &&
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:183:                          descriptor.ImplementationType == typeof(CognitiveMemoryLearningProposalWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:193:            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework")
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:200:            "CognitiveMemoryRecallWorkflowExecutor",
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:201:            "CognitiveMemoryProbeWorkflowExecutor",
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentContextContributorTests.cs:202:            "CognitiveMemoryLearningProposalWorkflowExecutor",
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:3:using CanDoItAll.AgentFramework.Tooling;
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:6:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:196:    public void AddAgentFrameworkModule_registers_generic_memory_runtime_tool_provider()
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:199:        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:226:                        MemoryProviderAssignmentScope.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:244:        Assert.Equal(MemoryProviderAssignmentScope.Workflow, Assert.Single(settings.ProviderAssignments).Scope);
tests\Unit\CanDoItAll.Tests.Unit\MemoryAgentRuntimeToolProviderTests.cs:309:                [MemoryAgentRuntimeToolTags.WorkflowId] = "workflow-a",
tests\Unit\CanDoItAll.Tests.Unit\McpRuntimeContractsTests.cs:1:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\McpRuntimeContractsTests.cs:2:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\McpRuntimeContractsTests.cs:3:using CanDoItAll.AgentFramework.Mcp;
tests\Unit\CanDoItAll.Tests.Unit\McpRuntimeContractsTests.cs:4:using CanDoItAll.AgentFramework.Mcp.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\ManagedCodeMarkItDownDocumentMarkdownConverterTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:3:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:7:public sealed class MemoryMafIntegrationCheckpointTests
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:10:    public void Maf_memory_entry_points_use_shared_policy_resolver_and_result_shaper()
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:13:        var toolSource = ReadSource(root, "src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/MemoryAgentRuntimeToolProvider.cs");
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:14:        var workflowSource = ReadSource(root, "src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutor.cs");
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:15:        var contextSource = ReadSource(root, "src/Modules/CanDoItAll.Modules.AgentFramework/Context/MemoryAgentContextContributor.cs");
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:17:        Assert.Contains("MemoryMafProviderPolicyResolver.Resolve", toolSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:18:        Assert.Contains("MemoryMafProviderPolicyResolver.Resolve", workflowSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:19:        Assert.Contains("MemoryMafProviderPolicyResolver.Resolve", contextSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:20:        Assert.Contains("MemoryMafToolResultShaper.ToQueryResult", toolSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:21:        Assert.Contains("MemoryMafToolResultShaper.ToQueryResult", workflowSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:32:        var resolution = MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
tests\Unit\CanDoItAll.Tests.Unit\MemoryMafIntegrationCheckpointTests.cs:77:        var shaped = MemoryMafToolResultShaper.ToQueryResult(result);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:6:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:12:public sealed class MemoryWorkflowExecutorTests
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:19:        var descriptors = new MemoryWorkflowExecutorDescriptorSource().ListExecutorDescriptors().ToArray();
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:22:        Assert.Equal(WorkflowExecutorIds.Memory, descriptor.Id);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:24:        Assert.True(MemoryWorkflowExecutorCompatibility.TryMapLegacyExecutorId(new WorkflowExecutorId("cognitive-memory.recall"), out var mappedId));
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:25:        Assert.Equal(WorkflowExecutorIds.Memory, mappedId);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:33:            QueryResult = RecordingMemoryOperationHandler.CompletedQuery("Workflow context", "Use the generic handler.")
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:35:        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:39:            new MemoryWorkflowExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:41:                Operation = MemoryWorkflowOperation.ContextQuery,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:48:        Assert.Equal("Workflow context", result.Summary);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:52:        Assert.Equal(MemoryOperationCallerKind.WorkflowExecutor, handler.LastQuery.Caller.Kind);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:63:        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:67:            new MemoryWorkflowExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:69:                Operation = MemoryWorkflowOperation.ContextQuery,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:85:        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:89:            new MemoryWorkflowExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:91:                Operation = MemoryWorkflowOperation.ContextQuery,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:112:        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:116:            new MemoryWorkflowExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:118:                Operation = MemoryWorkflowOperation.ContextQuery,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:131:        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:135:            new MemoryWorkflowExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:137:                Operation = MemoryWorkflowOperation.ContextQuery,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:151:        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:155:            new MemoryWorkflowExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:157:                Operation = MemoryWorkflowOperation.IngestText,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:159:                Title = "Workflow note",
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:171:    public void AddAgentFrameworkModule_registers_generic_memory_workflow_executor()
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:174:        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:178:            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor) &&
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:179:                          descriptor.ImplementationType == typeof(MemoryWorkflowExecutor) &&
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:183:            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorDescriptorSource) &&
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:184:                          descriptor.ImplementationType == typeof(MemoryWorkflowExecutorDescriptorSource) &&
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:189:        MemoryWorkflowExecutor executor,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:190:        MemoryWorkflowExecutorSettings settings,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:195:            new WorkflowNodeInput(inputJson));
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:200:    private static WorkflowExecutorExecutionContext CreateContext(
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:201:        WorkflowExecutorDescriptor descriptor,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:202:        MemoryWorkflowExecutorSettings settings)
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:204:        var node = new WorkflowNode(
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:205:            new WorkflowNodeId("workflow-memory"),
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:206:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:209:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:215:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:216:                ResultShape: WorkflowExecutorDescriptorFactory.JsonShape)
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:220:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:222:        var definition = new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:223:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:224:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:227:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:228:            new WorkflowGraph(node.Id, [node], []),
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:229:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:230:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:238:        return new WorkflowExecutorExecutionContext(
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:243:            WorkflowExecutorExecutionPolicy.Default)
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:245:            RunId = WorkflowRunId.New()
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:251:        private static readonly MemoryProviderProfile WorkflowProvider = CreateProvider("memory.workflow");
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:271:                        "Workflow",
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:273:                        [new MemoryCitation("workflow:source", "Workflow source")],
tests\Unit\CanDoItAll.Tests.Unit\MemoryWorkflowExecutorTests.cs:281:                MemoryProviderSelectionResult.Selected(WorkflowProvider, MemoryProviderSelectionReason.ExplicitProvider, MemoryCapabilityIds.ContextQuerySync),
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:4:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:6:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:74:            new WorkflowProjectStructureExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:76:                Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:116:            new WorkflowProjectStructureExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:118:                Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:150:            new WorkflowProjectStructureExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:152:                Operation = WorkflowProjectStructureOperation.CreateAsset,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:195:            new WorkflowProjectStructureExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:197:                Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:226:                new WorkflowProjectStructureExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:228:                    Operation = WorkflowProjectStructureOperation.ListProjects
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:282:    private static async Task<WorkflowNodeExecutionResult> ExecuteProjectStructureAsync(
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:284:        WorkflowProjectStructureExecutorSettings settings,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:287:        var executor = new ProjectStructureWorkflowExecutor(gateway);
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:288:        var node = new WorkflowNode(
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:289:            new WorkflowNodeId("project-structure"),
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:290:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:293:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:299:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:300:                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:302:                ExecutorId = BuiltInWorkflowExecutorDescriptors.ProjectStructure.Id,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:304:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:306:        var definition = new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:307:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:308:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:311:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:312:            new WorkflowGraph(node.Id, [node], []),
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:313:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:314:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:321:        var context = new WorkflowExecutorExecutionContext(
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:326:            WorkflowExecutorExecutionPolicy.Default);
tests\Unit\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs:328:        return await executor.ExecuteAsync(context, new WorkflowNodeInput(inputJson));
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:61:            capabilities: PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.SettingsRenderer | PluginCapabilityKind.SecretReference);
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:66:        Assert.Contains(result.Issues, issue => issue.Code == PluginManifestValidationIssueCode.DuplicateWorkflowExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:107:                    PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:108:                        WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:109:                        WorkflowExecutorCapabilityFlags.UsesSecrets |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:110:                        WorkflowExecutorCapabilityFlags.RunsHostCommand |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:111:                        WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:112:                        WorkflowExecutorApprovalRequirement.AlwaysRequired),
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:113:                    DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Fake mode")
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:116:            capabilities: PluginCapabilityKind.WorkflowExecutor);
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:137:                    PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:138:                        WorkflowExecutorCapabilityFlags.WritesExternalData |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:139:                        WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:140:                        WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:141:                        WorkflowExecutorApprovalRequirement.NotRequired)
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:146:                PluginCapabilityKind.WorkflowExecutor |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:167:                    PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:168:                        WorkflowExecutorCapabilityFlags.WritesExternalData |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:169:                        WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:170:                        WorkflowExecutorCapabilityFlags.UsesSecrets |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:171:                        WorkflowExecutorCapabilityFlags.IdempotentExternalMarker |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:172:                        WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:173:                        WorkflowExecutorApprovalRequirement.NotRequired),
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:174:                    SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:177:                    DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Fake marker mode")
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:182:                PluginCapabilityKind.WorkflowExecutor |
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:225:            capabilities: PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.SettingsRenderer | PluginCapabilityKind.SecretReference) with
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:227:            Tags = [PluginDescriptorTags.Email, PluginDescriptorTags.Workflow]
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:240:        Assert.Equal(descriptor.WorkflowExecutors[0].ExecutorId, roundTrip.WorkflowExecutors[0].ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:259:        Assert.DoesNotContain("CanDoItAll.AgentFramework.Core", referencedAssemblyNames);
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:260:        Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", referencedAssemblyNames);
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:267:        IReadOnlyList<PluginWorkflowExecutorDescriptor>? workflowExecutors = null,
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:293:    private static PluginWorkflowExecutorDescriptor CreateExecutor(
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:297:            new WorkflowExecutorId(executorId),
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:300:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:303:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:304:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\PluginManifestTests.cs:305:            WorkflowExecutorExecutionPolicy.Default);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:4:using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:11:public sealed class PluginWorkflowExecutorBoundaryTests
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:17:            "src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins.csproj");
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:22:                "CanDoItAll.AgentFramework.Models",
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:23:                "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:38:            capabilities: PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.OAuth2,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:45:            PluginGrantDecision.Allow(plugin.Descriptor.Id, PluginCapabilityKind.WorkflowExecutor),
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:51:        var source = new PluginWorkflowExecutorDescriptorSource([plugin], grantEvaluator);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:55:        Assert.Equal(plugin.Descriptor.WorkflowExecutors[0].ExecutorId, descriptor.Id);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:56:        Assert.Equal(WorkflowExecutorSourceKind.BundledPlugin, descriptor.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:58:        Assert.Equal(WorkflowExecutorTrustLevel.BundledPlugin, descriptor.Source.TrustLevel);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:59:        Assert.Equal(WorkflowExecutorAvailabilityKind.Unavailable, descriptor.Availability.Kind);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:62:        Assert.Equal(plugin.Descriptor.WorkflowExecutors[0].SideEffects, descriptor.SideEffects);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:63:        Assert.Equal(plugin.Descriptor.WorkflowExecutors[0].PermissionPolicy, descriptor.PermissionPolicy);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:76:        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:78:            [typeof(RuntimeFixtureWorkflowExecutor)],
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:82:        var executor = Assert.Single(provider.GetRequiredService<IEnumerable<IWorkflowExecutor>>());
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:83:        var descriptorSource = Assert.Single(provider.GetRequiredService<IEnumerable<IWorkflowExecutorDescriptorSource>>());
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:86:        Assert.IsType<RuntimePackageWorkflowExecutor>(executor);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:87:        Assert.Equal(RuntimeFixtureWorkflowExecutor.ExecutorId, descriptor.Id);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:88:        Assert.Equal(WorkflowExecutorSourceKind.LocalPackage, descriptor.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:91:        Assert.Equal(WorkflowExecutorTrustLevel.LocalPackage, descriptor.Source.TrustLevel);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:103:        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:109:        var exception = Assert.Throws<PluginWorkflowExecutorActivationException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:110:            provider.GetRequiredService<IEnumerable<IWorkflowExecutor>>().ToArray());
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:135:            "PluginWorkflowExecutorDescriptorSource.cs");
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:137:        Assert.Contains("AddPluginWorkflowExecutorBoundary()", moduleSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:138:        Assert.Contains("IPluginWorkflowExecutorGrantEvaluator", moduleSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:145:        PluginCapabilityKind capabilities = PluginCapabilityKind.WorkflowExecutor,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:171:    private static PluginWorkflowExecutorDescriptor CreatePluginExecutor()
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:173:            RuntimeFixtureWorkflowExecutor.ExecutorId,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:176:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:179:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:180:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:181:            WorkflowExecutorExecutionPolicy.Default)
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:183:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:184:                WorkflowExecutorCapabilityFlags.UsesNetwork |
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:185:                WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:186:                WorkflowExecutorApprovalRequirement.NotRequired),
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:187:            SideEffects = WorkflowExecutorSideEffectDescriptor.None,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:188:            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Boundary test preview.")
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:226:    private sealed class StaticGrantEvaluator(params PluginGrantDecision[] decisions) : IPluginWorkflowExecutorGrantEvaluator
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:239:    private sealed class RuntimeFixtureWorkflowExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:241:        public static WorkflowExecutorId ExecutorId { get; } = new("runtime.fixture.executor");
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:243:        public WorkflowExecutorDescriptor Descriptor { get; } = new(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:247:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:250:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:251:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:254:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:257:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:258:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:259:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:261:            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:267:    private sealed class MissingDependencyRuntimeExecutor(MissingDependency dependency) : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:269:        public WorkflowExecutorDescriptor Descriptor => new(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:270:            new WorkflowExecutorId("runtime.missing-dependency"),
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:273:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:276:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:277:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:280:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:283:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:284:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:285:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\PluginWorkflowExecutorBoundaryTests.cs:287:            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Components\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:6:using CanDoItAll.AgentFramework.Core;
tests\Components\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:7:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:146:        var agentWorkspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Components\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:336:        var agentWorkspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
tests\Components\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs:1527:        IAgentFrameworkWorkspaceService agentWorkspaceService,
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:15:            StandardProcessAdapterDriverIds.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:16:            StandardProcessAdapterDescriptors.WorkflowAdapter,
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:26:                        "Workflow failed with a retriable infrastructure error.",
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:32:                "Workflow adapter completed with a restricted diagnostic.",
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:62:        Assert.Equal(ProcessExecutionAdapterKind.Workflow, request.Kind);
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:82:            StandardProcessAdapterDriverIds.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:83:            StandardProcessAdapterDescriptors.WorkflowAdapter,
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:100:        Assert.Equal(StandardProcessAdapterDriverIds.Workflow, driver.Descriptor.DriverId);
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:101:        Assert.Equal(StandardProcessAdapterDescriptors.WorkflowAdapter, driver.Descriptor.Adapter);
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:108:            StandardProcessAdapterDriverIds.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:109:            StandardProcessAdapterDescriptors.WorkflowAdapter,
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:115:            new HashSet<CapabilityTag> { StandardProcessAdapterCapabilities.WorkflowExecution },
tests\Unit\CanDoItAll.Tests.Unit\ProcessExecutionAdapterBoundaryTests.cs:123:                StandardProcessAdapterDriverIds.Workflow.Value
tests\Unit\CanDoItAll.Tests.Unit\ProcessDefinitionCatalogProjectionTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessDefinitionCatalogProjectionTests.cs:1290:        Assert.Equal("adr-output", artifact.WorkflowOutputId);
tests\Unit\CanDoItAll.Tests.Unit\ProcessDefinitionCatalogProjectionTests.cs:1870:                              "WorkflowOutputId": "adr-output",
tests\Unit\CanDoItAll.Tests.Unit\ProcessDefinitionCatalogProjectionTests.cs:1871:                              "WorkflowOutputName": "Architecture decision record",
tests\Unit\CanDoItAll.Tests.Unit\ProcessDefinitionCatalogProjectionTests.cs:1872:                              "WorkflowOutputKind": "Artifact",
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:3:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:4:using CanDoItAll.Modules.AgentFramework.Hosting;
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:43:        var resolver = new AgentFrameworkProcessLaunchExecutorResolver(
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:791:        var repairService = new AgentFrameworkProcessRuntimeStepAssignmentRepairService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:853:    private static AgentFrameworkProcessLaunchExecutorResolver CreateResolver(ResolverWorkspaceFactory workspaceFactory)
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:855:        return new AgentFrameworkProcessLaunchExecutorResolver(
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:864:    private static IAgentReferenceDataProvider CreateReferenceDataProvider(IAgentFrameworkWorkspaceService workspaceService)
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:1559:        public ResolverWorkspaceFactory(IAgentFrameworkWorkspaceService workspaceService)
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:1564:        public IAgentFrameworkWorkspaceService WorkspaceService { get; }
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:1566:        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => WorkspaceService;
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:1568:        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) => WorkspaceService;
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:1577:        IReadOnlyList<ProviderProfile> providers) : IAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchExecutorResolverTests.cs:1690:        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:115:    public void AgentFramework_step_brief_keeps_project_structure_guidance_outside_generic_application_layer()
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:130:        var prompt = BuildStepPrompt(new AgentFrameworkProcessStepBriefBuilder(), runId, step);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:135:        Assert.Contains("AgentFramework execution contract:", prompt, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:231:    public void AgentFramework_step_brief_requires_workspace_file_probe_before_project_structure_fallback()
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:285:            new AgentFrameworkProcessStepBriefBuilder(),
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:296:        Assert.Contains("AgentFramework upstream artifact read rule:", prompt, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:297:        Assert.Contains("AgentFramework dependency artifact refs:", prompt, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:307:    public void AgentFramework_step_brief_uses_launch_variables_as_project_structure_context_instead_of_invented_snapshot_file()
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:321:            new AgentFrameworkProcessStepBriefBuilder(),
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:338:        Assert.Contains("AgentFramework project-structure context source:", prompt, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:363:    public void AgentFramework_step_brief_adds_product_mutation_gate_for_mutable_steps()
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:379:            new AgentFrameworkProcessStepBriefBuilder(),
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:387:        Assert.Contains("AgentFramework product mutation gate:", prompt, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:396:    public void AgentFramework_step_brief_lists_dependency_artifact_refs_without_slot_mapping()
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:422:            new AgentFrameworkProcessStepBriefBuilder(),
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:432:        Assert.Contains("AgentFramework dependency artifact refs:", prompt, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:510:    public void AgentFramework_step_brief_requires_evidence_tools_before_finalizer()
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:542:            new AgentFrameworkProcessStepBriefBuilder(),
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:560:        Assert.Contains("AgentFramework own-output bootstrap:", prompt, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:574:    public void AgentFramework_step_brief_appends_process_scoped_instruction_fragments()
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:591:        var prompt = BuildStepPrompt(new AgentFrameworkProcessStepBriefBuilder(), runId, step);
tests\Unit\CanDoItAll.Tests.Unit\ProcessLaunchPromptTests.cs:593:        Assert.Contains("AgentFramework process-scoped instructions:", prompt, StringComparison.Ordinal);
tests\Components\CanDoItAll.Tests.Components\ProviderModelPricingEditorTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessProjectionPipelineTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessProjectionPipelineTests.cs:1212:        Assert.Equal("AgentFramework execution run", activeAgent.ObservationSource);
tests\Unit\CanDoItAll.Tests.Unit\ProcessProjectionPipelineTests.cs:1346:        Assert.Equal("Runtime claim without AgentFramework execution evidence", activeAgent.ObservationSource);
tests\Unit\CanDoItAll.Tests.Unit\ProcessProjectionPipelineTests.cs:1347:        Assert.Contains("No AgentFramework execution run was observed", activeAgent.CurrentActivity, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProcessPersistenceStoreTests.cs:10:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:12:public sealed class ProcessRuntimeDispatchQueueTests
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:17:        var queue = new ProcessRuntimeDispatchQueue();
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:31:        var queue = new ProcessRuntimeDispatchQueue(new ProcessRuntimeDispatchQueueOptions
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:39:        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(firstRunId, "unit-test"));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:46:                new ProcessRuntimeDispatchQueueRequest(cancelledRunId, "unit-test"),
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:52:        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(cancelledRunId, "unit-test"));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:61:        var queue = new ProcessRuntimeDispatchQueue();
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:64:        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(runId, "unit-test"));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:65:        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(runId, "unit-test"));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:71:        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(runId, "unit-test"));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:80:        var exception = Assert.Throws<InvalidOperationException>(() => new ProcessRuntimeDispatchQueue(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:81:            new ProcessRuntimeDispatchQueueOptions
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:86:        Assert.Contains(nameof(ProcessRuntimeDispatchQueueOptions.ImmediateQueueCapacity), exception.Message);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:92:        var result = new ProcessRuntimeDispatchResult(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:101:        Assert.False(ProcessRuntimeDispatchQueueWorker.ShouldLogDispatchDiagnosticsAsWarning(result));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:107:        var result = new ProcessRuntimeDispatchResult(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:115:        Assert.False(ProcessRuntimeDispatchQueueWorker.ShouldLogDispatchDiagnosticsAsWarning(result));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:121:        var result = new ProcessRuntimeDispatchResult(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:127:        Assert.True(ProcessRuntimeDispatchQueueWorker.ShouldLogDispatchDiagnosticsAsWarning(result));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:174:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:218:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:245:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:271:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:294:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:324:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:348:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:385:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:422:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:467:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:507:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:546:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:585:        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1098:        var candidates = await AgentFrameworkProcessExecutionClaimRecoveryReconciler
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1115:        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1135:        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1148:        selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1161:        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1187:        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1200:        selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1213:        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1227:        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1241:        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1249:        Assert.True(AgentFrameworkProcessExecutionClaimRecoveryReconciler.ShouldReleaseClaimWithoutExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1256:        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryReconciler.ShouldReleaseClaimWithoutExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1268:        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryReconciler.ShouldReleaseClaimWithoutExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1280:        Assert.True(AgentFrameworkProcessExecutionClaimRecoveryReconciler.ShouldReleaseClaimWithoutExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1306:        Assert.True(AgentFrameworkProcessExecutionRecoveryObserver.HasNewerActiveExecutionRun(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1316:        Assert.False(AgentFrameworkProcessExecutionRecoveryObserver.HasNewerActiveExecutionRun(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1326:        Assert.False(AgentFrameworkProcessExecutionRecoveryObserver.HasNewerActiveExecutionRun(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1336:        Assert.True(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1339:        Assert.True(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1342:        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1345:        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchQueueTests.cs:1348:        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:11:public sealed class ProcessRuntimeDispatchApplicationServiceTests
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:53:        var exception = Assert.Throws<InvalidOperationException>(() => new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:61:            new ProcessRuntimeDispatchOptions
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:93:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:145:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:298:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:338:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:388:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:396:            new ProcessRuntimeDispatchOptions
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:476:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:534:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:591:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:633:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:674:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:712:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:754:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:794:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:867:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:914:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:951:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:993:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1047:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1100:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1155:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1213:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1251:        var service = new ProcessRuntimeDispatchApplicationService(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1259:            new ProcessRuntimeDispatchOptions
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1691:    private sealed class RecordingDispatchQueue : IProcessRuntimeDispatchQueue
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1693:        public List<ProcessRuntimeDispatchQueueRequest> Requests { get; } = [];
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1696:            ProcessRuntimeDispatchQueueRequest request,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeDispatchApplicationServiceTests.cs:1865:            throw new ProcessRuntimeDispatchDeferredException(
tests\Components\CanDoItAll.Tests.Components\ProviderModelSelectorTests.cs:3:using CanDoItAll.AgentFramework.Components;
tests\Components\CanDoItAll.Tests.Components\ProviderModelSelectorTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:5:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:1285:                var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:1296:                        ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3264:        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3289:        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3330:        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3354:        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolveExistingPendingChildRunAsync(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3412:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3422:            var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3427:                        ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3494:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3502:            var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3507:                        ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3547:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3558:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3610:        var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3625:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3671:        var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3680:        var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3685:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3711:            new InvalidOperationException("Finalizer tool 'submit_process_step_outcome' in Required mode failed validation. Errors: agent.finalizer.missing."));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3712:        var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3723:                ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3762:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3773:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3822:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3833:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3891:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3902:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:3989:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4000:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4084:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4095:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4180:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4191:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4282:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4293:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4381:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4392:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4447:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4458:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4522:            var adapter = new AgentFrameworkProcessExecutionAdapter(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4533:                    ProcessExecutionAdapterKind.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4569:            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4586:            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4929:    private static IAgentReferenceDataProvider CreateReferenceDataProvider(IAgentFrameworkWorkspaceService workspaceService)
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4934:    private sealed class FakeWorkspaceFactory(IAgentFrameworkWorkspaceService workspaceService) : ICanDoItAllAgentWorkspaceFactory
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4936:        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4939:        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope)
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:4955:        IReadOnlyList<ExecutionRunDetail>? executionDetails = null) : IAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationAdapterTests.cs:5126:        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:10:using Capabilities = CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:46:            AgentFinalizerPolicies.RequiredFinalizerModeValue,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:47:            metadataRoot.GetProperty(AgentFinalizerPolicies.FinalizerModeMetadataKey).GetString());
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:202:        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:230:        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:303:        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:396:            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessRuntimeUsageTelemetryReader")
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:453:        var reader = new AgentFrameworkProcessRuntimeUsageTelemetryReader(
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:622:            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:665:        IReadOnlyDictionary<Guid, ExecutionRunDetail> executionRunDetails) : IAgentFrameworkWorkspaceService
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeIntegrationMetadataTests.cs:807:        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeOperatorApplicationServiceTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeOperatorApplicationServiceTests.cs:487:    private sealed class RecordingDispatchQueue : IProcessRuntimeDispatchQueue
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeOperatorApplicationServiceTests.cs:489:        public List<ProcessRuntimeDispatchQueueRequest> Requests { get; } = [];
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeOperatorApplicationServiceTests.cs:492:            ProcessRuntimeDispatchQueueRequest request,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:4:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:136:    public async Task Workflow_gateway_adapter_translates_scope_id_into_workflow_run_request()
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:138:        var provider = new CapturingWorkflowRuntimeEvidenceSourceProvider(CreateWorkflowSnapshot(RunId));
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:139:        var adapter = new WorkflowRuntimeMemorySourceGatewayAdapter(provider);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:141:            MemorySourceKind.WorkflowRuntime,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:143:            GenericMemorySourceScope.Workflow,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:147:                [MemorySourceKind.WorkflowRuntime],
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:148:                [GenericMemorySourceScope.Workflow]),
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:166:        agentFrameworkServices.AddAgentFrameworkModule(configuration);
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:181:                descriptor.ImplementationType == typeof(WorkflowRuntimeMemorySourceGatewayAdapter) &&
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:338:    private static MemorySourceSnapshot CreateWorkflowSnapshot(Guid runId)
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:341:            MemorySourceKind.WorkflowRuntime,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:343:            MemorySourceEntityKind.WorkflowRun,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:347:            MemorySourceKind.WorkflowRuntime,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:348:            MemorySourceEntityKind.WorkflowRun,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:349:            $"Workflow run {runId:D}",
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:355:                MemorySourceKind.WorkflowRuntime,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:357:                MemorySourceEntityKind.WorkflowRun,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:373:                MemorySourceSnapshotId.Create(MemorySourceKind.WorkflowRuntime, runId, item.ContentHash),
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:374:                MemorySourceKind.WorkflowRuntime,
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:382:                MemorySourceSnapshotProviderVersions.WorkflowRuntime),
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:422:    private sealed class CapturingWorkflowRuntimeEvidenceSourceProvider(MemorySourceSnapshot snapshot) : IWorkflowRuntimeEvidenceSourceProvider
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:426:        public WorkflowRuntimeEvidenceSourceRequest? LastRequest { get; private set; }
tests\Unit\CanDoItAll.Tests.Unit\ProcessRuntimeSourceGatewayAdapterTests.cs:429:            WorkflowRuntimeEvidenceSourceRequest request,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:54:            TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:109:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:113:                    "Workflow for release readiness validation.",
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:178:        var workspace = CreateWorkflowWorkspace(workflowId, workflowVersionId);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:182:            TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:190:        var optionService = new StubSchedulerWorkflowInputOptionService(projectId);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:195:            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:196:            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:198:            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new StubSchedulerWorkflowInputSchemaService(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:199:                CreateOffice365WorkflowInputSchema(workflowId, workflowVersionId)));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:200:            services.AddSingleton<ISchedulerWorkflowInputOptionService>(optionService);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:260:        var workspace = CreateWorkflowWorkspace(workflowId, workflowVersionId);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:266:                TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:278:            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:279:            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:281:            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new StubSchedulerWorkflowInputSchemaService(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:282:                CreateOffice365WorkflowInputSchema(workflowId, workflowVersionId)));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:283:            services.AddSingleton<ISchedulerWorkflowInputOptionService>(new StubSchedulerWorkflowInputOptionService(Guid.NewGuid()));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:297:            Assert.Contains("Workflow input needs attention", cut.Markup);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:309:        var workspace = CreateWorkflowWorkspace(workflowId, workflowVersionId);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:315:                TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:327:            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:328:            services.RemoveAll<ISchedulerWorkflowInputOptionService>();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:330:            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new StubSchedulerWorkflowInputSchemaService(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:331:                CreateOffice365WorkflowInputSchema(workflowId, workflowVersionId)));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:332:            services.AddSingleton<ISchedulerWorkflowInputOptionService>(new StubSchedulerWorkflowInputOptionService(projectId));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:365:            Assert.Contains("Workflow input needs attention", cut.Markup);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:371:    private static SchedulerPlannerWorkspace CreateWorkflowWorkspace(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:380:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:415:            Description = "At 09:00 on Monday through Friday every month (UTC). / Workflow / Office365 email watch summary",
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:420:            EventType = SchedulerPlanTargetKind.Workflow.ToString(),
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:422:            Category = SchedulerPlanTargetKind.Workflow.ToString(),
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:430:                SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:449:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:497:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:512:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:519:                    SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:520:                    "Workflow run is waiting for approval.",
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:527:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:541:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:563:    private static SchedulerWorkflowInputSchema CreateOffice365WorkflowInputSchema(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:567:        return new SchedulerWorkflowInputSchema(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:568:            new WorkflowId(workflowId),
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:569:            new WorkflowVersionId(workflowVersionId),
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:572:                new WorkflowInputParameterDescriptor(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:575:                    WorkflowInputParameterKind.ExternalConnectionId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:580:                    new WorkflowInputParameterOptionSource(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:581:                        WorkflowInputParameterOptionSourceKind.Office365Connections,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:587:                new WorkflowInputParameterDescriptor(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:590:                    WorkflowInputParameterKind.EmailAddress,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:595:                    new WorkflowInputParameterOptionSource(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:596:                        WorkflowInputParameterOptionSourceKind.CrmContacts,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:602:                new WorkflowInputParameterDescriptor(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:605:                    WorkflowInputParameterKind.ProjectId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:610:                    new WorkflowInputParameterOptionSource(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:611:                        WorkflowInputParameterOptionSourceKind.ProjectStructureProjects,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:617:                new WorkflowInputParameterDescriptor(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:620:                    WorkflowInputParameterKind.ProjectNodeId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:625:                    new WorkflowInputParameterOptionSource(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:626:                        WorkflowInputParameterOptionSourceKind.ProjectStructureNodes,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:632:                new WorkflowInputParameterDescriptor(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:635:                    WorkflowInputParameterKind.Category,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:640:                    WorkflowInputParameterOptionSource.None,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:644:                new WorkflowInputParameterDescriptor(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:647:                    WorkflowInputParameterKind.Integer,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:652:                    WorkflowInputParameterOptionSource.None,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:788:                TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:855:        var replacementWorkflowId = Guid.NewGuid();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:856:        var replacementWorkflowVersionId = Guid.NewGuid();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:864:                    SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:865:                    replacementWorkflowId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:866:                    replacementWorkflowVersionId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:876:                TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:928:            Assert.Equal(replacementWorkflowId, schedulerService.LastSavedEditor?.TargetId);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:929:            Assert.Equal(replacementWorkflowVersionId, schedulerService.LastSavedEditor?.TargetVersionId);
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:945:                TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:988:                TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1031:                TargetKind = SchedulerPlanTargetKind.Workflow,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1061:    private sealed class StubSchedulerWorkflowInputSchemaService(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1062:        SchedulerWorkflowInputSchema schema) : ISchedulerWorkflowInputSchemaService
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1066:        public Task<SchedulerWorkflowInputSchema> ResolveSchemaAsync(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1067:            WorkflowId workflowId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1068:            WorkflowVersionId? versionId = null,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1074:        public Task<SchedulerWorkflowInputValidationResult> ValidateInputAsync(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1075:            WorkflowId workflowId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1076:            WorkflowVersionId? versionId,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1080:            var issues = new List<SchedulerWorkflowInputValidationIssue>();
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1088:                return Task.FromResult(new SchedulerWorkflowInputValidationResult(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1091:                    [new SchedulerWorkflowInputValidationIssue(string.Empty, exception.Message)]));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1096:                return Task.FromResult(new SchedulerWorkflowInputValidationResult(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1099:                    [new SchedulerWorkflowInputValidationIssue(string.Empty, "Workflow input must be a JSON object.")]));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1106:            return Task.FromResult(new SchedulerWorkflowInputValidationResult(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1116:            List<SchedulerWorkflowInputValidationIssue> issues)
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1123:                issues.Add(new SchedulerWorkflowInputValidationIssue(propertyName, message));
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1128:    private sealed class StubSchedulerWorkflowInputOptionService(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1129:        Guid projectId) : ISchedulerWorkflowInputOptionService
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1131:        public Task<IReadOnlyList<WorkflowInputParameterOption>> ListOptionsAsync(
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1132:            WorkflowInputParameterDescriptor parameter,
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1136:            IReadOnlyList<WorkflowInputParameterOption> options = parameter.OptionSource.Kind switch
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1138:                WorkflowInputParameterOptionSourceKind.Office365Connections =>
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1140:                    new WorkflowInputParameterOption(Guid.NewGuid().ToString("D"), "Office365 Main", "Connected")
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1142:                WorkflowInputParameterOptionSourceKind.CrmContacts =>
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1144:                    new WorkflowInputParameterOption("ada@example.com", "Ada Lovelace <ada@example.com>", "CRM contact")
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1146:                WorkflowInputParameterOptionSourceKind.ProjectStructureProjects =>
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1148:                    new WorkflowInputParameterOption(projectId.ToString("D"), "Project Alpha", "Active")
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1150:                WorkflowInputParameterOptionSourceKind.ProjectStructureNodes
tests\Components\CanDoItAll.Tests.Components\SchedulerPlannerPageTests.cs:1154:                    new WorkflowInputParameterOption("node-inbox", "Inbox", "ProjectRoot / Active")
tests\Unit\CanDoItAll.Tests.Unit\ProjectMemoryIngestionServiceTests.cs:7:using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;
tests\Unit\CanDoItAll.Tests.Unit\ProjectMemoryIngestionServiceTests.cs:36:        Assert.Equal(MafMemorySourceKind.WorkbenchProjectStructure, request.Payload.SourceGatewayRequest.SourceKind);
tests\Unit\CanDoItAll.Tests.Unit\ProjectMemoryIngestionServiceTests.cs:106:                CapturedSnapshotId: new CanDoItAll.AgentFramework.Core.MemorySourceSnapshotId("maf.snapshot.project.1"),
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:54:    public void Workflow_metadata_is_scoped_and_requires_workflow_id()
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:56:        var workflowId = WorkflowId.New();
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:59:            Workflow = new ProjectWorkflowNodeMetadata
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:61:                WorkflowId = workflowId,
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:62:                WorkflowName = "Order reconciliation"
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:67:        var normalized = ProjectNodeKindRegistry.NormalizeMetadata(ProjectObjectType.WorkflowDefinition, string.Empty, metadata, string.Empty, null);
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:69:        Assert.Equal(ProjectNodeKindFamily.Workflow, ProjectNodeKindRegistry.ResolveDescriptor(ProjectObjectType.WorkflowDefinition, string.Empty).Family);
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:70:        Assert.NotNull(normalized.Workflow);
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:71:        Assert.Equal(workflowId, normalized.Workflow!.WorkflowId);
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:75:                ProjectObjectType.WorkflowDefinition,
tests\Unit\CanDoItAll.Tests.Unit\ProjectNodeKindRegistryTests.cs:79:                    Workflow = new ProjectWorkflowNodeMetadata()
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:1:using CanDoItAll.Modules.AgentFramework;
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:11:    public void AgentFramework_contribution_inserts_workflows_after_agents()
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:13:        var items = ShellNavigation.GetItems(0, [new AgentFrameworkShellNavigationContributor()]);
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:27:        var item = ShellNavigation.MatchRoute("agents/workflows", [new AgentFrameworkShellNavigationContributor()]);
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:30:        Assert.Equal("Workflows", item.Title);
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:39:                new AgentFrameworkShellNavigationContributor(),
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:59:                new AgentFrameworkShellNavigationContributor(),
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:85:    public void AgentFramework_contribution_marks_workflows_as_subitem_for_future_menu_design()
tests\Components\CanDoItAll.Tests.Components\ShellNavigationContributionTests.cs:87:        var contribution = Assert.Single(new AgentFrameworkShellNavigationContributor().GetShellNavigationContributions());
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeToolProviderTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeToolProviderTests.cs:6:public sealed class ProjectStructureAgentRuntimeToolProviderTests
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeToolProviderTests.cs:18:        Assert.False(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeToolProviderTests.cs:29:        Assert.True(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeToolProviderTests.cs:45:        Assert.True(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeToolProviderTests.cs:58:        Assert.False(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeAssetContentSanitizerTests.cs:8:public sealed class ProjectStructureAgentRuntimeAssetContentSanitizerTests
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeAssetContentSanitizerTests.cs:18:        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeAssetContentSanitizerTests.cs:38:        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureAgentRuntimeAssetContentSanitizerTests.cs:53:        var bounded = ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureProcessParentNodePolicyTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureProcessStartEstimateCalculatorTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:2:using CanDoItAll.Modules.AgentFramework.Pages.Components;
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:6:public sealed class WorkflowExecutorCanvasCatalogTests
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:14:            WorkflowExecutorSourceDescriptor.BuiltIn("1.0.0"));
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:18:            WorkflowExecutorSourceDescriptor.BundledPlugin(
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:26:            WorkflowExecutorSourceDescriptor.BundledPlugin(
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:32:        var actions = WorkflowExecutorCanvasCatalog.BuildQuickCreateActions(
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:37:        var builtInActionId = WorkflowExecutorCanvasCatalog.BuildCreateActionId(builtIn.Id);
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:38:        var officeDownloadActionId = WorkflowExecutorCanvasCatalog.BuildCreateActionId(officeDownload.Id);
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:47:        Assert.Contains(officeGroup.Children, item => item.ActionId == WorkflowExecutorCanvasCatalog.BuildCreateActionId(officeMark.Id));
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:56:            WorkflowExecutorSourceDescriptor.BuiltIn("1.0.0"),
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:57:            WorkflowExecutorSideEffectDescriptor.ExternalWrite(
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:58:                WorkflowExecutorExternalMutationKind.None,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:63:            new WorkflowExecutorExecutionPolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:69:        var action = WorkflowExecutorCanvasCatalog.BuildCreateAction(executor);
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:76:    private static WorkflowExecutorDescriptor CreateExecutor(
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:79:        WorkflowExecutorSourceDescriptor source,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:80:        WorkflowExecutorSideEffectDescriptor? sideEffects = null,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:81:        WorkflowExecutorExecutionPolicy? defaultPolicy = null)
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:83:            new WorkflowExecutorId(id),
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:86:            WorkflowExecutorCategoryKind.Utility,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:89:            WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:90:            WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:93:            defaultPolicy ?? WorkflowExecutorExecutionPolicy.Default,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorCanvasCatalogTests.cs:97:            SideEffects = sideEffects ?? WorkflowExecutorSideEffectDescriptor.None
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:5:using CanDoItAll.AgentFramework.Workflows.Builder;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:6:using CanDoItAll.AgentFramework.Workflows.Templates;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:7:using CanDoItAll.Modules.AgentFramework;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:12:public sealed class ProjectStructureWorkflowPreviewSimulationSupportTests
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:19:        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:20:            new WorkflowNodeId("store-project"),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:21:            WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:23:        var payloadJson = WorkflowEventPayloads.Serialize(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:24:            WorkflowEventPayloadSource.Runtime,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:25:            "WorkflowExecutorFailed",
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:26:            nodeId: new WorkflowNodeId("store-project"),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:27:            executorId: WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:28:            inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:31:            new WorkflowEventRecord(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:33:                WorkflowRunId.New(),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:34:                WorkflowEventKind.ExecutorFailed,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:35:                new WorkflowNodeId("store-project"),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:41:        var message = ProjectStructureWorkflowNodeService.ResolveWorkflowStatusMessage(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:42:            WorkflowRunState.Failed,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:47:        Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, message, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:56:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:57:            CreateProjectStructureNode("create-summary", WorkflowProjectStructureOperation.CreateAsset),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:58:            CreateProjectStructureNode("create-tasks", WorkflowProjectStructureOperation.CreateTaskNodes),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:59:            CreateProjectStructureNode("read-tree", WorkflowProjectStructureOperation.ReadTree),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:60:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:63:        var options = ProjectStructureWorkflowPreviewSimulationSupport.Analyze(definition);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:66:        Assert.Contains(options, option => option.NodeId == "create-summary" && option.Operation == WorkflowProjectStructureOperation.CreateAsset);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:67:        Assert.Contains(options, option => option.NodeId == "create-tasks" && option.Operation == WorkflowProjectStructureOperation.CreateTaskNodes);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:73:        var pack = new WorkflowTemplatePackLoader().Load();
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:74:        var template = Assert.Single(pack.Workflows, item => item.Key == "office365-category-email-summary-to-project");
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:76:        var definition = new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:77:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:78:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:81:            WorkflowLifecycleStatus.Active,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:82:            pack.CreateGraph(template, WorkflowComponentId.New()),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:87:        var options = ProjectStructureWorkflowPreviewSimulationSupport.Analyze(definition);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:92:            option.Operation == WorkflowProjectStructureOperation.CreateAsset);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:98:        var pack = new WorkflowTemplatePackLoader().Load();
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:114:            WorkflowProjectStructureOperation.CreateAsset,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:120:            WorkflowProjectStructureOperation.CreateTaskNodes,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:126:            WorkflowProjectStructureOperation.CreateAsset,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:137:        Assert.Contains(gmailTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("gmail.messages-by-label"));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:138:        Assert.Contains(gmailTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("gmail.mark-message-processed"));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:139:        AssertProjectStructureOperation(gmailTasks, "create-gmail-task-nodes", WorkflowProjectStructureOperation.CreateTaskNodes, includeInputPayload: true);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:140:        AssertProjectStructureOperation(gmailTasks, "store-gmail-no-task-summary", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:142:        Assert.Contains(officeTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("office365.messages-by-category"));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:143:        Assert.Contains(officeTasks.Nodes, node => node.Settings.ExecutorId == new WorkflowExecutorId("office365.mark-message-processed"));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:144:        AssertProjectStructureOperation(officeTasks, "create-office365-task-nodes", WorkflowProjectStructureOperation.CreateTaskNodes, includeInputPayload: true);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:145:        AssertProjectStructureOperation(officeTasks, "store-office365-no-task-summary", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:148:        AssertProjectStructureOperation(mermaid, "store-mermaid-asset", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:151:        AssertProjectStructureOperation(sourceCode, "store-code-summary", WorkflowProjectStructureOperation.CreateAsset, includeInputPayload: true);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:158:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:159:            CreateProjectStructureNode("read-tree", WorkflowProjectStructureOperation.ReadTree),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:160:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:164:            ProjectStructureWorkflowPreviewSimulationSupport.BuildPlan(definition, ["read-tree"]));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:173:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:174:            CreateProjectStructureNode("create-summary", WorkflowProjectStructureOperation.CreateAsset),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:175:            CreateProjectStructureNode("create-tasks", WorkflowProjectStructureOperation.CreateTaskNodes),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:176:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:179:        var plan = ProjectStructureWorkflowPreviewSimulationSupport.BuildPlan(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:184:        Assert.All(plan.Steps, step => Assert.Equal(WorkflowExecutorIds.ProjectStructure, step.SourceExecutorId));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:194:    private static WorkflowDefinition CreateDefinition(IReadOnlyList<WorkflowNode> nodes)
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:196:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:197:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:200:            WorkflowLifecycleStatus.Active,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:201:            new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:202:                new WorkflowNodeId("start"),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:205:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:206:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:214:    private static WorkflowGraph AssertTemplateGraph(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:215:        WorkflowTemplatePack pack,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:218:        var template = Assert.Single(pack.Workflows, item => item.Key == key);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:219:        var graph = pack.CreateGraph(template, WorkflowComponentId.New());
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:227:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:229:        WorkflowProjectStructureOperation operation,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:234:        Assert.Equal(WorkflowExecutorIds.ProjectStructure, node.Settings.ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:236:        var settings = JsonSerializer.Deserialize<WorkflowProjectStructureExecutorSettings>(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:252:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:268:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:282:    private static WorkflowNode AssertSingleExecutorNode(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:283:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:288:        Assert.Equal(new WorkflowExecutorId(executorId), node.Settings.ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:293:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:302:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:311:                    edge.Routing.Kind == WorkflowRouteKind.SwitchCase &&
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:317:                    edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:321:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:326:        Assert.Equal(WorkflowExecutorIds.SourceIngestion, node.Settings.ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:328:        var settings = JsonSerializer.Deserialize<WorkflowSourceIngestionExecutorSettings>(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:339:    private static WorkflowNode CreateProjectStructureNode(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:341:        WorkflowProjectStructureOperation operation)
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:342:        => CreateNode(id, WorkflowNodeKind.Executor) with
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:344:            Settings = new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:350:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:351:                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:353:                ExecutorId = WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:355:                    new WorkflowProjectStructureExecutorSettings { Operation = operation },
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:360:    private static WorkflowNode CreateNode(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:362:        WorkflowNodeKind kind)
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:364:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:368:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:374:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowPreviewSimulationSupportTests.cs:375:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:6:public sealed class ProjectStructureWorkflowNodeKeysTests
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:9:    public void Workflow_node_keys_round_trip_typed_ids()
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:11:        var workflowId = WorkflowId.New();
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:12:        var runId = WorkflowRunId.New();
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:14:        var definitionKey = ProjectStructureWorkflowNodeKeys.BuildWorkflowDefinitionNodeKey(workflowId);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:15:        var runKey = ProjectStructureWorkflowNodeKeys.BuildWorkflowRunNodeKey(runId);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:17:        Assert.True(ProjectStructureWorkflowNodeKeys.TryParseWorkflowDefinitionNodeKey(definitionKey, out var parsedWorkflowId));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:18:        Assert.True(ProjectStructureWorkflowNodeKeys.TryParseWorkflowRunNodeKey(runKey, out var parsedRunId));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:19:        Assert.Equal(workflowId, parsedWorkflowId);
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:21:        Assert.False(ProjectStructureWorkflowNodeKeys.TryParseWorkflowDefinitionNodeKey("workflow-definition:not-a-guid", out _));
tests\Unit\CanDoItAll.Tests.Unit\ProjectStructureWorkflowNodeKeysTests.cs:22:        Assert.False(ProjectStructureWorkflowNodeKeys.TryParseWorkflowRunNodeKey("process-run:11111111-1111-1111-1111-111111111111", out _));
tests\Unit\CanDoItAll.Tests.Unit\ProjectWorkbenchServiceArchitectureTests.cs:3:using CanDoItAll.AgentFramework.Tooling;
tests\Unit\CanDoItAll.Tests.Unit\ProjectWorkbenchServiceArchitectureTests.cs:56:                descriptor.ImplementationType == typeof(ProjectStructureAgentRuntimeToolProvider) &&
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:1:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:2:using CanDoItAll.Modules.AgentFramework.Pages.Components;
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:6:public sealed class WorkflowExecutorDisplayAdapterTests
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:13:            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead()
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:16:        var badge = WorkflowExecutorDisplayAdapter.BuildPreviewCommitBadge(descriptor);
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:20:        Assert.Contains("preview and commit", WorkflowExecutorDisplayAdapter.BuildPreviewCommitDescription(descriptor), StringComparison.OrdinalIgnoreCase);
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:28:            SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker("$.idempotencyKey", "receipt.v1")
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:31:        var badges = WorkflowExecutorDisplayAdapter.BuildSummaryBadges(descriptor);
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:37:    private static WorkflowExecutorDescriptor CreateDescriptor()
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:39:        return new WorkflowExecutorDescriptor(
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:40:            Id: new WorkflowExecutorId("test.executor"),
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:43:            Category: WorkflowExecutorCategoryKind.Utility,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:46:            InputShape: WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:47:            ResultShape: WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowExecutorDisplayAdapterTests.cs:50:            DefaultPolicy: WorkflowExecutorExecutionPolicy.Default with
tests\Unit\CanDoItAll.Tests.Unit\ProviderPricingTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProviderPricingTests.cs:203:            sourcePhase: ProviderUsageSourcePhases.FinalizerRecovery);
tests\Unit\CanDoItAll.Tests.Unit\ProviderUsageNormalizationTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProviderUsageNormalizationTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProviderUsageNormalizationTests.cs:71:            SourcePhase: ProviderUsageSourcePhases.FinalizerShortCircuit,
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:242:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:248:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:250:            "AgentFrameworkProviderMetadata.cs");
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:261:        Assert.Contains("AgentFrameworkProviderKind.ComfyUi", metadataSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:306:        Assert.Contains("TryResolveAgentFrameworkProviderKind", workspaceModelsSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:307:        Assert.Contains("TryResolveAgentFrameworkProviderKind", settingsPageSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:308:        Assert.Contains("TryResolveAgentFrameworkProviderKind", providerPanelSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:309:        Assert.DoesNotContain("_ => AgentFrameworkProviderKind.Ollama", workspaceModelsSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:310:        Assert.DoesNotContain("_ => AgentFrameworkProviderKind.Ollama", settingsPageSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:311:        Assert.DoesNotContain("_ => AgentFrameworkProviderKind.Ollama", providerPanelSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:312:        Assert.Contains("No AgentFramework provider kind mapping exists for connector plugin", workspaceModelsSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:324:        Assert.Contains("ComfyUiWorkflowTemplateJson", providerExecutionSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:326:        Assert.Contains("ComfyUiWorkflowTemplatePath", providerExecutionSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:338:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:344:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:346:            "AgentFrameworkProviderMetadata.cs");
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:350:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:353:            "WorkflowCanvasEditor.razor.cs");
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:357:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:360:            "WorkflowCanvasEditor.razor");
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:364:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:370:        Assert.Contains("No AgentFramework provider kind mapping exists for connector plugin", registrySource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:371:        Assert.Contains("No AgentFramework provider transport mapping exists for connector plugin", registrySource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:372:        Assert.DoesNotContain("_ => AgentFrameworkProviderKind.Ollama", registrySource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:373:        Assert.Contains("AgentFrameworkProviderKind.ComfyUi => ComfyUiProviderAdapter.PluginKey", metadataSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:379:        Assert.Contains("selected.ExecutorId != WorkflowExecutorIds.ImageGeneration", workflowMarkup, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\ProviderFeatureMatrixTests.cs:405:            "CanDoItAll.AgentFramework.Models",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:5:using CanDoItAll.AgentFramework.Workflows.Templates;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:8:using CanDoItAll.Modules.AgentFramework;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:9:using CanDoItAll.Modules.AgentFramework.Pages;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:10:using CanDoItAll.Modules.AgentFramework.Pages.Components;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:20:using WorkflowFailureDiagnosticEnvelope = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureDiagnosticEnvelope;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:21:using WorkflowFailureKind = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureKind;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:22:using WorkflowFailureRetryability = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureRetryability;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:23:using WorkflowFailureSourceContext = CanDoItAll.AgentFramework.Workflows.Abstractions.WorkflowFailureSourceContext;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:27:public sealed class WorkflowsPageTests
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:30:    public async Task Workflows_page_creates_starter_workflow_and_runs_preview()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:32:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:35:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:36:        var componentLibrary = harness.Context.Services.GetRequiredService<IWorkflowComponentLibraryService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:37:        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:40:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:52:            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow created");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:67:        Assert.Contains("Workflows", workflowsTab.TextContent);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:80:            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow test completed");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:88:    public async Task Workflows_page_defers_component_library_until_component_sections_need_it()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:90:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterCountingWorkflowComponentLibrary);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:92:        var counter = harness.Context.Services.GetRequiredService<WorkflowComponentLibraryCallCounter>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:95:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:134:    public async Task Workflows_page_loads_full_selected_definition_before_rendering_editor_canvas()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:138:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:143:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:177:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:178:        var workflowId = WorkflowId.New();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:180:        var oldVersion = new WorkflowVersionId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:181:        var latestVersion = new WorkflowVersionId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:197:            dbContext.Set<WorkflowDefinitionRecord>().AddRange(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:198:                WorkflowDefinitionRecord.FromDefinition(oldDefinition),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:199:                WorkflowDefinitionRecord.FromDefinition(latestDefinition));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:213:    public async Task Workflows_page_defers_runtime_history_until_history_needs_it()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:215:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterCountingWorkflowRunStore);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:217:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:218:        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:219:        var counter = harness.Context.Services.GetRequiredService<WorkflowRunStoreCallCounter>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:221:        var runId = WorkflowRunId.New();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:223:        await runStore.SaveRunAsync(new WorkflowRunSnapshot(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:227:            WorkflowRunState.Completed,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:228:            WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:233:        await runStore.SaveEventAsync(new WorkflowEventRecord(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:236:            WorkflowEventKind.Completed,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:237:            new WorkflowNodeId("lazy-node"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:241:        await runStore.SaveArtifactAsync(new WorkflowArtifactRecord(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:242:            WorkflowArtifactId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:244:            WorkflowArtifactKind.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:245:            new WorkflowNodeId("lazy-node"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:251:        await runStore.SaveExternalRequestAsync(new WorkflowExternalRequestRecord(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:252:            WorkflowExternalRequestId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:254:            WorkflowExternalRequestKind.Approval,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:255:            new WorkflowNodeId("lazy-node"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:263:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:290:    public async Task Workflows_template_catalogue_dialog_loads_examples_from_workflows_tab()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:293:        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:297:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:301:        AssertNoWorkflowPageError(cut);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:321:    public async Task Workflows_template_catalogue_loads_pack_only_when_dialog_opens()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:328:            services.RemoveAll<WorkflowTemplatePackLoader>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:329:            services.AddScoped(_ => new WorkflowTemplatePackLoader(invalidPackRoot));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:334:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:354:    public async Task Workflows_template_preview_dialog_renders_canvas_without_saving()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:357:        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:359:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:363:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:382:    public async Task Workflows_template_add_to_drafts_uses_next_prefix_when_name_exists()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:385:        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:387:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:388:        var componentLibrary = harness.Context.Services.GetRequiredService<IWorkflowComponentLibraryService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:390:        var templatePack = new WorkflowTemplatePackLoader().Load();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:391:        var template = templatePack.Workflows.Single(item =>
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:397:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:410:        Assert.Equal(WorkflowLifecycleStatus.Draft, created.Status);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:414:    public async Task Workflow_canvas_toolbox_exposes_executor_catalog_metadata()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:417:        await using var harness = await CreateInMemoryWorkflowHarnessAsync(environment);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:421:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:426:        var toolboxSearch = EnsureWorkflowToolboxVisible(cut);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:462:    public async Task Workflow_canvas_places_llm_component_validates_runs_and_saves_definition()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:464:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:467:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:468:        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:471:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:490:        ClickWorkflowCanvasTab(cut, "workflow-canvas-tab-routes");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:497:        ClickWorkflowCanvasTab(cut, "workflow-canvas-tab-node");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:502:            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:507:        ClickWorkflowCanvasTab(cut, "workflow-canvas-tab-preview");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:510:            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow preview completed");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:515:            Assert.Contains("Workflow LLM test output", dialog.TextContent);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:523:            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow saved");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:536:        Assert.Contains(detail!.Definition.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:541:    public async Task Workflow_canvas_preview_prompts_for_project_context_and_can_skip_project_writes()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:544:        var runner = new CapturingWorkflowTestRunner();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:547:            services.RemoveAll<IWorkflowTestRunner>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:548:            services.AddSingleton<IWorkflowTestRunner>(runner);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:554:        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:572:        ClickWorkflowCanvasTab(cut, "workflow-canvas-tab-preview");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:587:        Assert.Equal(WorkflowNodeKind.Executor, storeNode.Kind);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:588:        Assert.Equal(WorkflowExecutorIds.ProjectStructure, storeNode.Settings.ExecutorId);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:591:        Assert.Equal(WorkflowExecutorIds.ProjectStructure, simulatedStep.SourceExecutorId);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:596:    public async Task Workflow_canvas_marks_planned_runtime_backends_unavailable()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:601:        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:609:            option => string.Equals(option.GetAttribute("value"), nameof(WorkflowRuntimeBackendKind.DurableTask), StringComparison.Ordinal));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:617:    public async Task Workflow_canvas_stats_count_workflow_node_usages_not_available_inventory()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:620:        var usedComponent = CreateWorkflowComponent("Used summary call");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:621:        var definition = CreateWorkflowUsageStatsDefinition(usedComponent.Id);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:623:        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:628:                CreateWorkflowComponent("Unused research call"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:629:                CreateWorkflowComponent("Unused validation call")
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:635:            Assert.Contains("Workflow usage stats target", cut.Markup, StringComparison.Ordinal);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:650:    public async Task Workflow_canvas_preview_selects_running_node_from_progress()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:652:        var runner = new NodeProgressWorkflowTestRunner(new WorkflowNodeId("work"));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:655:            services.RemoveAll<IWorkflowTestRunner>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:656:            services.AddSingleton<IWorkflowTestRunner>(runner);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:660:        var cut = harness.Context.RenderComponent<WorkflowCanvasEditor>(parameters => parameters
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:667:        ClickWorkflowCanvasTab(cut, "workflow-canvas-tab-preview");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:679:    public async Task Workflow_canvas_reconnects_linear_route_after_delete_and_accepts_canvas_connections()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:681:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:686:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:705:                node => node.Kind == WorkflowNodeKind.LlmCall.ToString());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:712:            Assert.Equal(2, surface.Nodes.Count(node => node.Kind == WorkflowNodeKind.LlmCall.ToString()));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:764:            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:770:    public async Task Workflow_canvas_authors_typed_predicate_route_metadata()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:772:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:777:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:791:        ClickWorkflowCanvasTab(cut, "workflow-canvas-tab-routes");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:795:        cut.Find("[data-testid='workflow-canvas-edge-route-kind']").Change(WorkflowRouteKind.Predicate.ToString());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:799:        cut.Find("[data-testid='workflow-canvas-edge-route-operator']").Change(WorkflowRouteOperator.GreaterThanOrEqual.ToString());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:800:        cut.Find("[data-testid='workflow-canvas-edge-route-value-kind']").Change(WorkflowRouteValueKind.Number.ToString());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:813:            Assert.Contains(notificationService.Messages, message => message.Summary == "Workflow canvas valid");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:819:    public async Task Workflow_canvas_decision_context_action_adds_and_edits_routes_in_node_dialog()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:821:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:825:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:889:    public async Task Workflow_example_seed_creates_production_examples_when_enabled()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:892:        var store = new InMemoryWorkflowCatalogStore();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:893:        var catalogService = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:894:        var templatePack = new WorkflowTemplatePackLoader().Load();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:895:        var seeder = new WorkflowExampleCatalogSeedService(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:902:            Options.Create(new WorkflowExampleCatalogSeedOptions
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:907:            NullLogger<WorkflowExampleCatalogSeedService>.Instance);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:925:        Assert.Equal(templatePack.Workflows.Count, examples.Length);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:928:        Assert.Equal(templatePack.Workflows.Count, components.Count(component => component.Name.StartsWith("Example LLM:", StringComparison.Ordinal)));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:940:        Assert.Contains(invoiceDetail.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:946:        Assert.Contains(fanOutDetail.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.FanOutSelector);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:953:            node.Settings.ExecutorId == WorkflowExecutorIds.HttpFetch &&
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:960:        Assert.Contains(folderReportDetail.Definition.Graph.Nodes, node => node.Settings.ExecutorId == WorkflowExecutorIds.MarkdownRender);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:966:        Assert.Contains(taskTransformDetail.Definition.Graph.Nodes, node => node.Settings.ExecutorId == WorkflowExecutorIds.JsonTransform);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:970:    public async Task Workflow_example_seed_preserves_non_managed_definitions_with_template_names()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:973:        var store = new InMemoryWorkflowCatalogStore();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:974:        var catalogService = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:975:        var templatePack = new WorkflowTemplatePackLoader().Load();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:976:        var template = templatePack.Workflows[0];
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:982:            WorkflowModality.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:983:            new WorkflowModelSettings(0.2, 256, RequireJsonOutput: false, ResponseFormatJsonSchema: string.Empty),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:985:            WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:986:            WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:989:        var userDefinition = await catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:994:            WorkflowLifecycleStatus.Active,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:996:            new WorkflowRuntimePolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:997:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1002:        var seeder = new WorkflowExampleCatalogSeedService(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1009:            Options.Create(new WorkflowExampleCatalogSeedOptions
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1014:            NullLogger<WorkflowExampleCatalogSeedService>.Instance);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1034:        Assert.Equal(templatePack.Workflows.Count, definitions.Count(item => item.Name.StartsWith("Example:", StringComparison.Ordinal)));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1038:    public async Task Workflow_history_paginates_runs_and_events_and_moves_full_payload_to_detail_dialog()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1040:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1042:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1043:        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1045:        var newestRunId = new WorkflowRunId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1052:                : new WorkflowRunId(Guid.Parse($"00000000-0000-0000-0000-{index:x12}"));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1053:            await runStore.SaveRunAsync(new WorkflowRunSnapshot(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1057:                WorkflowRunState.Completed,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1058:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1071:            await runStore.SaveEventAsync(new WorkflowEventRecord(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1074:                index % 2 == 0 ? WorkflowEventKind.ExecutorCompleted : WorkflowEventKind.SuperStep,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1075:                new WorkflowNodeId("history-node"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1082:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1124:    public async Task Workflow_history_displays_typed_failure_diagnostic_without_raw_message()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1126:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1128:        var catalogService = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1129:        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1131:        var runId = WorkflowRunId.New();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1133:        var diagnostic = new WorkflowFailureDiagnosticEnvelope(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1134:            WorkflowFailureKind.Executor,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1135:            WorkflowFailureRetryability.RetryableAfterRepair,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1143:            new WorkflowNodeId("store-project"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1144:            WorkflowExecutorIds.ProjectStructure,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1145:            WorkflowFailureSourceContext.ForExecutor(WorkflowExecutorIds.ProjectStructure),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1147:        var payloadJson = WorkflowEventPayloads.Serialize(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1148:            WorkflowEventPayloadSource.Runtime,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1149:            "WorkflowExecutorFailed",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1150:            nodeId: new WorkflowNodeId("store-project"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1151:            executorId: WorkflowExecutorIds.ProjectStructure,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1152:            inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1154:        await runStore.SaveRunAsync(new WorkflowRunSnapshot(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1158:            WorkflowRunState.Failed,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1159:            WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1161:            "Workflow executor failed with token=raw-token-value.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1164:        await runStore.SaveEventAsync(new WorkflowEventRecord(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1167:            WorkflowEventKind.ExecutorFailed,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1168:            new WorkflowNodeId("store-project"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1169:            "Workflow executor failed with token=raw-token-value.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1174:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1196:    public async Task Workflow_canvas_preserves_maximized_state_when_selection_changes()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1198:        await using var harness = await ComponentTestHarness.CreateAsync(RegisterDeterministicWorkflowLlmInvoker);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1202:        var cut = harness.Context.RenderComponent<WorkflowsPage>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1240:        var openWorkflows = typeof(AgentsHomePage).GetMethod(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1241:            "OpenWorkflows",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1243:        Assert.NotNull(openWorkflows);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1244:        await cut.InvokeAsync(() => openWorkflows.Invoke(cut.Instance, null));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1249:    private static void RegisterDeterministicWorkflowLlmInvoker(IServiceCollection services)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1251:        services.RemoveAll<IWorkflowLlmComponentInvoker>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1252:        services.AddScoped<IWorkflowLlmComponentInvoker, DeterministicWorkflowLlmComponentInvoker>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1255:    private static void RegisterCountingWorkflowComponentLibrary(IServiceCollection services)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1257:        services.AddSingleton<WorkflowComponentLibraryCallCounter>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1258:        services.RemoveAll<IWorkflowComponentLibraryService>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1259:        services.AddScoped<IWorkflowComponentLibraryService>(serviceProvider => new CountingWorkflowComponentLibraryService(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1260:            serviceProvider.GetRequiredService<PersistentWorkflowCatalogService>(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1261:            serviceProvider.GetRequiredService<WorkflowComponentLibraryCallCounter>()));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1264:    private static void RegisterCountingWorkflowRunStore(IServiceCollection services)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1266:        services.AddSingleton<WorkflowRunStoreCallCounter>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1267:        services.RemoveAll<IWorkflowRunStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1268:        services.RemoveAll<IWorkflowArtifactStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1269:        services.RemoveAll<IWorkflowExternalRequestStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1270:        services.RemoveAll<IWorkflowCheckpointStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1271:        services.AddSingleton<CountingWorkflowRunStore>();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1272:        services.AddSingleton<IWorkflowRunStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1273:        services.AddSingleton<IWorkflowArtifactStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1274:        services.AddSingleton<IWorkflowExternalRequestStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1275:        services.AddSingleton<IWorkflowCheckpointStore>(serviceProvider => serviceProvider.GetRequiredService<CountingWorkflowRunStore>());
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1278:    private static Task<ComponentTestHarness> CreateInMemoryWorkflowHarnessAsync(CanDoItAllTestEnvironment environment)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1309:        IWorkflowCatalogService catalogService,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1310:        IWorkflowComponentLibraryService componentLibrary,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1311:        WorkflowTemplatePack templatePack,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1312:        WorkflowTemplateDefinition template,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1320:            WorkflowModality.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1326:        await catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1331:            Status: WorkflowLifecycleStatus.Draft,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1343:    private static void ClickWorkflowCanvasTab(IRenderedFragment cut, string testId)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1354:                     throw new InvalidOperationException($"Workflow canvas tab '{testId}' did not render a clickable tab element.");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1365:    private static IElement EnsureWorkflowToolboxVisible(IRenderedFragment cut)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1378:    private static void AssertNoWorkflowPageError(IRenderedFragment cut)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1384:    private static WorkflowDefinition CreateCanvasLoadDefinition(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1385:        WorkflowId workflowId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1386:        WorkflowVersionId versionId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1391:        var start = new WorkflowNodeId("start");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1392:        var work = new WorkflowNodeId("work");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1393:        var end = new WorkflowNodeId("end");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1395:            ? new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1398:                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1399:                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1400:                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1403:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1404:                        new WorkflowEdgeId("start-to-work"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1409:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1411:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1412:                        new WorkflowEdgeId("work-to-end"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1417:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1420:            : new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1423:                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1424:                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1427:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1428:                        new WorkflowEdgeId("start-to-end"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1433:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1437:        return new WorkflowDefinition(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1441:            "Workflow definition used to verify selected editor loading.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1442:            WorkflowLifecycleStatus.Active,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1444:            new WorkflowRuntimePolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1445:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1454:    private static Task<WorkflowDefinition> CreateCanvasLoadDefinitionAsync(IWorkflowCatalogService catalogService)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1456:        var start = new WorkflowNodeId("start");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1457:        var work = new WorkflowNodeId("work");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1458:        var end = new WorkflowNodeId("end");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1459:        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1463:            Description: "Workflow definition used to verify selected editor loading.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1464:            WorkflowLifecycleStatus.Active,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1465:            new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1468:                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1469:                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1470:                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1473:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1474:                        new WorkflowEdgeId("start-to-work"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1479:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1481:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1482:                        new WorkflowEdgeId("work-to-end"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1487:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1490:            new WorkflowRuntimePolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1491:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1498:    private static Task<WorkflowDefinition> CreateHistoryDefinitionAsync(IWorkflowCatalogService catalogService)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1500:        var start = new WorkflowNodeId("start");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1501:        var end = new WorkflowNodeId("end");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1502:        return catalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1506:            Description: "Workflow definition used to verify bounded history paging.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1507:            WorkflowLifecycleStatus.Active,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1508:            new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1511:                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1512:                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1515:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1516:                        new WorkflowEdgeId("start-to-end"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1521:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1524:            new WorkflowRuntimePolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1525:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1532:    private static WorkflowDefinition CreateProjectStructurePreviewDefinition()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1534:        var start = new WorkflowNodeId("start");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1535:        var store = new WorkflowNodeId("store");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1536:        var end = new WorkflowNodeId("end");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1538:        return new WorkflowDefinition(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1539:            WorkflowId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1540:            WorkflowVersionId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1542:            "Workflow used to verify preview input prompting.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1543:            WorkflowLifecycleStatus.Draft,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1544:            new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1547:                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1548:                    new WorkflowNode(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1550:                        WorkflowNodeKind.Executor,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1553:                        new WorkflowNodeSettings(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1559:                            InputShape: WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1560:                            ResultShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1562:                            ExecutorId = WorkflowExecutorIds.ProjectStructure,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1565:                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1568:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1569:                        new WorkflowEdgeId("start-to-store"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1574:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1576:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1577:                        new WorkflowEdgeId("store-to-end"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1582:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1585:            new WorkflowRuntimePolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1586:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1595:    private static WorkflowDefinition CreatePreviewProgressDefinition()
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1597:        var start = new WorkflowNodeId("start");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1598:        var work = new WorkflowNodeId("work");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1599:        var end = new WorkflowNodeId("end");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1601:        return new WorkflowDefinition(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1602:            WorkflowId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1603:            WorkflowVersionId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1605:            "Workflow used to verify canvas selection follows preview execution.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1606:            WorkflowLifecycleStatus.Draft,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1607:            new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1610:                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1611:                    CreateHistoryNode(work, WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1612:                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1615:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1616:                        new WorkflowEdgeId("start-to-work"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1621:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1623:                    new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1624:                        new WorkflowEdgeId("work-to-end"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1629:                        WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1632:            new WorkflowRuntimePolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1633:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1642:    private static WorkflowDefinition CreateWorkflowUsageStatsDefinition(WorkflowComponentId componentId)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1644:        var start = new WorkflowNodeId("start");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1645:        var firstLlm = new WorkflowNodeId("llm-a");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1646:        var firstExecutor = new WorkflowNodeId("executor-a");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1647:        var secondLlm = new WorkflowNodeId("llm-b");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1648:        var secondExecutor = new WorkflowNodeId("executor-b");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1649:        var end = new WorkflowNodeId("end");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1652:        return new WorkflowDefinition(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1653:            WorkflowId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1654:            WorkflowVersionId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1655:            "Workflow usage stats target",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1656:            "Workflow used to verify canvas stat counts.",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1657:            WorkflowLifecycleStatus.Draft,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1658:            new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1661:                    CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1666:                    CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1669:                    CreateWorkflowEdge("start-to-llm-a", start, firstLlm),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1670:                    CreateWorkflowEdge("llm-a-to-executor-a", firstLlm, firstExecutor),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1671:                    CreateWorkflowEdge("executor-a-to-llm-b", firstExecutor, secondLlm),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1672:                    CreateWorkflowEdge("llm-b-to-executor-b", secondLlm, secondExecutor),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1673:                    CreateWorkflowEdge("executor-b-to-end", secondExecutor, end)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1675:            new WorkflowRuntimePolicy(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1676:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1685:    private static WorkflowNode CreateLlmUsageNode(WorkflowNodeId id, WorkflowComponentId componentId)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1688:            WorkflowNodeKind.LlmCall,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1691:            new WorkflowNodeSettings(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1697:                InputShape: WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1698:                ResultShape: WorkflowValueShape.Text));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1700:    private static WorkflowNode CreateExecutorUsageNode(WorkflowNodeId id)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1703:            WorkflowNodeKind.Executor,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1706:            new WorkflowNodeSettings(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1712:                InputShape: WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1713:                ResultShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1715:                ExecutorId = WorkflowExecutorIds.ProjectStructure,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1719:    private static WorkflowEdge CreateWorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1721:        WorkflowNodeId sourceNodeId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1722:        WorkflowNodeId targetNodeId)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1724:            new WorkflowEdgeId(id),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1729:            WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1732:    private static LlmCallComponent CreateWorkflowComponent(string name)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1737:            WorkflowComponentId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1741:            WorkflowModality.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1742:            new WorkflowModelSettings(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1748:            WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1749:            WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1755:    private static WorkflowNode CreateHistoryNode(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1756:        WorkflowNodeId id,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1757:        WorkflowNodeKind kind,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1758:        WorkflowValueShape? inputShape = null,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1759:        WorkflowValueShape? resultShape = null)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1765:            new WorkflowNodeSettings(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1771:                InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1772:                ResultShape: resultShape ?? WorkflowValueShape.Text));
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1774:    private static WorkflowGraph CreateStarterGraph(WorkflowComponentId componentId)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1776:        var start = new WorkflowNodeId("start");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1777:        var llm = new WorkflowNodeId("llm");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1778:        var end = new WorkflowNodeId("end");
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1779:        return new WorkflowGraph(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1782:                CreateHistoryNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1783:                new WorkflowNode(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1785:                    WorkflowNodeKind.LlmCall,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1788:                    new WorkflowNodeSettings(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1794:                        InputShape: WorkflowValueShape.Text,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1795:                        ResultShape: WorkflowValueShape.Text)),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1796:                CreateHistoryNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1799:                new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1800:                    new WorkflowEdgeId("start-to-llm"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1805:                    WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1807:                new WorkflowEdge(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1808:                    new WorkflowEdgeId("llm-to-end"),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1813:                    WorkflowEdgeKind.Direct,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1818:    private sealed class DeterministicWorkflowLlmComponentInvoker : IWorkflowLlmComponentInvoker
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1820:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1821:            WorkflowDefinition definition,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1822:            WorkflowNode node,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1824:            WorkflowNodeInput input,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1827:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1829:                $"Workflow LLM test output: {input.PayloadJson}",
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1834:    private sealed class WorkflowComponentLibraryCallCounter
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1854:    private sealed class CountingWorkflowComponentLibraryService(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1855:        IWorkflowComponentLibraryService inner,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1856:        WorkflowComponentLibraryCallCounter counter) : IWorkflowComponentLibraryService
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1858:        public async Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(CancellationToken cancellationToken = default)
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1871:            WorkflowComponentId componentId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1881:            WorkflowComponentId componentId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1886:    private sealed class WorkflowRunStoreCallCounter
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1938:    private sealed class CountingWorkflowRunStore(WorkflowRunStoreCallCounter counter) :
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1939:        IWorkflowRunStore,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1940:        IWorkflowArtifactStore,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1941:        IWorkflowExternalRequestStore
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1943:        private readonly InMemoryWorkflowRunStore inner = new();
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1946:            WorkflowRunSnapshot run,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1950:        public Task<WorkflowRunSnapshot?> GetRunAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1951:            WorkflowRunId runId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1958:        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1959:            WorkflowId? workflowId = null,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1963:        public Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1964:            WorkflowRunPageRequest request,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1972:            WorkflowEventRecord workflowEvent,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1976:        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1977:            WorkflowRunId runId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1984:        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1985:            WorkflowEventPageRequest request,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1992:        public Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1993:            WorkflowCheckpointRecord checkpoint,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1997:        public Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:1998:            WorkflowCheckpointId checkpointId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2002:        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2003:            WorkflowRunId runId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2007:        public Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2008:            WorkflowCheckpointId checkpointId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2014:            WorkflowExternalRequestRecord request,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2018:        public Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2019:            WorkflowExternalRequestId requestId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2023:        public Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2024:            WorkflowRunId runId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2032:            WorkflowArtifactRecord artifact,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2036:        public Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2037:            WorkflowRunId runId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2044:        async Task<WorkflowArtifactRecord> IWorkflowArtifactStore.SaveArtifactAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2045:            WorkflowArtifactRecord artifact,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2052:        Task<IReadOnlyList<WorkflowExternalRequestRecord>> IWorkflowExternalRequestStore.ListPendingRequestsAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2053:            WorkflowRunId runId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2057:        Task<WorkflowExternalRequestRecord> IWorkflowExternalRequestStore.SaveRequestAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2058:            WorkflowExternalRequestRecord request,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2060:            => ((IWorkflowExternalRequestStore)inner).SaveRequestAsync(request, cancellationToken);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2062:        Task<WorkflowExternalRequestRecord> IWorkflowExternalRequestStore.MarkRespondedAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2063:            WorkflowExternalRequestId requestId,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2067:            => ((IWorkflowExternalRequestStore)inner).MarkRespondedAsync(requestId, responseJson, respondedAtUtc, cancellationToken);
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2070:    private sealed class CapturingWorkflowTestRunner : IWorkflowTestRunner
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2072:        public WorkflowTestRunRequest? LastRequest { get; private set; }
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2074:        public Task<WorkflowTestRunResult> RunAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2075:            WorkflowTestRunRequest request,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2081:            var run = new WorkflowRunSnapshot(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2082:                WorkflowRunId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2085:                WorkflowRunState.Completed,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2086:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2091:            return Task.FromResult(new WorkflowTestRunResult(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2093:                WorkflowValidationResult.Success,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2102:    private sealed class NodeProgressWorkflowTestRunner(WorkflowNodeId runningNodeId) : IWorkflowTestRunner
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2106:        public async Task<WorkflowTestRunResult> RunAsync(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2107:            WorkflowTestRunRequest request,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2111:            var observer = WorkflowNodeExecutionProgressScope.Current;
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2116:                    new WorkflowNodeExecutionProgress(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2121:                        WorkflowNodeExecutionProgressState.Started,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2127:            var run = new WorkflowRunSnapshot(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2128:                WorkflowRunId.New(),
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2131:                WorkflowRunState.Completed,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2132:                WorkflowRuntimeBackendKind.InProcess,
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2137:            return new WorkflowTestRunResult(
tests\Components\CanDoItAll.Tests.Components\WorkflowsPageTests.cs:2139:                WorkflowValidationResult.Success,
tests\Unit\CanDoItAll.Tests.Unit\SecretReferenceSurfaceTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\SecretReferenceSurfaceTests.cs:39:    public void WorkflowHttpSecretHeader_serializes_reference_metadata_only()
tests\Unit\CanDoItAll.Tests.Unit\SecretReferenceSurfaceTests.cs:42:        var settings = new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\SecretReferenceSurfaceTests.cs:45:            SecretHeader = new WorkflowHttpSecretHeaderBinding
tests\Unit\CanDoItAll.Tests.Unit\SecretReferenceSurfaceTests.cs:50:                ValueFormat = WorkflowHttpSecretValueFormat.Bearer
tests\Unit\CanDoItAll.Tests.Unit\SecretReferenceSurfaceTests.cs:55:        var roundTrip = JsonSerializer.Deserialize<WorkflowHttpExecutorSettings>(json, JsonOptions);
tests\Unit\CanDoItAll.Tests.Unit\SecretReferenceSurfaceTests.cs:60:        Assert.Equal(WorkflowHttpSecretValueFormat.Bearer, roundTrip.SecretHeader.ValueFormat);
tests\Unit\CanDoItAll.Tests.Unit\SecretRuntimeAuthorizationTests.cs:54:                SecretRuntimePurposes.PluginWorkflowExecutorSecret,
tests\Unit\CanDoItAll.Tests.Unit\SkillLoaderContractsTests.cs:1:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\SkillLoaderContractsTests.cs:2:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\SkillLoaderContractsTests.cs:3:using CanDoItAll.AgentFramework.Skills;
tests\Unit\CanDoItAll.Tests.Unit\SkillLoaderContractsTests.cs:4:using CanDoItAll.AgentFramework.Skills.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:85:    public void WorkflowDefinitionValidator_applies_executor_configuration_schema()
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:88:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:89:        var validator = new WorkflowDefinitionValidator(catalog);
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:92:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:105:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:114:            issue.Code == WorkflowValidationIssueCode.InvalidExecutorSettings &&
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:117:            issue.Code == WorkflowValidationIssueCode.InvalidExecutorSettings &&
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:122:    private static WorkflowDefinition CreateDefinition(
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:123:        IReadOnlyList<WorkflowNode> nodes,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:124:        IReadOnlyList<WorkflowEdge> edges)
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:126:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:127:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:130:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:131:            new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:132:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:133:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:141:    private static WorkflowNode CreateExecutorNode(string id, WorkflowExecutorId executorId)
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:143:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:144:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:149:    private static WorkflowNodeSettings CreateSettings(WorkflowExecutorId executorId)
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:150:        => new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:156:            InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:157:            ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")) with
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:161:            ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:164:    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:166:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:170:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:176:                InputShape: kind == WorkflowNodeKind.End
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:177:                    ? new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Any result")
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:178:                    : WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:179:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:181:    private static WorkflowEdge CreateEdge(string id, string source, string target)
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:183:            new WorkflowEdgeId(id),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:184:            new WorkflowNodeId(source),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:186:            new WorkflowNodeId(target),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:188:            WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:191:            Routing = WorkflowEdgeRouting.Always
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:194:    private sealed class SchemaBackedExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:196:        public WorkflowExecutorDescriptor Descriptor { get; } = new(
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:197:            new WorkflowExecutorId("test.schema"),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:200:            WorkflowExecutorCategoryKind.Data,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:203:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:204:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:207:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:217:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:218:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:219:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\SettingsSchemaTests.cs:221:            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\ToolImplementationContractsTests.cs:3:using CanDoItAll.AgentFramework.Capabilities.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\ToolImplementationContractsTests.cs:4:using CanDoItAll.AgentFramework.Capabilities.Access;
tests\Unit\CanDoItAll.Tests.Unit\ToolImplementationContractsTests.cs:5:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\ToolImplementationContractsTests.cs:6:using CanDoItAll.AgentFramework.Tools;
tests\Unit\CanDoItAll.Tests.Unit\ToolImplementationContractsTests.cs:7:using CanDoItAll.AgentFramework.Tools.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\ToolImplementationContractsTests.cs:8:using CapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:5:using CanDoItAll.AgentFramework.Workflows.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:6:using CanDoItAll.AgentFramework.Workflows.Builder;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:10:public sealed class WorkflowAbstractionsBuilderTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:15:    public void WorkflowDefinitionBuilderCreatesDeterministicLinearLlmWorkflow()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:17:        var componentId = WorkflowComponentId.New();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:18:        var definition = WorkflowFixtureFactory.CreateLinearLlmWorkflow(componentId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:21:        Assert.Equal(new WorkflowNodeId("start"), definition.Graph.StartNodeId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:24:            node => Assert.Equal(WorkflowNodeKind.Start, node.Kind),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:27:                Assert.Equal(WorkflowNodeKind.LlmCall, node.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:30:            node => Assert.Equal(WorkflowNodeKind.End, node.Kind));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:32:            [new WorkflowEdgeId("start-to-llm"), new WorkflowEdgeId("llm-to-end")],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:37:    public void WorkflowDefinitionBuilderRejectsMissingStartWhenBuildingValidFixture()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:39:        var builder = WorkflowDefinitionBuilder
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:41:            .AddNode(WorkflowNodeBuilder.End("end"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:49:    public void WorkflowDefinitionBuilderCanCreateExplicitInvalidFixtureForValidatorTests()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:51:        var definition = WorkflowFixtureFactory.CreateInvalidMissingStartWorkflow();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:53:        Assert.Equal(new WorkflowNodeId("__missing-start__"), definition.Graph.StartNodeId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:55:        Assert.Equal(WorkflowNodeKind.End, definition.Graph.Nodes[0].Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:59:    public void WorkflowNodeBuilderRejectsExecutorNodeWithoutExplicitExecutorContract()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:61:        var missingExecutorId = WorkflowNodeBuilder.For("execute", WorkflowNodeKind.Executor);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:62:        var emptySettings = WorkflowNodeBuilder.For("execute", WorkflowNodeKind.Executor);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:65:        var emptySettingsException = Assert.Throws<ArgumentException>(() => emptySettings.WithExecutor(WorkflowExecutorIds.ProjectStructure, " "));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:72:    public void WorkflowBuilderPreservesSerializedWorkflowFields()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:74:        var definition = WorkflowDefinitionBuilder
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:77:            .AddInputParameter(WorkflowInputParameterBuilder
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:80:                .WithKind(WorkflowInputParameterKind.ProjectId)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:83:            .AddNode(WorkflowNodeBuilder.Start("start"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:84:            .AddNode(WorkflowNodeBuilder.Executor("read-project", WorkflowExecutorIds.ProjectStructure, """{"operation":"ReadTree"}"""))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:85:            .AddNode(WorkflowNodeBuilder.End("end"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:86:            .AddEdge(WorkflowEdgeBuilder.Direct("start-to-read", "start", "read-project"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:87:            .AddEdge(WorkflowEdgeBuilder.Direct("read-to-end", "read-project", "end"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:91:        var roundTripped = JsonSerializer.Deserialize<WorkflowDefinition>(json, SerializerOptions);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:95:        Assert.Equal(WorkflowExecutorIds.ProjectStructure, roundTripped.Graph.Nodes[1].Settings.ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:102:    public void WorkflowFixtureFactoryCreatesBranchingExecutorWorkflowWithPorts()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:104:        var definition = WorkflowFixtureFactory.CreateBranchingExecutorWorkflow(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:105:            WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:106:            WorkflowExecutorIds.ProjectStructure);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:108:        var triage = definition.Graph.Nodes.Single(node => node.Id == new WorkflowNodeId("triage"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:109:        var predicate = definition.Graph.Edges.Single(edge => edge.Id == new WorkflowEdgeId("triage-to-matched"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:110:        var defaultRoute = definition.Graph.Edges.Single(edge => edge.Id == new WorkflowEdgeId("triage-to-fallback"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:112:        Assert.Equal(WorkflowNodeKind.StrictLogic, triage.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:117:                Assert.Equal(new WorkflowPortId("input"), port.Id);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:118:                Assert.Equal(WorkflowPortDirection.Input, port.Direction);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:123:                Assert.Equal(new WorkflowPortId("matched"), port.Id);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:124:                Assert.Equal(WorkflowPortDirection.Output, port.Direction);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:129:                Assert.Equal(new WorkflowPortId("default"), port.Id);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:130:                Assert.Equal(WorkflowPortDirection.Output, port.Direction);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:133:        Assert.Equal(WorkflowRouteKind.Predicate, predicate.Routing.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:134:        Assert.Equal(WorkflowRouteOperator.Equals, predicate.Routing.Operator);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:135:        Assert.Equal(WorkflowRouteKind.SwitchDefault, defaultRoute.Routing.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:139:    public void WorkflowFailureDiagnosticEnvelopeSerializesRepairableContext()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:141:        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:142:            new WorkflowNodeId("read-project"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:143:            WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:147:        var roundTripped = JsonSerializer.Deserialize<WorkflowFailureDiagnosticEnvelope>(json, SerializerOptions);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:150:        Assert.Equal(WorkflowFailureKind.Executor, roundTripped.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:151:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, roundTripped.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:152:        Assert.Equal(new WorkflowNodeId("read-project"), roundTripped.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:153:        Assert.Equal(WorkflowExecutorIds.ProjectStructure, roundTripped.ExecutorId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:154:        Assert.Equal(WorkflowFailureSourceKind.Executor, roundTripped.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:160:    public void WorkflowAbstractionAndBuilderProjectsDoNotReferenceForbiddenImplementationProjects()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:165:            Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Abstractions", "CanDoItAll.AgentFramework.Workflows.Abstractions.csproj"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:166:            Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Builder", "CanDoItAll.AgentFramework.Workflows.Builder.csproj")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:170:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:171:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAbstractionsBuilderTests.cs:174:            "CanDoItAll.AgentFramework.Persistence"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:3:public sealed class WorkflowAdoptionHardeningCheckpointTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:7:        @"src\App\CanDoItAll.Web\Api\WorkflowsApi.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:8:        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:9:        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:10:        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:11:        @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:13:        @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:14:        @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:15:        @"src\Modules\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.WorkflowNodes.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:20:    public void ApiUiWorkbenchAdoptionDoesNotReferenceMafInternalsOrOldExecutorAliases()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:25:            "MafWorkflowCompiler",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:26:            "MafInProcessWorkflowExecutionBackend",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:27:            "MafWorkflowEventNormalizer",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:28:            "MafWorkflowLlmComponentInvoker",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:29:            "AddBuiltInWorkflowExecutors",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:30:            "Microsoft.Agents.AI.Workflows"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:40:    public void WorkflowUiAndWorkbenchAdoptionUseTypedFailureDisplayBoundary()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:45:            @"src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.Core\WorkflowFailureDisplayFormatter.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:48:            @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:51:            @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:54:            @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:56:        Assert.Contains("ToUserMessage(WorkflowEventRecord workflowEvent)", formatterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:58:        Assert.Contains("WorkflowEventPayloadEnvelope", formatterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:59:        Assert.Contains("ResolveEventDisplayMessage(WorkflowEventRecord workflowEvent)", workflowsPageCode, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:60:        Assert.Contains("WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent)", workflowsPageCode, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:61:        Assert.Contains("WorkflowFailureDisplayFormatter.TryResolveDiagnosticTechnicalDetail", workflowsPageCode, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:62:        Assert.Contains("WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent)", workflowNodeService, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:63:        Assert.Contains(".Select(WorkflowFailureDisplayFormatter.ToUserMessage)", workflowNodeService, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:68:    public void WorkflowUiAndWorkbenchDoNotUseRawEventMessageDisplayOrMessageOnlyStatus()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:73:            @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:76:            @"src\Modules\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:79:            @"src\Modules\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:93:        Assert.DoesNotContain("WorkflowFailureDiagnosticEnvelope", adoptionSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:94:        Assert.DoesNotContain("JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>", adoptionSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:98:    public void AdoptionSourceHasNoStubMarkersOrGenericWorkflowErrors()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowAdoptionHardeningCheckpointTests.cs:109:            "Catalog\\WorkflowTemplatePackLoader"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowArchitectureBoundaryTests.cs:5:public sealed class WorkflowArchitectureBoundaryTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowArchitectureBoundaryTests.cs:8:    public void AgentFrameworkCoreDoesNotReferenceMafWorkflowPackage()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowArchitectureBoundaryTests.cs:16:            "CanDoItAll.AgentFramework.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowArchitectureBoundaryTests.cs:17:            "CanDoItAll.AgentFramework.Core.csproj");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowArchitectureBoundaryTests.cs:26:            "Microsoft.Agents.AI.Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:2:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:7:public sealed class WorkflowCatalogTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:38:        var graph = new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:60:                    Kind = WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:61:                    Routing = WorkflowEdgeRouting.Predicate(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:63:                        WorkflowRouteOperator.GreaterThanOrEqual,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:65:                        WorkflowRouteValueKind.Number,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:71:        var saved = await catalog.SaveDefinitionAsync(CreateSaveRequest(new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:77:        var routedEdge = Assert.Single(detail!.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.Predicate);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:80:        Assert.Equal(WorkflowRouteOperator.GreaterThanOrEqual, routedEdge.Routing.Operator);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:81:        Assert.Equal(WorkflowRouteValueKind.Number, routedEdge.Routing.ExpectedValueKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:86:    public async Task CatalogPreservesWorkflowInputParametersOnSaveAndStatusChange()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:92:            InputParameters = CreateWorkflowInputParameters()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:95:        var active = await catalog.ChangeDefinitionStatusAsync(new WorkflowDefinitionStatusChangeRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:98:            WorkflowLifecycleStatus.Active));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:102:        Assert.Equal(WorkflowInputParameterKind.EmailAddress, email.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:103:        Assert.Equal(WorkflowInputParameterOptionSourceKind.CrmContacts, email.OptionSource.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:113:        var invalidGraph = new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:131:            InputShape = WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:132:            ResultShape = new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:134:        var orphan = CreateNode("orphan", WorkflowNodeKind.StrictLogic, resultShape: WorkflowValueShape.Text);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:136:            new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:137:                new WorkflowNodeId("start"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:139:                    CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:140:                    CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:141:                    CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:151:        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.DisconnectedNode);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:152:        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.ShapeMismatch);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:161:            CreateComponentRequest() with { Modality = WorkflowModality.Audio }));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:234:                ModelSettings = new WorkflowModelSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:245:    public async Task TestRunnerExecutesSavedWorkflowWithInProcessBackend()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:250:        var runStore = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:253:        var result = await runner.RunAsync(new WorkflowTestRunRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:258:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:263:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:271:        var runner = CreateRunner(catalog, new InMemoryWorkflowRunStore());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:272:        var definition = CreateDefinition(CreateDefinitionGraph(WorkflowComponentId.New()));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:274:        var result = await runner.RunAsync(new WorkflowTestRunRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:275:            WorkflowId: null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:279:            RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:284:        Assert.Contains(result.Validation.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidComponentReference);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:294:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:295:                WorkflowRuntimeBackendKind.DurableTask,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:301:        var runner = CreateRunner(catalog, new InMemoryWorkflowRunStore());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:303:        var result = await runner.RunAsync(new WorkflowTestRunRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:304:            WorkflowId: null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:308:            WorkflowRuntimeBackendKind.DurableTask,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:313:        var issue = Assert.Single(result.Validation.Issues, issue => issue.Code == WorkflowValidationIssueCode.UnsupportedRuntimeBackend);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:326:                runtimePolicy: new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:327:                    WorkflowRuntimeBackendKind.DurableTask,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:334:        Assert.Contains(nameof(WorkflowRuntimeBackendKind.DurableTask), exception.Message, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:340:        var runStore = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:341:        var runtimeManager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:343:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:344:                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:348:        var definition = CreateDefinition(new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:349:            new WorkflowNodeId("start"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:351:                CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:352:                CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:356:                RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:357:                    WorkflowRuntimeBackendKind.DurableTask,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:367:                new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:371:                    WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:378:    private static InMemoryWorkflowCatalogService CreateCatalog(IReadOnlyList<ProviderProfile>? providers = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:381:        return new InMemoryWorkflowCatalogService(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:382:            new InMemoryWorkflowCatalogStore(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:383:            new WorkflowDefinitionValidator(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:388:    private static WorkflowTestRunner CreateRunner(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:389:        InMemoryWorkflowCatalogService catalog,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:390:        InMemoryWorkflowRunStore runStore)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:392:        var runtimeManager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:394:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:395:                    new MafWorkflowCompiler(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:396:                        new WorkflowDefinitionValidator(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:402:        return new WorkflowTestRunner(catalog, runtimeManager, runStore);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:405:    private static WorkflowDefinitionSaveRequest CreateSaveRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:406:        WorkflowGraph graph,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:408:        WorkflowId? id = null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:409:        WorkflowVersionId? expectedVersionId = null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:410:        WorkflowRuntimePolicy? runtimePolicy = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:412:        return new WorkflowDefinitionSaveRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:416:            "Workflow for catalog tests.",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:417:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:419:            runtimePolicy ?? new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:420:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:427:    private static IReadOnlyList<WorkflowInputParameterDescriptor> CreateWorkflowInputParameters()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:431:            new WorkflowInputParameterDescriptor(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:434:                WorkflowInputParameterKind.EmailAddress,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:439:                new WorkflowInputParameterOptionSource(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:440:                    WorkflowInputParameterOptionSourceKind.CrmContacts,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:449:    private static WorkflowDefinition CreateDefinition(WorkflowGraph graph)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:451:        return new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:452:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:453:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:455:            "Workflow for catalog tests.",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:456:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:458:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:459:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:468:    private static WorkflowGraph CreateDefinitionGraph(WorkflowComponentId componentId)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:470:        return new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:471:            new WorkflowNodeId("start"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:473:                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:474:                CreateNode("llm", WorkflowNodeKind.LlmCall, componentId),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:475:                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:483:    private static WorkflowNode CreateNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:485:        WorkflowNodeKind kind,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:486:        WorkflowComponentId? componentId = null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:487:        WorkflowValueShape? inputShape = null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:488:        WorkflowValueShape? resultShape = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:490:        return new WorkflowNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:491:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:495:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:501:                InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:502:                ResultShape: resultShape ?? WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:505:    private static WorkflowEdge CreateEdge(string id, string source, string target)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:507:        return new WorkflowEdge(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:508:            new WorkflowEdgeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:509:            new WorkflowNodeId(source),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:511:            new WorkflowNodeId(target),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:513:            WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:524:            Modality: WorkflowModality.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:525:            ModelSettings: new WorkflowModelSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:531:            InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:532:            ResultShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:611:    private sealed class PassthroughLlmComponentInvoker : IWorkflowLlmComponentInvoker
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:613:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:614:            WorkflowDefinition definition,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:615:            WorkflowNode node,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:617:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs:621:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:5:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:6:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:7:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:8:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:9:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:10:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:11:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:12:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:17:public sealed class WorkflowExecutorCategoryIsolationTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:21:        "ControlWorkflowExecutors.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:22:        "HttpFetchWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:23:        "ImageGenerationWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:24:        "JsonTransformWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:25:        "MarkdownRenderWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:26:        "PlannedWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:27:        "ProjectStructureWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:28:        "SourceIngestionWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:29:        "SpreadsheetWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:30:        "WorkspaceFileWorkflowExecutor.cs"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:34:    public void DefaultExecutorImplementationsMovedOutOfMafWorkflowFolder()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:37:        var mafWorkflowFolder = Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime", "Workflows");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:41:                File.Exists(Path.Combine(mafWorkflowFolder, fileName)),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:45:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control", "ControlWorkflowExecutors.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:46:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms", "JsonTransformWorkflowExecutor.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:47:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace", "WorkspaceFileWorkflowExecutor.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:48:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network", "HttpFetchWorkflowExecutor.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:49:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents", "SpreadsheetWorkflowExecutor.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:50:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media", "ImageGenerationWorkflowExecutor.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:51:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure", "ProjectStructureWorkflowExecutor.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:57:        IWorkflowExecutorDescriptorSource[] sources =
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:59:            new StandardControlWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:60:            new StandardTransformWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:61:            new StandardWorkspaceWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:62:            new StandardNetworkWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:63:            new StandardDocumentWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:64:            new StandardMediaWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:65:            new StandardProjectStructureWorkflowExecutorDescriptorSource()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:79:            BuiltInWorkflowExecutorDescriptors.All.Select(descriptor => descriptor.Id.Value).Order(StringComparer.Ordinal),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:87:        var mafRegistration = File.ReadAllText(Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.MafAdapter", "MafWorkflowAdapterServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:88:        var moduleRegistration = File.ReadAllText(Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework", "Services", "AgentFrameworkModuleServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:90:        Assert.Contains("AddStandardWorkflowExecutors(executorLifetime)", mafRegistration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:91:        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Scoped)", moduleRegistration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:94:            Assert.DoesNotContain($"ServiceDescriptor.Singleton<IWorkflowExecutor, {executorTypeName}>", mafRegistration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:95:            Assert.DoesNotContain($"ServiceDescriptor.Scoped<IWorkflowExecutor, {executorTypeName}>", moduleRegistration, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:99:        services.AddStandardWorkflowExecutors();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:101:        var executorDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor)).ToArray();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:102:        var descriptorSources = services.Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorDescriptorSource)).ToArray();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:104:        Assert.Equal(10 + BuiltInWorkflowExecutorDescriptors.Planned.Count, executorDescriptors.Length);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:106:        Assert.Contains(descriptorSources, descriptor => descriptor.ImplementationType == typeof(StandardControlWorkflowExecutorDescriptorSource));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:107:        Assert.Contains(descriptorSources, descriptor => descriptor.ImplementationType == typeof(StandardTransformWorkflowExecutorDescriptorSource));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:108:        Assert.Contains(descriptorSources, descriptor => descriptor.ImplementationType == typeof(StandardWorkspaceWorkflowExecutorDescriptorSource));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:109:        Assert.Contains(descriptorSources, descriptor => descriptor.ImplementationType == typeof(StandardNetworkWorkflowExecutorDescriptorSource));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:110:        Assert.Contains(descriptorSources, descriptor => descriptor.ImplementationType == typeof(StandardDocumentWorkflowExecutorDescriptorSource));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:111:        Assert.Contains(descriptorSources, descriptor => descriptor.ImplementationType == typeof(StandardMediaWorkflowExecutorDescriptorSource));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:112:        Assert.Contains(descriptorSources, descriptor => descriptor.ImplementationType == typeof(StandardProjectStructureWorkflowExecutorDescriptorSource));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:119:            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control.csproj",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:120:            ["CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Runtime"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:123:            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms.csproj",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:124:            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Core"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:127:            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace.csproj",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:128:            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:131:            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network.csproj",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:132:            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Core", "CanDoItAll.Modules.Security"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:135:            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents.csproj",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:136:            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.Tools.Documents"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:139:            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media.csproj",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:140:            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:143:            "src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure.csproj",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:144:            ["CanDoItAll.AgentFramework.Core", "CanDoItAll.AgentFramework.Models", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "CanDoItAll.AgentFramework.Workflows.Core", "CanDoItAll.SharedKernel"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:153:            Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:154:            "SourceIngestionWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:156:                "SourceIngestionWorkflowReader.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:157:                "SourceIngestionWorkflowPaths.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:158:                "SourceIngestionWorkflowCandidates.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:159:                "SourceIngestionWorkflowModels.cs"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:162:            Path.Combine(root, "src", "MAF", "WorkflowExecutors", "Standard", "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:163:            "ProjectStructureWorkflowExecutor.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:165:                "ProjectStructureWorkflowTaskNodes.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:166:                "ProjectStructureWorkflowInputResolution.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:167:                "ProjectStructureWorkflowSupport.cs"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:190:        Assert.DoesNotContain(projectReferences, reference => reference == "CanDoItAll.AgentFramework.Maf");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorCategoryIsolationTests.cs:192:        Assert.DoesNotContain(projectReferences, reference => reference == "CanDoItAll.Modules.AgentFramework");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:4:using CanDoItAll.AgentFramework.Workflows.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:5:using CanDoItAll.AgentFramework.Workflows.Builder;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:7:using CoreRuntimeBackendCatalog = CanDoItAll.AgentFramework.Core.IWorkflowRuntimeBackendCatalog;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:8:using CoreWorkflowDefinitionValidator = CanDoItAll.AgentFramework.Core.IWorkflowDefinitionValidator;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:12:public sealed class WorkflowCoreExtractionTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:15:    public void WorkflowCoreProjectDoesNotReferenceForbiddenImplementationProjects()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:22:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:23:            "CanDoItAll.AgentFramework.Workflows.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:24:            "CanDoItAll.AgentFramework.Workflows.Core.csproj");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:27:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:28:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:31:            "CanDoItAll.AgentFramework.Persistence",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:53:    public void WorkflowCoreImplementationFilesMovedOutOfAgentFrameworkCoreProject()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:58:            "WorkflowDefinitionValidator.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:59:            "WorkflowCatalogServices.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:60:            "WorkflowRoutingCompiler.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:61:            "WorkflowPreviewSimulationRenderer.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:62:            "WorkflowPayloadPolicyService.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:63:            "WorkflowFailureDisplayFormatter.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:64:            "WorkflowProcessExecutorBridge.cs"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:70:                File.Exists(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", movedFile)),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:71:                $"{movedFile} must not remain in AgentFramework.Core.");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:73:                File.Exists(Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Core", movedFile)),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:74:                $"{movedFile} must exist in Workflows.Core.");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:79:    public void WorkflowCoreRegistrationExtensionOwnsCoreServiceRegistrations()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:82:        services.AddSingleton<IWorkflowExecutorCatalog>(WorkflowExecutorCatalog.FromDescriptors([]));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:83:        services.AddWorkflowCoreServices();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:85:        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(CoreWorkflowDefinitionValidator));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:87:        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowPayloadPolicyService));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:88:        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowProcessExecutorBridge));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:89:        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowTestRunner));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:92:        Assert.IsType<WorkflowDefinitionValidator>(provider.GetRequiredService<CoreWorkflowDefinitionValidator>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:93:        Assert.IsType<WorkflowRuntimeBackendCatalog>(provider.GetRequiredService<CoreRuntimeBackendCatalog>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:94:        Assert.IsType<WorkflowPayloadPolicyService>(provider.GetRequiredService<IWorkflowPayloadPolicyService>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:98:    public void HostAndModuleRegistrationUseWorkflowCoreExtension()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:103:            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Hosting", "AgentFrameworkServiceCollectionExtensions.cs"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:104:            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework", "Services", "AgentFrameworkModuleServiceCollectionExtensions.cs")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:111:            Assert.Contains("AddMafWorkflowAdapterServices", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:112:            Assert.DoesNotContain("TryAddScoped<IWorkflowDefinitionValidator>", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:113:            Assert.DoesNotContain("TryAddScoped<IWorkflowPayloadPolicyService, WorkflowPayloadPolicyService>", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:114:            Assert.DoesNotContain("TryAddScoped<IWorkflowProcessExecutorBridge, WorkflowProcessExecutorBridge>", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:115:            Assert.DoesNotContain("TryAddScoped<IWorkflowTestRunner, WorkflowTestRunner>", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:122:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:123:            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:124:            "MafWorkflowAdapterServiceCollectionExtensions.cs");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:127:        Assert.Contains("AddWorkflowCoreServices()", adapterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:128:        Assert.DoesNotContain("TryAddScoped<IWorkflowDefinitionValidator>", adapterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:129:        Assert.DoesNotContain("TryAddScoped<IWorkflowPayloadPolicyService, WorkflowPayloadPolicyService>", adapterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:130:        Assert.DoesNotContain("TryAddScoped<IWorkflowProcessExecutorBridge, WorkflowProcessExecutorBridge>", adapterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:131:        Assert.DoesNotContain("TryAddScoped<IWorkflowTestRunner, WorkflowTestRunner>", adapterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:137:        var catalog = new InMemoryWorkflowCatalogService(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:138:            new InMemoryWorkflowCatalogStore(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:139:            new WorkflowDefinitionValidator());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:140:        var invalidDefinition = WorkflowFixtureFactory.CreateInvalidMissingStartWorkflow();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:141:        var request = new WorkflowDefinitionSaveRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:151:        var diagnostics = WorkflowFailureDiagnosticMapper.GetDiagnostics(exception);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:154:            item => item.Kind == WorkflowFailureKind.Validation &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:155:                    item.NodeId == new WorkflowNodeId("__missing-start__"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:157:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:158:        Assert.Equal(WorkflowFailureSourceKind.Workflow, diagnostic.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:167:        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:168:            new WorkflowNodeId("read-project"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:169:            WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:172:        var message = WorkflowFailureDisplayFormatter.ToUserMessage(diagnostic);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:175:        Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, message, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:182:        var diagnostic = WorkflowFixtureFactory.CreateExecutorFailureDiagnostic(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:183:            new WorkflowNodeId("store-project"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:184:            WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:186:        var payloadJson = WorkflowEventPayloads.Serialize(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:187:            WorkflowEventPayloadSource.Runtime,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:188:            "WorkflowExecutorFailed",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:189:            nodeId: new WorkflowNodeId("store-project"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:190:            executorId: WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:191:            inlineJson: WorkflowRuntimeFailureDiagnosticMapper.Serialize(diagnostic));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:192:        var workflowEvent = new WorkflowEventRecord(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:194:            WorkflowRunId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:195:            WorkflowEventKind.ExecutorFailed,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:196:            new WorkflowNodeId("store-project"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:197:            "Workflow executor failed with token raw-token-value.",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:201:        var message = WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowCoreExtractionTests.cs:204:        Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, message, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:3:using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:4:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:5:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:6:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:7:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:8:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:9:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:10:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:11:using CanDoItAll.AgentFramework.Workflows.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:19:public sealed class WorkflowExecutorHardeningCheckpointTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:33:        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.Delay);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:34:        Assert.Contains(descriptors, descriptor => descriptor.Id == CognitiveMemoryWorkflowExecutorIds.Recall);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:36:        Assert.Contains(descriptors, descriptor => descriptor.Source.Kind == WorkflowExecutorSourceKind.BundledPlugin && descriptor.Source.PluginId == BundledPluginId.Value);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:37:        Assert.Contains(descriptors, descriptor => descriptor.Source.Kind == WorkflowExecutorSourceKind.LocalPackage && descriptor.Source.PackageId == RuntimePackageId.Value);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:46:            Assert.NotEqual(WorkflowExecutorTrustLevel.Untrusted, descriptor.Source.TrustLevel);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:55:            Source = WorkflowExecutorSourceDescriptor.Package(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:56:                WorkflowExecutorSourceKind.LocalPackage,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:60:                WorkflowExecutorTrustLevel.LocalPackage,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:68:        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:70:        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:71:            invoker.ExecuteAsync(CreateDefinition(node), node, new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:72:        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:74:        Assert.Equal(WorkflowFailureKind.Executor, diagnostic.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:75:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:97:        var exception = PluginWorkflowExecutorActivationException.ActivationFailed(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:106:        Assert.Equal(PluginWorkflowExecutorActivationFailureKind.ActivationFailed, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:107:        Assert.Equal(PluginWorkflowExecutorActivationRetryability.RetryableAfterRepair, exception.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:115:    public void ExecutorOwnershipAuditHasNoMafFallbackOrCategoryMonolith()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:118:        var mafWorkflowDirectory = Path.Combine(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:123:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:125:            "Workflows");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:127:        Assert.False(Directory.Exists(mafWorkflowDirectory), $"{mafWorkflowDirectory} must not own workflow executors.");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:131:            .Where(path => path.Contains("WorkflowExecutors.Standard", StringComparison.Ordinal))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:143:    public void BundledPluginWorkflowExecutorsShareSerializerOptions()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:146:        var gmailSource = File.ReadAllText(Path.Combine(root, "src", "plugins", "Implementations", "CanDoItAll.Plugin.Gmail", "GmailWorkflowExecutor.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:147:        var office365Source = File.ReadAllText(Path.Combine(root, "src", "plugins", "Implementations", "CanDoItAll.Plugin.Office365", "Office365WorkflowExecutor.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:149:        Assert.Contains("GmailWorkflowJson.Options", gmailSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:150:        Assert.Contains("Office365WorkflowJson.Options", office365Source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:155:    private static IReadOnlyList<WorkflowExecutorDescriptor> CollectCombinedDescriptors()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:157:        List<WorkflowExecutorDescriptor> descriptors = [];
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:158:        IWorkflowExecutorDescriptorSource[] standardSources =
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:160:            new StandardControlWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:161:            new StandardTransformWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:162:            new StandardWorkspaceWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:163:            new StandardNetworkWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:164:            new StandardDocumentWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:165:            new StandardMediaWorkflowExecutorDescriptorSource(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:166:            new StandardProjectStructureWorkflowExecutorDescriptorSource()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:169:        descriptors.Add(CognitiveMemoryWorkflowExecutorDescriptors.Recall);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:170:        descriptors.Add(CognitiveMemoryWorkflowExecutorDescriptors.Probe);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:171:        descriptors.Add(CognitiveMemoryWorkflowExecutorDescriptors.LearningProposal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:177:        descriptors.AddRange(new PluginWorkflowExecutorDescriptorSource(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:186:        PluginWorkflowExecutorRuntimeRegistration.RegisterWorkflowExecutors(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:192:            .GetRequiredService<IEnumerable<IWorkflowExecutorDescriptorSource>>()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:211:            PluginCapabilityKind.WorkflowExecutor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:226:    private static PluginWorkflowExecutorDescriptor CreatePluginExecutor()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:228:            new WorkflowExecutorId("plugin.hardening.bundled"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:231:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:234:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:235:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:236:            WorkflowExecutorExecutionPolicy.Default)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:238:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:239:                WorkflowExecutorCapabilityFlags.UsesNetwork,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:240:                WorkflowExecutorApprovalRequirement.NotRequired),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:241:            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead("hardening-read/v1"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:242:            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Hardening preview.")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:245:    private static WorkflowExecutorDescriptor CreateRuntimePackageDescriptor(string id)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:247:            new WorkflowExecutorId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:250:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:253:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:254:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:257:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:260:    private static WorkflowNode CreateNode(WorkflowExecutorId executorId)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:262:            new WorkflowNodeId("plugin-hardening-node"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:263:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:266:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:272:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:273:                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:277:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:280:    private static WorkflowDefinition CreateDefinition(WorkflowNode node)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:282:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:283:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:286:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:287:            new WorkflowGraph(node.Id, [node], []),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:288:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:289:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:325:    private static readonly WorkflowExecutorId RuntimePackageExecutorId = new("plugin.hardening.runtime-executor");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:332:    private sealed class AllowingGrantEvaluator : IPluginWorkflowExecutorGrantEvaluator
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:341:    private sealed class RuntimePackageExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:343:        public WorkflowExecutorDescriptor Descriptor { get; } = CreateRuntimePackageDescriptor(RuntimePackageExecutorId.Value);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:345:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:346:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:347:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:349:            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:356:        WorkflowExecutorDescriptor descriptor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:357:        Exception exception) : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:359:        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:361:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:362:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorHardeningCheckpointTests.cs:363:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:6:public sealed class WorkflowExecutorPolicyObservabilityTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:9:    public async Task WorkflowPayloadPolicy_redacts_bounds_and_references_artifact_for_oversized_json()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:11:        var settings = WorkflowSettings.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:13:            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:18:        var policy = new WorkflowPayloadPolicyService(new StaticWorkflowSettingsService(settings));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:19:        var result = await policy.ApplyAsync(new WorkflowPayloadPolicyRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:20:            RunId: WorkflowRunId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:21:            Scope: WorkflowPayloadPolicyScope.ExecutorOutput,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:23:            ArtifactKind: WorkflowArtifactKind.Json,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:28:            NodeId = new WorkflowNodeId("node-1"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:37:        Assert.Equal(WorkflowArtifactKind.Json, result.Artifact.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:38:        Assert.Equal(new WorkflowNodeId("node-1"), result.Artifact.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:43:    public void WorkflowSettings_default_policy_allows_runtime_payload_artifact_kinds()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:45:        Assert.Contains(WorkflowArtifactKind.Json, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:46:        Assert.Contains(WorkflowArtifactKind.Text, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:47:        Assert.Contains(WorkflowArtifactKind.File, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:48:        Assert.Contains(WorkflowArtifactKind.ToolReceipt, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:49:        Assert.Contains(WorkflowArtifactKind.PreviewSimulation, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:55:        var summary = WorkflowExecutorRedaction.RedactSettingsJson("""
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:78:            OutputPayloadJson = new string('x', WorkflowExecutorPayloadPolicy.MaxPluginOutputPayloadCharacters + 1)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:80:        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:82:        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:86:                new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:88:        Assert.IsType<WorkflowExecutorPayloadTooLargeException>(exception.InnerException);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:93:    public async Task WorkflowEvent_observer_receives_redacted_plugin_failure()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:100:        var observer = new RecordingWorkflowExecutorExecutionObserver();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:101:        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor], observer);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:110:        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:111:            invoker.ExecuteAsync(CreateDefinition(descriptor.Id, settingsJson), node, new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:116:        var failed = Assert.Single(observer.Records, record => record.Status == WorkflowExecutorExecutionAuditStatus.Failed);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:131:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:132:                WorkflowExecutorCapabilityFlags.WritesExternalData,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:133:                WorkflowExecutorApprovalRequirement.NotRequired),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:134:            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalWrite(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:135:                WorkflowExecutorExternalMutationKind.None,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:145:            WorkflowExecutorExecutionPolicy.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:149:        var validator = new WorkflowDefinitionValidator(new WorkflowExecutorCatalog([executor]));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:154:            issue.Code == WorkflowValidationIssueCode.InvalidExecutionPolicy &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:163:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:164:                WorkflowExecutorCapabilityFlags.WritesExternalData,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:165:                WorkflowExecutorApprovalRequirement.NotRequired),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:166:            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalWrite(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:167:                WorkflowExecutorExternalMutationKind.None,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:174:        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:178:            WorkflowExecutorExecutionPolicy.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:187:                new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:198:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:199:                WorkflowExecutorCapabilityFlags.WritesExternalData | WorkflowExecutorCapabilityFlags.IdempotentExternalMarker,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:200:                WorkflowExecutorApprovalRequirement.NotRequired),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:201:            SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:210:        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:214:            WorkflowExecutorExecutionPolicy.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:223:            new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:234:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:235:                WorkflowExecutorCapabilityFlags.WritesExternalData,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:236:                WorkflowExecutorApprovalRequirement.RequiredForExternalEffect)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:239:        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:245:                new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:256:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:257:                WorkflowExecutorCapabilityFlags.RunsHostCommand,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:258:                WorkflowExecutorApprovalRequirement.AlwaysRequired)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:261:        var invoker = new WorkflowExecutorInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:262:            new WorkflowExecutorCatalog([executor]),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:270:                new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:282:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:283:                WorkflowExecutorCapabilityFlags.WritesExternalData,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:284:                WorkflowExecutorApprovalRequirement.RequiredForExternalEffect)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:290:        var invoker = new WorkflowExecutorInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:291:            new WorkflowExecutorCatalog([executor]),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:298:            new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:305:    public async Task WorkflowExternalRequestApprovalGate_creates_redacted_pending_request_without_executing()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:309:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:310:                WorkflowExecutorCapabilityFlags.RunsHostCommand | WorkflowExecutorCapabilityFlags.UsesSecrets,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:311:                WorkflowExecutorApprovalRequirement.AlwaysRequired)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:314:        var invoker = new WorkflowExecutorInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:315:            new WorkflowExecutorCatalog([executor]),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:317:            approvalGate: new WorkflowExternalRequestApprovalGate());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:318:        using var auditScope = WorkflowExecutorExecutionAuditScope.Push(WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:320:        var exception = await Assert.ThrowsAsync<WorkflowExternalRequestPendingException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:324:                new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:326:        Assert.Equal(WorkflowExternalRequestKind.Approval, exception.Request.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:327:        Assert.Equal(new WorkflowNodeId("plugin-node"), exception.Request.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:334:    private static WorkflowExecutorDescriptor CreatePluginDescriptor(string id)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:336:            new WorkflowExecutorId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:339:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:342:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:343:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:346:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:349:            Source = WorkflowExecutorSourceDescriptor.BundledPlugin("sample.plugin", "1.0.0")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:352:    private static WorkflowNode CreateNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:353:        WorkflowExecutorId executorId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:355:        WorkflowExecutorExecutionPolicy? policy = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:357:            new WorkflowNodeId("plugin-node"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:358:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:361:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:367:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:368:                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:372:                ExecutionPolicy = policy ?? WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:375:    private static WorkflowDefinition CreateDefinition(WorkflowExecutorId executorId, string settingsJson)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:381:    private static WorkflowDefinition CreateDefinition(WorkflowNode node)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:383:        return new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:384:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:385:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:388:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:389:            new WorkflowGraph(node.Id, [node], []),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:390:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:391:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:400:    private sealed class RecordingPluginExecutor(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:402:        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:412:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:413:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:414:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:428:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:435:    private sealed class RecordingWorkflowExecutorExecutionObserver : IWorkflowExecutorExecutionObserver
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:437:        public List<WorkflowExecutorExecutionAuditRecord> Records { get; } = [];
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:440:            WorkflowExecutorExecutionAuditRecord auditRecord,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:448:    private sealed class DenyingApprovalGate(string message) : IWorkflowExecutorApprovalGate
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:450:        public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:451:            WorkflowExecutorApprovalRequest request,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:455:            return ValueTask.FromResult(new WorkflowExecutorApprovalDecision(false, message));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:459:    private sealed class ApprovingApprovalGate(string message) : IWorkflowExecutorApprovalGate
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:461:        public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:462:            WorkflowExecutorApprovalRequest request,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:466:            return ValueTask.FromResult(new WorkflowExecutorApprovalDecision(true, message));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:470:    private sealed class StaticWorkflowSettingsService(WorkflowSettings settings) : IWorkflowSettingsService
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:472:        public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:478:        public Task<WorkflowSettings> SaveSettingsAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorPolicyObservabilityTests.cs:479:            WorkflowSettings updatedSettings,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:5:public sealed class WorkflowFoundationHardeningCheckpointTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:9:        "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:10:        "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:13:        "CanDoItAll.AgentFramework.Persistence",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:25:                "CanDoItAll.AgentFramework.Workflows.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:26:                ["CanDoItAll.AgentFramework.Models"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:29:                "CanDoItAll.AgentFramework.Workflows.Builder",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:31:                    "CanDoItAll.AgentFramework.Models",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:32:                    "CanDoItAll.AgentFramework.Workflows.Abstractions"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:36:                "CanDoItAll.AgentFramework.Workflows.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:38:                    "CanDoItAll.AgentFramework.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:39:                    "CanDoItAll.AgentFramework.Models",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:40:                    "CanDoItAll.AgentFramework.WorkflowExecutors.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:41:                    "CanDoItAll.AgentFramework.Workflows.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:42:                    "CanDoItAll.AgentFramework.Workflows.Runtime",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:47:                "CanDoItAll.AgentFramework.Workflows.Runtime",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:49:                    "CanDoItAll.AgentFramework.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:50:                    "CanDoItAll.AgentFramework.Models",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:51:                    "CanDoItAll.AgentFramework.WorkflowExecutors.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:52:                    "CanDoItAll.AgentFramework.Workflows.Abstractions"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:78:            "CanDoItAll.AgentFramework.Workflows.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:79:            "CanDoItAll.AgentFramework.Workflows.Builder",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:80:            "CanDoItAll.AgentFramework.Workflows.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:81:            "CanDoItAll.AgentFramework.Workflows.Runtime"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:105:            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Core", "WorkflowCatalogServices.cs", ["InMemoryWorkflowCatalogService"]),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:106:            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Core", "WorkflowDefinitionValidator.cs", ["WorkflowDefinitionValidator"]),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:107:            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Runtime", "WorkflowRuntimeManager.cs", ["WorkflowRuntimeManager"]),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:108:            new PublicOwnerRule("CanDoItAll.AgentFramework.Workflows.Runtime", "WorkflowArtifactContentStores.cs", ["FileWorkflowArtifactContentStore"])
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:125:            Path.Combine(FindRepositoryRoot(), "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Core"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:126:            Path.Combine(FindRepositoryRoot(), "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Runtime")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:130:            "WorkflowContracts.cs"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:154:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:155:            "CanDoItAll.AgentFramework.Workflows.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:156:            "WorkflowFailureDiagnosticsMapper.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:161:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:162:            "CanDoItAll.AgentFramework.Workflows.Runtime",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:163:            "WorkflowRuntimeFailureDiagnostics.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:168:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:169:            "CanDoItAll.AgentFramework.Workflows.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:170:            "WorkflowFailureDiagnostics.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:175:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:176:            "CanDoItAll.AgentFramework.Workflows.Runtime",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:177:            "WorkflowEventPayloads.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:179:        Assert.Contains("WorkflowFailureDiagnosticEnvelope", failureContract, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:184:        Assert.Contains("WorkflowFailureDiagnosticEnvelope", coreDiagnosticMapper, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:185:        Assert.Contains("WorkflowFailureDiagnosticEnvelope", runtimeDiagnosticMapper, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:186:        Assert.Contains("WorkflowExecutorRedaction.RedactText", runtimeDiagnosticMapper, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:187:        Assert.Contains("WorkflowExecutorRedaction.RedactJson", eventPayloads, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:252:            "CanDoItAll.AgentFramework.Workflows.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:253:            "CanDoItAll.AgentFramework.Workflows.Builder",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:254:            "CanDoItAll.AgentFramework.Workflows.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:255:            "CanDoItAll.AgentFramework.Workflows.Runtime"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs:275:        => Path.Combine(root, "src", "MAF", "Workflows", projectName);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:5:using CanDoItAll.AgentFramework.Workflows.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:11:public sealed class WorkflowExecutorFoundationExtractionTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:14:    public void WorkflowExecutorFoundationProjectsHaveBoundedDependencies()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:16:        var abstractionReferences = ReadProjectReferences("src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions.csproj");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:18:            ["CanDoItAll.AgentFramework.Models"],
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:22:        var coreReferences = ReadProjectReferences("src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/CanDoItAll.AgentFramework.WorkflowExecutors.Core.csproj");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:25:                "CanDoItAll.AgentFramework.Models",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:26:                "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:27:                "CanDoItAll.AgentFramework.Workflows.Abstractions",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:37:            "CanDoItAll.AgentFramework.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:38:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:39:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:58:            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", "WorkflowExecutorContracts.cs"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:59:            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", "WorkflowExecutorObservability.cs")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:64:            Assert.False(File.Exists(oldCoreFile), $"{oldCoreFile} must not remain in AgentFramework.Core.");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:67:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions", "WorkflowExecutorContracts.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:68:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "WorkflowExecutorInvoker.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:69:        Assert.True(File.Exists(Path.Combine(root, "src", "MAF", "WorkflowExecutors", "CanDoItAll.AgentFramework.WorkflowExecutors.Core", "WorkflowExecutorJson.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:70:        Assert.False(File.Exists(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime", "Workflows", "WorkflowExecutorJson.cs")));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:76:            "WorkflowExecutors",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:77:            "CanDoItAll.AgentFramework.WorkflowExecutors.Core",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:78:            "BuiltInWorkflowExecutorDescriptors.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:79:        Assert.Contains("WorkflowExecutorDescriptorFactory.CreateImplemented", builtInDescriptorSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:90:            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Hosting", "AgentFrameworkServiceCollectionExtensions.cs"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:91:            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework", "Services", "AgentFrameworkModuleServiceCollectionExtensions.cs")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:97:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:98:            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:99:            "MafWorkflowAdapterServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:105:            Assert.Contains("AddMafWorkflowAdapterServices", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:106:            Assert.DoesNotContain("TryAddScoped<IWorkflowExecutorCatalog>", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:107:            Assert.DoesNotContain("TryAddScoped<IWorkflowExecutorExecutionObserver", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:108:            Assert.DoesNotContain("TryAddScoped<IWorkflowExecutorInvoker", source, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:111:        Assert.Contains("AddWorkflowExecutorCoreServices()", adapterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:117:        var descriptor = WorkflowExecutorDescriptorFactory.CreateImplemented(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:118:            new WorkflowExecutorId("test.executor.factory"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:121:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:132:            WorkflowExecutorSourceDescriptor.BuiltIn());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:134:        Assert.Equal(WorkflowExecutorDescriptorFactory.SettingsSchemaVersion, descriptor.SettingsSchema.Version);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:135:        Assert.Equal(WorkflowExecutorDescriptorFactory.DefaultObjectJsonSchema, descriptor.SettingsSchema.SchemaJson);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:147:        var settings = WorkflowExecutorJson.Deserialize<FactorySettings>(descriptor.DefaultSettingsJson);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:155:        var delay = BuiltInWorkflowExecutorDescriptors.Delay;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:156:        Assert.Equal(WorkflowExecutorDescriptorFactory.SettingsSchemaVersion, delay.ConfigurationSchema.Version);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:160:        var cognitiveRecall = CanDoItAll.Modules.CognitiveMemory.CognitiveMemoryWorkflowExecutorDescriptors.Recall;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:161:        Assert.Equal(WorkflowExecutorSourceKind.BuiltIn, cognitiveRecall.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:162:        Assert.Equal(WorkflowExecutorDescriptorFactory.SettingsSchemaVersion, cognitiveRecall.ConfigurationSchema.Version);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:172:        var invoker = new WorkflowExecutorInvoker(WorkflowExecutorCatalog.FromDescriptors([]), []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:175:            invoker.ExecuteAsync(definition, node, new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:176:        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:178:        Assert.Equal(WorkflowFailureKind.Executor, diagnostic.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:179:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:182:        Assert.Equal(WorkflowFailureSourceKind.Node, diagnostic.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:194:        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:196:        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:197:            invoker.ExecuteAsync(CreateDefinition(node), node, new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:198:        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:200:        Assert.Equal(WorkflowFailureKind.Executor, diagnostic.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:201:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:215:            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:216:                WorkflowExecutorCapabilityFlags.WritesExternalData | WorkflowExecutorCapabilityFlags.UsesSecrets,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:217:                WorkflowExecutorApprovalRequirement.AlwaysRequired)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:221:        var invoker = new WorkflowExecutorInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:222:            new WorkflowExecutorCatalog([executor]),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:227:            invoker.ExecuteAsync(CreateDefinition(node), node, new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:228:        var diagnostic = Assert.Single(WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:230:        Assert.Equal(WorkflowFailureKind.Approval, diagnostic.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:231:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:243:            "CanDoItAll.AgentFramework.WorkflowExecutors.Core");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:246:            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:249:            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:252:            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:255:            "CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:258:    private static WorkflowExecutorDescriptor CreateDescriptor(string id)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:260:            new WorkflowExecutorId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:263:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:266:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:267:            WorkflowExecutorDescriptorFactory.JsonShape,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:268:            WorkflowExecutorDescriptorFactory.DefaultObjectJsonSchema,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:270:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:273:    private static WorkflowNode CreateNode(WorkflowExecutorId? executorId)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:275:            new WorkflowNodeId("executor-node"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:276:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:279:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:285:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:286:                ResultShape: WorkflowExecutorDescriptorFactory.JsonShape)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:290:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:293:    private static WorkflowDefinition CreateDefinition(WorkflowNode node)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:295:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:296:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:299:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:300:            new WorkflowGraph(node.Id, [node], []),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:301:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:302:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:377:    private class RecordingExecutor(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:379:        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:383:        public virtual ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:384:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:385:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:389:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:397:        WorkflowExecutorDescriptor descriptor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:400:        public override ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:401:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:402:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:407:    private sealed class DenyingApprovalGate(string message) : IWorkflowExecutorApprovalGate
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:409:        public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:410:            WorkflowExecutorApprovalRequest request,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs:414:            return ValueTask.FromResult(new WorkflowExecutorApprovalDecision(false, message));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:4:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:6:using Microsoft.Agents.AI.Workflows;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:10:public sealed class WorkflowFoundationTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:15:    public void WorkflowIdRejectsEmptyValue()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:17:        Assert.Throws<ArgumentException>(() => new WorkflowId(Guid.Empty));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:24:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:25:            CreateNode("llm", WorkflowNodeKind.LlmCall),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:26:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:32:        var result = new WorkflowDefinitionValidator().Validate(definition, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:34:        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidComponentReference);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:38:    public void ValidatorAcceptsBasicLlmWorkflow()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:42:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:43:            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:44:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:50:        var result = new WorkflowDefinitionValidator().Validate(definition, [component]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:56:    public void MafCompilerBuildsWorkflowWithoutLeakingMafTypesThroughCoreContracts()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:60:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:61:            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:62:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:68:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:72:        Assert.NotNull(result.Workflow);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:73:        Assert.Equal("Sample workflow", result.Workflow.Name);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:77:    public void MafStatusMapperMapsPendingRequestsToWaitingForInput()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:79:        var state = MafWorkflowStatusMapper.MapRunStatus(RunStatus.PendingRequests);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:81:        Assert.Equal(WorkflowRunState.WaitingForInput, state);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:87:        var catalog = new WorkflowRuntimeBackendCatalog();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:89:        var inProcess = catalog.GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:90:        var durableTask = catalog.GetRequiredBackend(WorkflowRuntimeBackendKind.DurableTask);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:91:        var azureFunctions = catalog.GetRequiredBackend(WorkflowRuntimeBackendKind.AzureFunctions);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:93:        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Registered, inProcess.Availability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:96:        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, durableTask.Availability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:102:        Assert.Equal(WorkflowRuntimeBackendAvailabilityKind.Planned, azureFunctions.Availability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:107:    public void WorkflowEdgeDefaultsMissingRoutingMetadataForLegacyJson()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:121:        var edge = JsonSerializer.Deserialize<WorkflowEdge>(legacyEdgeJson, SerializerOptions);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:124:        Assert.Equal(WorkflowRouteKind.Always, edge.Routing.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:129:    public void WorkflowEdgeRoutingRoundTripsTypedPredicateMetadata()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:135:            WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:136:            WorkflowEdgeRouting.Predicate(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:138:                WorkflowRouteOperator.Equals,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:140:                WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:144:        var roundTripped = JsonSerializer.Deserialize<WorkflowEdge>(json, SerializerOptions);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:147:        Assert.Equal(WorkflowRouteKind.Predicate, roundTripped.Routing.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:157:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:158:            CreateNode("approved", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:164:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:165:                WorkflowEdgeRouting.Predicate(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:167:                    WorkflowRouteOperator.Equals,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:169:                    WorkflowRouteValueKind.String))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:172:        var result = new WorkflowDefinitionValidator().Validate(definition, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:175:            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:176:            issue.EdgeId == new WorkflowEdgeId("start-approved"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:183:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:184:            CreateNode("approved", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:190:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:191:                WorkflowEdgeRouting.Predicate(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:193:                    WorkflowRouteOperator.Equals,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:195:                    WorkflowRouteValueKind.String) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:197:                    RoutingLanguage = WorkflowRoutingLanguages.ArtlV1
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:201:        var result = new WorkflowDefinitionValidator().Validate(definition, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:204:            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:212:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:213:            CreateNode("manual", WorkflowNodeKind.End),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:214:            CreateNode("fallback", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:216:            CreateEdge("start-manual", "start", "manual", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchDefault("Manual")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:217:            CreateEdge("start-fallback", "start", "fallback", WorkflowEdgeKind.Conditional, WorkflowEdgeRouting.SwitchDefault("Fallback"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:220:        var result = new WorkflowDefinitionValidator().Validate(definition, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:223:            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:231:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:232:            CreateNode("email", WorkflowNodeKind.End),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:233:            CreateNode("slack", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:239:                WorkflowEdgeKind.FanOut,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:240:                WorkflowEdgeRouting.FanOutSelector(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:242:                    WorkflowRouteOperator.Contains,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:244:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:250:                WorkflowEdgeKind.FanOut,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:251:                WorkflowEdgeRouting.FanOutSelector(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:253:                    WorkflowRouteOperator.Contains,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:255:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:259:        var result = new WorkflowDefinitionValidator().Validate(definition, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:262:            issue.Code == WorkflowValidationIssueCode.InvalidRouteDefinition &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:267:    public async Task RuntimeManagerCompletesInProcessWorkflow()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:271:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:272:            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:273:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:279:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:280:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:286:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:287:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:289:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:290:                    new MafWorkflowCompiler(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:291:                        new WorkflowDefinitionValidator(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:299:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:303:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:307:        Assert.Equal(WorkflowRunState.Completed, run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:312:    public async Task InMemoryWorkflowCheckpointStore_saves_and_lists_metadata()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:314:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:316:        var runId = WorkflowRunId.New();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:317:        var checkpoint = new WorkflowCheckpointRecord(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:318:            WorkflowCheckpointId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:320:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:321:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:322:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:323:            WorkflowCheckpointKind.Completed,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:324:            WorkflowCheckpointTrustBoundary.MetadataOnly,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:325:            WorkflowResumeAvailability.NotSupported,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:340:        Assert.Equal(WorkflowCheckpointTrustBoundary.MetadataOnly, saved.TrustBoundary);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:341:        Assert.Equal(WorkflowResumeAvailability.NotSupported, saved.ResumeAvailability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:350:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:351:            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:352:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:358:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:359:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:365:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:366:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:368:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:369:                    new MafWorkflowCompiler(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:370:                        new WorkflowDefinitionValidator(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:378:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:382:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:387:        Assert.Equal(WorkflowCheckpointKind.Completed, checkpoint.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:388:        Assert.Equal(WorkflowCheckpointTrustBoundary.MetadataOnly, checkpoint.TrustBoundary);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:389:        Assert.Equal(WorkflowResumeAvailability.NotSupported, checkpoint.ResumeAvailability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:399:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:400:            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:401:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:407:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:408:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:414:        var settings = WorkflowSettings.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:416:            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:421:        var payloadPolicy = new WorkflowPayloadPolicyService(new StaticWorkflowSettingsService(settings));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:422:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:423:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:425:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:426:                    new MafWorkflowCompiler(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:427:                        new WorkflowDefinitionValidator(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:437:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:441:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:446:        var started = Assert.Single(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:447:        var startedPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(started.PayloadJson, SerializerOptions)!;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:449:            workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:450:            workflowEvent.NodeId == new WorkflowNodeId("llm"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:451:        var completedPayload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(completed.PayloadJson, SerializerOptions)!;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:453:        Assert.Equal(WorkflowRunState.Completed, run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:462:            artifact.Kind == WorkflowArtifactKind.Json &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:465:            artifact.Kind == WorkflowArtifactKind.Json &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:466:            artifact.NodeId == new WorkflowNodeId("llm"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:470:    public async Task WorkflowPayloadPolicyService_writes_retrievable_redacted_artifact_content()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:472:        var settings = WorkflowSettings.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:474:            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:479:        var contentStore = new InMemoryWorkflowArtifactContentStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:480:        var payloadPolicy = new WorkflowPayloadPolicyService(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:481:            new StaticWorkflowSettingsService(settings),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:484:        var result = await payloadPolicy.ApplyAsync(new WorkflowPayloadPolicyRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:485:            WorkflowRunId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:486:            WorkflowPayloadPolicyScope.RunInput,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:488:            WorkflowArtifactKind.Json,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:504:    public async Task InMemoryWorkflowArtifactContentStore_returns_null_for_missing_content()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:506:        var artifact = new WorkflowArtifactRecord(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:507:            WorkflowArtifactId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:508:            WorkflowRunId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:509:            WorkflowArtifactKind.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:516:        var contentStore = new InMemoryWorkflowArtifactContentStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:527:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:528:            CreateNode("human", WorkflowNodeKind.HumanInput),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:529:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:535:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:536:                WorkflowEdgeRouting.SwitchCase(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:539:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:545:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:546:                WorkflowEdgeRouting.SwitchDefault("Automatic route")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:550:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:551:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:557:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:558:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:560:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:561:                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:568:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:572:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:577:        Assert.Equal(WorkflowRunState.Completed, run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:585:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:586:            CreateNode("human", WorkflowNodeKind.HumanInput),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:587:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:593:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:594:                WorkflowEdgeRouting.SwitchCase(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:597:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:603:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:604:                WorkflowEdgeRouting.SwitchDefault("Automatic route")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:608:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:609:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:615:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:616:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:618:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:619:                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:626:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:630:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:636:        Assert.Equal(WorkflowRunState.WaitingForInput, run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:638:        Assert.Equal(new WorkflowNodeId("human"), request.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:640:        Assert.Equal(WorkflowCheckpointKind.WaitingForInput, checkpoint.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:642:        Assert.Equal(WorkflowResumeAvailability.NotSupported, checkpoint.ResumeAvailability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:644:            workflowEvent.Kind == WorkflowEventKind.ExecutorInvoked &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:645:            workflowEvent.NodeId == new WorkflowNodeId("start"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:652:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:653:            CreateNode("human", WorkflowNodeKind.HumanInput),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:654:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:660:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:661:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:667:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:668:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:670:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:671:                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:678:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:682:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:688:        Assert.Equal(WorkflowRunState.WaitingForInput, run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:690:        Assert.Equal(WorkflowRunState.Completed, completed.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:696:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:697:        var manager = new WorkflowRuntimeManager([], store);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:706:        Assert.Equal(WorkflowRunState.Completed, completed.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:709:            workflowEvent.Kind == WorkflowEventKind.Completed &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:716:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:717:        var manager = new WorkflowRuntimeManager([], store);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:725:        Assert.Equal(WorkflowRunState.Failed, failed.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:729:            workflowEvent.Kind == WorkflowEventKind.Error &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:736:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:737:        var manager = new WorkflowRuntimeManager([], store);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:748:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:749:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:753:        var manager = new WorkflowRuntimeManager([], new InMemoryWorkflowRunStore());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:757:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:761:                WorkflowRuntimeBackendKind.DurableTask,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:771:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:772:            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:773:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:779:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:780:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:786:        var manager = new WorkflowRuntimeManager(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:788:                new MafInProcessWorkflowExecutionBackend(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:789:                    new MafWorkflowCompiler(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:790:                        new WorkflowDefinitionValidator(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:794:            new InMemoryWorkflowRunStore());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:795:        var request = new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:799:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:808:        Assert.All(runs, run => Assert.Equal(WorkflowRunState.Completed, run.State));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:811:    private static WorkflowDefinition CreateDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:812:        IReadOnlyList<WorkflowNode> nodes,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:813:        IReadOnlyList<WorkflowEdge> edges)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:815:        return new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:816:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:817:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:820:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:821:            new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:822:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:823:                WorkflowRuntimeBackendKind.DurableTask,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:832:    private static async Task<WorkflowExternalRequestRecord> SaveWaitingApprovalRequestAsync(InMemoryWorkflowRunStore store)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:835:        var runId = WorkflowRunId.New();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:836:        var run = new WorkflowRunSnapshot(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:838:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:839:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:840:            WorkflowRunState.WaitingForInput,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:841:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:843:            Summary: "Workflow is waiting for approval.",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:846:        var request = new WorkflowExternalRequestRecord(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:847:            WorkflowExternalRequestId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:849:            WorkflowExternalRequestKind.Approval,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:850:            new WorkflowNodeId("approval-node"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:862:    private static WorkflowNode CreateNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:864:        WorkflowNodeKind kind,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:865:        WorkflowComponentId? componentId = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:867:        return new WorkflowNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:868:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:872:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:876:                ExternalRequestKind: kind == WorkflowNodeKind.HumanInput ? WorkflowExternalRequestKind.HumanInput : null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:878:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:879:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:882:    private static WorkflowEdge CreateEdge(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:886:        WorkflowEdgeKind kind = WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:887:        WorkflowEdgeRouting? routing = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:889:        return new WorkflowEdge(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:890:            new WorkflowEdgeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:891:            new WorkflowNodeId(source),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:893:            new WorkflowNodeId(target),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:898:            Routing = routing ?? WorkflowEdgeRouting.Always
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:905:            WorkflowComponentId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:909:            WorkflowModality.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:910:            new WorkflowModelSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:916:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:917:            WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:923:    private sealed class PassthroughLlmComponentInvoker : IWorkflowLlmComponentInvoker
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:925:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:926:            WorkflowDefinition definition,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:927:            WorkflowNode node,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:929:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:932:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:939:    private sealed class StaticWorkflowSettingsService(WorkflowSettings settings) : IWorkflowSettingsService
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:941:        public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:947:        public Task<WorkflowSettings> SaveSettingsAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowFoundationTests.cs:948:            WorkflowSettings updatedSettings,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:3:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:8:public sealed class WorkflowPreviewSimulationTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:14:        var definition = CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), node, CreateNode("end", WorkflowNodeKind.End)]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:15:        var step = new WorkflowPreviewSimulationStep(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:17:            WorkflowExecutorIds.ProjectStructure,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:31:        var output = WorkflowPreviewSimulationRenderer.Render(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:35:            new WorkflowNodeInput("""{"projectId":"project-1","runContext":{"gmailProcessing":{"messageIds":["msg-1"]}}}"""),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:40:        Assert.Equal(WorkflowExecutorIds.ProjectStructure.Value, document.RootElement.GetProperty("result").GetProperty("sourceExecutorId").GetString());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:47:    public async Task MafBackendUsesPreviewSimulationPlanInsteadOfInvokingExecutor()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:49:        var executor = new ThrowingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:50:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:51:        var compiler = new MafWorkflowCompiler(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:52:            new WorkflowDefinitionValidator(catalog),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:53:            new WorkflowExecutorInvoker(catalog, [executor]));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:54:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, Array.Empty<LlmCallComponent>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:56:        var definition = CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), toolNode, CreateNode("end", WorkflowNodeKind.End)]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:57:        var request = new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:61:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:65:            PreviewSimulationPlan = new WorkflowPreviewSimulationPlan(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:67:                new WorkflowPreviewSimulationStep(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:75:        var progressObserver = new RecordingWorkflowNodeExecutionProgressObserver();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:76:        using var progressScope = WorkflowNodeExecutionProgressScope.Push(progressObserver);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:78:        var result = await backend.StartAsync(definition, request, WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:80:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:84:            record.State == WorkflowNodeExecutionProgressState.Started);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:87:            record.State == WorkflowNodeExecutionProgressState.Completed);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:90:    private static WorkflowDefinition CreateDefinition(IReadOnlyList<WorkflowNode> nodes)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:93:        return new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:94:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:95:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:98:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:99:            new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:100:                new WorkflowNodeId("start"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:106:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:107:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:116:    private static WorkflowNode CreateExecutorNode(string id)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:118:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:119:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:122:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:128:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:129:                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:131:                ExecutorId = WorkflowExecutorIds.StorageFile,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:135:    private static WorkflowNode CreateNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:137:        WorkflowNodeKind kind)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:139:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:143:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:149:                InputShape: kind == WorkflowNodeKind.End
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:150:                    ? new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:151:                    : WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:152:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:154:    private static WorkflowEdge CreateEdge(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:159:            new WorkflowEdgeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:160:            new WorkflowNodeId(source),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:162:            new WorkflowNodeId(target),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:164:            WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:167:    private sealed class ThrowingWorkflowExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:169:        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:173:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:174:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:175:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:183:    private sealed class RecordingWorkflowNodeExecutionProgressObserver : IWorkflowNodeExecutionProgressObserver
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:185:        private readonly List<WorkflowNodeExecutionProgress> records = [];
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:187:        public IReadOnlyList<WorkflowNodeExecutionProgress> Records => records;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowPreviewSimulationTests.cs:190:            WorkflowNodeExecutionProgress progress,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:5:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:6:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:7:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:8:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:9:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:10:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:11:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:12:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:13:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:14:using CanDoItAll.AgentFramework.WorkflowExecutors.Standard;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:21:public sealed class WorkflowExecutorTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:23:    private static readonly WorkflowValueShape JsonObjectShape = new(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:24:        WorkflowValueShapeKind.Object,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:28:    private static readonly WorkflowValueShape JsonPayloadShape = new(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:29:        WorkflowValueShapeKind.Json,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:36:        var catalog = new WorkflowExecutorCatalog(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:38:            new RecordingWorkflowExecutor(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:39:            new JsonTransformWorkflowExecutor(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:40:            new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0])
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:45:        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.StorageFile);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:46:        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.JsonTransform && descriptor.CanExecute);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:47:        Assert.Contains(descriptors, descriptor => descriptor.Id == WorkflowExecutorIds.CommandProcess && !descriptor.IsImplemented);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:55:        services.AddStandardWorkflowExecutors();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:58:            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor))
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:60:        Assert.Equal(10 + BuiltInWorkflowExecutorDescriptors.Planned.Count, executorDescriptors.Length);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:61:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(WorkspaceFileWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:62:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(JsonTransformWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:63:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(MarkdownRenderWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:64:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(SourceIngestionWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:65:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(HttpFetchWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:66:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(DelayWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:67:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(HumanApprovalWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:68:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(SpreadsheetWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:69:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(ProjectStructureWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:70:        Assert.Contains(executorDescriptors, descriptor => descriptor.ImplementationType == typeof(ImageGenerationWorkflowExecutor));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:72:            BuiltInWorkflowExecutorDescriptors.Planned.Count,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:77:    public async Task ImageGenerationWorkflowExecutor_WritesProviderRuntimeOutput()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:88:        var executor = new ImageGenerationWorkflowExecutor(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:93:        var result = await ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:117:    public async Task ImageGenerationWorkflowExecutor_RejectsEditWithoutSourceContract()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:125:        var executor = new ImageGenerationWorkflowExecutor(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:130:        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:132:            Operation = WorkflowImageGenerationOperation.Edit,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:142:        var descriptor = BuiltInWorkflowExecutorDescriptors.StorageFile;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:143:        var planned = BuiltInWorkflowExecutorDescriptors.Planned[0];
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:145:        Assert.Equal(WorkflowExecutorSourceKind.BuiltIn, descriptor.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:146:        Assert.Equal(WorkflowExecutorSourceIds.BuiltIn, descriptor.Source.SourceId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:147:        Assert.Equal(WorkflowExecutorTrustLevel.Application, descriptor.Source.TrustLevel);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:148:        Assert.Equal(WorkflowExecutorAvailabilityKind.Available, descriptor.Availability.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:150:        Assert.Equal(WorkflowExecutorSettingsSchemaKind.JsonSchema, descriptor.SettingsSchema.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:152:        Assert.True(descriptor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsWorkspace));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:157:        Assert.Equal(WorkflowExecutorAvailabilityKind.Planned, planned.Availability.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:162:    public void WorkflowExecutorDescriptorDeserializesLegacyJsonWithDefaultMetadata()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:194:        var descriptor = System.Text.Json.JsonSerializer.Deserialize<WorkflowExecutorDescriptor>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:203:        Assert.Equal(WorkflowExecutorSourceKind.BuiltIn, descriptor.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:204:        Assert.Equal(WorkflowExecutorAvailabilityKind.Available, descriptor.Availability.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:205:        Assert.Equal(WorkflowExecutorSettingsSchemaKind.JsonSchema, descriptor.SettingsSchema.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:207:        Assert.Equal(WorkflowExecutorPermissionPolicy.None, descriptor.PermissionPolicy);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:208:        Assert.Equal(WorkflowExecutorSideEffectDescriptor.None, descriptor.SideEffects);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:215:        var plannedExecutor = new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:216:        var catalog = new WorkflowExecutorCatalog([plannedExecutor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:217:        var validator = new WorkflowDefinitionValidator(catalog);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:220:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:222:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:231:            issue.Code == WorkflowValidationIssueCode.InvalidExecutorReference &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:238:        var plannedExecutor = new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:239:        var catalog = new WorkflowExecutorCatalog([plannedExecutor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:240:        var invoker = new WorkflowExecutorInvoker(catalog, [plannedExecutor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:243:        var exception = await Assert.ThrowsAsync<WorkflowExecutorUnavailableException>(() => invoker.ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:246:            new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:249:        Assert.Equal(WorkflowExecutorAvailabilityKind.Planned, exception.Availability.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:255:        var first = new RecordingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:256:        var second = new RecordingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:257:        var catalog = new WorkflowExecutorCatalog([first]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:259:        var exception = Assert.Throws<InvalidOperationException>(() => new WorkflowExecutorInvoker(catalog, [first, second]));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:261:        Assert.Contains(WorkflowExecutorIds.StorageFile.Value, exception.Message, StringComparison.OrdinalIgnoreCase);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:267:        var catalog = new WorkflowExecutorCatalog([new RecordingWorkflowExecutor()]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:268:        var validator = new WorkflowDefinitionValidator(catalog);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:271:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:272:            CreateExecutorNode("tool", new WorkflowExecutorId("missing.executor")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:273:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:281:        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidExecutorReference);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:287:        var catalog = new WorkflowExecutorCatalog([new RecordingWorkflowExecutor()]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:288:        var validator = new WorkflowDefinitionValidator(catalog);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:291:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:292:            CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:294:                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:296:                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 0 }
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:299:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:307:        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidExecutionPolicy);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:311:    public async Task MafCompilerInvokesExecutorNodeThroughInvoker()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:313:        var executor = new RecordingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:314:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:315:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:316:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:317:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:320:            CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:321:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:327:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:328:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:337:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:341:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:344:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:346:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:349:            workflowEvent.Kind == WorkflowEventKind.ExecutorInvoked &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:350:            workflowEvent.NodeId == new WorkflowNodeId("tool"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:352:            workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted &&
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:353:            workflowEvent.NodeId == new WorkflowNodeId("tool"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:357:    public async Task MafBackendRecordsFailedExecutorEventWithoutAmbiguousDataReflection()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:359:        var executor = new RecordingWorkflowExecutor { FailuresBeforeSuccess = 1 };
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:360:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:361:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:362:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:363:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:366:            CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:367:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:373:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:374:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:383:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:387:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:390:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:392:        Assert.Equal(WorkflowRunState.Failed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:393:        Assert.Contains(result.Events, workflowEvent => workflowEvent.Kind is WorkflowEventKind.ExecutorFailed or WorkflowEventKind.Error);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:398:    public async Task MafBackendRecordsConfiguredFileArtifactsForCompletedFileWrites()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:400:        var executor = new RecordingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:401:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:402:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:403:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:404:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:408:            CreateExecutorNode("write-summary", WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:410:                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:413:                        new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:415:                            Operation = WorkflowStorageFileOperation.WriteText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:422:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:428:            RuntimePolicy = new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:429:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:438:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:442:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:445:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:447:        var artifact = Assert.Single(result.Artifacts, artifact => artifact.Kind == WorkflowArtifactKind.File);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:448:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:449:        Assert.Equal(WorkflowArtifactKind.File, artifact.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:450:        Assert.Equal(new WorkflowNodeId("write-summary"), artifact.NodeId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:456:    public async Task MafCompilerRoutesStartOutputIntoExecutorNode()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:458:        var executor = new RecordingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:459:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:460:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:461:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:462:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:465:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:466:            CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:467:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:475:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:479:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:482:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:484:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:489:    public async Task MafCompilerSkipsPredicateFalseBranch()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:491:        var executor = new BranchRecordingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:492:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:493:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:494:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:495:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:498:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:499:            CreateExecutorNode("spam", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:500:            CreateExecutorNode("normal", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:501:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:507:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:508:                WorkflowEdgeRouting.Predicate(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:510:                    WorkflowRouteOperator.Equals,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:512:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:518:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:519:                WorkflowEdgeRouting.Predicate(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:521:                    WorkflowRouteOperator.NotEquals,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:523:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:531:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:535:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:538:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:540:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:546:    public async Task MafCompilerUsesSwitchDefaultWhenNoCaseMatches()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:548:        var executor = new BranchRecordingWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:555:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:556:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:557:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:558:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:561:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:562:            CreateExecutorNode("classify", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:563:            CreateExecutorNode("approved", WorkflowExecutorIds.StorageFile, JsonObjectShape),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:564:            CreateExecutorNode("rework", WorkflowExecutorIds.StorageFile, JsonObjectShape),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:565:            CreateExecutorNode("manual", WorkflowExecutorIds.StorageFile, JsonObjectShape),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:566:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:573:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:574:                WorkflowEdgeRouting.SwitchCase("$.decision", "\"approved\"", WorkflowRouteValueKind.String, "approved")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:579:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:580:                WorkflowEdgeRouting.SwitchCase("$.decision", "\"rework\"", WorkflowRouteValueKind.String, "rework")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:585:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:586:                WorkflowEdgeRouting.SwitchDefault("default manual review")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:594:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:598:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:601:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:603:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:611:    public async Task MafCompilerFanOutRoutesOnlySelectedTargets()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:613:        var executor = new BranchRecordingWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:620:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:621:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:622:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:623:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, []);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:626:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:627:            CreateExecutorNode("select-channels", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:628:            CreateExecutorNode("email", WorkflowExecutorIds.StorageFile, JsonObjectShape),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:629:            CreateExecutorNode("slack", WorkflowExecutorIds.StorageFile, JsonObjectShape),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:630:            CreateExecutorNode("ticket", WorkflowExecutorIds.StorageFile, JsonObjectShape),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:631:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:638:                WorkflowEdgeKind.FanOut,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:639:                WorkflowEdgeRouting.FanOutSelector(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:641:                    WorkflowRouteOperator.Contains,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:643:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:650:                WorkflowEdgeKind.FanOut,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:651:                WorkflowEdgeRouting.FanOutSelector(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:653:                    WorkflowRouteOperator.Contains,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:655:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:662:                WorkflowEdgeKind.FanOut,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:663:                WorkflowEdgeRouting.FanOutSelector(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:665:                    WorkflowRouteOperator.Contains,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:667:                    WorkflowRouteValueKind.String,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:677:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:681:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:684:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:686:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:695:        var compiler = new BuiltInJsonWorkflowRoutingCompiler();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:698:            CreateRouteScenario("invoice over approval threshold", "{\"invoice\":{\"amount\":1250}}", "$.invoice.amount", WorkflowRouteOperator.GreaterThan, "1000", WorkflowRouteValueKind.Number, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:699:            CreateRouteScenario("small invoice auto approval", "{\"invoice\":{\"amount\":250}}", "$.invoice.amount", WorkflowRouteOperator.LessThanOrEqual, "500", WorkflowRouteValueKind.Number, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:700:            CreateRouteScenario("enterprise customer switch case", "{\"customer\":{\"tier\":\"enterprise\"}}", "$.customer.tier", WorkflowRouteOperator.Equals, "\"enterprise\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:701:            CreateRouteScenario("support ticket urgent priority", "{\"ticket\":{\"priority\":\"Urgent\"}}", "$.ticket.priority", WorkflowRouteOperator.Equals, "\"urgent\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:702:            CreateRouteScenario("fraud risk above review score", "{\"risk\":{\"score\":0.92}}", "$.risk.score", WorkflowRouteOperator.GreaterThanOrEqual, "0.85", WorkflowRouteValueKind.Number, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:703:            CreateRouteScenario("inventory does not need restock", "{\"stock\":{\"onHand\":42}}", "$.stock.onHand", WorkflowRouteOperator.LessThan, "10", WorkflowRouteValueKind.Number, false),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:704:            CreateRouteScenario("email notification selected", "{\"channels\":[\"email\",\"slack\"]}", "$.channels", WorkflowRouteOperator.Contains, "\"email\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:705:            CreateRouteScenario("sms notification not selected", "{\"channels\":[\"email\",\"slack\"]}", "$.channels", WorkflowRouteOperator.Contains, "\"sms\"", WorkflowRouteValueKind.String, false),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:706:            CreateRouteScenario("incident starts with sev prefix", "{\"incident\":{\"severity\":\"sev-1\"}}", "$.incident.severity", WorkflowRouteOperator.StartsWith, "\"sev-\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:707:            CreateRouteScenario("document ends with pdf extension", "{\"file\":{\"name\":\"contract.pdf\"}}", "$.file.name", WorkflowRouteOperator.EndsWith, "\".pdf\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:708:            CreateRouteScenario("customer note contains renewal", "{\"note\":\"Renewal requested by account owner\"}", "$.note", WorkflowRouteOperator.Contains, "\"renewal\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:709:            CreateRouteScenario("missing approval reason", "{\"approval\":{\"status\":\"approved\"}}", "$.approval.reason", WorkflowRouteOperator.DoesNotExist, "", WorkflowRouteValueKind.Json, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:710:            CreateRouteScenario("approval flag truthy", "{\"approval\":{\"approved\":true}}", "$.approval.approved", WorkflowRouteOperator.IsTruthy, "", WorkflowRouteValueKind.Json, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:711:            CreateRouteScenario("archive flag falsy", "{\"archive\":false}", "$.archive", WorkflowRouteOperator.IsFalsy, "", WorkflowRouteValueKind.Json, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:712:            CreateRouteScenario("region is not blocked", "{\"region\":\"emea\"}", "$.region", WorkflowRouteOperator.NotEquals, "\"blocked\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:713:            CreateRouteScenario("first line item sku match", "{\"items\":[{\"sku\":\"A1\"}]}", "$.items[0].sku", WorkflowRouteOperator.Equals, "\"A1\"", WorkflowRouteValueKind.String, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:714:            CreateRouteScenario("contract expiration is present", "{\"contract\":{\"expiresOn\":\"2026-12-31\"}}", "$.contract.expiresOn", WorkflowRouteOperator.Exists, "", WorkflowRouteValueKind.Json, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:715:            CreateRouteScenario("nullable manager assignment", "{\"manager\":null}", "$.manager", WorkflowRouteOperator.Equals, "null", WorkflowRouteValueKind.Null, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:716:            CreateRouteScenario("lead score is below sales handoff", "{\"lead\":{\"score\":61}}", "$.lead.score", WorkflowRouteOperator.LessThan, "75", WorkflowRouteValueKind.Number, true),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:717:            CreateRouteScenario("sentiment avoids negative path", "{\"sentiment\":\"neutral\"}", "$.sentiment", WorkflowRouteOperator.NotEquals, "\"negative\"", WorkflowRouteValueKind.String, true)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:719:        var definition = CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), CreateNode("end", WorkflowNodeKind.End)], [
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:730:                WorkflowEdgeKind.Conditional,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:731:                WorkflowEdgeRouting.Predicate(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:739:            Assert.Equal(scenario.Expected, route.Predicate(new WorkflowNodeInput(scenario.PayloadJson)));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:747:    public async Task MafCompilerRoutesExecutorOutputThroughLlmIntoNextExecutor()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:750:            inputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "Project tree JSON"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:751:            resultShape: WorkflowValueShape.Text);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:752:        var executor = new RoutingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:753:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:754:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:757:        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker, llmInvoker);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:758:        var backend = new MafInProcessWorkflowExecutionBackend(compiler, [component]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:761:            CreateNode("start", WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:762:            CreateExecutorNode("read-tree", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:764:            CreateExecutorNode("save-asset", WorkflowExecutorIds.StorageFile),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:765:            CreateNode("end", WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:775:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:779:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:782:            WorkflowRunId.New());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:784:        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:790:    public async Task MafWorkflowLlmComponentInvokerPassesProjectScopeFromWorkflowPayload()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:802:        var invoker = new MafWorkflowLlmComponentInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:814:            new WorkflowNodeInput($$"""
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:838:    public async Task MafWorkflowLlmComponentInvokerUsesProviderUsageObservationsForWorkflowUsage()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:846:                CreateWorkflowUsageObservation(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:857:        var invoker = new MafWorkflowLlmComponentInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:869:            new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:881:    public async Task MafWorkflowLlmComponentInvokerMarksUnavailableWorkflowUsageAsUnknown()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:889:                CreateWorkflowUsageObservation(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:900:        var invoker = new MafWorkflowLlmComponentInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:912:            new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:922:    public async Task MafWorkflowLlmComponentInvokerRequestsJsonResponseFormatSchemaForJsonComponents()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:945:        var invoker = new MafWorkflowLlmComponentInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:960:            new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:970:    public async Task MafWorkflowLlmComponentInvokerRequestsGenericJsonResponseFormatWhenSchemaIsMissing()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:974:        var invoker = new MafWorkflowLlmComponentInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:986:            new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:995:    public async Task MafWorkflowLlmComponentInvokerRejectsInvalidJsonWithoutRepairingPayload()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:999:        var invoker = new MafWorkflowLlmComponentInvoker(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1011:            new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1023:        var executor = new RecordingWorkflowExecutor { FailuresBeforeSuccess = 1 };
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1024:        var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1025:        var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1026:        var node = CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1028:            Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1030:                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1039:            CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), node, CreateNode("end", WorkflowNodeKind.End)], [
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1044:            new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1055:        var executor = new WorkspaceFileWorkflowExecutor(service);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1058:            new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1060:                Operation = WorkflowStorageFileOperation.WriteText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1065:        await executor.ExecuteAsync(writeContext, new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1069:            new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1071:                Operation = WorkflowStorageFileOperation.ReadText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1074:        var result = await executor.ExecuteAsync(readContext, new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1084:        var executor = new WorkspaceFileWorkflowExecutor(service);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1086:        await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1088:            Operation = WorkflowStorageFileOperation.CreateDirectory,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1091:        await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1093:            Operation = WorkflowStorageFileOperation.WriteText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1097:        var hash = await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1099:            Operation = WorkflowStorageFileOperation.Hash,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1102:        var zip = await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1104:            Operation = WorkflowStorageFileOperation.Zip,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1108:        var dryRun = await ExecuteDirectAsync(executor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1110:            Operation = WorkflowStorageFileOperation.Delete,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1125:        var executor = new JsonTransformWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1128:            new WorkflowJsonTransformExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1132:                    new WorkflowJsonTransformStep
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1134:                        Operation = WorkflowJsonTransformOperation.ArrayFilter,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1140:                    new WorkflowJsonTransformStep
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1142:                        Operation = WorkflowJsonTransformOperation.Count,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1153:            new WorkflowJsonTransformExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1157:                    new WorkflowJsonTransformStep
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1159:                        Operation = WorkflowJsonTransformOperation.Select,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1172:        var executor = new MarkdownRenderWorkflowExecutor(files);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1176:            new WorkflowMarkdownRenderExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1185:                    new WorkflowMarkdownTableBinding
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1203:        var delay = new DelayWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1204:        var delayResult = await ExecuteDirectAsync(delay, new WorkflowDelayExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1210:        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(delay, new WorkflowDelayExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1216:        var approval = new HumanApprovalWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1217:        var approvalContext = CreateExecutionContext(approval.Descriptor, new WorkflowApprovalExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1222:            RunId = WorkflowRunId.New()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1224:        var exception = await Assert.ThrowsAsync<WorkflowExternalRequestPendingException>(() => approval.ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1226:            new WorkflowNodeInput("{\"release\":\"v1\"}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1227:        Assert.Equal(WorkflowExternalRequestKind.Approval, exception.Request.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1234:        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1246:        var httpResult = await ExecuteDirectAsync(new HttpFetchWorkflowExecutor(files: files), new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1258:            new SourceIngestionWorkflowExecutor(new WorkspacePathResolutionService(temp.Path)),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1259:            new WorkflowSourceIngestionExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1370:    public async Task WorkflowExecutorScenarioMatrixCoversRealWorldExamples()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1374:        var storageExecutor = new WorkspaceFileWorkflowExecutor(workspaceFiles);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1375:        var spreadsheetExecutor = new SpreadsheetWorkflowExecutor(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1388:            await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1390:                Operation = WorkflowStorageFileOperation.WriteText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1400:            await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1402:                Operation = WorkflowStorageFileOperation.AppendText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1412:            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1414:                Operation = WorkflowStorageFileOperation.ReadText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1423:            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1425:                Operation = WorkflowStorageFileOperation.List,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1435:            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1437:                Operation = WorkflowStorageFileOperation.Stat,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1446:            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1448:                Operation = WorkflowStorageFileOperation.SearchText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1460:            var result = await ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1462:                Operation = WorkflowStorageFileOperation.DiffText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1472:            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(storageExecutor, new WorkflowStorageFileExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1474:                Operation = WorkflowStorageFileOperation.ReadText,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1482:            var result = await ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1484:                Method = WorkflowHttpMethodKind.Get,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1496:                new HttpFetchWorkflowExecutor(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1497:                new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1499:                    Method = WorkflowHttpMethodKind.Get,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1513:            var result = await ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1515:                Method = WorkflowHttpMethodKind.Post,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1529:                new HttpFetchWorkflowExecutor(new StaticSecretRuntimeResolver(secretId, "secret-token")),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1530:                new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1532:                    Method = WorkflowHttpMethodKind.Get,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1535:                    SecretHeader = new WorkflowHttpSecretHeaderBinding
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1540:                        ValueFormat = WorkflowHttpSecretValueFormat.Bearer
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1550:            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1552:                Method = WorkflowHttpMethodKind.Get,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1554:                SecretHeader = new WorkflowHttpSecretHeaderBinding
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1564:            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1566:                Method = WorkflowHttpMethodKind.Get,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1574:            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new HttpFetchWorkflowExecutor(), new WorkflowHttpExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1582:            await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1584:                Operation = WorkflowSpreadsheetOperation.WriteCell,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1597:            var result = await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1599:                Operation = WorkflowSpreadsheetOperation.ReadCell,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1610:            await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1612:                Operation = WorkflowSpreadsheetOperation.ApplyBatch,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1617:                    new WorkflowSpreadsheetRangeWrite("A2:C4", [["Customer", "Amount", "Status"], ["Aqua", "120", "Paid"], ["Contoso", "80", "Open"]])
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1626:            var result = await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1628:                Operation = WorkflowSpreadsheetOperation.RangeToMarkdown,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1639:            var result = await ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1641:                Operation = WorkflowSpreadsheetOperation.WorkbookSummary,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1650:            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(spreadsheetExecutor, new WorkflowSpreadsheetExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1652:                Operation = WorkflowSpreadsheetOperation.WorkbookSummary,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1659:            var executor = new RecordingWorkflowExecutor { FailuresBeforeSuccess = 1 };
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1660:            var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1661:            var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1662:            var node = CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1664:                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1666:                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1674:            await invoker.ExecuteAsync(CreateDefinition([node], [], "tool"), node, new WorkflowNodeInput("{}"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1681:            var executor = new RecordingWorkflowExecutor();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1682:            var catalog = new WorkflowExecutorCatalog([executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1683:            var invoker = new WorkflowExecutorInvoker(catalog, [executor]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1684:            var node = CreateExecutorNode("tool", WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1686:                Settings = CreateSettings(WorkflowExecutorIds.StorageFile) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1688:                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 0 }
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1692:            await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.ExecuteAsync(CreateDefinition([node], [], "tool"), node, new WorkflowNodeInput("{}")).AsTask());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1697:            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(new ProjectStructureWorkflowExecutor(new UnavailableProjectStructureRuntimeGateway()), new WorkflowProjectStructureExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1699:                Operation = WorkflowProjectStructureOperation.ListProjects,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1715:            var executor = new ImageGenerationWorkflowExecutor(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1720:            var result = await ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1751:            var executor = new ImageGenerationWorkflowExecutor(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1756:            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteDirectAsync(executor, new WorkflowImageGenerationExecutorSettings
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1758:                Operation = WorkflowImageGenerationOperation.Edit,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1767:            await Assert.ThrowsAsync<NotSupportedException>(() => ExecuteDirectAsync(new PlannedWorkflowExecutor(BuiltInWorkflowExecutorDescriptors.Planned[0]), new { }));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1773:    private static WorkflowExecutorExecutionContext CreateExecutionContext<TSettings>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1774:        WorkflowExecutorDescriptor descriptor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1788:        return new WorkflowExecutorExecutionContext(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1789:            CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), node, CreateNode("end", WorkflowNodeKind.End)], [
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1796:            WorkflowExecutorExecutionPolicy.Default);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1799:    private static WorkflowDefinition CreateDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1800:        IReadOnlyList<WorkflowNode> nodes,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1801:        IReadOnlyList<WorkflowEdge> edges,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1804:        return new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1805:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1806:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1809:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1810:            new WorkflowGraph(new WorkflowNodeId(startNodeId), nodes, edges),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1811:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1812:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1821:    private static async Task<WorkflowNodeExecutionResult> ExecuteDirectAsync<TSettings>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1822:        IWorkflowExecutor executor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1826:    private static async Task<WorkflowNodeExecutionResult> ExecuteDirectAsync<TSettings>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1827:        IWorkflowExecutor executor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1833:            new WorkflowNodeInput(inputJson));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1836:    private static WorkflowNode CreateExecutorNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1838:        WorkflowExecutorId executorId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1839:        WorkflowValueShape? inputShape = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1841:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1842:            WorkflowNodeKind.Executor,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1847:    private static WorkflowNode CreateLlmNode(string id, WorkflowComponentId componentId)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1849:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1850:            WorkflowNodeKind.LlmCall,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1853:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1859:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1860:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1862:    private static WorkflowNodeSettings CreateSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1863:        WorkflowExecutorId executorId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1864:        WorkflowValueShape? inputShape = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1865:        => new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1871:            InputShape: inputShape ?? WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1872:            ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")) with
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1876:            ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1879:    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1881:            new WorkflowNodeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1885:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1891:                InputShape: kind == WorkflowNodeKind.End
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1892:                    ? new WorkflowValueShape(WorkflowValueShapeKind.Object, "{}", "Any result")
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1893:                    : WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1894:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1896:    private static WorkflowEdge CreateEdge(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1900:        WorkflowEdgeKind kind = WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1901:        WorkflowEdgeRouting? routing = null)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1903:            new WorkflowEdgeId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1904:            new WorkflowNodeId(source),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1906:            new WorkflowNodeId(target),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1911:            Routing = routing ?? WorkflowEdgeRouting.Always
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1918:        WorkflowRouteOperator @operator,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1920:        WorkflowRouteValueKind expectedValueKind,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1932:        WorkflowValueShape inputShape,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1933:        WorkflowValueShape resultShape,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1936:            WorkflowComponentId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1940:            WorkflowModality.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1941:            new WorkflowModelSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1944:                RequireJsonOutput: resultShape.Kind == WorkflowValueShapeKind.Json,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1956:            "Workflow unit provider",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1974:    private static ProviderUsageObservation CreateWorkflowUsageObservation(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1983:            ProviderName: "Workflow unit provider",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1997:    private sealed class RecordingWorkflowExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:1999:        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2005:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2006:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2007:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2016:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2023:    private sealed class BranchRecordingWorkflowExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2027:        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2034:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2035:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2036:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2045:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2052:    private sealed class RoutingWorkflowExecutor : IWorkflowExecutor
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2054:        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2058:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2059:            WorkflowExecutorExecutionContext context,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2060:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2071:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2078:    private sealed class RecordingLlmComponentInvoker(Func<string, string> transform) : IWorkflowLlmComponentInvoker
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2082:        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2083:            WorkflowDefinition definition,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2084:            WorkflowNode node,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2086:            WorkflowNodeInput input,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2090:            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2385:        WorkflowRouteOperator Operator,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs:2387:        WorkflowRouteValueKind ExpectedValueKind,
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceArtifactToolServiceTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceArtifactToolServiceTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:3:using CanDoItAll.AgentFramework.Workflows.Templates;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:8:public sealed class WorkflowTemplatePackLoaderTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:13:        var pack = new WorkflowTemplatePackLoader().Load();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:15:        Assert.Equal(5, pack.Manifest.WorkflowFiles.Count);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:16:        Assert.NotEmpty(pack.Workflows);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:17:        Assert.All(pack.Workflows, template =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:32:        var previewCatalog = new WorkflowPreviewSimulationTemplateLoader().Load(pack.RootPath);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:33:        var projectStructure = Assert.Contains(WorkflowExecutorIds.ProjectStructure.Value, previewCatalog.Executors);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:34:        Assert.Contains(nameof(WorkflowProjectStructureOperation.CreateAsset), projectStructure.Operations.Keys);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:35:        Assert.Contains(nameof(WorkflowProjectStructureOperation.CreateTaskNodes), projectStructure.Operations.Keys);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:41:        var pack = new WorkflowTemplatePackLoader().Load();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:42:        var executorIds = pack.Workflows
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:49:        var catalog = WorkflowExecutorCatalog.FromDescriptors(executorIds.Select(id => CreateDescriptor(id)));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:51:        var validatedPack = new WorkflowTemplatePackLoader(pack.RootPath, catalog).Load();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:54:            pack.Workflows.Select(template => template.Key).OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:55:            validatedPack.Workflows.Select(template => template.Key).OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:61:        var pack = new WorkflowTemplatePackLoader().Load();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:64:            pack.Workflows.SelectMany(template =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:94:        Assert.Contains(pack.Workflows, template => template.Key == "offer-document-folder-summary");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:95:        Assert.Contains(pack.Workflows, template => template.Key == "offer-price-list-extraction");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:96:        Assert.Contains(pack.Workflows, template => template.Name == "Offer Document Folder Summary");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:97:        Assert.Contains(pack.Workflows, template => template.Name == "Offer Price List Extraction");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:120:        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:122:            CreateLinearExecutorWorkflow("missing-executor", "missing.executor"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:123:        var catalog = WorkflowExecutorCatalog.FromDescriptors([CreateDescriptor("known.executor")]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:125:        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:126:            new WorkflowTemplatePackLoader(packDirectory.RootPath, catalog).Load());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:128:        Assert.Equal(WorkflowTemplateFailureKind.DescriptorValidationFailed, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:139:        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:162:        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:163:            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:165:        Assert.Equal(WorkflowTemplateFailureKind.GraphMaterializationFailed, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:173:        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:195:        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:196:            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:198:        Assert.Equal(WorkflowTemplateFailureKind.InputParameterInvalid, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:206:        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:208:            CreateLinearExecutorWorkflow("invalid-runtime", "known.executor"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:211:        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:212:            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:214:        Assert.Equal(WorkflowTemplateFailureKind.GraphMaterializationFailed, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:222:        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:224:            CreateLinearExecutorWorkflow(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:230:        var catalog = WorkflowExecutorCatalog.FromDescriptors([
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:238:        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:239:            new WorkflowTemplatePackLoader(packDirectory.RootPath, catalog).Load());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:241:        Assert.Equal(WorkflowTemplateFailureKind.SemanticValidationFailed, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:249:        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:253:        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:254:            new WorkflowTemplatePackLoader(packDirectory.RootPath).Load());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:256:        Assert.Equal(WorkflowTemplateFailureKind.WorkflowLoadFailed, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:263:        using var packDirectory = TemporaryWorkflowTemplatePack.Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:265:            CreateLinearExecutorWorkflow("valid", "known.executor"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:268:        var exception = Assert.Throws<WorkflowTemplatePackException>(() =>
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:269:            new WorkflowPreviewSimulationTemplateLoader().Load(packDirectory.RootPath));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:271:        Assert.Equal(WorkflowTemplateFailureKind.PreviewSimulationInvalid, exception.FailureKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:283:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:285:            "WorkflowTemplatePackLoader.cs");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:290:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:291:            "CanDoItAll.Modules.AgentFramework.csproj"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:296:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:297:            "CanDoItAll.AgentFramework.Workflows.Templates",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:298:            "CanDoItAll.AgentFramework.Workflows.Templates.csproj"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:302:        Assert.DoesNotContain("CanDoItAll.Modules.AgentFramework", templateProject, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:303:        Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", templateProject, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:308:        WorkflowTemplatePack pack,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:309:        WorkflowTemplateDefinition template)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:311:            WorkflowComponentId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:315:            WorkflowModality.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:324:    private static WorkflowExecutorDescriptor CreateDescriptor(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:328:            new WorkflowExecutorId(id),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:331:            WorkflowExecutorCategoryKind.Utility,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:334:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON payload"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:335:            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON payload"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:338:            WorkflowExecutorExecutionPolicy.Default,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:344:    private static string CreateLinearExecutorWorkflow(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:400:    private sealed class TemporaryWorkflowTemplatePack : IDisposable
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:402:        private TemporaryWorkflowTemplatePack(string rootPath)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:409:        public static TemporaryWorkflowTemplatePack Create(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowTemplatePackLoaderTests.cs:465:            return new TemporaryWorkflowTemplatePack(root);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:5:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:6:using CanDoItAll.AgentFramework.Workflows.Abstractions;
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:11:public sealed class WorkflowRuntimeExtractionTests
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:16:    public void WorkflowRuntimeProjectDoesNotReferenceForbiddenImplementationProjects()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:23:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:24:            "CanDoItAll.AgentFramework.Workflows.Runtime",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:25:            "CanDoItAll.AgentFramework.Workflows.Runtime.csproj");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:28:            "CanDoItAll.AgentFramework.Maf",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:29:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:32:            "CanDoItAll.AgentFramework.Persistence",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:54:    public void WorkflowRuntimeImplementationFilesMovedOutOfAgentFrameworkCoreProject()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:59:            "WorkflowContracts.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:60:            "WorkflowRuntimeManager.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:61:            "WorkflowExternalRequestRuntime.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:62:            "WorkflowArtifactContentStores.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:63:            "WorkflowEventPayloads.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:64:            "WorkflowNodeExecutionProgress.cs"
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:70:                File.Exists(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", movedFile)),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:71:                $"{movedFile} must not remain in AgentFramework.Core.");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:73:                File.Exists(Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Runtime", movedFile)),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:74:                $"{movedFile} must exist in Workflows.Runtime.");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:79:    public void WorkflowRuntimeRegistrationExtensionOwnsRuntimeServiceRegistrations()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:89:            services.AddWorkflowRuntimeServices();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:90:            services.AddInMemoryWorkflowRuntimeStores(workspaceRoot);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:99:            Assert.IsType<WorkflowRuntimeManager>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:100:                scope.ServiceProvider.GetRequiredService<CanDoItAll.AgentFramework.Core.IWorkflowRuntimeManager>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:101:            Assert.IsType<InMemoryWorkflowRunStore>(scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:102:            Assert.IsType<FileWorkflowArtifactContentStore>(scope.ServiceProvider.GetRequiredService<IWorkflowArtifactContentStore>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:103:            Assert.IsType<WorkflowCheckpointFactory>(scope.ServiceProvider.GetRequiredService<IWorkflowCheckpointFactory>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:104:            Assert.IsType<NullWorkflowEventSink>(scope.ServiceProvider.GetRequiredService<IWorkflowEventSink>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:105:            Assert.IsType<WorkflowExternalRequestApprovalGate>(scope.ServiceProvider.GetRequiredService<IWorkflowExecutorApprovalGate>());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:117:    public void HostAndModuleRegistrationUseWorkflowRuntimeExtension()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:125:            "CanDoItAll.AgentFramework.Hosting",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:126:            "AgentFrameworkServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:131:            "CanDoItAll.Modules.AgentFramework",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:133:            "AgentFrameworkModuleServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:138:            "Workflows",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:139:            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:140:            "MafWorkflowAdapterServiceCollectionExtensions.cs"));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:142:        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Singleton)", hostingSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:143:        Assert.Contains("AddInMemoryWorkflowRuntimeStores", hostingSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:144:        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Scoped)", moduleSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:145:        Assert.Contains("AddFileWorkflowArtifactContentStore", moduleSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:146:        Assert.Contains("AddWorkflowRuntimeServices()", adapterSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:147:        Assert.DoesNotContain("TryAddScoped<IWorkflowRuntimeManager", hostingSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:148:        Assert.DoesNotContain("TryAddScoped<IWorkflowRuntimeManager", moduleSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:149:        Assert.DoesNotContain("TryAddSingleton<InMemoryWorkflowRunStore>", hostingSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:150:        Assert.DoesNotContain("new FileWorkflowArtifactContentStore", moduleSource, StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:157:        var manager = new WorkflowRuntimeManager([], new InMemoryWorkflowRunStore());
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:161:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:165:                WorkflowRuntimeBackendKind.DurableTask,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:168:        var diagnostic = Assert.Single(WorkflowRuntimeFailureDiagnosticMapper.GetDiagnostics(exception));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:170:        Assert.Equal(WorkflowFailureKind.Runtime, diagnostic.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:171:        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:172:        Assert.Equal(WorkflowFailureSourceKind.RuntimeBackend, diagnostic.Source.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:173:        Assert.Equal(WorkflowRuntimeBackendKind.DurableTask, diagnostic.Source.BackendKind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:174:        Assert.Equal(definition.Id, diagnostic.WorkflowId);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:181:        var store = new InMemoryWorkflowRunStore();
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:182:        var manager = new WorkflowRuntimeManager([], store);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:184:        var run = new WorkflowRunSnapshot(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:185:            WorkflowRunId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:186:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:187:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:188:            WorkflowRunState.Running,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:189:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:198:        var payload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:201:        var diagnostic = JsonSerializer.Deserialize<WorkflowFailureDiagnosticEnvelope>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:205:        Assert.Equal(WorkflowRunState.Cancelled, cancelled.State);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:206:        Assert.Equal(WorkflowEventKind.Cancelled, cancellationEvent.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:207:        Assert.Equal(WorkflowFailureKind.Cancellation, diagnostic.Kind);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:209:        Assert.Equal(WorkflowFailureRetryability.NotRetryable, diagnostic.Retryability);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:218:        var manager = new WorkflowRuntimeManager([new CompletedBackend()], store);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:222:            new WorkflowRunStartRequest(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:226:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:233:    private static WorkflowDefinition CreateDefinition()
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:235:        var start = new WorkflowNodeId("start");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:236:        var end = new WorkflowNodeId("end");
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:237:        return new WorkflowDefinition(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:238:            WorkflowId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:239:            WorkflowVersionId.New(),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:242:            WorkflowLifecycleStatus.Draft,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:243:            new WorkflowGraph(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:246:                    CreateNode(start, WorkflowNodeKind.Start),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:247:                    CreateNode(end, WorkflowNodeKind.End)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:251:                        new WorkflowEdgeId("start-end"),
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:256:                        WorkflowEdgeKind.Direct,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:259:                        Routing = WorkflowEdgeRouting.Always
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:262:            new WorkflowRuntimePolicy(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:263:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:272:    private static WorkflowNode CreateNode(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:273:        WorkflowNodeId id,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:274:        WorkflowNodeKind kind)
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:280:            new WorkflowNodeSettings(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:286:                InputShape: WorkflowValueShape.Text,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:287:                ResultShape: WorkflowValueShape.Text));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:312:    private sealed class CompletedBackend : IWorkflowExecutionBackend
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:314:        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:315:            WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:323:        public Task<WorkflowBackendStartResult> StartAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:324:            WorkflowDefinition definition,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:325:            WorkflowRunStartRequest request,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:326:            WorkflowRunId runId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:331:            var run = new WorkflowRunSnapshot(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:335:                WorkflowRunState.Completed,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:336:                WorkflowRuntimeBackendKind.InProcess,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:342:            return Task.FromResult(new WorkflowBackendStartResult(run, [], [], []));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:346:    private sealed class ThrowingSaveRunStore(Exception saveRunException) : IWorkflowRunStore
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:349:            WorkflowRunSnapshot run,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:353:        public Task<WorkflowRunSnapshot?> GetRunAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:354:            WorkflowRunId runId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:356:            => Task.FromResult<WorkflowRunSnapshot?>(null);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:358:        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:359:            WorkflowId? workflowId = null,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:361:            => Task.FromResult<IReadOnlyList<WorkflowRunSnapshot>>([]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:363:        public Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:364:            WorkflowRunPageRequest request,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:366:            => Task.FromResult(new WorkflowListPage<WorkflowRunSnapshot>([], request.PageIndex, request.PageSize, 0));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:369:            WorkflowEventRecord workflowEvent,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:373:        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:374:            WorkflowRunId runId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:376:            => Task.FromResult<IReadOnlyList<WorkflowEventRecord>>([]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:378:        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:379:            WorkflowEventPageRequest request,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:381:            => Task.FromResult(new WorkflowListPage<WorkflowEventRecord>([], request.PageIndex, request.PageSize, 0));
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:384:            WorkflowExternalRequestRecord request,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:388:        public Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:389:            WorkflowExternalRequestId requestId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:391:            => Task.FromResult<WorkflowExternalRequestRecord?>(null);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:393:        public Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:394:            WorkflowRunId runId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:396:            => Task.FromResult<IReadOnlyList<WorkflowExternalRequestRecord>>([]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:399:            WorkflowArtifactRecord artifact,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:403:        public Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:404:            WorkflowRunId runId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:406:            => Task.FromResult<IReadOnlyList<WorkflowArtifactRecord>>([]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:408:        public Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:409:            WorkflowCheckpointRecord checkpoint,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:413:        public Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:414:            WorkflowCheckpointId checkpointId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:416:            => Task.FromResult<WorkflowCheckpointRecord?>(null);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:418:        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:419:            WorkflowRunId runId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:421:            => Task.FromResult<IReadOnlyList<WorkflowCheckpointRecord>>([]);
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:423:        public Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:424:            WorkflowCheckpointId checkpointId,
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:427:            => Task.FromException<WorkflowCheckpointRecord>(
tests\Unit\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs:428:                new KeyNotFoundException($"Workflow checkpoint '{checkpointId}' was not found."));
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceCommandExecutionServiceTests.cs:2:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceCommandExecutionServiceTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:13:        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.sln");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:31:        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.App");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:32:        var externalFilePath = Path.Combine(externalDirectory, "WorkflowService.cs");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:38:        var result = service.WriteTextFile(aliasPath, "public static class WorkflowService {}");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:43:        Assert.Contains("WorkflowService", File.ReadAllText(externalFilePath), StringComparison.Ordinal);
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:50:        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:82:        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:130:        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:240:        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.App");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:242:        var externalFilePath = Path.Combine(externalDirectory, "WorkflowService.cs");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:243:        File.WriteAllText(externalFilePath, "public static class WorkflowService { public static int Add(int left, int right) => left + right; }");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:260:        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Workflow");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:262:        var externalSolutionPath = Path.Combine(externalDirectory, "Workflow.sln");
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs:281:        var plan = builder.BuildDotnetNew("blazor", "WorkflowApp", force: true);
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFileServiceTests.cs:3:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFileServiceTests.cs:4:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFileServiceTests.cs:94:                "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/Program.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFileServiceTests.cs:104:                        "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/Program.cs",
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFilesystemRuntimePluginTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFilesystemRuntimePluginTests.cs:2:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFilesystemRuntimePluginTests.cs:3:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceFileQueryServiceTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceProcessRunArtifactPathTests.cs:1:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceProcessRunArtifactPathTests.cs:2:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceImageSetEvidenceBuilderTests.cs:4:using CanDoItAll.AgentFramework.Core;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceImageSetEvidenceBuilderTests.cs:5:using CanDoItAll.AgentFramework.Maf;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceImageSetEvidenceBuilderTests.cs:6:using CanDoItAll.AgentFramework.Models;
tests\Unit\CanDoItAll.Tests.Unit\WorkspaceRetrievalNoisePolicyTests.cs:1:using CanDoItAll.AgentFramework.Core;
ExitCode: 0
