using System.Text.Json;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRequiredRuntimeToolNamesTests
{
    [Fact]
    public void FromProductCompletionRequiredToolReceipts_filters_receipt_predicates_from_runtime_tool_names()
    {
        var requiredReceipts = JsonSerializer.Serialize(new[]
        {
            "template=sln",
            "template=blazorwasm",
            "workspace_dotnet_new|name=Calculator,parentDirectory=external-target/C/programovani/dotnet,template=sln",
            "workspace-pwsh-run-script",
            "browser_take_screenshot",
            "project_structure_asset_create",
            "exit=0"
        });

        var toolNames = ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(requiredReceipts);

        Assert.Equal(
            [
                "browser_take_screenshot",
                "project_structure_asset_create",
                "workspace_dotnet_new",
                "workspace_pwsh_run_script"
            ],
            toolNames);
    }

    [Fact]
    public void Current_blazor_delivery_template_keeps_browser_proof_selector_out_of_runtime_tool_contract()
    {
        var definition = new ProcessTemplatePackLoader().LoadDefinition("blazor-app-delivery");
        var requiredReceipts = definition.Steps
            .SelectMany(step => step.CompletionPolicy?.RequiredProductToolReceipts ?? [])
            .ToArray();

        var toolNames = ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(
            JsonSerializer.Serialize(requiredReceipts));

        Assert.Contains(
            requiredReceipts,
            receipt => string.Equals(
                receipt.ToolName,
                ProcessProductToolReceiptRequirements.BrowserInteractionProof,
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker, toolNames);
        Assert.Contains("browser_navigate", toolNames);
        Assert.DoesNotContain(
            ProcessProductToolReceiptRequirements.BrowserInteractionProof,
            toolNames,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromUnconditionalProductCompletionRequiredToolReceipts_excludes_branch_scoped_rules()
    {
        var requiredReceipts = JsonSerializer.Serialize(new object[]
        {
            new
            {
                ToolReceipt = "workspace_dotnet_restore|exit=0"
            },
            new
            {
                ToolReceipt = "workspace_dotnet_test|exit=0",
                ApplicableBranchOutcomeKeys = new[] { "quality-accepted" }
            },
            new
            {
                ToolReceipt = "browser_take_screenshot",
                SkippedBranchOutcomeKeys = new[] { "repair-required" }
            }
        });

        var toolNames = ProcessRequiredRuntimeToolNames
            .FromUnconditionalProductCompletionRequiredToolReceipts(requiredReceipts);

        Assert.Equal(["workspace_dotnet_restore"], toolNames);
    }

    [Fact]
    public void FromUnconditionalCapabilityScope_excludes_branch_scoped_receipts()
    {
        var capabilityScope = new ProcessCapabilityScope
        {
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    Key = "restore",
                    ToolName = "workspace_dotnet_restore"
                },
                new ProcessRequiredToolReceipt
                {
                    Key = "quality-screenshot",
                    ToolName = "browser_take_screenshot",
                    ApplicableBranchOutcomeKeys = ["quality-accepted"]
                }
            ]
        };

        var allToolNames = ProcessRequiredRuntimeToolNames.FromCapabilityScope(capabilityScope);
        var unconditionalToolNames = ProcessRequiredRuntimeToolNames
            .FromUnconditionalCapabilityScope(capabilityScope);

        Assert.Equal(["browser_take_screenshot", "workspace_dotnet_restore"], allToolNames);
        Assert.Equal(["workspace_dotnet_restore"], unconditionalToolNames);
    }

    [Fact]
    public void FromCapabilityScope_replaces_invalid_tool_name_with_non_disclosing_contract_marker()
    {
        const string malformedToolName = "secret: C:\\private\\token=raw-capability-value";
        var capabilityScope = new ProcessCapabilityScope
        {
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    Key = "malformed-runtime-tool",
                    ToolName = malformedToolName
                }
            ]
        };

        var toolNames = ProcessRequiredRuntimeToolNames.FromCapabilityScope(capabilityScope);

        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], toolNames);
        Assert.DoesNotContain(malformedToolName, JsonSerializer.Serialize(toolNames), StringComparison.Ordinal);
        Assert.DoesNotContain("raw-capability-value", JsonSerializer.Serialize(toolNames), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FromCapabilityScope_rejects_undefined_required_receipt_enums(bool invalidKind)
    {
        var receipt = new ProcessRequiredToolReceipt
        {
            Key = "invalid-enum-runtime-tool",
            ToolName = "workspace_python_run_file",
            Kind = invalidKind
                ? (ProcessRequiredToolReceiptKind)999
                : ProcessRequiredToolReceiptKind.RuntimeToolName,
            Activation = invalidKind
                ? ProcessRequiredToolReceiptActivation.Always
                : (ProcessRequiredToolReceiptActivation)999
        };

        var toolNames = ProcessRequiredRuntimeToolNames.FromCapabilityScope(new ProcessCapabilityScope
        {
            RequiredReceipts = [receipt]
        });

        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], toolNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capability_scope_normalization_preserves_invalid_directive_as_non_disclosing_contract_failure(
        bool invalidDirectiveKind)
    {
        var scope = new ProcessCapabilityScope
        {
            Directives =
            [
                new ProcessCapabilityScopeDirective
                {
                    Kind = invalidDirectiveKind
                        ? (ProcessCapabilityScopeDirectiveKind)999
                        : ProcessCapabilityScopeDirectiveKind.AllowOnly,
                    Target = new ProcessCapabilityScopeTarget
                    {
                        Kind = ProcessCapabilityScopeTargetKind.Unspecified
                    }
                }
            ]
        };

        var normalized = ProcessCapabilityScope.Normalize(scope);
        var toolNames = ProcessRequiredRuntimeToolNames.FromCapabilityScope(scope);

        Assert.False(normalized.IsEmpty);
        Assert.Empty(normalized.Directives);
        Assert.Equal(
            ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker,
            Assert.Single(normalized.RequiredReceipts).ToolName);
        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], toolNames);
    }

    [Theory]
    [InlineData("{ malformed-json")]
    [InlineData("[]")]
    [InlineData("\"unexpected\"")]
    public void Product_completion_by_step_container_rejects_malformed_or_non_object_shape(string byStep)
    {
        var required = ProcessProductCompletionRuleParser
            .ResolveUnconditionalProductCompletionRequiredToolReceipts(
                new Dictionary<string, string>
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = byStep
                },
                "execute");

        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], required);
    }

    [Theory]
    [InlineData("{\"unexpected\":\"value\"}")]
    [InlineData("[\"workspace_dotnet_build\",{\"unexpected\":\"value\"}]")]
    [InlineData("null")]
    public void Product_completion_receipts_reject_unrecognized_or_mixed_json_shapes(string value)
    {
        var required = ProcessProductCompletionRuleParser
            .ResolveUnconditionalProductCompletionRequiredToolReceipts(
                new Dictionary<string, string>
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = value
                },
                "execute");

        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], required);
    }

    [Fact]
    public void NormalizeDeclaredRuntimeToolNames_rejects_entire_contract_when_any_name_is_not_canonical()
    {
        const string malformedToolName = "secret: C:\\private\\token=raw-template-value";

        var toolNames = ProcessRequiredRuntimeToolNames.NormalizeDeclaredRuntimeToolNames(
            ["workspace_dotnet_build", malformedToolName]);

        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], toolNames);
        Assert.DoesNotContain(malformedToolName, JsonSerializer.Serialize(toolNames), StringComparison.Ordinal);
        Assert.DoesNotContain("raw-template-value", JsonSerializer.Serialize(toolNames), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Product_completion_required_receipts_replace_malformed_tool_with_non_disclosing_marker(
        bool unconditional)
    {
        const string malformedToolName = "workspace_python_run_file!secret=C:\\private\\raw-product-token";
        var value = JsonSerializer.Serialize(new[] { malformedToolName });

        var toolNames = unconditional
            ? ProcessRequiredRuntimeToolNames.FromUnconditionalProductCompletionRequiredToolReceipts(value)
            : ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(value);

        var serialized = JsonSerializer.Serialize(toolNames);
        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], toolNames);
        Assert.DoesNotContain(malformedToolName, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-product-token", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Unconditional_product_completion_validation_rejects_malformed_branch_scoped_tool()
    {
        const string malformedToolName = "workspace_python_run_file!secret=/home/private/raw-branch-token";
        var value = JsonSerializer.Serialize(new object[]
        {
            new
            {
                toolReceipt = malformedToolName,
                applicableBranchOutcomeKeys = new[] { "quality-accepted" }
            }
        });

        var toolNames = ProcessRequiredRuntimeToolNames
            .FromUnconditionalProductCompletionRequiredToolReceipts(value);

        var serialized = JsonSerializer.Serialize(toolNames);
        Assert.Equal([ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker], toolNames);
        Assert.DoesNotContain(malformedToolName, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-branch-token", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_completion_receipts_reject_oversized_input_before_materializing_candidates()
    {
        var oversizedJson = JsonSerializer.Serialize(Enumerable
            .Range(0, ProcessRequiredRuntimeToolNames.MaximumCount + 1)
            .Select(index => $"workspace_test_tool_{index}"));
        var oversizedText = new string(
            'x',
            ProcessRequiredRuntimeToolNames.MaximumSerializedReceiptContractLength + 1);

        Assert.Equal(
            [ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker],
            ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(oversizedJson));
        Assert.Equal(
            [ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker],
            ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(oversizedText));
    }

    [Fact]
    public void Capability_scope_rejects_oversized_selector_and_branch_contracts()
    {
        var scope = new ProcessCapabilityScope
        {
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    Key = "oversized-selector",
                    ToolName = new string('x', ProcessRequiredRuntimeToolNames.MaximumNameLength + 1),
                    ApplicableBranchOutcomeKeys = Enumerable
                        .Range(0, ProcessRequiredRuntimeToolNames.MaximumBranchOutcomeCount + 1)
                        .Select(index => $"branch-{index}")
                        .ToList()
                }
            ]
        };

        Assert.Equal(
            [ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker],
            ProcessRequiredRuntimeToolNames.FromCapabilityScope(scope));
    }

    [Fact]
    public void Capability_scope_rejects_oversized_narratives_and_generated_receipt_keys()
    {
        var oversizedReasonScope = new ProcessCapabilityScope
        {
            Directives =
            [
                new ProcessCapabilityScopeDirective
                {
                    Kind = ProcessCapabilityScopeDirectiveKind.Require,
                    Target = new ProcessCapabilityScopeTarget
                    {
                        Kind = ProcessCapabilityScopeTargetKind.RuntimeToolName,
                        Value = "workspace_read_file"
                    },
                    Reason = new string('r', ProcessPublicReceiptTextPolicy.MaximumPublicMessageLength + 1)
                }
            ]
        };
        var generatedKeyScope = new ProcessCapabilityScope
        {
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    ToolName = "workspace_read_file",
                    ApplicableBranchOutcomeKeys = Enumerable
                        .Range(0, ProcessRequiredRuntimeToolNames.MaximumBranchOutcomeCount)
                        .Select(index => $"branch-{index:D2}-{new string('x', 100)}")
                        .ToList()
                }
            ]
        };

        Assert.Equal(
            [ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker],
            ProcessRequiredRuntimeToolNames.FromCapabilityScope(oversizedReasonScope));
        Assert.Equal(
            [ProcessRequiredRuntimeToolNames.InvalidRuntimeToolContractMarker],
            ProcessRequiredRuntimeToolNames.FromCapabilityScope(generatedKeyScope));
    }

    [Fact]
    public void Missing_receipt_summary_redacts_bounded_secret_and_physical_path_narratives()
    {
        const string secret = "password=raw-secret";
        const string physicalPath = @"C:\private\workspace\token.txt";
        var summary = ProcessRequiredToolReceiptGate.FormatMissingSummary(
        [
            new ProcessRequiredToolReceipt
            {
                Key = "required-read",
                ToolName = "workspace_read_file",
                Reason = $"{secret} at {physicalPath}"
            }
        ]);

        Assert.DoesNotContain(secret, summary, StringComparison.Ordinal);
        Assert.DoesNotContain(physicalPath, summary, StringComparison.Ordinal);
        Assert.True(summary.Length <= ProcessPublicReceiptTextPolicy.MaximumPublicMessageLength);
    }
}
