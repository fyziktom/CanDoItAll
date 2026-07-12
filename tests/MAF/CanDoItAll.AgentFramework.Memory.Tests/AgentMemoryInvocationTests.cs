using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory.Tests;

public sealed class AgentMemoryInvocationTests
{
    [Fact]
    public void Directive_parser_accepts_only_leading_provider_aliases()
    {
        var parsed = MemoryDirectiveParser.Parse("  /mem:zeta /mem:alpha  explain this decision ");
        var embedded = MemoryDirectiveParser.Parse("explain /mem:zeta this decision");

        Assert.True(parsed.Success);
        Assert.Equal(["zeta", "alpha"], parsed.ProviderAliases.Select(alias => alias.Value));
        Assert.Equal("explain this decision", parsed.Query);
        Assert.Empty(embedded.ProviderAliases);
        Assert.Equal("explain /mem:zeta this decision", embedded.Query);
    }

    [Fact]
    public async Task Duplicate_directive_alias_is_rejected_without_dispatch()
    {
        var handler = new RoutingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(CreateSettings(
            AgentMemoryInvocationMode.ExplicitDirective,
            Binding("primary", "memory.primary")));

        var result = await contributor.ContributeAsync(
            CreateRequest(agent, "/mem:primary /mem:primary recall this"));

        Assert.Equal(AgentContextContributionStatus.Failed, result.Status);
        Assert.Contains("more than once", result.FailureMessage, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("\"/mem:primary\" recall this quote")]
    [InlineData("`/mem:primary` recall this code literal")]
    [InlineData("> /mem:primary recall this block quote")]
    [InlineData("```text\n/mem:primary\n```")]
    public void Directive_parser_does_not_execute_quoted_or_code_literal_text(string prompt)
    {
        var parsed = MemoryDirectiveParser.Parse(prompt);

        Assert.True(parsed.Success);
        Assert.Empty(parsed.ProviderAliases);
        Assert.Equal(prompt.Trim(), parsed.Query);
    }

    [Fact]
    public void Explicit_directive_planner_rejects_unknown_alias_without_fallback()
    {
        var settings = CreateSettings(
            AgentMemoryInvocationMode.ExplicitDirective,
            Binding("primary", "memory.primary"));

        var plan = AgentMemoryInvocationPlanner.Plan(settings, "/mem:missing recall architecture");

        Assert.Equal(AgentMemoryInvocationPlanDecision.Reject, plan.Decision);
        Assert.Contains("not configured", plan.Diagnostic, StringComparison.Ordinal);
        Assert.Empty(plan.Providers);
    }

    [Fact]
    public void Leading_directive_overrides_automatic_fan_out()
    {
        var settings = CreateSettings(
            AgentMemoryInvocationMode.Automatic,
            Binding("first", "memory.first"),
            Binding("second", "memory.second"));

        var plan = AgentMemoryInvocationPlanner.Plan(settings, "/mem:second recall architecture");

        Assert.Equal(AgentMemoryInvocationPlanDecision.Query, plan.Decision);
        Assert.Equal("recall architecture", plan.Query);
        Assert.Equal("memory.second", Assert.Single(plan.Providers).ProviderInstanceId.Value);
    }

    [Fact]
    public async Task Automatic_mode_preserves_configured_binding_order_and_labels_context()
    {
        var handler = new RoutingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(CreateSettings(
            AgentMemoryInvocationMode.Automatic,
            Binding("zeta", "memory.zeta"),
            Binding("alpha", "memory.alpha")));
        var contextIntent = new AgentRuntimeContextIntent(
            SourceKind: "process-step",
            SourceId: "process-definition-7",
            ProcessRunId: "process-run-9",
            ProcessStepId: "step-2",
            TargetScope: "project",
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: false,
            AllowsProductMutation: false,
            WorkspaceToolProfile: null,
            WorkspaceScope: WorkspaceScopeDescriptor.Project("project-42"),
            AllowedOperations: []);
        var request = CreateRequest(agent, "recall architecture") with
        {
            ContextIntent = contextIntent
        };

        var result = await contributor.ContributeAsync(request);

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        Assert.Equal(
            ["memory.alpha", "memory.zeta"],
            handler.Requests
                .Select(item => item.SelectionPolicy.ExplicitProviderId?.Value)
                .OrderBy(value => value, StringComparer.Ordinal));
        Assert.All(
            handler.Requests,
            item => Assert.Equal(MemoryProviderFallbackBehavior.DenyImplicitFallback, item.SelectionPolicy.FallbackBehavior));
        Assert.All(
            handler.Requests,
            item => Assert.Equal(["memory.alpha", "memory.zeta"], item.SelectionPolicy.AllowedProviderIds.Select(id => id.Value)));
        var message = Assert.Single(result.Messages).Text;
        Assert.True(
            message.IndexOf("Memory provider 'zeta'", StringComparison.Ordinal) <
            message.IndexOf("Memory provider 'alpha'", StringComparison.Ordinal));
        Assert.Equal("project-42", handler.Requests[0].Payload.Context.Execution.ProjectId);
        Assert.Equal("process-run-9", handler.Requests[0].Payload.Context.Execution.ProcessId);
        Assert.Equal("step-2", handler.Requests[0].Payload.Context.Execution.ProcessStepId);
        Assert.Equal("process-run-9", handler.Requests[0].Caller.Requester.ProcessId);
        Assert.Equal("step-2", handler.Requests[0].Caller.Requester.ProcessStepId);
    }

    [Fact]
    public async Task Explicit_directive_selects_only_named_provider()
    {
        var handler = new RoutingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(CreateSettings(
            AgentMemoryInvocationMode.ExplicitDirective,
            Binding("first", "memory.first"),
            Binding("second", "memory.second")));

        var result = await contributor.ContributeAsync(
            CreateRequest(agent, "/mem:second recall customer decision"));

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        var dispatched = Assert.Single(handler.Requests);
        Assert.Equal("memory.second", dispatched.SelectionPolicy.ExplicitProviderId?.Value);
        Assert.Equal("recall customer decision", dispatched.Payload.Query);
        Assert.Contains("Memory provider 'second'", Assert.Single(result.Messages).Text, StringComparison.Ordinal);
        var replacement = Assert.Single(result.RequestMessageTransformation?.TextReplacements ?? []);
        Assert.Equal(0, replacement.MessageIndex);
        Assert.Equal("recall customer decision", replacement.Text);
    }

    [Fact]
    public async Task Chat_context_uses_typed_intent_source_as_memory_session_identity()
    {
        var handler = new RoutingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(CreateSettings(
            AgentMemoryInvocationMode.Automatic,
            Binding("primary", "memory.primary")));
        var request = CreateRequest(agent, "recall this chat") with
        {
            ContextIntent = AgentRuntimeContextIntent.Empty with
            {
                SourceKind = "chat-session",
                SourceId = "chat-7",
                WorkspaceScope = WorkspaceScopeDescriptor.Project("project-42")
            }
        };

        await contributor.ContributeAsync(request);

        Assert.Equal("chat-7", Assert.Single(handler.Requests).Caller.Requester.SessionId);
    }

    [Fact]
    public async Task Unknown_directive_alias_fails_before_dispatch()
    {
        var handler = new RoutingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(CreateSettings(
            AgentMemoryInvocationMode.ExplicitDirective,
            Binding("known", "memory.known")));

        var result = await contributor.ContributeAsync(
            CreateRequest(agent, "/mem:unknown recall customer decision"));

        Assert.Equal(AgentContextContributionStatus.Failed, result.Status);
        Assert.Empty(handler.Requests);
        Assert.Equal(
            MemoryAgentContextContributionTraceReasons.InvalidDirective,
            result.TraceMetadata[MemoryAgentContextContributionTraceKeys.Reason]);
    }

    [Fact]
    public void Metadata_codec_rejects_malformed_or_unknown_memory_configuration()
    {
        Assert.Throws<AgentMemoryConfigurationException>(() =>
            AgentMemoryAccessMetadata.Read("{not-json}"));
        Assert.Throws<AgentMemoryConfigurationException>(() =>
            AgentMemoryAccessMetadata.Read("""{"memory":{"invocationMode":"Automatic","unknown":true}}"""));
    }

    private static AgentMemoryAccessSettings CreateSettings(
        AgentMemoryInvocationMode mode,
        params AgentMemoryProviderBindingSetting[] bindings)
    {
        return new AgentMemoryAccessSettings
        {
            InvocationMode = mode,
            ProviderBindings = bindings,
            AllowedProviderInstanceIds = bindings.Select(binding => binding.ProviderInstanceId).ToArray(),
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync]
        };
    }

