using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetToolReceiptPolicyContribution : IProcessToolReceiptPolicyContribution
{
    private const string ToolPrefix = "workspace_dotnet_";
    private const string NewToolName = $"{ToolPrefix}new";
    private const string TemplateRequirementPrefix = "template=";

    public bool IsProductMutationReceipt(ToolExecutionReceiptRecord receipt)
        => string.Equals(receipt.ToolName, NewToolName, StringComparison.OrdinalIgnoreCase);

    public bool IsProductValidationTool(string toolName)
        => string.Equals(toolName, $"{ToolPrefix}build", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(toolName, $"{ToolPrefix}test", StringComparison.OrdinalIgnoreCase);

    public ProcessToolReceiptRequirementMatch MatchRequirement(
        ToolExecutionReceiptRecord receipt,
        string requirement)
    {
        if (!TryResolveRequiredTemplate(requirement, out var requiredTemplate))
        {
            return ProcessToolReceiptRequirementMatch.NotHandled;
        }

        if (!string.Equals(receipt.ToolName, NewToolName, StringComparison.OrdinalIgnoreCase))
        {
            return ProcessToolReceiptRequirementMatch.NotMatched;
        }

        var requestSummary = NormalizeCommandText(receipt.RequestSummary);
        var requiredTemplateText = NormalizeCommandText(requiredTemplate);
        var isMatch = requestSummary.Length > 0 &&
                      requiredTemplateText.Length > 0 &&
                      (string.Equals(requestSummary, $"new {requiredTemplateText}", StringComparison.OrdinalIgnoreCase) ||
                       requestSummary.StartsWith($"new {requiredTemplateText} ", StringComparison.OrdinalIgnoreCase) ||
                       requestSummary.Contains($" template={requiredTemplateText} ", StringComparison.OrdinalIgnoreCase) ||
                       requestSummary.Contains($" --template {requiredTemplateText} ", StringComparison.OrdinalIgnoreCase));
        return isMatch
            ? ProcessToolReceiptRequirementMatch.Matched
            : ProcessToolReceiptRequirementMatch.NotMatched;
    }

    public IEnumerable<string> EnumerateRequirementSearchTerms(string requirement)
    {
        var normalizedRequirement = NormalizeCommandText(requirement).ToLowerInvariant();
        if (TryResolveRequiredTemplate(normalizedRequirement, out var requiredTemplate))
        {
            var normalizedTemplate = NormalizeCommandText(requiredTemplate).ToLowerInvariant();
            yield return normalizedTemplate;
            yield return $"template {normalizedTemplate}";
            yield break;
        }

        if (!normalizedRequirement.StartsWith(ToolPrefix, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var verb = normalizedRequirement[ToolPrefix.Length..];
        if (!string.IsNullOrWhiteSpace(verb))
        {
            yield return verb;
            yield return $"dotnet {verb}";
        }
    }

    private static bool TryResolveRequiredTemplate(string requirement, out string requiredTemplate)
    {
        requiredTemplate = string.Empty;
        if (string.IsNullOrWhiteSpace(requirement) ||
            !requirement.StartsWith(TemplateRequirementPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        requiredTemplate = requirement[TemplateRequirementPrefix.Length..].Trim();
        return requiredTemplate.Length > 0;
    }

    private static string NormalizeCommandText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace('"', ' ')
            .Replace('\'', ' ')
            .ReplaceLineEndings(" ")
            .Trim();
        return string.Join(
            " ",
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
