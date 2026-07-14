using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

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
        Assert.Contains("Current-run writable product root is 'external-target/C/programovani/dotnet/PocketMeetingCostPlanner'", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("Abandon the denied external-target path", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denial_guidance_prefers_writable_product_root_over_read_only_aliases()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string productRoot = "external-target/C/programovani/dotnet-demo/output/run/product";
        const string backupRoot = "external-target/C/programovani/dotnet-demo/output/run/project-structure-backup";
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "external-target/C/programovani/dotnet-demo/output/old-run/product/App.razor"
            },
            allowedExternalTargetAliases: [productRoot],
            readOnlyExternalTargetAliases: [backupRoot]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains($"Current-run writable product root is '{productRoot}'", decision.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain($"Current-run writable product root is '{backupRoot}'", decision.Reason, StringComparison.Ordinal);
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
    public async Task EvaluateAsync_denies_unscoped_managed_project_media_file_for_governed_external_target_run()
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
                ["path"] = "managed-files/project-media/files/3324868f66e2478abb8f14f32a5db1e9/office365-category-email-summary.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/calculator-output"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("outside current-run evidence folders", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grounded external-target alias", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceStatPath)]
    [InlineData(ToolContractCatalog.WorkspaceReadFile)]
    public async Task EvaluateAsync_allows_managed_project_media_file_read_with_project_structure_right_for_governed_external_target_run(
        string toolName)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            toolName,
            AgentToolInvocationPolicyMetadata.Classify(toolName),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "managed-files/project-media/files/3324868f66e2478abb8f14f32a5db1e9/source-brief.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/calculator-output"],
            processStepAllowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure
            ],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly,
            contextWorkspaceScopeKind: "Project",
            contextWorkspaceScopeKey: "3324868f-66e2-478a-bb8f-14f32a5db1e9");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_project_media_file_from_different_project_for_governed_external_target_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            ToolContractCatalog.WorkspaceReadFile,
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "managed-files/project-media/files/ee266fad590440ff9b30d96804aadcb2/office365-category-email-summary.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/calculator-output"],
            processStepAllowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure
            ],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly,
            contextWorkspaceScopeKind: "Project",
            contextWorkspaceScopeKey: "3324868f-66e2-478a-bb8f-14f32a5db1e9");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("outside current-run evidence folders", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceStatPath)]
    [InlineData(ToolContractCatalog.WorkspaceReadFile)]
    [InlineData(ToolContractCatalog.WorkspaceInspectImage)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImage)]
    public async Task EvaluateAsync_allows_managed_project_media_image_read_for_governed_external_target_run(
        string toolName)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            toolName,
            AgentToolInvocationPolicyMetadata.Classify(toolName),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "managed-files/project-media/images/3324868f66e2478abb8f14f32a5db1e9/ui-proposal.png"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/calculator-output"],
            processStepAllowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure
            ],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly,
            contextWorkspaceScopeKind: "Project",
            contextWorkspaceScopeKey: "3324868f-66e2-478a-bb8f-14f32a5db1e9");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_project_media_image_analysis_without_project_or_runtime_proof_right()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            ToolContractCatalog.WorkspaceAnalyzeImage,
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "managed-files/project-media/images/3324868f66e2478abb8f14f32a5db1e9/ui-proposal.png"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/calculator-output"],
            processStepAllowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext
            ],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains(ProcessOperationContractNames.ReadProjectStructure, decision.Reason, StringComparison.Ordinal);
        Assert.Contains(ProcessOperationContractNames.CaptureRuntimeProof, decision.Reason, StringComparison.Ordinal);
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
    public async Task EvaluateAsync_denies_reading_current_step_primary_managed_artifact_before_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{currentRunId:D}/steps/feature-intake.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "WriteManagedProcessArtifacts"
            ],
            processRunId: currentRunId.ToString("D"),
            sourceId: "feature-intake");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("cannot read", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_write_file", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"artifacts/process-runs/{currentRunId:D}/steps/feature-intake.md", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_recoverable_ellipsized_current_process_run_artifact_ref_for_guid_governed_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/2a.../steps/classify-dotnet-application.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "WriteManagedProcessArtifacts"
            ],
            processRunId: currentRunId.ToString("D"));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_ellipsized_process_run_artifact_ref_for_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/2a.../steps/classify-dotnet-application.md",
                ["content"] = "content"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "WriteManagedProcessArtifacts"
            ],
            processRunId: currentRunId.ToString("D"));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("malformed managed process-run artifact ref", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not abbreviate", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_wrong_process_run_artifact_ref_without_external_action()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var guessedRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b5c1e8d6f3");
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{guessedRunId:D}/steps/classify-dotnet-application.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "WriteManagedProcessArtifacts"
            ],
            processRunId: currentRunId.ToString("D"));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("cannot read managed artifacts for process run", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(currentRunId.ToString("D"), decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_exact_runtime_authorized_parent_artifact_read()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var parentRunId = Guid.Parse("6480215a-afe9-4251-be05-d3525208b17c");
        var parentRef = $"artifacts/process-runs/{parentRunId:D}/steps/feature-intake.md";
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = parentRef
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            allowedManagedArtifactReadRefs: [parentRef],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "WriteManagedProcessArtifacts"
            ],
            processRunId: currentRunId.ToString("D"));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Theory]
    [InlineData("workspace_read_file", ToolInvocationClassification.Read, "peer-review.md")]
    [InlineData("workspace_write_file", ToolInvocationClassification.Mutation, "feature-intake.md")]
    public async Task EvaluateAsync_does_not_expand_parent_artifact_authorization(
        string toolName,
        ToolInvocationClassification classification,
        string requestedFile)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var parentRunId = Guid.Parse("6480215a-afe9-4251-be05-d3525208b17c");
        var parentRef = $"artifacts/process-runs/{parentRunId:D}/steps/feature-intake.md";
        var context = CreateContext(
            toolName,
            classification,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{parentRunId:D}/steps/{requestedFile}",
                ["content"] = "content"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            allowedManagedArtifactReadRefs: [parentRef],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "WriteManagedProcessArtifacts"
            ],
            processRunId: currentRunId.ToString("D"));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_child_process_run_artifact_ref_for_external_action_coordinator()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("886aba26-7806-4214-bbe5-15e4c9ff57d1");
        var childRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{childRunId:D}/steps/architecture-handoff.md"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "ExecuteExternalAction"
            ],
            processStepTargetScope: "ExternalActionControlled",
            processRunId: currentRunId.ToString("D"));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_external_product_mutation_when_process_step_disallows_product_mutation()
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
                ["path"] = "external-target/C/programovani/todo-summary/product/src/Program.cs",
                ["content"] = "Console.WriteLine(\"changed\");"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("not authorized to mutate product targets", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_output_product_write_when_process_step_disallows_product_mutation()
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
                ["path"] = "output/scopes/organization/demo/product/src/Program.cs",
                ["content"] = "Console.WriteLine(\"changed\");"
            },
            processAllowsProductMutation: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("managed output product files", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_current_run_artifact_when_process_step_disallows_product_mutation()
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
                ["path"] = "artifacts/process-runs/process-run-001/architecture-decision.md",
                ["content"] = "Decision artifact."
            },
            processAllowsProductMutation: false);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_pwsh_script_product_write_when_process_step_disallows_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/apply-change.ps1"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ManagedProcessArtifacts,
                declaredWritePaths: ["artifacts/process-runs/process-run-001/evidence/product-write-attempt.txt"]),
            inspectedScriptContent:
                "Set-Content -Path 'external-target/C/programovani/todo-summary/product/src/Program.cs' -Value 'changed'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("must not use external-target aliases as literal OS paths", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/C/programovani/todo-summary/product/src/Program.cs", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_python_script_product_write_when_process_step_disallows_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/apply_change.py"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ManagedProcessArtifacts,
                declaredWritePaths: ["artifacts/process-runs/process-run-001/evidence/product-write-attempt.txt"]),
            inspectedScriptContent:
                "Path('external-target/C/programovani/todo-summary/product/src/Program.cs').write_text('changed')");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("must not use external-target aliases as literal OS paths", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_read_only_validation_script_when_process_step_disallows_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/inspect.ps1"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.NoMutation,
                declaredReadPaths: ["C:\\programovani\\todo-summary\\product\\src\\Program.cs"]),
            inspectedScriptContent:
                "Get-Content -Path 'C:\\programovani\\todo-summary\\product\\src\\Program.cs'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_uninspected_script_when_process_step_disallows_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/opaque.ps1"
            },
            processAllowsProductMutation: false,
            scriptInspectionFailure: "script path was not readable");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("could not be inspected", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_governed_script_without_side_effect_manifest()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/inspect.ps1"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false,
            inspectedScriptContent:
                "Get-Content -Path 'artifacts/process-runs/process-run-001/evidence/report.md'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains(GovernedScriptSideEffectManifest.ArgumentName, decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_declared_no_mutation_script()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/inspect.ps1"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.NoMutation,
                declaredReadPaths: ["C:\\programovani\\todo-summary\\product\\src\\Program.cs"]),
            inspectedScriptContent:
                "Get-Content -Path 'C:\\programovani\\todo-summary\\product\\src\\Program.cs'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_powershell_static_io_product_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/write.ps1"
            },
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(GovernedScriptSideEffectMode.NoMutation),
            inspectedScriptContent:
                "[IO.File]::WriteAllText('C:\\programovani\\todo-summary\\product\\src\\Program.cs', 'changed')");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("write-capable operations", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_powershell_redirection_to_product_target()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/redirect.ps1"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ManagedProcessArtifacts,
                declaredWritePaths: ["artifacts/process-runs/process-run-001/evidence/redirect-attempt.txt"]),
            inspectedScriptContent:
                "'changed' > 'external-target/C/programovani/todo-summary/product/src/Program.cs'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("must not use external-target aliases as literal OS paths", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_undeclared_cmd_delegation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/delegate.ps1"
            },
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(GovernedScriptSideEffectMode.NoMutation),
            inspectedScriptContent:
                "cmd /c \"echo changed > C:\\programovani\\todo-summary\\product\\src\\Program.cs\"");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("shell delegation", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_encoded_powershell_command()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/encoded.ps1"
            },
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.NoMutation,
                allowEncodedCommands: true),
            inspectedScriptContent:
                "pwsh -NoProfile -EncodedCommand SQBFAFgA");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("encoded", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_python_path_open_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/write.py"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ManagedProcessArtifacts,
                declaredWritePaths: ["artifacts/process-runs/process-run-001/evidence/python-write-attempt.txt"]),
            inspectedScriptContent:
                "from pathlib import Path\nPath('external-target/C/programovani/todo-summary/product/src/Program.cs').open('w').write('changed')");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("must not use external-target aliases as literal OS paths", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_declared_current_run_artifact_script_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/write-evidence.ps1"
            },
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ManagedProcessArtifacts,
                declaredWritePaths: ["artifacts/process-runs/process-run-001/evidence/report.md"]),
            inspectedScriptContent:
                "Set-Content -Path 'artifacts/process-runs/process-run-001/evidence/report.md' -Value 'ok'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_declared_external_artifact_destination_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/export.ps1"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/process-run/evidence"],
            processAllowsProductMutation: false,
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ExternalArtifactDestination,
                declaredWritePaths: ["artifacts/process-runs/process-run-001/evidence/report.md"]),
            inspectedScriptContent:
                "[IO.File]::WriteAllText('artifacts/process-runs/process-run-001/evidence/report.md', 'ok')");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_product_mutation_step_script_with_declared_authority()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/helpers/apply-change.ps1"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/todo-summary/product"],
            processAllowsProductMutation: true,
            processStepAllowedOperations: ["MutateProductTarget"],
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ProductMutation,
                declaredWritePaths: ["C:\\programovani\\todo-summary\\product\\src\\Program.cs"]),
            inspectedScriptContent:
                "Set-Content -Path 'C:\\programovani\\todo-summary\\product\\src\\Program.cs' -Value 'changed'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void ProcessScriptSideEffectAnalyzer_detects_writes_and_child_scripts_without_runtime()
    {
        var analysis = ProcessScriptSideEffectAnalyzer.Analyze(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            "& './collect-evidence.ps1'\n'value' > 'artifacts/process-runs/process-run-001/evidence/report.txt'");

        Assert.True(analysis.HasWriteSignal);
        Assert.Contains(analysis.ChildScriptSignals, signal => signal.EndsWith("collect-evidence.ps1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessToolOperationAuthorizer_denies_missing_operation_without_full_policy_runtime()
    {
        var context = CreateContext(
            "workspace_dotnet_test",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processStepAllowedOperations: ["CaptureRuntimeProof"],
            processStepTargetScope: "QA review");
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            context.ToolName,
            context.RedactedArguments);

        var decision = ProcessToolOperationAuthorizer.Evaluate(
            context,
            signature,
            [OperationRequirement.Any("RunValidation")]);

        Assert.NotNull(decision);
        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("RunValidation", decision.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("workspace_write_file", "MutateProductTarget")]
    [InlineData("workspace_dotnet_test", "RunValidation")]
    [InlineData("workspace_dotnet_run", "LaunchRuntime")]
    [InlineData("workspace_dotnet_stop", "LaunchRuntime")]
    [InlineData(ToolContractCatalog.BrowserClick, "CaptureRuntimeProof")]
    [InlineData(ToolContractCatalog.BrowserWaitFor, "CaptureRuntimeProof")]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImage, "CaptureRuntimeProof")]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImages, "CaptureRuntimeProof")]
    [InlineData("processes_step_transition", "ExecuteExternalAction")]
    public void ProcessToolOperationAuthorizer_denies_governed_step_with_missing_operation_contract(
        string toolName,
        string requiredOperation)
    {
        var context = CreateContext(
            toolName,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processStepAllowedOperations: []);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            context.ToolName,
            context.RedactedArguments);

        var decision = ProcessToolOperationAuthorizer.Evaluate(
            context,
            signature,
            [OperationRequirement.Any(requiredOperation)]);

        Assert.NotNull(decision);
        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("missing an operation contract", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(requiredOperation, decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_dotnet_new_parent_outside_grounded_external_target_even_when_name_matches()
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
                ["parentDirectory"] = "external-target/C/workspace",
                ["name"] = "AllowedProduct",
                ["template"] = "console"
            },
            allowedExternalTargetAliases: ["external-target/C/workspace/AllowedProduct"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("outside the current run boundary", decision.Reason, StringComparison.Ordinal);
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
    public async Task EvaluateAsync_strips_escaped_line_break_annotations_in_allowed_external_target_alias()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_stat_path",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp"
            },
            allowedExternalTargetAliases:
            [
                "external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp/nWorkspace alias: external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp/nAll generated app source belongs under",
                "external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp Workspace alias: external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp All generated app source belongs under"
            ]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_dotnet_new_with_parent_under_grounded_external_target()
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
                ["parentDirectory"] = "external-target/C/workspace/AllowedProduct/child",
                ["name"] = "CreatedProduct",
                ["template"] = "console"
            },
            allowedExternalTargetAliases: ["external-target/C/workspace/AllowedProduct"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_dotnet_new_template_change_for_grounded_target()
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
                ["parentDirectory"] = "external-target/C/workspace/AllowedProduct",
                ["name"] = "CreatedProduct",
                ["template"] = "console"
            },
            allowedExternalTargetAliases: ["external-target/C/workspace/AllowedProduct"]);
        var secondContext = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/workspace/AllowedProduct",
                ["name"] = "CreatedProduct",
                ["template"] = "webapi"
            },
            allowedExternalTargetAliases: ["external-target/C/workspace/AllowedProduct"]);

        var firstDecision = await policy.EvaluateAsync(firstContext, CancellationToken.None);
        var secondDecision = await policy.EvaluateAsync(secondContext, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, firstDecision.Kind);
        Assert.Equal(ToolInvocationDecisionKind.Allow, secondDecision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_force_dotnet_new_for_governed_process_step()
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
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner",
                ["name"] = "PocketMeetingCostPlanner",
                ["template"] = "sln",
                ["force"] = "True"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recovered = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            context.ToolName,
            decision,
            context,
            out var recoverableResult);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("force=true", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(recovered);
        Assert.Contains("unsafe scaffold overwrite", recoverableResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_stale_external_target_without_echoing_requested_path()
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
                ["path"] = "external-target/C/programovani/candoitall-processes1-blazor-counter-d/ProcessCounter.slnx"
            },
            allowedExternalTargetAliases:
            [
                "external-target/C/programovani/candoitall-processes1-blazor-counter-g"
            ]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("outside the current run boundary", decision.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("blazor-counter-d", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/C/programovani/candoitall-processes1-blazor-counter-g", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_read_only_external_target_reads_and_denies_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string readOnlyRoot = "external-target/C/programovani/todo-summary";
        var readContext = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"{readOnlyRoot}/README.md"
            },
            readOnlyExternalTargetAliases: [readOnlyRoot]);

        var readDecision = await policy.EvaluateAsync(readContext, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, readDecision.Kind);

        var writeContext = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"{readOnlyRoot}/architecture.md",
                ["content"] = "Architecture note."
            },
            readOnlyExternalTargetAliases: [readOnlyRoot]);

        var writeDecision = await policy.EvaluateAsync(writeContext, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, writeDecision.Kind);
        Assert.Contains("read-only access", writeDecision.Reason, StringComparison.OrdinalIgnoreCase);
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
    public async Task EvaluateAsync_denies_broad_managed_workspace_search_for_read_only_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_search",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = "TetrisGame.slnx",
                ["relativePath"] = "."
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processAllowsProductMutation: false,
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Broad managed-workspace root discovery is denied", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_native_absolute_external_target_path_for_governed_process_run()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_list_files",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["relativePath"] = @"C:\programovani\dotnet\output"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("not native absolute paths", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry this structured workspace tool", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/output", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_native_absolute_managed_workspace_search_for_external_target_process_run()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_search",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = "TetrisGame",
                ["relativePath"] = @"C:\Users\lucys\AppData\Local\CanDoItAll\workspace"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processAllowsProductMutation: false,
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("outside the current-run external-target roots", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_native_absolute_project_media_file_read_for_external_target_process_run()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = @"C:\Users\lucys\AppData\Local\CanDoItAll\workspace\managed-files\project-media\files\3324868f66e2478abb8f14f32a5db1e9\office365-category-email-summary-c6c320f4b49d4790bdf7e71ab2a10fc3.md"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processAllowsProductMutation: false,
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("outside the current-run external-target roots", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_native_absolute_managed_workspace_evidence_file_read_for_external_target_process_run()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = @"C:\Users\lucys\AppData\Local\CanDoItAll\workspace\project-structure-context-brief.md"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processAllowsProductMutation: false,
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_native_absolute_managed_workspace_helper_file_read_for_external_target_process_run()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = @"C:\Users\lucys\AppData\Local\CanDoItAll\workspace\tools\launch_app.ps1"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processAllowsProductMutation: false,
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_broad_process_run_artifact_discovery_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_search",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = "TetrisGame",
                ["relativePath"] = "artifacts/process-runs"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processAllowsProductMutation: false,
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Broad process-run artifact discovery", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_specific_child_process_run_artifact_search_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var currentRunId = Guid.Parse("886aba26-7806-4214-bbe5-15e4c9ff57d1");
        var childRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var context = CreateContext(
            "workspace_search",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = "architecture-decision",
                ["relativePath"] = $"artifacts/scopes/organization/demo/process-runs/{childRunId:D}"
            },
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadUpstreamArtifacts",
                "ExecuteExternalAction"
            ],
            processStepTargetScope: "ExternalActionControlled",
            processRunId: currentRunId.ToString("D"));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
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
    public async Task EvaluateAsync_denies_current_step_primary_output_read_before_write()
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
                ["path"] = "artifacts/process-runs/process-run-001/steps/architecture-review.md"
            },
            processRunId: "process-run-001",
            sourceId: "architecture-review");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("before creating it", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_current_step_primary_output_read_when_runtime_explicitly_authorizes_recovery_read()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string primaryRef = "artifacts/process-runs/process-run-001/steps/architecture-review.md";
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef
            },
            allowedManagedArtifactReadRefs: [primaryRef],
            processRunId: "process-run-001",
            sourceId: "architecture-review");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_current_step_primary_output_read_after_successful_write_trace()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string primaryRef = "artifacts/process-runs/process-run-001/steps/architecture-review.md";
        var context = CreateContext(
            "workspace_read_file",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef
            },
            processRunId: "process-run-001",
            sourceId: "architecture-review",
            toolInvocationTraces:
            [
                new AgentToolInvocationTrace(
                    "workspace_write_file",
                    ToolInvocationClassification.Mutation,
                    1,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    Succeeded: true,
                    FailureMessage: string.Empty)
                {
                    Signature = AgentToolInvocationPolicyMetadata.BuildSignature(
                        "workspace_write_file",
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["content"] = "# Architecture handoff",
                            ["path"] = primaryRef
                        })
                }
            ]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_ungrounded_managed_creation_for_external_target_process_run()
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
                ["parentDirectory"] = "scratch/GeneratedProduct",
                ["name"] = "GeneratedProduct",
                ["template"] = "console"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Managed workspace path", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_ungrounded_managed_write_for_external_target_process_run()
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
                ["path"] = "scratch/GeneratedProduct/App.cs",
                ["content"] = "namespace GeneratedProduct;"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("Managed workspace path", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_output_creation_for_external_target_process_run()
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
                ["parentDirectory"] = "output/process-runs/process-run-001/TodoSummary",
                ["name"] = "TodoSummary.Console",
                ["template"] = "console"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/processes/TodoSummary"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("not a fallback product root", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_managed_output_validation_for_external_target_process_run()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_build",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetPath"] = "output/process-runs/process-run-001/TodoSummary/src/TodoSummary.Console/TodoSummary.Console.csproj"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/processes/TodoSummary"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("not a fallback product root", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_creation_under_grounded_external_target_for_external_target_process_run()
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
                ["parentDirectory"] = "external-target/C/workspace/AllowedProduct/modules",
                ["name"] = "CreatedProduct",
                ["template"] = "console"
            },
            allowedExternalTargetAliases: ["external-target/C/workspace/AllowedProduct"]);

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
    public async Task EvaluateAsync_allows_distinct_long_mutations_with_same_visible_prefix()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var prefix = new string('x', 220);

        for (var index = 0; index < DefaultAgentToolInvocationPolicy.MaxRepeatedMutationOrValidationInvocations + 1; index++)
        {
            var arguments = AgentToolInvocationPolicyMetadata.RedactArguments(
            [
                new KeyValuePair<string, object?>("path", "artifacts/result.md"),
                new KeyValuePair<string, object?>("content", prefix + index.ToString(CultureInfo.InvariantCulture))
            ]);
            var context = CreateContext(
                "workspace_write_file",
                ToolInvocationClassification.Mutation,
                isKnownTool: true,
                autoApprovalAllowed: true,
                approvalWrapperAvailable: false,
                arguments: arguments);

            var decision = await policy.EvaluateAsync(context, CancellationToken.None);

            Assert.NotEqual(ToolInvocationDecisionKind.Deny, decision.Kind);
        }
    }

    [Fact]
    public async Task EvaluateAsync_allows_product_file_mutation_when_external_target_is_trusted_writable_despite_readonly_overlap()
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

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_does_not_let_readonly_parent_deny_writable_child_alias()
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
            readOnlyExternalTargetAliases: ["external-target/C/work/apps"]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_product_file_mutation_when_external_target_is_only_read_only()
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
    public void RedactArguments_masks_HR_business_text_without_collapsing_distinct_targets()
    {
        var first = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
        [
            new KeyValuePair<string, object?>("request", new
            {
                agentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                name = "Private Alpha Name",
                instructions = "Confidential alpha instructions"
            })
        ]);
        var second = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
        [
            new KeyValuePair<string, object?>("request", new
            {
                agentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                name = "Private Beta Name",
                instructions = "Confidential beta instructions"
            })
        ]);
        var firstSignature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
            first);
        var secondSignature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
            second);

        Assert.DoesNotContain("Private Alpha Name", firstSignature, StringComparison.Ordinal);
        Assert.DoesNotContain("Confidential alpha instructions", firstSignature, StringComparison.Ordinal);
        Assert.Contains("11111111-1111-1111-1111-111111111111", firstSignature, StringComparison.Ordinal);
        Assert.NotEqual(firstSignature, secondSignature);
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
    public void BlockGuard_returns_recoverable_result_for_governed_read_discovery_denial()
    {
        var decision = ToolInvocationPolicyDecision.Deny(
            "workspace_search|relativePath=",
            "Broad managed-workspace root discovery is denied.");
        var context = CreateContext(
            "workspace_search",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly);

        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            "workspace_search",
            decision,
            context,
            out var result);

        Assert.True(recoverable);
        Assert.Contains("PolicyDenied", result, StringComparison.Ordinal);
        Assert.Contains("grounded external-target alias", result, StringComparison.Ordinal);
        Assert.Contains("Broad managed-workspace root discovery is denied", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BlockGuard_returns_write_first_guidance_for_current_step_own_output_read_denial()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/add-tests-and-proof.md";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceReadFile,
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef
            },
            processRunId: runId,
            sourceId: "add-tests-and-proof");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.WorkspaceReadFile,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("own primary managed output", decision.Reason, StringComparison.Ordinal);
        Assert.True(recoverable);
        Assert.Contains("not a missing tool permission", result, StringComparison.Ordinal);
        Assert.Contains("not a blocker", result, StringComparison.Ordinal);
        Assert.Contains("Do not retry the read, stat, list, or search", result, StringComparison.Ordinal);
        Assert.Contains("Do not write a status-only InProgress or Blocked placeholder and stop", result, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file or workspace_append_file", result, StringComparison.Ordinal);
        Assert.Contains("submit_process_step_outcome", result, StringComparison.Ordinal);
        Assert.Contains(primaryRef, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Policy_denies_current_step_primary_output_inprogress_placeholder_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/repair-solution-setup.md";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = """
                    Status: InProgress

                    # Repair solution setup findings

                    Reviewing upstream evidence before finalizing.
                    """
            },
            processRunId: runId,
            sourceId: "repair-solution-setup");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.WorkspaceWriteFile,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("cannot write primary managed output", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("status InProgress", decision.Reason, StringComparison.Ordinal);
        Assert.True(recoverable);
        Assert.Contains("not a missing tool permission", result, StringComparison.Ordinal);
        Assert.Contains("Do not retry the placeholder write", result, StringComparison.Ordinal);
        Assert.Contains("Continue the step's required product", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Policy_denies_current_step_primary_output_spaced_inprogress_placeholder_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/code-change.md";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = """
                    Status: In Progress

                    # Feature implementation change set

                    Work has started.
                    """
            },
            processRunId: runId,
            sourceId: "code-change");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.WorkspaceWriteFile,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("status InProgress", decision.Reason, StringComparison.Ordinal);
        Assert.True(recoverable);
        Assert.Contains("Do not retry the placeholder write", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Policy_denies_current_step_primary_output_status_only_blocked_placeholder_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/setup-handoff.md";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = """
                    Status: Blocked

                    # Setup handoff

                    Waiting for more context.
                    """
            },
            processRunId: runId,
            sourceId: "setup-handoff");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.WorkspaceWriteFile,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("status-only Blocked placeholder", decision.Reason, StringComparison.Ordinal);
        Assert.True(recoverable);
        Assert.Contains("Do not retry the placeholder write", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Policy_allows_current_step_primary_output_completed_evidence_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/setup-handoff.md";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = """
                    Status: Completed

                    # Setup handoff

                    Evidence refs:
                    - artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/validate-first-build.md
                    """
            },
            processRunId: runId,
            sourceId: "setup-handoff");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task Policy_denies_mutation_required_completed_handoff_before_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/code-change.md";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = "Status: Completed\n\n# Change set\n\nChanged files are planned."
            },
            allowedExternalTargetAliases: ["external-target/C/work/product"],
            processRequiresProductMutationBeforeManagedOutput: true,
            processProductMutationToolNames: [ToolContractCatalog.WorkspaceWriteFile],
            processRunId: runId,
            sourceId: "code-change");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.WorkspaceWriteFile,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("before a successful current-execution product-target mutation", decision.Reason, StringComparison.Ordinal);
        Assert.True(recoverable);
        Assert.Contains("ordering rule", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not retry the primary managed artifact write yet", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Policy_denies_mutation_required_branch_before_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{runId}/steps/repair.md",
                ["content"] = "Status: Completed\nBranch outcome key: product-repair-applied\n\n# Repair"
            },
            allowedExternalTargetAliases: ["external-target/C/work/product"],
            processRequiresProductMutationBeforeManagedOutput: true,
            processProductMutationToolNames: [ToolContractCatalog.WorkspaceWriteFile],
            processProductMutationRequiredBranchOutcomeKeys: ["product-repair-applied"],
            processRunId: runId,
            sourceId: "repair");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("before a successful current-execution product-target mutation", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Policy_allows_proof_only_branch_before_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{runId}/steps/repair.md",
                ["content"] = "Status: Completed\nBranch outcome key: proof-only-revalidation-prepared\n\n# Proof"
            },
            allowedExternalTargetAliases: ["external-target/C/work/product"],
            processRequiresProductMutationBeforeManagedOutput: true,
            processProductMutationToolNames: [ToolContractCatalog.WorkspaceWriteFile],
            processProductMutationRequiredBranchOutcomeKeys: ["product-repair-applied"],
            processRunId: runId,
            sourceId: "repair");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task Policy_denies_branch_specific_mutation_output_without_canonical_branch_key()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{runId}/steps/repair.md",
                ["content"] = "Status: Completed\n\n# Repair"
            },
            allowedExternalTargetAliases: ["external-target/C/work/product"],
            processRequiresProductMutationBeforeManagedOutput: true,
            processProductMutationToolNames: [ToolContractCatalog.WorkspaceWriteFile],
            processProductMutationRequiredBranchOutcomeKeys: ["product-repair-applied"],
            processRunId: runId,
            sourceId: "repair");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.WorkspaceWriteFile,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("must declare exactly one valid Branch outcome key", decision.Reason, StringComparison.Ordinal);
        Assert.True(recoverable);
        Assert.Contains("branch-selection rule", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Policy_allows_mutation_required_completed_handoff_after_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/code-change.md";
        var now = DateTimeOffset.UtcNow;
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = "Status: Completed\n\n# Change set\n\nProduct mutation is complete."
            },
            allowedExternalTargetAliases: ["external-target/C/work/product"],
            processRequiresProductMutationBeforeManagedOutput: true,
            processProductMutationToolNames: [ToolContractCatalog.WorkspaceWriteFile],
            processRunId: runId,
            sourceId: "code-change",
            toolInvocationTraces:
            [
                new AgentToolInvocationTrace(
                    ToolContractCatalog.WorkspaceWriteFile,
                    ToolInvocationClassification.Mutation,
                    1,
                    now,
                    now,
                    true,
                    string.Empty)
                {
                    Signature = "workspace_write_file|path=external-target/C/work/product/src/App/Home.razor"
                }
            ]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task Policy_does_not_treat_validation_tool_as_product_mutation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        var now = DateTimeOffset.UtcNow;
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"artifacts/process-runs/{runId}/steps/feature-repair.md",
                ["content"] = "Status: Completed\n\n# Repair\n\nValidation passed without source changes."
            },
            allowedExternalTargetAliases: ["external-target/C/work/product"],
            processRequiresProductMutationBeforeManagedOutput: true,
            processProductMutationToolNames: [ToolContractCatalog.WorkspaceWriteFile],
            processRunId: runId,
            sourceId: "feature-repair",
            toolInvocationTraces:
            [
                new AgentToolInvocationTrace(
                    "workspace_dotnet_build",
                    ToolInvocationClassification.Mutation,
                    1,
                    now,
                    now,
                    true,
                    string.Empty)
                {
                    Signature = "workspace_dotnet_build|targetPath=external-target/C/work/product/App.slnx"
                }
            ]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
    }

    [Fact]
    public async Task Policy_does_not_treat_product_alias_quoted_in_managed_artifact_as_mutation_target()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/feature-repair.md";
        var now = DateTimeOffset.UtcNow;
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = "Status: Completed\n\nProduct root: external-target/C/work/product"
            },
            allowedExternalTargetAliases: ["external-target/C/work/product"],
            processRequiresProductMutationBeforeManagedOutput: true,
            processProductMutationToolNames: [ToolContractCatalog.WorkspaceWriteFile],
            processRunId: runId,
            sourceId: "feature-repair",
            toolInvocationTraces:
            [
                new AgentToolInvocationTrace(
                    ToolContractCatalog.WorkspaceWriteFile,
                    ToolInvocationClassification.Mutation,
                    1,
                    now,
                    now,
                    true,
                    string.Empty)
                {
                    Signature = $"workspace_write_file|content=Status: Completed Product root: external-target/C/work/product,path={primaryRef}"
                }
            ]);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
    }

    [Fact]
    public async Task Policy_allows_current_step_primary_output_blocked_with_concrete_evidence_write()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string runId = "11111111-2222-3333-4444-555555555555";
        const string primaryRef = $"artifacts/process-runs/{runId}/steps/setup-handoff.md";
        var context = CreateContext(
            ToolContractCatalog.WorkspaceWriteFile,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = primaryRef,
                ["content"] = """
                    Status: Blocked

                    # Setup handoff

                    The required tool workspace_dotnet_build failed with exit code 1.
                    Evidence refs:
                    - artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/validate-first-build.md
                    """
            },
            processRunId: runId,
            sourceId: "setup-handoff");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task BlockGuard_returns_recoverable_result_for_governed_browser_snapshot_bounds_denial()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            ToolContractCatalog.BrowserSnapshot,
            AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.BrowserSnapshot),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["depth"] = "4",
                ["boxes"] = "True"
            },
            processStepAllowedOperations: [ProcessOperationContractNames.CaptureRuntimeProof],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.BrowserSnapshot,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.True(recoverable);
        Assert.Contains("PolicyDenied", result, StringComparison.Ordinal);
        Assert.Contains("governed browser proof bounds", result, StringComparison.Ordinal);
        Assert.Contains("depth=2 or boxes=false", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BlockGuard_does_not_return_recoverable_result_for_browser_snapshot_without_runtime_proof_authorization()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            ToolContractCatalog.BrowserSnapshot,
            AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.BrowserSnapshot),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["depth"] = "4",
                ["boxes"] = "True"
            },
            processStepAllowedOperations: [ProcessOperationContractNames.ReadProcessContext],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);
        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.BrowserSnapshot,
            decision,
            context,
            out var result);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.False(recoverable);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BlockGuard_does_not_return_recoverable_result_for_mutation_denial()
    {
        var decision = ToolInvocationPolicyDecision.Deny(
            "workspace_write_file|path=external-target/C/programovani/dotnet/output/Program.cs",
            "Read-only external-target roots cannot be mutated.");
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: true,
            readOnlyExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetReadOnly);

        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            "workspace_write_file",
            decision,
            context,
            out var result);

        Assert.False(recoverable);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BlockGuard_returns_recoverable_result_for_governed_workspace_boundary_mutation_denial()
    {
        var decision = ToolInvocationPolicyDecision.Deny(
            "workspace_dotnet_new|name=Calculator,parentDirectory=external-target/C/programovani/dotnet,template=sln",
            "Governed process runs may only access external-target paths grounded by the current run. The requested external-target path is outside the current run boundary; current-run roots: external-target/C/programovani/dotnet/calculator-output.");
        var context = CreateContext(
            ToolContractCatalog.WorkspaceDotNetNew,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: true,
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/calculator-output"],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetMutable);

        var recoverable = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            ToolContractCatalog.WorkspaceDotNetNew,
            decision,
            context,
            out var result);

        Assert.True(recoverable);
        Assert.Contains("PolicyDenied", result, StringComparison.Ordinal);
        Assert.Contains("wrong tool argument", result, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/calculator-output", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_governed_script_with_external_target_alias_literal()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/output"],
            processStepTargetScope: ProcessOperationContractNames.ExternalProductTargetMutable,
            inspectedScriptContent: "dotnet new blazorwasm -o 'external-target/C/programovani/dotnet/output/src/TetrisGame'",
            scriptSideEffectManifestJson: CreateSideEffectManifest(
                GovernedScriptSideEffectMode.ProductMutation,
                declaredWritePaths: ["external-target/C/programovani/dotnet/output/src/TetrisGame"]));

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("must not use external-target aliases as literal OS paths", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_new", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("native absolute ProductRoot", decision.Reason, StringComparison.Ordinal);
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
    [InlineData("workspace_list_directory", ToolInvocationClassification.Read)]
    [InlineData("workspace_hash_path", ToolInvocationClassification.Read)]
    [InlineData("workspace_zip_path", ToolInvocationClassification.Mutation)]
    [InlineData("workspace_unzip_archive", ToolInvocationClassification.Mutation)]
    [InlineData("workspace_write_file", ToolInvocationClassification.Mutation)]
    [InlineData("workspace_write_spreadsheet", ToolInvocationClassification.Mutation)]
    [InlineData("workspace_read_spreadsheet_range", ToolInvocationClassification.Read)]
    [InlineData("workspace_spreadsheet_function_catalog", ToolInvocationClassification.Read)]
    [InlineData("workspace_dotnet_test", ToolInvocationClassification.Validation)]
    [InlineData("provider-native-web-search", ToolInvocationClassification.HostedProviderNative)]
    [InlineData("mcp_project_query", ToolInvocationClassification.LocalMcp)]
    [InlineData("hosted_mcp_project_query", ToolInvocationClassification.HostedMcp)]
    [InlineData("workspace_read_file", ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.LoadSkill, ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.ReadSkillResource, ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.RunSkillScript, ToolInvocationClassification.Mutation)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProcessesTemplateImport, ToolInvocationClassification.Mutation)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList, ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProcessesTemplateLiveRunProfilesList, ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProjectStructureRead, ToolInvocationClassification.Read)]
    [InlineData(AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate, ToolInvocationClassification.Mutation)]
    [InlineData("project_structure_unregistered_mutation", ToolInvocationClassification.Unknown)]
    [InlineData("processes_unregistered_mutation", ToolInvocationClassification.Unknown)]
    public void Classify_returns_expected_tool_classification(string toolName, ToolInvocationClassification expected)
    {
        var classification = AgentToolInvocationPolicyMetadata.Classify(toolName);

        Assert.Equal(expected, classification);
    }

    [Theory]
    [InlineData("workspace_unregistered_side_effect")]
    [InlineData("browser_unregistered_side_effect")]
    [InlineData("arbitrary_unregistered_tool")]
    public void Classify_does_not_fallback_unknown_tools_to_read(string toolName)
    {
        var classification = AgentToolInvocationPolicyMetadata.Classify(toolName);

        Assert.Equal(ToolInvocationClassification.Unknown, classification);
    }

    [Fact]
    public void ToolPolicyMetadata_classifies_high_risk_catalog_tools_explicitly()
    {
        Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceCommandRun));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.WorkspaceCommandRun));
        Assert.True(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.WorkspaceCommandRun));

        Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.LocalMcpLaunch));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.LocalMcpLaunch));
        Assert.True(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.LocalMcpLaunch));

        Assert.Equal(ToolInvocationClassification.Validation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.BrowserClick));
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.BrowserClick));
        Assert.False(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.BrowserClick));

        Assert.Equal(ToolInvocationClassification.Validation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.BrowserTakeScreenshot));
        Assert.Equal(ToolInvocationClassification.Validation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.BrowserWaitFor));
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.BrowserWaitFor));
        Assert.False(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.BrowserWaitFor));
        Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceAnalyzeImage));
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.WorkspaceAnalyzeImage));
        Assert.False(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.WorkspaceAnalyzeImage));
        Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceAnalyzeImages));
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.WorkspaceAnalyzeImages));
        Assert.False(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.WorkspaceAnalyzeImages));
        Assert.Equal(ToolInvocationClassification.Validation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceConvertDocument));
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.WorkspaceConvertDocument));
        Assert.False(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.WorkspaceConvertDocument));
        Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceWriteSpreadsheet));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.WorkspaceWriteSpreadsheet));
        Assert.True(AgentToolInvocationPolicyMetadata.IsMutationTool(ToolContractCatalog.WorkspaceWriteSpreadsheet));
        Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceSpreadsheetFunctionCatalog));
        Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(ToolContractCatalog.WorkspaceSpreadsheetFunctionCatalog));
        Assert.Equal(ToolInvocationClassification.Validation, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceDotNetStop));
        Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceExecutionBoundary));
    }

    [Fact]
    public async Task EvaluateAsync_allows_known_finalizer_tools_without_approval_or_process_operation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();

        foreach (var toolName in ToolContractCatalog.FinalizerToolNames)
        {
            var context = CreateContext(
                toolName,
                AgentToolInvocationPolicyMetadata.Classify(toolName),
                isKnownTool: ToolContractCatalog.IsKnownToolName(toolName),
                autoApprovalAllowed: false,
                approvalWrapperAvailable: false,
                processStepAllowedOperations: [],
                processStepTargetScope: "ExternalProductTargetMutable");

            var decision = await policy.EvaluateAsync(context, CancellationToken.None);

            Assert.Equal(ToolInvocationClassification.Read, context.Classification);
            Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
            Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
        }
    }

    [Fact]
    public async Task EvaluateAsync_denies_command_run_without_execute_external_action_operation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            ToolContractCatalog.WorkspaceCommandRun,
            AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceCommandRun),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "RunValidation"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("ExecuteExternalAction", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolCapabilityRegistry_registers_every_known_catalog_tool()
    {
        var registeredToolNames = ToolCapabilityRegistry.Capabilities
            .Select(capability => capability.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingToolNames = ToolContractCatalog.KnownToolNames
            .Select(ToolContractCatalog.NormalizeToolName)
            .Where(toolName => !registeredToolNames.Contains(toolName))
            .OrderBy(toolName => toolName, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missingToolNames);
        Assert.Equal(registeredToolNames.Count, ToolCapabilityRegistry.Capabilities.Count);
    }

    [Fact]
    public void ToolCapabilityRegistry_declares_static_operation_requirements_for_high_risk_tools()
    {
        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceCommandRun, out var commandRun));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, commandRun.OperationRequirementKind);
        Assert.Contains(commandRun.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("ExecuteExternalAction", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.LocalMcpLaunch, out var localMcpLaunch));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, localMcpLaunch.OperationRequirementKind);
        Assert.Contains(localMcpLaunch.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("ExecuteExternalAction", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.BrowserClick, out var browserClick));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, browserClick.OperationRequirementKind);
        Assert.Contains(browserClick.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("CaptureRuntimeProof", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.BrowserWaitFor, out var browserWaitFor));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, browserWaitFor.OperationRequirementKind);
        Assert.Contains(browserWaitFor.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("CaptureRuntimeProof", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceAnalyzeImage, out var analyzeImage));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, analyzeImage.OperationRequirementKind);
        Assert.Contains(analyzeImage.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("CaptureRuntimeProof", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceAnalyzeImages, out var analyzeImages));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, analyzeImages.OperationRequirementKind);
        Assert.Contains(analyzeImages.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("CaptureRuntimeProof", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceConvertDocument, out var convertDocument));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, convertDocument.OperationRequirementKind);
        Assert.Contains(convertDocument.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("ReadProjectStructure", StringComparer.Ordinal) &&
            requirement.AnyOf.Contains("WriteManagedProcessArtifacts", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceDotNetStop, out var dotnetStop));
        Assert.Equal(ToolCapabilityOperationRequirementKind.Static, dotnetStop.OperationRequirementKind);
        Assert.Contains(dotnetStop.OperationRequirements, requirement =>
            requirement.AnyOf.Contains("LaunchRuntime", StringComparer.Ordinal) &&
            requirement.AnyOf.Contains("CaptureRuntimeProof", StringComparer.Ordinal));

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceWriteSpreadsheet, out var writeSpreadsheet));
        Assert.Equal(ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, writeSpreadsheet.OperationRequirementKind);
    }

    [Fact]
    public void ToolCapabilityRegistry_declares_side_effect_target_scope_and_proof_metadata()
    {
        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceCommandRun, out var commandRun));
        Assert.Equal(ToolCapabilitySideEffectKind.LocalProcessExecution, commandRun.SideEffectKind);
        Assert.True(commandRun.CanExecuteExternalAction);
        Assert.Contains(ProcessOperationContractNames.ExternalActionControlled, commandRun.TargetScopeRequirements);
        Assert.Equal(ToolCapabilityIdempotencyDescriptor.ExternalSideEffect, commandRun.IdempotencyDescriptor);

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceWriteFile, out var writeFile));
        Assert.True(writeFile.CanMutateProduct);
        Assert.True(writeFile.CanWriteManagedArtifact);
        Assert.Contains(ProcessOperationContractNames.ManagedProcessArtifactsOnly, writeFile.TargetScopeRequirements);
        Assert.Contains(ProcessOperationContractNames.ExternalProductTargetMutable, writeFile.TargetScopeRequirements);

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceWriteSpreadsheet, out var writeSpreadsheet));
        Assert.True(writeSpreadsheet.CanMutateProduct);
        Assert.True(writeSpreadsheet.CanWriteManagedArtifact);
        Assert.Contains(ProcessOperationContractNames.ManagedProcessArtifactsOnly, writeSpreadsheet.TargetScopeRequirements);

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.BrowserClick, out var browserClick));
        Assert.Equal(ToolCapabilityBrowserProofRole.Interaction, browserClick.BrowserProofRole);
        Assert.False(browserClick.CanMutateProduct);
        Assert.True(browserClick.CanReadExternalTarget);
        Assert.Contains(ProcessOperationContractNames.ExternalProductTargetReadOnly, browserClick.TargetScopeRequirements);

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.BrowserTakeScreenshot, out var screenshot));
        Assert.Equal(ToolCapabilityBrowserProofRole.EvidenceCapture, screenshot.BrowserProofRole);

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.BrowserWaitFor, out var browserWaitFor));
        Assert.Equal(ToolCapabilityBrowserProofRole.Observation, browserWaitFor.BrowserProofRole);
        Assert.False(browserWaitFor.CanMutateProduct);
        Assert.True(browserWaitFor.CanReadExternalTarget);
        Assert.Contains(ProcessOperationContractNames.ExternalProductTargetReadOnly, browserWaitFor.TargetScopeRequirements);

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.WorkspaceDotNetStop, out var dotnetStop));
        Assert.Equal(ToolCapabilitySideEffectKind.RuntimeLaunch, dotnetStop.SideEffectKind);
        Assert.False(dotnetStop.CanMutateProduct);
        Assert.Contains(ProcessOperationContractNames.ExternalProductTargetReadOnly, dotnetStop.TargetScopeRequirements);
    }

    [Fact]
    public void ProjectStructureToolInventory_classifies_all_runtime_project_structure_tools()
    {
        var expectedReadTools = new[]
        {
            AgentToolInvocationPolicyMetadata.ProjectStructureProjectsList,
            AgentToolInvocationPolicyMetadata.ProjectStructureHierarchyGet,
            AgentToolInvocationPolicyMetadata.ProjectStructureRead,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCatalog,
            AgentToolInvocationPolicyMetadata.ProjectStructureChecklist,
            AgentToolInvocationPolicyMetadata.ProjectStructureDependenciesQuery,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetGet,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetContentGet,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowAddOptions,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStatusGet,
            AgentToolInvocationPolicyMetadata.ProjectStructureKnowledgeQuery,
            AgentToolInvocationPolicyMetadata.ProjectStructureAnalyticsQuery,
            AgentToolInvocationPolicyMetadata.ProjectStructureLeaseGet
        };
        var expectedMutationTools = new[]
        {
            AgentToolInvocationPolicyMetadata.ProjectStructureProjectCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureProjectUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectLink,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesToNewSubproject,
            AgentToolInvocationPolicyMetadata.ProjectStructureDependencyLink,
            AgentToolInvocationPolicyMetadata.ProjectStructureDependencyUnlink,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeTypeUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeMetadataUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesStatusUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeStatusUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesProgressUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProgressUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesMarkerUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeMarkerUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesPriorityUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodePriorityUpdate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeMove,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeRecompose,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeReparent,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeDescendantsToProjectMove,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCommandExecute,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessDefinitionLink,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart,
            AgentToolInvocationPolicyMetadata.ProjectStructureProcessSubprocessLaunch,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowDefinitionCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeDelete,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesDelete,
            AgentToolInvocationPolicyMetadata.ProjectStructureApprovalRequest,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreateRevision,
            AgentToolInvocationPolicyMetadata.ProjectStructureLinkCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureLinkUnlink,
            AgentToolInvocationPolicyMetadata.ProjectStructureImport,
            AgentToolInvocationPolicyMetadata.ProjectStructureProjectLeaseAcquire,
            AgentToolInvocationPolicyMetadata.ProjectStructureRepoBranchLeaseAcquire,
            AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRenew,
            AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRelease
        };

        Assert.Equal(expectedReadTools.Order(StringComparer.Ordinal), AgentToolInvocationPolicyMetadata.ProjectStructureReadTools.Order(StringComparer.Ordinal));
        Assert.Equal(expectedMutationTools.Order(StringComparer.Ordinal), AgentToolInvocationPolicyMetadata.ProjectStructureMutationTools.Order(StringComparer.Ordinal));
        foreach (var toolName in expectedReadTools)
        {
            Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(toolName));
            Assert.False(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
        }

        foreach (var toolName in expectedMutationTools)
        {
            Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(toolName));
            Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
            Assert.True(AgentToolInvocationPolicyMetadata.IsProjectStructureMutationTool(toolName));
        }
    }

    [Fact]
    public void ProcessToolInventory_registers_every_process_tool_in_catalog_and_capability_registry()
    {
        var expectedProcessTools = new[]
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
            AgentToolInvocationPolicyMetadata.ProcessesTemplateImport,
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
            AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateLiveRunProfilesList
        };

        Assert.Equal(23, expectedProcessTools.Length);
        Assert.Equal(
            expectedProcessTools.Length,
            expectedProcessTools.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var toolName in expectedProcessTools)
        {
            Assert.Contains(toolName, ToolContractCatalog.KnownToolNames);
            Assert.True(ToolCapabilityRegistry.TryResolve(toolName, out var capability), toolName);
            Assert.Equal(AgentToolInvocationPolicyMetadata.Classify(toolName), capability.Classification);
            Assert.Equal(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName), capability.RequiresApprovalByDefault);
        }
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

    [Fact]
    public async Task EvaluateAsync_denies_unbounded_governed_browser_snapshot()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "browser_snapshot",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["depth"] = "20",
                ["boxes"] = "True"
            });

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("depth", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_allows_bounded_governed_browser_snapshot()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "browser_snapshot",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["depth"] = "2",
                ["boxes"] = "False"
            });

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_bounded_boxed_governed_browser_snapshot()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "browser_snapshot",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["depth"] = "2",
                ["boxes"] = "True"
            },
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "CaptureRuntimeProof"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_validation_without_run_validation_operation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_test",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "WriteManagedProcessArtifacts"
            ],
            processStepTargetScope: "ManagedProcessArtifactsOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("RunValidation", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_validation_when_run_validation_operation_is_allowed()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_test",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "RunValidation",
                "CaptureRuntimeProof"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_bounded_dotnet_run_with_run_validation_operation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_run",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["keepAlive"] = "False",
                ["lifetimeScope"] = "ExecutionRun"
            },
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "RunValidation"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_kept_alive_runtime_launch_without_launch_runtime_operation()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_run",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["keepAlive"] = "True",
                ["lifetimeScope"] = "ExecutionRun"
            },
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "RunValidation"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("LaunchRuntime", decision.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("artifacts/process-runs/dotnet-run/20260616/startup.json")]
    [InlineData("artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/dotnet-run/20260616/startup.json")]
    public async Task EvaluateAsync_allows_dotnet_stop_with_runtime_proof_operation(string startupReceiptPath)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_dotnet_stop",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["startupReceiptPath"] = startupReceiptPath
            },
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "CaptureRuntimeProof"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Theory]
    [InlineData(ToolContractCatalog.BrowserNavigate)]
    [InlineData(ToolContractCatalog.BrowserClick)]
    [InlineData(ToolContractCatalog.BrowserPressKey)]
    [InlineData(ToolContractCatalog.BrowserType)]
    [InlineData(ToolContractCatalog.BrowserWaitFor)]
    public async Task EvaluateAsync_denies_browser_tools_without_capture_runtime_proof_operation(string toolName)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            toolName,
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "RunValidation"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("CaptureRuntimeProof", decision.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ToolContractCatalog.BrowserNavigate)]
    [InlineData(ToolContractCatalog.BrowserClick)]
    [InlineData(ToolContractCatalog.BrowserPressKey)]
    [InlineData(ToolContractCatalog.BrowserType)]
    [InlineData(ToolContractCatalog.BrowserWaitFor)]
    public async Task EvaluateAsync_allows_browser_tools_with_capture_runtime_proof_operation(string toolName)
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            toolName,
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "CaptureRuntimeProof"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_artifact_only_write_under_current_run_artifacts()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/process-run-001/evidence/report.md"
            },
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "WriteManagedProcessArtifacts"
            ],
            processStepTargetScope: "ManagedProcessArtifactsOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_artifact_only_write_under_process_artifacts()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "process-artifacts/process-run-001/evidence/report.md"
            },
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "WriteManagedProcessArtifacts"
            ],
            processStepTargetScope: "ManagedProcessArtifactsOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_product_write_when_external_product_root_is_named_output()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string productRoot = "external-target/C/programovani/dotnet-demo/output";
        var context = CreateContext(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: true,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"{productRoot}/TetrisGame.Core/TetrisGame.Core.csproj",
                ["content"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>"
            },
            allowedExternalTargetAliases: [productRoot],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "MutateProductTarget",
                "WriteManagedProcessArtifacts"
            ],
            processStepTargetScope: "ExternalProductTargetMutable");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_product_root_named_output_script_for_mutation_step()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string productRoot = "external-target/C/programovani/dotnet-demo/output";
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: true,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = $"{productRoot}/build.ps1",
                ["workingDirectory"] = productRoot,
                ["timeoutSeconds"] = "1200"
            },
            allowedExternalTargetAliases: [productRoot],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "MutateProductTarget",
                "RunValidation"
            ],
            processStepTargetScope: "ExternalProductTargetMutable",
            inspectedScriptContent:
                "Set-Location -LiteralPath $PSScriptRoot\n& dotnet restore TetrisGame.csproj\n& dotnet build TetrisGame.csproj --no-restore");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Recoverable_denied_result_treats_missing_current_run_script_as_ordering_retry()
    {
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = "artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/wire-project.ps1"
            },
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "MutateProductTarget",
                "WriteManagedProcessArtifacts"
            ],
            processStepTargetScope: "ExternalProductTargetMutable");
        var decision = ToolInvocationPolicyDecision.Deny(
            "workspace_pwsh_run_script|path=artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/wire-project.ps1",
            "This governed step is not authorized to run scripts without declared side effects. Script 'artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/wire-project.ps1' could not be inspected before execution: script path 'artifacts/process-runs/11111111-2222-3333-4444-555555555555/steps/wire-project.ps1' does not exist.");

        var recovered = AgentToolPolicyBlockGuard.TryCreateRecoverableDeniedResult(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            decision,
            context,
            out var result);

        Assert.True(recovered);
        Assert.Contains("helper-script ordering", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_write_file", result, StringComparison.Ordinal);
        Assert.Contains("retry workspace_pwsh_run_script", result, StringComparison.Ordinal);
        Assert.Contains("Do not submit Blocked", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_denies_copying_previous_run_product_into_current_product_target()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string productRoot = "external-target/C/programovani/dotnet-demo/output";
        var context = CreateContext(
            "workspace_copy_path",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: true,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourcePath"] = $"{productRoot}/oldruns/codex-before-process-rerun/product",
                ["destinationPath"] = $"{productRoot}/product",
                ["overwrite"] = "True"
            },
            allowedExternalTargetAliases: [productRoot],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "MutateProductTarget",
                "WriteManagedProcessArtifacts"
            ],
            processStepTargetScope: "ExternalProductTargetMutable");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("previous-run", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project structure", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_validation_against_previous_run_product_archive()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        const string productRoot = "external-target/C/programovani/dotnet-demo/output";
        var context = CreateContext(
            "workspace_dotnet_build",
            ToolInvocationClassification.Validation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: true,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetPath"] = $"{productRoot}/oldruns/codex-before-process-rerun/src/TetrisGame/TetrisGame.csproj",
                ["configuration"] = "Release"
            },
            allowedExternalTargetAliases: [productRoot],
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "MutateProductTarget",
                "RunValidation"
            ],
            processStepTargetScope: "ExternalProductTargetMutable");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("previous-run", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current product", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_denies_project_structure_mutation_without_execute_external_action()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadProjectStructure",
                "RunValidation",
                "CaptureRuntimeProof"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("ExecuteExternalAction", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_project_structure_mutation_with_execute_external_action()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "WriteManagedProcessArtifacts",
                "ExecuteExternalAction"
            ],
            processStepTargetScope: "ExternalActionControlled");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_denies_project_structure_node_process_start_with_execute_external_action_only()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadProjectStructure",
                "ExecuteExternalAction"
            ],
            processStepTargetScope: "ExternalActionControlled");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains(ProcessOperationContractNames.StartProjectNodeProcess, decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_project_structure_subprocess_launch_with_execute_external_action()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.ProjectStructureProcessSubprocessLaunch,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ProjectStructureProcessSubprocessLaunch),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadProjectStructure",
                "ExecuteExternalAction"
            ],
            processStepTargetScope: "ExternalActionControlled");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_project_structure_approval_request_with_escalate_or_decide()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.ProjectStructureApprovalRequest,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ProjectStructureApprovalRequest),
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "EscalateOrDecide"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_allows_project_structure_read_without_execute_external_action()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            AgentToolInvocationPolicyMetadata.ProjectStructureRead,
            AgentToolInvocationPolicyMetadata.Classify(AgentToolInvocationPolicyMetadata.ProjectStructureRead),
            isKnownTool: true,
            autoApprovalAllowed: false,
            approvalWrapperAvailable: false,
            processAllowsProductMutation: false,
            processStepAllowedOperations:
            [
                "ReadProcessContext",
                "ReadProjectStructure"
            ],
            processStepTargetScope: "ExternalProductTargetReadOnly");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
        Assert.Equal(ToolInvocationClassification.Read, context.Classification);
    }

    [Fact]
    public async Task EvaluateAsync_denies_full_page_governed_browser_screenshot()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var context = CreateContext(
            "browser_take_screenshot",
            ToolInvocationClassification.Read,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fullPage"] = "True"
            });

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("viewport", decision.Reason, StringComparison.OrdinalIgnoreCase);
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
            AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList,
            AgentToolInvocationPolicyMetadata.ProcessesTemplateLiveRunProfilesList
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
        IReadOnlyList<string>? readOnlyExternalTargetAliases = null,
        IReadOnlyList<string>? allowedManagedArtifactReadRefs = null,
        bool processAllowsProductMutation = true,
        bool processRequiresProductMutationBeforeManagedOutput = false,
        IReadOnlyList<string>? processProductMutationToolNames = null,
        IReadOnlyList<string>? processStepAllowedOperations = null,
        string processStepTargetScope = "",
        string inspectedScriptContent = "",
        string scriptInspectionFailure = "",
        string scriptSideEffectManifestJson = "",
        string processRunId = "process-run-001",
        string sourceId = "feature-intake",
        string contextWorkspaceScopeKind = "",
        string contextWorkspaceScopeKey = "",
        IReadOnlyList<AgentToolInvocationTrace>? toolInvocationTraces = null,
        IReadOnlyList<string>? processProductMutationRequiredBranchOutcomeKeys = null)
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
            ProcessRunId: processRunId,
            ProcessStepId: "step-001",
            AllowedExternalTargetAliases: allowedExternalTargetAliases,
            ReadOnlyExternalTargetAliases: readOnlyExternalTargetAliases,
            ApprovalWrapperEffectiveForProvider: approvalWrapperEffectiveForProvider,
            ApplicationApprovalAvailable: applicationApprovalAvailable,
            ProcessAllowsProductMutation: processAllowsProductMutation,
            ProcessRequiresProductMutationBeforeManagedOutput: processRequiresProductMutationBeforeManagedOutput,
            ProcessProductMutationToolNames: processProductMutationToolNames,
            ProcessStepAllowedOperations: processStepAllowedOperations ?? ProcessOperationContractNames.AllOperations,
            ProcessStepTargetScope: processStepTargetScope,
            ContextWorkspaceScopeKind: contextWorkspaceScopeKind,
            ContextWorkspaceScopeKey: contextWorkspaceScopeKey,
            InspectedScriptContent: inspectedScriptContent,
            ScriptInspectionFailure: scriptInspectionFailure,
            ScriptSideEffectManifestJson: scriptSideEffectManifestJson,
            ToolInvocationTraces: toolInvocationTraces,
            ProcessProductMutationRequiredBranchOutcomeKeys: processProductMutationRequiredBranchOutcomeKeys)
        {
            SourceId = sourceId,
            AllowedManagedArtifactReadRefs = allowedManagedArtifactReadRefs ?? []
        };
    }

    private static string CreateSideEffectManifest(
        GovernedScriptSideEffectMode mode,
        string[]? declaredReadPaths = null,
        string[]? declaredWritePaths = null,
        string[]? declaredChildScripts = null,
        bool allowShellDelegation = false,
        bool allowEncodedCommands = false)
    {
        return JsonSerializer.Serialize(new GovernedScriptSideEffectManifest
        {
            Mode = mode,
            DeclaredReadPaths = declaredReadPaths ?? [],
            DeclaredWritePaths = declaredWritePaths ?? [],
            DeclaredChildScripts = declaredChildScripts ?? [],
            AllowShellDelegation = allowShellDelegation,
            AllowEncodedCommands = allowEncodedCommands
        });
    }
}
