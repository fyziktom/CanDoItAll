using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// Exercises <see cref="MafStreamingTurnExecutor"/>'s generic recovery coordinator directly (internal method,
/// per the SB13 direct-testability requirement) instead of re-driving the entire bounded streaming/repair
/// sequence: no suitable existing Integration harness feeds a controllable "missing required finalizer" provider
/// stream (see tests/Integration search performed for this subbundle), so this satisfies both the MAF-adapter
/// generic-typed-failure requirement and the "typed MAF failure -&gt; recovery coordinator -&gt; Processes policy
/// -&gt; ordinary completion assembly" integration requirement at the coordinator boundary. The success case
/// exercises the real <see cref="ProcessAgentExecutionOutcomeRecoveryPolicy"/> (Processes) through the real MAF
/// coordinator method and a real workspace-scoped artifact reader over a temporary filesystem root, so it is not a
/// pure in-process unit test of either side alone.
/// </summary>
public sealed class MafStreamingTurnExecutorRecoveryPolicyTests
{
    [Fact]
    public async Task Recovery_coordinator_recovers_governed_process_step_outcome_through_registered_processes_policy()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("streaming-turn-executor-recovery");
        try
        {
            var runId = Guid.NewGuid();
            const string sourceId = "code-change";
            var artifactRelativePath = $"artifacts/process-runs/{runId:D}/steps/{sourceId}.md";
            var artifactAbsolutePath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                runId.ToString("D"),
                "steps",
                $"{sourceId}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactAbsolutePath)!);
            File.WriteAllText(
                artifactAbsolutePath,
                """
                # Feature implementation change set

                Status: Completed
                """);

            var executor = new MafStreamingTurnExecutor(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                new NeverInvokedProviderAgentFactory(),
                new MafApprovalContinuationDriver(),
                new MafRuntimeSessionPersistenceDriver(),
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                [new ProcessAgentExecutionOutcomeRecoveryPolicy()]);
            AgentFinalizerPolicies.TryResolveForStructuredOutput(
                AgentStructuredOutputContracts.ProcessStepOutcomeResult,
                out var policy);
            var contextIntent = new AgentRuntimeContextIntent(
                SourceKind: "process-step",
                SourceId: sourceId,
                ProcessRunId: runId.ToString("D"),
                ProcessStepId: Guid.NewGuid().ToString("D"),
                TargetScope: "ExternalProductTargetMutable",
                IsGovernedProcessStep: true,
                BrowserToolsAllowed: false,
                AllowsProductMutation: true,
                WorkspaceToolProfile: null,
                WorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
                AllowedOperations: ["MutateProductTarget", "WriteManagedProcessArtifacts"]);
            var runtimeOptions = new AgentRuntimeExecutionOptions(
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult,
                FinalizerMode: AgentFinalizerMode.Required,
                RequireStructuredOutputValidation: true,
                MaxStructuredOutputRepairAttempts: 1,
                ContextIntent: contextIntent);
            var successfulWriteTrace = new AgentToolInvocationTrace(
                ToolContractCatalog.WorkspaceWriteFile,
                ToolInvocationClassification.Mutation,
                Sequence: 1,
                StartedAtUtc: DateTimeOffset.UtcNow,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                Succeeded: true,
                FailureMessage: string.Empty)
            {
                TargetPath = artifactRelativePath
            };
            var progressMessages = new List<string>();

            var response = await executor.TryCreateFinalizerResponseFromRecoveryPoliciesAsync(
                CreateProvider(),
                "unit-test-model",
                new NeverStreamedAgent(),
                new NeverStreamedAgent.NeverUsedSession(),
                runtimeSessionKey: "session-key",
                runtimeOptions,
                policy,
                updates: [],
                ProviderUsageSourcePhases.FinalizerRecovery,
                AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
                (_, _, message) =>
                {
                    progressMessages.Add(message);
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                () => [successfulWriteTrace]);

            Assert.NotNull(response);
            var outcome = JsonSerializer.Deserialize<ProcessStepOutcomeResult>(
                response!.ResponseText,
                AgentOutputJson.SerializerOptions);
            Assert.NotNull(outcome);
            Assert.Equal(ProcessStepOutcomeStatus.Completed, outcome!.Status);
            Assert.Equal([artifactRelativePath], outcome.EvidenceRefs);
            var finalizerInvocation = Assert.Single(response.FinalizerInvocations);
            Assert.Equal(policy.ToolName, finalizerInvocation.ToolName);
            Assert.Contains(response.ToolInvocationTraces, trace => trace.ToolName == policy.ToolName && trace.Succeeded);
            Assert.Contains(progressMessages, message => message.Contains(artifactRelativePath, StringComparison.Ordinal));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task Recovery_coordinator_returns_generic_failure_when_no_policies_are_registered()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("streaming-turn-executor-recovery-none");
        try
        {
            var executor = new MafStreamingTurnExecutor(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                new NeverInvokedProviderAgentFactory(),
                new MafApprovalContinuationDriver(),
                new MafRuntimeSessionPersistenceDriver(),
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                executionOutcomeRecoveryPolicies: []);
            AgentFinalizerPolicies.TryResolveForStructuredOutput(
                AgentStructuredOutputContracts.ProcessStepOutcomeResult,
                out var policy);
            var runId = Guid.NewGuid();
            var contextIntent = new AgentRuntimeContextIntent(
                SourceKind: "process-step",
                SourceId: "code-change",
                ProcessRunId: runId.ToString("D"),
                ProcessStepId: Guid.NewGuid().ToString("D"),
                TargetScope: "ExternalProductTargetMutable",
                IsGovernedProcessStep: true,
                BrowserToolsAllowed: false,
                AllowsProductMutation: true,
                WorkspaceToolProfile: null,
                WorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
                AllowedOperations: ["MutateProductTarget", "WriteManagedProcessArtifacts"]);
            var runtimeOptions = new AgentRuntimeExecutionOptions(
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult,
                FinalizerMode: AgentFinalizerMode.Required,
                RequireStructuredOutputValidation: true,
                MaxStructuredOutputRepairAttempts: 1,
                ContextIntent: contextIntent);

            var response = await executor.TryCreateFinalizerResponseFromRecoveryPoliciesAsync(
                CreateProvider(),
                "unit-test-model",
                new NeverStreamedAgent(),
                new NeverStreamedAgent.NeverUsedSession(),
                runtimeSessionKey: "session-key",
                runtimeOptions,
                policy,
                updates: [],
                ProviderUsageSourcePhases.FinalizerRecovery,
                AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer,
                (_, _, _) => Task.CompletedTask,
                CancellationToken.None,
                () => []);

            Assert.Null(response);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-5.4-mini",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    /// <summary>Never invoked: <c>TryCreateFinalizerResponseFromRecoveryPoliciesAsync</c> does not stream.</summary>
    private sealed class NeverInvokedProviderAgentFactory : IMafProviderAgentFactory
    {
        public AIAgent CreateFrameworkAgent(
            ProviderProfile provider,
            string model,
            ChatClientAgentOptions options,
            bool frameworkManagedHistory,
            bool allowBackgroundResponses)
            => throw new NotSupportedException(
                "The recovery coordinator does not stream and must never create a provider-backed agent.");
    }

    /// <summary>
    /// A minimal <see cref="AIAgent"/>/<see cref="AgentSession"/> pair. The recovery coordinator never streams and,
    /// for a governed process step with no pending approvals, session serialization is skipped entirely (see
    /// <c>MafRuntimeSessionPersistenceDriver.ShouldSkipRuntimeSessionSerialization</c>), so neither type needs a
    /// working implementation beyond existing.
    /// </summary>
    private sealed class NeverStreamedAgent : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Session serialization must be skipped for a governed process step recovery.");

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public sealed class NeverUsedSession : AgentSession;
    }
}
