using System.Runtime.CompilerServices;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentRuntimeHardeningStaticRegressionTests
{
    [Fact]
    public void Assistant_message_is_created_after_structured_output_validation()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Core",
            "Execution",
            "AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs");

        const string validationCall = "runtimeResponse = await ValidateMachineOutputBeforeCompletionAsync(";
        const string assistantMessageCreation = "var assistantMessage = session is null";
        var firstValidation = source.IndexOf(validationCall, StringComparison.Ordinal);
        var firstAssistant = source.IndexOf(assistantMessageCreation, StringComparison.Ordinal);
        var secondValidation = source.IndexOf(validationCall, firstValidation + validationCall.Length, StringComparison.Ordinal);
        var secondAssistant = source.IndexOf(assistantMessageCreation, firstAssistant + assistantMessageCreation.Length, StringComparison.Ordinal);

        Assert.True(firstValidation >= 0);
        Assert.True(secondValidation >= 0);
        Assert.True(firstAssistant >= 0);
        Assert.True(secondAssistant >= 0);
        Assert.True(firstValidation < firstAssistant);
        Assert.True(secondValidation < secondAssistant);
    }

    [Fact]
    public void Process_step_execution_request_sets_required_finalizer_policy()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.Execution.cs");

        Assert.DoesNotContain("MetadataJson: \"{}\"", source, StringComparison.Ordinal);
        Assert.Contains("AgentFinalizerMode.Required", source, StringComparison.Ordinal);
        Assert.Contains("ExecutionInvocationMetadata.DefaultGovernedRepairAttempts", source, StringComparison.Ordinal);
        Assert.Contains("StructuredOutput: ProcessStepOutcomeStructuredOutputContract", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_middleware_blocks_required_approval_when_no_effective_approval_path_exists()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.AgentFactory.cs");

        Assert.Contains("AgentToolPolicyBlockGuard.ThrowIfBlocked", source, StringComparison.Ordinal);
        Assert.Contains("policyContext.HasEffectiveApprovalPath", source, StringComparison.Ordinal);
        Assert.Contains("agentframework.tool_approval_effective", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Maf_middleware_uses_dedicated_policy_block_exception()
    {
        var runtimeSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.AgentFactory.cs");
        var policySource = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Core",
            "ToolPolicy",
            "AgentToolInvocationPolicy.cs");

        Assert.Contains("AgentToolPolicyBlockedException", policySource, StringComparison.Ordinal);
        Assert.Contains("ToolInvocationDecisionKind DecisionKind", policySource, StringComparison.Ordinal);
        Assert.Contains("AgentToolPolicyBlockGuard.ThrowIfBlocked", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPolicyException", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("exception is InvalidOperationException or NotSupportedException", runtimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_build_uses_execution_options_for_finalizer_mode()
    {
        var runtimeSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntime.AgentFactory.cs");
        var executionSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Core",
            "Execution",
            "AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs");

        Assert.Contains("AgentRuntimeExecutionOptions", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("runtimeOptions.FinalizerMode", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("CreateRuntimeExecutionOptions", executionSource, StringComparison.Ordinal);
        Assert.Contains("AgentFinalizerPolicies.ResolveMode", executionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_dispatch_has_explicit_process_step_outcome_context_validation()
    {
        var validationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.OutputValidation.cs");
        var toolValidationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ToolValidation.cs");

        Assert.Contains("ValidateProcessStepOutcomeContext", validationSource, StringComparison.Ordinal);
        Assert.Contains("process.step_outcome.context.branch_required", validationSource, StringComparison.Ordinal);
        Assert.Contains("process.step_outcome.context.evidence_refs_required", validationSource, StringComparison.Ordinal);
        Assert.Contains("ValidateProcessStepOutcomeContext(", toolValidationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_document_does_not_reference_missing_hardening_test_classes()
    {
        var docs = ReadRepositoryFile("docs", "agent-runtime-hardening-verification.md");
        var testRoot = Path.Combine(FindRepositoryRoot(), "tests", "CanDoItAll.Tests.Unit");
        var namedTestClasses = new[]
        {
            "AgentFinalizerPolicyTests",
            "AgentToolInvocationPolicyTests",
            "ProviderFeatureMatrixTests",
            "AgentRuntimeHardeningStaticRegressionTests",
            "AgentOutputContractTests"
        };

        foreach (var className in namedTestClasses)
        {
            Assert.True(File.Exists(Path.Combine(testRoot, $"{className}.cs")), className);
            Assert.Contains(className, docs, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Dispatch_recovery_and_proof_stay_domain_neutral()
    {
        var directiveSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.RecoveryDirective.cs");
        var providerSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs");
        var proofSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ImplementationProof.cs");

        Assert.DoesNotContain("BuildCalculatorRecoveryFocusGuidance", directiveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCalculatorRecoveryChecklist", directiveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildBlazorBuildRecoveryGuidance", directiveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CalculatorEngine", directiveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Components/Pages/Home.razor", directiveSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Calculator", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Blazor", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Calculator", proofSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Blazor", proofSource, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet", proofSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Seeded_inline_skills_do_not_embed_sample_specific_workloads()
    {
        var seedAssetRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.AgentFramework.Persistence",
            "SeedAssets");
        var searchableText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(seedAssetRoot, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("calculator", searchableText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SimpleCalculatorApp", searchableText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("office-order", searchableText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mouser", searchableText, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
