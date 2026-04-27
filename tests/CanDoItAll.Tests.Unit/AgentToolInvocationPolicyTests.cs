using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentToolInvocationPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_allows_known_read_tool()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_requires_wrapper_approval_for_mutation_without_auto_approval()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, decision.Kind);
        Assert.Contains("approval wrapper", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_mutation_when_no_auto_approval_or_wrapper_exists()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("neither auto-approval nor a framework approval wrapper", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_unknown_tool_even_when_read_like()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "unknown_tool",
            ToolInvocationClassification.Read,
            isKnownTool: false,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("not part of the composed capability set", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_fourth_identical_mutation_invocation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/result.md"
            });

        ToolInvocationPolicyDecision? decision = null;
        for (var index = 0; index < DefaultAgentToolInvocationPolicy.MaxRepeatedMutationOrValidationInvocations + 1; index++)
        {
            decision = await policy.EvaluateAsync(context, CancellationToken.None);
        }

        Assert.NotNull(decision);
        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("repeated the same mutation or validation signature", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactArguments_masks_sensitive_argument_names_before_signature_generation()
    {
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
        [
            new KeyValuePair<string, object?>("path", "artifacts/result.md"),
            new KeyValuePair<string, object?>("apiKey", "sk-secret"),
            new KeyValuePair<string, object?>("authorizationHeader", "Bearer secret")
        ]);

        var signature = AgentToolInvocationPolicyMetadata.BuildSignature("workspace_write_file", redacted);

        Assert.Equal("<redacted>", redacted["apiKey"]);
        Assert.Equal("<redacted>", redacted["authorizationHeader"]);
        Assert.DoesNotContain("sk-secret", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer secret", signature, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("workspace_write_file", ToolInvocationClassification.Mutation)]
    [InlineData("workspace_dotnet_test", ToolInvocationClassification.Validation)]
    [InlineData("provider-native-web-search", ToolInvocationClassification.HostedProviderNative)]
    [InlineData("mcp_project_query", ToolInvocationClassification.LocalMcp)]
    [InlineData("workspace_read_file", ToolInvocationClassification.Read)]
    public void Classify_returns_expected_tool_classification(string toolName, ToolInvocationClassification expected)
    {
        var classification = AgentToolInvocationPolicyMetadata.Classify(toolName);

        Assert.Equal(expected, classification);
    }

    private static ToolInvocationPolicyContext CreateContext(
        string toolName,
        ToolInvocationClassification classification,
        bool isKnownTool,
        bool autoApprovalAllowed,
        bool approvalWrapperAvailable,
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        return new ToolInvocationPolicyContext(
            AgentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AgentName: "Implementation Agent",
            ToolName: toolName,
            RedactedArguments: arguments ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Classification: classification,
            IsKnownTool: isKnownTool,
            AutoApprovalAllowed: autoApprovalAllowed,
            ApprovalWrapperAvailable: approvalWrapperAvailable,
            ExecutionRunId: "run-001",
            SourceKind: "process-step",
            ProcessRunId: "process-run-001",
            ProcessStepId: "step-001");
    }
}
