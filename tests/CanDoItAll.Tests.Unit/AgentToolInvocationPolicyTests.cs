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
    public async Task EvaluateAsync_SB03_INV_001_denies_pwsh_script_product_write_when_process_step_disallows_product_mutation()
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
        Assert.Contains("contains write operations against product target", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external-target/C/programovani/todo-summary/product/src/Program.cs", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_SB03_INV_001_denies_python_script_product_write_when_process_step_disallows_product_mutation()
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
        Assert.Contains("contains write operations against product target", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_SB03_INV_001_allows_read_only_validation_script_when_process_step_disallows_product_mutation()
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
                declaredReadPaths: ["external-target/C/programovani/todo-summary/product/src/Program.cs"]),
            inspectedScriptContent:
                "Get-Content -Path 'external-target/C/programovani/todo-summary/product/src/Program.cs'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_SB03_INV_001_denies_uninspected_script_when_process_step_disallows_product_mutation()
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
    public async Task EvaluateAsync_SB06_INV_001_denies_governed_script_without_side_effect_manifest()
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
                "Get-Content -Path 'external-target/C/programovani/todo-summary/product/src/Program.cs'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains(GovernedScriptSideEffectManifest.ArgumentName, decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_SB06_INV_002_allows_declared_no_mutation_script()
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
                declaredReadPaths: ["external-target/C/programovani/todo-summary/product/src/Program.cs"]),
            inspectedScriptContent:
                "Get-Content -Path 'external-target/C/programovani/todo-summary/product/src/Program.cs'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_SB06_INV_003_denies_powershell_static_io_product_write()
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
    public async Task EvaluateAsync_SB06_INV_004_denies_powershell_redirection_to_product_target()
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
        Assert.Contains("product target", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_SB06_INV_005_denies_undeclared_cmd_delegation()
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
    public async Task EvaluateAsync_SB06_INV_006_denies_encoded_powershell_command()
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
    public async Task EvaluateAsync_SB06_INV_007_denies_python_path_open_write()
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
        Assert.Contains("product target", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_SB06_INV_008_allows_declared_current_run_artifact_script_write()
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
    public async Task EvaluateAsync_SB06_INV_009_allows_declared_external_artifact_destination_write()
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
                declaredWritePaths: ["external-target/C/programovani/process-run/evidence/report.md"]),
            inspectedScriptContent:
                "[IO.File]::WriteAllText('external-target/C/programovani/process-run/evidence/report.md', 'ok')");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public async Task EvaluateAsync_SB06_INV_010_allows_product_mutation_step_script_with_declared_authority()
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
                declaredWritePaths: ["external-target/C/programovani/todo-summary/product/src/Program.cs"]),
            inspectedScriptContent:
                "Set-Content -Path 'external-target/C/programovani/todo-summary/product/src/Program.cs' -Value 'changed'");

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void ProcessScriptSideEffectAnalyzer_SB07_INV_001_detects_writes_and_child_scripts_without_runtime()
    {
        var analysis = ProcessScriptSideEffectAnalyzer.Analyze(
            AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
            "& './collect-evidence.ps1'\n'value' > 'artifacts/process-runs/process-run-001/evidence/report.txt'");

        Assert.True(analysis.HasWriteSignal);
        Assert.Contains(analysis.ChildScriptSignals, signal => signal.EndsWith("collect-evidence.ps1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessToolOperationAuthorizer_SB07_INV_001_denies_missing_operation_without_full_policy_runtime()
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
    [InlineData(ToolContractCatalog.BrowserClick, "CaptureRuntimeProof")]
    [InlineData("processes_step_transition", "ExecuteExternalAction")]
    public void ProcessToolOperationAuthorizer_SB01_INV_001_denies_governed_step_with_missing_operation_contract(
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
    public async Task EvaluateAsync_denies_direct_product_write_for_scaffold_tool_only_process_step()
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
                ["path"] = "external-target/C/programovani/candoitall-processes5-dotnet-cli-a/TodoSummary.sln",
                ["content"] = "Microsoft Visual Studio Solution File, Format Version 12.00"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/candoitall-processes5-dotnet-cli-a"],
            processScaffoldToolOnly: true);

        var decision = await policy.EvaluateAsync(context, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("scaffold step is tool-only", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_new", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvaluateAsync_allows_artifact_write_for_scaffold_tool_only_process_step()
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
                ["path"] = "artifacts/process-runs/process-run-001/02-solution-skeleton-change-set.md",
                ["content"] = "Created solution and requested project with scaffold tools."
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/candoitall-processes5-dotnet-cli-a"],
            processScaffoldToolOnly: true);

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
    public async Task EvaluateAsync_allows_dotnet_new_for_scaffold_tool_only_process_step()
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
                ["parentDirectory"] = "external-target/C/programovani",
                ["name"] = "candoitall-processes5-dotnet-cli-a",
                ["template"] = "console"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/candoitall-processes5-dotnet-cli-a"],
            processScaffoldToolOnly: true);

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
    public async Task EvaluateAsync_does_not_record_solution_template_as_project_scaffold_root()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var solutionContext = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner",
                ["name"] = "PocketMeetingCostPlanner",
                ["template"] = "sln"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);
        var appContext = CreateContext(
            "workspace_dotnet_new",
            ToolInvocationClassification.Mutation,
            isKnownTool: true,
            autoApprovalAllowed: true,
            approvalWrapperAvailable: false,
            arguments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner",
                ["name"] = "PocketMeetingCostPlanner",
                ["template"] = "blazor"
            },
            allowedExternalTargetAliases: ["external-target/C/programovani/dotnet/PocketMeetingCostPlanner"]);
        var conflictingProjectContext = appContext with
        {
            RedactedArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentDirectory"] = "external-target/C/programovani/dotnet/PocketMeetingCostPlanner",
                ["name"] = "PocketMeetingCostPlanner",
                ["template"] = "xunit"
            }
        };

        var solutionDecision = await policy.EvaluateAsync(solutionContext, CancellationToken.None);
        policy.RecordSuccessfulInvocation(solutionContext);
        var appDecision = await policy.EvaluateAsync(appContext, CancellationToken.None);
        policy.RecordSuccessfulInvocation(appContext);
        var conflictingProjectDecision = await policy.EvaluateAsync(conflictingProjectContext, CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Allow, solutionDecision.Kind);
        Assert.Equal(ToolInvocationDecisionKind.Allow, appDecision.Kind);
        Assert.Equal(ToolInvocationDecisionKind.Deny, conflictingProjectDecision.Kind);
        Assert.Contains("already scaffolded", conflictingProjectDecision.Reason, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("outside the current run boundary", decision.Reason, StringComparison.Ordinal);
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
        Assert.Contains("outside the current run boundary", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/LegacyWeatherLog/tests", decision.Reason, StringComparison.Ordinal);
        Assert.Contains("not the product parent", decision.Reason, StringComparison.Ordinal);
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
    public async Task EvaluateAsync_denies_managed_output_scaffold_for_external_target_process_run()
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
    public void Classify_SB02_INV_001_does_not_fallback_unknown_tools_to_read(string toolName)
    {
        var classification = AgentToolInvocationPolicyMetadata.Classify(toolName);

        Assert.Equal(ToolInvocationClassification.Unknown, classification);
    }

    [Fact]
    public void ToolPolicyMetadata_SB02_INV_002_classifies_high_risk_catalog_tools_explicitly()
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
        Assert.Equal(ToolInvocationClassification.Read, AgentToolInvocationPolicyMetadata.Classify(ToolContractCatalog.WorkspaceExecutionBoundary));
    }

    [Fact]
    public async Task EvaluateAsync_SB02_INV_003_denies_command_run_without_execute_external_action_operation()
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
    public void ToolCapabilityRegistry_SB02_INV_004_registers_every_known_catalog_tool()
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
    public void ToolCapabilityRegistry_SB02_INV_005_declares_static_operation_requirements_for_high_risk_tools()
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
    }

    [Fact]
    public void ToolCapabilityRegistry_SB02_INV_006_declares_side_effect_target_scope_and_proof_metadata()
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

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.BrowserClick, out var browserClick));
        Assert.Equal(ToolCapabilityBrowserProofRole.Interaction, browserClick.BrowserProofRole);
        Assert.False(browserClick.CanMutateProduct);
        Assert.True(browserClick.CanReadExternalTarget);
        Assert.Contains(ProcessOperationContractNames.ExternalProductTargetReadOnly, browserClick.TargetScopeRequirements);

        Assert.True(ToolCapabilityRegistry.TryResolve(ToolContractCatalog.BrowserTakeScreenshot, out var screenshot));
        Assert.Equal(ToolCapabilityBrowserProofRole.EvidenceCapture, screenshot.BrowserProofRole);
    }

    [Fact]
    public void ProjectStructureToolInventory_SB07_INV_001_classifies_all_runtime_project_structure_tools()
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
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeMove,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeRecompose,
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeReparent,
            AgentToolInvocationPolicyMetadata.ProjectStructureApprovalRequest,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreateRevision,
            AgentToolInvocationPolicyMetadata.ProjectStructureImport,
            AgentToolInvocationPolicyMetadata.ProjectStructureProjectLeaseAcquire,
            AgentToolInvocationPolicyMetadata.ProjectStructureRepoBranchLeaseAcquire,
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
    public async Task EvaluateAsync_SB02_INV_001_denies_validation_without_run_validation_operation()
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
    public async Task EvaluateAsync_SB02_INV_002_allows_validation_when_run_validation_operation_is_allowed()
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
    public async Task EvaluateAsync_SB02_INV_003_allows_bounded_dotnet_run_with_run_validation_operation()
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
    public async Task EvaluateAsync_SB02_INV_003_denies_kept_alive_runtime_launch_without_launch_runtime_operation()
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
    [InlineData(ToolContractCatalog.BrowserNavigate)]
    [InlineData(ToolContractCatalog.BrowserClick)]
    [InlineData(ToolContractCatalog.BrowserPressKey)]
    [InlineData(ToolContractCatalog.BrowserType)]
    public async Task EvaluateAsync_SB04_INV_001_denies_browser_tools_without_capture_runtime_proof_operation(string toolName)
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
    public async Task EvaluateAsync_SB04_INV_002_allows_browser_tools_with_capture_runtime_proof_operation(string toolName)
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
    public async Task EvaluateAsync_SB02_INV_004_allows_artifact_only_write_under_current_run_artifacts()
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
    public async Task EvaluateAsync_SB02_INV_005_allows_product_write_when_external_product_root_is_named_output()
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
    public async Task EvaluateAsync_SB02_INV_006_denies_copying_previous_run_product_into_current_product_target()
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
    public async Task EvaluateAsync_SB02_INV_007_denies_validation_against_previous_run_product_archive()
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
    public async Task EvaluateAsync_SB07_INV_001_denies_project_structure_mutation_without_execute_external_action()
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
    public async Task EvaluateAsync_SB07_INV_002_allows_project_structure_mutation_with_execute_external_action()
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
    public async Task EvaluateAsync_SB07_INV_003_allows_project_structure_read_without_execute_external_action()
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
        bool processScaffoldToolOnly = false,
        bool processAllowsProductMutation = true,
        IReadOnlyList<string>? processStepAllowedOperations = null,
        string processStepTargetScope = "",
        string inspectedScriptContent = "",
        string scriptInspectionFailure = "",
        string scriptSideEffectManifestJson = "")
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
            ApplicationApprovalAvailable: applicationApprovalAvailable,
            ProcessScaffoldToolOnly: processScaffoldToolOnly,
            ProcessAllowsProductMutation: processAllowsProductMutation,
            ProcessStepAllowedOperations: processStepAllowedOperations ?? ProcessOperationContractNames.AllOperations,
            ProcessStepTargetScope: processStepTargetScope,
            InspectedScriptContent: inspectedScriptContent,
            ScriptInspectionFailure: scriptInspectionFailure,
            ScriptSideEffectManifestJson: scriptSideEffectManifestJson);
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
