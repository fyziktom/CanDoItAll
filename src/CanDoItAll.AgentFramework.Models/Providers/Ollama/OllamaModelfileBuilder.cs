using System.Text;

namespace CanDoItAll.AgentFramework.Models;

public static class OllamaModelfileBuilder
{
    public static string Build(string baseModel, string systemPrompt, int contextLength)
    {
        var normalizedBaseModel = NormalizeRequired(baseModel, "Ollama base model");
        var normalizedSystemPrompt = NormalizeRequired(systemPrompt, "Ollama system prompt");
        var normalizedContextLength = ValidateContextLength(contextLength);
        var builder = new StringBuilder();
        builder.Append("FROM ");
        builder.AppendLine(normalizedBaseModel);
        builder.Append("PARAMETER num_ctx ");
        builder.AppendLine(normalizedContextLength.ToString());
        builder.AppendLine("SYSTEM \"\"\"");
        builder.AppendLine(normalizedSystemPrompt);
        builder.Append("\"\"\"");
        return builder.ToString();
    }

    public static int ValidateContextLength(int contextLength)
    {
        if (contextLength < 2048 || contextLength > 262_144)
        {
            throw new InvalidOperationException("Ollama context length must be between 2048 and 262144.");
        }

        return contextLength;
    }

    public static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value.Trim();
    }
}
