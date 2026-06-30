using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class ProcessAllowedOperationsCapabilityPolicyCompiler
{
    public static ProcessAllowedOperationsCapabilityPolicyCompilationResult Compile(
        IReadOnlyList<string> allowedOperations,
        TemplatePath templatePath,
        string fieldPath)
    {
        ArgumentNullException.ThrowIfNull(allowedOperations);

        var issues = new List<CapabilityValidationIssue>();
        var allowedClassifications = new HashSet<CapabilityOperationClassification>();
        for (var index = 0; index < allowedOperations.Count; index++)
        {
            var operation = allowedOperations[index]?.Trim() ?? string.Empty;
            if (!ProcessOperationKey.TryCreate(operation, out _) ||
                !ProcessOperationContractNames.IsOperationName(operation))
            {
                issues.Add(new CapabilityValidationIssue(
                    CapabilityDiagnosticCategory.AccessPolicy,
                    CapabilityValidationSeverity.Error,
                    null,
                    null,
                    templatePath,
                    $"{fieldPath}[{index}]",
                    $"Process allowed operation '{operation}' is not a known operation contract.",
                    "Use a value from ProcessOperationContractNames.AllOperations."));
                continue;
            }

            foreach (var classification in ResolveClassifications(operation))
            {
                allowedClassifications.Add(classification);
            }
        }

        var rules = new List<CapabilityAccessRule>();
        foreach (var classification in allowedClassifications.OrderBy(item => item.ToString(), StringComparer.Ordinal))
        {
            rules.Add(new CapabilityAccessRule(
                CapabilityRuleId.Create($"allow-{ToKebab(classification.ToString())}"),
                CapabilityAccessEffect.Allow,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(classification),
                $"Compatibility rule produced from process allowed operations at {templatePath.Value}."));
        }

        foreach (var classification in RestrictedClassifications.Where(classification => !allowedClassifications.Contains(classification)))
        {
            rules.Add(new CapabilityAccessRule(
                CapabilityRuleId.Create($"deny-{ToKebab(classification.ToString())}"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(classification),
                $"Process allowed operations at {templatePath.Value} do not include a contract for {classification}."));
        }

        return new ProcessAllowedOperationsCapabilityPolicyCompilationResult(
            new CapabilityAccessPolicy(rules),
            new CapabilityValidationResult(issues));
    }

    public static IReadOnlyList<CapabilityOperationClassification> ResolveClassifications(string operation)
    {
        return operation switch
        {
            ProcessOperationContractNames.ReadProcessContext => [CapabilityOperationClassification.Read],
            ProcessOperationContractNames.ReadProjectStructure => [CapabilityOperationClassification.Read, CapabilityOperationClassification.ProjectStructure],
            ProcessOperationContractNames.ReadUpstreamArtifacts => [CapabilityOperationClassification.Read],
            ProcessOperationContractNames.WriteManagedProcessArtifacts => [CapabilityOperationClassification.Write],
            ProcessOperationContractNames.WriteExternalArtifactDestination => [CapabilityOperationClassification.Write, CapabilityOperationClassification.ExternalAction],
            ProcessOperationContractNames.MutateProductTarget => [CapabilityOperationClassification.Mutation, CapabilityOperationClassification.Write, CapabilityOperationClassification.ScriptExecution],
            ProcessOperationContractNames.RunValidation => [CapabilityOperationClassification.Validation, CapabilityOperationClassification.ScriptExecution],
            ProcessOperationContractNames.LaunchRuntime => [CapabilityOperationClassification.Validation, CapabilityOperationClassification.RuntimeLaunch, CapabilityOperationClassification.ResourceCleanup, CapabilityOperationClassification.ScriptExecution],
            ProcessOperationContractNames.CaptureRuntimeProof => [CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup],
            ProcessOperationContractNames.ExecuteExternalAction => [CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution],
            ProcessOperationContractNames.StartProjectNodeProcess => [CapabilityOperationClassification.ProjectStructure, CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution],
            ProcessOperationContractNames.RecoverArtifactsOnly => [CapabilityOperationClassification.Read, CapabilityOperationClassification.Write],
            ProcessOperationContractNames.EscalateOrDecide => [CapabilityOperationClassification.Read],
            _ => []
        };
    }

    private static readonly CapabilityOperationClassification[] RestrictedClassifications =
    [
        CapabilityOperationClassification.Write,
        CapabilityOperationClassification.Mutation,
        CapabilityOperationClassification.Validation,
        CapabilityOperationClassification.ScriptExecution,
        CapabilityOperationClassification.BrowserAccess,
        CapabilityOperationClassification.ProjectStructure,
        CapabilityOperationClassification.DocumentProcessing,
        CapabilityOperationClassification.ExternalAction,
        CapabilityOperationClassification.RuntimeLaunch,
        CapabilityOperationClassification.ResourceCleanup
    ];

    private static string ToKebab(string value)
    {
        return string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? "-" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));
    }
}

public sealed record ProcessAllowedOperationsCapabilityPolicyCompilationResult(
    CapabilityAccessPolicy Policy,
    CapabilityValidationResult ValidationResult);
