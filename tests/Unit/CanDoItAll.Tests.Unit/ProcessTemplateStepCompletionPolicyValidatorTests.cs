using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessTemplateStepCompletionPolicyValidatorTests
{
    [Fact]
    public void Disjoint_branch_scoped_receipt_rules_for_the_same_tool_are_valid()
    {
        var step = CreateBranchStep(
            ReceiptRule("build-success", "accepted", allowFailed: false),
            ReceiptRule("build-attempt", "repair-required", allowFailed: true));

        ProcessTemplateStepCompletionPolicyValidator.Validate(
            step,
            "test-definition.validation");
    }

    [Fact]
    public void Failed_execution_receipt_policy_requires_an_explicit_branch_scope()
    {
        var rule = ReceiptRule("build-attempt", "repair-required", allowFailed: true);
        rule.EnforceBranchOutcomeKeys.Clear();
        var step = CreateBranchStep(rule);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessTemplateStepCompletionPolicyValidator.Validate(
                step,
                "test-definition.validation"));

        Assert.Contains("must be scoped to explicit branch outcomes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_tool_branch_rules_require_distinct_keys()
    {
        var step = CreateBranchStep(
            ReceiptRule(string.Empty, "accepted", allowFailed: false),
            ReceiptRule(string.Empty, "repair-required", allowFailed: true));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessTemplateStepCompletionPolicyValidator.Validate(
                step,
                "test-definition.validation"));

        Assert.Contains("distinct Key values", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Same_tool_branch_rules_cannot_have_overlapping_scopes()
    {
        var step = CreateBranchStep(
            ReceiptRule("build-success", "accepted", allowFailed: false),
            ReceiptRule("build-attempt", "accepted", allowFailed: true));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessTemplateStepCompletionPolicyValidator.Validate(
                step,
                "test-definition.validation"));

        Assert.Contains("overlapping required product tool receipt rules", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Branch_steps_cannot_use_implicit_all_branch_receipt_rules()
    {
        var rule = ReceiptRule("build-success", "accepted", allowFailed: false);
        rule.EnforceBranchOutcomeKeys.Clear();
        var step = CreateBranchStep(rule);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessTemplateStepCompletionPolicyValidator.Validate(
                step,
                "test-definition.validation"));

        Assert.Contains("must enumerate every branch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_routed_completion_issue_routes_require_explicit_non_self_sources()
    {
        var step = CreateBranchStep(
            ReceiptRule("build-success", "accepted", allowFailed: false));
        step.CompletionPolicy!.RuntimeRoutedBranchOutcomeKeys = ["repair-required"];
        step.CompletionPolicy.CompletionIssueRoutes =
        [
            new ProcessTemplateCompletionIssueRouteDocument
            {
                IssueCode = "process.adapter.required_receipt_missing",
                TargetBranchOutcomeKey = "repair-required"
            }
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProcessTemplateStepCompletionPolicyValidator.Validate(
                step,
                "test-definition.validation"));

        Assert.Contains("without explicit SourceBranchOutcomeKeys", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sole_runtime_routed_branch_can_route_an_unbranched_output()
    {
        var step = new ProcessTemplateDefinitionStepDocument
        {
            Key = "implementation",
            BranchOutcomes =
            [
                new ProcessTemplateDefinitionStepBranchOutcomeDocument
                {
                    Key = "implementation-attempt-incomplete"
                }
            ],
            CompletionPolicy = new ProcessTemplateStepCompletionPolicyDocument
            {
                RuntimeRoutedBranchOutcomeKeys = ["implementation-attempt-incomplete"],
                CompletionIssueRoutes =
                [
                    new ProcessTemplateCompletionIssueRouteDocument
                    {
                        IssueCode = "process.adapter.product_mutation_receipt_missing",
                        TargetBranchOutcomeKey = "implementation-attempt-incomplete"
                    }
                ]
            }
        };

        ProcessTemplateStepCompletionPolicyValidator.Validate(
            step,
            "test-definition.implementation");
    }

    private static ProcessTemplateDefinitionStepDocument CreateBranchStep(
        params ProcessTemplateProductToolReceiptRequirementDocument[] rules)
        => new()
        {
            Key = "validation",
            BranchOutcomes =
            [
                new ProcessTemplateDefinitionStepBranchOutcomeDocument { Key = "accepted" },
                new ProcessTemplateDefinitionStepBranchOutcomeDocument { Key = "repair-required" }
            ],
            CompletionPolicy = new ProcessTemplateStepCompletionPolicyDocument
            {
                RequiredProductToolReceipts = [.. rules]
            }
        };

    private static ProcessTemplateProductToolReceiptRequirementDocument ReceiptRule(
        string key,
        string branchOutcomeKey,
        bool allowFailed)
        => new()
        {
            Key = key,
            ToolName = "workspace_dotnet_build",
            EnforceBranchOutcomeKeys = [branchOutcomeKey],
            AllowFailedExecutionReceipt = allowFailed
        };
}
