using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimePolicyIsolationTests
{
    [Fact]
    public void Tool_receipt_catalog_composes_generic_and_dotnet_semantics()
    {
        var catalog = CreateToolReceiptPolicyCatalog();
        var receipt = CreateToolReceipt("workspace_dotnet_new", "new blazorwasm --name SampleApp");

        var match = catalog.MatchRequirement(receipt, "template=blazorwasm");

        Assert.True(match.IsHandled);
        Assert.True(match.IsMatch);
        Assert.True(catalog.MatchRequirement(
            CreateToolReceipt(ToolContractCatalog.BrowserPressKey, "key=Enter"),
            BrowserInteractionToolReceiptPolicyContribution.InteractionProofRequirement).IsMatch);
        Assert.False(catalog.MatchRequirement(
            CreateToolReceipt(ToolContractCatalog.BrowserSnapshot, "filename=state.yml"),
            BrowserInteractionToolReceiptPolicyContribution.InteractionProofRequirement).IsMatch);
        Assert.True(catalog.IsProductMutationReceipt(CreateToolReceipt("workspace_write_file", "path=product/app.cs")));
        Assert.True(catalog.IsProductMutationReceipt(CreateToolReceipt("workspace_dotnet_new", "new blazorwasm")));
        Assert.False(catalog.IsProductMutationReceipt(CreateToolReceipt("workspace_pwsh_run_script", "path=scripts/read.ps1")));
        Assert.True(catalog.IsProductMutationReceipt(
            CreateToolReceipt("workspace_pwsh_run_script", "path=scripts/repair.ps1") with
            {
                DeclaredSideEffectMode = ToolExecutionSideEffectMode.ProductMutation
            }));
        Assert.True(catalog.IsProductMutationReceipt(
            CreateToolReceipt("workspace_python_run_file", "path=scripts/repair.py") with
            {
                DeclaredSideEffectMode = ToolExecutionSideEffectMode.ProductMutation
            }));
        Assert.True(catalog.IsProductValidationTool("workspace_dotnet_build"));
        Assert.False(catalog.IsProductValidationTool("workspace_npm_install"));
        Assert.False(catalog.IsProductMutationReceipt(CreateToolReceipt("browser_navigate", "url=https://example.test")));
    }

    [Fact]
    public void Tool_receipt_catalog_rejects_ambiguous_policy_ownership()
    {
        var catalog = new ProcessToolReceiptPolicyCatalog(
        [
            new AlwaysMatchingToolReceiptPolicyContribution(),
            new AlwaysMatchingToolReceiptPolicyContribution()
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.MatchRequirement(CreateToolReceipt("custom_tool", "custom"), "custom-requirement"));

        Assert.Contains("Multiple process tool receipt policies", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subprocess_contract_resolver_uses_template_supplied_contract_without_fallbacks()
    {
        var resolver = new ProcessSubprocessContractResolver();
        var launchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson] =
                """{"definitionKey":"example-child-process","parentProducedArtifactExpectationKey":"handoff"}"""
        };

        Assert.True(resolver.TryResolve(launchVariables, "implementation", out var contract));
        Assert.Equal("example-child-process", contract.DefinitionKey);
        Assert.False(resolver.TryResolve(new Dictionary<string, string>(), "implementation", out _));
    }

    [Fact]
    public void Processes_module_registers_policy_catalogs_at_composition_root()
    {
        var services = new ServiceCollection();

        services.AddProcessesModule(new ConfigurationBuilder().Build());

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessToolReceiptPolicyContribution));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessExecutionMetadataContribution));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessRuntimeToolPreflightContribution));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessCompletionDefectEvidenceContribution));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProcessLaunchVariableContributor) &&
                          descriptor.ImplementationType == typeof(WorkspaceProductTargetFilesystemStateLaunchVariableContributor));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProcessToolReceiptPolicyCatalog));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProcessSubprocessContractResolver));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessRuntimeToolPlanGuard));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProcessToolReceiptEvidenceGate));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProcessCompletionGateContribution) &&
                           descriptor.ImplementationType == typeof(WorkspaceProductFilesystemCompletionGateContribution));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProcessCompletionGateContribution) &&
                           descriptor.ImplementationType == typeof(DotNetSolutionContextCompletionGateContribution));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProcessCompletionGateContribution) &&
                           descriptor.ImplementationType == typeof(BrowserRuntimeLifecycleCompletionGateContribution));
    }

    [Fact]
    public void Runtime_routed_branch_cannot_be_selected_directly_by_agent()
    {
        var runId = ProcessRunId.New();
        var assignment = new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "implement-quality-repair",
            "repair",
            "repair",
            "Repair",
            "Agent",
            Guid.NewGuid().ToString("D"),
            "Repair",
            "Apply repair.",
            "sha256:readiness",
            "Test assignment.",
            [ArtifactSlotId.New()],
            [],
            [],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep] =
                    "{\"implement-quality-repair\":[\"repair-attempt-incomplete\"]}"
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            BranchOutcomeKey = "repair-attempt-incomplete",
            Reason = "No product mutation was attempted."
        };

        var issue = ProcessProductCompletionStateGate.ValidateRuntimeRoutedBranchWasNotSelectedDirectly(
            assignment,
            output);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.RuntimeRoutedBranchSelectedDirectly, issue.Code);
        Assert.Contains("not an executable agent decision", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Agent_selectable_repair_branch_is_not_rejected_as_runtime_routed()
    {
        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "implement-quality-repair",
            "repair",
            "repair",
            "Repair",
            "Agent",
            Guid.NewGuid().ToString("D"),
            "Repair",
            "Apply repair.",
            "sha256:readiness",
            "Test assignment.",
            [ArtifactSlotId.New()],
            [],
            [],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep] =
                    "{\"implement-quality-repair\":[\"repair-attempt-incomplete\"]}"
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            BranchOutcomeKey = "product-repair-applied",
            Reason = "Repair applied."
        };

        var issue = ProcessProductCompletionStateGate.ValidateRuntimeRoutedBranchWasNotSelectedDirectly(
            assignment,
            output);

        Assert.Null(issue);
    }

    [Fact]
    public void Completion_disposition_contract_allows_open_issues_only_for_declared_branch()
    {
        var launchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepCompletionDispositionJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepCompletionDisposition(
                    new ProcessRuntimeCompletionDisposition(
                        false,
                        ["repair-required"]))
        };

        Assert.True(ProcessRuntimeLaunchVariables.TryReadProcessStepCompletionDisposition(
            launchVariables,
            out var disposition));
        Assert.Equal(["repair-required"], disposition.OpenIssueBranchOutcomeKeys);
        Assert.True(ProcessRuntimeLaunchVariables.AllowsCompletedOutcomeWithOpenIssues(
            launchVariables,
            "repair-required"));
        Assert.False(ProcessRuntimeLaunchVariables.AllowsCompletedOutcomeWithOpenIssues(
            launchVariables,
            "accepted"));
    }

    [Fact]
    public void Runtime_owned_executor_contract_is_explicit_and_template_materializable()
    {
        var launchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = "dotnet.solution-setup",
            [ProcessRuntimeLaunchVariables.ProcessStepDeterministicToolPlanDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepDeterministicToolPlanDescriptor(
                    new ProcessRuntimeDeterministicToolPlanDescriptor(
                        "dotnet.create-project",
                        "DotNetSolutionCreate",
                        "SetupExecutionPlan",
                        [
                            new ProcessToolOperationExecutionPolicy(
                                "run-helper-script",
                                "workspace_pwsh_run_script",
                                ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable,
                                ProcessToolOperationFailureReconciliationPolicy.AuthoritativeReadbackConvergence)
                        ])),
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                    new ProcessRuntimeScriptHelperDescriptor(
                        "SetupScript",
                        "SetupScriptRef",
                        "SetupManifest",
                        "dotnet.create-project",
                        "DotNetSolutionCreate",
                        "SetupExecutionPlan",
                        [
                            new ProcessToolOperationExecutionPolicy(
                                "run-helper-script",
                                "workspace_pwsh_run_script",
                                ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable,
                                ProcessToolOperationFailureReconciliationPolicy.AuthoritativeReadbackConvergence)
                        ]))
        };

        Assert.True(ProcessRuntimeLaunchVariables.TryReadProcessStepRuntimeOwnedExecutorKey(
            launchVariables,
            out var executorKey));
        Assert.Equal("dotnet.solution-setup", executorKey);
        Assert.True(ProcessRuntimeLaunchVariables.TryReadProcessStepDeterministicToolPlanDescriptor(
            launchVariables,
            out var deterministicDescriptor));
        Assert.Equal("dotnet.create-project", deterministicDescriptor.PlanKey);
        Assert.Equal("SetupExecutionPlan", deterministicDescriptor.ExecutionPlanVariableName);
        Assert.Single(deterministicDescriptor.OperationPolicies);
        Assert.True(ProcessRuntimeLaunchVariables.TryReadProcessStepScriptHelperDescriptor(
            launchVariables,
            out var descriptor));
        Assert.Equal("dotnet.create-project", descriptor.PlanKey);
        Assert.Equal("DotNetSolutionCreate", descriptor.PlanKind);
        Assert.Equal("SetupExecutionPlan", descriptor.ExecutionPlanVariableName);
        var operation = Assert.Single(descriptor.OperationPolicies!);
        Assert.Equal("run-helper-script", operation.OperationKey);
        Assert.Equal(
            ProcessToolOperationFailureReconciliationPolicy.AuthoritativeReadbackConvergence,
            operation.FailureReconciliation);
    }

    [Theory]
    [InlineData("Denied", "process.adapter.runtime_owned_execution_denied")]
    [InlineData("TimedOut", "process.adapter.runtime_owned_execution_timed_out")]
    public void Runtime_owned_boundary_failures_remain_unsafe_despite_repeatability(
        string outcome,
        string expectedCode)
    {
        var failure = ProcessRuntimeOwnedStepFailures.ResolveExecutionFailure(
            outcome,
            ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable);

        Assert.Equal(expectedCode, failure.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, failure.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Unknown, failure.Idempotency);
    }

    private static ProcessToolReceiptPolicyCatalog CreateToolReceiptPolicyCatalog()
        => new(
        [
            new GenericWorkspaceToolReceiptPolicyContribution(),
            new BrowserInteractionToolReceiptPolicyContribution(),
            new DotNetToolReceiptPolicyContribution()
        ]);

    private static ToolExecutionReceiptRecord CreateToolReceipt(string toolName, string requestSummary)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-provider",
            toolName,
            "ReadOnlyWorkspace",
            "NotRequired",
            "Policy isolation test.",
            requestSummary,
            ".",
            "Succeeded (exit code 0).",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class AlwaysMatchingToolReceiptPolicyContribution : IProcessToolReceiptPolicyContribution
    {
        public bool IsProductMutationReceipt(ToolExecutionReceiptRecord receipt)
            => false;

        public bool IsProductValidationTool(string toolName)
            => false;

        public ProcessToolReceiptRequirementMatch MatchRequirement(
            ToolExecutionReceiptRecord receipt,
            string requirement)
            => ProcessToolReceiptRequirementMatch.Matched;

        public IEnumerable<string> EnumerateRequirementSearchTerms(string requirement)
            => [];
    }
}