    private static AgentMemoryProviderBindingSetting Binding(string alias, string providerId)
    {
        return new AgentMemoryProviderBindingSetting(
            AgentMemoryProviderAlias.Parse(alias),
            MemoryProviderInstanceId.Parse(providerId));
    }

    private static AgentDefinition CreateAgent(AgentMemoryAccessSettings memoryAccess)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Memory test agent",
            "Memory tester",
            "Tests generic memory routing.",
            "Use memory deliberately.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentMemoryAccessMetadata.Write("{}", memoryAccess),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static AgentContextContributionRequest CreateRequest(
        AgentDefinition agent,
        string prompt)
    {
        return new AgentContextContributionRequest(
            agent,
            CreateProviderProfile(),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, prompt)],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit")));
    }

    private static ProviderProfile CreateProviderProfile()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI chat",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
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
    }

    private sealed class RoutingMemoryOperationHandler : IMemoryOperationHandler
    {
        public List<MemoryOperationHandlerRequest<MemoryContextQueryRequest>> Requests { get; } = [];

        public Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
            MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Requests)
            {
                Requests.Add(request);
            }
            var providerId = request.SelectionPolicy.ExplicitProviderId
                ?? throw new InvalidOperationException("Test dispatch requires an explicit provider id.");
            var provider = CreateMemoryProvider(providerId);
            var pack = new MemoryContextPack(
                MemoryContextPackId.New(),
                $"Summary from {providerId.Value}",
                [new MemoryContextSection("Context", $"Context from {providerId.Value}", [], 0.9m)],
                [],
                0.9m,
                MemoryFeedbackHandle.Parse("feedback-" + Guid.NewGuid().ToString("N")));
            return Task.FromResult(new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.Completed,
                MemoryProviderSelectionResult.Selected(
                    provider,
                    MemoryProviderSelectionReason.ExplicitProvider,
                    MemoryCapabilityIds.ContextQuerySync),
                OperationRecord: null,
                pack,
                AcceptedOperation: null,
                pack.FeedbackHandle,
                DriverDispatchAttempted: true,
                Diagnostic: "Completed."));
        }

        public Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
            MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
            MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
            MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
            MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static MemoryProviderProfile CreateMemoryProvider(MemoryProviderInstanceId providerId)
        {
            return new MemoryProviderProfile(
                providerId,
                providerId.Value,
                MemoryProviderDriverKind.Mock,
                IsEnabled: true,
                MemoryProviderHealthState.Healthy,
                MemoryProviderWorkspaceScope.AllWorkspaces,
                SelectionTags: [],
                MemoryProviderProfilePolicy.Default,
                new MemoryProviderManifest(
                    MemoryProviderKind.Parse("mock.memory"),
                    MemoryProtocolVersion.Current,
                    [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "v1", Supported: true)],
                    MemoryProviderInteractionSupport.SyncQueryOnly,
                    UiSurfaces: [],
                    MemoryProviderLimits.Default,
                    MemoryExtensionData.Empty));
        }
    }
}
