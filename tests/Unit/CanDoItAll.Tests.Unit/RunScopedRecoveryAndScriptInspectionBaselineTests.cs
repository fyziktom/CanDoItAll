using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// Failing-first characterization: finalizer recovery evidence and script
/// policy inspection must read through the workspace scope that the run
/// actually executed with, not the scope the long-lived runtime was
/// constructed for. Both tests place the artifact at the effective run-scope
/// location and are expected to fail until recovery readers and script
/// inspection bind to the run-owned workspace bundle.
/// </summary>
public sealed class RunScopedRecoveryAndScriptInspectionBaselineTests
{
    [Fact]
    public async Task Finalizer_recovery_reads_artifacts_through_the_effective_run_scope()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("run-scoped-recovery-baseline");
        try
        {
            var runId = Guid.NewGuid();
            const string sourceId = "code-change";
            var artifactRelativePath = $"artifacts/process-runs/{runId:D}/steps/{sourceId}.md";
            // The artifact exists at the effective sandbox run-scope location.
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

            // The long-lived runtime was constructed for the organization scope;
            // the run itself executes with the sandbox scope carried by its
            // context intent.
            var executor = new MafStreamingTurnExecutor(
                workspaceRoot,
                WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N")),
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
                () => [successfulWriteTrace]);

            // Recovery must find the artifact at the effective run-scope
            // location instead of probing the runtime construction scope.
            Assert.NotNull(response);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Script_policy_inspection_reads_scripts_through_the_effective_run_scope()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("run-scoped-script-inspection-baseline");
        try
        {
            const string scriptRelativePath = "artifacts/scripts/run.ps1";
            const string scriptContent = "Write-Output 'workspace script'";
            // The script exists at the effective sandbox run-scope location.
            var scriptAbsolutePath = Path.Combine(workspaceRoot, "artifacts", "scripts", "run.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptAbsolutePath)!);
            File.WriteAllText(scriptAbsolutePath, scriptContent);

            // The runtime factory now constructs the inspection service per
            // run from the effective run scope (the bundle guard script fails
            // the build if the construction-scope field returns). This asserts
            // the run-scoped service reads exactly the script the run's tools
            // can execute.
            var inspectionService = new MafScriptPolicyInspectionService(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                new ExternalTargetPathRegistryFactory().Create([]));
            var auditScope = CreateSandboxRunAuditScope();

            var inspection = inspectionService.ResolveScriptContentInspectionForPolicy(
                AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
                [new KeyValuePair<string, object?>("path", scriptRelativePath)],
                auditScope,
                scriptSideEffectManifestJson: string.Empty);

            Assert.Equal(string.Empty, inspection.FailureMessage);
            Assert.Equal(scriptContent, inspection.Content);

            // Negative: a service bound to a different scope must not resolve
            // the run-scope script — proving the scope binding is load-bearing
            // and the per-run construction is required.
            var foreignScopeService = new MafScriptPolicyInspectionService(
                workspaceRoot,
                WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N")),
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                new ExternalTargetPathRegistryFactory().Create([]));
            var foreignInspection = foreignScopeService.ResolveScriptContentInspectionForPolicy(
                AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
                [new KeyValuePair<string, object?>("path", scriptRelativePath)],
                auditScope,
                scriptSideEffectManifestJson: string.Empty);
            Assert.NotEqual(string.Empty, foreignInspection.FailureMessage);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Script_policy_inspection_resolves_authorized_versioned_external_target_alias()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("external-script-inspection-workspace");
        var externalRoot = TestFileSystem.CreateTemporaryRoot("external-script-inspection-target");
        try
        {
            var scriptPath = Path.Combine(externalRoot, "scripts", "run.ps1");
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            File.WriteAllText(scriptPath, "Write-Output 'external script'");

            var registry = new ExternalTargetPathRegistry();
            Assert.True(registry.TryCreateAlias(externalRoot, out var rootAlias));
            Assert.True(registry.TryCreateAlias(scriptPath, out var scriptAlias));
            var inspectionService = new MafScriptPolicyInspectionService(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                registry);

            var inspection = inspectionService.ResolveScriptContentInspectionForPolicy(
                AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
                [new KeyValuePair<string, object?>("path", scriptAlias)],
                CreateSandboxRunAuditScope([rootAlias]),
                scriptSideEffectManifestJson: string.Empty);

            Assert.Equal(string.Empty, inspection.FailureMessage);
            Assert.Equal("Write-Output 'external script'", inspection.Content);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(externalRoot);
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Script_policy_inspection_rejects_foreign_host_absolute_syntax()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("foreign-script-inspection");
        try
        {
            var foreignPath = OperatingSystem.IsWindows()
                ? "/tmp/foreign-script.ps1"
                : @"C:\foreign-script.ps1";
            var inspectionService = new MafScriptPolicyInspectionService(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                TestWorkspaceServices.PhysicalPathPolicyFactory,
                new ExternalTargetPathRegistryFactory().Create([]));

            var inspection = inspectionService.ResolveScriptContentInspectionForPolicy(
                AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
                [new KeyValuePair<string, object?>("path", foreignPath)],
                CreateSandboxRunAuditScope(),
                scriptSideEffectManifestJson: string.Empty);

            Assert.Contains("not valid on this host", inspection.FailureMessage, StringComparison.Ordinal);
            Assert.Equal(string.Empty, inspection.Content);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState CreateSandboxRunAuditScope(
        IReadOnlyList<string>? allowedExternalTargetAliases = null)
        => new(
            ExecutionRunId: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            CorrelationId: string.Empty,
            SourceKind: "process-step",
            SourceId: "code-change",
            ProcessRunId: Guid.NewGuid().ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"),
            SchedulerRunId: string.Empty,
            MessageId: string.Empty,
            ProviderName: "OpenAI",
            Model: "unit-test-model",
            ExternalTargetRootBindings: [],
            AllowedExternalTargetAliases: allowedExternalTargetAliases ?? [],
            ReadOnlyExternalTargetAliases: [],
            AllowedManagedArtifactReadRefs: [],
            ProcessCooperationMode: null,
            WorkspaceToolProfileOverride: null,
            ProcessBrowserToolsAllowed: false,
            ProcessAllowsProductMutation: true,
            ProcessRequiresProductMutationBeforeManagedOutput: false,
            ProcessProductMutationToolNames: [],
            ProcessProductMutationRequiredBranchOutcomeKeys: [],
            ProcessStepAllowedOperations: [],
            ProcessStepTargetScope: string.Empty,
            ContextWorkspaceScope: WorkspaceScopeDescriptor.Sandbox,
            ProjectStructureLaunchAgent: null,
            ProjectStructureProcessNodeContext: null);

    private static ProviderProfile CreateProvider()
        => new(
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

    private sealed class NeverInvokedProviderAgentFactory : IMafProviderAgentFactory
    {
        public Microsoft.Agents.AI.AIAgent CreateFrameworkAgent(
            ProviderProfile provider,
            string model,
            Microsoft.Agents.AI.ChatClientAgentOptions options,
            bool frameworkManagedHistory,
            bool allowBackgroundResponses)
            => throw new NotSupportedException(
                "The recovery coordinator does not stream and must never create a provider-backed agent.");
    }

    private sealed class NeverStreamedAgent : Microsoft.Agents.AI.AIAgent
    {
        protected override ValueTask<Microsoft.Agents.AI.AgentSession> CreateSessionCoreAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            Microsoft.Agents.AI.AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Session serialization must be skipped for a governed process step recovery.");

        protected override ValueTask<Microsoft.Agents.AI.AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override Task<Microsoft.Agents.AI.AgentResponse> RunCoreAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Agents.AI.AgentSession? session = null,
            Microsoft.Agents.AI.AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override IAsyncEnumerable<Microsoft.Agents.AI.AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Agents.AI.AgentSession? session = null,
            Microsoft.Agents.AI.AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public sealed class NeverUsedSession : Microsoft.Agents.AI.AgentSession;
    }
}
