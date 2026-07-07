using System.Text.Json;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

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
        Assert.Equal(ProcessCapabilityScopeDirectiveKind.AllowOnly, directive.Kind);
        Assert.Equal(ProcessCapabilityScopeTargetKind.RuntimeToolProviderKey, directive.Target.Kind);
        Assert.Equal("management.provider", directive.Target.Value);
        Assert.Equal("Management-only scope", instruction.Title);
        Assert.Contains("Do not implement product changes", instruction.Content, StringComparison.Ordinal);
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
        Assert.Empty(applicabilityStep.CapabilityScope.InstructionFragments);

        var requiredCapabilityKeys = storageStep.CapabilityScope.Directives
            .Where(directive =>
                directive.Kind == ProcessCapabilityScopeDirectiveKind.Require &&
                directive.Target.Kind == ProcessCapabilityScopeTargetKind.CapabilityIdentity)
            .Select(directive => directive.Target.SecondaryValue)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("development-image-analysis-guidance-inline-skill", requiredCapabilityKeys);
        Assert.Contains("workspace-inspect-image", requiredCapabilityKeys);
        Assert.Contains("workspace-analyze-image", requiredCapabilityKeys);
        Assert.Contains("workspace-analyze-images", requiredCapabilityKeys);
        var instruction = Assert.Single(storageStep.CapabilityScope.InstructionFragments);
        Assert.Equal("development-ui-screenshot-image-analysis", instruction.Key);
        Assert.Contains("software-delivery visual evidence", instruction.Content, StringComparison.Ordinal);
        Assert.Contains("Do not treat this as generic image interpretation", instruction.Content, StringComparison.Ordinal);
    }
}
