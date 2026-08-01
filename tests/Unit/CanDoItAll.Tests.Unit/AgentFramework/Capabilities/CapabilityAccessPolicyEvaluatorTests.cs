using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Capabilities.Templates;

namespace CanDoItAll.Tests.Unit.AgentFramework.Capabilities;

public sealed class CapabilityAccessPolicyEvaluatorTests
{
    [Fact]
    public void Evaluate_DenyAllPolicy_AllowsOnlyExplicitAllowRuleMatches()
    {
        var allowedTool = CreateToolDescriptor("allowed-tool", "allowed_tool");
        var deniedTool = CreateToolDescriptor("denied-tool", "denied_tool");
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("allow-selected-tool"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create("allowed_tool")),
                "Only the selected tool is allowed.")
        ],
        CapabilityAccessDefaultEffect.DenyAll,
        CapabilityAccessScope.ProcessStep,
        "Process step allows only selected capabilities.");

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [allowedTool, deniedTool],
            RequiredCapabilities: [],
            [policy],
            "default-deny-test"));

        Assert.Equal([allowedTool.Identity], result.AllowedCapabilities.Select(capability => capability.Identity));
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(deniedTool.Identity, diagnostic.Identity);
        Assert.Equal(CapabilityDiagnosticCategory.AccessPolicy, diagnostic.Category);
        Assert.Equal(CapabilityAccessScope.ProcessStep, diagnostic.Scope);
        Assert.Contains("allows only", diagnostic.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DenyAllPolicy_TreatsRequireRuleAsAllowedMatch()
    {
        var requiredTool = CreateToolDescriptor("required-tool", "required_tool");
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("require-selected-tool"),
                CapabilityAccessEffect.Require,
                CapabilityAccessScope.RuntimeOverride,
                CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create("required_tool")),
                "Required selected tool.")
        ],
        CapabilityAccessDefaultEffect.DenyAll);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [requiredTool],
            RequiredCapabilities: [],
            [policy],
            "require-default-deny-test"));

        Assert.Equal([requiredTool.Identity], result.AllowedCapabilities.Select(capability => capability.Identity));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Evaluate_DenyRule_OverridesAllowRuleUnderDenyAllPolicy()
    {
        var mutationTool = CreateToolDescriptor(
            "mutation-tool",
            "mutation_tool",
            new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Mutation });
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("allow-all-tools"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByKind(CapabilityKind.Tool),
                "Allow all tools in this scope."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-mutation-tools"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(CapabilityOperationClassification.Mutation),
                "Mutation tools are suppressed.")
        ],
        CapabilityAccessDefaultEffect.DenyAll);

        var result = new CapabilityAccessPolicyEvaluator().Evaluate(new CapabilityAccessEvaluationContext(
            [mutationTool],
            RequiredCapabilities: [],
            [policy],
            "deny-wins-test"));

        Assert.Empty(result.AllowedCapabilities);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityRuleId.Create("deny-mutation-tools"), diagnostic.RuleId);
        Assert.Equal("Mutation tools are suppressed.", diagnostic.Reason);
    }

    [Fact]
    public void Compile_PreservesDefaultEffect()
    {
        var compiler = new CapabilityAccessPolicyTemplateCompiler();

        var result = compiler.Compile(
            new CapabilityAccessPolicyTemplateDto
            {
                DefaultEffect = "denyAll",
                Rules =
                [
                    new CapabilityAccessRuleTemplateDto
                    {
                        Id = "allow-runtime-tool",
                        Effect = "allow",
                        Scope = "processStep",
                        Selector = new CapabilitySelectorTemplateDto
                        {
                            Kind = "runtimeToolName",
                            Value = "selected_tool"
                        },
                        Reason = "Selected tool remains available."
                    }
                ]
            },
            TemplatePath.Create("Templates/Capabilities/policies/unit-test.json"));

        Assert.True(result.ValidationResult.IsValid);
        Assert.NotNull(result.Policy);
        Assert.Equal(CapabilityAccessDefaultEffect.DenyAll, result.Policy!.DefaultEffect);
    }

    private static CapabilityExposureDescriptor CreateToolDescriptor(
        string capabilityKey,
        string runtimeToolName,
        IReadOnlySet<CapabilityOperationClassification>? operationClassifications = null)
    {
        var runtimeName = RuntimeToolName.Create(runtimeToolName);
        return new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.Tool, CapabilityKey.Create(capabilityKey)),
            capabilityKey,
            "Test capability.",
            ImplementationKey.Create("test." + capabilityKey.Replace('-', '.')),
            runtimeName,
            McpServerKey: null,
            McpToolName: null,
            Tags: new HashSet<CapabilityTag> { CapabilityTag.Create("tool") },
            OperationClassifications: operationClassifications ?? new HashSet<CapabilityOperationClassification> { CapabilityOperationClassification.Read },
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false),
            CapabilityAvailabilityState.Available,
            SourcePath: null);
    }
}
