using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class WorkspaceToolResultDisclosureTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admitted_workspace_composition_preserves_every_installed_name_schema_and_approval(bool explicitApproval) {
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection().BuildServiceProvider();
        var plain = await ComposeAsync(services, explicitApproval, admitted: false);
        var admitted = await ComposeAsync(services, explicitApproval, admitted: true);
        try {
            Assert.Equal(plain.Tools.Select(tool => tool.Name), admitted.Tools.Select(tool => tool.Name));
            var original = plain.Tools.OfType<AIFunction>().Where(tool => ToolContractCatalog.WorkspaceToolNames.Contains(tool.Name))
                .OrderBy(tool => tool.Name, StringComparer.Ordinal).ToArray();
            var wrapped = admitted.Tools.OfType<AIFunction>().Where(tool => ToolContractCatalog.WorkspaceToolNames.Contains(tool.Name))
                .OrderBy(tool => tool.Name, StringComparer.Ordinal).ToArray();
            Assert.NotEmpty(original);
            Assert.Equal(original.Select(tool => tool.Name), wrapped.Select(tool => tool.Name));
            for (var index = 0; index < original.Length; index++) {
                Assert.Equal(original[index].Description, wrapped[index].Description);
                Assert.Equal(original[index].JsonSchema.GetRawText(), wrapped[index].JsonSchema.GetRawText());
                Assert.Equal(original[index].ReturnJsonSchema?.GetRawText(), wrapped[index].ReturnJsonSchema?.GetRawText());
                Assert.Equal(original[index] is ApprovalRequiredAIFunction, wrapped[index] is ApprovalRequiredAIFunction);
                var metadata = Assert.Single(admitted.RuntimeToolMetadata, item => item.ToolName == wrapped[index].Name);
                Assert.NotNull(metadata.AuthorizeResultDisclosureAsync);
                Assert.Null(metadata.PrepareAdmission);
                Assert.Null(metadata.AuthorizeAdmissionAsync);
                Assert.Equal(original[index] is ApprovalRequiredAIFunction, metadata.RequiresApprovalByDefault);
                Assert.Equal(AgentToolPolicyCatalog.BuiltIn.Classify(original[index].Name), (ToolInvocationClassification)metadata.OperationKind);
                Assert.Equal(original[index].Name is ToolContractCatalog.WorkspaceAnalyzeImage or ToolContractCatalog.WorkspaceAnalyzeImages
                    ? AgentRuntimeToolRecoveryPolicy.ReconcileBeforeRetry : AgentRuntimeToolRecoveryPolicy.Default, metadata.RecoveryPolicy);
            }
            Assert.Equal(wrapped.Length, admitted.RuntimeToolMetadata.Count(item => ToolContractCatalog.WorkspaceToolNames.Contains(item.ToolName)));
        } finally {
            Assert.Empty(await plain.DisposeAcquiredResourcesAsync());
            Assert.Empty(await admitted.DisposeAcquiredResourcesAsync());
        }
    }

    [Fact]
    public async Task Missing_host_source_boundary_records_a_trusted_denial_before_an_admitted_workspace_read() {
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection().BuildServiceProvider();
        var state = await ComposeAsync(services, explicitApproval: false, admitted: true);
        try {
            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(state.Tools, item => item.Name == ToolContractCatalog.WorkspaceReadFile));
            using var effect = AgentToolInvocationEffectScope.Begin();
            var denied = Assert.IsType<AgentToolFailureResult>(await tool.InvokeAsync(new AIFunctionArguments { ["path"] = "not-read.txt" }));
            Assert.Equal("workspace.result-authority-unavailable", denied.ErrorCode);
            Assert.Equal(AgentToolEffectState.NotCommitted, denied.EffectState);
            Assert.Equal(denied.ErrorCode, effect.PreDispatchFailure!.FailureCode);
            Assert.Null(effect.CommittedEffect);
            Assert.Null(effect.DisclosureEvidence);
        } finally {
            Assert.Empty(await state.DisposeAcquiredResourcesAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unexpected_or_cancelled_capture_is_never_a_trusted_pre_dispatch_denial(bool cancelled) {
        var failure = cancelled ? (Exception)new OperationCanceledException("capture cancelled") : new IOException("capture unavailable");
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection()
            .AddSingleton<IAgentWorkspaceToolResultSource>(new FailureSource(failure, duringCompletion: false)).BuildServiceProvider();
        var state = await ComposeAsync(services, explicitApproval: false, admitted: true);
        try {
            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(state.Tools, item => item.Name == ToolContractCatalog.WorkspaceReadFile));
            using var effect = AgentToolInvocationEffectScope.Begin();
            var actual = await Assert.ThrowsAnyAsync<Exception>(async () => await tool.InvokeAsync(new AIFunctionArguments { ["path"] = "source.txt" }));
            Assert.Same(failure, actual);
            Assert.Null(effect.PreDispatchFailure);
            Assert.Null(effect.DisclosureEvidence);
        } finally {
            Assert.Empty(await state.DisposeAcquiredResourcesAsync());
        }
    }

    [Fact]
    public async Task A_post_invocation_capture_failure_never_claims_the_tool_was_not_dispatched() {
        var failure = new AgentToolAdmissionException("fixture.post-invocation", "Original post-invocation failure.");
        var source = new FailureSource(failure, duringCompletion: true);
        using var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection()
            .AddSingleton<IAgentWorkspaceToolResultSource>(source).BuildServiceProvider();
        var root = TestFileSystem.CreateTemporaryRoot("workspace-disclosure-after-invocation");
        RuntimeCapabilityState? state = null;
        try {
            state = await ComposeAsync(services, explicitApproval: false, admitted: true, workspaceRoot: root);
            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(state.Tools, item => item.Name == ToolContractCatalog.WorkspaceReadFile));
            using var effect = AgentToolInvocationEffectScope.Begin();
            var actual = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await tool.InvokeAsync(
                new AIFunctionArguments { ["path"] = "absent-reviewed-source.txt" }));
            Assert.Same(failure, actual);
            Assert.Equal(1, source.Completions);
            Assert.Null(effect.PreDispatchFailure);
            Assert.Null(effect.DisclosureEvidence);
        } finally {
            if (state is not null) {
                Assert.Empty(await state.DisposeAcquiredResourcesAsync());
            }
            TestFileSystem.DeleteDirectoryWithRetry(root);
        }
    }

    [Theory]
    [InlineData("source.txt", true)]
    [InlineData("data/source.txt", true)]
    [InlineData("data/scopes/project/original/source.txt", true)]
    [InlineData("data/scopes/project/replacement/source.txt", false)]
    [InlineData("output/scopes/organization/another/source.txt", false)]
    public void Cached_paths_never_infer_an_uncaptured_scope_from_a_result(string path, bool expected) {
        Assert.Equal(expected, WorkspaceToolResultDisclosure.IsWithinCapturedScope(path, WorkspaceScopeDescriptor.Project("original")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execution_scope_is_available_only_from_the_same_active_run_and_actor(bool changeActor) {
        var now = DateTimeOffset.UtcNow;
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), null, "Audited scope", "fixture", "original",
            string.Empty, string.Empty, "fixture", "system", "{}", string.Empty, string.Empty, "fixture", "fixture",
            ExecutionState.Running, null, now, now, now, null, string.Empty, null, []);
        var contextScope = WorkspaceScopeDescriptor.Project("original");
        var executionScope = WorkspaceScopeDescriptor.Organization("owning-workspace");
        Assert.Null(WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(run.Id, run.AgentId, contextScope));
        using (WorkspaceExecutionAuditContext.BeginScope(run, contextScope, executionScope)) {
            Assert.Equal(executionScope, WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(run.Id, run.AgentId, contextScope));
            var denied = Assert.Throws<AgentToolAdmissionException>(() => WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(
                changeActor ? run.Id : Guid.NewGuid(), changeActor ? Guid.NewGuid() : run.AgentId, contextScope));
            Assert.Equal("workspace.execution-scope-mismatch", denied.Code);
            Assert.Throws<AgentToolAdmissionException>(() => WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(
                run.Id, run.AgentId, WorkspaceScopeDescriptor.Project("another-context")));
        }
        Assert.Null(WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(run.Id, run.AgentId, contextScope));
    }

    [Fact]
    public async Task SDK_serialized_receipt_outputs_are_included_in_the_original_read_boundary() {
        var receipt = new WorkspaceToolReceipt("workspace_list_files", false, "workspace", "Succeeded", "listed",
            "artifacts/receipt.json", ["data/source"], [new("artifacts", "artifacts/preview.txt", "Preview", "text/plain", string.Empty)],
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) { DeclaredSideEffectMode = ToolExecutionSideEffectMode.NoMutation };
        var result = new WorkspaceTextFileReadResult(true, "read", receipt, "data/source/item.txt", "original", 8, false);
        var function = AIFunctionFactory.Create(() => result, ToolContractCatalog.WorkspaceReadFile);
        var serialized = Assert.IsType<JsonElement>(await function.InvokeAsync(new()));
        Assert.Equal(JsonValueKind.String, serialized.GetProperty("receipt").GetProperty("declaredSideEffectMode").ValueKind);
        var originalJson = serialized.GetRawText();
        var paths = WorkspaceToolResultDisclosure.ReadResultPaths(ToolContractCatalog.WorkspaceReadFile,
            serialized, function.JsonSerializerOptions);
        Assert.Equal(new[] { "data/source", "artifacts/preview.txt", "artifacts/receipt.json" }, paths);
        Assert.Equal(originalJson, serialized.GetRawText());
    }

    [Theory]
    [InlineData("{\"receipt\":{\"declaredSideEffectMode\":\"unsupported-future-mode\"}}")]
    [InlineData("{\"receipt\":{\"targetPaths\":17}}")]
    public void SDK_receipts_with_unknown_modes_or_malformed_paths_are_not_reconstructed(string payload) {
        var function = AIFunctionFactory.Create(() => payload, ToolContractCatalog.WorkspaceReadFile);
        using var document = JsonDocument.Parse(payload);
        Assert.Throws<JsonException>(() => WorkspaceToolResultDisclosure.ReadResultPaths(
            ToolContractCatalog.WorkspaceReadFile, document.RootElement, function.JsonSerializerOptions));
        Assert.Equal(payload, document.RootElement.GetRawText());
    }

    [Theory]
    [InlineData("unsupported-format", 1)]
    [InlineData("configured-workspace-result-authority", 2)]
    public void Unsupported_evidence_is_rejected_explicitly(string format, int version) {
        Assert.Throws<InvalidDataException>(() => WorkspaceToolResultEvidence.Read(AgentToolProtocolEnvelope.Create(format, version, "{}")));
    }

    [Fact]
    public void Maximum_supported_path_evidence_remains_below_the_protocol_byte_limit() {
        var root = Path.GetTempPath();
        var fullPath = Path.Combine(root, new string('é', WorkspaceToolResultEvidence.MaximumPathCharacters - root.Length));
        var path = new WorkspaceToolResultPath(new string('é', WorkspaceToolResultEvidence.MaximumPathCharacters),
            new string('é', WorkspaceToolResultEvidence.MaximumPathCharacters), fullPath, true);
        var evidence = new WorkspaceToolResultEvidence(ToolContractCatalog.WorkspaceReadFile, root, WorkspaceScopeDescriptor.Sandbox,
            AgentToolProtocolEnvelope.Create("owned-source-fixture", 1, "{}"), WorkspaceToolResultEvidenceState.Complete,
            Enumerable.Repeat(path, WorkspaceToolResultEvidence.MaximumPaths).ToImmutableArray());
        var envelope = WorkspaceToolResultEvidence.Write(evidence);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(envelope.PayloadJson) < AgentToolProtocolEnvelope.MaximumUtf8Bytes);
        Assert.Equal(WorkspaceToolResultEvidence.MaximumPaths, WorkspaceToolResultEvidence.Read(envelope).Paths.Length);
        Assert.Throws<InvalidDataException>(() => WorkspaceToolResultEvidence.Write(evidence with {
            Paths = [path with { RequestPath = path.RequestPath + "x" }]
        }));
    }

    private static Task<RuntimeCapabilityState> ComposeAsync(IServiceProvider services, bool explicitApproval, bool admitted, string? workspaceRoot = null) {
        var root = workspaceRoot ?? Path.GetTempPath();
        var seed = SandboxWorkspaceSeedFactory.Create().ToCatalog();
        var actor = seed.Agents.First(item => item.ProviderProfileId.HasValue);
        var provider = seed.Providers.Single(item => item.Id == actor.ProviderProfileId);
        CapabilityCatalogItem[] capabilities = explicitApproval ? [new(Guid.NewGuid(), CapabilityKind.Tool, "workspace-plugin",
            "Workspace", string.Empty, string.Empty, "{\"tool\":\"workspace_plugin\",\"approvalRequired\":true}",
            CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, true)] : [];
        actor = actor with {
            Permissions = actor.Permissions with { CanUseTools = true },
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}", new() {
                Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true, CanWriteFiles = true,
                CanManageWorkspacePaths = true, CanRunValidationCommands = true, CanScaffoldProjects = true,
                CanRunLocalScripts = true, CanTransformArtifacts = true
            }),
            Capabilities = capabilities.Select(item => new AgentCapabilityAssignment(item.Id, item.Key, item.Kind,
                item.ProofStatus, item.LastVerifiedAtUtc, item.ProofNotes)).ToImmutableArray()
        };
        return RuntimeCapabilityComposer.CreateDefault(root, services).CreateCapabilityStateCoreAsync(actor, provider,
            provider.DefaultModel, capabilities, [], (_, _, _) => Task.CompletedTask, default, false, WorkspaceScopeDescriptor.Sandbox,
            AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat, WorkspaceToolsEnabled = true,
                ToolCapabilitiesEnabled = true, RuntimeToolProvidersEnabled = false }, WorkspaceRuntimeServicesTestFactory.Create(root),
            admittedToolSession: admitted ? new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())) : null);
    }

    private sealed class FailureSource(Exception failure, bool duringCompletion) : IAgentWorkspaceToolResultSource {
        internal int Completions { get; private set; }
        public ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context, WorkspaceScopeDescriptor workspaceScope,
            CancellationToken cancellationToken = default)
            => duringCompletion ? ValueTask.FromResult(AgentToolProtocolEnvelope.Create("fixture-source", 1, "{}"))
                : ValueTask.FromException<AgentToolProtocolEnvelope>(failure);
        public ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default) {
            Completions++;
            return ValueTask.FromException<AgentToolProtocolEnvelope>(failure);
        }
        public ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context,
            WorkspaceScopeDescriptor workspaceScope, AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This capture-failure fixture never authorizes disclosure.");
    }
}
