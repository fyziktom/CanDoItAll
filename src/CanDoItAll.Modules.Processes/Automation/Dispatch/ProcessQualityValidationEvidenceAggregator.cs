using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessQualityValidationEvidenceAggregator
{
    public static IReadOnlyList<string> ResolveEvidenceTexts(
        string? inspectionText,
        string? resultSummary,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> toolReceipts,
        IReadOnlyList<(string ToolName, string Text)> sessionToolResultTexts,
        Func<string, bool> isQualityValidationEvidenceToolName,
        Func<string, string> normalizeToolToken)
    {
        var texts = new List<string>();
        AddEvidenceText(texts, inspectionText);
        AddEvidenceText(texts, resultSummary);

        foreach (var receipt in toolReceipts)
        {
            var toolName = normalizeToolToken(receipt.ToolName);
            if (!isQualityValidationEvidenceToolName(toolName))
            {
                continue;
            }

            AddEvidenceText(
                texts,
                string.Join(
                    Environment.NewLine,
                    receipt.RequestSummary,
                    receipt.WorkingDirectory,
                    receipt.ExitSummary));
        }

        foreach (var resultText in sessionToolResultTexts)
        {
            if (isQualityValidationEvidenceToolName(resultText.ToolName))
            {
                AddEvidenceText(texts, resultText.Text);
            }
        }

        return texts;
    }

    private static void AddEvidenceText(List<string> texts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            texts.Add(value);
        }
    }
}

