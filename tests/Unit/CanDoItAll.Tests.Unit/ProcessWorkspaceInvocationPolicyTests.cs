using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProcessWorkspaceInvocationPolicyTests {
    private const string ProductRoot = "external-target/C/current-product";

    [Theory]
    [InlineData(ToolInvocationScopePolicyPhase.PathArguments)]
    [InlineData(ToolInvocationScopePolicyPhase.Contract)]
    [InlineData(ToolInvocationScopePolicyPhase.BeforeExternalTargetBoundary)]
    [InlineData(ToolInvocationScopePolicyPhase.AfterExternalTargetBoundary)]
    [InlineData(ToolInvocationScopePolicyPhase.AfterReadOnlyTargetBoundary)]
    public async Task Owner_cannot_allow_at_any_phase_before_remaining_authorization(ToolInvocationScopePolicyPhase phase) {
        var owner = new AttemptedAllowance(phase);
        var context = CreateContext(ToolContractCatalog.WorkspaceWriteFile, []) with {
            SourceKind = "chat-session",
            ProcessRunId = string.Empty,
            ProcessStepId = string.Empty,
            ScopePolicy = owner,
            AutoApprovalAllowed = false,
            ApprovalWrapperAvailable = false
        };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None).AsTask());

        Assert.Contains("may only deny or defer", failure.Message, StringComparison.Ordinal);
        Assert.Equal(phase, owner.Seen[^1]);
        Assert.Equal((int)phase + 1, owner.Seen.Count);
    }

    [Fact]
    public async Task Ungrounded_alias_is_denied_before_archived_material_guidance() {
        var context = CreateContext(ToolContractCatalog.WorkspaceReadFile,
            [new("path", "external-target/C/another-product/archive/readme.md")]);

        var result = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, result.Kind);
        Assert.Contains("outside the current run boundary", result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot use archived", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_only_target_denial_precedes_force_retry_guidance() {
        var context = CreateContext(ToolContractCatalog.WorkspaceDotNetNew,
            [new("parentDirectory", ProductRoot), new("name", "application"), new("force", "true")]) with {
            AllowedExternalTargetAliases = [],
            ReadOnlyExternalTargetAliases = [ProductRoot]
        };

        var result = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, result.Kind);
        Assert.Contains("has read-only access to product target", result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("force=true", result.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ToolContractCatalog.BrowserSnapshot, "depth", "5", "Governed process browser snapshots must set depth to 4 or less. Retry once with depth=2 and do not repeat this blocked call.")]
    [InlineData(ToolContractCatalog.BrowserTakeScreenshot, "fullPage", "true", "Governed process browser screenshots must be viewport-bounded. Retry once with fullPage=false or omit fullPage, and do not repeat this blocked call.")]
    public async Task Real_owner_retains_exact_browser_bounds_with_proof_grant(string tool, string argument, string value,
        string expectedReason) {
        var context = CreateContext(tool, [new(argument, value)]);

        var result = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, result.Kind);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public async Task Copy_source_and_destination_are_checked_against_original_grounded_roots() {
        const string source = "external-target/C/previous-product/archive/program.cs";
        var context = CreateContext(ToolContractCatalog.WorkspaceCopyPath,
            [new("sourcePath", source), new("destinationPath", ProductRoot + "/program.cs")]) with {
            ReadOnlyExternalTargetAliases = ["external-target/C/previous-product"]
        };

        var result = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, result.Kind);
        Assert.Equal($"This governed product mutation step cannot copy archived, backup, or previous-run product material from '{source}' into the current product target. Use the current-run project structure and mutate the grounded product root directly.", result.Reason);
    }

    private static ToolInvocationPolicyContext CreateContext(string tool, KeyValuePair<string, string>[] arguments) {
        var values = arguments.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        return new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Process operator", tool, values,
            AgentToolPolicyCatalog.BuiltIn.Classify(tool), true, true, false,
            "execution-a", "process-step", "run-a", "step-instance-a", AllowedExternalTargetAliases: [ProductRoot],
            ProcessAllowsProductMutation: true,
            ProcessStepAllowedOperations: [ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.MutateProductTarget, ProcessOperationContractNames.CaptureRuntimeProof],
            ProcessStepTargetScope: ProcessOperationContractNames.ExternalProductTargetMutable) {
            SourceId = "step-a",
            ScopePolicy = ProcessToolInvocationScopePolicy.Instance,
            PathArguments = ToolInvocationPathArgumentResolver.Resolve(tool,
                arguments.Select(item => new KeyValuePair<string, object?>(item.Key, item.Value)))
        };
    }

    private sealed class AttemptedAllowance(ToolInvocationScopePolicyPhase target) : IToolInvocationScopePolicy {
        public List<ToolInvocationScopePolicyPhase> Seen { get; } = [];

        public ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature)
            => throw new InvalidOperationException("The typed phase entry point must be used.");

        public ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature,
            ToolInvocationScopePolicyPhase phase) {
            Seen.Add(phase);
            return phase == target ? ToolInvocationPolicyDecision.Allow(signature) : null;
        }

        public ToolInvocationWorkspaceScopeFacts GetWorkspaceFacts(ToolInvocationPolicyContext context)
            => new(false, string.Empty, false);

        public bool TryCreateRecoverableDeniedResult(string toolName, ToolInvocationPolicyDecision decision,
            ToolInvocationPolicyContext context, out string result) {
            result = string.Empty;
            return false;
        }
    }
}
