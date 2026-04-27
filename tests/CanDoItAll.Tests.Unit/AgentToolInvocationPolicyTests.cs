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
            approvalWrapperAvailable: true,
            approvalWrapperEffectiveForProvider: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, decision.Kind);
        Assert.True(context.HasEffectiveApprovalPath);
        Assert.Contains("approval path", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_requires_approval_but_marks_missing_effective_path()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, decision.Kind);
        Assert.False(context.HasEffectiveApprovalPath);
        Assert.Contains("no effective approval path", decision.Reason, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void AgentToolPolicyBlockedException_preserves_policy_reason_and_tool_name()
    {
        var exception = new AgentToolPolicyBlockedException(
            "workspace_write_file",
            ToolInvocationDecisionKind.RequireApproval,
            "Mutation tools require approval.");

        Assert.Equal("workspace_write_file", exception.ToolName);
        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, exception.DecisionKind);
        Assert.Equal("Mutation tools require approval.", exception.Reason);
        Assert.Contains("blocked by policy", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockGuard_throws_policy_exception_for_missing_approval_path()
    {
        var decision = ToolInvocationPolicyDecision.RequireApproval(
            "workspace_write_file|path=artifact.md",
            "Mutation tools require approval.");

        var exception = Assert.Throws<AgentToolPolicyBlockedException>(() =>
            AgentToolPolicyBlockGuard.ThrowIfBlocked(
                "workspace_write_file",
                decision,
                hasEffectiveApprovalPath: false));

        Assert.Equal("workspace_write_file", exception.ToolName);
        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, exception.DecisionKind);
    }

    [Fact]
    public void BlockGuard_does_not_reclassify_allowed_tool_exceptions()
    {
        var decision = ToolInvocationPolicyDecision.Allow("workspace_read_file|path=artifact.md");

        AgentToolPolicyBlockGuard.ThrowIfBlocked(
            "workspace_read_file",
            decision,
            hasEffectiveApprovalPath: false);

        var exception = Assert.Throws<InvalidOperationException>(ThrowToolException);

        Assert.IsNotType<AgentToolPolicyBlockedException>(exception);
        Assert.Equal("Tool implementation failed.", exception.Message);

        static void ThrowToolException()
        {
            throw new InvalidOperationException("Tool implementation failed.");
        }
    }

    [Theory]
    [InlineData("workspace_write_file", ToolInvocationClassification.Mutation)]
    [InlineData("workspace_dotnet_test", ToolInvocationClassification.Validation)]
    [InlineData("provider-native-web-search", ToolInvocationClassification.HostedProviderNative)]
    [InlineData("mcp_project_query", ToolInvocationClassification.LocalMcp)]
    [InlineData("workspace_read_file", ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProcessesTemplateImport, ToolInvocationClassification.Mutation)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList, ToolInvocationClassification.Read)]
    public void Classify_returns_expected_tool_classification(string toolName, ToolInvocationClassification expected)
    {
        var classification = AgentToolInvocationPolicyMetadata.Classify(toolName);

        Assert.Equal(expected, classification);
    }

    [Theory]
    [MemberData(nameof(ProcessMutationTools))]
    public async Task EvaluateAsync_requires_approval_for_process_mutation_tools(string toolName)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            toolName,
            AgentToolInvocationPolicyMetadata.Classify(toolName),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: true,
            approvalWrapperEffectiveForProvider: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationClassification.Mutation, context.Classification);
        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, decision.Kind);
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
        Assert.True(AgentToolInvocationPolicyMetadata.IsMutationTool(toolName));
    }

    [Theory]
    [MemberData(nameof(ProcessReadTools))]
    public async Task EvaluateAsync_allows_process_read_tools_without_approval(string toolName)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            toolName,
            AgentToolInvocationPolicyMetadata.Classify(toolName),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationClassification.Read, context.Classification);
        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
        Assert.False(AgentToolInvocationPolicyMetadata.IsMutationTool(toolName));
    }

    public static TheoryData<string> ProcessMutationTools()
    {
        return new TheoryData<string>
        {
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionSave,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionPublish,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionDelete,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionImport,
            AgentToolInvocationPolicyMetadata.ProcessesRunStart,
            AgentToolInvocationPolicyMetadata.ProcessesStepTransition,
            AgentToolInvocationPolicyMetadata.ProcessesAssignmentResolve,
            AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateImport
        };
    }

    public static TheoryData<string> ProcessReadTools()
    {
        return new TheoryData<string>
        {
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionsList,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionEditorGet,
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionExport,
            AgentToolInvocationPolicyMetadata.ProcessesRunsList,
            AgentToolInvocationPolicyMetadata.ProcessesRunDetailGet,
            AgentToolInvocationPolicyMetadata.ProcessesAnalyticsGet,
            AgentToolInvocationPolicyMetadata.ProcessesPartyOptionsList,
            AgentToolInvocationPolicyMetadata.ProcessesExecutorOptionsList,
            AgentToolInvocationPolicyMetadata.ProcessesTemplatesList,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateGet,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateMermaidGet,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList
        };
    }

    private static ToolInvocationPolicyContext CreateContext(
        string toolName,
        ToolInvocationClassification classification,
        bool isKnownTool,
        bool autoApprovalAllowed,
        bool approvalWrapperAvailable,
        bool approvalWrapperEffectiveForProvider = false,
        bool applicationApprovalAvailable = false,
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
            ProcessStepId: "step-001",
            ApprovalWrapperEffectiveForProvider: approvalWrapperEffectiveForProvider,
            ApplicationApprovalAvailable: applicationApprovalAvailable);
    }
}
