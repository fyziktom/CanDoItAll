using System.Text.Json;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessCapabilityScopeContractTests
{
    [Fact]
    public void Process_template_json_deserializes_capability_scope_contract()
    {
        const string json = """
            {
              "key": "management-review",
              "displayName": "Management review",
              "summary": "Coordinate management-only review.",
              "steps": [
                {
                  "key": "triage",
                  "title": "Triage",
                  "capabilityScope": {
                    "directives": [
                      {
                        "kind": "allowOnly",
                        "target": {
                          "kind": "runtimeToolProviderKey",
                          "value": "management.provider"
                        },
                        "reason": "Management-only step."
                      }
                    ],
                    "instructionFragments": [
                      {
                        "key": "management-only",
                        "title": "Management-only scope",
                        "content": "Coordinate status and staffing. Do not implement product changes."
                      }
                    ],
                    "requiredReceipts": [
                      {
                        "key": "browser-proof",
                        "kind": "runtimeToolName",
                        "toolName": "browser_take_screenshot",
                        "activation": "whenLaunchContextDeclaresTool",
                        "reason": "Browser proof is required only for UI launches."
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var definition = JsonSerializer.Deserialize(
            json,
            ProcessTemplateJsonContext.Default.ProcessTemplateDefinitionDocument);

        Assert.NotNull(definition);
        var step = Assert.Single(definition!.Steps);
        var directive = Assert.Single(step.CapabilityScope.Directives);
        var instruction = Assert.Single(step.CapabilityScope.InstructionFragments);
        var receipt = Assert.Single(step.CapabilityScope.RequiredReceipts);
        Assert.Equal(ProcessCapabilityScopeDirectiveKind.AllowOnly, directive.Kind);
        Assert.Equal(ProcessCapabilityScopeTargetKind.RuntimeToolProviderKey, directive.Target.Kind);
        Assert.Equal("management.provider", directive.Target.Value);
        Assert.Equal("Management-only scope", instruction.Title);
        Assert.Contains("Do not implement product changes", instruction.Content, StringComparison.Ordinal);
        Assert.Equal("browser-proof", receipt.Key);
        Assert.Equal(ProcessRequiredToolReceiptKind.RuntimeToolName, receipt.Kind);
        Assert.Equal("browser_take_screenshot", receipt.ToolName);
        Assert.Equal(ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool, receipt.Activation);
    }

    [Fact]
    public void Dotnet_ui_screenshot_template_scopes_development_image_guidance_to_storage_step()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-ui-screenshot-writeback");
        var applicabilityStep = definition.Steps.Single(step =>
            string.Equals(step.Key, "resolve-ui-screenshot-applicability", StringComparison.Ordinal));
        var storageStep = definition.Steps.Single(step =>
            string.Equals(step.Key, "store-ui-screenshots", StringComparison.Ordinal));

        Assert.Contains(applicabilityStep.CapabilityScope.Directives, directive =>
            directive.Kind == ProcessCapabilityScopeDirectiveKind.Deny &&
            directive.Target.Kind == ProcessCapabilityScopeTargetKind.CapabilityIdentity &&
            string.Equals(directive.Target.Value, "Skill", StringComparison.Ordinal) &&
            string.Equals(directive.Target.SecondaryValue, "development-image-analysis-guidance-inline-skill", StringComparison.Ordinal));
        Assert.Contains(applicabilityStep.CapabilityScope.Directives, directive =>
            directive.Kind == ProcessCapabilityScopeDirectiveKind.Deny &&
            directive.Target.Kind == ProcessCapabilityScopeTargetKind.CapabilityTag &&
            string.Equals(directive.Target.Value, "development", StringComparison.Ordinal));
        Assert.DoesNotContain(applicabilityStep.CapabilityScope.InstructionFragments, fragment =>
            fragment.Content.Contains("development-image-analysis-guidance-inline-skill", StringComparison.Ordinal));
        Assert.Contains(applicabilityStep.CapabilityScope.InstructionFragments, fragment =>
            string.Equals(fragment.Key, "pre-qa-screenshot-applicability-context", StringComparison.Ordinal));

        var requiredCapabilityKeys = storageStep.CapabilityScope.Directives
            .Where(directive =>
                directive.Kind == ProcessCapabilityScopeDirectiveKind.Require &&
                directive.Target.Kind == ProcessCapabilityScopeTargetKind.CapabilityIdentity)
            .Select(directive => directive.Target.SecondaryValue)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(requiredCapabilityKeys);
        var instruction = Assert.Single(storageStep.CapabilityScope.InstructionFragments);
        Assert.Equal("development-ui-screenshot-image-analysis", instruction.Key);
        Assert.Contains("software-delivery visual evidence", instruction.Content, StringComparison.Ordinal);
        Assert.Contains("Do not treat this as generic image interpretation", instruction.Content, StringComparison.Ordinal);

        var storageReceiptTools = storageStep.CapabilityScope.RequiredReceipts
            .Select(receipt => receipt.ToolName)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("workspace_inspect_image", storageReceiptTools);
        Assert.Contains("workspace_analyze_image", storageReceiptTools);
        Assert.Contains("workspace_analyze_images", storageReceiptTools);
        Assert.Contains("project_structure_node_create", storageReceiptTools);
        Assert.Contains("project_structure_asset_create", storageReceiptTools);
        Assert.All(storageStep.CapabilityScope.RequiredReceipts, receipt =>
            Assert.Equal(ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool, receipt.Activation));
    }

    [Fact]
    public void Software_delivery_qa_steps_declare_conditional_browser_and_image_receipts()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("software-delivery");
        var qaValidationStep = definition.Steps.Single(step =>
            string.Equals(step.Key, "qa-validation", StringComparison.Ordinal));
        var qaRecheckStep = definition.Steps.Single(step =>
            string.Equals(step.Key, "qa-recheck", StringComparison.Ordinal));

        AssertRequiredReceiptTools(qaValidationStep.CapabilityScope.RequiredReceipts);
        AssertRequiredReceiptTools(qaRecheckStep.CapabilityScope.RequiredReceipts);
    }

    [Fact]
    public void Dotnet_development_slice_validation_steps_declare_current_run_validation_receipts()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-development-slice");
        var addTestsStep = definition.Steps.Single(step =>
            string.Equals(step.Key, "add-tests-and-proof", StringComparison.Ordinal));
        var recheckStep = definition.Steps.Single(step =>
            string.Equals(step.Key, "add-tests-recheck", StringComparison.Ordinal));

        AssertDotNetValidationReceiptTools(addTestsStep.CapabilityScope.RequiredReceipts);
        AssertDotNetValidationReceiptTools(recheckStep.CapabilityScope.RequiredReceipts);
    }

    [Fact]
    public void Dotnet_quality_repair_diagnosis_declares_current_run_owning_source_receipt()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("dotnet-quality-repair");
        var diagnosisStep = definition.Steps.Single(step =>
            string.Equals(step.Key, "diagnose-quality-failure", StringComparison.Ordinal));

        var receipt = Assert.Single(diagnosisStep.CapabilityScope.RequiredReceipts);
        Assert.Equal("read-owning-product-source", receipt.Key);
        Assert.Equal(ProcessRequiredToolReceiptKind.RuntimeToolName, receipt.Kind);
        Assert.Equal("workspace_read_file", receipt.ToolName);
        Assert.Equal(ProcessRequiredToolReceiptPurpose.DefectEvidence, receipt.Purpose);
        Assert.Equal(ProcessRequiredToolReceiptActivation.Always, receipt.Activation);
        Assert.True(receipt.RequireCurrentRun);
        Assert.True(receipt.RequireSuccessfulExit);

        var instruction = Assert.Single(diagnosisStep.CapabilityScope.InstructionFragments);
        Assert.Equal("ground-owning-source-before-diagnosis-output", instruction.Key);
        Assert.Contains("before writing the diagnosis artifact", instruction.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_read_file", instruction.Content, StringComparison.Ordinal);
    }

    private static void AssertRequiredReceiptTools(IReadOnlyList<ProcessRequiredToolReceipt> receipts)
    {
        var receiptTools = receipts
            .Select(receipt => receipt.ToolName)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_run", receiptTools);
        Assert.Contains("browser_navigate", receiptTools);
        Assert.Contains("browser_snapshot", receiptTools);
        Assert.Contains("browser_take_screenshot", receiptTools);
        Assert.Contains("browser_console_messages", receiptTools);
        Assert.Contains("workspace_dotnet_stop", receiptTools);
        Assert.Contains("workspace_inspect_image", receiptTools);
        Assert.Contains("workspace_analyze_image", receiptTools);
        Assert.Contains("workspace_analyze_images", receiptTools);
        Assert.All(receipts, receipt =>
        {
            Assert.Equal(ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool, receipt.Activation);
            Assert.Equal(ProcessRequiredToolReceiptPurpose.AcceptanceProof, receipt.Purpose);
            Assert.Equal(["quality-accepted"], receipt.ApplicableBranchOutcomeKeys);
        });
    }

    private static void AssertDotNetValidationReceiptTools(IReadOnlyList<ProcessRequiredToolReceipt> receipts)
    {
        var receiptTools = receipts
            .Select(receipt => receipt.ToolName)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_restore", receiptTools);
        Assert.Contains("workspace_dotnet_build", receiptTools);
        Assert.Contains("workspace_dotnet_test", receiptTools);
        Assert.All(receipts, receipt =>
        {
            Assert.Equal(ProcessRequiredToolReceiptActivation.Always, receipt.Activation);
            Assert.True(receipt.RequireCurrentRun);
            Assert.False(receipt.RequireSuccessfulExit);
        });
    }
}
