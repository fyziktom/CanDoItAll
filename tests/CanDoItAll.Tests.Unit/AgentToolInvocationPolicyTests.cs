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
    public async Task EvaluateAsync_allows_known_framework_skill_loader_as_read_tool()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.LoadSkill,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.LoadSkill),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationClassification.Read, context.Classification);
        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(AgentToolInvocationPolicyMetadata.LoadSkill));
    }

    [Fact]
    public async Task EvaluateAsync_allows_known_framework_skill_resource_reader_as_read_tool()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.ReadSkillResource,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ReadSkillResource),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationClassification.Read, context.Classification);
        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(AgentToolInvocationPolicyMetadata.ReadSkillResource));
    }

    [Fact]
    public async Task EvaluateAsync_treats_known_framework_skill_script_runner_as_mutation_tool()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.RunSkillScript,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.RunSkillScript),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: true,
            approvalWrapperEffectiveForProvider: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationClassification.Mutation, context.Classification);
        Assert.Equal(ToolInvocationDecisionKind.RequireApproval, decision.Kind);
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(AgentToolInvocationPolicyMetadata.RunSkillScript));
        Assert.True(AgentToolInvocationPolicyMetadata.IsMutationTool(AgentToolInvocationPolicyMetadata.RunSkillScript));
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
    public async Task EvaluateAsync_denies_known_tool_when_policy_classification_is_missing()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "processes_unregistered_mutation",
            ToolInvocationClassification.Unknown,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("no registered invocation policy classification", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_ungrounded_external_target_path_for_governed_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "external-target/C/programovani/csharp/LegacyWeatherLog/scope_boundary_packet.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("current run", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Current-run product root is 'external-target/C/programovani/dotnet/PocketMeetingCostPlanner'", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("Abandon the denied external-target path", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_grounded_external_target_child_path_for_governed_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner/src/App.razor"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_shallow_shared_scope_artifact_for_governed_external_target_run()
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
                ["path"] = "artifacts/scopes/organization/demo/project-structure-context-brief.md",
                ["content"] = "Current run context"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("shallow shared scope artifact", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifacts/process-runs/process-run-001", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_current_run_artifact_for_governed_external_target_run()
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
                ["path"] = "artifacts/process-runs/process-run-001/01-project-structure-context-brief.md",
                ["content"] = "Current run context"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_dotnet_new_parent_when_parent_and_name_resolve_to_grounded_external_target()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet",
                ["name"] = "PocketMeetingCostPlanner",
                ["template"] = "blazor"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_dotnet_new_parent_with_duplicate_slashes_when_scaffold_root_is_grounded()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: true,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani//dotnet/",
                ["name"] = "PocketMeetingCostPlanner",
                ["template"] = "blazor"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_normalizes_trailing_punctuation_in_allowed_external_target_alias()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner/src/PocketMeetingCostPlanner.Domain/PocketMeetingCostPlanner.Domain.csproj"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner)"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_dotnet_new_support_library_under_grounded_external_target_root()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner/src",
                ["name"] = "PocketMeetingCostPlanner.Domain",
                ["template"] = "classlib"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner]"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_dotnet_new_template_switch_for_successful_same_scaffold_root()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var firstContext = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner/tests",
                ["name"] = "PocketMeetingCostPlanner.Tests",
                ["template"] = "mstest"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);
        var secondContext = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner/tests",
                ["name"] = "PocketMeetingCostPlanner.Tests",
                ["template"] = "xunit"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var firstDecision = await policy.EvaluateAsync(firstContext, CancellationToken.None);
        policy.RecordSuccessfulInvocation(firstContext);
        var secondDecision = await policy.EvaluateAsync(secondContext, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, firstDecision.Kind);
        Assert.Equal(ToolInvocationDecisionKind.Deny, secondDecision.Kind);
        Assert.Contains("already scaffolded", secondDecision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_dotnet_new_template_switch_when_previous_attempt_was_not_successful()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var firstContext = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner",
                ["name"] = "PocketMeetingCostPlanner.Tests",
                ["template"] = "unsupported-template"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);
        var secondContext = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner",
                ["name"] = "PocketMeetingCostPlanner.Tests",
                ["template"] = "xunit"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var firstDecision = await policy.EvaluateAsync(firstContext, CancellationToken.None);
        var secondDecision = await policy.EvaluateAsync(secondContext, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, firstDecision.Kind);
        Assert.Equal(ToolInvocationDecisionKind.Allow, secondDecision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_dotnet_new_parent_when_scaffold_root_is_not_grounded_external_target()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet",
                ["name"] = "OtherProject",
                ["template"] = "blazor"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("current run", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_dotnet_new_support_library_sibling_beside_grounded_external_target_root()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet",
                ["name"] = "PocketMeetingCostPlanner.Domain",
                ["template"] = "classlib"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Denied 'external-target/C/programovani/dotnet'", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("parentDirectory under the grounded product root", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/PocketMeetingCostPlanner/src", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_dotnet_new_test_project_sibling_with_corrected_nested_guidance()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = "mstest",
                ["parentDirectory"] = "external-target/C/programovani/dotnet",
                ["name"] = "LegacyWeatherLog.Tests"
            },
            allowedExternalTargetAliases:
            [
                "external-target/C/programovani/dotnet/LegacyWeatherLog"
            ]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Denied 'external-target/C/programovani/dotnet'", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/LegacyWeatherLog/tests", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("not the product parent", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_markdown_artifact_content_that_mentions_external_target_display_text()
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
                ["path"] = "artifacts/implementation-change-set/summary.md",
                ["content"] = "The requested product root is external-target/C/programovani/dotnet/Bike Commute Weather Scout."
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/BikeCommuteWeatherScout"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_broad_managed_workspace_listing_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_list_files",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Broad managed-workspace root discovery is denied", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_helper_root_read_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "tools/launch_app.ps1"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("stale source or helper files", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_current_run_artifact_read_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-run-001/scope.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_test_project_scaffold_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "tests/PocketMeetingCostPlanner.Tests",
                ["name"] = "PocketMeetingCostPlanner.Tests",
                ["template"] = "xunit"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Managed workspace path", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_source_write_for_external_target_process_run()
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
                ["path"] = "src/PocketMeetingCostPlanner/App.cs",
                ["content"] = "namespace PocketMeetingCostPlanner;"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Managed workspace path", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_external_target_test_project_scaffold_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner/tests",
                ["name"] = "PocketMeetingCostPlanner.Tests",
                ["template"] = "xunit"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
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
    public async Task EvaluateAsync_denies_product_file_mutation_when_external_target_is_read_only()
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
                ["path"] = "external-target/C/work/apps/Inventory/Program.cs"
            },
            allowedExternalTargetAliases: ["external-target/C/work/apps/Inventory"],
            readOnlyExternalTargetAliases: ["external-target/C/work/apps/Inventory"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("read-only access to product target", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_validation_when_external_target_is_read_only()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_run",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetPath"] = "external-target/C/work/apps/Inventory"
            },
            allowedExternalTargetAliases: ["external-target/C/work/apps/Inventory"],
            readOnlyExternalTargetAliases: ["external-target/C/work/apps/Inventory"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
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
    [InlineData(AgentToolInvocationPolicyMetadata.LoadSkill, ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.ReadSkillResource, ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.RunSkillScript, ToolInvocationClassification.Mutation)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProcessesTemplateImport, ToolInvocationClassification.Mutation)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList, ToolInvocationClassification.Read)]
    [InlineData("processes_unregistered_mutation", ToolInvocationClassification.Unknown)]
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
            AgentToolInvocationPolicyMetadata.ProcessesDefinitionRoleAdd,
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
        IReadOnlyDictionary<string, string>? arguments = null,
        IReadOnlyList<string>? allowedExternalTargetAliases = null,
        IReadOnlyList<string>? readOnlyExternalTargetAliases = null)
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
            AllowedExternalTargetAliases: allowedExternalTargetAliases,
            ReadOnlyExternalTargetAliases: readOnlyExternalTargetAliases,
            ApprovalWrapperEffectiveForProvider: approvalWrapperEffectiveForProvider,
            ApplicationApprovalAvailable: applicationApprovalAvailable);
    }
}
